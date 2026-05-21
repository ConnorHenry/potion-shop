using System.Collections.Generic;
using System.Linq;
using Godot;
using OccultShop.Autoload;

namespace OccultShop.Systems;

public sealed class PotionInventoryBrewService
{
	public bool TryGetRequiredIngredients(string potionItemId, out Dictionary<string, int> requiredIngredients, out string error)
	{
		requiredIngredients = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);

		if (string.IsNullOrWhiteSpace(potionItemId))
		{
			error = "Potion id is missing.";
			return false;
		}

		if (!GameState.TryGetPotionRecipe(potionItemId, out var ingredientIds) || ingredientIds.Count == 0)
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
			if (!GameState.HasItem(pair.Key, pair.Value))
				return false;
		}

		return true;
	}

	public bool TryBrewPotion(string potionItemId, out string error)
	{
		error = string.Empty;

		if (!TryGetRequiredIngredients(potionItemId, out var requiredIngredients, out error))
			return false;

		if (!HasRequiredIngredients(requiredIngredients))
		{
			error = "Missing required ingredients.";
			return false;
		}

		foreach (var pair in requiredIngredients)
		{
			if (!GameState.ConsumeItem(pair.Key, pair.Value))
			{
				error = $"Failed to consume {pair.Key} x{pair.Value}.";
				return false;
			}
		}

		GameState.AddItem(potionItemId, 1);
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
			var have = GameState.Inventory.GetValueOrDefault(pair.Key);
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
			var have = GameState.Inventory.GetValueOrDefault(pair.Key);
			if (have >= pair.Value)
				continue;

			missing.Add($"{ItemName(pair.Key)} {have}/{pair.Value}");
		}

		return missing.Count == 0
			? "Missing ingredients."
			: $"Missing: {string.Join(", ", missing)}";
	}

	private static string ItemName(string itemId)
	{
		return DataDb.Items.TryGetValue(itemId, out var item) ? item.Name : itemId;
	}

	private static DataDb DataDb => (DataDb)((SceneTree)Engine.GetMainLoop()).Root.GetNode("/root/DataDb");
	private static GameState GameState => (GameState)((SceneTree)Engine.GetMainLoop()).Root.GetNode("/root/GameState");
}
