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

			if (!TryBuildRecipePortions(recipe, normalizedIngredientIds, reportError, out var recipePortions))
				continue;

			var combinationKey = BuildCombinationKey(recipePortions);
			if (_recipesByCombination.ContainsKey(combinationKey))
			{
				reportError?.Invoke($"Duplicate predefined recipe combination '{combinationKey}'.");
				continue;
			}

			_recipesByCombination[combinationKey] = new RecipeEntry(recipe, normalizedIngredientIds, recipePortions);
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

	public bool TryGetRecipe(IReadOnlyList<IngredientPortionDef> ingredientPortions, out PotionRecipeDef recipe)
	{
		if (TryGetRecipe(BuildCombinationKey(ingredientPortions), out recipe))
			return true;

		return TryGetRecipe(BuildBaseCombinationKey(ingredientPortions), out recipe);
	}

	public bool MatchesAnyRecipePrefix(IReadOnlyList<string> ingredientIds)
	{
		var ingredientPortions = ingredientIds
			.Select(id => new IngredientPortionDef
			{
				IngredientId = id,
				ItemId = id,
				Grams = 0
			})
			.ToList();

		return MatchesAnyRecipePrefix(ingredientPortions);
	}

	public bool MatchesAnyRecipePrefix(IReadOnlyList<IngredientPortionDef> ingredientPortions)
	{
		if (ingredientPortions.Count == 0)
			return false;

		foreach (var entry in _recipesByCombination.Values)
		{
			var matches = true;
			foreach (var ingredientPortion in ingredientPortions)
			{
				if (!entry.IngredientPortions.Any(x => IngredientPortionMatchesRequirement(ingredientPortion, x)))
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

	public static string BuildCombinationKey(IReadOnlyList<IngredientPortionDef> ingredientPortions)
	{
		return string.Join(
			"|",
			ingredientPortions
				.Where(x => x is not null && !string.IsNullOrWhiteSpace(x.IngredientId))
				.Select(BuildCombinationKeyPart)
				.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
	}

	private static string BuildBaseCombinationKey(IReadOnlyList<IngredientPortionDef> ingredientPortions)
	{
		return BuildCombinationKey(
			ingredientPortions
				.Where(x => x is not null && !string.IsNullOrWhiteSpace(x.IngredientId))
				.Select(x => x.IngredientId)
				.ToList());
	}

	private static string BuildCombinationKeyPart(IngredientPortionDef ingredientPortion)
	{
		var ingredientId = ingredientPortion.IngredientId.Trim();
		var preparationId = IngredientPreparationCatalog.NormalizePreparationId(ingredientPortion.PreparationId);
		var keyPart = string.IsNullOrWhiteSpace(preparationId)
			? ingredientId
			: $"{ingredientId}#{preparationId}";

		return ingredientPortion.Grams > 0 ? $"{keyPart}@{ingredientPortion.Grams}g" : keyPart;
	}

	private static bool IngredientPortionMatchesRequirement(
		IngredientPortionDef candidate,
		IngredientPortionDef requirement)
	{
		if (!string.Equals(candidate.IngredientId, requirement.IngredientId, StringComparison.OrdinalIgnoreCase))
			return false;

		if (!string.IsNullOrWhiteSpace(requirement.PreparationId) &&
			!string.Equals(
				IngredientPreparationCatalog.NormalizePreparationId(candidate.PreparationId),
				IngredientPreparationCatalog.NormalizePreparationId(requirement.PreparationId),
				StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		return requirement.Grams <= 0 || candidate.Grams == requirement.Grams;
	}

	private static bool TryBuildRecipePortions(
		PotionRecipeDef recipe,
		IReadOnlyList<string> normalizedIngredientIds,
		Action<string>? reportError,
		out IReadOnlyList<IngredientPortionDef> ingredientPortions)
	{
		ingredientPortions = Array.Empty<IngredientPortionDef>();
		if (recipe.IngredientAmounts is null || recipe.IngredientAmounts.Count == 0)
		{
			ingredientPortions = normalizedIngredientIds
				.Select(id => new IngredientPortionDef
				{
					IngredientId = id,
					ItemId = id,
					Grams = 0
				})
				.ToList();
			return true;
		}

		var portions = recipe.IngredientAmounts
			.Where(x => x is not null && !string.IsNullOrWhiteSpace(x.IngredientId) && (x.Grams > 0 || !string.IsNullOrWhiteSpace(x.PreparationId)))
			.Select(x => new IngredientPortionDef
			{
				IngredientId = x.IngredientId.Trim(),
				ItemId = x.ItemId,
				PreparationId = IngredientPreparationCatalog.NormalizePreparationId(x.PreparationId),
				Grams = x.Grams
			})
			.ToList();
		if (portions.Count != 3)
		{
			reportError?.Invoke($"Predefined recipe '{recipe.Id}' must define exactly 3 ingredient portion requirements.");
			return false;
		}

		var portionIngredientIds = portions
			.Select(x => x.IngredientId)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();
		if (portionIngredientIds.Count != 3)
		{
			reportError?.Invoke($"Predefined recipe '{recipe.Id}' includes duplicate ingredient amount ids.");
			return false;
		}

		foreach (var ingredientId in portionIngredientIds)
		{
			if (normalizedIngredientIds.Any(x => string.Equals(x, ingredientId, StringComparison.OrdinalIgnoreCase)))
				continue;

			reportError?.Invoke($"Predefined recipe '{recipe.Id}' ingredient amounts do not match ingredientIds.");
			return false;
		}

		ingredientPortions = portions;
		return true;
	}

	private sealed class RecipeEntry
	{
		public RecipeEntry(
			PotionRecipeDef recipe,
			IReadOnlyList<string> ingredientIds,
			IReadOnlyList<IngredientPortionDef> ingredientPortions)
		{
			Recipe = recipe;
			IngredientIds = ingredientIds;
			IngredientPortions = ingredientPortions;
		}

		public PotionRecipeDef Recipe { get; }
		public IReadOnlyList<string> IngredientIds { get; }
		public IReadOnlyList<IngredientPortionDef> IngredientPortions { get; }
	}
}
