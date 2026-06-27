using System;
using System.Collections.Generic;
using Godot;
using OccultShop.Autoload;
using OccultShop.Dialogue;
using OccultShop.Models;
using OccultShop.Systems;

namespace OccultShop.UI;

public partial class StationCustomerPanel : Control
{
	[Signal]
	public delegate void SaleResolvedEventHandler(bool success, int goldDelta, int dreadDelta, float finalScore, string grade);
	[Signal]
	public delegate void CustomerSkippedEventHandler();
	[Signal]
	public delegate void CustomerResolvedEventHandler();
	[Signal]
	public delegate void CustomerQueueEmptiedEventHandler();
	[Signal]
	public delegate void SaleResultClosedEventHandler();
	[Signal]
	public delegate void PotionSoldEventHandler(string itemId, bool success);
	[Signal]
	public delegate void PotionSelectedForServingEventHandler(string itemId);
	[Signal]
	public delegate void InteractionShownEventHandler(string interactionId);
	[Signal]
	public delegate void DialogueResolvedEventHandler();
	[Signal]
	public delegate void PlotConversationStartedEventHandler();
	[Signal]
	public delegate void MotherPostServeDialogueResolvedEventHandler();

	private const float CustomerSlideSeconds = 0.28f;
	private const float CustomerSlideDistance = 420.0f;
	private const float CustomerImageHeight = 104.0f;
	private const float DialogueMinimumHeight = 76.0f;
	private const float SelectedPotionFitMinimumHeight = 154.0f;
	private const string FailedServeConsequenceText = "Filler consequence text: this potion may disappoint the customer and affect future outcomes.";
	private const string RefuseConsequenceText = "Filler consequence text: refusing a customer may affect future outcomes.";

	[Export] public NodePath PotionInventoryRowPath = new("../PotionInventoryRow");
	[Export] public NodePath GameStatePath = new(AutoloadNodePaths.GameState);
	[Export] public NodePath ItemCatalogPath = new(AutoloadNodePaths.ItemCatalog);
	[Export] public NodePath ShopSessionStatePath = new(AutoloadNodePaths.ShopSessionState);
	[Export] public int DialogueTypewriterCharactersPerSecond = 45;

	private readonly List<CustomerInteractionDef> _customers = new();
	private readonly List<Button> _dialogueOptionButtons = new();
	private readonly List<DialogueOption> _visibleDialogueOptions = new();
	private readonly List<MotherPostServeDialogueOption> _motherPostServeDialogueOptions = new();

	private GameState _gameState = default!;
	private ItemCatalogService _itemCatalog = default!;
	private ShopSessionState _shopSessionState = default!;
	private PotionInventoryRow? _potionInventoryRow;
	private CustomerSaleService _saleService = default!;

	private Label _title = default!;
	private Control _customerImageFrame = default!;
	private TextureRect _customerImage = default!;
	private RichTextLabel _dialogue = default!;
	private VBoxContainer _dialogueOptionsContainer = default!;
	private Label _fitTitle = default!;
	private RichTextLabel _fitCheck = default!;
	private Label _servingTitle = default!;
	private CustomerSellDropBox _servingDropBox = default!;
	private Label _servingDropLabel = default!;
	private HBoxContainer _servingActions = default!;
	private Button _serveButton = default!;
	private Button _refuseButton = default!;
	private Button _returnToDialogueButton = default!;
	private Label _outcomeLabel = default!;
	private PanelContainer _confirmationPanel = default!;
	private Label _confirmationTitle = default!;
	private RichTextLabel _confirmationBody = default!;
	private Button _confirmPrimaryButton = default!;
	private Button _confirmCancelButton = default!;

	private int _activeIndex = -1;
	private int _resolvingCustomerIndex = -1;
	private string _selectedPotionItemId = string.Empty;
	private PotionResult? _selectedPotionResult;
	private string _pendingPotionItemId = string.Empty;
	private PotionResult? _pendingPotionResult;
	private ConfirmationKind _pendingConfirmation = ConfirmationKind.None;
	private NarrativeTextPresenter? _dialoguePresenter;
	private Control.GuiInputEventHandler? _dialogueGuiInputHandler;
	private DialogueSession? _dialogueSession;
	private CustomerDialogueAdapter? _customerDialogueAdapter;
	private string _requestReturnDialogueNodeId = string.Empty;
	private bool _sellingMode;
	private bool _isResolvingCustomer;
	private bool _isShowingMotherPostServeDialogue;
	private Tween? _slideTween;
	private static readonly Color SeenDialogueOptionModulate = new(0.58f, 0.58f, 0.58f, 1.0f);
	private static readonly Color DefaultButtonModulate = new(1.0f, 1.0f, 1.0f, 1.0f);

	public bool HasActiveInteraction => ActiveCustomer is not null;

	public override void _Ready()
	{
		var gameState = GetNodeOrNull<GameState>(GameStatePath);
		if (gameState is null)
		{
			GD.PushError($"StationCustomerPanel: GameState was not found at '{GameStatePath}'.");
			return;
		}

		var itemCatalog = GetNodeOrNull<ItemCatalogService>(ItemCatalogPath);
		if (itemCatalog is null)
		{
			GD.PushError($"StationCustomerPanel: ItemCatalog was not found at '{ItemCatalogPath}'.");
			return;
		}

		var shopSessionState = GetNodeOrNull<ShopSessionState>(ShopSessionStatePath);
		if (shopSessionState is null)
		{
			GD.PushError($"StationCustomerPanel: ShopSessionState was not found at '{ShopSessionStatePath}'.");
			return;
		}

		_gameState = gameState;
		_itemCatalog = itemCatalog;
		_shopSessionState = shopSessionState;
		_saleService = new CustomerSaleService(_gameState, _itemCatalog);
		_potionInventoryRow = GetNodeOrNull<PotionInventoryRow>(PotionInventoryRowPath);
		if (_potionInventoryRow is null)
			GD.PushError($"StationCustomerPanel: PotionInventoryRow was not found at '{PotionInventoryRowPath}'.");

		BuildUi();
		MouseFilter = MouseFilterEnum.Ignore;

		if (_potionInventoryRow is not null)
			_potionInventoryRow.PotionQuickServeRequested += OnPotionQuickServeRequested;
		_gameState.Changed += OnGameStateChanged;
		_servingDropBox.ItemDropped += OnServingPotionDropped;
		_servingDropBox.ItemHoverPreview += OnServingPotionHoverPreview;
		_servingDropBox.HoverPreviewCleared += OnServingPotionHoverCleared;
		_serveButton.Pressed += OnServePressed;
		_refuseButton.Pressed += OnRefusePressed;
		_returnToDialogueButton.Pressed += OnReturnToDialoguePressed;
		_confirmPrimaryButton.Pressed += OnConfirmPrimaryPressed;
		_confirmCancelButton.Pressed += HideConfirmation;

		ShowEmptyCustomerPresentation(clearActiveRequest: false);
	}

