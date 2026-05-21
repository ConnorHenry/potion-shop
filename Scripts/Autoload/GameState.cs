using System;
using System.Collections.Generic;
using Godot;
using OccultShop.Models;

namespace OccultShop.Autoload;

public partial class GameState : Node
{
	public int Day { get; private set; } = 1;
	public int Gold { get; private set; } = 50000;
	public int Dread { get; private set; } = 0;

	// itemId -> qty
	public Dictionary<string, int> Inventory { get; } = new();
	public HashSet<string> ActiveRules { get; } = new();
	public HashSet<string> KnownPotions { get; } = new();
	public Dictionary<string, string> PotionDisplayNames { get; } = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, List<string>> _potionRecipes = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, string> _combinationPotionItems = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, Queue<List<string>>> _potionBatches = new(StringComparer.OrdinalIgnoreCase);
	public CustomerRequestDef? ActiveCustomerRequest { get; private set; }

	public event Action? Changed;

	public override void _Ready()
	{
		// Tiny starting kit
		AddItem("mooncap_mushroom", 10);
		AddItem("grave_mint", 10);
		AddItem("lavender_ash", 10);
		AddItem("black_ichor", 10);
		AddItem("obsidian_resin", 10);
		AddItem("obsidian_resin", 10);
		AddItem("amber_nightshade", 10);
		AddItem("silver_thorn_bloom", 10);
		AddItem("moonwhisper_orchid", 10);
		AddItem("raven_ash_peony", 10);
		AddItem("iron_lullaby_root", 10);
		AddItem("mercury_vision_resin", 10);
		AddItem("hallowed_balm_leaf", 10);
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
		ConsumePotionBatches(itemId, qty);
		if (Inventory[itemId] <= 0) Inventory.Remove(itemId);
		EmitChanged();
		return true;
	}

	public void LearnPotion(string potionId)
	{
		if (string.IsNullOrWhiteSpace(potionId)) return;
		if (KnownPotions.Add(potionId))
			EmitChanged();
	}

	public bool KnowsPotion(string potionId) => KnownPotions.Contains(potionId);

	public void RecordPotionRecipe(string potionItemId, IReadOnlyList<string> ingredientIds)
	{
		if (string.IsNullOrWhiteSpace(potionItemId) || ingredientIds is null || ingredientIds.Count == 0)
			return;

		if (!_potionRecipes.ContainsKey(potionItemId))
			_potionRecipes[potionItemId] = new List<string>(ingredientIds);

		LearnPotion(potionItemId);
	}

	public bool TryGetPotionRecipe(string potionItemId, out List<string> ingredientIds)
	{
		ingredientIds = new List<string>();
		if (!_potionRecipes.TryGetValue(potionItemId, out var stored))
			return false;

		ingredientIds = new List<string>(stored);
		return true;
	}

	public void SetPotionDisplayName(string potionId, string displayName)
	{
		if (string.IsNullOrWhiteSpace(potionId) || string.IsNullOrWhiteSpace(displayName))
			return;

		PotionDisplayNames[potionId] = displayName;
		EmitChanged();
	}

	public string? GetPotionDisplayName(string potionId)
	{
		return PotionDisplayNames.TryGetValue(potionId, out var displayName) ? displayName : null;
	}

	public bool TryGetPotionForCombination(string combinationKey, out string potionItemId)
	{
		return _combinationPotionItems.TryGetValue(combinationKey, out potionItemId!);
	}

	public void SetPotionForCombination(string combinationKey, string potionItemId)
	{
		if (string.IsNullOrWhiteSpace(combinationKey) || string.IsNullOrWhiteSpace(potionItemId))
			return;

		_combinationPotionItems[combinationKey] = potionItemId;
	}

	public void RecordPotionBatch(string potionItemId, IReadOnlyList<string> ingredientIds)
	{
		if (string.IsNullOrWhiteSpace(potionItemId) || ingredientIds is null || ingredientIds.Count == 0)
			return;

		if (!_potionBatches.TryGetValue(potionItemId, out var queue))
		{
			queue = new Queue<List<string>>();
			_potionBatches[potionItemId] = queue;
		}

		queue.Enqueue(new List<string>(ingredientIds));
	}

	public bool TryPeekPotionBatch(string potionItemId, out List<string> ingredientIds)
	{
		ingredientIds = new List<string>();
		if (!_potionBatches.TryGetValue(potionItemId, out var queue) || queue.Count == 0)
			return false;

		ingredientIds = new List<string>(queue.Peek());
		return true;
	}

	public void SetActiveCustomerRequest(CustomerRequestDef? request)
	{
		ActiveCustomerRequest = request;
		EmitChanged();
	}

	public void ClearActiveCustomerRequest()
	{
		if (ActiveCustomerRequest is null)
			return;

		ActiveCustomerRequest = null;
		EmitChanged();
	}

	private void EmitChanged() => Changed?.Invoke();

	private void ConsumePotionBatches(string itemId, int qty)
	{
		if (!_potionBatches.TryGetValue(itemId, out var queue) || queue.Count == 0)
			return;

		for (var i = 0; i < qty && queue.Count > 0; i++)
			queue.Dequeue();

		if (queue.Count == 0)
			_potionBatches.Remove(itemId);
	}
}
