using Godot;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using OccultShop.Autoload;
using OccultShop.Models;
using OccultShop.Systems;

namespace OccultShop.UI;

public partial class BrewPanel : Control
{
	private const string DefaultPotionIconPath = "res://Assets/Items/sight_tonic.svg";
	private const string PotionIconsDirectoryPath = "res://Assets/Potions";
	private const int BrewedPotionOutputQuantity = 1;

	[Export] public NodePath BrewBoxPath = default!;
	[Export] public NodePath IngredientSlotOnePath = default!;
	[Export] public NodePath IngredientSlotTwoPath = default!;
	[Export] public NodePath IngredientSlotThreePath = default!;
	[Export] public NodePath IngredientSlotOneContainerPath = default!;
	[Export] public NodePath IngredientSlotTwoContainerPath = default!;
	[Export] public NodePath IngredientSlotThreeContainerPath = default!;
	[Export] public NodePath IngredientSlotOneLabelPath = default!;
	[Export] public NodePath IngredientSlotTwoLabelPath = default!;
	[Export] public NodePath IngredientSlotThreeLabelPath = default!;
	[Export] public NodePath PotionNamePreviewLabelPath = default!;
	[Export] public NodePath ResultLabelPath = default!;
	[Export] public NodePath PricePreviewLabelPath = default!;
	[Export] public NodePath TraitPreviewLabelPath = default!;
	[Export] public NodePath RiskPreviewLabelPath = default!;
	[Export] public NodePath RiskStatusIconLabelPath = default!;
	[Export] public NodePath RiskStatusLabelPath = default!;
	[Export] public NodePath IngredientCountLabelPath = default!;
	[Export] public NodePath BrewButtonPath = default!;
	[Export] public NodePath ClearButtonPath = default!;

	private Button _closeButton = default!;
	private BrewDropBox _brewBox = default!;
	private TextureRect _ingredientSlotOne = default!;
	private TextureRect _ingredientSlotTwo = default!;
	private TextureRect _ingredientSlotThree = default!;
	private PanelContainer _ingredientSlotOneContainer = default!;
	private PanelContainer _ingredientSlotTwoContainer = default!;
	private PanelContainer _ingredientSlotThreeContainer = default!;
	private Label _ingredientSlotOneLabel = default!;
	private Label _ingredientSlotTwoLabel = default!;
	private Label _ingredientSlotThreeLabel = default!;
	private Label _potionNamePreviewLabel = default!;
	private Label _resultLabel = default!;
	private Label _pricePreviewLabel = default!;
	private Label _traitPreviewLabel = default!;
	private Label _riskPreviewLabel = default!;
	private Label _riskStatusIconLabel = default!;
	private Label _riskStatusLabel = default!;
	private Label _ingredientCountLabel = default!;
	private Button _brewButton = default!;
	private Button _clearButton = default!;
	private RuntimeContentDb _runtimeContentDb = default!;
	private DataDb _dataDb = default!;
	private GameState _gameState = default!;
	private readonly List<string> _queuedIngredients = new();
	private readonly PotionBrewingService _brewingService = new();
	private string _previewPotionCombinationKey = string.Empty;
	private string _previewPotionName = string.Empty;
	private Control.GuiInputEventHandler? _slotOneGuiInputHandler;
	private Control.GuiInputEventHandler? _slotTwoGuiInputHandler;
	private Control.GuiInputEventHandler? _slotThreeGuiInputHandler;
	private int _draggingSlotIndex = -1;
	private Vector2 _dragStartGlobalPosition = Vector2.Zero;
	private bool _slotDragThresholdReached;

