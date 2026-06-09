using System;
using System.Collections.Generic;

namespace OccultShop.Systems;

public readonly struct InventoryAddResult
{
	public InventoryAddResult(int addedQuantity, bool changed)
	{
		AddedQuantity = addedQuantity;
		Changed = changed;
	}

	public int AddedQuantity { get; }
	public bool Changed { get; }
}

public readonly struct PendingConsumableGrantResult
{
	public PendingConsumableGrantResult(bool accepted, bool changed)
	{
		Accepted = accepted;
		Changed = changed;
	}

	public bool Accepted { get; }
	public bool Changed { get; }
}

public sealed class InventoryState
{
	private readonly Dictionary<string, int> _inventory;
	private readonly Func<string, bool> _itemExists;
	private readonly Func<string, bool> _isPotion;
	private readonly Func<string, bool> _isConsumable;
	private readonly Func<string, bool> _isIngredient;
	private readonly Action<string> _pushError;
	private readonly int _maxUniquePotionInventoryQuantity;
	private readonly int _maxPotionStackQuantity;
	private readonly int _maxUniqueConsumableInventoryQuantity;
	private readonly int _maxConsumableStackQuantity;

	public InventoryState(
		Dictionary<string, int> inventory,
		Func<string, bool> itemExists,
		Func<string, bool> isPotion,
		Func<string, bool> isConsumable,
		Func<string, bool> isIngredient,
		Action<string> pushError,
		int maxUniquePotionInventoryQuantity,
		int maxPotionStackQuantity,
		int maxUniqueConsumableInventoryQuantity,
		int maxConsumableStackQuantity)
	{
		_inventory = inventory;
		_itemExists = itemExists;
		_isPotion = isPotion;
		_isConsumable = isConsumable;
		_isIngredient = isIngredient;
		_pushError = pushError;
		_maxUniquePotionInventoryQuantity = maxUniquePotionInventoryQuantity;
		_maxPotionStackQuantity = maxPotionStackQuantity;
		_maxUniqueConsumableInventoryQuantity = maxUniqueConsumableInventoryQuantity;
		_maxConsumableStackQuantity = maxConsumableStackQuantity;
	}

	public string PendingConsumableItemId { get; private set; } = string.Empty;
	public int PendingConsumableQuantity { get; private set; }
	public bool HasPendingConsumableGrant => !string.IsNullOrWhiteSpace(PendingConsumableItemId) && PendingConsumableQuantity > 0;

	public void Clear()
	{
		_inventory.Clear();
		ClearPendingConsumableGrant();
	}

	public Dictionary<string, int> CloneInventory()
	{
		return new Dictionary<string, int>(_inventory);
	}

	public void Restore(
		Dictionary<string, int>? inventory,
		string pendingConsumableItemId,
		int pendingConsumableQuantity)
	{
		Clear();
		if (inventory is not null)
		{
			foreach (var pair in inventory)
			{
				if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value <= 0)
					continue;
				if (!_itemExists(pair.Key))
					continue;

				_inventory[pair.Key] = pair.Value;
			}
		}

