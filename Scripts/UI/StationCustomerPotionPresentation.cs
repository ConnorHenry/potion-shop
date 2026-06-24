using System.Collections.Generic;
using OccultShop.Models;

namespace OccultShop.UI;

public static class StationCustomerPotionPresentation
{
	public const string EmptyPotionDropLabel = "Drop potion here";

	public static string BuildSelectedPotionLabel(string fallbackName, string? customName)
	{
		var displayName = string.IsNullOrWhiteSpace(customName) ? fallbackName : customName;
		if (string.IsNullOrWhiteSpace(displayName))
			displayName = "Unknown potion";

		return $"Selected: {displayName}";
	}

	public static string BuildRequestFitText(
		CustomerRequestDef request,
		PotionResult? brewResult,
		IReadOnlyList<IngredientPortionDef>? potionIngredients)
	{
		return CustomerDialogueTextFormatter.BuildCustomerPotionRequestComparisonText(
			request,
			brewResult?.Traits,
			brewResult?.Risks,
			potionIngredients);
	}

	public static string BuildHiddenRequestFitText()
	{
		return CustomerDialogueTextFormatter.HiddenRequestText;
	}
}
