using System;
using System.Collections.Generic;
using Godot;
using ImGuiGodot;
using ImGuiNET;
using OccultShop.Autoload;
using OccultShop.Models;
using Vector2 = System.Numerics.Vector2;

namespace OccultShop.Debug;

public partial class RuntimeDebugImGui : Node
{
	private readonly List<string> _potionItemIds = new();
	private readonly List<string> _traitNames = new();
	private readonly Dictionary<string, List<string>> _traitToItemIds = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, string> _itemDisplayNames = new(StringComparer.OrdinalIgnoreCase);

	private GameState _gameState = default!;
	private DataDb _dataDb = default!;
	private RuntimeContentDb _runtimeContentDb = default!;

	private int _goldInput;
	private int _dreadInput;
	private int _addPotionQuantity = 1;
	private int _removePotionQuantity = 1;
	private int _traitItemQuantity = 1;
	private int _selectedPotionIndex;
	private int _selectedPotionRemoveIndex;
	private int _selectedTraitIndex;
	private int _selectedTraitItemIndex;
	private string _statusMessage = string.Empty;

	public override void _Ready()
	{
		var gameState = GetNodeOrNull<GameState>("/root/GameState");
		if (gameState is null)
		{
			GD.PushError("RuntimeDebugImGui: /root/GameState was not found.");
			return;
		}

		var dataDb = GetNodeOrNull<DataDb>("/root/DataDb");
		if (dataDb is null)
		{
			GD.PushError("RuntimeDebugImGui: /root/DataDb was not found.");
			return;
		}

		var runtimeContentDb = GetNodeOrNull<RuntimeContentDb>("/root/RuntimeContentDb");
		if (runtimeContentDb is null)
		{
			GD.PushError("RuntimeDebugImGui: /root/RuntimeContentDb was not found.");
			return;
		}

		_gameState = gameState;
		_dataDb = dataDb;
		_runtimeContentDb = runtimeContentDb;

		_goldInput = _gameState.Gold;
		_dreadInput = _gameState.Dread;

		RebuildDebugCatalog();
		_runtimeContentDb.Changed += OnRuntimeContentChanged;

		ImGuiGD.Connect(DrawDebugWindow);
		ImGuiGD.Visible = true;
	}

	public override void _ExitTree()
	{
		if (_runtimeContentDb is not null)
			_runtimeContentDb.Changed -= OnRuntimeContentChanged;
	}

	private void DrawDebugWindow()
	{
		ImGui.SetNextWindowSize(new Vector2(500.0f, 680.0f), ImGuiCond.FirstUseEver);

		if (!ImGui.Begin("Scenario Debugger"))
		{
			ImGui.End();
			return;
		}

		DrawStateSection();
		ImGui.Separator();
		DrawPotionSection();
		ImGui.Separator();
		DrawTraitSection();

		if (!string.IsNullOrWhiteSpace(_statusMessage))
		{
			ImGui.Separator();
			ImGui.TextWrapped(_statusMessage);
		}

		ImGui.End();
	}

