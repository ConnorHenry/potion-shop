using System;

namespace OccultShop.Tutorial;

public sealed class TutorialStateMachine
{
	private readonly string _graveMintId;
	private readonly string _obsidianResinId;
	private readonly string _ironLullabyRootId;
	private readonly string _blackIchorId;
	private readonly string _tutorialPotionId;
	private readonly string _tutorialCustomerId;
	private readonly string _ambiguousCustomerId;

	public TutorialStateMachine(TutorialContentResource content)
	{
		ArgumentNullException.ThrowIfNull(content);

		_graveMintId = content.GraveMintId;
		_obsidianResinId = content.ObsidianResinId;
		_ironLullabyRootId = content.IronLullabyRootId;
		_blackIchorId = content.BlackIchorId;
		_tutorialPotionId = content.TutorialPotionId;
		_tutorialCustomerId = content.TutorialCustomerId;
		_ambiguousCustomerId = content.AmbiguousTutorialCustomerId;
	}

	public TutorialStepId ClampStep(int rawStep)
	{
		if (rawStep <= (int)TutorialStepId.Welcome)
			return TutorialStepId.Welcome;

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
			TutorialStepId.AmbiguousCustomer => TutorialTransition.To(TutorialStepId.InspectBlackIchor),
			TutorialStepId.BlackIchorRestTrait => TutorialTransition.To(TutorialStepId.AddBlackIchorToBrew),
			_ => TutorialTransition.None
		};
	}

	public TutorialTransition EvaluateIngredientQueued(TutorialStepId step, string itemId, int queuedCount)
	{
		if (step == TutorialStepId.QueueGraveMint && IsItem(itemId, _graveMintId))
			return TutorialTransition.To(TutorialStepId.QueueObsidianResin);

		if (step == TutorialStepId.QueueObsidianResin && IsItem(itemId, _obsidianResinId))
			return TutorialTransition.To(TutorialStepId.QueueIronLullabyRoot);

		if (step == TutorialStepId.QueueIronLullabyRoot && IsItem(itemId, _ironLullabyRootId))
			return TutorialTransition.To(TutorialStepId.BrewPotion);

		if (step == TutorialStepId.AddBlackIchorToBrew && IsItem(itemId, _blackIchorId))
			return TutorialTransition.To(TutorialStepId.AddTwoMoreSleepIngredients);

		return TutorialTransition.None;
	}

	public TutorialTransition EvaluateItemDetailShown(TutorialStepId step, string itemId)
	{
		if (step == TutorialStepId.InspectBlackIchor && IsItem(itemId, _blackIchorId))
			return TutorialTransition.To(TutorialStepId.BlackIchorRestTrait);

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
		if (step == TutorialStepId.CloseShop && !isShopOpen)
			return TutorialTransition.To(TutorialStepId.DaySummary);

		return TutorialTransition.None;
	}

	public TutorialTransition EvaluateDaySummaryContinued(TutorialStepId step)
	{
		if (step == TutorialStepId.DaySummary)
			return TutorialTransition.Complete();

		return TutorialTransition.None;
	}

	public TutorialTransition EvaluateCloseShopPrompt(TutorialStepId step, bool hasSoldPotion, bool isCloseShopMode)
	{
		if (step == TutorialStepId.AddTwoMoreSleepIngredients && hasSoldPotion && isCloseShopMode)
			return TutorialTransition.To(TutorialStepId.CloseShop);

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
			return TutorialTransition.To(TutorialStepId.QueueGraveMint);

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
