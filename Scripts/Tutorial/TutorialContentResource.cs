using Godot;

namespace OccultShop.Tutorial;

[GlobalClass]
public partial class TutorialContentResource : Resource
{
	[Export]
	public string MintId { get; set; } = "mint";

	[Export]
	public string GorseId { get; set; } = "gorse";

	[Export]
	public string ThymeId { get; set; } = "thyme";

	[Export]
	public string ElderId { get; set; } = "elder";

	[Export]
	public string TutorialPotionId { get; set; } = "potion_gravekeepers_balm";

	[Export]
	public string TutorialCustomerId { get; set; } = "customer_requests_gravekeepers_balm";

	[Export]
	public string AmbiguousTutorialCustomerId { get; set; } = "customer_requests_sleep_draught";

	[Export(PropertyHint.MultilineText)]
	public string SaleResultSuccessBody { get; set; } =
		"You used the ingredients the customer wanted. This means they are happy with their purchase.";

	[Export(PropertyHint.MultilineText)]
	public string SaleResultFallbackBody { get; set; } =
		"You need to read the customer request more carefully next time.";

	public string BuildSaleResultBody(bool saleSucceeded)
	{
		return saleSucceeded ? SaleResultSuccessBody : SaleResultFallbackBody;
	}

	[Export]
	public Godot.Collections.Array<TutorialStepContentResource> Steps { get; set; } = BuildDefaultSteps();

	public TutorialStepContentResource GetStepContent(TutorialStepId step)
	{
		var stepId = (int)step;
		foreach (var content in Steps)
		{
			if (content is null || content.StepId != stepId)
				continue;

			return content;
		}

		return BuildMissingStep(step);
	}

	private static TutorialStepContentResource BuildMissingStep(TutorialStepId step)
	{
		return new TutorialStepContentResource
		{
			StepId = (int)step,
			Title = "Tutorial",
			Body = "Tutorial step content is missing.",
			ShowNextButton = false,
			NextButtonText = "Next",
			PanelAtTop = false,
			DimBackground = true,
			LockOtherButtons = false
		};
	}

	private static Godot.Collections.Array<TutorialStepContentResource> BuildDefaultSteps()
	{
		return new Godot.Collections.Array<TutorialStepContentResource>
		{
			new()
			{
				StepId = (int)TutorialStepId.Welcome,
				Title = "Welcome to the Shop",
				Body = "This tutorial walks through your stock, brewing your first potion, Gravekeeper's Balm, and selling it to your first customer.",
				ShowNextButton = true,
				PanelAtTop = false
			},
			new()
			{
				StepId = (int)TutorialStepId.Status,
				Title = "Gold and Day",
				Body = "Gold pays for brewing. Day shows your current run progress.",
				ShowNextButton = true,
				PanelAtTop = false
			},
			new()
			{
				StepId = (int)TutorialStepId.OpenBrewPanel,
				Title = "Open the Brew Panel",
				Body = "Click the brewing station shelf to open the cauldron controls.",
				ShowNextButton = false,
				PanelAtTop = false,
				LockOtherButtons = true
			},
			new()
			{
				StepId = (int)TutorialStepId.QueueMint,
				Title = "Add Mint",
				Body = "Add Mint to the brew. You can right-click an ingredient slot or drag it out if you add the wrong item.",
				ShowNextButton = false,
				PanelAtTop = true,
				LockOtherButtons = true
			},
			new()
			{
				StepId = (int)TutorialStepId.QueueGorse,
				Title = "Add Gorse",
				Body = "Add Gorse as the second ingredient.",
				ShowNextButton = false,
				PanelAtTop = true,
				LockOtherButtons = true
			},
			new()
			{
				StepId = (int)TutorialStepId.QueueThyme,
				Title = "Add Thyme",
				Body = "Add Thyme as the third ingredient. The preview should show Gravekeeper's Balm.",
				ShowNextButton = false,
				PanelAtTop = true,
				LockOtherButtons = true
			},
			new()
			{
				StepId = (int)TutorialStepId.BrewPotion,
				Title = "Brew Gravekeeper's Balm",
				Body = "Click Brew in the brew panel to create Gravekeeper's Balm. It will appear in your inventory.",
				ShowNextButton = false,
				PanelAtTop = true,
				LockOtherButtons = true
			},
			new()
			{
				StepId = (int)TutorialStepId.StartDay,
				Title = "Open the Shop",
				Body = "Click Start Day, then click the customer in the shop front to read their request.",
				ShowNextButton = false,
				PanelAtTop = false,
				LockOtherButtons = true
			},
			new()
			{
				StepId = (int)TutorialStepId.SellPotion,
				Title = "Sell the Potion",
				Body = "Drag Gravekeeper's Balm from your inventory to the customer's Drop potion here box.",
				ShowNextButton = false,
				PanelAtTop = false,
				LockOtherButtons = true
			},
			new()
			{
				StepId = (int)TutorialStepId.SaleResult,
				Title = "Sale Review",
				Body = "Review the sale result and read the customer request carefully next time.",
				ShowNextButton = true,
				NextButtonText = "Continue",
				PanelAtTop = false
			},
			new()
			{
				StepId = (int)TutorialStepId.NextCustomer,
				Title = "Let in the Next Customer",
				Body = "Click Next customer to bring in the next request.",
				ShowNextButton = false,
				PanelAtTop = false,
				LockOtherButtons = true
			},
			new()
			{
				StepId = (int)TutorialStepId.AmbiguousCustomer,
				Title = "Read the Request Carefully",
				Body = "This customer cannot sleep, but they are not asking for a potion by name. Read the customer request carefully and choose ingredients whose traits best match what they need.",
				ShowNextButton = true,
				PanelAtTop = false
			},
			new()
			{
				StepId = (int)TutorialStepId.AddTwoMoreSleepIngredients,
				Title = "Choose Two More Ingredients",
				Body = "Add two more ingredients that may suit the customer's need for rest, calm, or dreams. Brew the potion and serve the customer.",
				ShowNextButton = false,
				PanelAtTop = true,
				DimBackground = false
			},
			new()
			{
				StepId = (int)TutorialStepId.CloseShop,
				Title = "Close the Shop",
				Body = "It is night time. Close the shop to end the day.",
				ShowNextButton = false,
				PanelAtTop = true,
				LockOtherButtons = true
			},
			new()
			{
				StepId = (int)TutorialStepId.DaySummary,
				Title = "End of Day Summary",
				Body = "This is the end of day summary. This will show you how your day has gone. Click on the Continue to Night button.",
				ShowNextButton = false,
				PanelAtTop = false,
				LockOtherButtons = true
			}
		};
	}
}
