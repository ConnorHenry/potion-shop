using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using OccultShop.Models;

namespace OccultShop.Autoload;

public partial class DataDb : Node
{
	public IReadOnlyDictionary<string, ItemDef> Items => _items;
	public IReadOnlyDictionary<string, RuleDef> Rules => _rules;
	public IReadOnlyList<EventCardDef> Events => _events;
	public IReadOnlyList<CustomerInteractionDef> CustomerInteractions => _customerInteractions;
	public IReadOnlyList<SynergyRule> Synergies => _synergies;

	private Dictionary<string, ItemDef> _items = new();
	private Dictionary<string, RuleDef> _rules = new();
	private List<EventCardDef> _events = new();
	private List<CustomerInteractionDef> _customerInteractions = new();
	private List<SynergyRule> _synergies = new();

	private static readonly JsonSerializerOptions JsonOpts = new()
	{
		PropertyNameCaseInsensitive = true
	};

	public override void _Ready()
	{
		ReloadAll();
	}

	public void ReloadAll()
	{
		_items = LoadArray<ItemDef>("res://Data/items.json").ToDictionary(x => x.Id, x => x);
		_rules = LoadArray<RuleDef>("res://Data/rules.json").ToDictionary(x => x.Id, x => x);
		_events = LoadArray<EventCardDef>("res://Data/events.json");
		_customerInteractions = LoadArray<CustomerInteractionDef>("res://Data/customers.json");
		_synergies = LoadArray<SynergyRule>("res://Data/synergies.json");
	}

	public bool TryGetItem(string itemId, out ItemDef item)
	{
		return _items.TryGetValue(itemId, out item!);
	}

	private static List<T> LoadArray<T>(string path)
	{
		if (!Godot.FileAccess.FileExists(path))
			throw new Exception($"Missing data file: {path}");

		using var f = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
		var json = f.GetAsText();
		var data = JsonSerializer.Deserialize<List<T>>(json, JsonOpts);
		return data ?? new List<T>();
	}
}