	public override void _Ready()
	{
		var runtimeContentDb = GetNodeOrNull<RuntimeContentDb>("/root/RuntimeContentDb");
		if (runtimeContentDb is null)
		{
			GD.PushError("BrewPanel: /root/RuntimeContentDb was not found.");
			return;
		}

		var dataDb = GetNodeOrNull<DataDb>("/root/DataDb");
		if (dataDb is null)
		{
			GD.PushError("BrewPanel: /root/DataDb was not found.");
			return;
		}

		var gameState = GetNodeOrNull<GameState>("/root/GameState");
		if (gameState is null)
		{
			GD.PushError("BrewPanel: /root/GameState was not found.");
			return;
		}

		_runtimeContentDb = runtimeContentDb;
		_dataDb = dataDb;
		_gameState = gameState;

		_brewBox = GetNode<BrewDropBox>(BrewBoxPath);
		_ingredientSlotOne = GetNode<TextureRect>(IngredientSlotOnePath);
		_ingredientSlotTwo = GetNode<TextureRect>(IngredientSlotTwoPath);
		_ingredientSlotThree = GetNode<TextureRect>(IngredientSlotThreePath);
		_ingredientSlotOneContainer = GetNode<PanelContainer>(IngredientSlotOneContainerPath);
		_ingredientSlotTwoContainer = GetNode<PanelContainer>(IngredientSlotTwoContainerPath);
		_ingredientSlotThreeContainer = GetNode<PanelContainer>(IngredientSlotThreeContainerPath);
		_ingredientSlotOneLabel = GetNode<Label>(IngredientSlotOneLabelPath);
		_ingredientSlotTwoLabel = GetNode<Label>(IngredientSlotTwoLabelPath);
		_ingredientSlotThreeLabel = GetNode<Label>(IngredientSlotThreeLabelPath);
		_potionNamePreviewLabel = GetNode<Label>(PotionNamePreviewLabelPath);
		_resultLabel = GetNode<Label>(ResultLabelPath);
		_pricePreviewLabel = GetNode<Label>(PricePreviewLabelPath);
		_traitPreviewLabel = GetNode<Label>(TraitPreviewLabelPath);
		_riskPreviewLabel = GetNode<Label>(RiskPreviewLabelPath);
		_riskStatusIconLabel = GetNode<Label>(RiskStatusIconLabelPath);
		_riskStatusLabel = GetNode<Label>(RiskStatusLabelPath);
		_ingredientCountLabel = GetNode<Label>(IngredientCountLabelPath);
		_brewButton = GetNode<Button>(BrewButtonPath);
		_clearButton = GetNode<Button>(ClearButtonPath);

		SetInteractiveCursor(_ingredientSlotOneContainer);
		SetInteractiveCursor(_ingredientSlotTwoContainer);
		SetInteractiveCursor(_ingredientSlotThreeContainer);

		MouseFilter = MouseFilterEnum.Ignore;
		_brewBox.ItemDropped += TryQueueIngredient;
		_brewButton.Pressed += TryBrew;
		_clearButton.Pressed += ClearQueue;
		_slotOneGuiInputHandler = @event => HandleIngredientSlotGuiInput(0, @event);
		_slotTwoGuiInputHandler = @event => HandleIngredientSlotGuiInput(1, @event);
		_slotThreeGuiInputHandler = @event => HandleIngredientSlotGuiInput(2, @event);
		_ingredientSlotOneContainer.GuiInput += _slotOneGuiInputHandler;
		_ingredientSlotTwoContainer.GuiInput += _slotTwoGuiInputHandler;
		_ingredientSlotThreeContainer.GuiInput += _slotThreeGuiInputHandler;
		Visible = false;
		RefreshIngredientIcons();
	}

	public override void _ExitTree()
	{
		if (_brewBox is not null)
			_brewBox.ItemDropped -= TryQueueIngredient;
		if (_brewButton is not null)
			_brewButton.Pressed -= TryBrew;
		if (_clearButton is not null)
			_clearButton.Pressed -= ClearQueue;
		if (_slotOneGuiInputHandler is not null)
			_ingredientSlotOneContainer.GuiInput -= _slotOneGuiInputHandler;
		if (_slotTwoGuiInputHandler is not null)
			_ingredientSlotTwoContainer.GuiInput -= _slotTwoGuiInputHandler;
		if (_slotThreeGuiInputHandler is not null)
			_ingredientSlotThreeContainer.GuiInput -= _slotThreeGuiInputHandler;
	}

