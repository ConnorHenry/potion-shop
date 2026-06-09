using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OccultShop.Autoload;
using OccultShop.Models;
using OccultShop.Systems;

namespace OccultShop.UI;

public partial class CustomerPanel : Control
{
	[Signal]
	public delegate void SaleResolvedEventHandler(bool success, int goldDelta, int dreadDelta, float finalScore, string grade);
	[Signal]
	public delegate void CustomerSkippedEventHandler();
	[Signal]
	public delegate void SaleResultClosedEventHandler();
	[Signal]
	public delegate void PotionSoldEventHandler(string itemId, bool success);
	[Signal]
	public delegate void InteractionShownEventHandler(string interactionId);
	[Signal]
	public delegate void DialogueResolvedEventHandler();
	[Signal]
	public delegate void PlotConversationStartedEventHandler();

	public bool SuppressSaleResultPanel { get; set; }
	public string? CurrentCustomerImagePath => _interaction?.CharacterImagePath;

	[Export] public int DialogueTypewriterCharactersPerSecond = 45;
	[Export] public NodePath TitlePath = default!;
	[Export] public NodePath DesiredTraitsPath = default!;
	[Export] public NodePath BadTraitsPath = default!;
	[Export] public NodePath DialoguePath = default!;
	[Export] public NodePath SellDropBoxPath = default!;
	[Export] public NodePath SaleResultPanelPath = default!;
	[Export] public NodePath SaleResultTitlePath = default!;
	[Export] public NodePath SaleResultBodyPath = default!;
	[Export] public NodePath SaleResultCloseButtonPath = default!;
	[Export] public NodePath CloseButtonPath = default!;
	[Export] public NodePath InventoryPanelPath = new("../InventoryPanel");
	[Export] public NodePath GameStatePath = new(AutoloadNodePaths.GameState);
	[Export] public NodePath ItemCatalogPath = new(AutoloadNodePaths.ItemCatalog);

	private Label? _title;
	private RichTextLabel _desiredTraits = default!;
	private RichTextLabel _badTraits = default!;
	private RichTextLabel _dialogue = default!;
	private CustomerSellDropBox _sellDropBox = default!;
	private Control _saleResultPanel = default!;
	private Label _saleResultTitle = default!;
	private RichTextLabel _saleResultBody = default!;
	private Button _saleResultCloseButton = default!;
	private Button? _closeButton;
	private HBoxContainer _potionSlotsRow = default!;
	private HBoxContainer _customerActions = default!;
	private VBoxContainer _dialogueOptionsContainer = default!;
	private HBoxContainer _sellingActions = default!;
	private Button _comeBackTomorrowButton = default!;
	private Button _sorryCantHelpYouButton = default!;
	private Button _nextCustomerButton = default!;
	private Button _refusePotionButton = default!;
	private Button _returnToDialogueButton = default!;
	private CustomerInteractionDef? _interaction;
	private readonly PotionBrewingService _brewingService = new();
	private readonly List<Button> _dialogueOptionButtons = new();
	private readonly List<CustomerDialogueOptionDef> _visibleDialogueOptions = new();
	private readonly List<string> _conversationHistory = new();
	private readonly Queue<DialogueLine> _pendingDialogueLines = new();
	private readonly List<PotionSlotView> _potionSlotViews = new();
	private GameState _gameState = default!;
	private ItemCatalogService _itemCatalog = default!;
	private InventoryPanel? _inventoryPanel;
	private Godot.Timer? _dialogueTypewriterTimer;
	private Control.GuiInputEventHandler? _dialogueGuiInputHandler;
	private DialogueLine? _activeDialogueLine;
	private int _activeDialogueTextCharacters;
	private Action? _dialogueQueueCompletedAction;
	private bool _closeShopMode;
	private bool _awaitingNextCustomer;
	private bool _requestRevealed;
	private bool _sellingMode;
	private bool _interactionPresentationStarted;
	private string _activeDialogueNodeId = string.Empty;
	private string _requestReturnDialogueNodeId = string.Empty;
	private const int SuccessDreadChange = -2;
	private const int FailureDreadChange = 4;
	private const int CustomerPotionSlotCount = 4;
	private const float CustomerPotionSlotSize = 70.0f;
	private const float CustomerPotionIconSize = 46.0f;
	private static readonly Color SeenDialogueOptionModulate = new(0.58f, 0.58f, 0.58f, 1f);
	private static readonly Color DefaultButtonModulate = new(1f, 1f, 1f, 1f);

	public override void _Ready()
	{
		var gameState = GetNodeOrNull<GameState>(GameStatePath);
		if (gameState is null)
		{
			GD.PushError($"CustomerPanel: GameState was not found at '{GameStatePath}'.");
			return;
		}

		var itemCatalog = GetNodeOrNull<ItemCatalogService>(ItemCatalogPath);
		if (itemCatalog is null)
		{
			GD.PushError($"CustomerPanel: ItemCatalog was not found at '{ItemCatalogPath}'.");
			return;
		}

		_gameState = gameState;
		_itemCatalog = itemCatalog;
		_inventoryPanel = GetNodeOrNull<InventoryPanel>(InventoryPanelPath);
		if (_inventoryPanel is null)
			GD.PushError($"CustomerPanel: InventoryPanel was not found at '{InventoryPanelPath}'.");

		_title = ResolveOptionalNode(TitlePath, "TitlePath", GetNodeOrNull<Label>);
		_desiredTraits = GetNode<RichTextLabel>(DesiredTraitsPath);
		_badTraits = GetNode<RichTextLabel>(BadTraitsPath);
		_dialogue = GetNode<RichTextLabel>(DialoguePath);
		_sellDropBox = GetNode<CustomerSellDropBox>(SellDropBoxPath);
		_saleResultPanel = GetNode<Control>(SaleResultPanelPath);
		_saleResultTitle = GetNode<Label>(SaleResultTitlePath);
		_saleResultBody = GetNode<RichTextLabel>(SaleResultBodyPath);
		_saleResultCloseButton = GetNode<Button>(SaleResultCloseButtonPath);
		_closeButton = ResolveOptionalNode(CloseButtonPath, "CloseButtonPath", GetNodeOrNull<Button>);
		BindOrCreateSkipButtons();

		_desiredTraits.BbcodeEnabled = true;
		_badTraits.BbcodeEnabled = true;
		_dialogue.BbcodeEnabled = true;
		_dialogue.ScrollActive = true;
		_dialogue.ScrollFollowing = true;
		_dialogue.MouseFilter = MouseFilterEnum.Stop;

		MouseFilter = MouseFilterEnum.Ignore;
		_dialogueTypewriterTimer = new Godot.Timer
		{
			OneShot = false,
			Autostart = false,
			WaitTime = GetDialogueTypewriterIntervalSeconds()
		};
		AddChild(_dialogueTypewriterTimer);
		_dialogueTypewriterTimer.Timeout += OnDialogueTypewriterTimeout;
		_dialogueGuiInputHandler = OnDialogueGuiInput;
		_dialogue.GuiInput += _dialogueGuiInputHandler;
		//_closeButton.Pressed += HidePanel;
		if (_comeBackTomorrowButton is not null)
			_comeBackTomorrowButton.Pressed += OnComeBackTomorrowPressed;

		if (_sorryCantHelpYouButton is not null)
			_sorryCantHelpYouButton.Pressed += OnSorryCantHelpYouPressed;
		if (_nextCustomerButton is not null)
			_nextCustomerButton.Pressed += OnSaleResultClosePressed;
		if (_refusePotionButton is not null)
			_refusePotionButton.Pressed += OnRefusePotionPressed;
		if (_returnToDialogueButton is not null)
			_returnToDialogueButton.Pressed += OnReturnToDialoguePressed;

		_saleResultCloseButton.Text = "Next customer";
		_saleResultCloseButton.Pressed += OnSaleResultClosePressed;
		_sellDropBox.ItemDropped += OnItemDropped;
		_sellDropBox.ItemHoverPreview += OnSellDropHoverPreview;
		_sellDropBox.HoverPreviewCleared += OnSellDropHoverPreviewCleared;
		_gameState.Changed += RefreshPotionSlotRow;
		UpdateCloseShopButtonText();
		_saleResultPanel.Visible = false;
		SetSalePendingState();
		Visible = false;
	}

