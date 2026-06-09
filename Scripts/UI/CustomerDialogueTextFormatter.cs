using System.Collections.Generic;
using System.Linq;
using OccultShop.Models;

namespace OccultShop.UI;

public static class CustomerDialogueTextFormatter
{
	public const string PlayerSpeakerName = "You";
	public const string CustomerSpeakerName = "Customer";
	public const string PlayerSpeakerColorHex = "#59D959";
	public const string CustomerSpeakerColorHex = "#F5D76E";
	public const string MatchedDesiredColorHex = "#59D959";
	public const string MatchedRiskColorHex = "#E64040";

	public static string FormatTraitListWithMatches(
		Dictionary<string, int> requiredValues,
		IReadOnlyDictionary<string, int>? producedValues,
		string matchedColorHex)
	{
		if (requiredValues is null || requiredValues.Count == 0)
			return "None";

		return string.Join(
			"\n",
			requiredValues
				.OrderByDescending(x => x.Value)
				.ThenBy(x => x.Key)
			.Select(pair => FormatTraitLine(pair.Key, pair.Value, producedValues, matchedColorHex)));
	}

	public static string BuildDesiredRequestText(
		CustomerRequestDef request,
		IReadOnlyDictionary<string, int>? producedTraits)
	{
		var lines = new List<string>();
		var desiredTraitText = FormatTraitListWithMatches(
			request.DesiredTraits,
			producedTraits,
			MatchedDesiredColorHex);
		if (!string.Equals(desiredTraitText, "None", System.StringComparison.Ordinal))
			lines.Add(desiredTraitText);

		var minTraitText = FormatMinTraitThresholdsWithMatches(
			request.RequiredMinTraits,
			producedTraits);
		if (!string.IsNullOrWhiteSpace(minTraitText))
			lines.Add(minTraitText);

		var ingredientAmountText = FormatIngredientAmountRequirements(request.RequiredIngredientAmounts);
		if (!string.IsNullOrWhiteSpace(ingredientAmountText))
			lines.Add(ingredientAmountText);

		return lines.Count == 0 ? "None" : string.Join("\n", lines);
	}

	public static string BuildBadRequestText(
		CustomerRequestDef request,
		IReadOnlyDictionary<string, int>? producedTraits,
		IReadOnlyDictionary<string, int>? producedRisks)
	{
		var lines = new List<string>();
		var badTraitText = FormatTraitListWithMatches(
			request.BadTraits,
			producedRisks,
			MatchedRiskColorHex);
		if (!string.Equals(badTraitText, "None", System.StringComparison.Ordinal))
			lines.Add(badTraitText);

		var maxTraitText = FormatMaxTraitThresholdsWithViolations(
			request.RequiredMaxTraits,
			producedTraits);
		if (!string.IsNullOrWhiteSpace(maxTraitText))
			lines.Add(maxTraitText);

		return lines.Count == 0 ? "None" : string.Join("\n", lines);
	}

	public static string FormatIngredientAmountRequirements(IReadOnlyList<IngredientPortionDef>? requiredIngredientAmounts)
	{
		if (requiredIngredientAmounts is null || requiredIngredientAmounts.Count == 0)
			return string.Empty;

		var lines = requiredIngredientAmounts
			.Where(x => x is not null && !string.IsNullOrWhiteSpace(x.IngredientId) && x.Grams > 0)
			.OrderBy(x => x.IngredientId)
			.Select(x => $"{EscapeBbCodeText(x.IngredientId)}: {x.Grams}g")
			.ToList();

		return lines.Count == 0 ? string.Empty : string.Join("\n", lines);
	}

