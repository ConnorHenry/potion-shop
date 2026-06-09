using System.Collections.Generic;
using System.Linq;
using OccultShop.Models;

namespace OccultShop.UI;

public static class BrewPanelTextFormatter
{
	public static string BuildIngredientInstructionText(int ingredientCount)
	{
		var remainingIngredients = Math.Clamp(3 - ingredientCount, 0, 3);
		return remainingIngredients switch
		{
			3 => "Add 3 ingredients to the cauldron.",
			2 => "Add 2 more ingredients to the cauldron.",
			1 => "Add 1 more ingredient to the cauldron.",
			_ => "Ready to brew."
		};
	}

	public static string BuildStatListText(IReadOnlyDictionary<string, int> values, int maxCount)
	{
		if (values.Count == 0)
			return "None detected";

		var lines = values
			.OrderByDescending(x => x.Value)
			.ThenBy(x => x.Key)
			.Take(maxCount)
			.Select(x => $"{x.Key} +{x.Value}")
			.ToList();

		if (lines.Count == 0)
			return "None detected";

		return string.Join("\n", lines);
	}

	public static string BuildRiskChanceListText(IReadOnlyDictionary<string, int> values, int maxCount)
	{
		if (values.Count == 0)
			return "None detected";

		var lines = values
			.OrderByDescending(x => x.Value)
			.ThenBy(x => x.Key)
			.Take(maxCount)
			.Select(x => $"{x.Key} {GetRiskChancePercent(x.Value)}%")
			.ToList();

		if (lines.Count == 0)
			return "None detected";

		return string.Join("\n", lines);
	}

	public static int GetRiskChancePercent(int chanceValue)
	{
		return Math.Clamp(chanceValue, 0, 10) * 10;
	}

	public static string BuildPreviewEffectText(PotionResult previewResult)
	{
		var lines = new List<string>();

		foreach (var ingredientEffect in previewResult.TriggeredIngredientEffects.Take(2))
		{
			var ingredientName = string.IsNullOrWhiteSpace(ingredientEffect.IngredientName)
				? ingredientEffect.IngredientId
				: ingredientEffect.IngredientName;
			var effectName = string.IsNullOrWhiteSpace(ingredientEffect.EffectName)
				? "Ingredient effect"
				: ingredientEffect.EffectName;
			var resultText = string.IsNullOrWhiteSpace(ingredientEffect.ResultText)
				? ingredientEffect.Description
				: ingredientEffect.ResultText;

			lines.Add(
				$"{EscapeBbCodeText(ingredientName)}: {EscapeBbCodeText(effectName)} - {EscapeBbCodeText(resultText)}");
		}

		foreach (var synergy in previewResult.TriggeredSynergyDetails.Take(1))
			lines.Add($"Synergy preview: {EscapeBbCodeText(synergy.Id)}");

		return string.Join("\n", lines);
	}

	public static string BuildBrewResultText(string potionName, PotionResult brewResult)
	{
		var safePotionName = EscapeBbCodeText(potionName);
		var lines = new List<string>
		{
			$"Brewed: {safePotionName}"
		};

		foreach (var risk in brewResult.Risks
			.Where(x => !string.IsNullOrWhiteSpace(x.Key) && x.Value > 0)
			.OrderBy(x => x.Key))
		{
			lines.Add(
				$"[color=#E7C84E]{safePotionName}[/color] has been tainted with - [color=#E64040]{EscapeBbCodeText(risk.Key)}[/color]");
		}

		foreach (var ingredientEffect in brewResult.TriggeredIngredientEffects)
		{
			var ingredientName = string.IsNullOrWhiteSpace(ingredientEffect.IngredientName)
				? ingredientEffect.IngredientId
				: ingredientEffect.IngredientName;
			var effectName = string.IsNullOrWhiteSpace(ingredientEffect.EffectName)
				? "Ingredient effect"
				: ingredientEffect.EffectName;
			var resultText = string.IsNullOrWhiteSpace(ingredientEffect.ResultText)
				? ingredientEffect.Description
				: ingredientEffect.ResultText;

			lines.Add(
				$"{EscapeBbCodeText(ingredientName)}: {EscapeBbCodeText(effectName)} - {EscapeBbCodeText(resultText)}");
		}

		if (brewResult.TriggeredSynergyDetails.Count == 0)
			return string.Join("\n", lines);

		foreach (var synergy in brewResult.TriggeredSynergyDetails)
		{
			lines.Add($"Synergy triggered: {EscapeBbCodeText(synergy.Id)}");

			var contributingTraits = synergy.ContributingTraits.Count == 0
				? "None"
				: string.Join(", ", synergy.ContributingTraits
					.OrderBy(x => x.Key)
					.Select(x => $"{EscapeBbCodeText(x.Key)} {x.Value}"));

			var contributingRisks = synergy.ContributingRisks.Count == 0
				? "None"
				: string.Join(", ", synergy.ContributingRisks
					.OrderBy(x => x.Key)
					.Select(x => $"{EscapeBbCodeText(x.Key)} {x.Value}"));

			lines.Add($"Traits: {contributingTraits}");
			lines.Add($"Risks: {contributingRisks}");

			if (!string.IsNullOrWhiteSpace(synergy.Description))
				lines.Add(EscapeBbCodeText(synergy.Description));
		}

		return string.Join("\n", lines);
	}

	public static string EscapeBbCodeText(string text)
	{
		return text
			.Replace("[", "[lb]")
			.Replace("]", "[rb]");
	}
}
