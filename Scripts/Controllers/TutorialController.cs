using Godot;
using OccultShop.Autoload;
using OccultShop.Systems;
using OccultShop.Tutorial;
using OccultShop.Tutorial.Presentation;
using OccultShop.UI;

namespace OccultShop.Controllers;

public partial class TutorialController : Node
{
	[Export] public NodePath TutorialOverlayPath = default!;
	[Export] public NodePath HudPath = default!;
	[Export] public NodePath BrewPanelPath = default!;
	[Export] public NodePath StationShelfInventoryPath = default!;
	[Export] public NodePath IngredientPreparationTrayPath = default!;
	[Export] public NodePath StationCustomerPanelPath = default!;
	[Export] public NodePath DaySummaryPanelPath = default!;
	[Export] public NodePath DayControllerPath = default!;
	[Export] public NodePath CustomerEventControllerPath = default!;
	[Export] public NodePath GameStatePath = new(AutoloadNodePaths.GameState);
	[Export] public NodePath ShopSessionStatePath = new(AutoloadNodePaths.ShopSessionState);
	[Export] public NodePath HudStartDayButtonPath = new("Content/Actions/ServeCustomer");
	[Export] public NodePath HudGardenButtonPath = new("Content/Actions/Garden");
	[Export] public NodePath HudSettingsButtonPath = new("Content/Actions/MainMenu");
	[Export] public NodePath HudDateControlPath = new("Content/Status/Day");
	[Export] public NodePath BrewPanelFramePath = new("Panel");
	[Export] public TutorialContentResource TutorialContent = new();

	private GameState _gameState = default!;
	private ShopSessionState _shopSessionState = default!;
	private TutorialOverlay _overlay = default!;
	private TutorialOverlayPresenter _overlayPresenter = default!;
	private TutorialContentResource _tutorialContent = default!;
	private TutorialStateMachine _stateMachine = default!;
	private readonly TutorialInteractionGate _interactionGate = new();

	private Control? _hud;
	private Control? _hudDateControl;
	private BrewPanel? _brewPanel;
	private Control? _brewPanelFrame;
	private StationShelfInventory? _stationShelfInventory;
	private IngredientPreparationTray? _ingredientPreparationTray;
	private StationCustomerPanel? _stationCustomerPanel;
	private DaySummaryPanel? _daySummaryPanel;
	private DayController? _dayController;
	private CustomerEventController? _customerEventController;
	private Button? _startDayButton;
	private Button? _gardenButton;
	private Button? _settingsButton;

	private bool _isRunning;
	private bool _lastTutorialSaleSucceeded;
	private TutorialStepId? _lastMotherLineStep;

