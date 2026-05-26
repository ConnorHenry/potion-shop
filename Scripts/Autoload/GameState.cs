using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OccultShop.Models;
using OccultShop.Persistence;

namespace OccultShop.Autoload;

public partial class GameState : Node
{
	[Export] public NodePath DataDbPath { get; set; } = new("/root/DataDb");
	[Export] public NodePath ItemCatalogPath { get; set; } = new("/root/ItemCatalog");

	public int Day { get; private set; } = 1;
	public int Gold { get; private set; } = 50000;
	public int Dread { get; private set; } = 0;

	// itemId -> qty
	public Dictionary<string, int> Inventory { get; } = new();
	public HashSet<string> ActiveRules { get; } = new();
	public HashSet<string> KnownPotions { get; } = new();
	public Dictionary<string, string> PotionDisplayNames { get; } = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, int> _potionBasePrices = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, List<string>> _potionRecipes = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, string> _combinationPotionItems = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, Queue<List<string>>> _potionBatches = new(StringComparer.OrdinalIgnoreCase);
	public CustomerRequestDef? ActiveCustomerRequest { get; private set; }

	public event Action? Changed;
	private ItemCatalogService _itemCatalog = default!;

	public override void _Ready()
	{
		var itemCatalog = GetNodeOrNull<ItemCatalogService>(ItemCatalogPath);
		if (itemCatalog is null)
		{
			GD.PushError($"GameState: ItemCatalog was not found at '{ItemCatalogPath}'.");
			return;
		}

		_itemCatalog = itemCatalog;
		ResetForNewGame();
	}

	public void ResetForNewGame()
	{
		Day = 1;
		Gold = 50000;
		Dread = 0;
		Inventory.Clear();
		ActiveRules.Clear();
		KnownPotions.Clear();
		PotionDisplayNames.Clear();
		_potionBasePrices.Clear();
		_potionRecipes.Clear();
		_combinationPotionItems.Clear();
		_potionBatches.Clear();
		ActiveCustomerRequest = null;

		SeedStartingInventory();
		EmitChanged();
	}

	public GameStateSnapshot BuildSnapshot()
	{
		var snapshot = new GameStateSnapshot
		{
			Day = Day,
			Gold = Gold,
			Dread = Dread,
			Inventory = new Dictionary<string, int>(Inventory),
			ActiveRules = ActiveRules.ToList(),
			KnownPotions = KnownPotions.ToList(),
			PotionDisplayNames = new Dictionary<string, string>(PotionDisplayNames, StringComparer.OrdinalIgnoreCase),
			PotionBasePrices = new Dictionary<string, int>(_potionBasePrices, StringComparer.OrdinalIgnoreCase),
			PotionRecipes = ClonePotionRecipes(),
			CombinationPotionItems = new Dictionary<string, string>(_combinationPotionItems, StringComparer.OrdinalIgnoreCase),
			PotionBatches = ClonePotionBatches(),
			ActiveCustomerRequest = CloneCustomerRequest(ActiveCustomerRequest)
		};

		return snapshot;
	}

	public void ApplySnapshot(GameStateSnapshot? snapshot)
	{
		if (snapshot is null)
		{
			GD.PushError("GameState: Cannot apply a null snapshot.");
			return;
		}

		Day = Math.Max(1, snapshot.Day);
		Gold = Math.Max(0, snapshot.Gold);
		Dread = Math.Clamp(snapshot.Dread, 0, 100);

		Inventory.Clear();
		if (snapshot.Inventory is not null)
		{
			foreach (var pair in snapshot.Inventory)
			{
				if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value <= 0)
					continue;
				if (!_itemCatalog.TryGetItem(pair.Key, out _))
					continue;

				Inventory[pair.Key] = pair.Value;
			}
		}

		ActiveRules.Clear();
		if (snapshot.ActiveRules is not null)
		{
			foreach (var ruleId in snapshot.ActiveRules)
			{
				if (!string.IsNullOrWhiteSpace(ruleId))
					ActiveRules.Add(ruleId);
			}
		}

		KnownPotions.Clear();
		if (snapshot.KnownPotions is not null)
		{
			foreach (var potionId in snapshot.KnownPotions)
			{
				if (!string.IsNullOrWhiteSpace(potionId))
					KnownPotions.Add(potionId);
			}
		}