	private T? ResolveOptionalNode<T>(NodePath path, string pathName, System.Func<NodePath, T?> resolver) where T : class
	{
		if (string.IsNullOrWhiteSpace(path.ToString()))
			return null;

		var node = resolver(path);
		if (node is null)
			GD.PushError($"CustomerPanel: {pathName} was set but node was not found at '{path}'.");

		return node;
	}

	public override void _ExitTree()
	{
		if (_closeButton is not null)
			_closeButton.Pressed -= HidePanel;
		if (_comeBackTomorrowButton is not null)
			_comeBackTomorrowButton.Pressed -= OnComeBackTomorrowPressed;
		if (_sorryCantHelpYouButton is not null)
			_sorryCantHelpYouButton.Pressed -= OnSorryCantHelpYouPressed;
		if (_nextCustomerButton is not null)
			_nextCustomerButton.Pressed -= OnSaleResultClosePressed;
		if (_refusePotionButton is not null)
			_refusePotionButton.Pressed -= OnRefusePotionPressed;
		if (_returnToDialogueButton is not null)
			_returnToDialogueButton.Pressed -= OnReturnToDialoguePressed;
		if (_saleResultCloseButton is not null)
			_saleResultCloseButton.Pressed -= OnSaleResultClosePressed;
		if (_dialogueTypewriterTimer is not null)
			_dialogueTypewriterTimer.Timeout -= OnDialogueTypewriterTimeout;
		if (_dialogue is not null && _dialogueGuiInputHandler is not null)
			_dialogue.GuiInput -= _dialogueGuiInputHandler;
		if (_sellDropBox is not null)
		{
			_sellDropBox.ItemDropped -= OnItemDropped;
			_sellDropBox.ItemHoverPreview -= OnSellDropHoverPreview;
			_sellDropBox.HoverPreviewCleared -= OnSellDropHoverPreviewCleared;
		}
		if (_gameState is not null)
			_gameState.Changed -= RefreshPotionSlotRow;
	}

	public void ShowInteraction(CustomerInteractionDef interaction)
	{
		PrepareInteraction(interaction);
		ShowPreparedInteraction();
	}

	public void PrepareInteraction(CustomerInteractionDef interaction)
	{
		HideSaleResult();
		_interaction = interaction;
		_interactionPresentationStarted = false;
		_awaitingNextCustomer = false;
		_activeDialogueNodeId = string.Empty;
		_requestReturnDialogueNodeId = string.Empty;
		_requestRevealed = false;
		_sellingMode = false;
		_gameState.ClearActiveCustomerRequest();
		Visible = false;
	}

	public void ShowPreparedInteraction()
	{
		var interaction = _interaction;
		if (interaction is null)
		{
			GD.PushError("CustomerPanel: Cannot show a customer request because no customer is active.");
			return;
		}

		if (_interactionPresentationStarted)
		{
			Visible = true;
			return;
		}

		HideSaleResult();
		_awaitingNextCustomer = false;
		_activeDialogueNodeId = string.Empty;
		_requestReturnDialogueNodeId = string.Empty;
		_requestRevealed = false;
		_sellingMode = false;
		var request = interaction.BuildRequest();
		if (HasActiveDialogueInteraction())
			_gameState.ClearActiveCustomerRequest();
		else
			_gameState.SetActiveCustomerRequest(request);
		Visible = true;
		if (_title is not null)
			_title.Text = interaction.Title;
		ResetConversationHistory();
		if (!HasActiveDialogueInteraction())
			AppendCustomerLine(interaction.Text);
		SetRequestTraits(request);
		if (TryShowDialogueStart())
		{
			EmitSignal(SignalName.PlotConversationStarted);
		}
		else
		{
			SetSalePendingState();
		}
		_interactionPresentationStarted = true;
		EmitSignal(SignalName.InteractionShown, interaction.Id);
	}

	public Button? GetNextCustomerButton()
	{
		return _nextCustomerButton;
	}

	public bool IsCloseShopMode => _closeShopMode;

	public bool HasActiveInteraction => _interaction is not null;

	public void SetCloseShopMode(bool closeShopMode)
	{
		_closeShopMode = closeShopMode;
		UpdateCloseShopButtonText();
	}

	public void HidePanel()
	{
		_interaction = null;
		_gameState.ClearActiveCustomerRequest();
		_desiredTraits.Text = "";
		_badTraits.Text = "";
		ResetConversationHistory();
		_awaitingNextCustomer = false;
		_activeDialogueNodeId = string.Empty;
		_requestReturnDialogueNodeId = string.Empty;
		_requestRevealed = false;
		_sellingMode = false;
		_interactionPresentationStarted = false;
		SetSalePendingState();
		HideSaleResult();
		Visible = false;
	}