	public override void _Ready()
	{
		if (!TryGetRequiredNode(GameStatePath, nameof(GameStatePath), out _gameState))
			return;
		if (!TryGetRequiredNode(ShopSessionStatePath, nameof(ShopSessionStatePath), out _shopSessionState))
			return;
		if (!TryGetRequiredNode(TutorialOverlayPath, nameof(TutorialOverlayPath), out _overlay))
			return;

		_hud = GetOptionalControl(HudPath, nameof(HudPath));
		_brewPanel = GetOptionalNode<BrewPanel>(BrewPanelPath, nameof(BrewPanelPath));
		_brewPanelFrame = GetOptionalBrewPanelControl(BrewPanelFramePath, nameof(BrewPanelFramePath));
		_stationShelfInventory = GetOptionalNode<StationShelfInventory>(StationShelfInventoryPath, nameof(StationShelfInventoryPath));
		_ingredientPreparationTray = GetOptionalNode<IngredientPreparationTray>(IngredientPreparationTrayPath, nameof(IngredientPreparationTrayPath));
		_stationCustomerPanel = GetOptionalNode<StationCustomerPanel>(StationCustomerPanelPath, nameof(StationCustomerPanelPath));
		_daySummaryPanel = GetOptionalNode<DaySummaryPanel>(DaySummaryPanelPath, nameof(DaySummaryPanelPath));
		_dayController = GetOptionalNode<DayController>(DayControllerPath, nameof(DayControllerPath));
		_customerEventController = GetOptionalNode<CustomerEventController>(CustomerEventControllerPath, nameof(CustomerEventControllerPath));
		_startDayButton = GetOptionalHudButton(HudStartDayButtonPath, nameof(HudStartDayButtonPath));
		_gardenButton = GetOptionalHudButton(HudGardenButtonPath, nameof(HudGardenButtonPath));
		_settingsButton = GetOptionalHudButton(HudSettingsButtonPath, nameof(HudSettingsButtonPath));
		_hudDateControl = GetOptionalHudControl(HudDateControlPath, nameof(HudDateControlPath));

		_tutorialContent = TutorialContent ?? new TutorialContentResource();
		_stateMachine = new TutorialStateMachine(_tutorialContent);
		_overlayPresenter = new TutorialOverlayPresenter(_overlay);

		_overlay.NextPressed += OnNextPressed;
		_overlay.SkipPressed += OnSkipPressed;
		if (_brewPanel is not null)
		{
			_brewPanel.IngredientQueued += OnIngredientQueued;
			_brewPanel.PotionBrewed += OnPotionBrewed;
		}
		if (_ingredientPreparationTray is not null)
		{
			_ingredientPreparationTray.IngredientSelected += OnIngredientSelectedForPreparation;
			_ingredientPreparationTray.IngredientPrepared += OnIngredientPrepared;
		}
		if (_dayController is not null)
			_dayController.ShopStateChanged += OnShopStateChanged;
		if (_daySummaryPanel is not null)
			_daySummaryPanel.ContinuePressed += OnDaySummaryContinuePressed;
		if (_stationCustomerPanel is not null)
		{
			_stationCustomerPanel.PotionSold += OnPotionSold;
			_stationCustomerPanel.PotionSelectedForServing += OnPotionSelectedForServing;
			_stationCustomerPanel.SaleResultClosed += OnSaleResultClosed;
			_stationCustomerPanel.InteractionShown += OnCustomerInteractionShown;
			_stationCustomerPanel.MotherPostServeDialogueResolved += OnMotherPostServeDialogueResolved;
		}

		_overlayPresenter.Hide();
		Callable.From(StartTutorialIfRequested).CallDeferred();
	}

	public override void _ExitTree()
	{
		_interactionGate.Restore();

		if (_overlay is not null)
		{
			_overlay.NextPressed -= OnNextPressed;
			_overlay.SkipPressed -= OnSkipPressed;
		}
		if (_brewPanel is not null)
		{
			_brewPanel.IngredientQueued -= OnIngredientQueued;
			_brewPanel.PotionBrewed -= OnPotionBrewed;
		}
		if (_ingredientPreparationTray is not null)
		{
			_ingredientPreparationTray.IngredientSelected -= OnIngredientSelectedForPreparation;
			_ingredientPreparationTray.IngredientPrepared -= OnIngredientPrepared;
		}
		if (_dayController is not null)
			_dayController.ShopStateChanged -= OnShopStateChanged;
		if (_daySummaryPanel is not null)
			_daySummaryPanel.ContinuePressed -= OnDaySummaryContinuePressed;
		if (_stationCustomerPanel is not null)
		{
			_stationCustomerPanel.PotionSold -= OnPotionSold;
			_stationCustomerPanel.PotionSelectedForServing -= OnPotionSelectedForServing;
			_stationCustomerPanel.SaleResultClosed -= OnSaleResultClosed;
			_stationCustomerPanel.InteractionShown -= OnCustomerInteractionShown;
			_stationCustomerPanel.MotherPostServeDialogueResolved -= OnMotherPostServeDialogueResolved;
		}
	}

	private void StartTutorialIfRequested()
	{
		if (!_gameState.TutorialRequested || _gameState.TutorialCompleted || _gameState.TutorialSkipped)
			return;

		_isRunning = true;
		_lastMotherLineStep = null;
		ResetLastTutorialSaleFeedback();
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
		ResetLastTutorialSaleFeedback();
		_lastMotherLineStep = null;
		_gameState.SkipTutorial();
	}

	private void OnIngredientQueued(string itemId, int queuedCount)
	{
		if (!_isRunning)
			return;

		ApplyTransition(_stateMachine.EvaluateIngredientQueued(CurrentStep(), itemId, queuedCount));
	}

