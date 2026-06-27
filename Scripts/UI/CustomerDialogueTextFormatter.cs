using System.Collections.Generic;
using System.Linq;
using OccultShop.Models;
using OccultShop.Systems;

namespace OccultShop.UI;

public static class CustomerDialogueTextFormatter
{
	public const string HiddenRequestText = "?????";
	public const string PlayerSpeakerName = "You";
	public const string CustomerSpeakerName = "Customer";
	public const string PlayerSpeakerColorHex = "#59D959";
	public const string CustomerSpeakerColorHex = "#F5D76E";
	public const string MatchedDesiredColorHex = "#59D959";
	public const string MatchedRiskColorHex = "#E64040";
	public const string ChecklistPartialColorHex = "#E7C84E";
	public const string ChecklistMissingColorHex = "#A8A093";

	public static string FormatTraitListWithMatches(
		Dictionary<string, CustomerTraitRangeDef> requiredValues,
		IReadOnlyDictionary<string, int>? producedValues,
		string matchedColorHex,
		string missingColorHex)
	{
		if (requiredValues is null || requiredValues.Count == 0)
			return "None";

		return string.Join(
			"\n",
			requiredValues
				.OrderByDescending(x => GetTraitRangeSortValue(x.Value))
				.ThenBy(x => x.Key)
				.Select(pair => FormatDesiredTraitLine(pair.Key, pair.Value, producedValues, matchedColorHex, missingColorHex)));
	}

