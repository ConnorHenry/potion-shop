using System;
using System.Collections.Generic;
using System.Linq;
using OccultShop.Autoload;
using OccultShop.Models;

namespace OccultShop.Systems;

public sealed class CustomerSaleService
{
	private const int SuccessDreadChange = -2;
	private const int FailureDreadChange = 4;
	private const int SuccessReputationChange = 2;
	private const int FailureReputationChange = -3;
	private const int RefusalReputationChange = -1;
	private const int StoryCustomerSuccessRelationshipChange = 5;
	private const int StoryCustomerFailureRelationshipChange = -5;
	private const int StoryCustomerRefusalRelationshipChange = -2;

	private readonly GameState _gameState;
	private readonly ItemCatalogService _itemCatalog;
	private readonly PotionBrewingService _brewingService = new();

	public CustomerSaleService(GameState gameState, ItemCatalogService itemCatalog)
	{
		_gameState = gameState;
		_itemCatalog = itemCatalog;
	}

	public CustomerSaleResolutionResult ResolveSale(
		CustomerInteractionDef interaction,
		string itemId,
		PotionResult brewResult)
	{
		var outcomeText = BuildOutcomeText(interaction, itemId, brewResult);
		var saleResult = ApplySale(interaction, itemId, brewResult);
		return new CustomerSaleResolutionResult(outcomeText, saleResult);
	}

	public string ResolveRefusal(CustomerInteractionDef interaction)
	{
		ApplyRefusal(interaction);
		return BuildRefusalText(interaction);
	}

	public bool TryEvaluatePotion(
		CustomerInteractionDef interaction,
		string itemId,
		out PotionResult? brewResult)
	{
		brewResult = null;

		if (interaction is null)
			return false;
		if (!_itemCatalog.TryGetItem(itemId, out var item))
			return false;
		if (!_itemCatalog.IsPotion(itemId))
			return false;

		brewResult = _brewingService.EvaluatePotionItem(item, interaction.BuildRequest());
		return true;
	}

	public bool IsRequestSatisfiedByPotion(
		string potionItemId,
		CustomerRequestDef request,
		PotionResult brewResult)
	{
		return CustomerSaleRules.IsRequestSatisfiedByPotion(
			potionItemId,
			request,
			brewResult,
			DoesPotionBatchSatisfyIngredientAmountRequirements(potionItemId, request.RequiredIngredientAmounts));
	}

	public CustomerSaleApplicationResult ApplySale(
		CustomerInteractionDef interaction,
		string itemId,
		PotionResult brewResult)
	{
		var request = interaction.BuildRequest();
		var isSuccess = IsRequestSatisfiedByPotion(itemId, request, brewResult);
		var goldDelta = GetSalePrice(itemId);
		var dreadDelta = isSuccess ? SuccessDreadChange : FailureDreadChange;

		_gameState.AddGold(goldDelta);
		_gameState.AddDread(dreadDelta);
		_gameState.ConsumeItem(itemId, 1);

		ApplyAutomaticSaleOutcome(interaction, isSuccess);
		ApplyOutcomeEffects(isSuccess ? interaction.OnSuccessEffects : interaction.OnFailureEffects);
		var response = FindPotionResponse(interaction, itemId, request, brewResult, isSuccess);
		ApplyOutcomeEffects(response?.Effects);

		var outcome = isSuccess
			? GameState.StoryCustomerOutcomeSuccess
			: GameState.StoryCustomerOutcomeFailure;
		_gameState.RecordStoryCustomerInteractionOutcome(interaction, outcome);

		return new CustomerSaleApplicationResult(isSuccess, goldDelta, dreadDelta);
	}

	public void ApplyRefusal(CustomerInteractionDef interaction)
	{
		ApplyAutomaticRefusalOutcome(interaction);
		var effects = interaction.OnPotionRefusedEffects.Count > 0
			? interaction.OnPotionRefusedEffects
			: interaction.OnSkipEffects;
		ApplyOutcomeEffects(effects);
		_gameState.RecordStoryCustomerInteractionOutcome(interaction, "refused");
	}

	public string BuildOutcomeText(
		CustomerInteractionDef interaction,
		string itemId,
		PotionResult brewResult)
	{
		var lines = new List<string>();
		var itemName = DisplayName(itemId, _itemCatalog.GetItemName(itemId));
		lines.Add($"Potion: {itemName}");

		var request = interaction.BuildRequest();
		var isSuccess = IsRequestSatisfiedByPotion(itemId, request, brewResult);
		lines.Add($"Sale: {(isSuccess ? "Success" : "Failure")}");
		lines.Add(string.Empty);

		var authoredResponse = FindPotionResponse(interaction, itemId, request, brewResult, isSuccess);
		if (authoredResponse is not null)
		{
			if (authoredResponse.Lines.Count > 0)
			{
				foreach (var line in authoredResponse.Lines)
				{
					if (!string.IsNullOrWhiteSpace(line.Text))
						lines.Add(FormatPlainAuthoredLine(line));
				}
			}
			else if (!string.IsNullOrWhiteSpace(authoredResponse.Text))
			{
				lines.Add(authoredResponse.Text);
			}

			return string.Join("\n", lines);
		}

		var matchedDesiredTraitCount = CustomerSaleRules.CountMatchedDesiredTraits(request, brewResult.Traits);
		lines.Add($"Customer response: {GetCustomerResponseText(matchedDesiredTraitCount)}");
		return string.Join("\n", lines);
	}