	public override void _ExitTree()
	{
		_slideTween?.Kill();
		if (_potionInventoryRow is not null)
			_potionInventoryRow.PotionQuickServeRequested -= OnPotionQuickServeRequested;
		if (_gameState is not null)
			_gameState.Changed -= OnGameStateChanged;
		if (_servingDropBox is not null)
		{
			_servingDropBox.ItemDropped -= OnServingPotionDropped;
			_servingDropBox.ItemHoverPreview -= OnServingPotionHoverPreview;
			_servingDropBox.HoverPreviewCleared -= OnServingPotionHoverCleared;
		}
		if (_serveButton is not null)
			_serveButton.Pressed -= OnServePressed;
		if (_refuseButton is not null)
			_refuseButton.Pressed -= OnRefusePressed;
		if (_returnToDialogueButton is not null)
			_returnToDialogueButton.Pressed -= OnReturnToDialoguePressed;
		if (_confirmPrimaryButton is not null)
			_confirmPrimaryButton.Pressed -= OnConfirmPrimaryPressed;
		if (_confirmCancelButton is not null)
			_confirmCancelButton.Pressed -= HideConfirmation;
		if (_dialoguePresenter is not null)
			_dialoguePresenter.LineStarted -= OnDialogueLineStarted;
		_dialoguePresenter?.Dispose();
		_dialoguePresenter = null;
		if (_dialogue is not null && _dialogueGuiInputHandler is not null)
			_dialogue.GuiInput -= _dialogueGuiInputHandler;
	}

	public void SetCustomers(IReadOnlyList<CustomerInteractionDef> customers)
	{
		_customers.Clear();
		if (customers is not null)
		{
			foreach (var customer in customers)
			{
				if (customer is not null)
					_customers.Add(customer);
			}
		}

		_activeIndex = _customers.Count > 0 ? 0 : -1;
		_isResolvingCustomer = false;
		_resolvingCustomerIndex = -1;
		ResetDialogueState();
		ClearSelectedPotion();
		HideConfirmation();
		RefreshActiveCustomer(emitShownSignal: true);
	}

	public void RestoreActiveCustomer(CustomerInteractionDef customer)
	{
		_customers.Clear();
		if (customer is not null)
			_customers.Add(customer);

		_activeIndex = _customers.Count > 0 ? 0 : -1;
		_isResolvingCustomer = false;
		_resolvingCustomerIndex = -1;
		ResetDialogueState();
		ClearSelectedPotion();
		HideConfirmation();
		RefreshActiveCustomer(emitShownSignal: false, restorePublishedRequest: true);
	}

	public void ClearCustomers()
	{
		_customers.Clear();
		_activeIndex = -1;
		_isResolvingCustomer = false;
		_resolvingCustomerIndex = -1;
		ShowEmptyCustomerPresentation(clearActiveRequest: true);
	}

	public Control? GetVisiblePotionSlot(string itemId)
	{
		return _potionInventoryRow?.GetVisiblePotionSlot(itemId);
	}

	public Control? GetServingDropBox()
	{
		return _servingDropBox;
	}

	public Button? GetServeButton()
	{
		return _serveButton;
	}

	public void ShowTutorialMotherLine(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
			return;

		_dialoguePresenter?.AddHistoryLine(new NarrativeTextLine(
			MotherPostServeDialogueFlow.MotherSpeakerName,
			text,
			allowMarkup: false));
	}

	public void RefreshSlotLayoutSettings()
	{
		_potionInventoryRow?.RefreshSlotLayoutSettings();
	}

	private CustomerInteractionDef? ActiveCustomer =>
		_activeIndex >= 0 && _activeIndex < _customers.Count
			? _customers[_activeIndex]
			: null;

