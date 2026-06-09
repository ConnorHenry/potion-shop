using System;
using System.Collections.Generic;
using System.Linq;
using OccultShop.Autoload;

namespace OccultShop.UI;

public sealed class InventoryPanelStackQueryOptions
{
	public string? ActivePotionTraitFilter { get; init; }
	public string? ActivePotionRiskFilter { get; init; }
	public string? ActiveIngredientTypeFilter { get; init; }
	public string? ActiveIngredientTraitFilter { get; init; }
	public string? ActiveIngredientRiskFilter { get; init; }
	public bool HasPotionTraitFilter { get; init; }
	public bool HasPotionRiskFilter { get; init; }
	public bool HasIngredientTypeFilter { get; init; }
	public bool HasIngredientTraitFilter { get; init; }
	public bool HasIngredientRiskFilter { get; init; }
	public bool IngredientsAscending { get; init; }
}

public sealed class InventoryPanelStackQueryResult
{
	public List<KeyValuePair<string, int>> PotionStacksToRender { get; init; } = new();
	public List<KeyValuePair<string, int>> ConsumableStacksToRender { get; init; } = new();
	public List<KeyValuePair<string, int>> IngredientStacksToRender { get; init; } = new();
	public List<string> PotionTraitNames { get; init; } = new();
	public List<string> PotionRiskNames { get; init; } = new();
	public List<string> IngredientTraitNames { get; init; } = new();
	public List<string> IngredientRiskNames { get; init; } = new();
	public string? ActivePotionTraitFilter { get; init; }
	public string? ActivePotionRiskFilter { get; init; }
	public string? ActiveIngredientTypeFilter { get; init; }
	public string? ActiveIngredientTraitFilter { get; init; }
	public string? ActiveIngredientRiskFilter { get; init; }
}

public static class InventoryPanelStackQuery
{
	public static InventoryPanelStackQueryResult Build(
		IEnumerable<KeyValuePair<string, int>> inventory,
		ItemCatalogService itemCatalog,
		IReadOnlyList<string> ingredientTypeFilterOptions,
		InventoryPanelStackQueryOptions options)
	{
		var potionStacks = inventory.Where(stack => itemCatalog.IsPotion(stack.Key)).ToList();
		var consumableStacks = inventory.Where(stack => itemCatalog.IsConsumable(stack.Key)).ToList();
		var ingredientStacks = inventory
			.Where(stack => !itemCatalog.IsPotion(stack.Key) && !itemCatalog.IsConsumable(stack.Key))
			.ToList();

		var potionTraitNames = ItemFilterUtilities.BuildTopTraitNames(potionStacks.Select(stack => stack.Key), 3, itemCatalog);
		var potionRiskNames = ItemFilterUtilities.BuildRiskNames(potionStacks.Select(stack => stack.Key), itemCatalog);
		var ingredientTraitNames = ItemFilterUtilities.BuildTraitNames(ingredientStacks.Select(stack => stack.Key), itemCatalog);
		var ingredientRiskNames = ItemFilterUtilities.BuildRiskNames(ingredientStacks.Select(stack => stack.Key), itemCatalog);

		var activePotionTraitFilter = ClearMissingFilter(options.ActivePotionTraitFilter, potionTraitNames);
		var activePotionRiskFilter = ClearMissingFilter(options.ActivePotionRiskFilter, potionRiskNames);
		var activeIngredientTraitFilter = ClearMissingFilter(options.ActiveIngredientTraitFilter, ingredientTraitNames);
		var activeIngredientTypeFilter = ClearMissingFilter(options.ActiveIngredientTypeFilter, ingredientTypeFilterOptions);
		var activeIngredientRiskFilter = ClearMissingFilter(options.ActiveIngredientRiskFilter, ingredientRiskNames);

		if (!options.HasPotionTraitFilter)
			activePotionTraitFilter = null;
		if (!options.HasPotionRiskFilter)
			activePotionRiskFilter = null;
		if (!options.HasIngredientTypeFilter)
			activeIngredientTypeFilter = null;
		if (!options.HasIngredientTraitFilter)
			activeIngredientTraitFilter = null;
		if (!options.HasIngredientRiskFilter)
			activeIngredientRiskFilter = null;

		var potionStacksToRender = ApplyPotionFilters(
			potionStacks,
			activePotionTraitFilter,
			activePotionRiskFilter,
			itemCatalog);
		var ingredientStacksToRender = ApplyIngredientFilters(
			ingredientStacks,
			activeIngredientTypeFilter,
			activeIngredientTraitFilter,
			activeIngredientRiskFilter,
			itemCatalog);

		return new InventoryPanelStackQueryResult
		{
			PotionStacksToRender = SortAscending(potionStacksToRender, itemCatalog),
			ConsumableStacksToRender = SortAscending(consumableStacks, itemCatalog),
			IngredientStacksToRender = options.IngredientsAscending
				? SortAscending(ingredientStacksToRender, itemCatalog)
				: SortDescending(ingredientStacksToRender, itemCatalog),
			PotionTraitNames = potionTraitNames,
			PotionRiskNames = potionRiskNames,
			IngredientTraitNames = ingredientTraitNames,
			IngredientRiskNames = ingredientRiskNames,
			ActivePotionTraitFilter = activePotionTraitFilter,
			ActivePotionRiskFilter = activePotionRiskFilter,
			ActiveIngredientTypeFilter = activeIngredientTypeFilter,
			ActiveIngredientTraitFilter = activeIngredientTraitFilter,
			ActiveIngredientRiskFilter = activeIngredientRiskFilter
		};
	}