	private void DrawStateSection()
	{
		if (!ImGui.CollapsingHeader("State", ImGuiTreeNodeFlags.DefaultOpen))
			return;

		ImGui.Text($"Current Gold: {_gameState.Gold}");
		ImGui.InputInt("Gold Input", ref _goldInput);
		if (_goldInput < 0)
			_goldInput = 0;

		if (ImGui.Button("Apply Gold"))
		{
			var delta = _goldInput - _gameState.Gold;
			_gameState.AddGold(delta);
			_statusMessage = $"Gold set to {_gameState.Gold}.";
		}

		ImGui.SameLine();
		if (ImGui.SmallButton("+100 Gold"))
		{
			_gameState.AddGold(100);
			_goldInput = _gameState.Gold;
			_statusMessage = $"Gold increased to {_gameState.Gold}.";
		}

		ImGui.Text($"Current Dread: {_gameState.Dread}");
		ImGui.InputInt("Dread Input", ref _dreadInput);
		_dreadInput = Math.Clamp(_dreadInput, 0, 100);

		if (ImGui.Button("Apply Dread"))
		{
			var delta = _dreadInput - _gameState.Dread;
			_gameState.AddDread(delta);
			_statusMessage = $"Dread set to {_gameState.Dread}.";
		}

		ImGui.SameLine();
		if (ImGui.SmallButton("-5 Dread"))
		{
			_gameState.AddDread(-5);
			_dreadInput = _gameState.Dread;
			_statusMessage = $"Dread adjusted to {_gameState.Dread}.";
		}

		ImGui.Text($"Current Day: {_gameState.Day}");
		if (ImGui.Button("Advance Day +1"))
		{
			_gameState.NextDay();
			_statusMessage = $"Advanced to day {_gameState.Day}.";
		}

		ImGui.SameLine();
		if (ImGui.SmallButton("Advance Day +5"))
		{
			for (var i = 0; i < 5; i++)
				_gameState.NextDay();
			_statusMessage = $"Advanced to day {_gameState.Day}.";
		}
	}

	private void DrawPotionSection()
	{
		if (!ImGui.CollapsingHeader("Potions", ImGuiTreeNodeFlags.DefaultOpen))
			return;

		if (_potionItemIds.Count == 0)
		{
			ImGui.Text("No potion definitions available.");
			return;
		}

		var safePotionIndex = ClampIndex(_selectedPotionIndex, _potionItemIds.Count);
		var selectedPotionId = _potionItemIds[safePotionIndex];
		var potionPreview = BuildItemLabel(selectedPotionId);

		if (ImGui.BeginCombo("Add Potion", potionPreview))
		{
			for (var i = 0; i < _potionItemIds.Count; i++)
			{
				var potionId = _potionItemIds[i];
				var isSelected = i == safePotionIndex;
				if (ImGui.Selectable(BuildItemLabel(potionId), isSelected))
					_selectedPotionIndex = i;
				if (isSelected)
					ImGui.SetItemDefaultFocus();
			}
			ImGui.EndCombo();
		}

		ImGui.InputInt("Add Qty", ref _addPotionQuantity);
		if (_addPotionQuantity < 1)
			_addPotionQuantity = 1;

		if (ImGui.Button("Add Potion Stack"))
		{
			_gameState.AddItem(selectedPotionId, _addPotionQuantity);
			_statusMessage = $"Added {_addPotionQuantity}x {BuildItemLabel(selectedPotionId)}.";
		}

		var potionInventory = BuildPotionInventorySnapshot();
		if (potionInventory.Count == 0)
		{
			ImGui.Text("No potions currently in inventory.");
			return;
		}

		var safeRemoveIndex = ClampIndex(_selectedPotionRemoveIndex, potionInventory.Count);
		var removePotionId = potionInventory[safeRemoveIndex].ItemId;
		var removePreview = $"{BuildItemLabel(removePotionId)} x{potionInventory[safeRemoveIndex].Quantity}";

		if (ImGui.BeginCombo("Remove Potion", removePreview))
		{
			for (var i = 0; i < potionInventory.Count; i++)
			{
				var potion = potionInventory[i];
				var isSelected = i == safeRemoveIndex;
				var label = $"{BuildItemLabel(potion.ItemId)} x{potion.Quantity}";
				if (ImGui.Selectable(label, isSelected))
					_selectedPotionRemoveIndex = i;
				if (isSelected)
					ImGui.SetItemDefaultFocus();
			}
			ImGui.EndCombo();
		}

		ImGui.InputInt("Remove Qty", ref _removePotionQuantity);
		if (_removePotionQuantity < 1)
			_removePotionQuantity = 1;

		if (ImGui.Button("Consume Potion"))
		{
			var before = _gameState.Inventory.TryGetValue(removePotionId, out var existingBefore) ? existingBefore : 0;
			var requested = Math.Min(before, _removePotionQuantity);
			if (requested > 0 && _gameState.ConsumeItem(removePotionId, requested))
			{
				_statusMessage = $"Consumed {requested}x {BuildItemLabel(removePotionId)}.";
			}
			else
			{
				_statusMessage = $"Cannot consume potion {BuildItemLabel(removePotionId)}.";
			}
		}
	}