	private void OnIngredientSelectedForPreparation(string itemId)
	{
		if (!_isRunning)
			return;

		ApplyTransition(_stateMachine.EvaluateIngredientSelected(CurrentStep(), itemId));
	}

	private void OnIngredientPrepared(string ingredientId, string preparationId, string preparedItemId)
	{
		if (!_isRunning)
			return;

		ApplyTransition(_stateMachine.EvaluateIngredientPrepared(CurrentStep(), ingredientId, preparationId));
	}

	private void OnPotionBrewed(string potionItemId)
	{
		if (!_isRunning)
			return;

		ApplyTransition(_stateMachine.EvaluatePotionBrewed(CurrentStep(), potionItemId));
	}

	private void OnPotionSelectedForServing(string itemId)
	{
		if (!_isRunning)
			return;

		ApplyTransition(_stateMachine.EvaluatePotionSelectedForServing(CurrentStep(), itemId));
	}

	private void OnShopStateChanged()
	{
		if (!_isRunning)
			return;

		var currentStep = CurrentStep();
		ApplyTransition(_stateMachine.EvaluateShopStateChanged(currentStep, _dayController is not null && _dayController.IsShopOpen));
	}

	private void OnPotionSold(string itemId, bool success)
	{
		if (!_isRunning)
			return;

		var currentStep = CurrentStep();
		var transition = _stateMachine.EvaluatePotionSold(currentStep, itemId);
		if (transition.HasNextStep && transition.NextStep == TutorialStepId.PostServeMotherDialogue)
		{
			_lastTutorialSaleSucceeded = success;
			_customerEventController?.ForceNextCustomerInteraction(string.Empty);
			_dayController?.ForceCloseShopAfterCurrentCustomerForTutorial();
		}

		ApplyTransition(transition);
	}

