using System.Collections.Generic;
using System.Linq;
using OccultShop.Models;

namespace OccultShop.Systems;

public static class CustomerSaleRules
{
	public static bool IsRequestSatisfiedByPotion(
		CustomerRequestDef request,
		PotionResult brewResult,
		bool ingredientAmountRequirementsMet)
	{
		return HasAllDesiredTraitsPresent(request, brewResult.Traits) &&
			AreRequiredTraitThresholdsSatisfied(request, brewResult.Traits) &&
			ingredientAmountRequirementsMet;
	}

	public static bool HasAllDesiredTraitsPresent(CustomerRequestDef request, IReadOnlyDictionary<string, int> producedTraits)
	{
		var totalDesiredTraitCount = request.DesiredTraits.Count(pair => !string.IsNullOrWhiteSpace(pair.Key));
		var matchedDesiredTraitCount = CountMatchedDesiredTraits(request, producedTraits);

		if (totalDesiredTraitCount == 0)
			return true;

		var requiredMatchCount = GetRequiredDesiredTraitMatchCount(totalDesiredTraitCount);
		return matchedDesiredTraitCount >= requiredMatchCount;
	}

	public static int CountMatchedDesiredTraits(
		CustomerRequestDef request,
		IReadOnlyDictionary<string, int> producedTraits)
	{
		var matchedDesiredTraitCount = 0;
		foreach (var desiredTrait in request.DesiredTraits)
		{
			if (string.IsNullOrWhiteSpace(desiredTrait.Key))
				continue;

			if (!TryGetValueIgnoreCase(producedTraits, desiredTrait.Key, out var producedValue))
				continue;

			if (producedValue <= 0)
				continue;

			matchedDesiredTraitCount += 1;
		}

		return matchedDesiredTraitCount;
	}

	public static int CountMatchedBadTraits(
		CustomerRequestDef request,
		IReadOnlyDictionary<string, int> producedRisks)
	{
		var matchedBadTraitCount = 0;
		foreach (var badTrait in request.BadTraits)
		{
			if (string.IsNullOrWhiteSpace(badTrait.Key))
				continue;

			if (!TryGetValueIgnoreCase(producedRisks, badTrait.Key, out var producedValue))
				continue;

			if (producedValue <= 0)
				continue;

			matchedBadTraitCount += 1;
		}

		return matchedBadTraitCount;
	}

	public static bool AreRequiredTraitThresholdsSatisfied(
		CustomerRequestDef request,
		IReadOnlyDictionary<string, int> producedTraits)
	{
		return AreRequiredMinTraitsSatisfied(request.RequiredMinTraits, producedTraits) &&
			AreRequiredMaxTraitsSatisfied(request.RequiredMaxTraits, producedTraits);
	}

	public static bool AreRequiredMinTraitsSatisfied(
		IReadOnlyDictionary<string, int> requiredMinTraits,
		IReadOnlyDictionary<string, int> producedTraits)
	{
		if (requiredMinTraits is null || requiredMinTraits.Count == 0)
			return true;

		foreach (var threshold in requiredMinTraits)
		{
			if (string.IsNullOrWhiteSpace(threshold.Key))
				continue;

			TryGetValueIgnoreCase(producedTraits, threshold.Key, out var producedValue);
			if (producedValue < threshold.Value)
				return false;
		}

		return true;
	}

	public static bool AreRequiredMaxTraitsSatisfied(
		IReadOnlyDictionary<string, int> requiredMaxTraits,
		IReadOnlyDictionary<string, int> producedTraits)
	{
		if (requiredMaxTraits is null || requiredMaxTraits.Count == 0)
			return true;

		foreach (var threshold in requiredMaxTraits)
		{
			if (string.IsNullOrWhiteSpace(threshold.Key))
				continue;

			TryGetValueIgnoreCase(producedTraits, threshold.Key, out var producedValue);
			if (producedValue > threshold.Value)
				return false;
		}

		return true;
	}

	public static bool PotionResponseMatches(
		CustomerPotionResponseDef response,
		string itemId,
		CustomerRequestDef request,
		PotionResult brewResult,
		bool isSuccess)
	{
		if (response.Success is bool requiredSuccess && requiredSuccess != isSuccess)
			return false;

		if (!string.IsNullOrWhiteSpace(response.PotionItemId) &&
			!string.Equals(response.PotionItemId, itemId, System.StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		if (!string.IsNullOrWhiteSpace(response.Grade) &&
			!string.Equals(response.Grade, brewResult.Grade, System.StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		if (response.MinFinalScore is int minFinalScore && brewResult.FinalScore < minFinalScore)
			return false;

		if (response.MaxFinalScore is int maxFinalScore && brewResult.FinalScore > maxFinalScore)
			return false;

		if (response.MinMatchedDesiredTraits is int minMatchedDesiredTraits &&
			CountMatchedDesiredTraits(request, brewResult.Traits) < minMatchedDesiredTraits)
		{
			return false;
		}

		if (response.MaxMatchedBadTraits is int maxMatchedBadTraits &&
			CountMatchedBadTraits(request, brewResult.Risks) > maxMatchedBadTraits)
		{
			return false;
		}

		return true;
	}

	public static int GetRequiredDesiredTraitMatchCount(int totalDesiredTraitCount)
	{
		if (totalDesiredTraitCount <= 0)
			return 0;

		if (totalDesiredTraitCount >= 3)
			return totalDesiredTraitCount - 1;

		return totalDesiredTraitCount;
	}

	public static bool TryGetValueIgnoreCase(
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
