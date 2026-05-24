using System.Collections.Generic;
using System.Linq;
using OccultShop.Models;

namespace OccultShop.Autoload;

public partial class RuntimeContentDb : Godot.Node
{
	public IReadOnlyDictionary<string, ItemDef> Items => _items;

	private readonly Dictionary<string, ItemDef> _items = new();

	public bool TryGetItem(string itemId, out ItemDef item)
	{
		return _items.TryGetValue(itemId, out item!);
	}

	public List<ItemDef> BuildRuntimeItemSnapshot()
	{
		var snapshot = new List<ItemDef>(_items.Count);
		foreach (var item in _items.Values)
			snapshot.Add(CloneItem(item));

		return snapshot;
	}

	public void RestoreRuntimeItems(IEnumerable<ItemDef>? items)
	{
		_items.Clear();
		if (items is null)
		{
			Changed?.Invoke();
			return;
		}

		foreach (var item in items)
		{
			if (item is null || string.IsNullOrWhiteSpace(item.Id))
				continue;

			_items[item.Id] = CloneItem(item);
		}

		Changed?.Invoke();
	}

	public ItemDef RegisterRuntimePotionItem(
		string itemId,
		string name,
		string? iconPath,
		int basePrice,
		int quality,
		Dictionary<string, int> traits,
		Dictionary<string, int> risks)
	{
		if (_items.TryGetValue(itemId, out var existing))
			return existing;

		var item = new ItemDef
		{
			Id = itemId,
			Name = name,
			IconPath = iconPath,
			BasePrice = basePrice,
			Quality = quality,
			Tags = new List<string> { "potion" },
			Traits = traits,
			Risks = risks
		};

		_items[itemId] = item;
		Changed?.Invoke();
		return item;
	}

	public void ClearRuntimeItems()
	{
		_items.Clear();
		Changed?.Invoke();
	}

	public event System.Action? Changed;

	private static ItemDef CloneItem(ItemDef item)
	{
		return new ItemDef
		{
			Id = item.Id,
			Name = item.Name,
			IconPath = item.IconPath,
			Description = item.Description,
			Tags = item.Tags?.ToList() ?? new List<string>(),
			Quality = item.Quality,
			Traits = item.Traits is null ? new Dictionary<string, int>() : new Dictionary<string, int>(item.Traits),
			Risks = item.Risks is null ? new Dictionary<string, int>() : new Dictionary<string, int>(item.Risks),
			BasePrice = item.BasePrice
		};
	}
}