	public static string BuildDesiredRequestText(
		CustomerRequestDef request,
		IReadOnlyDictionary<string, int>? producedTraits)
	{
		if (request.HideRequestDetails)
			return HiddenRequestText;

		var lines = new List<string>();
		AddRequiredPotionRequestLine(lines, request);

		var desiredTraitText = FormatTraitListWithMatches(
			request.DesiredTraits,
			producedTraits,
			MatchedDesiredColorHex,
			MatchedRiskColorHex);
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

	public static string BuildBrewingRequestChecklistText(
		CustomerRequestDef? request,
		IReadOnlyDictionary<string, int>? producedTraits,
		IReadOnlyDictionary<string, int>? possibleRisks,
		IReadOnlyList<IngredientPortionDef>? queuedIngredients)
	{
		if (request is null)
			return "No active request.";
		if (request.HideRequestDetails)
			return HiddenRequestText;

		var lines = new List<string>();
		AddRequiredPotionChecklistLine(lines, request);
		AddDesiredTraitChecklistLines(lines, request.DesiredTraits, producedTraits);
		AddRequiredMinTraitChecklistLines(lines, request.RequiredMinTraits, producedTraits);
		AddBadTraitChecklistLines(lines, request.BadTraits, producedTraits, possibleRisks);
		AddRequiredMaxTraitChecklistLines(lines, request.RequiredMaxTraits, producedTraits);
		AddIngredientRequirementChecklistLines(lines, request.RequiredIngredientAmounts, queuedIngredients);

		return lines.Count == 0
			? "No specific brew requirements."
			: string.Join("\n", lines);
	}

	public static string BuildCustomerPotionRequestComparisonText(
		CustomerRequestDef request,
		IReadOnlyDictionary<string, int>? producedTraits,
		IReadOnlyDictionary<string, int>? producedRisks,
		IReadOnlyList<IngredientPortionDef>? potionIngredients,
		string potionItemId = "")
	{
		if (request is null)
			return "No active request.";
		if (request.HideRequestDetails)
			return HiddenRequestText;

		var lines = new List<string>();
		AddRequiredPotionComparisonLine(lines, request, potionItemId);
		AddDesiredTraitComparisonLines(lines, request.DesiredTraits, producedTraits);
		AddRequiredMinTraitComparisonLines(lines, request.RequiredMinTraits, producedTraits);
		AddBadTraitComparisonLines(lines, request.BadTraits, producedTraits, producedRisks);
		AddRequiredMaxTraitComparisonLines(lines, request.RequiredMaxTraits, producedTraits);
		AddIngredientRequirementComparisonLines(lines, request.RequiredIngredientAmounts, potionIngredients);

		return lines.Count == 0
			? "No specific request."
			: string.Join("\n", lines);
	}

	public static string BuildBadRequestText(
		CustomerRequestDef request,
		IReadOnlyDictionary<string, int>? producedTraits,
		IReadOnlyDictionary<string, int>? producedRisks)
	{
		if (request.HideRequestDetails)
			return HiddenRequestText;

		var lines = new List<string>();
		var badTraitText = FormatBadTraitListWithViolations(
			request.BadTraits,
			producedTraits,
			producedRisks,
			MatchedDesiredColorHex,
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
			.Where(x => x is not null &&
				!string.IsNullOrWhiteSpace(x.IngredientId) &&
				(x.Grams > 0 || !string.IsNullOrWhiteSpace(x.PreparationId)))
			.OrderBy(x => x.IngredientId)
			.Select(FormatIngredientPortionRequirement)
			.ToList();

		return lines.Count == 0 ? string.Empty : string.Join("\n", lines);
	}

	private static string FormatIngredientPortionRequirement(IngredientPortionDef portion)
	{
		var details = new List<string>();
		if (!string.IsNullOrWhiteSpace(portion.PreparationId))
			details.Add($"{IngredientPreparationCatalog.GetDisplayName(portion.PreparationId)} prep");
		if (portion.Grams > 0)
			details.Add($"{portion.Grams}g");

		return $"{EscapeBbCodeText(portion.IngredientId)}: {EscapeBbCodeText(string.Join(", ", details))}";
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
					MatchedDesiredColorHex,
					MatchedRiskColorHex,
					colorFailedValueOnly: false)));
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
					producedValue => producedValue <= pair.Value,
					MatchedDesiredColorHex,
					MatchedRiskColorHex,
					colorFailedValueOnly: false)));
	}

	public static string FormatBadTraitListWithViolations(
		Dictionary<string, CustomerTraitRangeDef> requiredValues,
		IReadOnlyDictionary<string, int>? producedTraits,
		IReadOnlyDictionary<string, int>? producedRisks,
		string safeColorHex,
		string violationColorHex)
	{
		if (requiredValues is null || requiredValues.Count == 0)
			return "None";

		return string.Join(
			"\n",
			requiredValues
				.OrderByDescending(x => GetTraitRangeSortValue(x.Value))
				.ThenBy(x => x.Key)
				.Select(pair => FormatBadTraitLine(pair.Key, pair.Value, producedTraits, producedRisks, safeColorHex, violationColorHex)));
	}

	public static string GetVisibleDialogueText(string text, int visibleCharacters)
	{
		var characterCount = Math.Min(Math.Max(visibleCharacters, 0), text.Length);
		return characterCount >= text.Length ? text : text[..characterCount];
	}

	public static string FormatConversationLine(string? speaker, string text)
	{
		if (string.IsNullOrWhiteSpace(speaker))
			return FormatNarrationLine(text);

		var safeText = EscapeBbCodeText(text);
		return $"{FormatSpeakerName(speaker)}\n{safeText}";
	}

	public static string FormatNarrationLine(string text)
	{
		return EscapeBbCodeText(text);
	}

	public static string EscapeBbCodeText(string text)
	{
		return text
			.Replace("[", "[lb]")
			.Replace("]", "[rb]");
	}

	private static string FormatDesiredTraitLine(
		string key,
		CustomerTraitRangeDef? requiredRange,
		IReadOnlyDictionary<string, int>? producedValues,
		string matchedColorHex,
		string missingColorHex)
	{
		var safeKey = EscapeBbCodeText(key);
		var requestText = $"{safeKey}: {FormatTraitRange(requiredRange)}";
		if (producedValues is null)
			return requestText;

		TryGetValueIgnoreCase(producedValues, key, out var producedValue);
		var line = $"{requestText} ({producedValue})";
		if (IsValueWithinRange(producedValue, requiredRange))
			return $"[color={matchedColorHex}]{line}[/color]";

		return $"[color={missingColorHex}]{line}[/color]";
	}

	private static string FormatBadTraitLine(
		string key,
		CustomerTraitRangeDef? requiredRange,
		IReadOnlyDictionary<string, int>? producedTraits,
		IReadOnlyDictionary<string, int>? producedRisks,
		string safeColorHex,
		string violationColorHex)
	{
		var safeKey = EscapeBbCodeText(key);
		var requestText = $"{safeKey}: {FormatTraitRange(requiredRange)}";
		if (producedTraits is null && producedRisks is null)
			return requestText;

		var producedValue = GetCombinedProducedValue(key, producedTraits, producedRisks);
		var line = $"{requestText} ({producedValue})";
		var colorHex = IsValueWithinRange(producedValue, requiredRange)
			? safeColorHex
			: violationColorHex;

		return $"[color={colorHex}]{line}[/color]";
	}

	private static string FormatTraitThresholdLine(
		string key,
		string comparison,
		int thresholdValue,
		IReadOnlyDictionary<string, int>? producedValues,
		System.Func<int, bool> isSatisfied,
		string satisfiedColorHex,
		string failedColorHex,
		bool colorFailedValueOnly)
	{
		var safeKey = EscapeBbCodeText(key);
		var requestText = $"{safeKey} {comparison} {thresholdValue}";
		if (producedValues is null)
			return requestText;

		TryGetValueIgnoreCase(producedValues, key, out var producedValue);
		var line = $"{requestText} ({producedValue})";
		if (isSatisfied(producedValue))
			return $"[color={satisfiedColorHex}]{line}[/color]";

		return colorFailedValueOnly
			? $"{requestText} ([color={failedColorHex}]{producedValue}[/color])"
			: $"[color={failedColorHex}]{line}[/color]";
	}

	private static void AddDesiredTraitChecklistLines(
		List<string> lines,
		Dictionary<string, CustomerTraitRangeDef> desiredTraits,
		IReadOnlyDictionary<string, int>? producedTraits)
	{
		if (desiredTraits is null || desiredTraits.Count == 0)
			return;

		foreach (var desired in desiredTraits
			.Where(x => !string.IsNullOrWhiteSpace(x.Key))
			.OrderByDescending(x => GetTraitRangeSortValue(x.Value))
			.ThenBy(x => x.Key))
		{
			var producedValue = GetProducedValue(producedTraits, desired.Key);
			var status = GetDesiredChecklistStatus(producedValue, desired.Value);
			var text = $"{desired.Key} {producedValue} / {FormatTraitRange(desired.Value)}";
			lines.Add(FormatChecklistLine(status, text));
		}
	}

	private static void AddRequiredPotionRequestLine(List<string> lines, CustomerRequestDef request)
	{
		if (string.IsNullOrWhiteSpace(request.RequiredPotionItemId))
			return;

		lines.Add($"Potion: {EscapeBbCodeText(GetRequiredPotionDisplayName(request))}");
	}

	private static void AddRequiredPotionChecklistLine(List<string> lines, CustomerRequestDef request)
	{
		if (string.IsNullOrWhiteSpace(request.RequiredPotionItemId))
			return;

		lines.Add(FormatChecklistLine(ChecklistStatus.Missing, $"Brew {GetRequiredPotionDisplayName(request)}"));
	}

	private static void AddRequiredPotionComparisonLine(
		List<string> lines,
		CustomerRequestDef request,
		string potionItemId)
	{
		if (string.IsNullOrWhiteSpace(request.RequiredPotionItemId))
			return;

		lines.Add(FormatBinaryComparisonLine(
			CustomerSaleRules.IsRequiredPotionSatisfied(potionItemId, request.RequiredPotionItemId),
			$"Required potion: {GetRequiredPotionDisplayName(request)}"));
	}

	private static string GetRequiredPotionDisplayName(CustomerRequestDef request)
	{
		return string.IsNullOrWhiteSpace(request.RequiredPotionDisplayName)
			? request.RequiredPotionItemId.Trim()
			: request.RequiredPotionDisplayName.Trim();
	}

	private static void AddRequiredMinTraitChecklistLines(
		List<string> lines,
		Dictionary<string, int> requiredMinTraits,
		IReadOnlyDictionary<string, int>? producedTraits)
	{
		if (requiredMinTraits is null || requiredMinTraits.Count == 0)
			return;

		foreach (var required in requiredMinTraits
			.Where(x => !string.IsNullOrWhiteSpace(x.Key))
			.OrderByDescending(x => x.Value)
			.ThenBy(x => x.Key))
		{
			var producedValue = GetProducedValue(producedTraits, required.Key);
			var status = producedValue >= required.Value
				? ChecklistStatus.Ok
				: producedValue > 0
					? ChecklistStatus.Partial
					: ChecklistStatus.Missing;
			var text = $"{required.Key} {producedValue} / >= {required.Value}";
			lines.Add(FormatChecklistLine(status, text));
		}
	}

	private static void AddBadTraitChecklistLines(
		List<string> lines,
		Dictionary<string, CustomerTraitRangeDef> badTraits,
		IReadOnlyDictionary<string, int>? producedTraits,
		IReadOnlyDictionary<string, int>? possibleRisks)
	{
		if (badTraits is null || badTraits.Count == 0)
			return;

		foreach (var badTrait in badTraits
			.Where(x => !string.IsNullOrWhiteSpace(x.Key))
			.OrderByDescending(x => GetTraitRangeSortValue(x.Value))
			.ThenBy(x => x.Key))
		{
			var producedValue = GetCombinedProducedValue(badTrait.Key, producedTraits, possibleRisks);
			var status = IsValueWithinRange(producedValue, badTrait.Value)
				? ChecklistStatus.Ok
				: ChecklistStatus.Conflict;
			var text = $"{badTrait.Key} {producedValue} / {FormatTraitRange(badTrait.Value)}";
			lines.Add(FormatChecklistLine(status, text));
		}
	}

	private static void AddRequiredMaxTraitChecklistLines(
		List<string> lines,
		Dictionary<string, int> requiredMaxTraits,
		IReadOnlyDictionary<string, int>? producedTraits)
	{
		if (requiredMaxTraits is null || requiredMaxTraits.Count == 0)
			return;

		foreach (var required in requiredMaxTraits
			.Where(x => !string.IsNullOrWhiteSpace(x.Key))
			.OrderBy(x => x.Key))
		{
			var producedValue = GetProducedValue(producedTraits, required.Key);
			var status = producedValue <= required.Value
				? ChecklistStatus.Ok
				: ChecklistStatus.Conflict;
			var text = $"{required.Key} {producedValue} / <= {required.Value}";
			lines.Add(FormatChecklistLine(status, text));
		}
	}

	private static void AddIngredientRequirementChecklistLines(
		List<string> lines,
		IReadOnlyList<IngredientPortionDef>? requiredIngredientAmounts,
		IReadOnlyList<IngredientPortionDef>? queuedIngredients)
	{
		if (requiredIngredientAmounts is null || requiredIngredientAmounts.Count == 0)
			return;

		foreach (var requiredIngredient in requiredIngredientAmounts
			.Where(IsSpecificIngredientRequirement)
			.OrderBy(x => x.IngredientId))
		{
			var status = IsIngredientRequirementMet(requiredIngredient, queuedIngredients)
				? ChecklistStatus.Ok
				: ChecklistStatus.Missing;
			lines.Add(FormatChecklistLine(status, FormatIngredientChecklistRequirement(requiredIngredient)));
		}
	}

	private static void AddDesiredTraitComparisonLines(
		List<string> lines,
		Dictionary<string, CustomerTraitRangeDef> desiredTraits,
		IReadOnlyDictionary<string, int>? producedTraits)
	{
		if (desiredTraits is null || desiredTraits.Count == 0)
			return;

		foreach (var desired in desiredTraits
			.Where(x => !string.IsNullOrWhiteSpace(x.Key))
			.OrderByDescending(x => GetTraitRangeSortValue(x.Value))
			.ThenBy(x => x.Key))
		{
			var producedValue = GetProducedValue(producedTraits, desired.Key);
			var text = $"{desired.Key} {producedValue} / {FormatTraitRange(desired.Value)}";
			lines.Add(FormatBinaryComparisonLine(IsValueWithinRange(producedValue, desired.Value), text));
		}
	}

	private static void AddRequiredMinTraitComparisonLines(
		List<string> lines,
		Dictionary<string, int> requiredMinTraits,
		IReadOnlyDictionary<string, int>? producedTraits)
	{
		if (requiredMinTraits is null || requiredMinTraits.Count == 0)
			return;

		foreach (var required in requiredMinTraits
			.Where(x => !string.IsNullOrWhiteSpace(x.Key))
			.OrderByDescending(x => x.Value)
			.ThenBy(x => x.Key))
		{
			var producedValue = GetProducedValue(producedTraits, required.Key);
			var text = $"{required.Key} {producedValue} / >= {required.Value}";
			lines.Add(FormatBinaryComparisonLine(producedValue >= required.Value, text));
		}
	}

	private static void AddBadTraitComparisonLines(
		List<string> lines,
		Dictionary<string, CustomerTraitRangeDef> badTraits,
		IReadOnlyDictionary<string, int>? producedTraits,
		IReadOnlyDictionary<string, int>? producedRisks)
	{
		if (badTraits is null || badTraits.Count == 0)
			return;

		foreach (var badTrait in badTraits
			.Where(x => !string.IsNullOrWhiteSpace(x.Key))
			.OrderByDescending(x => GetTraitRangeSortValue(x.Value))
			.ThenBy(x => x.Key))
		{
			var producedValue = GetCombinedProducedValue(badTrait.Key, producedTraits, producedRisks);
			var text = $"{badTrait.Key} {producedValue} / {FormatTraitRange(badTrait.Value)}";
			lines.Add(FormatBinaryComparisonLine(IsValueWithinRange(producedValue, badTrait.Value), text));
		}
	}

	private static void AddRequiredMaxTraitComparisonLines(
		List<string> lines,
		Dictionary<string, int> requiredMaxTraits,
		IReadOnlyDictionary<string, int>? producedTraits)
	{
		if (requiredMaxTraits is null || requiredMaxTraits.Count == 0)
			return;

		foreach (var required in requiredMaxTraits
			.Where(x => !string.IsNullOrWhiteSpace(x.Key))
			.OrderBy(x => x.Key))
		{
			var producedValue = GetProducedValue(producedTraits, required.Key);
			var text = $"{required.Key} {producedValue} / <= {required.Value}";
			lines.Add(FormatBinaryComparisonLine(producedValue <= required.Value, text));
		}
	}

	private static void AddIngredientRequirementComparisonLines(
		List<string> lines,
		IReadOnlyList<IngredientPortionDef>? requiredIngredientAmounts,
		IReadOnlyList<IngredientPortionDef>? potionIngredients)
	{
		if (requiredIngredientAmounts is null || requiredIngredientAmounts.Count == 0)
			return;

		foreach (var requiredIngredient in requiredIngredientAmounts
			.Where(IsSpecificIngredientRequirement)
			.OrderBy(x => x.IngredientId))
		{
			lines.Add(FormatBinaryComparisonLine(
				IsIngredientRequirementMet(requiredIngredient, potionIngredients),
				FormatIngredientChecklistRequirement(requiredIngredient)));
		}
	}

	private static ChecklistStatus GetDesiredChecklistStatus(int producedValue, CustomerTraitRangeDef? requiredRange)
	{
		if (IsValueWithinRange(producedValue, requiredRange))
			return ChecklistStatus.Ok;

		if (producedValue <= 0)
			return ChecklistStatus.Missing;

		if (requiredRange?.Max is int max && producedValue > max)
			return ChecklistStatus.Conflict;

		return ChecklistStatus.Partial;
	}

	private static int GetProducedValue(IReadOnlyDictionary<string, int>? producedValues, string key)
	{
		if (producedValues is not null && TryGetValueIgnoreCase(producedValues, key, out var producedValue))
			return producedValue;

		return 0;
	}

	private static bool IsSpecificIngredientRequirement(IngredientPortionDef? requiredIngredient)
	{
		if (requiredIngredient is null || string.IsNullOrWhiteSpace(requiredIngredient.IngredientId))
			return false;

		return requiredIngredient.Grams > 0 || !string.IsNullOrWhiteSpace(requiredIngredient.PreparationId);
	}

	private static bool IsIngredientRequirementMet(
		IngredientPortionDef requiredIngredient,
		IReadOnlyList<IngredientPortionDef>? queuedIngredients)
	{
		if (queuedIngredients is null || queuedIngredients.Count == 0)
			return false;

		foreach (var queuedIngredient in queuedIngredients)
		{
			if (queuedIngredient is null)
				continue;

			if (!string.Equals(
				queuedIngredient.IngredientId,
				requiredIngredient.IngredientId,
				System.StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			if (requiredIngredient.Grams > 0 && queuedIngredient.Grams != requiredIngredient.Grams)
				continue;

			if (!string.IsNullOrWhiteSpace(requiredIngredient.PreparationId) &&
				!string.Equals(
					IngredientPreparationCatalog.NormalizePreparationId(queuedIngredient.PreparationId),
					IngredientPreparationCatalog.NormalizePreparationId(requiredIngredient.PreparationId),
					System.StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			return true;
		}

		return false;
	}

	private static string FormatIngredientChecklistRequirement(IngredientPortionDef requiredIngredient)
	{
		var details = new List<string>();
		if (!string.IsNullOrWhiteSpace(requiredIngredient.PreparationId))
			details.Add($"{IngredientPreparationCatalog.GetDisplayName(requiredIngredient.PreparationId)} prep");
		if (requiredIngredient.Grams > 0)
			details.Add($"{requiredIngredient.Grams}g");

		var suffix = details.Count == 0 ? "required" : string.Join(", ", details);
		return $"{requiredIngredient.IngredientId}: {suffix}";
	}

	private static string FormatChecklistLine(ChecklistStatus status, string text)
	{
		var (label, colorHex) = status switch
		{
			ChecklistStatus.Ok => ("OK", MatchedDesiredColorHex),
			ChecklistStatus.Partial => ("~", ChecklistPartialColorHex),
			ChecklistStatus.Conflict => ("!", MatchedRiskColorHex),
			_ => ("-", ChecklistMissingColorHex)
		};

		return $"[color={colorHex}]{label}[/color] {EscapeBbCodeText(text)}";
	}

	private static string FormatBinaryComparisonLine(bool matches, string text)
	{
		var label = matches ? "Match" : "No match";
		var colorHex = matches ? MatchedDesiredColorHex : MatchedRiskColorHex;
		return $"[color={colorHex}]{label}[/color] {EscapeBbCodeText(text)}";
	}

	public static string FormatSpeakerName(string speaker)
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

	private static int GetCombinedProducedValue(
		string key,
		IReadOnlyDictionary<string, int>? producedTraits,
		IReadOnlyDictionary<string, int>? producedRisks)
	{
		var producedValue = 0;
		if (producedTraits is not null && TryGetValueIgnoreCase(producedTraits, key, out var producedTraitValue))
			producedValue += System.Math.Max(0, producedTraitValue);

		if (producedRisks is not null && TryGetValueIgnoreCase(producedRisks, key, out var producedRiskValue))
			producedValue += System.Math.Max(0, producedRiskValue);

		return producedValue;
	}

	private static string FormatTraitRange(CustomerTraitRangeDef? range)
	{
		if (range is null)
			return "any";

		if (range.Min is int min && range.Max is int max)
			return min == max ? min.ToString() : $"{min}-{max}";

		if (range.Min is int minOnly)
			return $">= {minOnly}";

		if (range.Max is int maxOnly)
			return $"<= {maxOnly}";

		return "any";
	}

	private static int GetTraitRangeSortValue(CustomerTraitRangeDef? range)
	{
		if (range is null)
			return 0;

		if (range.Min is int min)
			return min;

		if (range.Max is int max)
			return max;

		return 0;
	}

	private static bool IsValueWithinRange(int producedValue, CustomerTraitRangeDef? range)
	{
		if (range is null)
			return true;

		if (range.Min is int min && producedValue < min)
			return false;

		if (range.Max is int max && producedValue > max)
			return false;

		return true;
	}

	private enum ChecklistStatus
	{
		Ok,
		Partial,
		Conflict,
		Missing
	}
}
