using System.Collections.Generic;
using System.Linq;
using OccultShop.Models;
using OccultShop.Systems;

namespace OccultShop.UI;

public sealed class InventoryItemTagDisplayRule
{
	public string Tag { get; init; } = string.Empty;
	public string DisplayName { get; init; } = string.Empty;
	public bool VisibleToPlayer { get; init; }
}

public static class InventoryItemTextFormatter
{
	public static readonly InventoryItemTagDisplayRule[] ItemTagDisplayRules =
	{
		new() { Tag = ItemTags.Herb, DisplayName = "Herb", VisibleToPlayer = true },
		new() { Tag = ItemTags.Liquid, DisplayName = "Liquid", VisibleToPlayer = true },
		new() { Tag = ItemTags.Catalyst, DisplayName = "Catalyst", VisibleToPlayer = true },
		new() { Tag = ItemTags.Consumable, DisplayName = "Consumable", VisibleToPlayer = true },
		new() { Tag = ItemTags.Treated, DisplayName = "Treated", VisibleToPlayer = true },
		new() { Tag = ItemTags.Ingredient, DisplayName = "Ingredient", VisibleToPlayer = false }
	};

	public static void SplitInventoryName(string itemName, out string firstLine, out string secondLine)
	{
		if (string.IsNullOrWhiteSpace(itemName))
		{
			firstLine = itemName;
			secondLine = string.Empty;
			return;
		}

		var firstSpaceIndex = itemName.IndexOf(' ');
		if (firstSpaceIndex <= 0 || firstSpaceIndex >= itemName.Length - 1)
		{
			firstLine = itemName;
			secondLine = string.Empty;
			return;
		}

		firstLine = itemName[..firstSpaceIndex];
		secondLine = itemName[(firstSpaceIndex + 1)..];
	}

	public static string FormatItemDetailName(string itemName)
	{
		if (string.IsNullOrWhiteSpace(itemName))
			return itemName;

		var trimmedName = itemName.Trim();
		var firstSpaceIndex = trimmedName.IndexOf(' ');
		if (firstSpaceIndex <= 0 || firstSpaceIndex >= trimmedName.Length - 1)
			return trimmedName;

		if (trimmedName.IndexOf(' ', firstSpaceIndex + 1) >= 0)
			return trimmedName;

		return $"{trimmedName[..firstSpaceIndex]}\n{trimmedName[(firstSpaceIndex + 1)..]}";
	}

	public static string BuildSlotTraitText(ItemDef? item)
	{
		if (item?.Traits is null)
			return string.Empty;

		var topTrait = item.Traits
			.Where(x => !string.IsNullOrWhiteSpace(x.Key) && x.Value > 0)
			.OrderByDescending(x => x.Value)
			.ThenBy(x => x.Key)
			.FirstOrDefault();

		if (string.IsNullOrWhiteSpace(topTrait.Key) || topTrait.Value <= 0)
			return string.Empty;

		return $"{DisplayStatName(topTrait.Key)} +{topTrait.Value}";
	}

	public static string BuildConsumableEffectText(ItemDef item)
	{
		if (item.ConsumableEffect is null)
			return "Unknown";

		if (!string.IsNullOrWhiteSpace(item.ConsumableEffect.Description))
			return item.ConsumableEffect.Description;

		if (string.Equals(item.ConsumableEffect.Kind, ConsumableEffectDef.RemoveRiskKind, System.StringComparison.OrdinalIgnoreCase))
		{
			return string.IsNullOrWhiteSpace(item.ConsumableEffect.RiskId)
				? "Removes one risk from the selected item."
				: $"Removes {DisplayStatName(item.ConsumableEffect.RiskId)} from the selected item.";
		}

		return DisplayStatName(item.ConsumableEffect.Kind);
	}

	public static string BuildConsumableGateText(ItemDef item)
	{
		var allowedTargetTags = item.ConsumableGate?.AllowedTargetTags;
		if (allowedTargetTags is null || allowedTargetTags.Count == 0)
			return "Ingredients\nPotions\n";

		var lines = allowedTargetTags
			.Where(tag => !string.IsNullOrWhiteSpace(tag))
			.Select(DisplayStatName)
			.OrderBy(tag => tag)
			.Take(3)
			.ToList();

		if (lines.Count == 0)
			lines.Add("Ingredients");

		while (lines.Count < 3)
			lines.Add(string.Empty);

		return string.Join("\n", lines);
	}

	public static string BuildDescriptionWithIngredientEffects(ItemDef item)
	{
		var lines = new List<string>();
		if (!string.IsNullOrWhiteSpace(item.Description))
			lines.Add(item.Description);

		var preparationText = BuildPreparedIngredientText(item);
		if (!string.IsNullOrWhiteSpace(preparationText))
			lines.Add(preparationText);

		var effectText = BuildIngredientEffectsText(item);
		if (!string.IsNullOrWhiteSpace(effectText))
			lines.Add(effectText);

		return lines.Count == 0 ? "No description recorded." : string.Join("\n\n", lines);
	}

