using System;

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

		if (rawStep >= (int)TutorialStepId.DaySummary)
			return TutorialStepId.DaySummary;

		return (TutorialStepId)rawStep;
	}

	public TutorialTransition EvaluateNextPressed(TutorialStepId step)
	{
		return step switch
		{
			TutorialStepId.Welcome => TutorialTransition.To(TutorialStepId.Status),
			TutorialStepId.Status => TutorialTransition.To(TutorialStepId.OpenBrewPanel),
			TutorialStepId.SaleResult => TutorialTransition.To(TutorialStepId.NextCustomer),
			TutorialStepId.AmbiguousCustomer => TutorialTransition.To(TutorialStepId.AddTwoMoreSleepIngredients),
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

	public TutorialTransition EvaluatePotionBrewed(TutorialStepId step, string potionItemId)
	{
		if (step == TutorialStepId.BrewPotion && IsItem(potionItemId, _tutorialPotionId))
			return TutorialTransition.To(TutorialStepId.StartDay);

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
		if (step == TutorialStepId.SellPotion && IsItem(itemId, _tutorialPotionId))
			return TutorialTransition.To(TutorialStepId.SaleResult);

		return TutorialTransition.None;
	}

	public TutorialTransition EvaluateCustomerInteractionShown(TutorialStepId step, string interactionId)
	{
		if (step == TutorialStepId.StartDay && IsCustomerInteractionMatch(interactionId, _tutorialCustomerId))
			return TutorialTransition.To(TutorialStepId.SellPotion);

		if (step == TutorialStepId.NextCustomer && IsCustomerInteractionMatch(interactionId, _ambiguousCustomerId))
			return TutorialTransition.To(TutorialStepId.AmbiguousCustomer);

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
		if (step == TutorialStepId.NextCustomer && IsCustomerInteractionMatch(activeCustomerRequestId ?? string.Empty, _ambiguousCustomerId))
			return TutorialTransition.To(TutorialStepId.AmbiguousCustomer);

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
