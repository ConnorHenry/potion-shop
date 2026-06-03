using Godot;
using OccultShop.Models;

namespace OccultShop.Autoload;

public static class ItemCatalog
{
	public static bool TryGetItem(string itemId, out ItemDef item)
	{
		return Service.TryGetItem(itemId, out item);
	}

	public static string GetItemName(string itemId)
	{
		return Service.GetItemName(itemId);
	}

	public static bool IsPotion(string itemId)
	{
		return Service.IsPotion(itemId);
	}

	private static ItemCatalogService Service => ((SceneTree)Engine.GetMainLoop()).Root.GetNode<ItemCatalogService>(AutoloadNodePaths.ItemCatalog);
}