		PotionDisplayNames.Clear();
		if (snapshot.PotionDisplayNames is not null)
		{
			foreach (var pair in snapshot.PotionDisplayNames)
			{
				if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
					continue;

				PotionDisplayNames[pair.Key] = pair.Value;
			}
		}

		_potionBasePrices.Clear();
		if (snapshot.PotionBasePrices is not null)
		{
			foreach (var pair in snapshot.PotionBasePrices)
			{
				if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value < 0)
					continue;

				_potionBasePrices[pair.Key] = pair.Value;
			}
		}

		_potionRecipes.Clear();
		if (snapshot.PotionRecipes is not null)
		{
			foreach (var pair in snapshot.PotionRecipes)
			{
				if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value is null || pair.Value.Count == 0)
					continue;

				_potionRecipes[pair.Key] = new List<string>(pair.Value);
			}
		}

		_combinationPotionItems.Clear();
		if (snapshot.CombinationPotionItems is not null)
		{
			foreach (var pair in snapshot.CombinationPotionItems)
			{
				if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
					continue;

				_combinationPotionItems[pair.Key] = pair.Value;
			}
		}

		_potionBatches.Clear();
		if (snapshot.PotionBatches is not null)
		{
			foreach (var pair in snapshot.PotionBatches)
			{
				if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value is null || pair.Value.Count == 0)
					continue;

				var queue = new Queue<List<string>>();
				foreach (var batch in pair.Value)
				{
					if (batch is null || batch.Count == 0)
						continue;

					queue.Enqueue(new List<string>(batch));
				}

				if (queue.Count > 0)
					_potionBatches[pair.Key] = queue;
			}
		}

		ActiveCustomerRequest = CloneCustomerRequest(snapshot.ActiveCustomerRequest);
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
		if (qty <= 0 || string.IsNullOrWhiteSpace(itemId))
			return;
		if (!_itemCatalog.TryGetItem(itemId, out _))
		{
			GD.PushError($"GameState: Cannot add unknown item '{itemId}' to inventory.");
			return;
		}

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

	public void RegisterPotionBasePrice(string potionId, int basePrice)
	{
		if (string.IsNullOrWhiteSpace(potionId) || basePrice < 0)
			return;

		if (_potionBasePrices.ContainsKey(potionId))
			return;

		_potionBasePrices[potionId] = basePrice;
		EmitChanged();
	}

	public bool TryGetPotionBasePrice(string potionId, out int basePrice)
	{
		return _potionBasePrices.TryGetValue(potionId, out basePrice);
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

	private void SeedStartingInventory()
	{
		var dataDb = GetNodeOrNull<DataDb>(DataDbPath);
		if (dataDb is null)
		{
			GD.PushError($"GameState: DataDb was not found at '{DataDbPath}'. Starting inventory could not be seeded.");
			return;
		}

		foreach (var item in dataDb.Items.Values.OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase))
		{
			if (!IsIngredient(item))
				continue;

			AddStartingStack(item.Id, 10);
		}
	}

	private void AddStartingStack(string itemId, int qty)
	{
		if (qty <= 0 || string.IsNullOrWhiteSpace(itemId))
			return;

		Inventory[itemId] = Inventory.GetValueOrDefault(itemId) + qty;
	}

	private static bool IsIngredient(ItemDef item)
	{
		if (item.Tags is null)
			return false;

		return item.Tags.Any(tag => string.Equals(tag, "ingredient", StringComparison.OrdinalIgnoreCase));
	}

	private Dictionary<string, List<string>> ClonePotionRecipes()
	{
		var copy = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
		foreach (var pair in _potionRecipes)
			copy[pair.Key] = new List<string>(pair.Value);

		return copy;
	}

	private Dictionary<string, List<List<string>>> ClonePotionBatches()
	{
		var copy = new Dictionary<string, List<List<string>>>(StringComparer.OrdinalIgnoreCase);
		foreach (var pair in _potionBatches)
			copy[pair.Key] = pair.Value.Select(batch => new List<string>(batch)).ToList();

		return copy;
	}

	private static CustomerRequestDef? CloneCustomerRequest(CustomerRequestDef? request)
	{
		if (request is null)
			return null;

		return new CustomerRequestDef
		{
			Id = request.Id,
			Description = request.Description,
			DesiredTraits = request.DesiredTraits is null ? new Dictionary<string, int>() : new Dictionary<string, int>(request.DesiredTraits),
			BadTraits = request.BadTraits is null ? new Dictionary<string, int>() : new Dictionary<string, int>(request.BadTraits)
		};
	}

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