	public static string BuildItemDetailDescription(ItemDef item)
	{
		var lines = new List<string>();
		if (!string.IsNullOrWhiteSpace(item.Description))
			lines.Add(item.Description);

		var preparationText = BuildPreparedIngredientText(item);
		if (!string.IsNullOrWhiteSpace(preparationText))
			lines.Add(preparationText);

		return string.Join("\n", lines);
	}

	public static string BuildPreparedIngredientText(ItemDef item)
	{
		return IngredientPreparationCatalog.TryGetPreparedIngredientInfo(item, out _, out var preparationId)
			? $"Preparation: {IngredientPreparationCatalog.GetDisplayName(preparationId)}."
			: string.Empty;
	}

	public static string BuildIngredientEffectsText(ItemDef item)
	{
		if (item.IngredientEffects is null || item.IngredientEffects.Count == 0)
			return string.Empty;

		var lines = new List<string>();
		foreach (var effect in item.IngredientEffects)
		{
			if (effect is null)
				continue;

			var effectText = BuildAuthoredIngredientEffectText(effect);
			if (!string.IsNullOrWhiteSpace(effectText))
				lines.Add(effectText);
		}

		return lines.Count == 0 ? string.Empty : string.Join("\n", lines);
	}

	public static string TryGetVisibleTypeTag(ItemDef item)
	{
		foreach (var rule in ItemTagDisplayRules)
		{
			if (!rule.VisibleToPlayer)
				continue;

			if (!HasTag(item, rule.Tag))
				continue;

			return rule.DisplayName;
		}

		return string.Empty;
	}

	public static string FormatTopStats(Dictionary<string, int>? values, int maxCount, string emptyLabel = "None")
	{
		var lines = new List<string>(maxCount);
		if (values is not null)
		{
			lines.AddRange(values
				.OrderByDescending(x => x.Value)
				.ThenBy(x => x.Key)
				.Take(maxCount)
				.Select(x => $"{DisplayStatName(x.Key)} +{x.Value}"));
		}

		if (lines.Count == 0)
			lines.Add(emptyLabel);

		while (lines.Count < maxCount)
			lines.Add(string.Empty);

		return string.Join("\n", lines);
	}

	public static string FormatTopStatNames(Dictionary<string, int>? values, int maxCount, string emptyLabel = "None")
	{
		var lines = new List<string>(maxCount);
		if (values is not null)
		{
			lines.AddRange(values
				.Where(x => !string.IsNullOrWhiteSpace(x.Key) && x.Value > 0)
				.OrderBy(x => x.Key)
				.Take(maxCount)
				.Select(x => DisplayStatName(x.Key)));
		}

		if (lines.Count == 0)
			lines.Add(emptyLabel);

		while (lines.Count < maxCount)
			lines.Add(string.Empty);

		return string.Join("\n", lines);
	}

	public static string FormatPreparationTraitNames(
		Dictionary<string, IngredientPreparationDef>? preparations,
		int maxCount,
		string emptyLabel = "None")
	{
		return FormatPreparationStatNames(preparations, maxCount, emptyLabel, showTraits: true);
	}

	public static string FormatPreparationRiskNames(
		Dictionary<string, IngredientPreparationDef>? preparations,
		int maxCount,
		string emptyLabel = "None")
	{
		return FormatPreparationStatNames(preparations, maxCount, emptyLabel, showTraits: false);
	}

	public static string DisplayStatName(string key)
	{
		if (string.IsNullOrWhiteSpace(key))
			return string.Empty;

		var normalized = key.Replace('_', ' ').Trim();
		if (normalized.Length == 0)
			return string.Empty;

		return char.ToUpperInvariant(normalized[0]) + normalized[1..];
	}

	private static string FormatPreparationStatNames(
		Dictionary<string, IngredientPreparationDef>? preparations,
		int maxCount,
		string emptyLabel,
		bool showTraits)
	{
		var statNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
		if (preparations is not null)
		{
			foreach (var preparation in preparations.Values)
			{
				if (preparation is null)
					continue;

				var stats = showTraits ? preparation.Traits : preparation.Risks;
				if (stats is null)
					continue;

				foreach (var stat in stats)
				{
					if (string.IsNullOrWhiteSpace(stat.Key) || stat.Value <= 0)
						continue;

					statNames.Add(stat.Key);
				}
			}
		}

		var lines = statNames
			.OrderBy(DisplayStatName)
			.Take(maxCount)
			.Select(DisplayStatName)
			.ToList();

		if (lines.Count == 0)
			lines.Add(emptyLabel);

		while (lines.Count < maxCount)
			lines.Add(string.Empty);

		return string.Join("\n", lines);
	}

	private static string BuildAuthoredIngredientEffectText(IngredientEffectDef effect)
	{
		if (string.IsNullOrWhiteSpace(effect.Name))
			return effect.Description;
		if (string.IsNullOrWhiteSpace(effect.Description))
			return effect.Name;

		return $"{effect.Name}: {effect.Description}";
	}

	private static bool HasTag(ItemDef item, string tag)
	{
		if (item.Tags is null || string.IsNullOrWhiteSpace(tag))
			return false;

		return item.Tags.Any(existingTag => string.Equals(existingTag, tag, System.StringComparison.OrdinalIgnoreCase));
	}
}
