using System;
using System.Collections.Generic;
using Godot;
using OccultShop.Autoload;
using OccultShop.UI;

namespace OccultShop.Controllers;

public partial class TutorialController : Node
{
	private const string GraveMintId = "grave_mint";
	private const string ObsidianResinId = "obsidian_resin";
	private const string IronLullabyRootId = "iron_lullaby_root";
	private const string BlackIchorId = "black_ichor";
	private const string TutorialPotionId = "potion_gravekeepers_balm";
	private const string TutorialCustomerId = "customer_requests_gravekeepers_balm";
	private const string AmbiguousTutorialCustomerId = "customer_requests_sleep_draught";

	[Export] public NodePath TutorialOverlayPath = default!;
	[Export] public NodePath HudPath = default!;
	[Export] public NodePath InventoryPanelPath = default!;
	[Export] public NodePath BrewPanelPath = default!;
	[Export] public NodePath CustomerPanelPath = default!;
	[Export] public NodePath DayControllerPath = default!;
	[Export] public NodePath CustomerEventControllerPath = default!;
	[Export] public NodePath GameStatePath = new("/root/GameState");
	[Export] public NodePath HudBrewButtonPath = new("BrewPotion");
	[Export] public NodePath HudStartDayButtonPath = new("ServeCustomer");
	[Export] public NodePath HudSettingsButtonPath = new("MainMenu");
	[Export] public NodePath BrewPanelFramePath = new("Panel");

	private GameState _gameState = default!;
	private TutorialOverlay _overlay = default!;
	private Control? _hud;
	private InventoryPanel? _inventoryPanel;
	private BrewPanel? _brewPanel;
	private Control? _brewPanelFrame;
	private CustomerPanel? _customerPanel;
	private DayController? _dayController;
	private CustomerEventController? _customerEventController;
	private Button? _brewButton;
	private Button? _startDayButton;
	private Button? _settingsButton;
	private readonly Dictionary<BaseButton, bool> _openBrewButtonDisabledStates = new();
	private bool _isRunning;
	private bool _lastTutorialSaleSucceeded;

	private enum TutorialStep
	{
		Welcome = 0,
		Status = 1,
		OpenBrewPanel = 2,
		QueueGraveMint = 3,
		QueueObsidianResin = 4,
		QueueIronLullabyRoot = 5,
		BrewPotion = 6,
		StartDay = 7,
		SellPotion = 8,
		SaleResult = 9,
		NextCustomer = 10,
		AmbiguousCustomer = 11,
		InspectBlackIchor = 12,
		BlackIchorRestTrait = 13,
		AddBlackIchorToBrew = 14,
		AddTwoMoreSleepIngredients = 15
	}

	public override void _Ready()
	{
		if (!TryGetRequiredNode(GameStatePath, nameof(GameStatePath), out _gameState))
			return;
		if (!TryGetRequiredNode(TutorialOverlayPath, nameof(TutorialOverlayPath), out _overlay))
			return;

		_hud = GetOptionalControl(HudPath, nameof(HudPath));
		_inventoryPanel = GetOptionalNode<InventoryPanel>(InventoryPanelPath, nameof(InventoryPanelPath));
		_brewPanel = GetOptionalNode<BrewPanel>(BrewPanelPath, nameof(BrewPanelPath));
		_brewPanelFrame = GetOptionalBrewPanelControl(BrewPanelFramePath, nameof(BrewPanelFramePath));
		_customerPanel = GetOptionalNode<CustomerPanel>(CustomerPanelPath, nameof(CustomerPanelPath));
		_dayController = GetOptionalNode<DayController>(DayControllerPath, nameof(DayControllerPath));
		_customerEventController = GetOptionalNode<CustomerEventController>(CustomerEventControllerPath, nameof(CustomerEventControllerPath));
		_brewButton = GetOptionalHudButton(HudBrewButtonPath, nameof(HudBrewButtonPath));
		_startDayButton = GetOptionalHudButton(HudStartDayButtonPath, nameof(HudStartDayButtonPath));
		_settingsButton = GetOptionalHudButton(HudSettingsButtonPath, nameof(HudSettingsButtonPath));

		_overlay.NextPressed += OnNextPressed;
		_overlay.SkipPressed += OnSkipPressed;
		if (_inventoryPanel is not null)
			_inventoryPanel.ItemDetailShown += OnItemDetailShown;
		if (_brewButton is not null)
			_brewButton.Pressed += OnBrewButtonPressed;
		if (_brewPanel is not null)
		{
			_brewPanel.IngredientQueued += OnIngredientQueued;
			_brewPanel.PotionBrewed += OnPotionBrewed;
		}
		if (_dayController is not null)
			_dayController.ShopStateChanged += OnShopStateChanged;
		if (_customerPanel is not null)
		{
			_customerPanel.PotionSold += OnPotionSold;
			_customerPanel.SaleResultClosed += OnSaleResultClosed;
			_customerPanel.InteractionShown += OnCustomerInteractionShown;
		}

		_overlay.HideOverlay();
		Callable.From(StartTutorialIfRequested).CallDeferred();
	}