	private void OnItemDropped(string itemId)
	{
		if (_interaction is null)
			return;

		if (HasActiveDialogueInteraction() && !_sellingMode)
			return;

		if (!_itemCatalog.TryGetItem(itemId, out _))
			return;

		if (!IsPotionItem(itemId))
			return;

		if (!TryResolvePotionScore(itemId, out var brewResult))
			return;

		if (brewResult is null)
			return;

		if (HasActiveDialogueInteraction())
			AppendPlayerLine($"Give {DisplayName(itemId, _itemCatalog.GetItemName(itemId))}");

		var saleResult = ApplySale(itemId, brewResult);

		if (SuppressSaleResultPanel)
		{
			_interaction = null;
			_interactionPresentationStarted = false;
			Visible = false;
			_gameState.ClearActiveCustomerRequest();
			HideSaleResult();

			EmitSignal(
				SignalName.SaleResolved,
				saleResult.IsSuccess,
				saleResult.GoldDelta,
				saleResult.DreadDelta,
				brewResult.FinalScore,
				brewResult.Grade);
			EmitSignal(SignalName.PotionSold, itemId, saleResult.IsSuccess);
			return;
		}

		ShowSaleResult(itemId, brewResult);
		EmitSignal(
			SignalName.SaleResolved,
			saleResult.IsSuccess,
			saleResult.GoldDelta,
			saleResult.DreadDelta,
			brewResult.FinalScore,
			brewResult.Grade);
		EmitSignal(SignalName.PotionSold, itemId, saleResult.IsSuccess);
	}

	private bool TryResolvePotionScore(string itemId, out PotionResult? brewResult)
	{
		brewResult = null;

		if (_interaction is null)
			return false;

		if (!_itemCatalog.TryGetItem(itemId, out var item))
			return false;

		var request = _interaction.BuildRequest();
		brewResult = _brewingService.EvaluatePotionItem(item, request);

		return true;
	}

	private (bool IsSuccess, int GoldDelta, int DreadDelta) ApplySale(string itemId, PotionResult brewResult)
	{
		var request = _interaction?.BuildRequest();
		var isSuccess = request is not null && IsRequestSatisfiedByPotion(itemId, request, brewResult);
		var goldDelta = GetSalePrice(itemId);
		var dreadDelta = isSuccess ? SuccessDreadChange : FailureDreadChange;

		_gameState.AddGold(goldDelta);
		_gameState.AddDread(dreadDelta);

		_gameState.ConsumeItem(itemId, 1);
		ApplyOutcomeEffects(isSuccess ? _interaction?.OnSuccessEffects : _interaction?.OnFailureEffects);
		var response = request is null ? null : FindPotionResponse(itemId, request, brewResult, isSuccess);
		ApplyOutcomeEffects(response?.Effects);
		if (_interaction is not null)
		{
			var outcome = isSuccess
				? GameState.StoryCustomerOutcomeSuccess
				: GameState.StoryCustomerOutcomeFailure;
			_gameState.RecordStoryCustomerInteractionOutcome(_interaction, outcome);
		}

		return (isSuccess, goldDelta, dreadDelta);
	}

	private void ShowSaleResult(string itemId, PotionResult brewResult)
	{
		_gameState.ClearActiveCustomerRequest();
		_requestRevealed = false;
		_sellingMode = false;
		var outcomeText = BuildOutcomeText(itemId, brewResult);
		if (HasActiveDialogueInteraction())
			AppendCustomerLine(outcomeText);
		else
			SetConversationHistory(outcomeText);
		_saleResultTitle.Text = "Sale Result";
		_saleResultBody.Text = outcomeText;
		_awaitingNextCustomer = true;
		SetSaleResolvedState();
		HideSaleResult();
	}

	private void HideSaleResult()
	{
		_saleResultPanel.Visible = false;
		_saleResultBody.Text = "";
	}

	private void OnSaleResultClosePressed()
	{
		if (!_awaitingNextCustomer && !_saleResultPanel.Visible)
			return;

		_awaitingNextCustomer = false;
		HideSaleResult();
		_interaction = null;
		_activeDialogueNodeId = string.Empty;
		_requestReturnDialogueNodeId = string.Empty;
		_requestRevealed = false;
		_sellingMode = false;
		_interactionPresentationStarted = false;
		EmitSignal(SignalName.SaleResultClosed);
	}

	private string BuildOutcomeText(string itemId, PotionResult brewResult)
	{
		var lines = new List<string>();
		var itemName = _itemCatalog.GetItemName(itemId);
		itemName = DisplayName(itemId, itemName);
		lines.Add($"Potion: {itemName}");

		var request = _interaction?.BuildRequest();
		var isSuccess = request is not null && IsRequestSatisfiedByPotion(itemId, request, brewResult);
		lines.Add($"Sale: {(isSuccess ? "Success" : "Failure")}");
		lines.Add(string.Empty);
		var authoredResponse = request is null ? null : FindPotionResponse(itemId, request, brewResult, isSuccess);
		if (authoredResponse is not null)
		{
			lines.Add(authoredResponse.Text);
			return string.Join("\n", lines);
		}

		var matchedDesiredTraitCount = request is null
			? 0
			: CustomerSaleRules.CountMatchedDesiredTraits(request, brewResult.Traits);
		lines.Add($"Customer response: {GetCustomerResponseText(matchedDesiredTraitCount)}");

		return string.Join("\n", lines);
	}

	private static string GetCustomerResponseText(int matchedDesiredTraitCount)
	{
		if (matchedDesiredTraitCount >= 3)
			return "The customer is happy";

		if (matchedDesiredTraitCount == 2)
			return "The customer is satisfied";

		return "The customer is disappointed";
	}

	private CustomerPotionResponseDef? FindPotionResponse(
		string itemId,
		CustomerRequestDef request,
		PotionResult brewResult,
		bool isSuccess)
	{
		if (_interaction is null || _interaction.PotionResponses.Count == 0)
			return null;

		foreach (var response in _interaction.PotionResponses)
		{
			if (!CustomerSaleRules.PotionResponseMatches(response, itemId, request, brewResult, isSuccess))
				continue;

			return response;
		}

		return null;
	}

	private void SetRequestTraits(CustomerRequestDef request)
	{
		SetRequestTraits(request, null, null);
	}

	private void SetRequestTraits(
		CustomerRequestDef request,
		IReadOnlyDictionary<string, int>? producedTraits,
		IReadOnlyDictionary<string, int>? producedRisks)
	{
		_desiredTraits.Text = CustomerDialogueTextFormatter.BuildDesiredRequestText(request, producedTraits);
		_badTraits.Text = CustomerDialogueTextFormatter.BuildBadRequestText(request, producedTraits, producedRisks);
	}

	private bool IsPotionItem(string itemId)
	{
		return _itemCatalog.IsPotion(itemId);
	}

	private int GetSalePrice(string itemId)
	{
		if (_gameState.TryGetPotionBasePrice(itemId, out var potionBasePrice))
			return System.Math.Max(0, potionBasePrice);

		if (_itemCatalog.TryGetItem(itemId, out var item))
			return System.Math.Max(0, item.BasePrice);

		return 0;
	}

	private string DisplayName(string itemId, string fallbackName)
	{
		if (!IsPotionItem(itemId))
			return fallbackName;

		var customName = _gameState.GetPotionDisplayName(itemId);
		return string.IsNullOrWhiteSpace(customName) ? fallbackName : customName;
	}

