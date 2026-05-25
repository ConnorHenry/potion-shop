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
	private CustomerInteractionDef? _interaction;
	private readonly PotionBrewingService _brewingService = new();
	private GameState _gameState = default!;
	private DataDb _dataDb = default!;
	private const float SuccessScoreThreshold = 60.0f;
	private const int SuccessDreadChange = -2;
	private const int FailureDreadChange = 4;

	public override void _Ready()
	{
		var gameState = GetNodeOrNull<GameState>("/root/GameState");
		if (gameState is null)
		{
			GD.PushError("CustomerPanel: /root/GameState was not found.");
			return;
		}

		var dataDb = GetNodeOrNull<DataDb>("/root/DataDb");
		if (dataDb is null)
		{
			GD.PushError("CustomerPanel: /root/DataDb was not found.");
			return;
		}

		_gameState = gameState;
		_dataDb = dataDb;

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

		MouseFilter = MouseFilterEnum.Ignore;
		_closeButton.Pressed += HidePanel;
		_saleResultCloseButton.Pressed += HideSaleResult;
		_sellDropBox.ItemDropped += OnItemDropped;
		_portrait.Visible = false;
		_saleResultPanel.Visible = false;
		Visible = false;
	}

	public override void _ExitTree()
	{
		if (_closeButton is not null)
			_closeButton.Pressed -= HidePanel;
		if (_saleResultCloseButton is not null)
			_saleResultCloseButton.Pressed -= HideSaleResult;
		if (_sellDropBox is not null)
			_sellDropBox.ItemDropped -= OnItemDropped;
	}

	public void ShowInteraction(CustomerInteractionDef interaction)
	{
		HideSaleResult();
		_interaction = interaction;
		var request = interaction.BuildRequest();
		_gameState.SetActiveCustomerRequest(request);
		Visible = true;
		_title.Text = interaction.Title;
		_dialogue.Text = interaction.Text;
		SetPortrait(interaction.CharacterImagePath);
		SetRequestTraits(request);
	}

	public void HidePanel()
	{
		_interaction = null;
		_gameState.ClearActiveCustomerRequest();
		_portrait.Texture = null;
		_portrait.Visible = false;
		_desiredTraits.Text = "";
		_badTraits.Text = "";
		HideSaleResult();
		Visible = false;
	}

	private void OnItemDropped(string itemId)
	{
		if (_interaction is null)
			return;

		if (!ItemCatalog.TryGetItem(itemId, out _))
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
	}

	private bool TryResolvePotionScore(string itemId, out PotionResult? brewResult)
	{
		brewResult = null;

		if (_interaction is null)
			return false;

		if (!ItemCatalog.TryGetItem(itemId, out var item))
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
			return new List<IngredientDef> { BuildPotionIngredientDef(fallbackPotionItem) };

		var ingredients = new List<IngredientDef>();
		foreach (var ingredientId in batchIngredientIds)
		{
			if (!ItemCatalog.TryGetItem(ingredientId, out var ingredientItem))
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

	private (bool IsSuccess, int GoldDelta, int DreadDelta) ApplySale(string itemId, PotionResult brewResult)
	{
		var isSuccess = brewResult.FinalScore >= SuccessScoreThreshold;
		var goldDelta = GetSalePrice(itemId);
		var dreadDelta = isSuccess ? SuccessDreadChange : FailureDreadChange;

		_gameState.AddGold(goldDelta);
		_gameState.AddDread(dreadDelta);

		_gameState.ConsumeItem(itemId, 1);
		return (isSuccess, goldDelta, dreadDelta);
	}

	private void ShowSaleResult(string itemId, PotionResult brewResult)
	{
		Visible = false;
		_gameState.ClearActiveCustomerRequest();
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
		var itemName = ItemCatalog.GetItemName(itemId);
		itemName = DisplayName(itemId, itemName);
		lines.Add($"Sold: {itemName}");
		lines.Add($"Q={brewResult.IngredientQualityScore}, F={brewResult.EffectFitScore}, Y={brewResult.SynergyScore}, T={brewResult.StabilityScore}, P={brewResult.PenaltyScore}");

		var isSuccess = brewResult.FinalScore >= SuccessScoreThreshold;
		var salePrice = GetSalePrice(itemId);
		if (isSuccess)
		{
			lines.Add($"Gold gained: {salePrice}");
			lines.Add($"Dread reduced: {System.Math.Abs(SuccessDreadChange)}");
		}
		else
		{
			lines.Add($"Gold gained: {salePrice}");
			lines.Add($"Dread gained: {FailureDreadChange}");
		}

		return string.Join("\n", lines);
	}

	private void SetRequestTraits(CustomerRequestDef request)
	{
		_desiredTraits.Text = FormatTraitList(request.DesiredTraits);
		_badTraits.Text = FormatTraitList(request.BadTraits);
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
		return ItemCatalog.IsPotion(itemId);
	}

	private int GetSalePrice(string itemId)
	{
		if (_gameState.TryGetPotionBasePrice(itemId, out var potionBasePrice))
			return System.Math.Max(0, potionBasePrice);

		if (ItemCatalog.TryGetItem(itemId, out var item))
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

	private static string FormatTraitList(Dictionary<string, int> values)
	{
		if (values is null || values.Count == 0)
			return "None";

		return string.Join("\n",
			values
				.OrderByDescending(x => x.Value)
				.ThenBy(x => x.Key)
				.Select(x => $"{x.Key}: {x.Value}"));
	}

}
