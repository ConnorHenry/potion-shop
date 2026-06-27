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
	public string TutorialCustomerId { get; set; } = "customer_requests_opening_gravekeepers_balm";

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
				Body = "This tutorial walks through your stock, brewing your first potion, Minor Healing Potion, and selling it to your first customer.",
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
				Body = "Use the cauldron controls in the brewing station.",
				ShowNextButton = false,
				PanelAtTop = false,
				LockOtherButtons = true
			},
			new()
			{
				StepId = (int)TutorialStepId.QueueMint,
				Title = "Choose Mint",
				Body = "Right click or drag an ingredient to the preparation tray.",
				ShowNextButton = false,
				PanelAtTop = true,
				LockOtherButtons = true
			},
			new()
			{
				StepId = (int)TutorialStepId.PrepareMintRaw,
				Title = "Prepare Mint",
				Body = "Ingredients can be prepared in different ways. For this potion, choose Raw.",
				ShowNextButton = false,
				PanelAtTop = true,
				LockOtherButtons = true
			},
			new()
			{
				StepId = (int)TutorialStepId.QueueGorse,
				Title = "Choose Gorse",
				Body = "Right click or drag Gorse to the preparation tray.",
				ShowNextButton = false,
				PanelAtTop = true,
				LockOtherButtons = true
			},
			new()
			{
				StepId = (int)TutorialStepId.PrepareGorseRaw,
				Title = "Prepare Gorse",
				Body = "Keep this ingredient Raw as well.",
				ShowNextButton = false,
				PanelAtTop = true,
				LockOtherButtons = true
			},
			new()
			{
				StepId = (int)TutorialStepId.QueueThyme,
				Title = "Choose Thyme",
				Body = "Right click or drag Thyme to the preparation tray.",
				ShowNextButton = false,
				PanelAtTop = true,
				LockOtherButtons = true
			},
			new()
			{
				StepId = (int)TutorialStepId.PrepareThymeRaw,
				Title = "Prepare Thyme",
				Body = "Choose Raw one more time.",
				ShowNextButton = false,
				PanelAtTop = true,
				LockOtherButtons = true
			},
			new()
			{
				StepId = (int)TutorialStepId.BrewPotion,
				Title = "Brew Minor Healing Potion",
				Body = "Click Brew to create Minor Healing Potion. It will appear in your inventory.",
				ShowNextButton = false,
				PanelAtTop = true,
				LockOtherButtons = true
			},
			new()
			{
				StepId = (int)TutorialStepId.StartDay,
				Title = "Open the Shop",
				Body = "Click Start Day to bring the first customer to the station.",
				ShowNextButton = false,
				PanelAtTop = false,
				LockOtherButtons = true
			},
			new()
			{
				StepId = (int)TutorialStepId.SellPotion,
				Title = "Bring It to Mother",
				Body = "Drag Minor Healing Potion from your inventory to the Serving Slot.",
				ShowNextButton = false,
				PanelAtTop = false,
				LockOtherButtons = true
			},
			new()
			{
				StepId = (int)TutorialStepId.ConfirmServe,
				Title = "Serve the Potion",
				Body = "Click Serve to give the Minor Healing Potion to Mother.",
				ShowNextButton = false,
				PanelAtTop = false,
				LockOtherButtons = true
			},
			new()
			{
				StepId = (int)TutorialStepId.PostServeMotherDialogue,
				Title = "Talk With Mother",
				Body = "Answer Mother in the customer dialog box.",
				ShowNextButton = false,
				PanelAtTop = false,
				DimBackground = false,
				LockOtherButtons = false
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
				Body = "The next customer is coming to the counter. Read their request before choosing ingredients.",
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
				Body = "Close the shop to end the day.",
				ShowNextButton = false,
				PanelAtTop = true,
				LockOtherButtons = false
			},
			new()
			{
				StepId = (int)TutorialStepId.DaySummary,
				Title = "End of Day Summary",
				Body = "This is the end of day summary. It shows how your day has gone. Click Continue to start the next day.",
				ShowNextButton = false,
				PanelAtTop = false,
				LockOtherButtons = true
			}
		};
	}
}
