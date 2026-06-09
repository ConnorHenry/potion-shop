using System;
using OccultShop.Models;

namespace OccultShop.Systems;

public static class BrewPricing
{
	public static int CalculateBrewCost(int totalIngredientPrice, PotionResult brewResult)
	{
		var qualityBonus = Math.Max(0, brewResult.IngredientQualityScore - 50) / 10;
		var rawCost = (int)MathF.Round((totalIngredientPrice * 0.30f) + qualityBonus);
		return Math.Max(5, rawCost);
	}
}