	private void OnMotherPostServeDialogueResolved()
	{
		if (!_isRunning)
			return;

		ApplyTransition(_stateMachine.EvaluateMotherPostServeDialogueResolved(CurrentStep()));
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

	private void OnDaySummaryContinuePressed()
	{
		if (!_isRunning)
			return;

		ApplyTransition(_stateMachine.EvaluateDaySummaryContinued(CurrentStep()));
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

		ApplyTransition(_stateMachine.EvaluateAmbiguousCustomerState(CurrentStep(), _shopSessionState.ActiveCustomerRequest?.Id));
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
			_dayController?.ForceCloseShopAfterCurrentCustomerForTutorial();

		_gameState.SetTutorialStep((int)step);
		ShowStep(step);
	}

	private void CompleteTutorial()
	{
		FinishTutorialRuntimeState();
		_gameState.CompleteTutorial();
	}

	private void FinishTutorialRuntimeState()
	{
		_customerEventController?.ForceNextCustomerInteraction(string.Empty);
		_isRunning = false;
		_overlayPresenter.Hide();
		_interactionGate.Restore();
		ResetLastTutorialSaleFeedback();
		_lastMotherLineStep = null;
	}

	private void ShowStep(TutorialStepId step)
	{
		if (step == TutorialStepId.OpenBrewPanel)
			EnsureOpeningTutorialCustomer();

		var brewPanelTransition = _stateMachine.EvaluateOpenBrewPanelState(step, _brewPanel is not null && _brewPanel.Visible);
		if (brewPanelTransition.HasNextStep || brewPanelTransition.ShouldComplete)
		{
			ApplyTransition(brewPanelTransition);
			return;
		}

		if (step == TutorialStepId.StartDay)
		{
			var activeCustomerTransition = _stateMachine.EvaluateCustomerInteractionShown(
				step,
				_shopSessionState.ActiveCustomerRequest?.Id ?? string.Empty);
			if (activeCustomerTransition.HasNextStep || activeCustomerTransition.ShouldComplete)
			{
				ApplyTransition(activeCustomerTransition);
				return;
			}

			if (_dayController is null || !_dayController.IsShopOpen)
				_customerEventController?.ForceNextCustomerInteraction(_tutorialContent.TutorialCustomerId);

			var shopStateTransition = _stateMachine.EvaluateShopStateChanged(step, _dayController is not null && _dayController.IsShopOpen);
			if (shopStateTransition.HasNextStep || shopStateTransition.ShouldComplete)
			{
				ApplyTransition(shopStateTransition);
				return;
			}
		}

		if (IsOpeningPotionStep(step))
			EnsureOpeningTutorialCustomer();

		var stepContent = _tutorialContent.GetStepContent(step);
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
				_overlayPresenter.ShowForTarget(stepContent, _brewPanelFrame ?? _brewPanel);
				break;
			case TutorialStepId.QueueMint:
				ShowIngredientSelectionStep(stepContent, _tutorialContent.MintId, "Let's start with the Mint.");
				break;
			case TutorialStepId.PrepareMintRaw:
				ShowRawPreparationStep(stepContent, "Ingredients can be prepared a few different ways. Use Raw for this potion.");
				break;
			case TutorialStepId.QueueGorse:
				ShowIngredientSelectionStep(stepContent, _tutorialContent.GorseId, "Next, the Gorse.");
				break;
			case TutorialStepId.PrepareGorseRaw:
				ShowRawPreparationStep(stepContent, "Keep the Gorse Raw as well.");
				break;
			case TutorialStepId.QueueThyme:
				ShowIngredientSelectionStep(stepContent, _tutorialContent.ThymeId, "Finally, the Thyme.");
				break;
			case TutorialStepId.PrepareThymeRaw:
				ShowRawPreparationStep(stepContent, "Raw again, then the brew will be ready.");
				break;
			case TutorialStepId.BrewPotion:
				ShowBrewStep(stepContent);
				break;
			case TutorialStepId.StartDay:
				_overlayPresenter.ShowForTarget(stepContent, _startDayButton);
				break;
			case TutorialStepId.SellPotion:
				ShowServingDropStep(stepContent);
				break;
			case TutorialStepId.ConfirmServe:
				ShowServeButtonStep(stepContent);
				break;
			case TutorialStepId.PostServeMotherDialogue:
				_overlayPresenter.Hide();
				break;
			case TutorialStepId.SaleResult:
				_overlayPresenter.ShowForTarget(
					stepContent,
					_stationCustomerPanel,
					_tutorialContent.BuildSaleResultBody(_lastTutorialSaleSucceeded));
				break;
			case TutorialStepId.NextCustomer:
				_customerEventController?.ForceNextCustomerInteraction(string.Empty);
				_overlayPresenter.Hide();
				break;
			case TutorialStepId.AmbiguousCustomer:
				_overlayPresenter.Hide();
				break;
			case TutorialStepId.AddTwoMoreSleepIngredients:
				_overlayPresenter.ShowMessage(stepContent);
				break;
			case TutorialStepId.CloseShop:
				_overlayPresenter.ShowForTarget(stepContent, _stationCustomerPanel);
				break;
			case TutorialStepId.DaySummary:
				_overlayPresenter.ShowForTarget(stepContent, _daySummaryPanel);
				break;
		}
	}

	private void ShowIngredientSelectionStep(TutorialStepContentResource stepContent, string itemId, string motherLine)
	{
		var expectedStep = CurrentStep();
		ShowMotherLineForStep(expectedStep, motherLine);
		Callable.From(() =>
		{
			if (!_isRunning || CurrentStep() != expectedStep)
				return;

			var ingredientTarget = FocusIngredientShelfSlot(itemId) ?? _stationShelfInventory;
			var preparationTarget = FocusPreparationDropBox() ?? _ingredientPreparationTray;
			_overlayPresenter.ShowForTargetsWithArrow(stepContent, ingredientTarget, preparationTarget);
		}).CallDeferred();
	}

	private void ShowRawPreparationStep(TutorialStepContentResource stepContent, string motherLine)
	{
		var expectedStep = CurrentStep();
		ShowMotherLineForStep(expectedStep, motherLine);
		Callable.From(() =>
		{
			if (!_isRunning || CurrentStep() != expectedStep)
				return;

			var preparationTarget = FocusPreparationDropBox() ?? _ingredientPreparationTray;
			Control? rawButton = FocusRawPreparationButton() ?? (Control?)_ingredientPreparationTray;
			_overlayPresenter.ShowForTargetsWithArrow(stepContent, preparationTarget, rawButton);
		}).CallDeferred();
	}

