using System;
using System.Collections.Generic;
using System.Linq;
using OccultShop.Models;

namespace OccultShop.Systems;

public static class PreparedIngredientFactory
{
	public static bool TryBuildPreparedIngredient(
		ItemDef baseIngredient,
		string preparationId,
		out ItemDef preparedIngredient,
		out string error)
	{
		preparedIngredient = default!;
		error = string.Empty;

		if (baseIngredient is null || string.IsNullOrWhiteSpace(baseIngredient.Id))
		{
			error = "Ingredient is not recognized.";
			return false;
		}

		var normalizedPreparationId = IngredientPreparationCatalog.NormalizePreparationId(preparationId);
		if (!IngredientPreparationCatalog.IsKnownPreparationId(normalizedPreparationId))
		{
			error = "Preparation method is not recognized.";
			return false;
		}

		if (!IngredientPreparationCatalog.TryGetPreparation(baseIngredient, normalizedPreparationId, out var preparation))
		{
			error = $"{baseIngredient.Name} cannot be {IngredientPreparationCatalog.GetDisplayName(normalizedPreparationId).ToLowerInvariant()}.";
			return false;
		}

		var tags = CloneTags(baseIngredient.Tags);
		if (!tags.Any(tag => string.Equals(tag, ItemTags.PreparedIngredient, StringComparison.OrdinalIgnoreCase)))
			tags.Add(ItemTags.PreparedIngredient);

		var preparationName = IngredientPreparationCatalog.GetDisplayName(normalizedPreparationId);
		preparedIngredient = new ItemDef
		{
			Id = IngredientPreparationCatalog.BuildPreparedItemId(baseIngredient.Id, normalizedPreparationId),
			Name = $"{baseIngredient.Name} ({preparationName})",
			IconPath = baseIngredient.IconPath,
			Description = baseIngredient.Description,
			StartsKnownInIngredientBook = baseIngredient.StartsKnownInIngredientBook,
			Tags = tags,
			Quality = baseIngredient.Quality,
			Traits = CloneStatMap(preparation.Traits),
			Risks = CloneStatMap(preparation.Risks),
			IngredientEffects = CloneIngredientEffects(baseIngredient.IngredientEffects),
			BasePrice = baseIngredient.BasePrice,
			PreparedIngredient = new PreparedIngredientDef
			{
				BaseIngredientId = baseIngredient.Id,
				PreparationId = normalizedPreparationId
			}
		};

		return true;
	}

	private static List<string> CloneTags(List<string>? tags)
	{
		var clone = new List<string>();
		if (tags is null)
			return clone;

		foreach (var tag in tags)
		{
			if (string.IsNullOrWhiteSpace(tag))
				continue;
			if (clone.Any(existing => string.Equals(existing, tag, StringComparison.OrdinalIgnoreCase)))
				continue;

			clone.Add(tag);
		}

		return clone;
	}

	private static Dictionary<string, int> CloneStatMap(Dictionary<string, int>? values)
	{
		return values is null
			? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
			: new Dictionary<string, int>(values, StringComparer.OrdinalIgnoreCase);
	}

	private static List<IngredientEffectDef> CloneIngredientEffects(List<IngredientEffectDef>? effects)
	{
		var clones = new List<IngredientEffectDef>();
		if (effects is null)
			return clones;

		foreach (var effect in effects)
		{
			if (effect is null)
				continue;

			clones.Add(new IngredientEffectDef
			{
				Kind = effect.Kind,
				Family = effect.Family,
				Name = effect.Name,
				Description = effect.Description,
				Amount = effect.Amount,
				SecondaryAmount = effect.SecondaryAmount,
				TraitId = effect.TraitId,
				RiskId = effect.RiskId
			});
		}

		return clones;
	}
}