	public void Toggle()
	{
		Visible = !Visible;
	}

	public void HidePanel()
	{
		ReturnQueuedIngredients();
		ResetSlotDragState();
		Visible = false;
		_resultLabel.Text = "";
		RefreshIngredientIcons();
	}

	public override void _Input(InputEvent @event)
	{
		if (!Visible)
			return;

		if (_draggingSlotIndex < 0)
			return;

		if (@event is InputEventMouseMotion mouseMotion)
		{
			if (_slotDragThresholdReached)
				return;

			const float dragThresholdPixels = 8.0f;
			if (mouseMotion.GlobalPosition.DistanceTo(_dragStartGlobalPosition) >= dragThresholdPixels)
				_slotDragThresholdReached = true;

			return;
		}

		if (@event is not InputEventMouseButton mouseButton)
			return;

		if (mouseButton.ButtonIndex != MouseButton.Left || mouseButton.Pressed)
			return;

		if (_slotDragThresholdReached && !IsPointInsideAnyIngredientSlot(mouseButton.GlobalPosition))
			RemoveQueuedIngredientAt(_draggingSlotIndex);

		ResetSlotDragState();
	}

	public void TryQueueIngredient(string itemId)
	{
		if (!ItemCatalog.TryGetItem(itemId, out var item))
		{
			_resultLabel.Text = "That item is not recognized.";
			return;
		}

		if (!IsIngredient(item))
		{
			_resultLabel.Text = IsPotion(itemId)
				? "Brewing only accepts ingredients, not potions."
				: "Brewing only accepts ingredients.";
			return;
		}

		if (_queuedIngredients.Any(x => string.Equals(x, itemId, System.StringComparison.OrdinalIgnoreCase)))
		{
			_resultLabel.Text = "Each ingredient can only be used once per potion.";
			return;
		}

		if (_queuedIngredients.Count >= 3)
		{
			_resultLabel.Text = "Brewing requires exactly 3 ingredients.";
			return;
		}

		if (!_gameState.HasItem(itemId, 1))
		{
			_resultLabel.Text = "Not enough stock for that ingredient.";
			return;
		}

		if (!_gameState.ConsumeItem(itemId, 1))
		{
			_resultLabel.Text = "Could not take that ingredient.";
			return;
		}

		_queuedIngredients.Add(itemId);
		_resultLabel.Text = "";
		RefreshIngredientIcons();
	}

	private void ClearQueue()
	{
		ResetSlotDragState();
		ReturnQueuedIngredients();
		_queuedIngredients.Clear();
		_resultLabel.Text = "";
		RefreshIngredientIcons();
	}

	private void TryBrew()
	{
		if (_queuedIngredients.Count != 3)
		{
			_resultLabel.Text = "Brewing requires exactly 3 ingredients.";
			return;
		}

		if (!TryBuildIngredientDefs(_queuedIngredients, out var ingredientDefs, out var ingredientError))
		{
			_resultLabel.Text = ingredientError;
			return;
		}

		var brewResult = _brewingService.BrewPotion(
			ingredientDefs,
			null,
			_dataDb.Synergies.ToList());

		var potionBasePrice = CalculateIngredientTotalPrice(_queuedIngredients);
		var brewCost = CalculateBrewCost(potionBasePrice, brewResult);
		if (_gameState.Gold < brewCost)
		{
			_resultLabel.Text = $"Need {brewCost} gold to brew this potion.";
			return;
		}

		_gameState.AddGold(-brewCost);

		var combinationKey = BuildCombinationKey(_queuedIngredients);
		var potionDisplayName = GetPreviewPotionName(combinationKey);
		var isNewCombination = !_gameState.TryGetPotionForCombination(combinationKey, out var potionItemId);
		if (isNewCombination)
		{
			potionItemId = $"brew_{_gameState.PotionDisplayNames.Count + 1}";
			var iconPath = ResolvePotionIconPath();

			_runtimeContentDb.RegisterRuntimePotionItem(
				potionItemId,
				potionDisplayName,
				iconPath,
				potionBasePrice,
				brewResult.IngredientQualityScore,
				new Dictionary<string, int>(brewResult.Traits),
				new Dictionary<string, int>(brewResult.Risks));

			_gameState.SetPotionForCombination(combinationKey, potionItemId);
			_gameState.SetPotionDisplayName(potionItemId, potionDisplayName);
		}

		_gameState.RegisterPotionBasePrice(potionItemId, potionBasePrice);
		_runtimeContentDb.TrySetRuntimeItemBasePrice(potionItemId, potionBasePrice);

		_gameState.RecordPotionRecipe(potionItemId, _queuedIngredients);
		_gameState.AddItem(potionItemId, BrewedPotionOutputQuantity);
		_gameState.RecordPotionBatch(potionItemId, _queuedIngredients);
		_queuedIngredients.Clear();
		ResetSlotDragState();
		RefreshIngredientIcons();
		_resultLabel.Text = BuildBrewResultText(potionItemId, brewResult);
	}