		RestorePendingConsumableGrant(pendingConsumableItemId, pendingConsumableQuantity);
	}

	public bool HasItem(string itemId, int quantity)
	{
		return _inventory.TryGetValue(itemId, out var have) && have >= quantity;
	}

	public bool AddRawStack(string itemId, int quantity)
	{
		if (quantity <= 0 || string.IsNullOrWhiteSpace(itemId))
			return false;

		_inventory[itemId] = _inventory.GetValueOrDefault(itemId) + quantity;
		return true;
	}

	public InventoryAddResult AddItem(string itemId, int quantity)
	{
		if (quantity <= 0 || string.IsNullOrWhiteSpace(itemId))
			return new InventoryAddResult(0, changed: false);
		if (!_itemExists(itemId))
		{
			_pushError($"GameState: Cannot add unknown item '{itemId}' to inventory.");
			return new InventoryAddResult(0, changed: false);
		}

		var quantityToAdd = ResolveInventoryAddQuantity(itemId, quantity, out var changed);
		if (quantityToAdd <= 0)
			return new InventoryAddResult(0, changed);

		_inventory[itemId] = _inventory.GetValueOrDefault(itemId) + quantityToAdd;
		return new InventoryAddResult(quantityToAdd, changed: true);
	}

	public bool ConsumeItem(string itemId, int quantity)
	{
		if (quantity <= 0)
			return true;
		if (!HasItem(itemId, quantity))
			return false;

		_inventory[itemId] -= quantity;
		if (_inventory[itemId] <= 0)
			_inventory.Remove(itemId);

		return true;
	}

	public PendingConsumableGrantResult TryAcceptPendingConsumableByDiscarding(string discardItemId, out string error)
	{
		error = string.Empty;
		if (!HasPendingConsumableGrant)
		{
			error = "No pending consumable is waiting.";
			return new PendingConsumableGrantResult(accepted: false, changed: false);
		}

		if (string.IsNullOrWhiteSpace(discardItemId))
		{
			error = "Choose a consumable to discard.";
			return new PendingConsumableGrantResult(accepted: false, changed: false);
		}

		if (!_isConsumable(discardItemId))
		{
			error = "Only consumables can be discarded to make room.";
			return new PendingConsumableGrantResult(accepted: false, changed: false);
		}

		if (!_inventory.TryGetValue(discardItemId, out var discardQuantity) || discardQuantity <= 0)
		{
			error = "Selected consumable is not in inventory.";
			return new PendingConsumableGrantResult(accepted: false, changed: false);
		}

		var pendingItemId = PendingConsumableItemId;
		var pendingQuantity = PendingConsumableQuantity;
		if (!_itemExists(pendingItemId) || !_isConsumable(pendingItemId))
		{
			ClearPendingConsumableGrant();
			error = "Pending consumable no longer exists.";
			return new PendingConsumableGrantResult(accepted: false, changed: true);
		}

		_inventory.Remove(discardItemId);
		var quantityToAdd = Math.Min(Math.Max(1, pendingQuantity), _maxConsumableStackQuantity);
		_inventory[pendingItemId] = _inventory.GetValueOrDefault(pendingItemId) + quantityToAdd;
		if (quantityToAdd < pendingQuantity)
			_pushError($"GameState: Added {quantityToAdd} of consumable '{pendingItemId}' because consumable stacks are capped at {_maxConsumableStackQuantity}.");

		ClearPendingConsumableGrant();
		return new PendingConsumableGrantResult(accepted: true, changed: true);
	}

	public bool DeclinePendingConsumableGrant()
	{
		if (!HasPendingConsumableGrant)
			return false;

		ClearPendingConsumableGrant();
		return true;
	}

	public int ConsumeEachIngredient(int quantity)
	{
		if (quantity <= 0)
			return 0;

		var consumedCount = 0;
		var ingredientIds = new List<string>();
		foreach (var pair in _inventory)
		{
			if (pair.Value <= 0)
				continue;
			if (!_isIngredient(pair.Key))
				continue;

			ingredientIds.Add(pair.Key);
		}

		foreach (var ingredientId in ingredientIds)
		{
			var consumeQuantity = Math.Min(quantity, _inventory.GetValueOrDefault(ingredientId));
			if (consumeQuantity <= 0)
				continue;

			_inventory[ingredientId] -= consumeQuantity;
			consumedCount += consumeQuantity;
			if (_inventory[ingredientId] <= 0)
				_inventory.Remove(ingredientId);
		}

		return consumedCount;
	}

	public int CountPotionStacks()
	{
		var count = 0;
		foreach (var pair in _inventory)
		{
			if (pair.Value <= 0)
				continue;
			if (!_isPotion(pair.Key))
				continue;

			count++;
		}

		return count;
	}

	public int CountConsumableStacks()
	{
		var count = 0;
		foreach (var pair in _inventory)
		{
			if (pair.Value <= 0)
				continue;
			if (!_isConsumable(pair.Key))
				continue;

			count++;
		}

		return count;
	}

	private int ResolveInventoryAddQuantity(string itemId, int requestedQuantity, out bool changed)
	{
		changed = false;
		if (!_isPotion(itemId))
		{
			if (_isConsumable(itemId))
				return ResolveConsumableInventoryAddQuantity(itemId, requestedQuantity, out changed);

			return requestedQuantity;
		}

		var currentQuantity = _inventory.GetValueOrDefault(itemId);
		if (currentQuantity <= 0 && CountPotionStacks() >= _maxUniquePotionInventoryQuantity)
		{
			_pushError($"GameState: Cannot add potion '{itemId}'. Potion inventory already has {_maxUniquePotionInventoryQuantity} unique potions.");
			return 0;
		}

		var availableQuantity = _maxPotionStackQuantity - currentQuantity;
		if (availableQuantity <= 0)
		{
			_pushError($"GameState: Cannot add potion '{itemId}'. Potion stack already has {_maxPotionStackQuantity} items.");
			return 0;
		}

		var quantityToAdd = Math.Min(requestedQuantity, availableQuantity);
		if (quantityToAdd < requestedQuantity)
			_pushError($"GameState: Added {quantityToAdd} of potion '{itemId}' because potion stacks are capped at {_maxPotionStackQuantity}.");

		return quantityToAdd;
	}

	private int ResolveConsumableInventoryAddQuantity(string itemId, int requestedQuantity, out bool changed)
	{
		changed = false;
		var currentQuantity = _inventory.GetValueOrDefault(itemId);
		if (currentQuantity <= 0 && CountConsumableStacks() >= _maxUniqueConsumableInventoryQuantity)
		{
			SetPendingConsumableGrant(itemId, requestedQuantity);
			changed = true;
			_pushError($"GameState: Cannot add consumable '{itemId}'. Consumable inventory already has {_maxUniqueConsumableInventoryQuantity} unique consumables.");
			return 0;
		}

		var availableQuantity = _maxConsumableStackQuantity - currentQuantity;
		if (availableQuantity <= 0)
		{
			_pushError($"GameState: Cannot add consumable '{itemId}'. Consumable stack already has {_maxConsumableStackQuantity} items.");
			return 0;
		}

		var quantityToAdd = Math.Min(requestedQuantity, availableQuantity);
		if (quantityToAdd < requestedQuantity)
			_pushError($"GameState: Added {quantityToAdd} of consumable '{itemId}' because consumable stacks are capped at {_maxConsumableStackQuantity}.");

		return quantityToAdd;
	}

	private void SetPendingConsumableGrant(string itemId, int quantity)
	{
		PendingConsumableItemId = itemId;
		PendingConsumableQuantity = Math.Max(1, quantity);
	}

	private void ClearPendingConsumableGrant()
	{
		PendingConsumableItemId = string.Empty;
		PendingConsumableQuantity = 0;
	}

	private void RestorePendingConsumableGrant(string itemId, int quantity)
	{
		if (string.IsNullOrWhiteSpace(itemId) || quantity <= 0)
			return;
		if (!_itemExists(itemId) || !_isConsumable(itemId))
			return;

		SetPendingConsumableGrant(itemId, quantity);
	}
}
