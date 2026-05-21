using System.Collections.Generic;
using System.Linq;
using Godot;
using OccultShop.Autoload;
using OccultShop.Models;
using OccultShop.Systems;

namespace OccultShop.UI;

public partial class CustomerPanel : Control
{
	[Export] public NodePath TitlePath = default!;
	[Export] public NodePath PortraitPath = default!;
	[Export] public NodePath DialoguePath = default!;
	[Export] public NodePath SellDropBoxPath = default!;
	[Export] public NodePath ConfirmDialogPath = default!;
	[Export] public NodePath ConfirmDialogLabelPath = default!;
	[Export] public NodePath SaleResultPanelPath = default!;
	[Export] public NodePath SaleResultTitlePath = default!;
	[Export] public NodePath SaleResultBodyPath = default!;
	[Export] public NodePath SaleResultCloseButtonPath = default!;
	[Export] public NodePath CloseButtonPath = default!;

	private Label _title = default!;
	private TextureRect _portrait = default!;
	private RichTextLabel _dialogue = default!;
	private CustomerSellDropBox _sellDropBox = default!;
	private ConfirmationDialog _confirmDialog = default!;
	private Label _confirmDialogLabel = default!;
	private Control _saleResultPanel = default!;
	private Label _saleResultTitle = default!;
	private RichTextLabel _saleResultBody = default!;
	private Button _saleResultCloseButton = default!;
	private Button _closeButton = default!;
	private CustomerInteractionDef? _interaction;
	private string? _pendingItemId;
	private readonly PotionBrewingService _brewingService = new();
	private const float SuccessScoreThreshold = 60.0f;
    private const int SuccessGoldGain = 45;
    private const int SuccessDreadChange = -2;
    private const int FailureGoldGain = 15;
    private const int FailureDreadChange = 4;

	public override void _Ready()
	{
		_title = GetNode<Label>(TitlePath);
		_portrait = GetNode<TextureRect>(PortraitPath);
		_dialogue = GetNode<RichTextLabel>(DialoguePath);
		_sellDropBox = GetNode<CustomerSellDropBox>(SellDropBoxPath);
		_confirmDialog = GetNode<ConfirmationDialog>(ConfirmDialogPath);
		_confirmDialogLabel = GetNode<Label>(ConfirmDialogLabelPath);
		_saleResultPanel = GetNode<Control>(SaleResultPanelPath);
		_saleResultTitle = GetNode<Label>(SaleResultTitlePath);
		_saleResultBody = GetNode<RichTextLabel>(SaleResultBodyPath);
		_saleResultCloseButton = GetNode<Button>(SaleResultCloseButtonPath);
		_closeButton = GetNode<Button>(CloseButtonPath);

		MouseFilter = MouseFilterEnum.Ignore;
		_closeButton.Pressed += HidePanel;
		_saleResultCloseButton.Pressed += HideSaleResult;
		_sellDropBox.Connect("ItemDropped", new Callable(this, nameof(OnItemDropped)));
		_confirmDialog.Confirmed += ConfirmPendingSale;
		_portrait.Visible = false;
		_saleResultPanel.Visible = false;
		Visible = false;
	}

	public void ShowInteraction(CustomerInteractionDef interaction)
	{
		HideSaleResult();
		_interaction = interaction;
		GameState.SetActiveCustomerRequest(interaction.BuildRequest());
		Visible = true;
		_title.Text = interaction.Title;
		_dialogue.Text = interaction.Text;
		SetPortrait(interaction.CharacterImagePath);
	}

	public void HidePanel()
	{
		_interaction = null;
		_pendingItemId = null;
		GameState.ClearActiveCustomerRequest();
		_portrait.Texture = null;
		_portrait.Visible = false;
		_confirmDialog.Hide();
		HideSaleResult();
		Visible = false;
	}

	private void OnItemDropped(string itemId)
	{
		if (_interaction is null)
			return;

		if (!DataDb.Items.ContainsKey(itemId))
		{
			_confirmDialogLabel.Text = "That item is not recognized.";
			_confirmDialog.PopupCentered();
			return;
		}

		if (!IsPotionItem(itemId))
		{
			_confirmDialogLabel.Text = "Customers only accept brewed potions.";
			_confirmDialog.PopupCentered();
			return;
		}

		var itemName = DataDb.Items.TryGetValue(itemId, out var item) ? item.Name : itemId;
		itemName = DisplayName(itemId, itemName);
		_pendingItemId = itemId;
		_confirmDialogLabel.Text = $"Sell {itemName} to this customer?";
		_confirmDialog.PopupCentered();
	}