	private void OnSellDropHoverPreview(string itemId)
	{
		if (_interaction is null)
		{
			_sellDropBox.SetHoverHighlight(false);
			return;
		}

		if (HasActiveDialogueInteraction() && !_sellingMode)
		{
			_sellDropBox.SetHoverHighlight(false);
			return;
		}

		var request = _interaction.BuildRequest();

		if (!IsPotionItem(itemId))
		{
			_sellDropBox.SetHoverHighlight(false);
			SetRequestTraits(request);
			return;
		}

		if (!TryResolvePotionScore(itemId, out var brewResult))
		{
			_sellDropBox.SetHoverHighlight(false);
			SetRequestTraits(request);
			return;
		}

		if (brewResult is null)
		{
			_sellDropBox.SetHoverHighlight(false);
			SetRequestTraits(request);
			return;
		}

		_sellDropBox.SetHoverHighlight(true);
		SetRequestTraits(request, brewResult.Traits, brewResult.Risks);
	}

	private void OnSellDropHoverPreviewCleared()
	{
		_sellDropBox.SetHoverHighlight(false);

		if (_interaction is null)
			return;

		if (HasActiveDialogueInteraction() && !_requestRevealed)
			return;

		SetRequestTraits(_interaction.BuildRequest());
	}

	private bool IsRequestSatisfiedByPotion(string potionItemId, CustomerRequestDef request, PotionResult brewResult)
	{
		return CustomerSaleRules.IsRequestSatisfiedByPotion(
			request,
			brewResult,
			DoesPotionBatchSatisfyIngredientAmountRequirements(potionItemId, request.RequiredIngredientAmounts));
	}

	private bool DoesPotionBatchSatisfyIngredientAmountRequirements(
		string potionItemId,
		IReadOnlyList<IngredientPortionDef>? requiredIngredientAmounts)
	{
		if (requiredIngredientAmounts is null || requiredIngredientAmounts.Count == 0)
			return true;

		if (!_gameState.TryPeekPotionIngredientPortionBatch(potionItemId, out var potionBatch))
			return false;

		foreach (var requiredIngredientAmount in requiredIngredientAmounts)
		{
			if (requiredIngredientAmount is null || string.IsNullOrWhiteSpace(requiredIngredientAmount.IngredientId))
				continue;
			if (requiredIngredientAmount.Grams <= 0)
				continue;

			var hasMatchingPortion = potionBatch.Any(portion =>
				string.Equals(portion.IngredientId, requiredIngredientAmount.IngredientId, System.StringComparison.OrdinalIgnoreCase) &&
				portion.Grams == requiredIngredientAmount.Grams);
			if (!hasMatchingPortion)
				return false;
		}

		return true;
	}

	private void ResetConversationHistory()
	{
		StopQueuedDialoguePresentation();
		_conversationHistory.Clear();
		_dialogue.Text = "";
	}

	private void SetConversationHistory(string text)
	{
		ResetConversationHistory();
		if (!string.IsNullOrWhiteSpace(text))
			_conversationHistory.Add(CustomerDialogueTextFormatter.EscapeBbCodeText(text));

		RefreshConversationHistory();
	}

	private void AppendCustomerLine(string text)
	{
		AppendConversationLine(CustomerDialogueTextFormatter.CustomerSpeakerName, text);
	}

	private void AppendPlayerLine(string text)
	{
		AppendConversationLine(CustomerDialogueTextFormatter.PlayerSpeakerName, text);
	}

	private void AppendConversationLine(string speaker, string text)
	{
		if (string.IsNullOrWhiteSpace(text))
			return;

		_conversationHistory.Add(CustomerDialogueTextFormatter.FormatConversationLine(speaker, text));
		RefreshConversationHistory();
	}

	private void RefreshConversationHistory()
	{
		if (_activeDialogueLine is null)
		{
			_dialogue.Text = string.Join("\n\n", _conversationHistory);
			return;
		}

		var visibleLines = new List<string>(_conversationHistory.Count + 1);
		visibleLines.AddRange(_conversationHistory);
		visibleLines.Add(CustomerDialogueTextFormatter.FormatConversationLine(
			_activeDialogueLine.Speaker,
			CustomerDialogueTextFormatter.GetVisibleDialogueText(_activeDialogueLine.Text, _activeDialogueTextCharacters)));
		_dialogue.Text = string.Join("\n\n", visibleLines);
	}

	private void QueueCustomerLine(string text)
	{
		QueueConversationLine(CustomerDialogueTextFormatter.CustomerSpeakerName, text);
	}

	private void QueuePlayerLine(string text)
	{
		QueueConversationLine(CustomerDialogueTextFormatter.PlayerSpeakerName, text);
	}

	private void QueueConversationLine(string speaker, string text)
	{
		if (string.IsNullOrWhiteSpace(text))
			return;

		_pendingDialogueLines.Enqueue(new DialogueLine(speaker, text));
	}

	private void PlayQueuedDialogueLines(Action? completedAction)
	{
		_dialogueQueueCompletedAction = completedAction;
		if (_activeDialogueLine is null && _pendingDialogueLines.Count > 0)
			StartNextQueuedDialogueLine();

		TryCompleteQueuedDialogueLines();
	}

	private void StartNextQueuedDialogueLine()
	{
		if (_pendingDialogueLines.Count == 0)
		{
			TryCompleteQueuedDialogueLines();
			return;
		}

		_activeDialogueLine = _pendingDialogueLines.Dequeue();
		_activeDialogueTextCharacters = 0;
		RefreshConversationHistory();

		if (_dialogueTypewriterTimer is null)
		{
			CompleteActiveDialogueLine();
			return;
		}

		_dialogueTypewriterTimer.WaitTime = GetDialogueTypewriterIntervalSeconds();
		_dialogueTypewriterTimer.Start();
	}

	private void OnDialogueTypewriterTimeout()
	{
		if (_activeDialogueLine is null)
		{
			_dialogueTypewriterTimer?.Stop();
			TryCompleteQueuedDialogueLines();
			return;
		}

		_activeDialogueTextCharacters += 1;
		if (_activeDialogueTextCharacters >= _activeDialogueLine.Text.Length)
		{
			CompleteActiveDialogueLine();
			return;
		}

		RefreshConversationHistory();
	}

	private void OnDialogueGuiInput(InputEvent @event)
	{
		if (@event is not InputEventMouseButton mouseButton)
			return;

		if (mouseButton.ButtonIndex != MouseButton.Left || !mouseButton.Pressed)
			return;

		AdvanceQueuedDialoguePresentation();
	}