	private void BuildUi()
	{
		foreach (var child in GetChildren())
		{
			RemoveChild(child);
			child.QueueFree();
		}

		var root = new PanelContainer
		{
			Name = "Panel",
			AnchorLeft = 0.0f,
			AnchorTop = 0.0f,
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f,
			MouseFilter = MouseFilterEnum.Ignore
		};
		root.AddThemeStyleboxOverride("panel", CreatePanelStyleBox(new Color(0.07f, 0.03f, 0.09f, 0.90f)));
		AddChild(root);

		var margin = new MarginContainer { Name = "Margin", MouseFilter = MouseFilterEnum.Ignore };
		margin.AddThemeConstantOverride("margin_left", 10);
		margin.AddThemeConstantOverride("margin_top", 8);
		margin.AddThemeConstantOverride("margin_right", 10);
		margin.AddThemeConstantOverride("margin_bottom", 8);
		root.AddChild(margin);

		var vbox = new VBoxContainer { Name = "VBox", MouseFilter = MouseFilterEnum.Ignore };
		vbox.AddThemeConstantOverride("separation", 6);
		margin.AddChild(vbox);

		_dialogue = new RichTextLabel
		{
			Name = "RequestText",
			BbcodeEnabled = true,
			FitContent = false,
			ScrollActive = true,
			CustomMinimumSize = new Vector2(0.0f, DialogueMinimumHeight),
			SizeFlagsVertical = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Stop
		};
		vbox.AddChild(_dialogue);
		_dialoguePresenter = new NarrativeTextPresenter(this, _dialogue)
		{
			DefaultCharactersPerSecond = DialogueTypewriterCharactersPerSecond
		};
		_dialoguePresenter.LineStarted += OnDialogueLineStarted;
		_dialogueGuiInputHandler = OnDialogueGuiInput;
		_dialogue.GuiInput += _dialogueGuiInputHandler;

		_dialogueOptionsContainer = new VBoxContainer
		{
			Name = "DialogueOptions",
			Visible = false
		};
		_dialogueOptionsContainer.AddThemeConstantOverride("separation", 5);
		vbox.AddChild(_dialogueOptionsContainer);

		for (var index = 0; index < CustomerInteractionDef.MaxDialogueOptionsPerNode; index += 1)
		{
			var optionIndex = index;
			var button = new Button
			{
				Name = $"DialogueOption{index + 1}",
				Visible = false,
				SizeFlagsHorizontal = SizeFlags.ExpandFill
			};
			button.Pressed += () => TrySelectDialogueOption(optionIndex);
			_dialogueOptionsContainer.AddChild(button);
			_dialogueOptionButtons.Add(button);
		}

		_fitTitle = new Label { Name = "FitTitle", Text = "Selected Potion Fit" };
		vbox.AddChild(_fitTitle);

		_fitCheck = new RichTextLabel
		{
			Name = "FitCheck",
			BbcodeEnabled = true,
			FitContent = false,
			ScrollActive = true,
			CustomMinimumSize = new Vector2(0.0f, SelectedPotionFitMinimumHeight),
			SizeFlagsVertical = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Stop
		};
		vbox.AddChild(_fitCheck);

		_title = new Label
		{
			Name = "Title",
			Text = "No customer",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_title.AddThemeFontSizeOverride("font_size", 22);
		vbox.AddChild(_title);

		_customerImageFrame = new Control
		{
			Name = "CustomerImageFrame",
			CustomMinimumSize = new Vector2(0.0f, CustomerImageHeight),
			ClipContents = true,
			MouseFilter = MouseFilterEnum.Ignore
		};
		vbox.AddChild(_customerImageFrame);

		_customerImage = new TextureRect
		{
			Name = "CustomerImage",
			Position = Vector2.Zero,
			Size = new Vector2(360.0f, CustomerImageHeight),
			MouseFilter = MouseFilterEnum.Ignore,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
		};
		_customerImageFrame.AddChild(_customerImage);

		_servingTitle = new Label { Name = "ServingTitle", Text = "Serving Slot" };
		vbox.AddChild(_servingTitle);
		_servingDropBox = CreateServingDropBox();
		vbox.AddChild(_servingDropBox);

		_servingActions = new HBoxContainer { Name = "Actions" };
		_servingActions.AddThemeConstantOverride("separation", 8);
		vbox.AddChild(_servingActions);

		_serveButton = new Button
		{
			Name = "Serve",
			Text = "Serve",
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		_servingActions.AddChild(_serveButton);

		_refuseButton = new Button
		{
			Name = "Refuse",
			Text = "Refuse",
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		_servingActions.AddChild(_refuseButton);

		_returnToDialogueButton = new Button
		{
			Name = "ReturnToDialogue",
			Text = "Return to dialogue",
			Visible = false,
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		_servingActions.AddChild(_returnToDialogueButton);

		_outcomeLabel = new Label
		{
			Name = "Outcome",
			Text = "",
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		vbox.AddChild(_outcomeLabel);

		BuildConfirmationPanel();
	}

	private CustomerSellDropBox CreateServingDropBox()
	{
		var dropBox = new CustomerSellDropBox
		{
			Name = "ServingDropBox",
			CustomMinimumSize = new Vector2(0.0f, 50.0f),
			MouseFilter = MouseFilterEnum.Stop
		};
		dropBox.AddThemeStyleboxOverride("panel", CreatePanelStyleBox(new Color(0.06f, 0.05f, 0.07f, 0.86f)));

		var dropMargin = new MarginContainer { Name = "DropMargin" };
		dropMargin.AddThemeConstantOverride("margin_left", 10);
		dropMargin.AddThemeConstantOverride("margin_top", 10);
		dropMargin.AddThemeConstantOverride("margin_right", 10);
		dropMargin.AddThemeConstantOverride("margin_bottom", 10);
		dropBox.AddChild(dropMargin);

		_servingDropLabel = new Label
		{
			Name = "DropLabel",
			Text = "Drop potion here",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		dropMargin.AddChild(_servingDropLabel);
		return dropBox;
	}

	private void BuildConfirmationPanel()
	{
		_confirmationPanel = new PanelContainer
		{
			Name = "ConfirmationPanel",
			Visible = false,
			ZIndex = 50,
			AnchorLeft = 0.04f,
			AnchorTop = 0.22f,
			AnchorRight = 0.96f,
			AnchorBottom = 0.78f,
			MouseFilter = MouseFilterEnum.Stop
		};
		_confirmationPanel.AddThemeStyleboxOverride("panel", CreatePanelStyleBox(new Color(0.08f, 0.04f, 0.10f, 0.98f)));
		AddChild(_confirmationPanel);

		var margin = new MarginContainer { Name = "Margin" };
		margin.AddThemeConstantOverride("margin_left", 16);
		margin.AddThemeConstantOverride("margin_top", 14);
		margin.AddThemeConstantOverride("margin_right", 16);
		margin.AddThemeConstantOverride("margin_bottom", 14);
		_confirmationPanel.AddChild(margin);

		var vbox = new VBoxContainer { Name = "VBox" };
		vbox.AddThemeConstantOverride("separation", 10);
		margin.AddChild(vbox);

		_confirmationTitle = new Label { Name = "Title", Text = "Confirm" };
		_confirmationTitle.AddThemeFontSizeOverride("font_size", 20);
		vbox.AddChild(_confirmationTitle);

		_confirmationBody = new RichTextLabel
		{
			Name = "Body",
			BbcodeEnabled = true,
			FitContent = false,
			CustomMinimumSize = new Vector2(0.0f, 150.0f),
			MouseFilter = MouseFilterEnum.Stop
		};
		vbox.AddChild(_confirmationBody);

		var actions = new HBoxContainer { Name = "Actions" };
		actions.AddThemeConstantOverride("separation", 8);
		vbox.AddChild(actions);

		_confirmPrimaryButton = new Button
		{
			Name = "Confirm",
			Text = "Confirm",
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		actions.AddChild(_confirmPrimaryButton);

		_confirmCancelButton = new Button
		{
			Name = "Cancel",
			Text = "Cancel",
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		actions.AddChild(_confirmCancelButton);
	}

	private static StyleBoxFlat CreatePanelStyleBox(Color bgColor)
	{
		return new StyleBoxFlat
		{
			BgColor = bgColor,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			BorderColor = new Color(0.54f, 0.38f, 0.20f, 0.70f),
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomRight = 8,
			CornerRadiusBottomLeft = 8
		};
	}

	private void RefreshActiveCustomer(bool emitShownSignal, bool restorePublishedRequest = false)
	{
		var interaction = ActiveCustomer;
		if (interaction is null)
		{
			ShowEmptyCustomerPresentation(clearActiveRequest: false);
			return;
		}

		ResetDialogueState();
		ClearSelectedPotion();
		HideConfirmation();
		_outcomeLabel.Text = string.Empty;
		_slideTween?.Kill();
		_customerImage.Position = Vector2.Zero;
		_customerImage.Modulate = Colors.White;
		_title.Text = string.IsNullOrWhiteSpace(interaction.Title) ? "Customer" : interaction.Title;
		RefreshCustomerImage(interaction);

		if (restorePublishedRequest && TryRestorePublishedRequest(interaction, emitShownSignal))
			return;

		if (TryShowDialogueStart(interaction))
		{
			_shopSessionState.ClearActiveCustomerRequest();
			SetServingControlsVisible(false);
			SetServingControlsEnabled(false);
			EmitSignal(SignalName.PlotConversationStarted);
			if (emitShownSignal)
				EmitSignal(SignalName.InteractionShown, interaction.Id);
			return;
		}

		_dialoguePresenter?.SetHistory(BuildAuthoredNarrativeLines(
			interaction.Lines,
			interaction.Text,
			CustomerDialogueTextFormatter.CustomerSpeakerName));

		var request = interaction.BuildRequest();
		_shopSessionState.SetActiveCustomerRequest(request);
		_sellingMode = true;
		SetServingControlsVisible(true);
		SetServingControlsEnabled(true);
		RefreshSelectedPotionComparison();

		if (emitShownSignal)
			EmitSignal(SignalName.InteractionShown, interaction.Id);
	}

	private static string BuildRequestText(CustomerInteractionDef interaction)
	{
		if (interaction.Lines.Count == 0)
			return string.IsNullOrWhiteSpace(interaction.Text) ? "..." : interaction.Text;

		var lines = new List<string>();
		foreach (var line in interaction.Lines)
		{
			if (string.IsNullOrWhiteSpace(line.Text))
				continue;
			lines.Add(string.IsNullOrWhiteSpace(line.Speaker)
				? line.Text
				: $"{line.Speaker}: {line.Text}");
		}

		return lines.Count == 0 ? interaction.Text : string.Join("\n", lines);
	}

	private void ResetDialogueState()
	{
		_dialoguePresenter?.Clear();
		_visibleDialogueOptions.Clear();
		_motherPostServeDialogueOptions.Clear();
		_dialogueSession = null;
		_customerDialogueAdapter = null;
		_requestReturnDialogueNodeId = string.Empty;
		_sellingMode = false;
		_isShowingMotherPostServeDialogue = false;
		if (_dialogueOptionsContainer is not null)
			_dialogueOptionsContainer.Visible = false;
		foreach (var button in _dialogueOptionButtons)
		{
			button.Visible = false;
			button.Disabled = true;
			button.Modulate = DefaultButtonModulate;
		}
		if (_returnToDialogueButton is not null)
			_returnToDialogueButton.Visible = false;
	}

	private void SetServingControlsVisible(bool visible)
	{
		if (_fitTitle is not null)
			_fitTitle.Visible = visible;
		if (_fitCheck is not null)
			_fitCheck.Visible = visible;
		if (_servingTitle is not null)
			_servingTitle.Visible = visible;
		if (_servingDropBox is not null)
			_servingDropBox.Visible = visible;
		if (_servingActions is not null)
			_servingActions.Visible = visible;
		if (_returnToDialogueButton is not null)
		{
			_returnToDialogueButton.Visible =
				visible &&
				HasActiveDialogueInteraction() &&
				_sellingMode &&
				!string.IsNullOrWhiteSpace(_requestReturnDialogueNodeId);
		}
	}

	private void SetServingControlsEnabled(bool enabled)
	{
		if (_serveButton is not null)
			_serveButton.Disabled = !enabled;
		if (_refuseButton is not null)
			_refuseButton.Disabled = !enabled;
		if (_returnToDialogueButton is not null)
			_returnToDialogueButton.Disabled = !enabled || !HasActiveDialogueInteraction();
		if (_servingDropBox is not null)
		{
			_servingDropBox.SetAcceptDrops(enabled);
			_servingDropBox.SetDisabledVisual(!enabled);
		}
	}

	private bool HasActiveDialogueInteraction()
	{
		return _dialogueSession is { IsActive: true } && _customerDialogueAdapter is not null;
	}

	private void ShowEmptyCustomerPresentation(bool clearActiveRequest)
	{
		if (clearActiveRequest)
			_shopSessionState.ClearActiveCustomerRequest();

		ResetDialogueState();
		ClearSelectedPotion();
		HideConfirmation();
		_title.Text = "No customer waiting";
		if (_dialoguePresenter is not null)
			_dialoguePresenter.Clear();
		_dialogue.Text = "No active customer.";
		_fitCheck.Text = "Select a customer and potion.";
		_outcomeLabel.Text = string.Empty;
		_customerImage.Texture = null;
		_customerImage.Visible = false;
		SetServingControlsVisible(true);
		SetServingControlsEnabled(false);
	}

	private bool TryRestorePublishedRequest(CustomerInteractionDef interaction, bool emitShownSignal)
	{
		var request = _shopSessionState.ActiveCustomerRequest;
		if (request is null ||
			!string.Equals(request.Id, interaction.Id, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		_dialoguePresenter?.SetHistory(BuildAuthoredNarrativeLines(
			interaction.Lines,
			interaction.Text,
			CustomerDialogueTextFormatter.CustomerSpeakerName));

		_sellingMode = true;
		SetServingControlsVisible(true);
		SetServingControlsEnabled(true);
		RefreshSelectedPotionComparison();

		if (emitShownSignal)
			EmitSignal(SignalName.InteractionShown, interaction.Id);

		return true;
	}

	private static List<NarrativeTextLine> BuildAuthoredNarrativeLines(
		IReadOnlyList<CustomerDialogueLineDef> lines,
		string fallbackText,
		string? fallbackSpeaker)
	{
		return CustomerNarrativeLineBuilder.BuildAuthoredNarrativeLines(lines, fallbackText, fallbackSpeaker);
	}

	private void QueueAuthoredLines(
		IReadOnlyList<CustomerDialogueLineDef> lines,
		string fallbackText,
		string? fallbackSpeaker)
	{
		if (_dialoguePresenter is null)
			return;

		foreach (var line in BuildAuthoredNarrativeLines(lines, fallbackText, fallbackSpeaker))
			_dialoguePresenter.QueueLine(line);
	}

	private void QueueDialogueLines(
		IReadOnlyList<DialogueLine> lines,
		string fallbackText,
		string? fallbackSpeaker)
	{
		if (_dialoguePresenter is null)
			return;

		foreach (var line in DialogueNarrativeLineBuilder.BuildNarrativeLines(lines, fallbackText, fallbackSpeaker))
			_dialoguePresenter.QueueLine(line);
	}

	private void QueuePlayerLine(string text)
	{
		_dialoguePresenter?.QueueLine(new NarrativeTextLine(
			CustomerDialogueTextFormatter.PlayerSpeakerName,
			text,
			allowMarkup: false));
	}

	private void QueueCustomerLine(string text, bool allowMarkup)
	{
		_dialoguePresenter?.QueueLine(new NarrativeTextLine(
			CustomerDialogueTextFormatter.CustomerSpeakerName,
			text,
			allowMarkup));
	}

	private void PlayQueuedDialogueLines(Action? completedAction)
	{
		if (_dialoguePresenter is null)
		{
			completedAction?.Invoke();
			return;
		}

		_dialoguePresenter.DefaultCharactersPerSecond = DialogueTypewriterCharactersPerSecond;
		_dialoguePresenter.PlayQueued(completedAction);
	}

	private void StopQueuedDialoguePresentation()
	{
		_dialoguePresenter?.StopQueuedPresentation();
	}

	private void AdvanceQueuedDialoguePresentation()
	{
		_dialoguePresenter?.AdvanceQueuedPresentation();
	}

	private void OnDialogueGuiInput(InputEvent @event)
	{
		if (!HasActiveDialogueInteraction() && !_isShowingMotherPostServeDialogue)
			return;
		if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
			return;

		AcceptEvent();
		AdvanceQueuedDialoguePresentation();
	}

	private void OnDialogueLineStarted(NarrativeTextLine line)
	{
		var interaction = ActiveCustomer;
		if (interaction is null)
			return;

		RefreshCustomerImage(interaction, line.CharacterImageKey);
	}

	private void RefreshCustomerImage(CustomerInteractionDef interaction, string characterImageKey = "")
	{
		var imagePath = interaction.CharacterImagePath;
		if (!string.IsNullOrWhiteSpace(characterImageKey))
		{
			var trimmedKey = characterImageKey.Trim();
			if (interaction.CharacterImagePaths.TryGetValue(trimmedKey, out var keyedImagePath) &&
				!string.IsNullOrWhiteSpace(keyedImagePath))
			{
				imagePath = keyedImagePath;
			}
			else
			{
				GD.PushError($"StationCustomerPanel: Customer interaction '{interaction.Id}' references unknown character image key '{trimmedKey}'.");
			}
		}

		if (string.IsNullOrWhiteSpace(imagePath))
		{
			_customerImage.Texture = null;
			_customerImage.Visible = false;
			return;
		}

		var texture = ResourceLoader.Load<Texture2D>(imagePath);
		if (texture is null)
		{
			GD.PushError($"StationCustomerPanel: Customer image could not be loaded from '{imagePath}'.");
			_customerImage.Texture = null;
			_customerImage.Visible = false;
			return;
		}

		_customerImage.Texture = texture;
		_customerImage.Visible = true;
	}

	private bool TryShowDialogueStart(CustomerInteractionDef interaction)
	{
		if (!CustomerDialogueAdapter.TryCreate(interaction, _gameState, out var adapter, out var error) ||
			adapter is null)
		{
			if (!string.IsNullOrWhiteSpace(error))
				GD.PushError($"StationCustomerPanel: {error}");
			return false;
		}

		var session = new DialogueSession(
			adapter.Graph,
			adapter.IsOptionAvailable,
			CustomerInteractionDef.MaxDialogueOptionsPerNode);
		if (!session.TryStart(out var startNode) || startNode is null)
		{
			GD.PushError($"StationCustomerPanel: Story customer '{interaction.Id}' has dialogue data but no valid start node.");
			return false;
		}

		_customerDialogueAdapter = adapter;
		_dialogueSession = session;
		ShowDialogueNode(startNode);
		return true;
	}

	private void ShowDialogueNode(DialogueNode node)
	{
		SetDialoguePresentationState();
		QueueDialogueNodeText(node);
		PlayQueuedDialogueLines(() => FinishShowingDialogueNode(node));
	}

	private void QueueDialogueNodeText(DialogueNode node)
	{
		QueueDialogueLines(node.Lines, node.Text, null);
	}

	private void QueueDialogueOptionResponse(DialogueOption option)
	{
		QueueDialogueLines(
			option.ResponseLines,
			option.ResponseText,
			CustomerDialogueTextFormatter.CustomerSpeakerName);
	}

	private static bool HasDialogueOptionResponse(DialogueOption option)
	{
		return option.HasResponse;
	}

	private void FinishShowingDialogueNode(DialogueNode node)
	{
		if (node.Options.Count == 0)
		{
			CompleteDialogueInteraction("dialogue_complete");
			return;
		}

		SetDialogueOptionState(node);
	}

	private bool TrySelectDialogueOption(int optionIndex)
	{
		if (_isShowingMotherPostServeDialogue)
			return TrySelectMotherPostServeDialogueOption(optionIndex);

		if (!HasActiveDialogueInteraction())
			return false;

		var session = _dialogueSession;
		var adapter = _customerDialogueAdapter;
		var node = session?.ActiveNode;
		if (session is null || adapter is null || node is null)
		{
			GD.PushError("StationCustomerPanel: Active dialogue session was not found.");
			return true;
		}

		if (!session.TrySelectVisibleOption(optionIndex, out var option) || option is null)
			return true;

		SetDialoguePresentationState();
		QueuePlayerLine(option.Label);
		adapter.RecordOptionSelected(option);
		adapter.ApplyOptionEffects(option);
		QueueDialogueOptionResponse(option);

		if (adapter.RevealsRequest(option))
		{
			if (!HasDialogueOptionResponse(option))
				QueueDialogueLines(adapter.RequestLines, adapter.RequestText, CustomerDialogueTextFormatter.CustomerSpeakerName);
			PlayQueuedDialogueLines(() => EnterPotionSellingMode(option));
			return true;
		}

		if (adapter.ReturnsToDialogue(option))
		{
			if (!session.TryResolveReturnNode(option, _requestReturnDialogueNodeId, out var returnNode, out var error) ||
				returnNode is null)
			{
				GD.PushError($"StationCustomerPanel: {error}");
				CompleteDialogueInteraction(adapter.BuildOutcome(option));
				return true;
			}

			QueueDialogueNodeText(returnNode);
			PlayQueuedDialogueLines(() => FinishShowingDialogueNode(returnNode));
			return true;
		}

		if (option.EndsDialogue)
		{
			PlayQueuedDialogueLines(() => CompleteDialogueInteraction(adapter.BuildOutcome(option)));
			return true;
		}

		if (!string.IsNullOrWhiteSpace(option.NextNodeId))
		{
			if (!session.TryMoveToNextNode(option, out var nextNode, out _ ) || nextNode is null)
			{
				GD.PushError($"StationCustomerPanel: Dialogue option '{option.Id}' points to missing node '{option.NextNodeId}'.");
				CompleteDialogueInteraction(adapter.BuildOutcome(option));
				return true;
			}

			QueueDialogueNodeText(nextNode);
			PlayQueuedDialogueLines(() => FinishShowingDialogueNode(nextNode));
			return true;
		}

		PlayQueuedDialogueLines(() => SetDialogueOptionState(node));
		return true;
	}

	private void EnterPotionSellingMode(DialogueOption option)
	{
		var interaction = ActiveCustomer;
		if (interaction is null)
			return;

		_sellingMode = true;
		_requestReturnDialogueNodeId = !string.IsNullOrWhiteSpace(option.ReturnNodeId)
			? option.ReturnNodeId
			: _dialogueSession?.ActiveNodeId ?? string.Empty;

		_shopSessionState.SetActiveCustomerRequest(interaction.BuildRequest());
		SetSellingModeState();
		RefreshSelectedPotionComparison();
	}

	private void OnReturnToDialoguePressed()
	{
		if (!HasActiveDialogueInteraction())
			return;

		ClearSelectedPotion();
		StopQueuedDialoguePresentation();
		SetDialoguePresentationState();
		QueuePlayerLine(_returnToDialogueButton.Text);
		_shopSessionState.ClearActiveCustomerRequest();
		_sellingMode = false;

		var session = _dialogueSession;
		if (session is null ||
			!session.TryMoveToNode(_requestReturnDialogueNodeId, out var returnNode, out _) ||
			returnNode is null)
		{
			GD.PushError($"StationCustomerPanel: Cannot return to dialogue node '{_requestReturnDialogueNodeId}'.");
			return;
		}

		QueueDialogueNodeText(returnNode);
		PlayQueuedDialogueLines(() => FinishShowingDialogueNode(returnNode));
	}

	private void CompleteDialogueInteraction(string outcome)
	{
		var interaction = ActiveCustomer;
		if (interaction is null || _isResolvingCustomer)
			return;

		_gameState.RecordStoryCustomerInteractionOutcome(interaction, outcome);
		_shopSessionState.ClearActiveCustomerRequest();
		_sellingMode = false;
		SetServingControlsVisible(false);
		SetServingControlsEnabled(false);
		EmitSignal(SignalName.DialogueResolved);
		BeginResolveActiveCustomer();
	}

	private void SetDialogueOptionState(DialogueNode node)
	{
		_visibleDialogueOptions.Clear();
		_dialogueOptionsContainer.Visible = true;
		SetServingControlsVisible(false);
		SetServingControlsEnabled(false);

		var session = _dialogueSession;
		if (session is not null)
			_visibleDialogueOptions.AddRange(session.RefreshVisibleOptions());

		if (_visibleDialogueOptions.Count == 0)
		{
			CompleteDialogueInteraction("dialogue_no_options");
			return;
		}

		for (var index = 0; index < _dialogueOptionButtons.Count; index += 1)
			SetDialogueOptionButton(_dialogueOptionButtons[index], index);
	}

	private void SetDialoguePresentationState()
	{
		_dialogueOptionsContainer.Visible = false;
		foreach (var button in _dialogueOptionButtons)
			button.Visible = false;
		SetServingControlsVisible(false);
		SetServingControlsEnabled(false);
	}

	private void SetDialogueOptionButton(Button button, int optionIndex)
	{
		if (optionIndex < 0 || optionIndex >= _visibleDialogueOptions.Count)
		{
			button.Visible = false;
			button.Disabled = true;
			return;
		}

		var option = _visibleDialogueOptions[optionIndex];
		button.Text = option.Label;
		button.Visible = true;
		button.Disabled = false;
		button.Modulate = _customerDialogueAdapter?.HasOptionBeenSelected(option) == true
				? SeenDialogueOptionModulate
				: DefaultButtonModulate;
	}

	private bool TryBeginMotherPostServeDialogue(CustomerInteractionDef interaction, bool saleSucceeded)
	{
		if (!MotherPostServeDialogueFlow.ShouldBegin(interaction, saleSucceeded))
			return false;

		_isShowingMotherPostServeDialogue = true;
		_motherPostServeDialogueOptions.Clear();
		_motherPostServeDialogueOptions.AddRange(MotherPostServeDialogueFlow.BuildOptions());

		_dialoguePresenter?.Clear();
		_visibleDialogueOptions.Clear();
		_dialogueSession = null;
		_customerDialogueAdapter = null;
		_requestReturnDialogueNodeId = string.Empty;
		_sellingMode = false;
		_outcomeLabel.Text = string.Empty;
		SetDialoguePresentationState();

		_dialoguePresenter?.QueueLine(new NarrativeTextLine(
			MotherPostServeDialogueFlow.MotherSpeakerName,
			MotherPostServeDialogueFlow.BuildThankYouText(_gameState.PlayerName),
			allowMarkup: false));
		PlayQueuedDialogueLines(ShowMotherPostServeDialogueOptions);
		return true;
	}

	private void ShowMotherPostServeDialogueOptions()
	{
		if (!_isShowingMotherPostServeDialogue)
			return;

		_dialogueOptionsContainer.Visible = true;
		SetServingControlsVisible(false);
		SetServingControlsEnabled(false);

		for (var index = 0; index < _dialogueOptionButtons.Count; index += 1)
			SetMotherPostServeDialogueOptionButton(_dialogueOptionButtons[index], index);
	}

	private void SetMotherPostServeDialogueOptionButton(Button button, int optionIndex)
	{
		if (optionIndex < 0 || optionIndex >= _motherPostServeDialogueOptions.Count)
		{
			button.Visible = false;
			button.Disabled = true;
			return;
		}

		var option = _motherPostServeDialogueOptions[optionIndex];
		button.Text = option.Label;
		button.Visible = true;
		button.Disabled = false;
		button.Modulate = DefaultButtonModulate;
	}

	private bool TrySelectMotherPostServeDialogueOption(int optionIndex)
	{
		if (optionIndex < 0 || optionIndex >= _motherPostServeDialogueOptions.Count)
			return true;

		var option = _motherPostServeDialogueOptions[optionIndex];
		SetDialoguePresentationState();
		QueuePlayerLine(option.Label);
		_dialoguePresenter?.QueueLine(new NarrativeTextLine(
			MotherPostServeDialogueFlow.MotherSpeakerName,
			option.ResponseText,
			allowMarkup: false));
		PlayQueuedDialogueLines(FinishMotherPostServeDialogue);
		return true;
	}

	private void FinishMotherPostServeDialogue()
	{
		if (!_isShowingMotherPostServeDialogue)
			return;

		_isShowingMotherPostServeDialogue = false;
		_motherPostServeDialogueOptions.Clear();
		EmitSignal(SignalName.MotherPostServeDialogueResolved);
		BeginResolveActiveCustomer();
	}

	private void SetSellingModeState()
	{
		_dialogueOptionsContainer.Visible = false;
		foreach (var button in _dialogueOptionButtons)
			button.Visible = false;
		SetServingControlsVisible(true);
		SetServingControlsEnabled(true);
	}

	private void OnGameStateChanged()
	{
		if (string.IsNullOrWhiteSpace(_selectedPotionItemId))
			return;
		if (_gameState.HasItem(_selectedPotionItemId, 1) && _itemCatalog.IsPotion(_selectedPotionItemId))
		{
			RefreshSelectedPotionComparison();
			return;
		}

		ClearSelectedPotion();
		RefreshSelectedPotionComparison();
	}

	private void OnPotionQuickServeRequested(string itemId)
	{
		TrySelectPotion(itemId);
	}

	private void OnServingPotionDropped(string itemId)
	{
		TrySelectPotion(itemId);
	}

	private void OnServingPotionHoverPreview(string itemId)
	{
		var interaction = ActiveCustomer;
		if (!CanServeActiveCustomer() || interaction is null || !_itemCatalog.IsPotion(itemId))
		{
			_servingDropBox.SetHoverHighlight(false);
			return;
		}

		if (!_saleService.TryEvaluatePotion(interaction, itemId, out var brewResult) || brewResult is null)
		{
			_servingDropBox.SetHoverHighlight(false);
			return;
		}

		_servingDropBox.SetHoverHighlight(true);
		var request = interaction.BuildRequest();
		if (!request.HideRequestDetails)
			SetRequestFitText(request, itemId, brewResult);
	}

	private void OnServingPotionHoverCleared()
	{
		_servingDropBox.SetHoverHighlight(false);
		RefreshSelectedPotionComparison();
	}

	private void TrySelectPotion(string itemId)
	{
		if (ActiveCustomer is null)
		{
			CursorToast.Show(this, "No customer selected.");
			return;
		}

		if (!CanServeActiveCustomer())
		{
			CursorToast.Show(this, "Finish the conversation first.");
			return;
		}

		if (string.IsNullOrWhiteSpace(itemId) || !_itemCatalog.IsPotion(itemId))
		{
			CursorToast.Show(this, "Only potions can be served.");
			return;
		}

		if (!_gameState.HasItem(itemId, 1))
		{
			CursorToast.Show(this, "That potion is no longer in inventory.");
			return;
		}

		if (!_saleService.TryEvaluatePotion(ActiveCustomer, itemId, out var brewResult) || brewResult is null)
		{
			CursorToast.Show(this, "Could not evaluate that potion.");
			return;
		}

		_selectedPotionItemId = itemId;
		_selectedPotionResult = brewResult;
		_servingDropLabel.Text = BuildSelectedPotionLabel(itemId);
		RefreshSelectedPotionComparison();
		EmitSignal(SignalName.PotionSelectedForServing, itemId);
	}

	private string BuildSelectedPotionLabel(string itemId)
	{
		return StationCustomerPotionPresentation.BuildSelectedPotionLabel(
			_itemCatalog.GetItemName(itemId),
			_gameState.GetPotionDisplayName(itemId));
	}

	private void RefreshSelectedPotionComparison()
	{
		var interaction = ActiveCustomer;
		if (interaction is null)
			return;

		var request = interaction.BuildRequest();
		if (string.IsNullOrWhiteSpace(_selectedPotionItemId) ||
			_selectedPotionResult is null ||
			!_gameState.HasItem(_selectedPotionItemId, 1))
		{
			_selectedPotionResult = null;
			_servingDropLabel.Text = StationCustomerPotionPresentation.EmptyPotionDropLabel;
			SetRequestFitText(request, string.Empty, null);
			return;
		}

		if (request.HideRequestDetails)
		{
			_fitCheck.Text = StationCustomerPotionPresentation.BuildHiddenRequestFitText();
			return;
		}

		if (!_saleService.TryEvaluatePotion(interaction, _selectedPotionItemId, out var brewResult) || brewResult is null)
		{
			ClearSelectedPotion();
			SetRequestFitText(request, string.Empty, null);
			return;
		}

		_selectedPotionResult = brewResult;
		SetRequestFitText(request, _selectedPotionItemId, brewResult);
	}

	private void SetRequestFitText(CustomerRequestDef request, string potionItemId, PotionResult? brewResult)
	{
		var potionIngredients = string.IsNullOrWhiteSpace(potionItemId)
			? null
			: _saleService.GetPotionIngredientPortions(potionItemId);
		_fitCheck.Text = StationCustomerPotionPresentation.BuildRequestFitText(
			request,
			brewResult,
			potionIngredients,
			potionItemId);
	}

	private void ClearSelectedPotion()
	{
		_selectedPotionItemId = string.Empty;
		_selectedPotionResult = null;
		if (_servingDropLabel is not null)
			_servingDropLabel.Text = StationCustomerPotionPresentation.EmptyPotionDropLabel;
	}

	private void OnServePressed()
	{
		var interaction = ActiveCustomer;
		if (interaction is null)
			return;

		if (!CanServeActiveCustomer())
			return;

		if (string.IsNullOrWhiteSpace(_selectedPotionItemId) || _selectedPotionResult is null)
		{
			CursorToast.Show(this, "Select a potion to serve.");
			return;
		}

		var request = interaction.BuildRequest();
		if (request.HideRequestDetails)
		{
			ResolveSale(_selectedPotionItemId, _selectedPotionResult);
			return;
		}

		var isSuccess = _saleService.IsRequestSatisfiedByPotion(_selectedPotionItemId, request, _selectedPotionResult);
		if (isSuccess)
		{
			ResolveSale(_selectedPotionItemId, _selectedPotionResult);
			return;
		}

		ShowConfirmation(
			"Serve anyway?",
			$"This potion does not satisfy the request.\n\n{FailedServeConsequenceText}",
			"Serve Anyway",
			ConfirmationKind.FailedServe,
			_selectedPotionItemId,
			_selectedPotionResult);
	}

	private void OnRefusePressed()
	{
		if (ActiveCustomer is null)
			return;
		if (!CanServeActiveCustomer())
			return;

		ShowConfirmation(
			"Refuse customer?",
			$"Refuse this customer without serving a potion?\n\n{RefuseConsequenceText}",
			"Refuse",
			ConfirmationKind.Refuse,
			string.Empty,
			null);
	}

	private bool CanServeActiveCustomer()
	{
		return !HasActiveDialogueInteraction() || _sellingMode;
	}

	private void ShowConfirmation(
		string title,
		string body,
		string confirmText,
		ConfirmationKind kind,
		string potionItemId,
		PotionResult? brewResult)
	{
		_pendingConfirmation = kind;
		_pendingPotionItemId = potionItemId;
		_pendingPotionResult = brewResult;
		_confirmationTitle.Text = title;
		_confirmationBody.Text = body;
		_confirmPrimaryButton.Text = confirmText;
		_confirmationPanel.Visible = true;
		_confirmationPanel.MoveToFront();
	}

	private void HideConfirmation()
	{
		if (_confirmationPanel is not null)
			_confirmationPanel.Visible = false;
		_pendingConfirmation = ConfirmationKind.None;
		_pendingPotionItemId = string.Empty;
		_pendingPotionResult = null;
	}

	private void OnConfirmPrimaryPressed()
	{
		var pendingKind = _pendingConfirmation;
		var potionItemId = _pendingPotionItemId;
		var potionResult = _pendingPotionResult;
		HideConfirmation();

		if (pendingKind == ConfirmationKind.FailedServe && potionResult is not null)
		{
			ResolveSale(potionItemId, potionResult);
			return;
		}

		if (pendingKind == ConfirmationKind.Refuse)
			ResolveRefusal();
	}

	private void ResolveSale(string itemId, PotionResult brewResult)
	{
		var interaction = ActiveCustomer;
		if (interaction is null || _isResolvingCustomer)
			return;

		var resolution = _saleService.ResolveSale(interaction, itemId, brewResult);
		var saleResult = resolution.SaleResult;
		_outcomeLabel.Text = resolution.OutcomeText;
		ClearSelectedPotion();
		_shopSessionState.ClearActiveCustomerRequest();

		EmitSignal(
			SignalName.SaleResolved,
			saleResult.IsSuccess,
			saleResult.GoldDelta,
			saleResult.DreadDelta,
			brewResult.FinalScore,
			brewResult.Grade);
		EmitSignal(SignalName.PotionSold, itemId, saleResult.IsSuccess);
		if (TryBeginMotherPostServeDialogue(interaction, saleResult.IsSuccess))
			return;

		BeginResolveActiveCustomer();
	}

	private void ResolveRefusal()
	{
		var interaction = ActiveCustomer;
		if (interaction is null || _isResolvingCustomer)
			return;

		_outcomeLabel.Text = _saleService.ResolveRefusal(interaction);
		ClearSelectedPotion();
		_shopSessionState.ClearActiveCustomerRequest();
		EmitSignal(SignalName.CustomerSkipped);
		BeginResolveActiveCustomer();
	}

	private void BeginResolveActiveCustomer()
	{
		if (ActiveCustomer is null)
			return;

		_isResolvingCustomer = true;
		_resolvingCustomerIndex = _activeIndex;
		_serveButton.Disabled = true;
		_refuseButton.Disabled = true;
		_servingDropBox.SetAcceptDrops(false);
		_servingDropBox.SetDisabledVisual(true);

		if (!_customerImage.Visible)
		{
			FinishResolvedCustomer();
			return;
		}

		_slideTween?.Kill();
		_slideTween = CreateTween();
		_slideTween.SetTrans(Tween.TransitionType.Sine);
		_slideTween.SetEase(Tween.EaseType.In);
		_slideTween.TweenProperty(
			_customerImage,
			"position",
			_customerImage.Position + new Vector2(CustomerSlideDistance, 0.0f),
			CustomerSlideSeconds);
		_slideTween.Finished += FinishResolvedCustomer;
	}

	private void FinishResolvedCustomer()
	{
		if (_resolvingCustomerIndex >= 0 && _resolvingCustomerIndex < _customers.Count)
			_customers.RemoveAt(_resolvingCustomerIndex);

		_isResolvingCustomer = false;
		_resolvingCustomerIndex = -1;
		_activeIndex = _customers.Count == 0 ? -1 : Math.Clamp(_activeIndex, 0, _customers.Count - 1);
		RefreshActiveCustomer(emitShownSignal: _customers.Count > 0);
		EmitSignal(SignalName.CustomerResolved);
		EmitSignal(SignalName.SaleResultClosed);

		if (_customers.Count == 0)
			EmitSignal(SignalName.CustomerQueueEmptied);
	}

	private enum ConfirmationKind
	{
		None,
		FailedServe,
		Refuse
	}
}