	private void DrawTraitSection()
	{
		if (!ImGui.CollapsingHeader("Traits Present", ImGuiTreeNodeFlags.DefaultOpen))
			return;

		var traitTotals = BuildInventoryTraitTotals();
		if (traitTotals.Count == 0)
		{
			ImGui.Text("No positive traits in current inventory.");
		}
		else
		{
			ImGui.Text("Inventory Trait Totals:");
			for (var i = 0; i < traitTotals.Count; i++)
				ImGui.BulletText($"{traitTotals[i].Key}: {traitTotals[i].Value}");
		}

		if (_traitNames.Count == 0)
		{
			ImGui.Text("No traits available for scenario injection.");
			return;
		}

		var safeTraitIndex = ClampIndex(_selectedTraitIndex, _traitNames.Count);
		var selectedTrait = _traitNames[safeTraitIndex];
		if (ImGui.BeginCombo("Inject Trait", selectedTrait))
		{
			for (var i = 0; i < _traitNames.Count; i++)
			{
				var trait = _traitNames[i];
				var isSelected = i == safeTraitIndex;
				if (ImGui.Selectable(trait, isSelected))
				{
					_selectedTraitIndex = i;
					_selectedTraitItemIndex = 0;
				}
				if (isSelected)
					ImGui.SetItemDefaultFocus();
			}
			ImGui.EndCombo();
		}

		if (!_traitToItemIds.TryGetValue(selectedTrait, out var candidateItems) || candidateItems.Count == 0)
		{
			ImGui.Text("No items found for selected trait.");
			return;
		}

		var safeTraitItemIndex = ClampIndex(_selectedTraitItemIndex, candidateItems.Count);
		var selectedTraitItemId = candidateItems[safeTraitItemIndex];
		if (ImGui.BeginCombo("Source Item", BuildItemLabel(selectedTraitItemId)))
		{
			for (var i = 0; i < candidateItems.Count; i++)
			{
				var itemId = candidateItems[i];
				var isSelected = i == safeTraitItemIndex;
				if (ImGui.Selectable(BuildItemLabel(itemId), isSelected))
					_selectedTraitItemIndex = i;
				if (isSelected)
					ImGui.SetItemDefaultFocus();
			}
			ImGui.EndCombo();
		}

		ImGui.InputInt("Trait Item Qty", ref _traitItemQuantity);
		if (_traitItemQuantity < 1)
			_traitItemQuantity = 1;

		if (ImGui.Button("Add Trait Item"))
		{
			_gameState.AddItem(selectedTraitItemId, _traitItemQuantity);
			_statusMessage = $"Added {_traitItemQuantity}x {BuildItemLabel(selectedTraitItemId)} for trait {selectedTrait}.";
		}
	}

	private void OnRuntimeContentChanged()
	{
		RebuildDebugCatalog();
	}