	private void AdvanceQueuedDialoguePresentation()
	{
		if (_activeDialogueLine is not null)
		{
			CompleteActiveDialogueLine();
			return;
		}

		if (_pendingDialogueLines.Count > 0)
			StartNextQueuedDialogueLine();
	}

	private void CompleteActiveDialogueLine()
	{
		if (_activeDialogueLine is null)
			return;

		_dialogueTypewriterTimer?.Stop();
		_conversationHistory.Add(CustomerDialogueTextFormatter.FormatConversationLine(_activeDialogueLine.Speaker, _activeDialogueLine.Text));
		_activeDialogueLine = null;
		_activeDialogueTextCharacters = 0;
		RefreshConversationHistory();
		TryCompleteQueuedDialogueLines();
	}

	private void TryCompleteQueuedDialogueLines()
	{
		if (_activeDialogueLine is not null || _pendingDialogueLines.Count > 0)
			return;

		var completedAction = _dialogueQueueCompletedAction;
		_dialogueQueueCompletedAction = null;
		completedAction?.Invoke();
	}

	private void StopQueuedDialoguePresentation()
	{
		_dialogueTypewriterTimer?.Stop();
		_pendingDialogueLines.Clear();
		_activeDialogueLine = null;
		_activeDialogueTextCharacters = 0;
		_dialogueQueueCompletedAction = null;
	}

	private double GetDialogueTypewriterIntervalSeconds()
	{
		var charactersPerSecond = Math.Max(1, DialogueTypewriterCharactersPerSecond);
		return 1.0 / charactersPerSecond;
	}

	private void OnComeBackTomorrowPressed()
	{
		OnSkipCustomerPressed();
	}

	private void OnSorryCantHelpYouPressed()
	{
		OnSkipCustomerPressed();
	}

	private bool TryShowDialogueStart()
	{
		if (!HasActiveDialogueInteraction())
			return false;

		var startNode = _interaction?.GetDialogueNode(string.Empty);
		if (startNode is null)
		{
			GD.PushError($"CustomerPanel: Story customer '{_interaction?.Id}' has dialogue data but no valid start node.");
			return false;
		}

		ShowDialogueNode(startNode);
		return true;
	}

	private bool TrySelectDialogueOption(int optionIndex)
	{
		if (!HasActiveDialogueInteraction())
			return false;

		var node = _interaction?.GetDialogueNode(_activeDialogueNodeId);
		if (node is null)
		{
			GD.PushError($"CustomerPanel: Active dialogue node '{_activeDialogueNodeId}' was not found.");
			return true;
		}

		if (optionIndex < 0 || optionIndex >= _visibleDialogueOptions.Count)
			return true;

		var option = _visibleDialogueOptions[optionIndex];
		SetDialoguePresentationState();
		QueuePlayerLine(option.Label);
		if (_interaction is not null)
			_gameState.RecordStoryCustomerDialogueOptionSelected(_interaction, option.Id);
		ApplyOutcomeEffects(option.Effects);

		if (!string.IsNullOrWhiteSpace(option.ResponseText))
			QueueCustomerLine(option.ResponseText);

		if (option.RevealsRequest)
		{
			if (string.IsNullOrWhiteSpace(option.ResponseText) && !string.IsNullOrWhiteSpace(_interaction?.Text))
				QueueCustomerLine(_interaction.Text);
			PlayQueuedDialogueLines(() => EnterPotionSellingMode(option));
			return true;
		}

		if (option.ReturnsToDialogue)
		{
			var returnNode = ResolveDialogueReturnNode(option, node);
			QueueDialogueNodeText(returnNode);
			_activeDialogueNodeId = returnNode.Id;
			PlayQueuedDialogueLines(() => FinishShowingDialogueNode(returnNode));
			return true;
		}

		if (option.EndsInteraction)
		{
			PlayQueuedDialogueLines(() => CompleteDialogueInteraction(option));
			return true;
		}

		if (!string.IsNullOrWhiteSpace(option.NextNodeId))
		{
			var nextNode = _interaction?.GetDialogueNode(option.NextNodeId);
			if (nextNode is null)
			{
				GD.PushError($"CustomerPanel: Dialogue option '{option.Id}' points to missing node '{option.NextNodeId}'.");
				CompleteDialogueInteraction(option);
				return true;
			}

			QueueDialogueNodeText(nextNode);
			_activeDialogueNodeId = nextNode.Id;
			PlayQueuedDialogueLines(() => FinishShowingDialogueNode(nextNode));
			return true;
		}

		PlayQueuedDialogueLines(() => SetDialogueOptionState(node));
		return true;
	}

	private CustomerDialogueNodeDef ResolveDialogueReturnNode(
		CustomerDialogueOptionDef option,
		CustomerDialogueNodeDef fallbackNode)
	{
		if (_interaction is null)
			return fallbackNode;

		var targetNodeId = !string.IsNullOrWhiteSpace(option.ReturnNodeId)
			? option.ReturnNodeId
			: option.NextNodeId;
		if (string.IsNullOrWhiteSpace(targetNodeId))
			targetNodeId = _requestReturnDialogueNodeId;

		if (!string.IsNullOrWhiteSpace(targetNodeId))
		{
			var targetNode = _interaction.GetDialogueNode(targetNodeId);
			if (targetNode is not null)
				return targetNode;

			GD.PushError($"CustomerPanel: Dialogue option '{option.Id}' returns to missing node '{targetNodeId}'.");
		}

		return fallbackNode;
	}

	private void EnterPotionSellingMode(CustomerDialogueOptionDef option)
	{
		if (_interaction is null)
			return;

		_requestRevealed = true;
		_sellingMode = true;
		_requestReturnDialogueNodeId = !string.IsNullOrWhiteSpace(option.ReturnNodeId)
			? option.ReturnNodeId
			: _activeDialogueNodeId;

		var request = _interaction.BuildRequest();
		_gameState.SetActiveCustomerRequest(request);
		SetRequestTraits(request);
		SetSellingModeState();
	}

	private void OnRefusePotionPressed()
	{
		if (_interaction is null)
			return;

		SetDialoguePresentationState();
		QueuePlayerLine(_refusePotionButton.Text);
		var responseText = string.IsNullOrWhiteSpace(_interaction.PotionRefusedText)
			? "The customer leaves without a potion."
			: _interaction.PotionRefusedText;
		QueueCustomerLine(responseText);
		var effects = _interaction.OnPotionRefusedEffects.Count > 0
			? _interaction.OnPotionRefusedEffects
			: _interaction.OnSkipEffects;
		ApplyOutcomeEffects(effects);
		PlayQueuedDialogueLines(() => CompleteDialogueInteraction("refused"));
	}