	public override void _ExitTree()
	{
		RestoreOpenBrewInputLock();

		if (_overlay is not null)
		{
			_overlay.NextPressed -= OnNextPressed;
			_overlay.SkipPressed -= OnSkipPressed;
		}
		if (_inventoryPanel is not null)
			_inventoryPanel.ItemDetailShown -= OnItemDetailShown;
		if (_brewButton is not null)
			_brewButton.Pressed -= OnBrewButtonPressed;
		if (_brewPanel is not null)
		{
			_brewPanel.IngredientQueued -= OnIngredientQueued;
			_brewPanel.PotionBrewed -= OnPotionBrewed;
		}
		if (_dayController is not null)
			_dayController.ShopStateChanged -= OnShopStateChanged;
		if (_customerPanel is not null)
		{
			_customerPanel.PotionSold -= OnPotionSold;
			_customerPanel.SaleResultClosed -= OnSaleResultClosed;
			_customerPanel.InteractionShown -= OnCustomerInteractionShown;
		}
	}

	private void StartTutorialIfRequested()
	{
		if (!_gameState.TutorialRequested || _gameState.TutorialCompleted || _gameState.TutorialSkipped)
			return;

		_isRunning = true;
		ShowStep(ClampStep(_gameState.TutorialStep));
	}

	private void OnNextPressed()
	{
		if (!_isRunning)
			return;

		var step = ClampStep(_gameState.TutorialStep);
		if (step == TutorialStep.Welcome)
		{
			AdvanceTo(TutorialStep.Status);
			return;
		}

		if (step == TutorialStep.Status)
		{
			AdvanceTo(TutorialStep.OpenBrewPanel);
			return;
		}

		if (step == TutorialStep.SaleResult)
		{
			AdvanceTo(TutorialStep.NextCustomer);
			return;
		}

		if (step == TutorialStep.AmbiguousCustomer)
		{
			AdvanceTo(TutorialStep.InspectBlackIchor);
			return;
		}

		if (step == TutorialStep.BlackIchorRestTrait)
		{
			AdvanceTo(TutorialStep.AddBlackIchorToBrew);
			return;
		}

		if (step == TutorialStep.AddTwoMoreSleepIngredients)
			CompleteTutorial();
	}

	private void OnSkipPressed()
	{
		if (!_isRunning)
			return;

		_customerEventController?.ForceNextCustomerInteraction(string.Empty);
		_isRunning = false;
		_overlay.HideOverlay();
		RestoreOpenBrewInputLock();
		_gameState.SkipTutorial();
	}

	private void OnBrewButtonPressed()
	{
		if (!_isRunning || ClampStep(_gameState.TutorialStep) != TutorialStep.OpenBrewPanel)
			return;

		Callable.From(AdvanceIfBrewPanelIsOpen).CallDeferred();
	}

