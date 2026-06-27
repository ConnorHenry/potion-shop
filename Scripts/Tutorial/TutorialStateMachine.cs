using System;
using OccultShop.Systems;

namespace OccultShop.Tutorial;

public sealed class TutorialStateMachine
{
	private readonly string _mintId;
	private readonly string _gorseId;
	private readonly string _thymeId;
	private readonly string _tutorialPotionId;
	private readonly string _tutorialCustomerId;
	private readonly string _ambiguousCustomerId;

	public TutorialStateMachine(TutorialContentResource content)
	{
		ArgumentNullException.ThrowIfNull(content);

		_mintId = content.MintId;
		_gorseId = content.GorseId;
		_thymeId = content.ThymeId;
		_tutorialPotionId = content.TutorialPotionId;
		_tutorialCustomerId = content.TutorialCustomerId;
		_ambiguousCustomerId = content.AmbiguousTutorialCustomerId;
	}

	public TutorialStepId ClampStep(int rawStep)
	{
		if (rawStep <= (int)TutorialStepId.Welcome)
			return TutorialStepId.Welcome;

		if (rawStep >= (int)TutorialStepId.InspectElder && rawStep <= (int)TutorialStepId.AddElderToBrew)
			return TutorialStepId.AddTwoMoreSleepIngredients;

		if (rawStep >= (int)TutorialStepId.PrepareMintRaw && rawStep <= (int)TutorialStepId.ConfirmServe)
			return (TutorialStepId)rawStep;

		if (rawStep >= (int)TutorialStepId.DaySummary && rawStep < (int)TutorialStepId.PrepareMintRaw)
			return TutorialStepId.DaySummary;

		if (rawStep >= (int)TutorialStepId.PostServeMotherDialogue)
			return TutorialStepId.PostServeMotherDialogue;

		return (TutorialStepId)rawStep;
	}

	public TutorialTransition EvaluateNextPressed(TutorialStepId step)
	{
		return step switch
		{
			TutorialStepId.Welcome => TutorialTransition.To(TutorialStepId.Status),
			TutorialStepId.Status => TutorialTransition.To(TutorialStepId.OpenBrewPanel),
			TutorialStepId.SaleResult => TutorialTransition.Complete(),
			TutorialStepId.AmbiguousCustomer => TutorialTransition.Complete(),
			_ => TutorialTransition.None
		};
	}

	public TutorialTransition EvaluateIngredientQueued(TutorialStepId step, string itemId, int queuedCount)
	{
		if (step == TutorialStepId.QueueMint && IsItem(itemId, _mintId))
			return TutorialTransition.To(TutorialStepId.QueueGorse);

		if (step == TutorialStepId.QueueGorse && IsItem(itemId, _gorseId))
			return TutorialTransition.To(TutorialStepId.QueueThyme);

		if (step == TutorialStepId.QueueThyme && IsItem(itemId, _thymeId))
			return TutorialTransition.To(TutorialStepId.BrewPotion);

		return TutorialTransition.None;
	}

	public TutorialTransition EvaluateIngredientSelected(TutorialStepId step, string itemId)
	{
		if (step == TutorialStepId.QueueMint && IsItem(itemId, _mintId))
			return TutorialTransition.To(TutorialStepId.PrepareMintRaw);

		if (step == TutorialStepId.QueueGorse && IsItem(itemId, _gorseId))
			return TutorialTransition.To(TutorialStepId.PrepareGorseRaw);

		if (step == TutorialStepId.QueueThyme && IsItem(itemId, _thymeId))
			return TutorialTransition.To(TutorialStepId.PrepareThymeRaw);

		return TutorialTransition.None;
	}

