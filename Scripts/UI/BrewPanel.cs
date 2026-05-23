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

    [Export] public NodePath CloseButtonPath = default!;
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
    [Export] public NodePath ResultLabelPath = default!;
    [Export] public NodePath TraitPreviewLabelPath = default!;
    [Export] public NodePath RiskPreviewLabelPath = default!;
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
    private Label _resultLabel = default!;
    private Label _traitPreviewLabel = default!;
    private Label _riskPreviewLabel = default!;
    private Button _brewButton = default!;
    private Button _clearButton = default!;
    private readonly List<string> _queuedIngredients = new();
    private readonly PotionBrewingService _brewingService = new();
    private int _draggingSlotIndex = -1;
    private Vector2 _dragStartGlobalPosition = Vector2.Zero;
    private bool _slotDragThresholdReached;

    public override void _Ready()
    {
        _closeButton = GetNode<Button>(CloseButtonPath);
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
        _resultLabel = GetNode<Label>(ResultLabelPath);
        _traitPreviewLabel = GetNode<Label>(TraitPreviewLabelPath);
        _riskPreviewLabel = GetNode<Label>(RiskPreviewLabelPath);
        _brewButton = GetNode<Button>(BrewButtonPath);
        _clearButton = GetNode<Button>(ClearButtonPath);

        MouseFilter = MouseFilterEnum.Ignore;
        _closeButton.Pressed += HidePanel;
        _brewBox.ItemDropped += TryQueueIngredient;
        _brewButton.Pressed += TryBrew;
        _clearButton.Pressed += ClearQueue;
        _ingredientSlotOneContainer.GuiInput += @event => HandleIngredientSlotGuiInput(0, @event);
        _ingredientSlotTwoContainer.GuiInput += @event => HandleIngredientSlotGuiInput(1, @event);
        _ingredientSlotThreeContainer.GuiInput += @event => HandleIngredientSlotGuiInput(2, @event);
        Visible = false;
        RefreshIngredientIcons();
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

        if (!GameState.HasItem(itemId, 1))
        {
            _resultLabel.Text = "Not enough stock for that ingredient.";
            return;
        }

        if (!GameState.ConsumeItem(itemId, 1))
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
			DataDb.Synergies.ToList());

		var brewCost = CalculateBrewCost(_queuedIngredients, brewResult);
        if (GameState.Gold < brewCost)
        {
            _resultLabel.Text = $"Need {brewCost} gold to brew this potion.";
            return;
        }

		GameState.AddGold(-brewCost);

		var combinationKey = BuildCombinationKey(_queuedIngredients);
		var isNewCombination = !GameState.TryGetPotionForCombination(combinationKey, out var potionItemId);
		if (isNewCombination)
		{
			var randomName = GeneratePotionName();
			potionItemId = $"brew_{GameState.PotionDisplayNames.Count + 1}";
			var iconPath = ResolvePotionIconPath();
			var description = BuildPotionDescription(_queuedIngredients, brewResult);
			var basePrice = CalculatePotionBasePrice(brewCost, brewResult);

			RuntimeContentDb.RegisterRuntimePotionItem(
				potionItemId,
				randomName,
				description,
				iconPath,
				basePrice,
				brewResult.IngredientQualityScore,
				new Dictionary<string, int>(brewResult.Traits),
				new Dictionary<string, int>(brewResult.Risks));

			GameState.SetPotionForCombination(combinationKey, potionItemId);
			GameState.SetPotionDisplayName(potionItemId, randomName);
		}

		GameState.RecordPotionRecipe(potionItemId, _queuedIngredients);
		GameState.AddItem(potionItemId, BrewedPotionOutputQuantity);
		GameState.RecordPotionBatch(potionItemId, _queuedIngredients);
		_queuedIngredients.Clear();
        ResetSlotDragState();
		RefreshIngredientIcons();
		_resultLabel.Text = BuildBrewResultText(potionItemId, brewResult);
    }

    private void ReturnQueuedIngredients()
    {
        foreach (var itemId in _queuedIngredients)
            GameState.AddItem(itemId, 1);
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
        GameState.AddItem(removedIngredientId, 1);
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
        if (_queuedIngredients.Count == 0)
        {
            _traitPreviewLabel.Text = "Top Traits:\n-";
            _riskPreviewLabel.Text = "Top Risks:\n-";
            return;
        }

        if (!TryBuildIngredientDefs(_queuedIngredients, out var ingredientDefs, out _))
        {
            _traitPreviewLabel.Text = "Top Traits:\n-";
            _riskPreviewLabel.Text = "Top Risks:\n-";
            return;
        }

        var previewResult = _brewingService.BrewPotion(
            ingredientDefs,
            null,
            DataDb.Synergies.ToList());

        _traitPreviewLabel.Text = BuildTopListText("Top Traits", previewResult.Traits, 3);
        _riskPreviewLabel.Text = BuildTopListText("Top Risks", previewResult.Risks, 2);
    }

    private static string BuildTopListText(string title, IReadOnlyDictionary<string, int> values, int maxCount)
    {
        if (values.Count == 0)
            return $"{title}:\n-";

        var lines = values
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.Key)
            .Take(maxCount)
            .Select(x => $"{x.Key} {x.Value}")
            .ToList();

        if (lines.Count == 0)
            return $"{title}:\n-";

        return $"{title}:\n{string.Join("\n", lines)}";
    }

    private static Texture2D? LoadIcon(string? iconPath)
    {
        if (string.IsNullOrWhiteSpace(iconPath))
            return null;

        return ResourceLoader.Load<Texture2D>(iconPath);
    }

	private string FormatIngredientSummary(IEnumerable<string> ingredientIds)
	{
		var grouped = ingredientIds
			.GroupBy(x => x)
			.OrderBy(g => ItemName(g.Key))
			.Select(g => $"{ItemName(g.Key)} x{g.Count()}");

		return string.Join("\n", grouped);
	}

    private string ItemName(string itemId)
    {
        return PotionDisplayName(itemId, DefaultItemName(itemId));
    }

	private string PotionDisplayName(string itemId, string fallbackName)
	{
		if (IsPotion(itemId))
		{
			var customName = GameState.GetPotionDisplayName(itemId);
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

			if (!GameState.PotionDisplayNames.Values.Any(x => string.Equals(x, candidate, System.StringComparison.OrdinalIgnoreCase)))
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

			lines.Add($"Risks: {contributingRisks}");

			if (!string.IsNullOrWhiteSpace(synergy.Description))
				lines.Add(synergy.Description);
		}

		return string.Join("\n", lines);
	}

	private int CalculateBrewCost(IReadOnlyList<string> ingredientIds, PotionResult brewResult)
	{
		var totalBasePrice = 0;

		foreach (var itemId in ingredientIds)
		{
			if (!ItemCatalog.TryGetItem(itemId, out var item))
				continue;

			totalBasePrice += Math.Max(1, item.BasePrice);
		}

		var qualityBonus = Math.Max(0, brewResult.IngredientQualityScore - 50) / 10;
		var rawCost = (int)MathF.Round((totalBasePrice * 0.30f) + qualityBonus);
		return Math.Max(5, rawCost);
	}

	private static int CalculatePotionBasePrice(int brewCost, PotionResult brewResult)
	{
		var qualityBonus = Math.Max(0, brewResult.IngredientQualityScore - 50);
		return Math.Max(1, (brewCost * 2) + qualityBonus);
	}

	private string BuildPotionDescription(IReadOnlyList<string> ingredientIds, PotionResult brewResult)
	{
		return "A brewed potion discovered from:";
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
		List<string> ingredientIds,
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

    private RuntimeContentDb RuntimeContentDb => GetTree().Root.GetNode<RuntimeContentDb>("/root/RuntimeContentDb");
    private DataDb DataDb => GetTree().Root.GetNode<DataDb>("/root/DataDb");
    private GameState GameState => GetTree().Root.GetNode<GameState>("/root/GameState");
}
