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

	public bool SuppressSaleResultPanel { get; set; }

	[Export] public NodePath TitlePath = default!;
	[Export] public NodePath PortraitPath = default!;
	[Export] public NodePath DesiredTraitsPath = default!;
	[Export] public NodePath BadTraitsPath = default!;
	[Export] public NodePath DialoguePath = default!;
	[Export] public NodePath SellDropBoxPath = default!;
	[Export] public NodePath SaleResultPanelPath = default!;
	[Export] public NodePath SaleResultTitlePath = default!;
	[Export] public NodePath SaleResultBodyPath = default!;
	[Export] public NodePath SaleResultCloseButtonPath = default!;
	[Export] public NodePath CloseButtonPath = default!;
	[Export] public NodePath GameStatePath = new("/root/GameState");
	[Export] public NodePath DataDbPath = new("/root/DataDb");
	[Export] public NodePath ItemCatalogPath = new("/root/ItemCatalog");

	private Label _title = default!;
	private TextureRect _portrait = default!;
	private RichTextLabel _desiredTraits = default!;
	private RichTextLabel _badTraits = default!;
	private RichTextLabel _dialogue = default!;
	private CustomerSellDropBox _sellDropBox = default!;
	private Control _saleResultPanel = default!;
	private Label _saleResultTitle = default!;
	private RichTextLabel _saleResultBody = default!;
	private Button _saleResultCloseButton = default!;
	private Button _closeButton = default!;
	private HBoxContainer _customerActions = default!;
	private Button _comeBackTomorrowButton = default!;
	private Button _sorryCantHelpYouButton = default!;
	private Button _nextCustomerButton = default!;
	private CustomerInteractionDef? _interaction;
	private readonly PotionBrewingService _brewingService = new();
	private GameState _gameState = default!;
	private DataDb _dataDb = default!;
	private ItemCatalogService _itemCatalog = default!;
	private bool _closeShopMode;
	private bool _awaitingNextCustomer;
	private const int SuccessDreadChange = -2;
	private const int FailureDreadChange = 4;
	private const string MatchedDesiredColorHex = "#59D959";
	private const string MatchedRiskColorHex = "#E64040";

	public override void _Ready()
	{
		var gameState = GetNodeOrNull<GameState>(GameStatePath);
		if (gameState is null)
		{
			GD.PushError($"CustomerPanel: GameState was not found at '{GameStatePath}'.");
			return;
		}

		var dataDb = GetNodeOrNull<DataDb>(DataDbPath);
		if (dataDb is null)
		{
			GD.PushError($"CustomerPanel: DataDb was not found at '{DataDbPath}'.");
			return;
		}

		var itemCatalog = GetNodeOrNull<ItemCatalogService>(ItemCatalogPath);
		if (itemCatalog is null)
		{
			GD.PushError($"CustomerPanel: ItemCatalog was not found at '{ItemCatalogPath}'.");
			return;
		}

		_gameState = gameState;
		_dataDb = dataDb;
		_itemCatalog = itemCatalog;

		_title = GetNode<Label>(TitlePath);
		_portrait = GetNode<TextureRect>(PortraitPath);
		_desiredTraits = GetNode<RichTextLabel>(DesiredTraitsPath);
		_badTraits = GetNode<RichTextLabel>(BadTraitsPath);
		_dialogue = GetNode<RichTextLabel>(DialoguePath);
		_sellDropBox = GetNode<CustomerSellDropBox>(SellDropBoxPath);
		_saleResultPanel = GetNode<Control>(SaleResultPanelPath);
		_saleResultTitle = GetNode<Label>(SaleResultTitlePath);
		_saleResultBody = GetNode<RichTextLabel>(SaleResultBodyPath);
		_saleResultCloseButton = GetNode<Button>(SaleResultCloseButtonPath);
		_closeButton = GetNode<Button>(CloseButtonPath);
		BindOrCreateSkipButtons();

		_desiredTraits.BbcodeEnabled = true;
		_badTraits.BbcodeEnabled = true;

		MouseFilter = MouseFilterEnum.Ignore;
		//_closeButton.Pressed += HidePanel;
		if (_comeBackTomorrowButton is not null)
			_comeBackTomorrowButton.Pressed += OnSkipCustomerPressed;

		if (_sorryCantHelpYouButton is not null)
			_sorryCantHelpYouButton.Pressed += OnSkipCustomerPressed;
		if (_nextCustomerButton is not null)
			_nextCustomerButton.Pressed += OnSaleResultClosePressed;

		_saleResultCloseButton.Text = "Next customer";
		_saleResultCloseButton.Pressed += OnSaleResultClosePressed;
		_sellDropBox.ItemDropped += OnItemDropped;
		_sellDropBox.ItemHoverPreview += OnSellDropHoverPreview;
		_sellDropBox.HoverPreviewCleared += OnSellDropHoverPreviewCleared;
		UpdateCloseShopButtonText();
		_portrait.Visible = false;
		_saleResultPanel.Visible = false;
		SetSalePendingState();
		Visible = false;
	}

	public override void _ExitTree()
	{
		if (_closeButton is not null)
			_closeButton.Pressed -= HidePanel;
		if (_comeBackTomorrowButton is not null)
			_comeBackTomorrowButton.Pressed -= OnSkipCustomerPressed;
		if (_sorryCantHelpYouButton is not null)
			_sorryCantHelpYouButton.Pressed -= OnSkipCustomerPressed;
		if (_nextCustomerButton is not null)
			_nextCustomerButton.Pressed -= OnSaleResultClosePressed;
		if (_saleResultCloseButton is not null)
			_saleResultCloseButton.Pressed -= OnSaleResultClosePressed;
		if (_sellDropBox is not null)
		{
			_sellDropBox.ItemDropped -= OnItemDropped;
			_sellDropBox.ItemHoverPreview -= OnSellDropHoverPreview;
			_sellDropBox.HoverPreviewCleared -= OnSellDropHoverPreviewCleared;
		}
	}

	public void ShowInteraction(CustomerInteractionDef interaction)
	{
		HideSaleResult();
		_interaction = interaction;
		_awaitingNextCustomer = false;
		var request = interaction.BuildRequest();
		_gameState.SetActiveCustomerRequest(request);
		Visible = true;
		//_title.Text = interaction.Title;
		_dialogue.Text = interaction.Text;
		SetPortrait(interaction.CharacterImagePath);
		SetRequestTraits(request);
		SetSalePendingState();
		EmitSignal(SignalName.InteractionShown, interaction.Id);
	}

	public Button? GetNextCustomerButton()
	{
		return _nextCustomerButton;
	}

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
		_portrait.Texture = null;
		_portrait.Visible = false;
		_desiredTraits.Text = "";
		_badTraits.Text = "";
		_dialogue.Text = "";
		_awaitingNextCustomer = false;
		SetSalePendingState();
		HideSaleResult();
		Visible = false;
	}

	private void OnItemDropped(string itemId)
	{
		if (_interaction is null)
			return;

		if (!_itemCatalog.TryGetItem(itemId, out _))
			return;

		if (!IsPotionItem(itemId))
			return;

		if (!TryResolvePotionScore(itemId, out var brewResult))
			return;

		if (brewResult is null)
			return;

		var saleResult = ApplySale(itemId, brewResult);

		if (SuppressSaleResultPanel)
		{
			_interaction = null;
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
		var scoringIngredients = BuildScoringIngredients(itemId, item);
		brewResult = _brewingService.BrewPotion(
			scoringIngredients,
			request,
			_dataDb.Synergies.ToList());

		return true;
	}

	private List<IngredientDef> BuildScoringIngredients(string itemId, ItemDef fallbackPotionItem)
	{
		if (!_gameState.TryPeekPotionBatch(itemId, out var batchIngredientIds) || batchIngredientIds.Count == 0)
			return new List<IngredientDef> { IngredientDefFactory.FromItemDef(fallbackPotionItem) };

		var ingredients = new List<IngredientDef>();
		foreach (var ingredientId in batchIngredientIds)
		{
			if (!_itemCatalog.TryGetItem(ingredientId, out var ingredientItem))
				continue;

			ingredients.Add(IngredientDefFactory.FromItemDef(ingredientItem));
		}

		if (ingredients.Count == 0)
			return new List<IngredientDef> { IngredientDefFactory.FromItemDef(fallbackPotionItem) };

		return ingredients;
	}

	private (bool IsSuccess, int GoldDelta, int DreadDelta) ApplySale(string itemId, PotionResult brewResult)
	{
		var request = _interaction?.BuildRequest();
		var isSuccess = request is not null && HasAllDesiredTraitsPresent(request, brewResult.Traits);
		var goldDelta = GetSalePrice(itemId);
		var dreadDelta = isSuccess ? SuccessDreadChange : FailureDreadChange;

		_gameState.AddGold(goldDelta);
		_gameState.AddDread(dreadDelta);

		_gameState.ConsumeItem(itemId, 1);
		return (isSuccess, goldDelta, dreadDelta);
	}

	private void ShowSaleResult(string itemId, PotionResult brewResult)
	{
		_gameState.ClearActiveCustomerRequest();
		var outcomeText = BuildOutcomeText(itemId, brewResult);
		_dialogue.Text = outcomeText;
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
		SetSalePendingState();
		HideSaleResult();
		_interaction = null;
		EmitSignal(SignalName.SaleResultClosed);
	}

	private string BuildOutcomeText(string itemId, PotionResult brewResult)
	{
		var lines = new List<string>();
		var itemName = _itemCatalog.GetItemName(itemId);
		itemName = DisplayName(itemId, itemName);
		lines.Add($"Potion: {itemName}");

		var request = _interaction?.BuildRequest();
		var isSuccess = request is not null && HasAllDesiredTraitsPresent(request, brewResult.Traits);
		lines.Add($"Sale: {(isSuccess ? "Success" : "Failure")}");
		lines.Add(string.Empty);
		lines.Add("Desired Traits:");
		lines.Add(FormatTraitDictionaryForResult(request?.DesiredTraits));
		lines.Add(string.Empty);
		lines.Add("Potion Traits:");
		lines.Add(FormatTraitDictionaryForResult(brewResult.Traits));

		return string.Join("\n", lines);
	}

	private static string FormatTraitDictionaryForResult(IReadOnlyDictionary<string, int>? values)
	{
		if (values is null || values.Count == 0)
			return "None";

		var lines = values
			.Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
			.OrderByDescending(pair => pair.Value)
			.ThenBy(pair => pair.Key)
			.Select(pair => $"- {pair.Key}: {pair.Value}")
			.ToList();

		if (lines.Count == 0)
			return "None";

		return string.Join("\n", lines);
	}

	private void SetRequestTraits(CustomerRequestDef request)
	{
		_desiredTraits.Text = FormatTraitListWithMatches(request.DesiredTraits, null, MatchedDesiredColorHex);
		_badTraits.Text = FormatTraitListWithMatches(request.BadTraits, null, MatchedRiskColorHex);
	}

	private void SetPortrait(string? portraitPath)
	{
		if (string.IsNullOrWhiteSpace(portraitPath))
		{
			_portrait.Texture = null;
			_portrait.Visible = false;
			return;
		}

		var texture = ResourceLoader.Load<Texture2D>(portraitPath);
		_portrait.Texture = texture;
		_portrait.Visible = texture is not null;
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
			return;

		if (!TryResolvePotionScore(itemId, out var brewResult))
		{
			SetRequestTraits(_interaction.BuildRequest());
			return;
		}

		if (brewResult is null)
		{
			SetRequestTraits(_interaction.BuildRequest());
			return;
		}

		var request = _interaction.BuildRequest();
		_desiredTraits.Text = FormatTraitListWithMatches(request.DesiredTraits, brewResult.Traits, MatchedDesiredColorHex);
		_badTraits.Text = FormatTraitListWithMatches(request.BadTraits, brewResult.Risks, MatchedRiskColorHex);
	}

	private void OnSellDropHoverPreviewCleared()
	{
		if (_interaction is null)
			return;

		SetRequestTraits(_interaction.BuildRequest());
	}

	private static string FormatTraitListWithMatches(
		Dictionary<string, int> requiredValues,
		IReadOnlyDictionary<string, int>? producedValues,
		string matchedColorHex)
	{
		if (requiredValues is null || requiredValues.Count == 0)
			return "None";

		return string.Join(
			"\n",
			requiredValues
				.OrderByDescending(x => x.Value)
				.ThenBy(x => x.Key)
				.Select(pair => FormatTraitLine(pair.Key, pair.Value, producedValues, matchedColorHex)));
	}

	private static string FormatTraitLine(
		string key,
		int requiredValue,
		IReadOnlyDictionary<string, int>? producedValues,
		string matchedColorHex)
	{
		var safeKey = EscapeBbCodeText(key);
		var line = $"{safeKey}: {requiredValue}";
		if (producedValues is null)
			return line;

		if (!TryGetValueIgnoreCase(producedValues, key, out var producedValue))
			return line;

		if (producedValue <= 0)
			return line;

		return $"[color={matchedColorHex}]{line}[/color]";
	}

	private static bool HasAllDesiredTraitsPresent(CustomerRequestDef request, IReadOnlyDictionary<string, int> producedTraits)
	{
		var totalDesiredTraitCount = 0;
		var matchedDesiredTraitCount = 0;

		foreach (var desiredTrait in request.DesiredTraits)
		{
			if (string.IsNullOrWhiteSpace(desiredTrait.Key))
				continue;

			totalDesiredTraitCount += 1;

			if (!TryGetValueIgnoreCase(producedTraits, desiredTrait.Key, out var producedValue))
				continue;

			if (producedValue <= 0)
				continue;

			matchedDesiredTraitCount += 1;
		}

		if (totalDesiredTraitCount == 0)
			return true;

		var requiredMatchCount = GetRequiredDesiredTraitMatchCount(totalDesiredTraitCount);
		return matchedDesiredTraitCount >= requiredMatchCount;
	}

	private static int GetRequiredDesiredTraitMatchCount(int totalDesiredTraitCount)
	{
		if (totalDesiredTraitCount <= 0)
			return 0;

		if (totalDesiredTraitCount >= 3)
			return totalDesiredTraitCount - 1;

		return totalDesiredTraitCount;
	}

	private static bool TryGetValueIgnoreCase(
		IReadOnlyDictionary<string, int> values,
		string key,
		out int value)
	{
		foreach (var pair in values)
		{
			if (!string.Equals(pair.Key, key, System.StringComparison.OrdinalIgnoreCase))
				continue;

			value = pair.Value;
			return true;
		}

		value = 0;
		return false;
	}

	private static string EscapeBbCodeText(string text)
	{
		return text
			.Replace("[", "[lb]")
			.Replace("]", "[rb]");
	}

	private void OnSkipCustomerPressed()
	{
		if (_interaction is null)
			return;

		HidePanel();
		EmitSignal(SignalName.CustomerSkipped);
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
	}

	private void SetSalePendingState()
	{
		if (_comeBackTomorrowButton is not null)
			_comeBackTomorrowButton.Visible = true;

		if (_sorryCantHelpYouButton is not null)
			_sorryCantHelpYouButton.Visible = true;

		if (_nextCustomerButton is not null)
			_nextCustomerButton.Visible = false;

		if (_sellDropBox is not null)
			_sellDropBox.MouseFilter = MouseFilterEnum.Stop;
	}

	private void SetSaleResolvedState()
	{
		if (_comeBackTomorrowButton is not null)
			_comeBackTomorrowButton.Visible = false;

		if (_sorryCantHelpYouButton is not null)
			_sorryCantHelpYouButton.Visible = false;

		if (_nextCustomerButton is not null)
			_nextCustomerButton.Visible = true;

		if (_sellDropBox is not null)
			_sellDropBox.MouseFilter = MouseFilterEnum.Ignore;
	}

	private void UpdateCloseShopButtonText()
	{
		var buttonText = _closeShopMode ? "Close Shop" : "Next customer";

		if (_saleResultCloseButton is not null)
			_saleResultCloseButton.Text = buttonText;

		if (_nextCustomerButton is not null)
			_nextCustomerButton.Text = buttonText;
	}

}
