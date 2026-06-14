using System.Collections.Generic;
using System.Linq;
using OccultShop.Autoload;

namespace OccultShop.Systems;

public sealed class PotionInventoryBrewService
{
	public const int MaxUniquePotionInventoryQuantity = GameState.MaxUniquePotionInventoryQuantity;
	public const int MaxPotionStackQuantity = GameState.MaxPotionStackQuantity;
	public const string PotionInventoryFullMessage = "Potion inventory is full. Sell a potion before brewing another.";

	private readonly GameState _gameState;
	private readonly ItemCatalogService _itemCatalog;

	public PotionInventoryBrewService(GameState gameState, ItemCatalogService itemCatalog)
	{
		_gameState = gameState;
		_itemCatalog = itemCatalog;
	}

	public bool TryGetRequiredIngredients(string potionItemId, out Dictionary<string, int> requiredIngredients, out string error)
	{
		requiredIngredients = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);

		if (string.IsNullOrWhiteSpace(potionItemId))
		{
			error = "Potion id is missing.";
			return false;
		}

		if (!_gameState.TryGetPotionRecipe(potionItemId, out var ingredientIds) || ingredientIds.Count == 0)
		{
			error = "Recipe not discovered yet.";
			return false;
		}

		foreach (var ingredientId in ingredientIds)
		{
			if (string.IsNullOrWhiteSpace(ingredientId))
				continue;

			requiredIngredients[ingredientId] = requiredIngredients.GetValueOrDefault(ingredientId) + 1;
		}

		if (requiredIngredients.Count == 0)
		{
			error = "Recipe is empty.";
			return false;
		}

		error = string.Empty;
		return true;
	}

	public bool HasRequiredIngredients(Dictionary<string, int> requiredIngredients)
	{
		foreach (var pair in requiredIngredients)
		{
			if (!_gameState.HasItem(pair.Key, pair.Value))
				return false;
		}

		return true;
	}

	public int CountOwnedUniquePotions()
	{
		var count = 0;
		foreach (var pair in _gameState.Inventory)
		{
			if (pair.Value <= 0)
				continue;
			if (!_itemCatalog.IsPotion(pair.Key))
				continue;

			count++;
		}

		return count;
	}

	public bool CanAddPotion(string potionItemId, int quantity = 1)
	{
		if (quantity <= 0)
			return true;
		if (string.IsNullOrWhiteSpace(potionItemId))
			return false;

		var currentQuantity = _gameState.Inventory.GetValueOrDefault(potionItemId);
		if (currentQuantity + quantity > MaxPotionStackQuantity)
			return false;

		return currentQuantity > 0 || CountOwnedUniquePotions() < MaxUniquePotionInventoryQuantity;
	}

	public bool TryBrewPotion(string potionItemId, out string error)
	{
		error = string.Empty;

		if (!TryGetRequiredIngredients(potionItemId, out var requiredIngredients, out error))
			return false;

		if (!CanAddPotion(potionItemId))
		{
			error = PotionInventoryFullMessage;
			return false;
		}

		if (!HasRequiredIngredients(requiredIngredients))
		{
			error = "Missing required ingredients.";
			return false;
		}

		foreach (var pair in requiredIngredients)
		{
			if (!_gameState.ConsumeItem(pair.Key, pair.Value))
			{
				error = $"Failed to consume {pair.Key} x{pair.Value}.";
				return false;
			}
		}

		_gameState.AddItem(potionItemId, 1);
		_gameState.RecordIngredientPreparationKnowledge(requiredIngredients.Keys);
		return true;
	}

	public string BuildPotionDescriptionText(string potionItemId, string baseDescription)
	{
		var lines = new List<string>();

		if (!string.IsNullOrWhiteSpace(baseDescription))
			lines.Add(baseDescription);

		var ingredientsText = BuildIngredientAvailabilityText(potionItemId, includeHeading: false);
		if (!string.IsNullOrWhiteSpace(ingredientsText))
			lines.Add(ingredientsText);

		return string.Join("\n", lines);
	}

	public string BuildIngredientAvailabilityText(string potionItemId, bool includeHeading = false)
	{
		if (!TryGetRequiredIngredients(potionItemId, out var requiredIngredients, out var error))
			return error;

		var lines = new List<string>();

		if (includeHeading)
			lines.Add("Ingredients:");

		foreach (var pair in requiredIngredients.OrderBy(x => ItemName(x.Key)).ThenBy(x => x.Key))
		{
			var have = _gameState.Inventory.GetValueOrDefault(pair.Key);
			var hasEnough = have >= pair.Value;
			var color = hasEnough ? "#B7F59C" : "#B9B9B9";
			lines.Add($"[color={color}]{ItemName(pair.Key)} x{pair.Value}[/color]");
		}

		return string.Join("\n", lines);
	}

	public string BuildMissingIngredientsText(Dictionary<string, int> requiredIngredients)
	{
		var missing = new List<string>();

		foreach (var pair in requiredIngredients.OrderBy(x => ItemName(x.Key)).ThenBy(x => x.Key))
		{
			var have = _gameState.Inventory.GetValueOrDefault(pair.Key);
			if (have >= pair.Value)
				continue;

			missing.Add($"{ItemName(pair.Key)} {have}/{pair.Value}");
		}

		return missing.Count == 0
			? "Missing ingredients."
			: $"Missing: {string.Join(", ", missing)}";
	}

	private string ItemName(string itemId)
	{
		return _itemCatalog.GetItemName(itemId);
	}
}
