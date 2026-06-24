using System;
using OccultShop.Autoload;
using OccultShop.Controllers;
using OccultShop.Models;

namespace OccultShop.Systems;

public readonly record struct ShopDayFastForwardResult(
	int OriginalDay,
	int TargetDay,
	int DaysAdvanced,
	int ScheduledStoryCustomersResolved,
	int SharedOutcomeEffectsApplied)
{
	public bool Applied => DaysAdvanced > 0;
}

public static class ShopDayFastForwardService
{
	private const string DebugFastForwardOutcome = "debug_fast_forward";

	public static ShopDayFastForwardResult FastForwardToDay(
		DataDb dataDb,
		GameState gameState,
		CustomerEventController customerEventController,
		int targetDay,
		int maxCustomersPerShopDay)
	{
		if (dataDb is null)
			throw new ArgumentNullException(nameof(dataDb));
		if (gameState is null)
			throw new ArgumentNullException(nameof(gameState));
		if (customerEventController is null)
			throw new ArgumentNullException(nameof(customerEventController));

		var originalDay = gameState.Day;
		var safeTargetDay = Math.Max(1, targetDay);
		var safeMaxCustomersPerShopDay = Math.Max(0, maxCustomersPerShopDay);
		if (safeTargetDay <= originalDay)
			return new ShopDayFastForwardResult(originalDay, safeTargetDay, 0, 0, 0);

		var scheduledStoryCustomersResolved = 0;
		var sharedOutcomeEffectsApplied = 0;

		while (gameState.Day < safeTargetDay)
		{
			ResolveCurrentShopDay(
				dataDb,
				gameState,
				customerEventController,
				safeMaxCustomersPerShopDay,
				ref scheduledStoryCustomersResolved,
				ref sharedOutcomeEffectsApplied);
			gameState.NextDay();
		}

		return new ShopDayFastForwardResult(
			originalDay,
			safeTargetDay,
			gameState.Day - originalDay,
			scheduledStoryCustomersResolved,
			sharedOutcomeEffectsApplied);
	}

	private static void ResolveCurrentShopDay(
		DataDb dataDb,
		GameState gameState,
		CustomerEventController customerEventController,
		int maxCustomersPerShopDay,
		ref int scheduledStoryCustomersResolved,
		ref int sharedOutcomeEffectsApplied)
	{
		var wasShopOpen = gameState.IsShopDayOpen;
		customerEventController.BeginShopDay();
		if (!wasShopOpen)
			gameState.BeginShopDayState();

		if (TryResolveActiveStoryCustomer(
			dataDb,
			gameState,
			ref scheduledStoryCustomersResolved,
			ref sharedOutcomeEffectsApplied))
		{
			gameState.ClearActiveShopCustomer();
		}

		while (gameState.ShopDayCustomersArrived < maxCustomersPerShopDay)
		{
			var interaction = customerEventController.DrawScheduledStoryCustomerInteraction(dataDb, gameState);
			if (interaction is null)
				break;

			gameState.RecordShopDayCustomerArrived(interaction);
			ResolveStoryCustomerForFastForward(
				gameState,
				interaction,
				ref scheduledStoryCustomersResolved,
				ref sharedOutcomeEffectsApplied);
		}

		gameState.CloseShopDayState();
	}

	private static bool TryResolveActiveStoryCustomer(
		DataDb dataDb,
		GameState gameState,
		ref int scheduledStoryCustomersResolved,
		ref int sharedOutcomeEffectsApplied)
	{
		var interactionId = gameState.ActiveCustomerInteractionId;
		if (string.IsNullOrWhiteSpace(interactionId))
			interactionId = gameState.ActiveCustomerRequest?.Id ?? string.Empty;
		if (string.IsNullOrWhiteSpace(interactionId))
			return false;

		if (!TryFindInteraction(dataDb, interactionId, out var interaction) || interaction is null)
			return false;
		if (!interaction.IsStoryInteraction)
			return false;

		ResolveStoryCustomerForFastForward(
			gameState,
			interaction,
			ref scheduledStoryCustomersResolved,
			ref sharedOutcomeEffectsApplied);
		return true;
	}

	private static bool TryFindInteraction(
		DataDb dataDb,
		string interactionId,
		out CustomerInteractionDef? interaction)
	{
		interaction = null;
		if (string.IsNullOrWhiteSpace(interactionId))
			return false;

		foreach (var candidate in dataDb.CustomerInteractions)
		{
			if (!string.Equals(candidate.Id, interactionId.Trim(), StringComparison.OrdinalIgnoreCase))
				continue;

			interaction = candidate;
			return true;
		}

		return false;
	}

	private static void ResolveStoryCustomerForFastForward(
		GameState gameState,
		CustomerInteractionDef interaction,
		ref int scheduledStoryCustomersResolved,
		ref int sharedOutcomeEffectsApplied)
	{
		sharedOutcomeEffectsApplied += ApplySharedOutcomeEffects(gameState, interaction);
		gameState.RecordStoryCustomerInteractionOutcome(interaction, DebugFastForwardOutcome);
		gameState.RecordShopDaySale(success: true, goldDelta: 0, dreadDelta: 0);
		gameState.ClearActiveShopCustomer();
		scheduledStoryCustomersResolved += 1;
	}

	private static int ApplySharedOutcomeEffects(GameState gameState, CustomerInteractionDef interaction)
	{
		if (interaction.OnSuccessEffects.Count == 0 || interaction.OnFailureEffects.Count == 0)
			return 0;

		var appliedCount = 0;
		var matchedFailureEffects = new bool[interaction.OnFailureEffects.Count];
		foreach (var successEffect in interaction.OnSuccessEffects)
		{
			for (var index = 0; index < interaction.OnFailureEffects.Count; index += 1)
			{
				if (matchedFailureEffects[index])
					continue;

				var failureEffect = interaction.OnFailureEffects[index];
				if (!EffectsMatch(successEffect, failureEffect))
					continue;

				EffectApplier.Apply(gameState, successEffect);
				matchedFailureEffects[index] = true;
				appliedCount += 1;
				break;
			}
		}

		return appliedCount;
	}

	private static bool EffectsMatch(EffectDef left, EffectDef right)
	{
		return left.AddGold == right.AddGold &&
			left.AddDread == right.AddDread &&
			StringValuesMatch(left.AddRule, right.AddRule) &&
			StringValuesMatch(left.AddStoryFlag, right.AddStoryFlag) &&
			StringValuesMatch(left.RemoveStoryFlag, right.RemoveStoryFlag) &&
			StringValuesMatch(left.AddItemId, right.AddItemId) &&
			left.AddItemQty == right.AddItemQty &&
			StringValuesMatch(left.RestockItemId, right.RestockItemId) &&
			left.RestockItemQty == right.RestockItemQty &&
			StringValuesMatch(left.EnableIngredientPreparationMethodId, right.EnableIngredientPreparationMethodId) &&
			StringValuesMatch(left.ConsumeItemId, right.ConsumeItemId) &&
			left.ConsumeItemQty == right.ConsumeItemQty &&
			left.ConsumeEachIngredientQty == right.ConsumeEachIngredientQty;
	}

	private static bool StringValuesMatch(string? left, string? right)
	{
		var normalizedLeft = string.IsNullOrWhiteSpace(left) ? string.Empty : left.Trim();
		var normalizedRight = string.IsNullOrWhiteSpace(right) ? string.Empty : right.Trim();
		return string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
	}
}
