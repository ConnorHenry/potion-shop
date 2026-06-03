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
			Tags = new List<string>(item.Tags)
		};
	}
}