	private void ShowBrewStep(TutorialStepContentResource stepContent)
	{
		var expectedStep = CurrentStep();
		ShowMotherLineForStep(expectedStep, "That's everything. Brew it now.");
		Callable.From(() =>
		{
			if (!_isRunning || CurrentStep() != expectedStep)
				return;

			_overlayPresenter.ShowForTargetsWithArrow(
				stepContent,
				FocusTutorialBrewPanel(),
				_brewPanel?.GetBrewButton());
		}).CallDeferred();
	}

	private void ShowServingDropStep(TutorialStepContentResource stepContent)
	{
		var expectedStep = CurrentStep();
		ShowMotherLineForStep(expectedStep, BuildGreatJobMotherLine());
		Callable.From(() =>
		{
			if (!_isRunning || CurrentStep() != expectedStep)
				return;

			_overlayPresenter.ShowForTargetsWithArrow(
				stepContent,
				FocusTutorialPotionInventorySlot(),
				FocusServingDropBox());
		}).CallDeferred();
	}

	private void ShowServeButtonStep(TutorialStepContentResource stepContent)
	{
		var expectedStep = CurrentStep();
		ShowMotherLineForStep(expectedStep, "Now click Serve so I can take it.");
		Callable.From(() =>
		{
			if (!_isRunning || CurrentStep() != expectedStep)
				return;

			_overlayPresenter.ShowForTargetsWithArrow(
				stepContent,
				FocusServingDropBox(),
				_stationCustomerPanel?.GetServeButton());
		}).CallDeferred();
	}

	private TutorialStepId CurrentStep()
	{
		return _stateMachine.ClampStep(_gameState.TutorialStep);
	}

	private Control? FocusTutorialPotionInventorySlot()
	{
		if (_stationCustomerPanel is null)
			return null;

		return _stationCustomerPanel.GetVisiblePotionSlot(_tutorialContent.TutorialPotionId);
	}

	private Control? FocusIngredientShelfSlot(string itemId)
	{
		return _stationShelfInventory?.GetVisibleIngredientSlot(itemId);
	}

	private Control? FocusPreparationDropBox()
	{
		return _ingredientPreparationTray?.GetPreparationDropBox();
	}

	private Button? FocusRawPreparationButton()
	{
		return _ingredientPreparationTray?.GetPreparationButton(IngredientPreparationCatalog.RawPreparationId);
	}

	private Control? FocusServingDropBox()
	{
		return _stationCustomerPanel?.GetServingDropBox();
	}

	private void EnsureOpeningTutorialCustomer()
	{
		if (_stationCustomerPanel is not null && _stationCustomerPanel.HasActiveInteraction)
			return;

		if (_dayController is null || _customerEventController is null)
			return;

		_customerEventController.ForceNextCustomerInteraction(_tutorialContent.TutorialCustomerId);
		if (!_dayController.IsShopOpen)
			_dayController.StartShopDay();
	}

	private static bool IsOpeningPotionStep(TutorialStepId step)
	{
		return step is TutorialStepId.QueueMint
			or TutorialStepId.PrepareMintRaw
			or TutorialStepId.QueueGorse
			or TutorialStepId.PrepareGorseRaw
			or TutorialStepId.QueueThyme
			or TutorialStepId.PrepareThymeRaw
			or TutorialStepId.BrewPotion
			or TutorialStepId.SellPotion
			or TutorialStepId.ConfirmServe;
	}

	private void ShowMotherLineForStep(TutorialStepId step, string line)
	{
		if (_lastMotherLineStep == step)
			return;

		_stationCustomerPanel?.ShowTutorialMotherLine(line);
		_lastMotherLineStep = step;
	}

	private string BuildGreatJobMotherLine()
	{
		return $"Great job {GetPlayerNameForMotherLine()}. Now bring it over here.";
	}

	private string GetPlayerNameForMotherLine()
	{
		return string.IsNullOrWhiteSpace(_gameState.PlayerName)
			? "there"
			: _gameState.PlayerName.Trim();
	}