	private void OnIngredientQueued(string itemId, int queuedCount)
	{
		if (!_isRunning)
			return;

		var step = ClampStep(_gameState.TutorialStep);
		if (step == TutorialStep.QueueGraveMint && IsItem(itemId, GraveMintId))
		{
			AdvanceTo(TutorialStep.QueueObsidianResin);
			return;
		}

		if (step == TutorialStep.QueueObsidianResin && IsItem(itemId, ObsidianResinId))
		{
			AdvanceTo(TutorialStep.QueueIronLullabyRoot);
			return;
		}

		if (step == TutorialStep.QueueIronLullabyRoot && IsItem(itemId, IronLullabyRootId))
		{
			AdvanceTo(TutorialStep.BrewPotion);
			return;
		}

		if (step == TutorialStep.AddBlackIchorToBrew && IsItem(itemId, BlackIchorId))
		{
			AdvanceTo(TutorialStep.AddTwoMoreSleepIngredients);
			return;
		}

		if (step == TutorialStep.AddTwoMoreSleepIngredients && queuedCount >= 3)
			CompleteTutorial();
	}

	private void OnItemDetailShown(string itemId)
	{
		if (!_isRunning || ClampStep(_gameState.TutorialStep) != TutorialStep.InspectBlackIchor)
			return;

		if (IsItem(itemId, BlackIchorId))
			AdvanceTo(TutorialStep.BlackIchorRestTrait);
	}

	private void OnPotionBrewed(string potionItemId)
	{
		if (!_isRunning || ClampStep(_gameState.TutorialStep) != TutorialStep.BrewPotion)
			return;

		if (IsItem(potionItemId, TutorialPotionId))
			AdvanceTo(TutorialStep.StartDay);
	}

	private void OnShopStateChanged()
	{
		if (!_isRunning || ClampStep(_gameState.TutorialStep) != TutorialStep.StartDay)
			return;

		if (_dayController is not null && _dayController.IsShopOpen)
			AdvanceTo(TutorialStep.SellPotion);
	}

	private void OnPotionSold(string itemId, bool success)
	{
		if (!_isRunning || ClampStep(_gameState.TutorialStep) != TutorialStep.SellPotion)
			return;

		if (!IsItem(itemId, TutorialPotionId))
			return;

		_lastTutorialSaleSucceeded = success;
		AdvanceTo(TutorialStep.SaleResult);
	}

	private void OnSaleResultClosed()
	{
		if (!_isRunning || ClampStep(_gameState.TutorialStep) != TutorialStep.NextCustomer)
			return;

		Callable.From(AdvanceIfAmbiguousCustomerIsActive).CallDeferred();
	}

	private void OnCustomerInteractionShown(string interactionId)
	{
		if (!_isRunning || ClampStep(_gameState.TutorialStep) != TutorialStep.NextCustomer)
			return;

		if (IsItem(interactionId, AmbiguousTutorialCustomerId))
			AdvanceTo(TutorialStep.AmbiguousCustomer);
	}

	private void AdvanceIfBrewPanelIsOpen()
	{
		if (!_isRunning || ClampStep(_gameState.TutorialStep) != TutorialStep.OpenBrewPanel)
			return;

		if (_brewPanel is not null && _brewPanel.Visible)
			AdvanceTo(TutorialStep.QueueGraveMint);
	}

	private void AdvanceIfAmbiguousCustomerIsActive()
	{
		if (!_isRunning || ClampStep(_gameState.TutorialStep) != TutorialStep.NextCustomer)
			return;

		if (IsItem(_gameState.ActiveCustomerRequest?.Id ?? string.Empty, AmbiguousTutorialCustomerId))
			AdvanceTo(TutorialStep.AmbiguousCustomer);
	}

	private void AdvanceTo(TutorialStep step)
	{
		_gameState.SetTutorialStep((int)step);
		ShowStep(step);
	}

