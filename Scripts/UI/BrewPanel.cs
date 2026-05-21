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
    [Export] public NodePath IngredientsLabelPath = default!;
    [Export] public NodePath ResultLabelPath = default!;
    [Export] public NodePath BrewButtonPath = default!;
    [Export] public NodePath ClearButtonPath = default!;

    private Button _closeButton = default!;
    private BrewDropBox _brewBox = default!;
    private Label _ingredientsLabel = default!;
    private Label _resultLabel = default!;
    private Button _brewButton = default!;
    private Button _clearButton = default!;
    private readonly List<string> _queuedIngredients = new();
    private readonly PotionBrewingService _brewingService = new();

    public override void _Ready()
    {
        _closeButton = GetNode<Button>(CloseButtonPath);
        _brewBox = GetNode<BrewDropBox>(BrewBoxPath);
        _ingredientsLabel = GetNode<Label>(IngredientsLabelPath);
        _resultLabel = GetNode<Label>(ResultLabelPath);
        _brewButton = GetNode<Button>(BrewButtonPath);
        _clearButton = GetNode<Button>(ClearButtonPath);

        MouseFilter = MouseFilterEnum.Ignore;
        _closeButton.Pressed += HidePanel;
        _brewBox.ItemDropped += QueueIngredient;
        _brewButton.Pressed += TryBrew;
        _clearButton.Pressed += ClearQueue;
        Visible = false;
        RefreshIngredientsLabel();
    }

    public void Toggle()
    {
        Visible = !Visible;
    }

    public void HidePanel()
    {
        ReturnQueuedIngredients();
        Visible = false;
        _resultLabel.Text = "";
        RefreshIngredientsLabel();
    }

    private void QueueIngredient(string itemId)
    {
        if (!DataDb.TryGetItem(itemId, out var item))
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
        RefreshIngredientsLabel();
    }

    private void ClearQueue()
    {
        ReturnQueuedIngredients();
        _queuedIngredients.Clear();
        _resultLabel.Text = "";
        RefreshIngredientsLabel();
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

			DataDb.RegisterRuntimePotionItem(
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
		RefreshIngredientsLabel();
		_resultLabel.Text = BuildBrewResultText(potionItemId, brewResult);
    }

    private void ReturnQueuedIngredients()
    {
        foreach (var itemId in _queuedIngredients)
            GameState.AddItem(itemId, 1);
    }

    private void RefreshIngredientsLabel()
    {
        if (_queuedIngredients.Count == 0)
        {
            _ingredientsLabel.Text = "Ingredients: (none)";
            return;
        }

        var grouped = _queuedIngredients
            .GroupBy(x => x)
            .OrderBy(g => ItemName(g.Key))
            .Select(g => $"{ItemName(g.Key)} x{g.Count()}");

        _ingredientsLabel.Text = $"Ingredients: {string.Join(", ", grouped)}";
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
		return DataDb.TryGetItem(itemId, out var item) ? item.Name : itemId;
	}

	private bool IsPotion(string itemId)
	{
		if (!DataDb.TryGetItem(itemId, out var item))
			return false;

		return item.Tags.Any(tag => string.Equals(tag, "potion", System.StringComparison.OrdinalIgnoreCase));
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
			if (!DataDb.TryGetItem(itemId, out var item))
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
			if (!DataDb.Items.TryGetValue(itemId, out var item))
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

    private DataDb DataDb => GetTree().Root.GetNode<DataDb>("/root/DataDb");
    private GameState GameState => GetTree().Root.GetNode<GameState>("/root/GameState");
}
