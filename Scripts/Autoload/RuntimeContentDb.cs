using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using OccultShop.Models;

namespace OccultShop.Autoload;

public partial class RuntimeContentDb : Node
{
	private const string DefaultRuntimeCatalogPath = "user://runtime/runtime_item_catalog.tres";

	[Export]
	public RuntimeItemCatalogResource RuntimeCatalog
	{
		get => _runtimeCatalog;
		set => SetRuntimeCatalog(value, rebuildItems: true, emitChanged: true);
	}

	[Export(PropertyHint.File, "*.tres,*.res")]
	public string RuntimeCatalogPath { get; set; } = DefaultRuntimeCatalogPath;

	public IReadOnlyDictionary<string, ItemDef> Items => _items;

	private readonly Dictionary<string, ItemDef> _items = new(StringComparer.OrdinalIgnoreCase);
	private RuntimeItemCatalogResource _runtimeCatalog = new();
	private bool _suspendCatalogSignalHandling;

	public event Action? Changed;

	public override void _Ready()
	{
		SetRuntimeCatalog(_runtimeCatalog, rebuildItems: false, emitChanged: false);

		if (!LoadRuntimeCatalogFromDisk())
			RebuildItemsFromCatalog(emitChanged: false);
	}

	public override void _ExitTree()
	{
		DisconnectCatalogSignals(_runtimeCatalog);
	}

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
			SyncCatalogFromItems();
			SaveRuntimeCatalogToDisk();
			Changed?.Invoke();
			return;
		}

		foreach (var item in items)
		{
			if (item is null || string.IsNullOrWhiteSpace(item.Id))
				continue;

			_items[item.Id] = CloneItem(item);
		}

		SyncCatalogFromItems();
		SaveRuntimeCatalogToDisk();
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
		SyncCatalogFromItems();
		SaveRuntimeCatalogToDisk();
		Changed?.Invoke();
		return item;
	}

	public bool TrySetRuntimeItemBasePrice(string itemId, int basePrice)
	{
		if (string.IsNullOrWhiteSpace(itemId))
			return false;

		if (!_items.TryGetValue(itemId, out var item))
			return false;

		if (item.BasePrice == basePrice)
			return true;

		item.BasePrice = basePrice;
		SyncCatalogFromItems();
		SaveRuntimeCatalogToDisk();
		Changed?.Invoke();
		return true;
	}

	public bool UpsertRuntimeItem(ItemDef item)
	{
		if (item is null)
		{
			GD.PushError("RuntimeContentDb: Cannot upsert a null runtime item.");
			return false;
		}

		if (string.IsNullOrWhiteSpace(item.Id))
		{
			GD.PushError("RuntimeContentDb: Runtime item Id is required.");
			return false;
		}

		var normalizedItem = CloneItem(item);
		normalizedItem.Id = normalizedItem.Id.Trim();
		normalizedItem.Tags = NormalizeTags(normalizedItem.Tags);

		_items[normalizedItem.Id] = normalizedItem;
		SyncCatalogFromItems();
		SaveRuntimeCatalogToDisk();
		Changed?.Invoke();
		return true;
	}

	public void ClearRuntimeItems()
	{
		_items.Clear();
		SyncCatalogFromItems();
		SaveRuntimeCatalogToDisk();
		Changed?.Invoke();
	}

	public bool SaveRuntimeCatalogToDisk()
	{
		if (string.IsNullOrWhiteSpace(RuntimeCatalogPath))
		{
			GD.PushError("RuntimeContentDb: RuntimeCatalogPath is missing.");
			return false;
		}

		EnsureRuntimeCatalogDirectoryExists(RuntimeCatalogPath);
		var error = ResourceSaver.Save(_runtimeCatalog, RuntimeCatalogPath);
		if (error != Error.Ok)
		{
			GD.PushError($"RuntimeContentDb: Failed to save runtime catalog to '{RuntimeCatalogPath}'. Error: {error}");
			return false;
		}

		return true;
	}

	public bool LoadRuntimeCatalogFromDisk()
	{
		if (string.IsNullOrWhiteSpace(RuntimeCatalogPath))
		{
			GD.PushError("RuntimeContentDb: RuntimeCatalogPath is missing.");
			return false;
		}

		if (!Godot.FileAccess.FileExists(RuntimeCatalogPath))
			return false;

		var loaded = ResourceLoader.Load<RuntimeItemCatalogResource>(RuntimeCatalogPath);
		if (loaded is null)
		{
			GD.PushError($"RuntimeContentDb: Could not load runtime catalog from '{RuntimeCatalogPath}'.");
			return false;
		}

		SetRuntimeCatalog(loaded, rebuildItems: true, emitChanged: true);
		return true;
	}

	private void SetRuntimeCatalog(RuntimeItemCatalogResource? value, bool rebuildItems, bool emitChanged)
	{
		DisconnectCatalogSignals(_runtimeCatalog);
		_runtimeCatalog = value ?? new RuntimeItemCatalogResource();
		ConnectCatalogSignals(_runtimeCatalog);

		if (rebuildItems)
			RebuildItemsFromCatalog(emitChanged);
	}

	private void ConnectCatalogSignals(RuntimeItemCatalogResource catalog)
	{
		if (!catalog.IsConnected(Resource.SignalName.Changed, Callable.From(OnRuntimeCatalogChanged)))
			catalog.Changed += OnRuntimeCatalogChanged;

		ConnectCatalogItemSignals(catalog.Items);
	}

	private void DisconnectCatalogSignals(RuntimeItemCatalogResource catalog)
	{
		if (catalog.IsConnected(Resource.SignalName.Changed, Callable.From(OnRuntimeCatalogChanged)))
			catalog.Changed -= OnRuntimeCatalogChanged;

		DisconnectCatalogItemSignals(catalog.Items);
	}

	private void ConnectCatalogItemSignals(Godot.Collections.Array<ItemDefResource> items)
	{
		foreach (var item in items)
		{
			if (item is not null && !item.IsConnected(Resource.SignalName.Changed, Callable.From(OnRuntimeCatalogItemChanged)))
				item.Changed += OnRuntimeCatalogItemChanged;
		}
	}

	private void DisconnectCatalogItemSignals(Godot.Collections.Array<ItemDefResource> items)
	{
		foreach (var item in items)
		{
			if (item is not null && item.IsConnected(Resource.SignalName.Changed, Callable.From(OnRuntimeCatalogItemChanged)))
				item.Changed -= OnRuntimeCatalogItemChanged;
		}
	}

	private void OnRuntimeCatalogChanged()
	{
		if (_suspendCatalogSignalHandling)
			return;

		// Rebind nested item change events after inserts/removals through the inspector.
		DisconnectCatalogItemSignals(_runtimeCatalog.Items);
		ConnectCatalogItemSignals(_runtimeCatalog.Items);
		RebuildItemsFromCatalog(emitChanged: true);
		SaveRuntimeCatalogToDisk();
	}

	private void OnRuntimeCatalogItemChanged()
	{
		if (_suspendCatalogSignalHandling)
			return;

		RebuildItemsFromCatalog(emitChanged: true);
		SaveRuntimeCatalogToDisk();
	}

	private void RebuildItemsFromCatalog(bool emitChanged)
	{
		_items.Clear();

		foreach (var itemResource in _runtimeCatalog.Items)
		{
			if (itemResource is null)
				continue;

			var item = itemResource.ToItemDef();
			if (string.IsNullOrWhiteSpace(item.Id))
				continue;

			_items[item.Id] = CloneItem(item);
		}

		if (emitChanged)
			Changed?.Invoke();
	}

	private void SyncCatalogFromItems()
	{
		_suspendCatalogSignalHandling = true;
		try
		{
			DisconnectCatalogItemSignals(_runtimeCatalog.Items);
			_runtimeCatalog.Items.Clear();

			foreach (var item in _items.Values.OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase))
				_runtimeCatalog.Items.Add(ItemDefResource.FromItemDef(item));

			ConnectCatalogItemSignals(_runtimeCatalog.Items);
			_runtimeCatalog.EmitChanged();
		}
		finally
		{
			_suspendCatalogSignalHandling = false;
		}
	}

	private static void EnsureRuntimeCatalogDirectoryExists(string resourcePath)
	{
		var absolutePath = ProjectSettings.GlobalizePath(resourcePath);
		var directory = Path.GetDirectoryName(absolutePath);
		if (!string.IsNullOrWhiteSpace(directory))
			Directory.CreateDirectory(directory);
	}

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
			BasePrice = item.BasePrice,
			ConsumableEffect = item.ConsumableEffect is null
				? null
				: new ConsumableEffectDef
				{
					Kind = item.ConsumableEffect.Kind,
					RiskId = item.ConsumableEffect.RiskId,
					Description = item.ConsumableEffect.Description
				},
			ConsumableGate = item.ConsumableGate is null
				? null
				: new ConsumableGateDef
				{
					AllowedTargetTags = item.ConsumableGate.AllowedTargetTags?.ToList() ?? new List<string>()
				},
			Treatment = item.Treatment is null
				? null
				: new ItemTreatmentDef
				{
					BaseItemId = item.Treatment.BaseItemId,
					ConsumableItemId = item.Treatment.ConsumableItemId,
					RemovedRisk = item.Treatment.RemovedRisk
				}
		};
	}

	private static List<string> NormalizeTags(List<string>? tags)
	{
		var normalized = new List<string>();
		if (tags is null)
			return normalized;

		foreach (var tag in tags)
		{
			if (string.IsNullOrWhiteSpace(tag))
				continue;

			var trimmedTag = tag.Trim();
			if (normalized.Any(existing => string.Equals(existing, trimmedTag, StringComparison.OrdinalIgnoreCase)))
				continue;

			normalized.Add(trimmedTag);
		}

		return normalized;
	}
}