	private void OnReturnToDialoguePressed()
	{
		if (!HasActiveDialogueInteraction())
			return;

		SetDialoguePresentationState();
		QueuePlayerLine(_returnToDialogueButton.Text);
		_gameState.ClearActiveCustomerRequest();
		_requestRevealed = false;
		_sellingMode = false;

		var fallbackNode = _interaction?.GetDialogueNode(_activeDialogueNodeId);
		var returnNode = _interaction?.GetDialogueNode(_requestReturnDialogueNodeId) ?? fallbackNode;
		if (returnNode is null)
		{
			GD.PushError($"CustomerPanel: Cannot return to dialogue node '{_requestReturnDialogueNodeId}'.");
			return;
		}

		QueueDialogueNodeText(returnNode);
		_activeDialogueNodeId = returnNode.Id;
		PlayQueuedDialogueLines(() => FinishShowingDialogueNode(returnNode));
	}

	private bool HasActiveDialogueInteraction()
	{
		return _interaction is not null &&
			_interaction.IsStoryInteraction &&
			_interaction.HasDialogueTree;
	}

	private void ShowDialogueNode(CustomerDialogueNodeDef node)
	{
		_activeDialogueNodeId = node.Id;
		SetDialoguePresentationState();
		QueueDialogueNodeText(node);
		PlayQueuedDialogueLines(() => FinishShowingDialogueNode(node));
	}

	private void QueueDialogueNodeText(CustomerDialogueNodeDef node)
	{
		if (!string.IsNullOrWhiteSpace(node.Text))
			QueueCustomerLine(node.Text);
	}

	private void FinishShowingDialogueNode(CustomerDialogueNodeDef node)
	{
		if (node.Options.Count == 0)
		{
			CompleteDialogueInteraction("dialogue_complete");
			return;
		}

		SetDialogueOptionState(node);
	}

	private void CompleteDialogueInteraction(CustomerDialogueOptionDef option)
	{
		var outcomeId = string.IsNullOrWhiteSpace(option.Id) ? option.Label : option.Id;
		CompleteDialogueInteraction($"dialogue:{outcomeId}");
	}

	private void CompleteDialogueInteraction(string outcome)
	{
		if (_interaction is null)
			return;

		_gameState.RecordStoryCustomerInteractionOutcome(_interaction, outcome);
		_gameState.ClearActiveCustomerRequest();
		_activeDialogueNodeId = string.Empty;
		_requestReturnDialogueNodeId = string.Empty;
		_requestRevealed = false;
		_sellingMode = false;
		_awaitingNextCustomer = true;
		SetSaleResolvedState();
		EmitSignal(SignalName.DialogueResolved);
	}

	private void OnSkipCustomerPressed()
	{
		if (_interaction is null)
			return;

		ApplyOutcomeEffects(_interaction.OnSkipEffects);
		_gameState.RecordStoryCustomerInteractionOutcome(_interaction, GameState.StoryCustomerOutcomeSkipped);
		HidePanel();
		Visible = true;
		EmitSignal(SignalName.CustomerSkipped);
	}

	private void ApplyOutcomeEffects(IReadOnlyList<EffectDef>? effects)
	{
		if (effects is null || effects.Count == 0)
			return;

		foreach (var effect in effects)
			EffectApplier.Apply(_gameState, effect);
	}

	private void BindOrCreateSkipButtons()
	{
		_comeBackTomorrowButton = GetNodeOrNull<Button>("Panel/Margin/VBox/CustomerActions/ComeBackTomorrow");
		_sorryCantHelpYouButton = GetNodeOrNull<Button>("Panel/Margin/VBox/CustomerActions/SorryCantHelpYou");
		var customerVBox = GetNodeOrNull<VBoxContainer>("Panel/Margin/VBox");
		if (customerVBox is null)
		{
			GD.PushError("CustomerPanel: Panel/Margin/VBox not found; cannot create skip buttons.");
			return;
		}

		_customerActions = customerVBox.GetNodeOrNull<HBoxContainer>("CustomerActions");
		if (_customerActions is null)
		{
			_customerActions = new HBoxContainer
			{
				Name = "CustomerActions"
			};
			customerVBox.AddChild(_customerActions);
		}

		BindOrCreatePotionSlotRow(customerVBox);

		_comeBackTomorrowButton = _customerActions.GetNodeOrNull<Button>("ComeBackTomorrow");
		if (_comeBackTomorrowButton is null)
		{
			_comeBackTomorrowButton = new Button
			{
				Name = "ComeBackTomorrow",
				Text = "Come back tomorrow",
				SizeFlagsHorizontal = SizeFlags.ExpandFill
			};
			_customerActions.AddChild(_comeBackTomorrowButton);
		}

		_sorryCantHelpYouButton = _customerActions.GetNodeOrNull<Button>("SorryCantHelpYou");
		if (_sorryCantHelpYouButton is null)
		{
			_sorryCantHelpYouButton = new Button
			{
				Name = "SorryCantHelpYou",
				Text = "Sorry can't help you",
				SizeFlagsHorizontal = SizeFlags.ExpandFill
			};
			_customerActions.AddChild(_sorryCantHelpYouButton);
		}

		_nextCustomerButton = _customerActions.GetNodeOrNull<Button>("NextCustomer");
		if (_nextCustomerButton is null)
		{
			_nextCustomerButton = new Button
			{
				Name = "NextCustomer",
				Text = "Next customer",
				Visible = false,
				SizeFlagsHorizontal = SizeFlags.ExpandFill
			};
			_customerActions.AddChild(_nextCustomerButton);
		}

		_dialogueOptionsContainer = customerVBox.GetNodeOrNull<VBoxContainer>("DialogueOptions");
		if (_dialogueOptionsContainer is null)
		{
			_dialogueOptionsContainer = new VBoxContainer
			{
				Name = "DialogueOptions",
				Visible = false,
				SizeFlagsHorizontal = SizeFlags.ExpandFill
			};
			_dialogueOptionsContainer.AddThemeConstantOverride("separation", 6);
			customerVBox.AddChild(_dialogueOptionsContainer);
		}

		_dialogueOptionButtons.Clear();
		for (var index = 0; index < CustomerInteractionDef.MaxDialogueOptionsPerNode; index += 1)
		{
			var button = _dialogueOptionsContainer.GetNodeOrNull<Button>($"Option{index + 1}");
			if (button is null)
			{
				button = new Button
				{
					Name = $"Option{index + 1}",
					Visible = false,
					SizeFlagsHorizontal = SizeFlags.ExpandFill
				};
				_dialogueOptionsContainer.AddChild(button);
			}

			var optionIndex = index;
			button.Pressed += () => TrySelectDialogueOption(optionIndex);
			_dialogueOptionButtons.Add(button);
		}

		_sellingActions = customerVBox.GetNodeOrNull<HBoxContainer>("SellingActions");
		if (_sellingActions is null)
		{
			_sellingActions = new HBoxContainer
			{
				Name = "SellingActions",
				Visible = false,
				SizeFlagsHorizontal = SizeFlags.ExpandFill
			};
			_sellingActions.AddThemeConstantOverride("separation", 8);
			customerVBox.AddChild(_sellingActions);
		}

		_refusePotionButton = GetOrCreateSellingButton("RefusePotion", "Refuse potion");
		_returnToDialogueButton = GetOrCreateSellingButton("ReturnToDialogue", "Return to dialogue");
	}

