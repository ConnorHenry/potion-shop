using System;
using System.Collections.Generic;
using OccultShop.Models;

namespace OccultShop.Systems;

public static class IngredientDefFactory
{
	public static IngredientDef FromItemDef(ItemDef item)
	{
		return new IngredientDef
		{
			Id = item.Id,
			Name = item.Name,
			Quality = item.Quality,
			BasePrice = item.BasePrice,
			Traits = new Dictionary<string, int>(item.Traits),
			Risks = CloneRisksForBrewing(item),
			IngredientEffects = CloneIngredientEffects(item.IngredientEffects),
			Tags = new List<string>(item.Tags)
		};
	}

	private static Dictionary<string, int> CloneRisksForBrewing(ItemDef item)
	{
		return IsSuccessfulBoiledIngredient(item)
			? new Dictionary<string, int>()
			: new Dictionary<string, int>(item.Risks);
	}

	private static bool IsSuccessfulBoiledIngredient(ItemDef item)
	{
		if (item.PreparedIngredient is null)
			return false;

		var preparationId = IngredientPreparationCatalog.NormalizePreparationId(item.PreparedIngredient.PreparationId);
		if (!string.Equals(preparationId, IngredientPreparationCatalog.BoiledPreparationId, StringComparison.OrdinalIgnoreCase))
			return false;

		return !HasTag(item, ItemTags.FailedBoiling);
	}

	private static bool HasTag(ItemDef item, string tag)
	{
		if (item.Tags is null)
			return false;

		foreach (var candidate in item.Tags)
		{
			if (string.Equals(candidate, tag, StringComparison.OrdinalIgnoreCase))
				return true;
		}

		return false;
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
