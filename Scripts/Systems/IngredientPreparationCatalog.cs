using System;
using System.Collections.Generic;
using System.Linq;
using OccultShop.Models;

namespace OccultShop.Systems;

public readonly record struct IngredientPreparationOption(string Id, string DisplayName);

public static class IngredientPreparationCatalog
{
	public const string RawPreparationId = "raw";
	public const string SteepedPreparationId = "steeped";
	public const string CrushedPreparationId = "crushed";
	public const string BoiledPreparationId = "boiled";
	public const string PreparedItemIdMarker = "__prep_";

	private static readonly IngredientPreparationOption[] Options =
	{
		new(RawPreparationId, "Raw"),
		new(SteepedPreparationId, "Steeped"),
		new(CrushedPreparationId, "Crushed"),
		new(BoiledPreparationId, "Boiled")
	};

	public static IReadOnlyList<IngredientPreparationOption> AllOptions => Options;

	public static bool IsKnownPreparationId(string preparationId)
	{
		var normalized = NormalizePreparationId(preparationId);
		return Options.Any(option => string.Equals(option.Id, normalized, StringComparison.OrdinalIgnoreCase));
	}

	public static string GetDisplayName(string preparationId)
	{
		var normalized = NormalizePreparationId(preparationId);
		foreach (var option in Options)
		{
			if (string.Equals(option.Id, normalized, StringComparison.OrdinalIgnoreCase))
				return option.DisplayName;
		}

		return DisplayStatName(normalized);
	}

	public static string NormalizePreparationId(string preparationId)
	{
		return string.IsNullOrWhiteSpace(preparationId)
			? string.Empty
			: preparationId.Trim().ToLowerInvariant();
	}

	public static string BuildPreparedItemId(string baseIngredientId, string preparationId)
	{
		return $"{NormalizeVariantIdPart(baseIngredientId)}{PreparedItemIdMarker}{NormalizeVariantIdPart(preparationId)}";
	}

	public static bool IsPreparedIngredient(ItemDef? item)
	{
		return item?.PreparedIngredient is not null &&
			!string.IsNullOrWhiteSpace(item.PreparedIngredient.BaseIngredientId) &&
			!string.IsNullOrWhiteSpace(item.PreparedIngredient.PreparationId);
	}

	public static bool TryGetPreparedIngredientInfo(
		ItemDef? item,
		out string baseIngredientId,
		out string preparationId)
	{
		baseIngredientId = string.Empty;
		preparationId = string.Empty;
		if (!IsPreparedIngredient(item))
			return false;

		baseIngredientId = item!.PreparedIngredient!.BaseIngredientId.Trim();
		preparationId = NormalizePreparationId(item.PreparedIngredient.PreparationId);
		return !string.IsNullOrWhiteSpace(baseIngredientId) && !string.IsNullOrWhiteSpace(preparationId);
	}

	public static bool TryGetPreparation(
		ItemDef baseIngredient,
		string preparationId,
		out IngredientPreparationDef preparation)
	{
		preparation = default!;
		if (baseIngredient.Preparations is null || baseIngredient.Preparations.Count == 0)
			return false;

		var normalized = NormalizePreparationId(preparationId);
		foreach (var pair in baseIngredient.Preparations)
		{
			if (!string.Equals(pair.Key, normalized, StringComparison.OrdinalIgnoreCase))
				continue;

			preparation = pair.Value;
			return preparation is not null;
		}

		return false;
	}

	private static string NormalizeVariantIdPart(string value)
	{
		var trimmed = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
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

		return count == 0 ? "unknown" : new string(chars, 0, count);
	}

	private static string DisplayStatName(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return string.Empty;

		var normalized = value.Trim().Replace('_', ' ');
		return normalized.Length == 0
			? string.Empty
			: char.ToUpperInvariant(normalized[0]) + normalized[1..];
	}
}