	public string BuildRefusalText(CustomerInteractionDef interaction)
	{
		var lines = new List<string>();
		if (interaction.PotionRefusedLines.Count > 0)
		{
			foreach (var line in interaction.PotionRefusedLines)
			{
				if (!string.IsNullOrWhiteSpace(line.Text))
					lines.Add(FormatPlainAuthoredLine(line));
			}
		}
		else if (!string.IsNullOrWhiteSpace(interaction.PotionRefusedText))
		{
			lines.Add(interaction.PotionRefusedText);
		}
		else
		{
			lines.Add("The customer leaves without a potion.");
		}

		return string.Join("\n", lines);
	}

	public IReadOnlyList<IngredientPortionDef>? GetPotionIngredientPortions(string potionItemId)
	{
		return _gameState.TryPeekPotionIngredientPortionBatch(potionItemId, out var portions)
			? portions
			: null;
	}

	private bool DoesPotionBatchSatisfyIngredientAmountRequirements(
		string potionItemId,
		IReadOnlyList<IngredientPortionDef>? requiredIngredientAmounts)
	{
		if (requiredIngredientAmounts is null || requiredIngredientAmounts.Count == 0)
			return true;

		if (!_gameState.TryPeekPotionIngredientPortionBatch(potionItemId, out var potionBatch))
			return false;

		foreach (var requiredIngredientAmount in requiredIngredientAmounts)
		{
			if (requiredIngredientAmount is null || string.IsNullOrWhiteSpace(requiredIngredientAmount.IngredientId))
				continue;
			if (requiredIngredientAmount.Grams <= 0 && string.IsNullOrWhiteSpace(requiredIngredientAmount.PreparationId))
				continue;

			var hasMatchingPortion = potionBatch.Any(portion =>
				string.Equals(portion.IngredientId, requiredIngredientAmount.IngredientId, StringComparison.OrdinalIgnoreCase) &&
				(requiredIngredientAmount.Grams <= 0 || portion.Grams == requiredIngredientAmount.Grams) &&
				(string.IsNullOrWhiteSpace(requiredIngredientAmount.PreparationId) ||
					string.Equals(
						IngredientPreparationCatalog.NormalizePreparationId(portion.PreparationId),
						IngredientPreparationCatalog.NormalizePreparationId(requiredIngredientAmount.PreparationId),
						StringComparison.OrdinalIgnoreCase)));
			if (!hasMatchingPortion)
				return false;
		}

		return true;
	}

	private CustomerPotionResponseDef? FindPotionResponse(
		CustomerInteractionDef interaction,
		string itemId,
		CustomerRequestDef request,
		PotionResult brewResult,
		bool isSuccess)
	{
		if (interaction.PotionResponses.Count == 0)
			return null;

		foreach (var response in interaction.PotionResponses)
		{
			if (!CustomerSaleRules.PotionResponseMatches(response, itemId, request, brewResult, isSuccess))
				continue;

			return response;
		}

		return null;
	}

	private void ApplyOutcomeEffects(IReadOnlyList<EffectDef>? effects)
	{
		if (effects is null || effects.Count == 0)
			return;

		foreach (var effect in effects)
			EffectApplier.Apply(_gameState, effect);
	}

	private void ApplyAutomaticSaleOutcome(CustomerInteractionDef interaction, bool isSuccess)
	{
		_gameState.AddReputation(isSuccess ? SuccessReputationChange : FailureReputationChange);
		if (string.IsNullOrWhiteSpace(interaction.StoryCharacterId))
			return;

		_gameState.AddRelationship(
			interaction.StoryCharacterId,
			isSuccess ? StoryCustomerSuccessRelationshipChange : StoryCustomerFailureRelationshipChange);
	}

	private void ApplyAutomaticRefusalOutcome(CustomerInteractionDef interaction)
	{
		_gameState.AddReputation(RefusalReputationChange);
		if (string.IsNullOrWhiteSpace(interaction.StoryCharacterId))
			return;

		_gameState.AddRelationship(interaction.StoryCharacterId, StoryCustomerRefusalRelationshipChange);
	}

	private int GetSalePrice(string itemId)
	{
		if (_gameState.TryGetPotionBasePrice(itemId, out var potionBasePrice))
			return Math.Max(0, potionBasePrice);

		if (_itemCatalog.TryGetItem(itemId, out var item))
			return Math.Max(0, item.BasePrice);

		return 0;
	}

	private string DisplayName(string itemId, string fallbackName)
	{
		if (!_itemCatalog.IsPotion(itemId))
			return fallbackName;

		var customName = _gameState.GetPotionDisplayName(itemId);
		return string.IsNullOrWhiteSpace(customName) ? fallbackName : customName;
	}

	private static string FormatPlainAuthoredLine(CustomerDialogueLineDef line)
	{
		if (string.IsNullOrWhiteSpace(line.Speaker))
			return line.Text;

		return $"{line.Speaker}: {line.Text}";
	}

	private static string GetCustomerResponseText(int matchedDesiredTraitCount)
	{
		if (matchedDesiredTraitCount >= 3)
			return "The customer is happy";

		if (matchedDesiredTraitCount == 2)
			return "The customer is satisfied";

		return "The customer is disappointed";
	}
}

public readonly record struct CustomerSaleResolutionResult(
	string OutcomeText,
	CustomerSaleApplicationResult SaleResult);

public readonly record struct CustomerSaleApplicationResult(
	bool IsSuccess,
	int GoldDelta,
	int DreadDelta);