	private void CompleteTutorial()
	{
		_customerEventController?.ForceNextCustomerInteraction(string.Empty);
		_isRunning = false;
		_overlay.HideOverlay();
		RestoreOpenBrewInputLock();
		_gameState.CompleteTutorial();
	}

	private void ShowStep(TutorialStep step)
	{
		if (step == TutorialStep.OpenBrewPanel && _brewPanel is not null && _brewPanel.Visible)
		{
			AdvanceTo(TutorialStep.QueueGraveMint);
			return;
		}

		if (step == TutorialStep.StartDay)
		{
			_customerEventController?.ForceNextCustomerInteraction(TutorialCustomerId);
			if (_dayController is not null && _dayController.IsShopOpen)
			{
				AdvanceTo(TutorialStep.SellPotion);
				return;
			}
		}

		UpdateOpenBrewInputLock(step);
		_overlay.SetSkipButtonVisible(true);

		switch (step)
		{
			case TutorialStep.Welcome:
				ShowManualStep(
					"Welcome to the Shop",
					"This tutorial walks through your stock, brewing your first potion, Gravekeeper's Balm, and selling it to your first customer.",
					"Next",
					panelAtTop: false,
					target: null);
				break;
			case TutorialStep.Status:
				ShowManualStep(
					"Gold, Dread, and Day",
					"Gold pays for brewing. Dread tracks how dangerous the shop has become. Day shows your current run progress.",
					"Next",
					panelAtTop: false,
					target: _hud);
				break;
			case TutorialStep.OpenBrewPanel:
				ShowActionStep(
					"Open the Brew Panel",
					"Click Brew Potion to open the cauldron controls.",
					_brewButton,
					panelAtTop: false);
				break;
			case TutorialStep.QueueGraveMint:
				ShowActionStep(
					"Add Grave Mint",
					"Add Grave Mint to the brew. You can right-click an ingredient slot or drag it out if you add the wrong item.",
					_inventoryPanel,
					panelAtTop: true);
				break;
			case TutorialStep.QueueObsidianResin:
				ShowActionStep(
					"Add Obsidian Resin",
					"Add Obsidian Resin as the second ingredient.",
					_inventoryPanel,
					panelAtTop: true);
				break;
			case TutorialStep.QueueIronLullabyRoot:
				ShowActionStep(
					"Add Iron Lullaby Root",
					"Add Iron Lullaby Root as the third ingredient. The preview should show Gravekeeper's Balm.",
					_inventoryPanel,
					panelAtTop: true);
				break;
			case TutorialStep.BrewPotion:
				ShowActionStep(
					"Brew Gravekeeper's Balm",
					"Click Brew Potion in the brew panel to create Gravekeeper's Balm. It will appear in your inventory.",
					_brewPanelFrame ?? _brewPanel,
					panelAtTop: true);
				break;
			case TutorialStep.StartDay:
				ShowActionStep(
					"Open the Shop",
					"Click Start Day. The tutorial will send in a customer who wants Gravekeeper's Balm.",
					_startDayButton,
					panelAtTop: false);
				break;
			case TutorialStep.SellPotion:
				ShowActionStepForTargets(
					"Sell the Potion",
					"Drag Gravekeeper's Balm from your inventory to the customer's Drop potion here box.",
					false,
					GetTutorialPotionInventorySlot(),
					_customerPanel);
				break;
			case TutorialStep.SaleResult:
				ShowManualStep(
					"Sale Complete",
					_lastTutorialSaleSucceeded
						? "The sale succeeded because Gravekeeper's Balm matched the customer's desired traits. Not every customer will ask for a potion by name, so the next request needs more interpretation."
						: "The sale finished. Finish the tutorial to continue the day.",
					"Next",
					panelAtTop: false,
					target: _customerPanel);
				break;
			case TutorialStep.NextCustomer:
				_customerEventController?.ForceNextCustomerInteraction(AmbiguousTutorialCustomerId);
				ShowActionStep(
					"Let in the Next Customer",
					"Click Next customer to bring in the next request.",
					_customerPanel?.GetNextCustomerButton(),
					panelAtTop: false);
				break;
			case TutorialStep.AmbiguousCustomer:
				ShowManualStep(
					"Read the Request Carefully",
					"This customer cannot sleep, but they are not asking for a potion by name. Read the customer request carefully and choose ingredients whose traits best match what they need.",
					"Next",
					panelAtTop: false,
					target: _customerPanel);
				break;
			case TutorialStep.InspectBlackIchor:
				ShowActionStep(
					"Inspect Black Ichor",
					"Left-click Black Ichor in the inventory to view its details.",
					GetBlackIchorInventorySlot(),
					panelAtTop: false);
				break;
			case TutorialStep.BlackIchorRestTrait:
				ShowManualStep(
					"Rest Helps Sleepless Customers",
					"Black Ichor has a strong Rest trait. That would probably suit a customer who cannot sleep.",
					"Next",
					panelAtTop: false,
					target: _inventoryPanel?.GetItemDetailFrame());
				break;
			case TutorialStep.AddBlackIchorToBrew:
				ShowActionStep(
					"Add Black Ichor",
					"Click Add to Brew to use Black Ichor as the first ingredient for this customer's potion.",
					_inventoryPanel?.GetItemDetailBrewButton(),
					panelAtTop: false);
				break;
			case TutorialStep.AddTwoMoreSleepIngredients:
				ShowActionStepWithoutDim(
					"Choose Two More Ingredients",
					"Add two more ingredients that may suit the customer's need for rest, calm, or dreams.",
					panelAtTop: true);
				break;
		}
	}

