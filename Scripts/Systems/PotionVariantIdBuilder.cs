using System.Collections.Generic;
using System.Linq;

namespace OccultShop.Systems;

public static class PotionVariantIdBuilder
{
	public const string RiskVariantSeparator = "__risk_";
	public const string CleanRiskSignature = "clean";

	public static string BuildPredefinedPotionItemId(string recipeId)
	{
		return $"potion_{recipeId}";
	}

	public static string BuildRiskVariantItemId(string basePotionItemId, IReadOnlyDictionary<string, int> carriedRisks)
	{
		var activeRiskKeys = carriedRisks
			.Where(x => !string.IsNullOrWhiteSpace(x.Key) && x.Value > 0)
			.Select(x => NormalizeVariantIdPart(x.Key))
			.Where(x => !string.IsNullOrWhiteSpace(x))
			.OrderBy(x => x, System.StringComparer.OrdinalIgnoreCase)
			.ToList();

		var riskSignature = activeRiskKeys.Count == 0
			? CleanRiskSignature
			: string.Join("_", activeRiskKeys);

		return $"{basePotionItemId}{RiskVariantSeparator}{riskSignature}";
	}

	public static bool RisksMatch(
		IReadOnlyDictionary<string, int>? existingRisks,
		IReadOnlyDictionary<string, int> carriedRisks)
	{
		var normalizedExisting = NormalizeCarriedRisks(existingRisks);
		var normalizedCarried = NormalizeCarriedRisks(carriedRisks);

		if (normalizedExisting.Count != normalizedCarried.Count)
			return false;

		foreach (var risk in normalizedExisting)
		{
			if (!normalizedCarried.Any(x => string.Equals(x, risk, System.StringComparison.OrdinalIgnoreCase)))
				return false;
		}

		return true;
	}

	private static List<string> NormalizeCarriedRisks(IReadOnlyDictionary<string, int>? risks)
	{
		var normalized = new List<string>();
		if (risks is null)
			return normalized;

		foreach (var risk in risks)
		{
			if (string.IsNullOrWhiteSpace(risk.Key) || risk.Value <= 0)
				continue;

			var normalizedKey = risk.Key.Trim();
			if (normalized.Any(x => string.Equals(x, normalizedKey, System.StringComparison.OrdinalIgnoreCase)))
				continue;

			normalized.Add(normalizedKey);
		}

		normalized.Sort(System.StringComparer.OrdinalIgnoreCase);
		return normalized;
	}

	private static string NormalizeVariantIdPart(string value)
	{
		var trimmed = value.Trim().ToLowerInvariant();
		var chars = new char[trimmed.Length];
		var count = 0;
		var previousWasSeparator = false;

		foreach (var character in trimmed)
		{
			if (char.IsLetterOrDigit(character))
			{
				chars[count] = character;
				count += 1;
				previousWasSeparator = false;
				continue;
			}

			if (previousWasSeparator || count == 0)
				continue;

			chars[count] = '_';
			count += 1;
			previousWasSeparator = true;
		}

		if (count > 0 && chars[count - 1] == '_')
			count -= 1;

		return count == 0 ? string.Empty : new string(chars, 0, count);
	}
}