	public static string FormatMinTraitThresholdsWithMatches(
		Dictionary<string, int> requiredMinTraits,
		IReadOnlyDictionary<string, int>? producedValues)
	{
		if (requiredMinTraits is null || requiredMinTraits.Count == 0)
			return string.Empty;

		return string.Join(
			"\n",
			requiredMinTraits
				.OrderByDescending(x => x.Value)
				.ThenBy(x => x.Key)
				.Select(pair => FormatTraitThresholdLine(
					pair.Key,
					">=",
					pair.Value,
					producedValues,
					producedValue => producedValue >= pair.Value,
					MatchedDesiredColorHex)));
	}

	public static string FormatMaxTraitThresholdsWithViolations(
		Dictionary<string, int> requiredMaxTraits,
		IReadOnlyDictionary<string, int>? producedValues)
	{
		if (requiredMaxTraits is null || requiredMaxTraits.Count == 0)
			return string.Empty;

		return string.Join(
			"\n",
			requiredMaxTraits
				.OrderBy(x => x.Key)
				.Select(pair => FormatTraitThresholdLine(
					pair.Key,
					"<=",
					pair.Value,
					producedValues,
					producedValue => producedValue > pair.Value,
					MatchedRiskColorHex)));
	}

	public static string GetVisibleDialogueText(string text, int visibleCharacters)
	{
		var characterCount = Math.Min(Math.Max(visibleCharacters, 0), text.Length);
		return characterCount >= text.Length ? text : text[..characterCount];
	}

	public static string FormatConversationLine(string speaker, string text)
	{
		var safeText = EscapeBbCodeText(text);
		return $"{FormatSpeakerName(speaker)}\n{safeText}";
	}

	public static string EscapeBbCodeText(string text)
	{
		return text
			.Replace("[", "[lb]")
			.Replace("]", "[rb]");
	}

	private static string FormatTraitLine(
		string key,
		int requiredValue,
		IReadOnlyDictionary<string, int>? producedValues,
		string matchedColorHex)
	{
		var safeKey = EscapeBbCodeText(key);
		var line = $"{safeKey}: {requiredValue}";
		if (producedValues is null)
			return line;

		if (!TryGetValueIgnoreCase(producedValues, key, out var producedValue))
			return line;

		if (producedValue <= 0)
			return line;

		return $"[color={matchedColorHex}]{line}[/color]";
	}

	private static string FormatTraitThresholdLine(
		string key,
		string comparison,
		int thresholdValue,
		IReadOnlyDictionary<string, int>? producedValues,
		System.Func<int, bool> shouldHighlight,
		string highlightColorHex)
	{
		var safeKey = EscapeBbCodeText(key);
		var line = $"{safeKey} {comparison} {thresholdValue}";
		if (producedValues is null)
			return line;

		TryGetValueIgnoreCase(producedValues, key, out var producedValue);
		if (!shouldHighlight(producedValue))
			return line;

		return $"[color={highlightColorHex}]{line}[/color]";
	}

	private static string FormatSpeakerName(string speaker)
	{
		var safeSpeaker = EscapeBbCodeText(speaker);
		var colorHex = GetSpeakerColorHex(speaker);
		if (string.IsNullOrWhiteSpace(colorHex))
			return $"[b]{safeSpeaker}[/b]";

		return $"[b][color={colorHex}]{safeSpeaker}[/color][/b]";
	}

	private static string GetSpeakerColorHex(string speaker)
	{
		if (string.Equals(speaker, PlayerSpeakerName, System.StringComparison.OrdinalIgnoreCase))
			return PlayerSpeakerColorHex;

		if (string.Equals(speaker, CustomerSpeakerName, System.StringComparison.OrdinalIgnoreCase))
			return CustomerSpeakerColorHex;

		return string.Empty;
	}

	private static bool TryGetValueIgnoreCase(
		IReadOnlyDictionary<string, int> values,
		string key,
		out int value)
	{
		foreach (var pair in values)
		{
			if (!string.Equals(pair.Key, key, System.StringComparison.OrdinalIgnoreCase))
				continue;

			value = pair.Value;
			return true;
		}

		value = 0;
		return false;
	}
}