	private void ReturnQueuedIngredients()
	{
		foreach (var itemId in _queuedIngredients)
			_gameState.AddItem(itemId, 1);
	}

	private void HandleIngredientSlotGuiInput(int slotIndex, InputEvent @event)
	{
		if (slotIndex < 0 || slotIndex >= _queuedIngredients.Count)
			return;

		if (@event is InputEventMouseButton rightMouseButton &&
			rightMouseButton.ButtonIndex == MouseButton.Right &&
			rightMouseButton.Pressed)
		{
			RemoveQueuedIngredientAt(slotIndex);
			AcceptEvent();
			return;
		}

		if (@event is InputEventMouseButton leftMouseButton &&
			leftMouseButton.ButtonIndex == MouseButton.Left)
		{
			if (leftMouseButton.Pressed)
			{
				_draggingSlotIndex = slotIndex;
				_dragStartGlobalPosition = leftMouseButton.GlobalPosition;
				_slotDragThresholdReached = false;
			}
			else
			{
				ResetSlotDragState();
			}

			AcceptEvent();
		}
	}

	private void RemoveQueuedIngredientAt(int slotIndex)
	{
		if (slotIndex < 0 || slotIndex >= _queuedIngredients.Count)
			return;

		var removedIngredientId = _queuedIngredients[slotIndex];
		_queuedIngredients.RemoveAt(slotIndex);
		_gameState.AddItem(removedIngredientId, 1);
		_resultLabel.Text = "";
		RefreshIngredientIcons();
	}

	private bool IsPointInsideAnyIngredientSlot(Vector2 globalPosition)
	{
		return IsPointInsideSlot(_ingredientSlotOneContainer, globalPosition)
			|| IsPointInsideSlot(_ingredientSlotTwoContainer, globalPosition)
			|| IsPointInsideSlot(_ingredientSlotThreeContainer, globalPosition);
	}

	private static bool IsPointInsideSlot(Control slot, Vector2 globalPosition)
	{
		return new Rect2(slot.GlobalPosition, slot.Size).HasPoint(globalPosition);
	}

	private void ResetSlotDragState()
	{
		_draggingSlotIndex = -1;
		_dragStartGlobalPosition = Vector2.Zero;
		_slotDragThresholdReached = false;
	}

	private void RefreshIngredientIcons()
	{
		var slots = new[] { _ingredientSlotOne, _ingredientSlotTwo, _ingredientSlotThree };
		var labels = new[] { _ingredientSlotOneLabel, _ingredientSlotTwoLabel, _ingredientSlotThreeLabel };

		for (var i = 0; i < slots.Length; i++)
		{
			if (i >= _queuedIngredients.Count)
			{
				slots[i].Texture = null;
				labels[i].Text = string.Empty;
				continue;
			}

			var ingredientId = _queuedIngredients[i];
			if (!ItemCatalog.TryGetItem(ingredientId, out var item))
			{
				slots[i].Texture = null;
				labels[i].Text = string.Empty;
				continue;
			}

			slots[i].Texture = LoadIcon(item.IconPath);
			labels[i].Text = ItemName(ingredientId);
		}

		RefreshBrewPreview();
	}