	private void ShowManualStep(string title, string body, string nextButtonText, bool panelAtTop, Control? target)
	{
		SetOverlayButtons(nextVisible: true, nextButtonText);
		SetOverlayPanelPlacement(panelAtTop);
		ShowOverlay(title, body, target);
	}

	private void ShowActionStep(string title, string body, Control? target, bool panelAtTop)
	{
		SetOverlayButtons(nextVisible: false, "Next");
		SetOverlayPanelPlacement(panelAtTop);
		ShowOverlay(title, body, target);
	}

	private void ShowActionStepForTargets(string title, string body, bool panelAtTop, params Control?[] targets)
	{
		SetOverlayButtons(nextVisible: false, "Next");
		SetOverlayPanelPlacement(panelAtTop);
		_overlay.ShowForTargets(title, body, targets);
	}

	private void ShowActionStepWithoutDim(string title, string body, bool panelAtTop)
	{
		SetOverlayButtons(nextVisible: false, "Next");
		SetOverlayPanelPlacement(panelAtTop);
		_overlay.ShowMessageWithoutDim(title, body);
	}

	private void SetOverlayButtons(bool nextVisible, string nextButtonText)
	{
		_overlay.SetNextButtonVisible(nextVisible);
		_overlay.SetNextButtonEnabled(true);
		_overlay.SetNextButtonText(nextButtonText);
	}

	private void SetOverlayPanelPlacement(bool panelAtTop)
	{
		if (panelAtTop)
		{
			_overlay.PlacePanelAtTop();
			return;
		}

		_overlay.PlacePanelAtBottom();
	}

	private void ShowOverlay(string title, string body, Control? target)
	{
		if (target is null)
		{
			_overlay.ShowMessage(title, body);
			return;
		}

		_overlay.ShowForTarget(title, body, target);
	}

	private Control? GetTutorialPotionInventorySlot()
	{
		if (_inventoryPanel is null)
			return null;

		_inventoryPanel.ClearPotionFiltersForTutorial();
		return _inventoryPanel.GetVisibleItemSlot(TutorialPotionId);
	}

	private Control? GetBlackIchorInventorySlot()
	{
		if (_inventoryPanel is null)
			return null;

		_inventoryPanel.ClearIngredientFiltersForTutorial();
		return _inventoryPanel.GetVisibleItemSlot(BlackIchorId);
	}

