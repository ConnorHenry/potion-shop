using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OccultShop.Autoload;

namespace OccultShop.UI;

public static class ItemFilterUtilities
{
	public static void RefreshFilterOptions(OptionButton? filter, List<string> values, string placeholderLabel, ref string? activeFilter)
	{
		if (filter is null)
			return;

		filter.Clear();
		filter.AddItem(placeholderLabel);

		foreach (var value in values)
			filter.AddItem(value);

		if (string.IsNullOrWhiteSpace(activeFilter))
		{
			filter.Selected = 0;
			return;
		}

		for (var index = 1; index < filter.ItemCount; index++)
		{
			var itemText = filter.GetItemText(index);
			if (!string.Equals(itemText, activeFilter, StringComparison.OrdinalIgnoreCase))
				continue;

			filter.Selected = index;
			return;
		}

		activeFilter = null;
		filter.Selected = 0;
	}

	public static List<string> BuildTopTraitNames(IEnumerable<string> itemIds, int maxCount, ItemCatalogService itemCatalog)
	{
		var uniqueNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var itemId in itemIds)
		{
			if (!itemCatalog.TryGetItem(itemId, out var item))
				continue;

			foreach (var trait in item.Traits
				.OrderByDescending(x => x.Value)
				.ThenBy(x => x.Key)
				.Take(maxCount))
			{
				if (string.IsNullOrWhiteSpace(trait.Key))
					continue;
				if (trait.Value <= 0)
					continue;

				uniqueNames.Add(trait.Key);
			}
		}

		return uniqueNames.OrderBy(name => name).ToList();
	}

	public static List<string> BuildTraitNames(IEnumerable<string> itemIds, ItemCatalogService itemCatalog)
	{
		var uniqueNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var itemId in itemIds)
		{
			if (!itemCatalog.TryGetItem(itemId, out var item))
				continue;

			foreach (var trait in item.Traits)
			{
				if (string.IsNullOrWhiteSpace(trait.Key))
					continue;
				if (trait.Value <= 0)
					continue;

				uniqueNames.Add(trait.Key);
			}
		}

		return uniqueNames.OrderBy(name => name).ToList();
	}

	public static List<string> BuildRiskNames(IEnumerable<string> itemIds, ItemCatalogService itemCatalog)
	{
		var uniqueNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var itemId in itemIds)
		{
			if (!itemCatalog.TryGetItem(itemId, out var item))
				continue;

			foreach (var risk in item.Risks)
			{
				if (string.IsNullOrWhiteSpace(risk.Key))
					continue;
				if (risk.Value <= 0)
					continue;

				uniqueNames.Add(risk.Key);
			}
		}

		return uniqueNames.OrderBy(name => name).ToList();
	}

	public static bool ItemHasTrait(string itemId, string traitName, ItemCatalogService itemCatalog, int topCount = -1)
	{
		if (!itemCatalog.TryGetItem(itemId, out var item))
			return false;

		var traits = topCount > 0
			? item.Traits.OrderByDescending(x => x.Value).ThenBy(x => x.Key).Take(topCount)
			: item.Traits;

		foreach (var trait in traits)
		{
			if (!string.Equals(trait.Key, traitName, StringComparison.OrdinalIgnoreCase))
				continue;

			return trait.Value > 0;
		}

		return false;
	}

	public static bool ItemHasRisk(string itemId, string riskName, ItemCatalogService itemCatalog)
	{
		if (!itemCatalog.TryGetItem(itemId, out var item))
			return false;

		foreach (var risk in item.Risks)
		{
			if (!string.Equals(risk.Key, riskName, StringComparison.OrdinalIgnoreCase))
				continue;

			return risk.Value > 0;
		}

		return false;
	}
}
