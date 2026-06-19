using System;
using System.Collections.Generic;
using Godot;
using OccultShop.Autoload;
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
	public delegate void InteractionShownEventHandler(string interactionId);

	private const float CustomerSlideSeconds = 0.28f;
	private const float CustomerSlideDistance = 420.0f;
	private const string FailedServeConsequenceText = "Filler consequence text: this potion may disappoint the customer and affect future outcomes.";
	private const string RefuseConsequenceText = "Filler consequence text: refusing a customer may affect future outcomes.";

	[Export] public NodePath PotionInventoryRowPath = new("../PotionInventoryRow");
	[Export] public NodePath GameStatePath = new(AutoloadNodePaths.GameState);
	[Export] public NodePath ItemCatalogPath = new(AutoloadNodePaths.ItemCatalog);

	private readonly List<CustomerInteractionDef> _customers = new();
	private readonly List<Button> _queueButtons = new();

	private GameState _gameState = default!;
	private ItemCatalogService _itemCatalog = default!;
	private PotionInventoryRow? _potionInventoryRow;
	private CustomerSaleService _saleService = default!;

	private Label _title = default!;
	private Control _customerImageFrame = default!;
	private TextureRect _customerImage = default!;
	private RichTextLabel _dialogue = default!;
	private RichTextLabel _fitCheck = default!;
	private CustomerSellDropBox _servingDropBox = default!;
	private Label _servingDropLabel = default!;
	private Button _serveButton = default!;
	private Button _refuseButton = default!;
	private HBoxContainer _queueRow = default!;
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
	private bool _isResolvingCustomer;
	private Tween? _slideTween;

	public bool HasActiveInteraction => ActiveCustomer is not null;
	public bool HasQueuedCustomers => _customers.Count > 0;

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

		_gameState = gameState;
		_itemCatalog = itemCatalog;
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
		_confirmPrimaryButton.Pressed += OnConfirmPrimaryPressed;
		_confirmCancelButton.Pressed += HideConfirmation;

		ClearCustomers();
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
		if (_confirmPrimaryButton is not null)
			_confirmPrimaryButton.Pressed -= OnConfirmPrimaryPressed;
		if (_confirmCancelButton is not null)
			_confirmCancelButton.Pressed -= HideConfirmation;
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
		ClearSelectedPotion();
		HideConfirmation();
		RefreshQueue();
		RefreshActiveCustomer(emitShownSignal: true);
	}

	public void ClearCustomers()
	{
		_customers.Clear();
		_activeIndex = -1;
		_isResolvingCustomer = false;
		_resolvingCustomerIndex = -1;
		ClearSelectedPotion();
		HideConfirmation();
		RefreshQueue();
		RefreshActiveCustomer(emitShownSignal: false);
	}

	public Button? GetNextCustomerButton()
	{
		return _queueButtons.Count > 0 ? _queueButtons[0] : null;
	}

	public Control? GetVisiblePotionSlot(string itemId)
	{
		return _potionInventoryRow?.GetVisiblePotionSlot(itemId);
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
			CustomMinimumSize = new Vector2(0.0f, 120.0f),
			ClipContents = true,
			MouseFilter = MouseFilterEnum.Ignore
		};
		vbox.AddChild(_customerImageFrame);

		_customerImage = new TextureRect
		{
			Name = "CustomerImage",
			Position = Vector2.Zero,
			Size = new Vector2(360.0f, 120.0f),
			MouseFilter = MouseFilterEnum.Ignore,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
		};
		_customerImageFrame.AddChild(_customerImage);

		_dialogue = new RichTextLabel
		{
			Name = "RequestText",
			BbcodeEnabled = true,
			FitContent = false,
			ScrollActive = true,
			CustomMinimumSize = new Vector2(0.0f, 76.0f),
			MouseFilter = MouseFilterEnum.Stop
		};
		vbox.AddChild(_dialogue);

		var fitTitle = new Label { Name = "FitTitle", Text = "Selected Potion Fit" };
		vbox.AddChild(fitTitle);

		_fitCheck = new RichTextLabel
		{
			Name = "FitCheck",
			BbcodeEnabled = true,
			FitContent = false,
			ScrollActive = true,
			CustomMinimumSize = new Vector2(0.0f, 76.0f),
			MouseFilter = MouseFilterEnum.Stop
		};
		vbox.AddChild(_fitCheck);

		var servingTitle = new Label { Name = "ServingTitle", Text = "Serving Slot" };
		vbox.AddChild(servingTitle);
		_servingDropBox = CreateServingDropBox();
		vbox.AddChild(_servingDropBox);

		var actions = new HBoxContainer { Name = "Actions" };
		actions.AddThemeConstantOverride("separation", 8);
		vbox.AddChild(actions);

		_serveButton = new Button
		{
			Name = "Serve",
			Text = "Serve",
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		actions.AddChild(_serveButton);

		_refuseButton = new Button
		{
			Name = "Refuse",
			Text = "Refuse",
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		actions.AddChild(_refuseButton);

		_outcomeLabel = new Label
		{
			Name = "Outcome",
			Text = "",
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		vbox.AddChild(_outcomeLabel);

		var queueTitle = new Label { Name = "QueueTitle", Text = "Customer Queue" };
		vbox.AddChild(queueTitle);
		_queueRow = new HBoxContainer { Name = "Queue" };
		_queueRow.AddThemeConstantOverride("separation", 6);
		vbox.AddChild(_queueRow);

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

	private void RefreshQueue()
	{
		foreach (var child in _queueRow.GetChildren())
		{
			_queueRow.RemoveChild(child);
			child.QueueFree();
		}
		_queueButtons.Clear();

		for (var i = 0; i < _customers.Count; i++)
		{
			var index = i;
			var customer = _customers[i];
			var button = new Button
			{
				Name = $"Customer{index + 1}",
				Text = BuildQueueLabel(customer, index),
				ToggleMode = true,
				ButtonPressed = index == _activeIndex,
				Disabled = _isResolvingCustomer,
				SizeFlagsHorizontal = SizeFlags.ExpandFill
			};
			button.Pressed += () => SelectCustomer(index);
			_queueRow.AddChild(button);
			_queueButtons.Add(button);
		}
	}

	private static string BuildQueueLabel(CustomerInteractionDef customer, int index)
	{
		var title = string.IsNullOrWhiteSpace(customer.Title) ? "Customer" : customer.Title;
		return $"{index + 1}. {title}";
	}

	private void SelectCustomer(int index)
	{
		if (_isResolvingCustomer)
			return;
		if (index < 0 || index >= _customers.Count || index == _activeIndex)
			return;

		_activeIndex = index;
		RefreshQueue();
		RefreshActiveCustomer(emitShownSignal: true);
	}

	private void RefreshActiveCustomer(bool emitShownSignal)
	{
		var interaction = ActiveCustomer;
		if (interaction is null)
		{
			_gameState.ClearActiveCustomerRequest();
			_title.Text = "No customer waiting";
			_dialogue.Text = "No active customer.";
			_fitCheck.Text = "Select a customer and potion.";
			_customerImage.Texture = null;
			_customerImage.Visible = false;
			_serveButton.Disabled = true;
			_refuseButton.Disabled = true;
			_servingDropBox.SetAcceptDrops(false);
			_servingDropBox.SetDisabledVisual(true);
			return;
		}

		_slideTween?.Kill();
		_customerImage.Position = Vector2.Zero;
		_customerImage.Modulate = Colors.White;
		_title.Text = string.IsNullOrWhiteSpace(interaction.Title) ? "Customer" : interaction.Title;
		_dialogue.Text = BuildRequestText(interaction);
		RefreshCustomerImage(interaction);

		var request = interaction.BuildRequest();
		_gameState.SetActiveCustomerRequest(request);
		_serveButton.Disabled = false;
		_refuseButton.Disabled = false;
		_servingDropBox.SetAcceptDrops(true);
		_servingDropBox.SetDisabledVisual(false);
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

	private void RefreshCustomerImage(CustomerInteractionDef interaction)
	{
		var imagePath = interaction.CharacterImagePath;
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
		if (interaction is null || !_itemCatalog.IsPotion(itemId))
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
		SetRequestFitText(interaction.BuildRequest(), itemId, brewResult);
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
	}

	private string BuildSelectedPotionLabel(string itemId)
	{
		var fallbackName = _itemCatalog.GetItemName(itemId);
		var customName = _gameState.GetPotionDisplayName(itemId);
		var displayName = string.IsNullOrWhiteSpace(customName) ? fallbackName : customName;
		return $"Selected: {displayName}";
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
			_servingDropLabel.Text = "Drop potion here";
			SetRequestFitText(request, string.Empty, null);
			_fitCheck.Text = CustomerDialogueTextFormatter.BuildCustomerPotionRequestComparisonText(
				request,
				null,
				null,
				null);
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
		_fitCheck.Text = CustomerDialogueTextFormatter.BuildCustomerPotionRequestComparisonText(
			request,
			brewResult?.Traits,
			brewResult?.Risks,
			string.IsNullOrWhiteSpace(potionItemId) ? null : _saleService.GetPotionIngredientPortions(potionItemId));
	}

	private void ClearSelectedPotion()
	{
		_selectedPotionItemId = string.Empty;
		_selectedPotionResult = null;
		if (_servingDropLabel is not null)
			_servingDropLabel.Text = "Drop potion here";
	}

	private void OnServePressed()
	{
		var interaction = ActiveCustomer;
		if (interaction is null)
			return;

		if (string.IsNullOrWhiteSpace(_selectedPotionItemId) || _selectedPotionResult is null)
		{
			CursorToast.Show(this, "Select a potion to serve.");
			return;
		}

		var request = interaction.BuildRequest();
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

		ShowConfirmation(
			"Refuse customer?",
			$"Refuse this customer without serving a potion?\n\n{RefuseConsequenceText}",
			"Refuse",
			ConfirmationKind.Refuse,
			string.Empty,
			null);
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

		var outcomeText = _saleService.BuildOutcomeText(interaction, itemId, brewResult);
		var saleResult = _saleService.ApplySale(interaction, itemId, brewResult);
		_outcomeLabel.Text = outcomeText;
		ClearSelectedPotion();
		_gameState.ClearActiveCustomerRequest();

		EmitSignal(
			SignalName.SaleResolved,
			saleResult.IsSuccess,
			saleResult.GoldDelta,
			saleResult.DreadDelta,
			brewResult.FinalScore,
			brewResult.Grade);
		EmitSignal(SignalName.PotionSold, itemId, saleResult.IsSuccess);
		BeginResolveActiveCustomer();
	}

	private void ResolveRefusal()
	{
		var interaction = ActiveCustomer;
		if (interaction is null || _isResolvingCustomer)
			return;

		_saleService.ApplyRefusal(interaction);
		_outcomeLabel.Text = _saleService.BuildRefusalText(interaction);
		ClearSelectedPotion();
		_gameState.ClearActiveCustomerRequest();
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
		RefreshQueue();

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
		RefreshQueue();
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