	private void UpdateOpenBrewInputLock(TutorialStep step)
	{
		if (step == TutorialStep.OpenBrewPanel)
		{
			ApplyOpenBrewInputLock();
			return;
		}

		RestoreOpenBrewInputLock();
	}

	private void ApplyOpenBrewInputLock()
	{
		DisableButtonsExceptAllowed(_hud);
		DisableButtonsExceptAllowed(_inventoryPanel);
		DisableButtonsExceptAllowed(_brewPanel);
		DisableButtonsExceptAllowed(_customerPanel);
	}

	private void RestoreOpenBrewInputLock()
	{
		foreach (var pair in _openBrewButtonDisabledStates)
		{
			if (!GodotObject.IsInstanceValid(pair.Key))
				continue;

			pair.Key.Disabled = pair.Value;
		}

		_openBrewButtonDisabledStates.Clear();
	}

	private void DisableButtonsExceptAllowed(Node? root)
	{
		if (root is null)
			return;

		foreach (var child in root.GetChildren())
		{
			if (child is BaseButton button)
				DisableButtonExceptAllowed(button);

			DisableButtonsExceptAllowed(child);
		}
	}

	private void DisableButtonExceptAllowed(BaseButton button)
	{
		if (button == _brewButton || button == _settingsButton)
			return;

		if (!_openBrewButtonDisabledStates.ContainsKey(button))
			_openBrewButtonDisabledStates[button] = button.Disabled;

		button.Disabled = true;
	}

	private static TutorialStep ClampStep(int rawStep)
	{
		if (rawStep <= (int)TutorialStep.Welcome)
			return TutorialStep.Welcome;

		if (rawStep >= (int)TutorialStep.AddTwoMoreSleepIngredients)
			return TutorialStep.AddTwoMoreSleepIngredients;

		return (TutorialStep)rawStep;
	}

	private static bool IsItem(string actualItemId, string expectedItemId)
	{
		return string.Equals(actualItemId, expectedItemId, StringComparison.OrdinalIgnoreCase);
	}

	private bool TryGetRequiredNode<TNode>(NodePath path, string exportName, out TNode node) where TNode : Node
	{
		node = default!;
		if (path.IsEmpty)
		{
			GD.PushError($"TutorialController: {exportName} is not assigned.");
			return false;
		}

		var resolvedNode = GetNodeOrNull<TNode>(path);
		if (resolvedNode is null)
		{
			GD.PushError($"TutorialController: Node was not found at '{path}'.");
			return false;
		}

		node = resolvedNode;
		return true;
	}

	private TNode? GetOptionalNode<TNode>(NodePath path, string exportName) where TNode : Node
	{
		if (path.IsEmpty)
		{
			GD.PushError($"TutorialController: {exportName} is not assigned.");
			return null;
		}

		var node = GetNodeOrNull<TNode>(path);
		if (node is null)
			GD.PushError($"TutorialController: Node was not found at '{path}'.");

		return node;
	}

	private Control? GetOptionalControl(NodePath path, string exportName)
	{
		return GetOptionalNode<Control>(path, exportName);
	}

	private Button? GetOptionalHudButton(NodePath path, string exportName)
	{
		if (_hud is null)
			return null;

		if (path.IsEmpty)
		{
			GD.PushError($"TutorialController: {exportName} is not assigned.");
			return null;
		}

		var button = _hud.GetNodeOrNull<Button>(path);
		if (button is null)
			GD.PushError($"TutorialController: HUD button was not found at '{path}'.");

		return button;
	}

	private Control? GetOptionalBrewPanelControl(NodePath path, string exportName)
	{
		if (_brewPanel is null)
			return null;

		if (path.IsEmpty)
		{
			GD.PushError($"TutorialController: {exportName} is not assigned.");
			return null;
		}

		var control = _brewPanel.GetNodeOrNull<Control>(path);
		if (control is null)
			GD.PushError($"TutorialController: Brew panel control was not found at '{path}'.");

		return control;
	}
}