	private void BindOrCreatePotionSlotRow(VBoxContainer customerVBox)
	{
		_potionSlotsRow = customerVBox.GetNodeOrNull<HBoxContainer>("PotionSlots");
		if (_potionSlotsRow is null)
		{
			_potionSlotsRow = new HBoxContainer
			{
				Name = "PotionSlots",
				Visible = false,
				CustomMinimumSize = new Vector2(0, CustomerPotionSlotSize),
				SizeFlagsHorizontal = SizeFlags.ExpandFill
			};
			_potionSlotsRow.AddThemeConstantOverride("separation", 8);
			customerVBox.AddChild(_potionSlotsRow);
		}

		if (_customerActions is not null && _potionSlotsRow.GetParent() == customerVBox)
			customerVBox.MoveChild(_potionSlotsRow, _customerActions.GetIndex());

		_potionSlotViews.Clear();
		for (var index = 0; index < CustomerPotionSlotCount; index += 1)
		{
			var slotName = $"PotionSlot{index + 1}";
			var slot = _potionSlotsRow.GetNodeOrNull<InventoryItemSlot>(slotName);
			if (slot is null)
			{
				slot = CreatePotionSlot(slotName);
				_potionSlotsRow.AddChild(slot);
			}

			var icon = slot.GetNodeOrNull<TextureRect>("Icon");
			if (icon is null)
			{
				icon = CreatePotionSlotIcon();
				slot.AddChild(icon);
			}

			var quantity = slot.GetNodeOrNull<Label>("Quantity");
			if (quantity is null)
			{
				quantity = CreatePotionSlotQuantityLabel();
				slot.AddChild(quantity);
			}

			_potionSlotViews.Add(new PotionSlotView(slot, icon, quantity));
		}

		RefreshPotionSlotRow();
	}

	private InventoryItemSlot CreatePotionSlot(string name)
	{
		var slot = new InventoryItemSlot
		{
			Name = name,
			Text = "",
			CustomMinimumSize = new Vector2(CustomerPotionSlotSize, CustomerPotionSlotSize),
			Size = new Vector2(CustomerPotionSlotSize, CustomerPotionSlotSize),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			FocusMode = FocusModeEnum.None,
			Disabled = true
		};
		slot.AddThemeStyleboxOverride("normal", CreatePotionSlotStyleBox());
		slot.AddThemeStyleboxOverride("hover", CreatePotionSlotHoverStyleBox());
		slot.AddThemeStyleboxOverride("disabled", CreatePotionSlotStyleBox());
		slot.SlotActivated += ShowPotionDetail;
		return slot;
	}

	private static TextureRect CreatePotionSlotIcon()
	{
		return new TextureRect
		{
			Name = "Icon",
			Position = new Vector2((CustomerPotionSlotSize - CustomerPotionIconSize) * 0.5f, 8.0f),
			CustomMinimumSize = new Vector2(CustomerPotionIconSize, CustomerPotionIconSize),
			Size = new Vector2(CustomerPotionIconSize, CustomerPotionIconSize),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			MouseFilter = MouseFilterEnum.Ignore
		};
	}

	private static Label CreatePotionSlotQuantityLabel()
	{
		var quantity = new Label
		{
			Name = "Quantity",
			Position = new Vector2(42.0f, 48.0f),
			CustomMinimumSize = new Vector2(24.0f, 18.0f),
			Size = new Vector2(24.0f, 18.0f),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
			Visible = false
		};
		quantity.AddThemeFontSizeOverride("font_size", 12);
		quantity.AddThemeColorOverride("font_color", Colors.White);
		return quantity;
	}

