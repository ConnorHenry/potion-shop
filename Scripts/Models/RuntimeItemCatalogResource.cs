using Godot;

namespace OccultShop.Models;

[GlobalClass]
public partial class RuntimeItemCatalogResource : Resource
{
	private Godot.Collections.Array<ItemDefResource> _items = new();

	[Export]
	public Godot.Collections.Array<ItemDefResource> Items
	{
		get => _items;
		set
		{
			_items = value ?? new Godot.Collections.Array<ItemDefResource>();
			EmitChanged();
		}
	}
}
