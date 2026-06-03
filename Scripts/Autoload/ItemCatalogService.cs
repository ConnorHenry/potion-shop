using System;
using System.Linq;
using Godot;
using OccultShop.Infrastructure;
using OccultShop.Models;

namespace OccultShop.Autoload;

public partial class ItemCatalogService : Node
{
	[Export] public NodePath RuntimeContentDbPath { get; set; } = new(AutoloadNodePaths.RuntimeContentDb);
	[Export] public NodePath DataDbPath { get; set; } = new(AutoloadNodePaths.DataDb);

	private RuntimeContentDb _runtimeContentDb = default!;
	private DataDb _dataDb = default!;

	public override void _Ready()
	{
		if (!NodeLookup.TryGetRequiredNode<RuntimeContentDb>(
			this,
			RuntimeContentDbPath,
			nameof(ItemCatalogService),
			nameof(RuntimeContentDbPath),
			out _runtimeContentDb))
		{
			return;
		}

		if (!NodeLookup.TryGetRequiredNode<DataDb>(
			this,
			DataDbPath,
			nameof(ItemCatalogService),
			nameof(DataDbPath),
			out _dataDb))
		{
			return;
		}
	}

	public bool TryGetItem(string itemId, out ItemDef item)
	{
		item = default!;

		if (string.IsNullOrWhiteSpace(itemId))
			return false;

		if (_runtimeContentDb.TryGetItem(itemId, out item))
			return true;

		return _dataDb.TryGetItem(itemId, out item);
	}

	public string GetItemName(string itemId)
	{
		return TryGetItem(itemId, out var item) ? item.Name : itemId;
	}

	public bool IsPotion(string itemId)
	{
		return TryGetItem(itemId, out var item) && HasTag(item, ItemTags.Potion);
	}

	public bool IsIngredient(string itemId)
	{
		return TryGetItem(itemId, out var item) && HasTag(item, ItemTags.Ingredient);
	}

	public bool IsConsumable(string itemId)
	{
		return TryGetItem(itemId, out var item) && HasTag(item, ItemTags.Consumable);
	}

	public bool IsTreatedItem(string itemId)
	{
		return TryGetItem(itemId, out var item) && item.Treatment is not null;
	}

	public static bool HasTag(ItemDef item, string tag)
	{
		if (item.Tags is null || string.IsNullOrWhiteSpace(tag))
			return false;

		return item.Tags.Any(existingTag => string.Equals(existingTag, tag, StringComparison.OrdinalIgnoreCase));
	}
}
