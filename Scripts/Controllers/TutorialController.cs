using Godot;
using OccultShop.Autoload;
using OccultShop.Tutorial;
using OccultShop.Tutorial.Presentation;
using OccultShop.UI;

namespace OccultShop.Controllers;

public partial class TutorialController : Node
{
	[Export] public NodePath TutorialOverlayPath = default!;
	[Export] public NodePath HudPath = default!;
	[Export] public NodePath InventoryPanelPath = default!;
	[Export] public NodePath BrewPanelPath = default!;
	[Export] public NodePath CustomerPanelPath = default!;
	[Export] public NodePath DayControllerPath = default!;
	[Export] public NodePath CustomerEventControllerPath = default!;
	[Export] public NodePath GameStatePath = new(AutoloadNodePaths.GameState);
	[Export] public NodePath HudBrewButtonPath = new("BrewPotion");
	[Export] public NodePath HudStartDayButtonPath = new("ServeCustomer");
	[Export] public NodePath HudSettingsButtonPath = new("MainMenu");
	[Export] public NodePath BrewPanelFramePath = new("Panel");
	[Export] public TutorialContentResource TutorialContent = new();

	private GameState _gameState = default!;
	private TutorialOverlay _overlay = default!;
	private TutorialOverlayPresenter _overlayPresenter = default!;
	private TutorialContentResource _tutorialContent = default!;
	private TutorialStateMachine _stateMachine = default!;
	private readonly TutorialInteractionGate _interactionGate = new();

	private Control? _hud;
	private Label? _hudDayLabel;
	private Label? _hudShopTimerLabel;
	private InventoryPanel? _inventoryPanel;
	private BrewPanel? _brewPanel;
	private Control? _brewPanelFrame;
	private CustomerPanel? _customerPanel;
	private DayController? _dayController;
	private CustomerEventController? _customerEventController;
	private Button? _brewButton;
	private Button? _startDayButton;
	private Button? _settingsButton;

	private bool _isRunning;
	private bool _lastTutorialSaleSucceeded;
	private bool _sleepCustomerPotionSold;

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
		_hudDayLabel = GetOptionalHudLabel("Day");
		_hudShopTimerLabel = GetOptionalHudLabel("ShopTimer");

		_tutorialContent = TutorialContent ?? new TutorialContentResource();
		_stateMachine = new TutorialStateMachine(_tutorialContent);
		_overlayPresenter = new TutorialOverlayPresenter(_overlay);

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

