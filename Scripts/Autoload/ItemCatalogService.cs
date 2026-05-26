using System;
using System.Linq;
using Godot;
using OccultShop.Models;

namespace OccultShop.Autoload;

public partial class ItemCatalogService : Node
{
	[Export] public NodePath RuntimeContentDbPath { get; set; } = new("/root/RuntimeContentDb");
	[Export] public NodePath DataDbPath { get; set; } = new("/root/DataDb");

	private RuntimeContentDb _runtimeContentDb = default!;
	private DataDb _dataDb = default!;

	public override void _Ready()
	{
		var runtimeContentDb = GetNodeOrNull<RuntimeContentDb>(RuntimeContentDbPath);
		if (runtimeContentDb is null)
		{
			GD.PushError($"ItemCatalogService: RuntimeContentDb was not found at '{RuntimeContentDbPath}'.");
			return;
		}

		var dataDb = GetNodeOrNull<DataDb>(DataDbPath);
		if (dataDb is null)
		{
			GD.PushError($"ItemCatalogService: DataDb was not found at '{DataDbPath}'.");
			return;
		}

		_runtimeContentDb = runtimeContentDb;
		_dataDb = dataDb;
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
		return TryGetItem(itemId, out var item) && HasTag(item, "potion");
	}

	public bool IsIngredient(string itemId)
	{
		return TryGetItem(itemId, out var item) && HasTag(item, "ingredient");
	}

	public static bool HasTag(ItemDef item, string tag)
	{
		if (item.Tags is null || string.IsNullOrWhiteSpace(tag))
			return false;

		return item.Tags.Any(existingTag => string.Equals(existingTag, tag, StringComparison.OrdinalIgnoreCase));
	}
}