	private bool TryGetStatusHighlightRect(out Rect2 highlightRect)
	{
		highlightRect = default;

		if (_hud is null)
			return false;

		var hasHighlightRect = false;
		foreach (var control in new Control?[] { _hud, _hudDateControl })
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

	private Control? FocusTutorialBrewPanel()
	{
		return _brewPanelFrame ?? _brewPanel;
	}

	private void UpdateTutorialButtonLock(TutorialStepContentResource stepContent, params BaseButton?[] allowedButtons)
	{
		if (!stepContent.LockOtherButtons)
		{
			_interactionGate.Restore();
			KeepAlwaysEnabledButtonsEnabled();
			return;
		}

		_interactionGate.Apply(
			new Node?[] { _hud, _brewPanel, _stationShelfInventory, _ingredientPreparationTray, _stationCustomerPanel, _daySummaryPanel },
			BuildAllowedButtonsWithAlwaysEnabledButtons(allowedButtons));
		KeepAlwaysEnabledButtonsEnabled();
	}

	private BaseButton?[] BuildAllowedButtonsWithAlwaysEnabledButtons(BaseButton?[] allowedButtons)
	{
		var rawButton = FocusRawPreparationButton();
		var extraButtonCount = 0;
		if (rawButton is not null)
			extraButtonCount += 1;
		if (_gardenButton is not null)
			extraButtonCount += 1;
		if (extraButtonCount == 0)
			return allowedButtons;

		var allowedButtonsWithAlwaysEnabled = new BaseButton?[allowedButtons.Length + extraButtonCount];
		allowedButtons.CopyTo(allowedButtonsWithAlwaysEnabled, 0);

		var index = allowedButtons.Length;
		if (rawButton is not null)
		{
			allowedButtonsWithAlwaysEnabled[index] = rawButton;
			index += 1;
		}

		if (_gardenButton is not null)
			allowedButtonsWithAlwaysEnabled[index] = _gardenButton;

		return allowedButtonsWithAlwaysEnabled;
	}

	private void KeepAlwaysEnabledButtonsEnabled()
	{
		KeepRawPreparationButtonEnabled();
		if (_gardenButton is not null)
			_gardenButton.Disabled = false;
	}

	private void KeepRawPreparationButtonEnabled()
	{
		var rawButton = FocusRawPreparationButton();
		if (rawButton is not null)
			rawButton.Disabled = false;
	}

	private BaseButton?[] GetAllowedButtonsForStep(TutorialStepId step)
	{
		return step switch
		{
			TutorialStepId.OpenBrewPanel => new BaseButton?[] { _settingsButton },
			TutorialStepId.QueueMint => new BaseButton?[] { GetAllowedButton(FocusIngredientShelfSlot(_tutorialContent.MintId)) },
			TutorialStepId.PrepareMintRaw => new BaseButton?[] { FocusRawPreparationButton() },
			TutorialStepId.QueueGorse => new BaseButton?[] { GetAllowedButton(FocusIngredientShelfSlot(_tutorialContent.GorseId)) },
			TutorialStepId.PrepareGorseRaw => new BaseButton?[] { FocusRawPreparationButton() },
			TutorialStepId.QueueThyme => new BaseButton?[] { GetAllowedButton(FocusIngredientShelfSlot(_tutorialContent.ThymeId)) },
			TutorialStepId.PrepareThymeRaw => new BaseButton?[] { FocusRawPreparationButton() },
			TutorialStepId.BrewPotion => new BaseButton?[] { _brewPanel?.GetBrewButton() },
			TutorialStepId.StartDay => new BaseButton?[] { _startDayButton },
			TutorialStepId.SellPotion => new BaseButton?[] { GetAllowedButton(FocusTutorialPotionInventorySlot()) },
			TutorialStepId.ConfirmServe => new BaseButton?[] { _stationCustomerPanel?.GetServeButton() },
			TutorialStepId.PostServeMotherDialogue => new BaseButton?[] { },
			TutorialStepId.NextCustomer => new BaseButton?[] { },
			TutorialStepId.CloseShop => new BaseButton?[] { },
			TutorialStepId.DaySummary => new BaseButton?[] { _daySummaryPanel?.GetContinueButton() },
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

	private Control? GetOptionalHudControl(NodePath path, string exportName)
	{
		if (_hud is null)
			return null;

		if (path.IsEmpty)
		{
			GD.PushError($"TutorialController: {exportName} is not assigned.");
			return null;
		}

		var control = _hud.GetNodeOrNull<Control>(path);
		if (control is null)
			GD.PushError($"TutorialController: HUD control was not found at '{path}'.");

		return control;
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