		_overlayPresenter.Hide();
		Callable.From(StartTutorialIfRequested).CallDeferred();
	}

	public override void _ExitTree()
	{
		_interactionGate.Restore();
		ResumeShopTimerAfterTutorial();

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
		ResetLastTutorialSaleFeedback();
		ResetCloseShopTutorialState();
		UpdateShopTimerPause();
		ShowStep(CurrentStep());
	}

	private void OnNextPressed()
	{
		if (!_isRunning)
			return;

		ApplyTransition(_stateMachine.EvaluateNextPressed(CurrentStep()));
	}

	private void OnSkipPressed()
	{
		if (!_isRunning)
			return;

		_customerEventController?.ForceNextCustomerInteraction(string.Empty);
		_isRunning = false;
		_overlayPresenter.Hide();
		_interactionGate.Restore();
		ResumeShopTimerAfterTutorial();
		ResetLastTutorialSaleFeedback();
		ResetCloseShopTutorialState();
		_gameState.SkipTutorial();
	}

	private void OnBrewButtonPressed()
	{
		if (!_isRunning || CurrentStep() != TutorialStepId.OpenBrewPanel)
			return;

		Callable.From(AdvanceIfBrewPanelIsOpen).CallDeferred();
	}

	private void OnIngredientQueued(string itemId, int queuedCount)
	{
		if (!_isRunning)
			return;

		ApplyTransition(_stateMachine.EvaluateIngredientQueued(CurrentStep(), itemId, queuedCount));
	}

	private void OnItemDetailShown(string itemId)
	{
		if (!_isRunning)
			return;

		ApplyTransition(_stateMachine.EvaluateItemDetailShown(CurrentStep(), itemId));
	}

	private void OnPotionBrewed(string potionItemId)
	{
		if (!_isRunning)
			return;

		ApplyTransition(_stateMachine.EvaluatePotionBrewed(CurrentStep(), potionItemId));
	}

	private void OnShopStateChanged()
	{
		if (!_isRunning)
			return;

		var currentStep = CurrentStep();
		ApplyTransition(_stateMachine.EvaluateShopStateChanged(currentStep, _dayController is not null && _dayController.IsShopOpen));
		ApplyTransition(_stateMachine.EvaluateCloseShopPrompt(
			currentStep,
			_sleepCustomerPotionSold,
			_customerPanel is not null && _customerPanel.IsCloseShopMode));
	}

	private void OnPotionSold(string itemId, bool success)
	{
		if (!_isRunning)
			return;

		var currentStep = CurrentStep();
		var transition = _stateMachine.EvaluatePotionSold(currentStep, itemId);
		if (transition.HasNextStep && transition.NextStep == TutorialStepId.SaleResult)
			_lastTutorialSaleSucceeded = success;

		if (currentStep == TutorialStepId.AddTwoMoreSleepIngredients)
		{
			_sleepCustomerPotionSold = true;
			ApplyTransition(_stateMachine.EvaluateCloseShopPrompt(
				currentStep,
				_sleepCustomerPotionSold,
				_customerPanel is not null && _customerPanel.IsCloseShopMode));
		}

		ApplyTransition(transition);
	}

	private void OnSaleResultClosed()
	{
		if (!_isRunning || CurrentStep() != TutorialStepId.NextCustomer)
			return;

		Callable.From(AdvanceIfAmbiguousCustomerIsActive).CallDeferred();
	}

	private void OnCustomerInteractionShown(string interactionId)
	{
		if (!_isRunning)
			return;

		ApplyTransition(_stateMachine.EvaluateCustomerInteractionShown(CurrentStep(), interactionId));
	}

	private void AdvanceIfBrewPanelIsOpen()
	{
		if (!_isRunning)
			return;

		ApplyTransition(_stateMachine.EvaluateOpenBrewPanelState(CurrentStep(), _brewPanel is not null && _brewPanel.Visible));
	}

	private void AdvanceIfAmbiguousCustomerIsActive()
	{
		if (!_isRunning)
			return;

		ApplyTransition(_stateMachine.EvaluateAmbiguousCustomerState(CurrentStep(), _gameState.ActiveCustomerRequest?.Id));
	}

	private void ApplyTransition(TutorialTransition transition)
	{
		if (transition.ShouldComplete)
		{
			CompleteTutorial();
			return;
		}

		if (!transition.HasNextStep)
			return;

		AdvanceTo(transition.NextStep);
	}

	private void AdvanceTo(TutorialStepId step)
	{
		if (step == TutorialStepId.AddTwoMoreSleepIngredients)
			_dayController?.ForceShopTimerToZeroForTutorial();

		_gameState.SetTutorialStep((int)step);
		ShowStep(step);
	}

	private void CompleteTutorial()
	{
		_customerEventController?.ForceNextCustomerInteraction(string.Empty);
		_isRunning = false;
		_overlayPresenter.Hide();
		_interactionGate.Restore();
		ResumeShopTimerAfterTutorial();
		ResetLastTutorialSaleFeedback();
		ResetCloseShopTutorialState();
		_gameState.CompleteTutorial();
	}

	private void ShowStep(TutorialStepId step)
	{
		var brewPanelTransition = _stateMachine.EvaluateOpenBrewPanelState(step, _brewPanel is not null && _brewPanel.Visible);
		if (brewPanelTransition.HasNextStep || brewPanelTransition.ShouldComplete)
		{
			ApplyTransition(brewPanelTransition);
			return;
		}

		if (step == TutorialStepId.StartDay)
		{
			_customerEventController?.ForceNextCustomerInteraction(_tutorialContent.TutorialCustomerId);
			var shopStateTransition = _stateMachine.EvaluateShopStateChanged(step, _dayController is not null && _dayController.IsShopOpen);
			if (shopStateTransition.HasNextStep || shopStateTransition.ShouldComplete)
			{
				ApplyTransition(shopStateTransition);
				return;
			}
		}

		var stepContent = _tutorialContent.GetStepContent(step);
		UpdateShopTimerPause();
		UpdateTutorialButtonLock(stepContent, GetAllowedButtonsForStep(step));
		_overlayPresenter.SetSkipButtonVisible(true);

		switch (step)
		{
			case TutorialStepId.Welcome:
				_overlayPresenter.ShowMessage(stepContent);
				break;
			case TutorialStepId.Status:
				if (TryGetStatusHighlightRect(out var statusHighlightRect))
				{
					_overlayPresenter.ShowForHighlightRect(stepContent, statusHighlightRect);
					break;
				}

				_overlayPresenter.ShowForTarget(stepContent, _hud);
				break;
			case TutorialStepId.OpenBrewPanel:
				_overlayPresenter.ShowForTarget(stepContent, _brewButton);
				break;
			case TutorialStepId.QueueGraveMint:
				ShowIngredientQueueStep(stepContent, _tutorialContent.GraveMintId);
				break;
			case TutorialStepId.QueueObsidianResin:
				ShowIngredientQueueStep(stepContent, _tutorialContent.ObsidianResinId);
				break;
			case TutorialStepId.QueueIronLullabyRoot:
				ShowIngredientQueueStep(stepContent, _tutorialContent.IronLullabyRootId);
				break;
			case TutorialStepId.BrewPotion:
				_overlayPresenter.ShowForTarget(stepContent, _brewPanelFrame ?? _brewPanel);
				break;
			case TutorialStepId.StartDay:
				_overlayPresenter.ShowForTarget(stepContent, _startDayButton);
				break;
			case TutorialStepId.SellPotion:
				_overlayPresenter.ShowForTargets(stepContent, null, FocusTutorialPotionInventorySlot(), _customerPanel);
				break;
			case TutorialStepId.SaleResult:
				_overlayPresenter.ShowForTarget(
					stepContent,
					_customerPanel,
					_tutorialContent.BuildSaleResultBody(_lastTutorialSaleSucceeded));
				break;
			case TutorialStepId.NextCustomer:
				_gameState.SeedNextCustomerTutorialInventory();
				_customerEventController?.ForceNextCustomerInteraction(_tutorialContent.AmbiguousTutorialCustomerId);
				_overlayPresenter.ShowForTarget(stepContent, _customerPanel?.GetNextCustomerButton());
				break;
			case TutorialStepId.AmbiguousCustomer:
				_overlayPresenter.ShowForTarget(stepContent, _customerPanel);
				break;
			case TutorialStepId.InspectBlackIchor:
				_overlayPresenter.ShowForTarget(stepContent, FocusBlackIchorInventorySlot());
				break;
			case TutorialStepId.BlackIchorRestTrait:
				_overlayPresenter.ShowForTarget(stepContent, _inventoryPanel?.GetItemDetailFrame());
				break;
			case TutorialStepId.AddBlackIchorToBrew:
				_overlayPresenter.ShowForTarget(stepContent, _inventoryPanel?.GetItemDetailBrewButton());
				break;
			case TutorialStepId.AddTwoMoreSleepIngredients:
				_overlayPresenter.ShowMessage(stepContent);
				break;
			case TutorialStepId.CloseShop:
				_overlayPresenter.ShowForTarget(stepContent, _customerPanel?.GetNextCustomerButton());
				break;
		}
	}

	private void ShowIngredientQueueStep(TutorialStepContentResource stepContent, string itemId)
	{
		var expectedStep = CurrentStep();
		Callable.From(() =>
		{
			if (!_isRunning || CurrentStep() != expectedStep)
				return;

			_overlayPresenter.ShowForTargets(
				stepContent,
				null,
				FocusTutorialIngredientInventorySlot(itemId),
				FocusTutorialBrewPanel());
		}).CallDeferred();
	}

	private TutorialStepId CurrentStep()
	{
		return _stateMachine.ClampStep(_gameState.TutorialStep);
	}

	private Control? FocusTutorialPotionInventorySlot()
	{
		if (_inventoryPanel is null)
			return null;

		_inventoryPanel.ClearPotionFiltersForTutorial();
		return _inventoryPanel.GetVisibleItemSlot(_tutorialContent.TutorialPotionId);
	}

	private bool TryGetStatusHighlightRect(out Rect2 highlightRect)
	{
		highlightRect = default;

		if (_hud is null)
			return false;

		var hasHighlightRect = false;
		foreach (var control in new Control?[] { _hud, _hudDayLabel, _hudShopTimerLabel })
		{
			if (control is null)
				continue;

			var rect = control.GetGlobalRect();
			if (!hasHighlightRect)
			{
				highlightRect = rect;
				hasHighlightRect = true;
				continue;
			}

			var left = Mathf.Min(highlightRect.Position.X, rect.Position.X);
			var top = Mathf.Min(highlightRect.Position.Y, rect.Position.Y);
			var right = Mathf.Max(highlightRect.Position.X + highlightRect.Size.X, rect.Position.X + rect.Size.X);
			var bottom = Mathf.Max(highlightRect.Position.Y + highlightRect.Size.Y, rect.Position.Y + rect.Size.Y);
			highlightRect = new Rect2(new Vector2(left, top), new Vector2(right - left, bottom - top));
		}

		return hasHighlightRect;
	}

	private Control? FocusBlackIchorInventorySlot()
	{
		if (_inventoryPanel is null)
			return null;

		_inventoryPanel.ClearIngredientFiltersForTutorial();
		return _inventoryPanel.GetVisibleItemSlot(_tutorialContent.BlackIchorId);
	}

	private Control? FocusTutorialIngredientInventorySlot(string itemId)
	{
		if (_inventoryPanel is null)
			return null;

		_inventoryPanel.ClearIngredientFiltersForTutorial();
		return _inventoryPanel.GetVisibleItemSlot(itemId);
	}

	private Control? FocusTutorialBrewPanel()
	{
		return _brewPanelFrame ?? _brewPanel;
	}

	private void UpdateShopTimerPause()
	{
		if (_dayController is null || !_isRunning)
			return;

		_dayController.PauseShopTimerForTutorial();
	}

	private void ResumeShopTimerAfterTutorial()
	{
		_dayController?.ResumeShopTimerAfterTutorial();
	}

	private void UpdateTutorialButtonLock(TutorialStepContentResource stepContent, params BaseButton?[] allowedButtons)
	{
		if (!stepContent.LockOtherButtons)
		{
			_interactionGate.Restore();
			return;
		}

		_interactionGate.Apply(
			new Node?[] { _hud, _inventoryPanel, _brewPanel, _customerPanel },
			allowedButtons);
	}

	private BaseButton?[] GetAllowedButtonsForStep(TutorialStepId step)
	{
		return step switch
		{
			TutorialStepId.OpenBrewPanel => new BaseButton?[] { _brewButton, _settingsButton },
			TutorialStepId.QueueGraveMint => new BaseButton?[] { GetAllowedButton(FocusTutorialIngredientInventorySlot(_tutorialContent.GraveMintId)) },
			TutorialStepId.QueueObsidianResin => new BaseButton?[] { GetAllowedButton(FocusTutorialIngredientInventorySlot(_tutorialContent.ObsidianResinId)) },
			TutorialStepId.QueueIronLullabyRoot => new BaseButton?[] { GetAllowedButton(FocusTutorialIngredientInventorySlot(_tutorialContent.IronLullabyRootId)) },
			TutorialStepId.BrewPotion => new BaseButton?[] { _brewPanel?.GetBrewButton() },
			TutorialStepId.StartDay => new BaseButton?[] { _startDayButton },
			TutorialStepId.SellPotion => new BaseButton?[] { GetAllowedButton(FocusTutorialPotionInventorySlot()) },
			TutorialStepId.NextCustomer => new BaseButton?[] { _customerPanel?.GetNextCustomerButton() },
			TutorialStepId.InspectBlackIchor => new BaseButton?[] { GetAllowedButton(FocusBlackIchorInventorySlot()) },
			TutorialStepId.AddBlackIchorToBrew => new BaseButton?[] { _inventoryPanel?.GetItemDetailBrewButton() },
			TutorialStepId.CloseShop => new BaseButton?[] { _customerPanel?.GetNextCustomerButton() },
			_ => new BaseButton?[] { }
		};
	}

	private static BaseButton? GetAllowedButton(Control? control)
	{
		return control as BaseButton;
	}

	private void ResetLastTutorialSaleFeedback()
	{
		_lastTutorialSaleSucceeded = false;
	}

	private void ResetCloseShopTutorialState()
	{
		_sleepCustomerPotionSold = false;
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

	private Label? GetOptionalHudLabel(string labelName)
	{
		if (_hud is null)
			return null;

		var label = _hud.GetNodeOrNull<Label>(labelName);
		if (label is null)
			GD.PushError($"TutorialController: HUD label was not found at '{labelName}'.");

		return label;
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