	private void ConfirmPendingSale()
	{
		if (string.IsNullOrWhiteSpace(_pendingItemId))
			return;

		if (!TryResolvePotionScore(_pendingItemId, out var brewResult))
			return;

		if (brewResult is null)
		{
			_confirmDialogLabel.Text = "No customer score could be calculated.";
			_confirmDialog.PopupCentered();
			return;
		}

		var itemId = _pendingItemId;
		_pendingItemId = null;
		_confirmDialog.Hide();
		ApplySale(itemId, brewResult);
		ShowSaleResult(itemId, brewResult);
	}

	private bool TryResolvePotionScore(string itemId, out PotionResult? brewResult)
	{
		brewResult = null;

		if (_interaction is null)
			return false;

		if (!DataDb.Items.TryGetValue(itemId, out var item))
			return false;

		var request = _interaction.BuildRequest();
		var scoringIngredients = BuildScoringIngredients(itemId, item);
		brewResult = _brewingService.BrewPotion(
			scoringIngredients,
			request,
			DataDb.Synergies.ToList());

		return true;
	}

	private List<IngredientDef> BuildScoringIngredients(string itemId, ItemDef fallbackPotionItem)
	{
		if (!GameState.TryPeekPotionBatch(itemId, out var batchIngredientIds) || batchIngredientIds.Count == 0)
			return new List<IngredientDef> { BuildPotionIngredientDef(fallbackPotionItem) };

		var ingredients = new List<IngredientDef>();
		foreach (var ingredientId in batchIngredientIds)
		{
			if (!DataDb.Items.TryGetValue(ingredientId, out var ingredientItem))
				continue;

			ingredients.Add(BuildPotionIngredientDef(ingredientItem));
		}

		if (ingredients.Count == 0)
			return new List<IngredientDef> { BuildPotionIngredientDef(fallbackPotionItem) };

		return ingredients;
	}

	private static IngredientDef BuildPotionIngredientDef(ItemDef item)
	{
		return new IngredientDef
		{
			Id = item.Id,
			Name = item.Name,
			Quality = item.Quality,
			Traits = new Dictionary<string, int>(item.Traits),
			Risks = new Dictionary<string, int>(item.Risks),
			Tags = new List<string>(item.Tags)
		};
	}

	private void ApplySale(string itemId, PotionResult brewResult)
	{
		var isSuccess = brewResult.FinalScore >= SuccessScoreThreshold;

		if (isSuccess)
		{
			GameState.AddGold(SuccessGoldGain);
			GameState.AddDread(SuccessDreadChange);
		}
		else
		{
			GameState.AddGold(FailureGoldGain);
			GameState.AddDread(FailureDreadChange);
		}

		GameState.ConsumeItem(itemId, 1);
	}

	private void ShowSaleResult(string itemId, PotionResult brewResult)
	{
		Visible = false;
		GameState.ClearActiveCustomerRequest();
		_saleResultTitle.Text = $"Potion Score: {brewResult.FinalScore:0.##} ({brewResult.Grade})";
		_saleResultBody.Text = BuildOutcomeText(itemId, brewResult);
		_saleResultPanel.Visible = true;
	}

	private void HideSaleResult()
	{
		_saleResultPanel.Visible = false;
		_saleResultBody.Text = "";
	}

	private string BuildOutcomeText(string itemId, PotionResult brewResult)
	{
		var lines = new List<string>();
		var itemName = DataDb.Items.TryGetValue(itemId, out var item) ? item.Name : itemId;
		itemName = DisplayName(itemId, itemName);
		lines.Add($"Sold: {itemName}");
		lines.Add($"Q={brewResult.IngredientQualityScore}, F={brewResult.EffectFitScore}, Y={brewResult.SynergyScore}, T={brewResult.StabilityScore}, P={brewResult.PenaltyScore}");

		var isSuccess = brewResult.FinalScore >= SuccessScoreThreshold;
		if (isSuccess)
		{
			lines.Add($"Gold gained: {SuccessGoldGain}");
			lines.Add($"Dread reduced: {System.Math.Abs(SuccessDreadChange)}");
		}
		else
		{
			lines.Add($"Gold gained: {FailureGoldGain}");
			lines.Add($"Dread gained: {FailureDreadChange}");
		}

		return string.Join("\n", lines);
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
		if (!DataDb.Items.TryGetValue(itemId, out var item))
			return false;

		return item.Tags.Any(tag => string.Equals(tag, "potion", System.StringComparison.OrdinalIgnoreCase));
	}

	private string DisplayName(string itemId, string fallbackName)
	{
		if (!IsPotionItem(itemId))
			return fallbackName;

		var customName = GameState.GetPotionDisplayName(itemId);
		return string.IsNullOrWhiteSpace(customName) ? fallbackName : customName;
	}

	private GameState GameState => GetTree().Root.GetNode<GameState>("/root/GameState");
	private DataDb DataDb => GetTree().Root.GetNode<DataDb>("/root/DataDb");
}
