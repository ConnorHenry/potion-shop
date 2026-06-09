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
			Risks = new Dictionary<string, int>(item.Risks),
			IngredientEffects = CloneIngredientEffects(item.IngredientEffects),
			Tags = new List<string>(item.Tags)
		};
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
