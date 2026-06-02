using Godot;

namespace OccultShop.Tutorial;

[GlobalClass]
public partial class TutorialContentResource : Resource
{
	[Export]
	public string GraveMintId { get; set; } = "grave_mint";

	[Export]
	public string ObsidianResinId { get; set; } = "obsidian_resin";

	[Export]
	public string IronLullabyRootId { get; set; } = "iron_lullaby_root";

	[Export]
	public string BlackIchorId { get; set; } = "black_ichor";

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
				Title = "Gold, Dread, and Day",
				Body = "Gold pays for brewing. Dread tracks how dangerous the shop has become. Day shows your current run progress.",
				ShowNextButton = true,
				PanelAtTop = false
			},
			new()
			{
				StepId = (int)TutorialStepId.OpenBrewPanel,
				Title = "Open the Brew Panel",
				Body = "Click Brew Potion to open the cauldron controls.",
				ShowNextButton = false,
				PanelAtTop = false,
				LockOtherButtons = true
			},
			new()
			{
				StepId = (int)TutorialStepId.QueueGraveMint,
				Title = "Add Grave Mint",
				Body = "Add Grave Mint to the brew. You can right-click an ingredient slot or drag it out if you add the wrong item.",
				ShowNextButton = false,
				PanelAtTop = true,
				LockOtherButtons = true
			},
			new()
			{
				StepId = (int)TutorialStepId.QueueObsidianResin,
				Title = "Add Obsidian Resin",
				Body = "Add Obsidian Resin as the second ingredient.",
				ShowNextButton = false,
				PanelAtTop = true,
				LockOtherButtons = true
			},
			new()
			{
				StepId = (int)TutorialStepId.QueueIronLullabyRoot,
				Title = "Add Iron Lullaby Root",
				Body = "Add Iron Lullaby Root as the third ingredient. The preview should show Gravekeeper's Balm.",
				ShowNextButton = false,
				PanelAtTop = true,
				LockOtherButtons = true
			},
			new()
			{
				StepId = (int)TutorialStepId.BrewPotion,
				Title = "Brew Gravekeeper's Balm",
				Body = "Click Brew Potion in the brew panel to create Gravekeeper's Balm. It will appear in your inventory.",
				ShowNextButton = false,
				PanelAtTop = true,
				LockOtherButtons = true
			},
			new()
			{
				StepId = (int)TutorialStepId.StartDay,
				Title = "Open the Shop",
				Body = "Click Start Day. The tutorial will send in a customer who wants Gravekeeper's Balm.",
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
				StepId = (int)TutorialStepId.InspectBlackIchor,
				Title = "Inspect Black Ichor",
				Body = "Left-click Black Ichor in the inventory to view its details.",
				ShowNextButton = false,
				PanelAtTop = false,
				LockOtherButtons = true
			},
			new()
			{
				StepId = (int)TutorialStepId.BlackIchorRestTrait,
				Title = "Rest Helps Sleepless Customers",
				Body = "Black Ichor has a strong Rest trait. That would probably suit a customer who cannot sleep.",
				ShowNextButton = true,
				PanelAtTop = false
			},
			new()
			{
				StepId = (int)TutorialStepId.AddBlackIchorToBrew,
				Title = "Add Black Ichor",
				Body = "Click Add to Brew to use Black Ichor as the first ingredient for this customer's potion.",
				ShowNextButton = false,
				PanelAtTop = false,
				LockOtherButtons = true
			},
			new()
			{
				StepId = (int)TutorialStepId.AddTwoMoreSleepIngredients,
				Title = "Choose Two More Ingredients",
				Body = "Add two more ingredients that may suit the customer's need for rest, calm, or dreams.",
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
			}
		};
	}
}
