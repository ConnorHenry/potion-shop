using System;
using System.Collections.Generic;
using Godot;

namespace OccultShop.Autoload;

public partial class GameState : Node
{
	public int Day { get; private set; } = 1;
	public int Gold { get; private set; } = 50;
	public int Dread { get; private set; } = 0;

	// itemId -> qty
	public Dictionary<string, int> Inventory { get; } = new();
	public HashSet<string> ActiveRules { get; } = new();

	public event Action? Changed;

	public override void _Ready()
	{
		// Tiny starting kit
		AddItem("salt", 2);
		AddItem("black_candle", 1);
		AddItem("bone_charm", 1);
		AddItem("eye_of_newt", 1);
		AddItem("broken_heart", 1);
		EmitChanged();
	}

	public void NextDay()
	{
		Day += 1;
		EmitChanged();
	}

	public void AddGold(int amount)
	{
		Gold = Math.Max(0, Gold + amount);
		EmitChanged();
	}

	public void AddDread(int amount)
	{
		Dread = Math.Clamp(Dread + amount, 0, 100);
		EmitChanged();
	}

	public void AddRule(string ruleId)
	{
		if (string.IsNullOrWhiteSpace(ruleId)) return;
		ActiveRules.Add(ruleId);
		EmitChanged();
	}

	public bool HasItem(string itemId, int qty)
		=> Inventory.TryGetValue(itemId, out var have) && have >= qty;

	public void AddItem(string itemId, int qty)
	{
		if (qty <= 0) return;
		Inventory[itemId] = Inventory.GetValueOrDefault(itemId) + qty;
		EmitChanged();
	}

	public bool ConsumeItem(string itemId, int qty)
	{
		if (qty <= 0) return true;
		if (!HasItem(itemId, qty)) return false;
		Inventory[itemId] -= qty;
		if (Inventory[itemId] <= 0) Inventory.Remove(itemId);
		EmitChanged();
		return true;
	}

	private void EmitChanged() => Changed?.Invoke();
}
