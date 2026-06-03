using System;
using System.Collections.Generic;
using System.Linq;
using OccultShop.Models;

namespace OccultShop.Systems;

public sealed class PotionRecipeLookup
{
	private readonly Dictionary<string, RecipeEntry> _recipesByCombination = new(StringComparer.OrdinalIgnoreCase);

	public void Rebuild(
		IEnumerable<PotionRecipeDef> recipes,
		Func<string, bool> isKnownIngredient,
		Action<string>? reportError = null)
	{
		_recipesByCombination.Clear();

		foreach (var recipe in recipes)
		{
			if (recipe is null || string.IsNullOrWhiteSpace(recipe.Id) || string.IsNullOrWhiteSpace(recipe.Name))
				continue;
			if (recipe.IngredientIds is null || recipe.IngredientIds.Count != 3)
			{
				reportError?.Invoke($"Predefined recipe '{recipe.Id}' must define exactly 3 ingredients.");
				continue;
			}

			var normalizedIngredientIds = recipe.IngredientIds
				.Where(id => !string.IsNullOrWhiteSpace(id))
				.Select(id => id.Trim())
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();
			if (normalizedIngredientIds.Count != 3)
			{
				reportError?.Invoke($"Predefined recipe '{recipe.Id}' includes duplicate or empty ingredient ids.");
				continue;
			}

			var hasUnknownIngredient = false;
			foreach (var ingredientId in normalizedIngredientIds)
			{
				if (!isKnownIngredient(ingredientId))
				{
					reportError?.Invoke($"Predefined recipe '{recipe.Id}' references unknown ingredient '{ingredientId}'.");
					hasUnknownIngredient = true;
					break;
				}
			}

			if (hasUnknownIngredient)
				continue;

			var combinationKey = BuildCombinationKey(normalizedIngredientIds);
			if (_recipesByCombination.ContainsKey(combinationKey))
			{
				reportError?.Invoke($"Duplicate predefined recipe combination '{combinationKey}'.");
				continue;
			}

			_recipesByCombination[combinationKey] = new RecipeEntry(recipe, normalizedIngredientIds);
		}
	}

	public bool TryGetRecipe(string combinationKey, out PotionRecipeDef recipe)
	{
		if (_recipesByCombination.TryGetValue(combinationKey, out var entry))
		{
			recipe = entry.Recipe;
			return true;
		}

		recipe = default!;
		return false;
	}

	public bool MatchesAnyRecipePrefix(IReadOnlyList<string> ingredientIds)
	{
		if (ingredientIds.Count == 0)
			return false;

		foreach (var entry in _recipesByCombination.Values)
		{
			var matches = true;
			foreach (var ingredientId in ingredientIds)
			{
				if (!entry.IngredientIds.Any(x => string.Equals(x, ingredientId, StringComparison.OrdinalIgnoreCase)))
				{
					matches = false;
					break;
				}
			}

			if (matches)
				return true;
		}

		return false;
	}

	public static string BuildCombinationKey(IReadOnlyList<string> ingredientIds)
	{
		return string.Join("|", ingredientIds.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
	}

	private sealed class RecipeEntry
	{
		public RecipeEntry(PotionRecipeDef recipe, IReadOnlyList<string> ingredientIds)
		{
			Recipe = recipe;
			IngredientIds = ingredientIds;
		}

		public PotionRecipeDef Recipe { get; }
		public IReadOnlyList<string> IngredientIds { get; }
	}
}