	private static StyleBoxFlat CreatePotionSlotStyleBox()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.08f, 0.09f, 0.12f, 0.88f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			BorderColor = new Color(0.28f, 0.31f, 0.38f, 0.95f),
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomRight = 6,
			CornerRadiusBottomLeft = 6
		};
	}

	private static StyleBoxFlat CreatePotionSlotHoverStyleBox()
	{
		var style = CreatePotionSlotStyleBox();
		style.BorderColor = new Color(0.58f, 0.68f, 0.92f, 1f);
		return style;
	}

	private void SetPotionSlotRowVisible(bool visible)
	{
		if (_potionSlotsRow is null)
			return;

		_potionSlotsRow.Visible = visible;
		if (visible)
			RefreshPotionSlotRow();
	}

	private void RefreshPotionSlotRow()
	{
		if (_potionSlotsRow is null || _potionSlotViews.Count == 0)
			return;

		var potionStacks = _gameState.Inventory
			.Where(stack => IsPotionItem(stack.Key) && stack.Value > 0)
			.OrderBy(stack => DisplayName(stack.Key, _itemCatalog.GetItemName(stack.Key)))
			.ThenBy(stack => stack.Key)
			.Take(CustomerPotionSlotCount)
			.ToList();

		for (var index = 0; index < _potionSlotViews.Count; index += 1)
		{
			if (index < potionStacks.Count)
				SetPotionSlot(_potionSlotViews[index], potionStacks[index].Key, potionStacks[index].Value);
			else
				ClearPotionSlot(_potionSlotViews[index]);
		}
	}

	private void SetPotionSlot(PotionSlotView slotView, string itemId, int quantity)
	{
		if (!_itemCatalog.TryGetItem(itemId, out var item))
		{
			ClearPotionSlot(slotView);
			return;
		}

		var displayName = DisplayName(itemId, item.Name);
		slotView.Button.ItemId = itemId;
		slotView.Button.ItemName = displayName;
		slotView.Button.IconPath = item.IconPath;
		slotView.Button.Quantity = quantity;
		slotView.Button.Disabled = false;
		slotView.Button.Text = "";
		slotView.Button.TooltipText = $"{displayName} x{quantity}";
		slotView.Icon.Texture = UiIconLoader.LoadIcon(item.IconPath);
		slotView.Icon.Visible = true;
		slotView.Quantity.Text = quantity > 1 ? quantity.ToString() : "";
		slotView.Quantity.Visible = quantity > 1;
	}

	private void ShowPotionDetail(string itemId)
	{
		if (string.IsNullOrWhiteSpace(itemId))
			return;
		if (!IsPotionItem(itemId))
			return;

		_inventoryPanel?.OpenItemDetail(itemId);
	}

	private static void ClearPotionSlot(PotionSlotView slotView)
	{
		slotView.Button.ItemId = "";
		slotView.Button.ItemName = "";
		slotView.Button.IconPath = null;
		slotView.Button.Quantity = 0;
		slotView.Button.Disabled = true;
		slotView.Button.Text = "";
		slotView.Button.TooltipText = "";
		slotView.Icon.Texture = null;
		slotView.Icon.Visible = false;
		slotView.Quantity.Text = "";
		slotView.Quantity.Visible = false;
	}

	private Button GetOrCreateSellingButton(string name, string text)
	{
		var button = _sellingActions.GetNodeOrNull<Button>(name);
		if (button is not null)
			return button;

		button = new Button
		{
			Name = name,
			Text = text,
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		_sellingActions.AddChild(button);
		return button;
	}

	private void SetSalePendingState()
	{
		if (_customerActions is not null)
			_customerActions.Visible = true;
		if (_dialogueOptionsContainer is not null)
			_dialogueOptionsContainer.Visible = false;
		if (_sellingActions is not null)
			_sellingActions.Visible = false;
		SetPotionSlotRowVisible(true);

		if (_comeBackTomorrowButton is not null)
		{
			_comeBackTomorrowButton.Text = "Come back tomorrow";
			_comeBackTomorrowButton.Visible = true;
			_comeBackTomorrowButton.Modulate = DefaultButtonModulate;
			_comeBackTomorrowButton.Disabled = false;
		}

		if (_sorryCantHelpYouButton is not null)
		{
			_sorryCantHelpYouButton.Text = "Sorry can't help you";
			_sorryCantHelpYouButton.Visible = true;
			_sorryCantHelpYouButton.Modulate = DefaultButtonModulate;
			_sorryCantHelpYouButton.Disabled = false;
		}

		if (_nextCustomerButton is not null)
			_nextCustomerButton.Visible = false;

		SetDropBoxEnabled(true);
	}

	private void SetDialogueOptionState(CustomerDialogueNodeDef node)
	{
		_visibleDialogueOptions.Clear();
		if (_customerActions is not null)
			_customerActions.Visible = false;
		if (_dialogueOptionsContainer is not null)
			_dialogueOptionsContainer.Visible = true;
		if (_sellingActions is not null)
			_sellingActions.Visible = false;
		SetPotionSlotRowVisible(false);

		foreach (var option in node.Options)
		{
			if (_visibleDialogueOptions.Count >= CustomerInteractionDef.MaxDialogueOptionsPerNode)
				break;
			if (!Requirements.Met(_gameState, option.Requires))
				continue;

			_visibleDialogueOptions.Add(option);
		}

		if (_visibleDialogueOptions.Count == 0)
		{
			CompleteDialogueInteraction("dialogue_no_options");
			return;
		}

		for (var index = 0; index < _dialogueOptionButtons.Count; index += 1)
			SetDialogueOptionButton(_dialogueOptionButtons[index], index);

		if (_nextCustomerButton is not null)
			_nextCustomerButton.Visible = false;

		SetDropBoxEnabled(false);
	}

	private void SetDialoguePresentationState()
	{
		if (_customerActions is not null)
			_customerActions.Visible = false;
		if (_dialogueOptionsContainer is not null)
			_dialogueOptionsContainer.Visible = false;
		if (_sellingActions is not null)
			_sellingActions.Visible = false;
		foreach (var button in _dialogueOptionButtons)
			button.Visible = false;
		SetPotionSlotRowVisible(false);
		SetDropBoxEnabled(false);
	}

	private void SetDialogueOptionButton(Button? button, int optionIndex)
	{
		if (button is null)
			return;

		if (optionIndex < 0 || optionIndex >= _visibleDialogueOptions.Count)
		{
			button.Visible = false;
			return;
		}

		var option = _visibleDialogueOptions[optionIndex];
		button.Text = option.Label;
		button.Visible = true;
		button.Disabled = false;
		button.Modulate = _interaction is not null &&
			_gameState.HasStoryCustomerDialogueOptionSelected(_interaction, option.Id)
				? SeenDialogueOptionModulate
				: DefaultButtonModulate;
	}

	private void SetSellingModeState()
	{
		if (_customerActions is not null)
			_customerActions.Visible = false;
		if (_dialogueOptionsContainer is not null)
			_dialogueOptionsContainer.Visible = false;
		if (_sellingActions is not null)
			_sellingActions.Visible = true;
		SetPotionSlotRowVisible(true);

		if (_refusePotionButton is not null)
		{
			_refusePotionButton.Visible = true;
			_refusePotionButton.Disabled = false;
			_refusePotionButton.Modulate = DefaultButtonModulate;
		}

		if (_returnToDialogueButton is not null)
		{
			_returnToDialogueButton.Visible = true;
			_returnToDialogueButton.Disabled = false;
			_returnToDialogueButton.Modulate = DefaultButtonModulate;
		}

		SetDropBoxEnabled(true);
	}

	private void SetSaleResolvedState()
	{
		if (_customerActions is not null)
			_customerActions.Visible = true;
		if (_dialogueOptionsContainer is not null)
			_dialogueOptionsContainer.Visible = false;
		if (_sellingActions is not null)
			_sellingActions.Visible = false;
		SetPotionSlotRowVisible(false);

		if (_comeBackTomorrowButton is not null)
			_comeBackTomorrowButton.Visible = false;

		if (_sorryCantHelpYouButton is not null)
			_sorryCantHelpYouButton.Visible = false;

		if (_nextCustomerButton is not null)
			_nextCustomerButton.Visible = true;

		foreach (var button in _dialogueOptionButtons)
			button.Visible = false;

		SetDropBoxEnabled(false);
	}

	private void SetDropBoxEnabled(bool enabled)
	{
		if (_sellDropBox is null)
			return;

		_sellDropBox.SetDisabledVisual(!enabled);
		_sellDropBox.MouseFilter = enabled ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
		_sellDropBox.SetAcceptDrops(enabled);
		_sellDropBox.SetHoverHighlight(false);
	}

	private void UpdateCloseShopButtonText()
	{
		var buttonText = _closeShopMode ? "Close Shop" : "Next customer";

		if (_saleResultCloseButton is not null)
			_saleResultCloseButton.Text = buttonText;

		if (_nextCustomerButton is not null)
			_nextCustomerButton.Text = buttonText;
	}

	private sealed class DialogueLine
	{
		public DialogueLine(string speaker, string text)
		{
			Speaker = speaker;
			Text = text;
		}

		public string Speaker { get; }
		public string Text { get; }
	}

	private sealed class PotionSlotView
	{
		public PotionSlotView(InventoryItemSlot button, TextureRect icon, Label quantity)
		{
			Button = button;
			Icon = icon;
			Quantity = quantity;
		}

		public InventoryItemSlot Button { get; }
		public TextureRect Icon { get; }
		public Label Quantity { get; }
	}
}