	private void RebuildDebugCatalog()
	{
		_potionItemIds.Clear();
		_traitNames.Clear();
		_traitToItemIds.Clear();
		_itemDisplayNames.Clear();

		var merged = new Dictionary<string, ItemDef>(StringComparer.OrdinalIgnoreCase);
		foreach (var pair in _dataDb.Items)
			merged[pair.Key] = pair.Value;
		foreach (var pair in _runtimeContentDb.Items)
			merged[pair.Key] = pair.Value;

		foreach (var pair in merged)
		{
			var item = pair.Value;
			if (item is null || string.IsNullOrWhiteSpace(item.Id))
				continue;

			_itemDisplayNames[item.Id] = string.IsNullOrWhiteSpace(item.Name) ? item.Id : item.Name;

			var isPotion = HasTag(item, "potion");
			var isIngredient = HasTag(item, "ingredient");
			if (isPotion)
				_potionItemIds.Add(item.Id);

			if (!isPotion && !isIngredient)
				continue;

			if (item.Traits is null)
				continue;

			foreach (var trait in item.Traits)
			{
				if (string.IsNullOrWhiteSpace(trait.Key) || trait.Value <= 0)
					continue;

				if (!_traitToItemIds.TryGetValue(trait.Key, out var itemIds))
				{
					itemIds = new List<string>();
					_traitToItemIds[trait.Key] = itemIds;
					_traitNames.Add(trait.Key);
				}

				itemIds.Add(item.Id);
			}
		}

		_potionItemIds.Sort((a, b) => string.Compare(BuildItemLabel(a), BuildItemLabel(b), StringComparison.OrdinalIgnoreCase));
		_traitNames.Sort((a, b) => string.Compare(a, b, StringComparison.OrdinalIgnoreCase));

		foreach (var pair in _traitToItemIds)
			pair.Value.Sort((a, b) => string.Compare(BuildItemLabel(a), BuildItemLabel(b), StringComparison.OrdinalIgnoreCase));

		_selectedPotionIndex = ClampIndex(_selectedPotionIndex, _potionItemIds.Count);
		_selectedTraitIndex = ClampIndex(_selectedTraitIndex, _traitNames.Count);
	}

	private List<KeyValuePair<string, int>> BuildInventoryTraitTotals()
	{
		var totals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		foreach (var stack in _gameState.Inventory)
		{
			if (stack.Value <= 0)
				continue;

			if (!ItemCatalog.TryGetItem(stack.Key, out var item))
				continue;

			if (item.Traits is null)
				continue;

			foreach (var trait in item.Traits)
			{
				if (string.IsNullOrWhiteSpace(trait.Key) || trait.Value <= 0)
					continue;

				var contribution = stack.Value * trait.Value;
				if (!totals.TryAdd(trait.Key, contribution))
					totals[trait.Key] += contribution;
			}
		}

		var sorted = new List<KeyValuePair<string, int>>(totals);
		sorted.Sort((a, b) =>
		{
			var byValue = b.Value.CompareTo(a.Value);
			if (byValue != 0)
				return byValue;
			return string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase);
		});
		return sorted;
	}

	private List<(string ItemId, int Quantity)> BuildPotionInventorySnapshot()
	{
		var potions = new List<(string ItemId, int Quantity)>();
		foreach (var stack in _gameState.Inventory)
		{
			if (stack.Value <= 0)
				continue;
			if (!IsPotionId(stack.Key))
				continue;
			potions.Add((stack.Key, stack.Value));
		}

		potions.Sort((a, b) => string.Compare(BuildItemLabel(a.ItemId), BuildItemLabel(b.ItemId), StringComparison.OrdinalIgnoreCase));
		return potions;
	}

	private bool IsPotionId(string itemId)
	{
		if (!ItemCatalog.TryGetItem(itemId, out var item))
			return false;

		return HasTag(item, "potion");
	}

	private static bool HasTag(ItemDef item, string tag)
	{
		if (item.Tags is null)
			return false;

		foreach (var existingTag in item.Tags)
		{
			if (string.Equals(existingTag, tag, StringComparison.OrdinalIgnoreCase))
				return true;
		}

		return false;
	}

	private string BuildItemLabel(string itemId)
	{
		if (_itemDisplayNames.TryGetValue(itemId, out var name))
			return $"{name} ({itemId})";

		return itemId;
	}

	private static int ClampIndex(int index, int count)
	{
		if (count <= 0)
			return 0;
		if (index < 0)
			return 0;
		if (index >= count)
			return count - 1;
		return index;
	}
}