	private static List<KeyValuePair<string, int>> ApplyPotionFilters(
		List<KeyValuePair<string, int>> potionStacks,
		string? activePotionTraitFilter,
		string? activePotionRiskFilter,
		ItemCatalogService itemCatalog)
	{
		var stacksToRender = potionStacks;
		if (!string.IsNullOrWhiteSpace(activePotionTraitFilter))
		{
			stacksToRender = stacksToRender
				.Where(stack => ItemFilterUtilities.ItemHasTrait(stack.Key, activePotionTraitFilter, itemCatalog, topCount: 3))
				.ToList();
		}

		if (!string.IsNullOrWhiteSpace(activePotionRiskFilter))
		{
			stacksToRender = stacksToRender
				.Where(stack => ItemFilterUtilities.ItemHasRisk(stack.Key, activePotionRiskFilter, itemCatalog))
				.ToList();
		}

		return stacksToRender;
	}

	private static List<KeyValuePair<string, int>> ApplyIngredientFilters(
		List<KeyValuePair<string, int>> ingredientStacks,
		string? activeIngredientTypeFilter,
		string? activeIngredientTraitFilter,
		string? activeIngredientRiskFilter,
		ItemCatalogService itemCatalog)
	{
		var stacksToRender = ingredientStacks;
		if (!string.IsNullOrWhiteSpace(activeIngredientTypeFilter))
		{
			stacksToRender = stacksToRender
				.Where(stack => ItemHasIngredientType(stack.Key, activeIngredientTypeFilter, itemCatalog))
				.ToList();
		}

		if (!string.IsNullOrWhiteSpace(activeIngredientTraitFilter))
		{
			stacksToRender = stacksToRender
				.Where(stack => ItemFilterUtilities.ItemHasTrait(stack.Key, activeIngredientTraitFilter, itemCatalog))
				.ToList();
		}

		if (!string.IsNullOrWhiteSpace(activeIngredientRiskFilter))
		{
			stacksToRender = stacksToRender
				.Where(stack => ItemFilterUtilities.ItemHasRisk(stack.Key, activeIngredientRiskFilter, itemCatalog))
				.ToList();
		}

		return stacksToRender;
	}

	private static string? ClearMissingFilter(string? activeFilter, IEnumerable<string> availableValues)
	{
		if (string.IsNullOrWhiteSpace(activeFilter))
			return activeFilter;

		foreach (var value in availableValues)
		{
			if (string.Equals(value, activeFilter, StringComparison.OrdinalIgnoreCase))
				return activeFilter;
		}

		return null;
	}

	private static bool ItemHasIngredientType(string itemId, string ingredientType, ItemCatalogService itemCatalog)
	{
		if (!itemCatalog.TryGetItem(itemId, out var item))
			return false;

		return ItemCatalogService.HasTag(item, ingredientType);
	}

	private static List<KeyValuePair<string, int>> SortAscending(
		IEnumerable<KeyValuePair<string, int>> stacks,
		ItemCatalogService itemCatalog)
	{
		return stacks
			.OrderBy(stack => itemCatalog.GetItemName(stack.Key))
			.ThenBy(stack => stack.Key)
			.ToList();
	}

	private static List<KeyValuePair<string, int>> SortDescending(
		IEnumerable<KeyValuePair<string, int>> stacks,
		ItemCatalogService itemCatalog)
	{
		return stacks
			.OrderByDescending(stack => itemCatalog.GetItemName(stack.Key))
			.ThenByDescending(stack => stack.Key)
			.ToList();
	}
}