	public TutorialTransition EvaluateIngredientPrepared(TutorialStepId step, string ingredientId, string preparationId)
	{
		var normalizedPreparationId = IngredientPreparationCatalog.NormalizePreparationId(preparationId);
		if (!IsItem(normalizedPreparationId, IngredientPreparationCatalog.RawPreparationId))
			return TutorialTransition.None;

		if (step == TutorialStepId.PrepareMintRaw && IsItem(ingredientId, _mintId))
			return TutorialTransition.To(TutorialStepId.QueueGorse);

		if (step == TutorialStepId.PrepareGorseRaw && IsItem(ingredientId, _gorseId))
			return TutorialTransition.To(TutorialStepId.QueueThyme);

		if (step == TutorialStepId.PrepareThymeRaw && IsItem(ingredientId, _thymeId))
			return TutorialTransition.To(TutorialStepId.BrewPotion);

		return TutorialTransition.None;
	}

	public TutorialTransition EvaluatePotionBrewed(TutorialStepId step, string potionItemId)
	{
		if (step == TutorialStepId.BrewPotion && IsItem(potionItemId, _tutorialPotionId))
			return TutorialTransition.To(TutorialStepId.SellPotion);

		return TutorialTransition.None;
	}

	public TutorialTransition EvaluateShopStateChanged(TutorialStepId step, bool isShopOpen)
	{
		if ((step == TutorialStepId.AddTwoMoreSleepIngredients || step == TutorialStepId.CloseShop) && !isShopOpen)
			return TutorialTransition.To(TutorialStepId.DaySummary);

		return TutorialTransition.None;
	}

	public TutorialTransition EvaluateDaySummaryContinued(TutorialStepId step)
	{
		if (step == TutorialStepId.DaySummary)
			return TutorialTransition.Complete();

		return TutorialTransition.None;
	}

	public TutorialTransition EvaluatePotionSold(TutorialStepId step, string itemId)
	{
		if (step == TutorialStepId.ConfirmServe && IsItem(itemId, _tutorialPotionId))
			return TutorialTransition.To(TutorialStepId.PostServeMotherDialogue);

		return TutorialTransition.None;
	}

	public TutorialTransition EvaluateMotherPostServeDialogueResolved(TutorialStepId step)
	{
		if (step == TutorialStepId.PostServeMotherDialogue)
			return TutorialTransition.Complete();

		return TutorialTransition.None;
	}

	public TutorialTransition EvaluatePotionSelectedForServing(TutorialStepId step, string itemId)
	{
		if (step == TutorialStepId.SellPotion && IsItem(itemId, _tutorialPotionId))
			return TutorialTransition.To(TutorialStepId.ConfirmServe);

		return TutorialTransition.None;
	}

	public TutorialTransition EvaluateCustomerInteractionShown(TutorialStepId step, string interactionId)
	{
		if (step == TutorialStepId.StartDay && IsCustomerInteractionMatch(interactionId, _tutorialCustomerId))
			return TutorialTransition.To(TutorialStepId.SellPotion);

		return TutorialTransition.None;
	}

	public TutorialTransition EvaluateOpenBrewPanelState(TutorialStepId step, bool isBrewPanelVisible)
	{
		if (step == TutorialStepId.OpenBrewPanel && isBrewPanelVisible)
			return TutorialTransition.To(TutorialStepId.QueueMint);

		return TutorialTransition.None;
	}

	public TutorialTransition EvaluateAmbiguousCustomerState(TutorialStepId step, string? activeCustomerRequestId)
	{
		return TutorialTransition.None;
	}

	private static bool IsItem(string actualItemId, string expectedItemId)
	{
		return string.Equals(actualItemId, expectedItemId, StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsCustomerInteractionMatch(string actualInteractionId, string expectedInteractionId)
	{
		if (IsItem(actualInteractionId, expectedInteractionId))
			return true;

		var normalizedExpectedId = NormalizeLegacyCustomerRequestId(expectedInteractionId);
		return !string.IsNullOrWhiteSpace(normalizedExpectedId) &&
			actualInteractionId.EndsWith("_" + normalizedExpectedId, StringComparison.OrdinalIgnoreCase);
	}

	private static string NormalizeLegacyCustomerRequestId(string interactionId)
	{
		const string legacyPrefix = "customer_requests_";
		return interactionId.StartsWith(legacyPrefix, StringComparison.OrdinalIgnoreCase)
			? interactionId[legacyPrefix.Length..]
			: interactionId;
	}
}
