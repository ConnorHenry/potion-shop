using Godot;
using System.Linq;
using OccultShop.Models;

namespace OccultShop.Autoload;

public static class ItemCatalog
{
	public static bool TryGetItem(string itemId, out ItemDef item)
	{
		if (RuntimeContentDb.TryGetItem(itemId, out item))
			return true;

		return DataDb.TryGetItem(itemId, out item);
	}

	public static string GetItemName(string itemId)
	{
		return TryGetItem(itemId, out var item) ? item.Name : itemId;
	}

	public static bool IsPotion(string itemId)
	{
		return TryGetItem(itemId, out var item) &&
			item.Tags.Any(tag => string.Equals(tag, "potion", System.StringComparison.OrdinalIgnoreCase));
	}

	private static RuntimeContentDb RuntimeContentDb => ((SceneTree)Engine.GetMainLoop()).Root.GetNode<RuntimeContentDb>("/root/RuntimeContentDb");
	private static DataDb DataDb => ((SceneTree)Engine.GetMainLoop()).Root.GetNode<DataDb>("/root/DataDb");
}