	private void RefreshBrewPreview()
	{
		var ingredientCount = _queuedIngredients.Count;
		var totalIngredientPrice = CalculateIngredientTotalPrice(_queuedIngredients);
		_ingredientCountLabel.Text = $"{ingredientCount}/3";
		_ingredientCountLabel.AddThemeColorOverride("font_color", ingredientCount == 3
			? new Color(0.43f, 0.83f, 0.48f, 1f)
			: new Color(0.65f, 0.68f, 0.72f, 1f));
		_pricePreviewLabel.Text = $"Estimated Sell Price: \u00A3{totalIngredientPrice}";

		if (ingredientCount < 3)
		{
			SetIncompletePreviewState();
			return;
		}

		if (!TryBuildIngredientDefs(_queuedIngredients, out var ingredientDefs, out _))
		{
			SetIncompletePreviewState();
			return;
		}

		var combinationKey = BuildCombinationKey(_queuedIngredients);
		_potionNamePreviewLabel.Text = GetPreviewPotionName(combinationKey);
		_potionNamePreviewLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.96f, 0.98f, 1f));

		var previewResult = _brewingService.BrewPotion(
			ingredientDefs,
			null,
			_dataDb.Synergies.ToList());

		_traitPreviewLabel.Text = BuildStatListText(previewResult.Traits, 3);
		_riskPreviewLabel.Text = previewResult.Risks.Count == 0
			? "None detected"
			: BuildStatListText(previewResult.Risks, 2);
		SetRiskStatusPreview(previewResult.Risks.Count == 0);
	}

	private void SetIncompletePreviewState()
	{
		ClearPreviewPotionName();
		_potionNamePreviewLabel.Text = "Add 3 ingredients to preview";
		_potionNamePreviewLabel.AddThemeColorOverride("font_color", new Color(0.73f, 0.76f, 0.79f, 1f));
		_traitPreviewLabel.Text = "-\n-\n-";
		_riskPreviewLabel.Text = "-";
		_riskStatusIconLabel.Text = "v";
		_riskStatusLabel.Text = "Waiting for 3 ingredients";
		_riskStatusIconLabel.AddThemeColorOverride("font_color", new Color(0.65f, 0.68f, 0.72f, 1f));
		_riskStatusLabel.AddThemeColorOverride("font_color", new Color(0.65f, 0.68f, 0.72f, 1f));
	}

	private void SetRiskStatusPreview(bool hasNoRisks)
	{
		if (hasNoRisks)
		{
			_riskStatusIconLabel.Text = "v";
			_riskStatusLabel.Text = "No detected risks";
			_riskStatusIconLabel.AddThemeColorOverride("font_color", new Color(0.43f, 0.83f, 0.48f, 1f));
			_riskStatusLabel.AddThemeColorOverride("font_color", new Color(0.43f, 0.83f, 0.48f, 1f));
			return;
		}

		_riskStatusIconLabel.Text = "!";
		_riskStatusLabel.Text = "Risks detected";
		_riskStatusIconLabel.AddThemeColorOverride("font_color", new Color(0.94f, 0.38f, 0.33f, 1f));
		_riskStatusLabel.AddThemeColorOverride("font_color", new Color(0.94f, 0.38f, 0.33f, 1f));
	}

	private void ClearPreviewPotionName()
	{
		_previewPotionCombinationKey = string.Empty;
		_previewPotionName = string.Empty;
	}

	private string GetPreviewPotionName(string combinationKey)
	{
		if (string.IsNullOrWhiteSpace(combinationKey))
			return string.Empty;

		if (_previewPotionCombinationKey == combinationKey && !string.IsNullOrWhiteSpace(_previewPotionName))
			return _previewPotionName;

		if (_gameState.TryGetPotionForCombination(combinationKey, out var potionItemId))
		{
			_previewPotionCombinationKey = combinationKey;
			_previewPotionName = PotionDisplayName(potionItemId, DefaultItemName(potionItemId));
			return _previewPotionName;
		}

		_previewPotionCombinationKey = combinationKey;
		_previewPotionName = GeneratePotionName();
		return _previewPotionName;
	}

	private static string BuildStatListText(IReadOnlyDictionary<string, int> values, int maxCount)
	{
		if (values.Count == 0)
			return "None detected";

		var lines = values
			.OrderByDescending(x => x.Value)
			.ThenBy(x => x.Key)
			.Take(maxCount)
			.Select(x => $"{x.Key} +{x.Value}")
			.ToList();

		if (lines.Count == 0)
			return "None detected";

		return string.Join("\n", lines);
	}

	private static Texture2D? LoadIcon(string? iconPath)
	{
		if (string.IsNullOrWhiteSpace(iconPath))
			return null;

		return ResourceLoader.Load<Texture2D>(iconPath);
	}

	private static void SetInteractiveCursor(Control control)
	{
		control.MouseDefaultCursorShape = CursorShape.PointingHand;
	}

	private string ItemName(string itemId)
	{
		return PotionDisplayName(itemId, DefaultItemName(itemId));
	}

	private string PotionDisplayName(string itemId, string fallbackName)
	{
		if (IsPotion(itemId))
		{
			var customName = _gameState.GetPotionDisplayName(itemId);
			if (!string.IsNullOrWhiteSpace(customName))
				return customName;
		}

		return fallbackName;
	}

	private string DefaultItemName(string itemId)
	{
		return ItemCatalog.GetItemName(itemId);
	}

	private bool IsPotion(string itemId)
	{
		return ItemCatalog.IsPotion(itemId);
	}

	private static bool IsIngredient(ItemDef item)
	{
		return item.Tags.Any(tag => string.Equals(tag, "ingredient", System.StringComparison.OrdinalIgnoreCase));
	}

	private string GeneratePotionName()
	{
		var prefixes = new[]
		{
			"Moon", "Velvet", "Ashen", "Gilded", "Silent", "Waking", "Hollow", "Duskwind", "Ivory", "Sable",
			"Grave", "Blood", "Fever", "Widow", "Saint", "Witch", "Mournful", "Whispering", "Buried", "Forgotten",
			"Crimson", "Silver", "Pale", "Black", "Honeyed", "Thorn", "Lantern", "Raven", "Serpent", "Spider",
			"Cursed", "Hallowed", "Profane", "Restless", "Lucid", "Delirious", "Withered", "Blooming", "Frozen", "Burning",
			"Marrow", "Salt", "Iron", "Mercury", "Obsidian", "Amber", "Violet", "Opal", "Spectral", "Haunted"
		};

		var suffixes = new[]
		{
			"Draught", "Tonic", "Elixir", "Brew", "Vial", "Concoction", "Essence", "Infusion", "Philter",
			"Serum", "Mixture", "Distillate", "Extract", "Syrup", "Remedy", "Cordial", "Tincture", "Decoction",
			"Salve", "Balm", "Oil", "Poultice", "Powder", "Salt", "Ash", "Dust", "Resin", "Venom", "Ichor",
			"Phial", "Ampoule", "Flask", "Mist", "Vapour", "Smoke", "Fume", "Charm", "Hex", "Curse",
			"Blessing", "Rite", "Offering", "Relic", "Memory", "Dream", "Vision", "Whisper", "Lullaby",
			"Confession", "Mercy", "Fever", "Shiver", "Rot", "Binding", "Release", "Awakening"
		};

		for (var i = 0; i < 12; i++)
		{
			var prefix = prefixes[Random.Shared.Next(prefixes.Length)];
			var suffix = suffixes[Random.Shared.Next(suffixes.Length)];
			var candidate = $"{prefix} {suffix}";

			if (!_gameState.PotionDisplayNames.Values.Any(x => string.Equals(x, candidate, System.StringComparison.OrdinalIgnoreCase)))
				return candidate;
		}

		return $"{prefixes[Random.Shared.Next(prefixes.Length)]} {suffixes[Random.Shared.Next(suffixes.Length)]}";
	}

	private string BuildCombinationKey(IReadOnlyList<string> ingredientIds)
	{
		return string.Join("|", ingredientIds.OrderBy(x => x, System.StringComparer.OrdinalIgnoreCase));
	}

	private string BuildBrewResultText(string potionItemId, PotionResult brewResult)
	{
		var lines = new List<string>
		{
			$"Brewed: {PotionDisplayName(potionItemId, DefaultItemName(potionItemId))}"
		};

		if (brewResult.TriggeredSynergyDetails.Count == 0)
			return string.Join("\n", lines);

		foreach (var synergy in brewResult.TriggeredSynergyDetails)
		{
			lines.Add($"Synergy triggered: {synergy.Id}");

			var contributingTraits = synergy.ContributingTraits.Count == 0
				? "None"
				: string.Join(", ", synergy.ContributingTraits
					.OrderBy(x => x.Key)
					.Select(x => $"{x.Key} {x.Value}"));

			var contributingRisks = synergy.ContributingRisks.Count == 0
				? "None"
				: string.Join(", ", synergy.ContributingRisks
					.OrderBy(x => x.Key)
					.Select(x => $"{x.Key} {x.Value}"));

			lines.Add($"Traits: {contributingTraits}");
			lines.Add($"Risks: {contributingRisks}");

			if (!string.IsNullOrWhiteSpace(synergy.Description))
				lines.Add(synergy.Description);
		}

		return string.Join("\n", lines);
	}

	private int CalculateBrewCost(int totalIngredientPrice, PotionResult brewResult)
	{
		var qualityBonus = Math.Max(0, brewResult.IngredientQualityScore - 50) / 10;
		var rawCost = (int)MathF.Round((totalIngredientPrice * 0.30f) + qualityBonus);
		return Math.Max(5, rawCost);
	}

	private static int CalculateIngredientTotalPrice(IReadOnlyList<string> ingredientIds)
	{
		var totalPrice = 0;

		foreach (var itemId in ingredientIds)
		{
			if (!ItemCatalog.TryGetItem(itemId, out var item))
				continue;

			totalPrice += Math.Max(0, item.BasePrice);
		}

		return Math.Max(0, totalPrice);
	}

	private string ResolvePotionIconPath()
	{
		var iconPaths = GetPotionIconPaths();
		if (iconPaths.Count == 0)
			return DefaultPotionIconPath;

		return iconPaths[Random.Shared.Next(iconPaths.Count)];
	}

	private static List<string> GetPotionIconPaths()
	{
		var absoluteDirectoryPath = ProjectSettings.GlobalizePath(PotionIconsDirectoryPath);
		if (!Directory.Exists(absoluteDirectoryPath))
			return new List<string>();

		var files = Directory.GetFiles(absoluteDirectoryPath, "*.svg");
		var iconPaths = new List<string>(files.Length);

		foreach (var filePath in files)
		{
			var fileName = Path.GetFileName(filePath);
			if (string.IsNullOrWhiteSpace(fileName))
				continue;

			iconPaths.Add($"{PotionIconsDirectoryPath}/{fileName}");
		}

		return iconPaths;
	}

	private bool TryBuildIngredientDefs(
		IReadOnlyList<string> ingredientIds,
		out List<IngredientDef> ingredients,
		out string error)
	{
		ingredients = new List<IngredientDef>();

		foreach (var itemId in ingredientIds)
		{
			if (!ItemCatalog.TryGetItem(itemId, out var item))
			{
				error = $"Unknown ingredient: {itemId}";
				return false;
			}

			var ingredient = new IngredientDef
			{
				Id = item.Id,
				Name = item.Name,
				Quality = item.Quality,
				Traits = new Dictionary<string, int>(item.Traits),
				Risks = new Dictionary<string, int>(item.Risks),
				Tags = [.. item.Tags]
			};

			ingredients.Add(ingredient);
		}

		error = string.Empty;
		return true;
	}
}
