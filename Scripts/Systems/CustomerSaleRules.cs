using System.Collections.Generic;
using System.Linq;
using OccultShop.Models;

namespace OccultShop.Systems;

public static class CustomerSaleRules
{
	public static bool IsRequestSatisfiedByPotion(
		string potionItemId,
		CustomerRequestDef request,
		PotionResult brewResult,
		bool ingredientAmountRequirementsMet)
	{
		return IsRequiredPotionSatisfied(potionItemId, request.RequiredPotionItemId) &&
			IsRequestSatisfiedByPotion(request, brewResult, ingredientAmountRequirementsMet);
	}

	public static bool IsRequestSatisfiedByPotion(
		CustomerRequestDef request,
		PotionResult brewResult,
		bool ingredientAmountRequirementsMet)
	{
		return HasAllDesiredTraitsPresent(request, brewResult.Traits) &&
			AreBadTraitRangesSatisfied(request, brewResult.Traits, brewResult.Risks) &&
			AreRequiredTraitThresholdsSatisfied(request, brewResult.Traits) &&
			ingredientAmountRequirementsMet;
	}

	public static bool IsRequiredPotionSatisfied(string potionItemId, string requiredPotionItemId)
	{
		if (string.IsNullOrWhiteSpace(requiredPotionItemId))
			return true;

		return !string.IsNullOrWhiteSpace(potionItemId) &&
			string.Equals(potionItemId.Trim(), requiredPotionItemId.Trim(), System.StringComparison.OrdinalIgnoreCase);
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

			TryGetValueIgnoreCase(producedTraits, desiredTrait.Key, out var producedValue);

			if (!DoesDesiredTraitMatch(request.HideRequestDetails, producedValue, desiredTrait.Value))
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

			TryGetValueIgnoreCase(producedRisks, badTrait.Key, out var producedValue);

			if (IsValueWithinRange(producedValue, badTrait.Value))
				continue;

			matchedBadTraitCount += 1;
		}

		return matchedBadTraitCount;
	}

	public static int CountMatchedBadTraits(
		CustomerRequestDef request,
		IReadOnlyDictionary<string, int> producedTraits,
		IReadOnlyDictionary<string, int> producedRisks)
	{
		var matchedBadTraitCount = 0;
		foreach (var badTrait in request.BadTraits)
		{
			if (string.IsNullOrWhiteSpace(badTrait.Key))
				continue;

			var producedValue = GetCombinedProducedValue(badTrait.Key, producedTraits, producedRisks);
			if (IsValueWithinRange(producedValue, badTrait.Value))
				continue;

			matchedBadTraitCount += 1;
		}

		return matchedBadTraitCount;
	}

	public static bool AreBadTraitRangesSatisfied(
		CustomerRequestDef request,
		IReadOnlyDictionary<string, int> producedTraits,
		IReadOnlyDictionary<string, int> producedRisks)
	{
		if (request.BadTraits is null || request.BadTraits.Count == 0)
			return true;

		foreach (var badTrait in request.BadTraits)
		{
			if (string.IsNullOrWhiteSpace(badTrait.Key))
				continue;

			var producedValue = GetCombinedProducedValue(badTrait.Key, producedTraits, producedRisks);
			if (!IsValueWithinRange(producedValue, badTrait.Value))
				return false;
		}

		return true;
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
			CountMatchedBadTraits(request, brewResult.Traits, brewResult.Risks) > maxMatchedBadTraits)
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

	private static int GetCombinedProducedValue(
		string key,
		IReadOnlyDictionary<string, int> producedTraits,
		IReadOnlyDictionary<string, int> producedRisks)
	{
		TryGetValueIgnoreCase(producedTraits, key, out var producedTraitValue);
		TryGetValueIgnoreCase(producedRisks, key, out var producedRiskValue);
		return System.Math.Max(0, producedTraitValue) + System.Math.Max(0, producedRiskValue);
	}

	private static bool DoesDesiredTraitMatch(
		bool requestDetailsHidden,
		int producedValue,
		CustomerTraitRangeDef? range)
	{
		if (requestDetailsHidden)
			return producedValue > 0;

		return IsValueWithinRange(producedValue, range);
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
}
