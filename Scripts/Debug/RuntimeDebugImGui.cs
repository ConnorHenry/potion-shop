using System;
using System.Collections.Generic;
using Godot;
using ImGuiGodot;
using ImGuiNET;
using OccultShop.Controllers;
using OccultShop.Autoload;
using OccultShop.Models;
using OccultShop.Systems;
using Vector2 = System.Numerics.Vector2;

namespace OccultShop.Debug;

public partial class RuntimeDebugImGui : Node
{
	private readonly List<string> _potionItemIds = new();
	private readonly List<string> _consumableItemIds = new();
	private readonly List<string> _ingredientItemIds = new();
	private readonly List<string> _bookPotionRecipeIds = new();
	private readonly List<string> _bookIngredientItemIds = new();
	private readonly List<string> _traitNames = new();
	private readonly Dictionary<string, List<string>> _traitToItemIds = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, string> _itemDisplayNames = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, string> _bookPotionDisplayNames = new(StringComparer.OrdinalIgnoreCase);

	private GameState _gameState = default!;
	private DataDb _dataDb = default!;
	private RuntimeContentDb _runtimeContentDb = default!;
	private DayController? _dayController;

	private int _goldInput;
	private int _dreadInput;
	private int _addPotionQuantity = 1;
	private int _removePotionQuantity = 1;
	private int _addConsumableQuantity = 1;
	private int _removeConsumableQuantity = 1;
	private int _traitItemQuantity = 1;
	private int _selectedPotionIndex;
	private int _selectedPotionRemoveIndex;
	private int _selectedConsumableIndex;
	private int _selectedConsumableRemoveIndex;
	private int _selectedTraitIndex;
	private int _selectedTraitItemIndex;
	private int _selectedBookPotionIndex;
	private int _selectedBookIngredientIndex;
	private string _statusMessage = string.Empty;
	private string _runtimeItemIdInput = string.Empty;
	private string _runtimeItemNameInput = string.Empty;
	private string _runtimeItemIconPathInput = string.Empty;
	private string _runtimeItemDescriptionInput = string.Empty;
	private string _runtimeItemTraitsInput = string.Empty;
	private string _runtimeItemRisksInput = string.Empty;
	private int _runtimeItemBasePrice = 10;
	private int _runtimeItemQuality = 50;
	private int _runtimeItemStartingQuantity = 1;
	private bool _runtimeItemTagIngredient = true;
	private bool _runtimeItemTagPotion;
	[Export] public NodePath DayControllerPath = new("../DayController");

	public override void _Ready()
	{
		var gameState = GetNodeOrNull<GameState>(AutoloadNodePaths.GameState);
		if (gameState is null)
		{
			GD.PushError($"RuntimeDebugImGui: {AutoloadNodePaths.GameState} was not found.");
			return;
		}

		var dataDb = GetNodeOrNull<DataDb>(AutoloadNodePaths.DataDb);
		if (dataDb is null)
		{
			GD.PushError($"RuntimeDebugImGui: {AutoloadNodePaths.DataDb} was not found.");
			return;
		}

		var runtimeContentDb = GetNodeOrNull<RuntimeContentDb>(AutoloadNodePaths.RuntimeContentDb);
		if (runtimeContentDb is null)
		{
			GD.PushError($"RuntimeDebugImGui: {AutoloadNodePaths.RuntimeContentDb} was not found.");
			return;
		}

		_dayController = GetNodeOrNull<DayController>(DayControllerPath);
		if (_dayController is null)
		{
			GD.PushError($"RuntimeDebugImGui: DayController was not found at '{DayControllerPath}'.");
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
		DrawConsumableSection();
		ImGui.Separator();
		DrawIngredientSection();
		ImGui.Separator();
		DrawBookRecordingSection();
		ImGui.Separator();
		DrawTraitSection();
		ImGui.Separator();
		DrawRuntimeCatalogSection();

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

		ImGui.Separator();
		DrawShopDayControls();
	}

	private void DrawShopDayControls()
	{
		if (_dayController is null)
		{
			ImGui.Text("Shop Day: DayController unavailable");
			return;
		}

		if (!_dayController.IsShopOpen)
		{
			ImGui.Text("Shop Day: closed");
			return;
		}

		ImGui.Text($"Shop Day: open ({_dayController.CustomersArrivedToday}/{_dayController.MaxCustomersPerDay} customers)");
		if (ImGui.Button("Close Shop Now"))
			TryCloseShopDay();
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

	private void DrawIngredientSection()
	{
		if (!ImGui.CollapsingHeader("Ingredients", ImGuiTreeNodeFlags.DefaultOpen))
			return;

		ImGui.TextWrapped("Developer action for filling the inventory with every base ingredient.");

		if (_ingredientItemIds.Count == 0)
		{
			ImGui.Text("No ingredient definitions available.");
			return;
		}

		if (ImGui.Button("Add 10x of Every Ingredient"))
		{
			var addedStackCount = AddEveryIngredientStack(10);

			_statusMessage = addedStackCount > 0
				? $"Added 10x of {addedStackCount} base ingredient stacks to inventory."
				: "No base ingredient stacks were added.";
		}
	}

	private int AddEveryIngredientStack(int quantity)
	{
		if (quantity <= 0)
			return 0;

		var addedStackCount = 0;
		var ingredientIds = new List<string>(_ingredientItemIds);
		foreach (var ingredientId in ingredientIds)
		{
			if (!TryGetDebugItem(ingredientId, out var item) || !IsBaseIngredient(item))
				continue;

			if (TryAddInventoryStack(item.Id, quantity))
				addedStackCount += 1;
		}

		return addedStackCount;
	}

	private bool TryAddInventoryStack(string itemId, int quantity)
	{
		var before = _gameState.Inventory.GetValueOrDefault(itemId);
		_gameState.AddItem(itemId, quantity);
		return _gameState.Inventory.GetValueOrDefault(itemId) > before;
	}

	private bool TryGetDebugItem(string itemId, out ItemDef item)
	{
		if (_runtimeContentDb.TryGetItem(itemId, out item))
			return true;

		return _dataDb.TryGetItem(itemId, out item);
	}

	private void DrawConsumableSection()
	{
		if (!ImGui.CollapsingHeader("Consumables", ImGuiTreeNodeFlags.DefaultOpen))
			return;

		if (_consumableItemIds.Count == 0)
		{
			ImGui.Text("No consumable definitions available.");
			return;
		}

		var safeConsumableIndex = ClampIndex(_selectedConsumableIndex, _consumableItemIds.Count);
		var selectedConsumableId = _consumableItemIds[safeConsumableIndex];
		var consumablePreview = BuildItemLabel(selectedConsumableId);

		if (ImGui.BeginCombo("Add Consumable", consumablePreview))
		{
			for (var i = 0; i < _consumableItemIds.Count; i++)
			{
				var consumableId = _consumableItemIds[i];
				var isSelected = i == safeConsumableIndex;
				if (ImGui.Selectable(BuildItemLabel(consumableId), isSelected))
					_selectedConsumableIndex = i;
				if (isSelected)
					ImGui.SetItemDefaultFocus();
			}
			ImGui.EndCombo();
		}

		ImGui.InputInt("Add Consumable Qty", ref _addConsumableQuantity);
		if (_addConsumableQuantity < 1)
			_addConsumableQuantity = 1;

		if (ImGui.Button("Add Consumable Stack"))
		{
			_gameState.AddItem(selectedConsumableId, _addConsumableQuantity);
			_statusMessage = $"Added {_addConsumableQuantity}x {BuildItemLabel(selectedConsumableId)}.";
		}

		var consumableInventory = BuildConsumableInventorySnapshot();
		if (consumableInventory.Count == 0)
		{
			ImGui.Text("No consumables currently in inventory.");
			return;
		}

		var safeRemoveIndex = ClampIndex(_selectedConsumableRemoveIndex, consumableInventory.Count);
		var removeConsumableId = consumableInventory[safeRemoveIndex].ItemId;
		var removePreview = $"{BuildItemLabel(removeConsumableId)} x{consumableInventory[safeRemoveIndex].Quantity}";

		if (ImGui.BeginCombo("Remove Consumable", removePreview))
		{
			for (var i = 0; i < consumableInventory.Count; i++)
			{
				var consumable = consumableInventory[i];
				var isSelected = i == safeRemoveIndex;
				var label = $"{BuildItemLabel(consumable.ItemId)} x{consumable.Quantity}";
				if (ImGui.Selectable(label, isSelected))
					_selectedConsumableRemoveIndex = i;
				if (isSelected)
					ImGui.SetItemDefaultFocus();
			}
			ImGui.EndCombo();
		}

		ImGui.InputInt("Remove Consumable Qty", ref _removeConsumableQuantity);
		if (_removeConsumableQuantity < 1)
			_removeConsumableQuantity = 1;

		if (ImGui.Button("Consume Consumable"))
		{
			var before = _gameState.Inventory.TryGetValue(removeConsumableId, out var existingBefore) ? existingBefore : 0;
			var requested = Math.Min(before, _removeConsumableQuantity);
			if (requested > 0 && _gameState.ConsumeItem(removeConsumableId, requested))
				_statusMessage = $"Consumed {requested}x {BuildItemLabel(removeConsumableId)}.";
			else
				_statusMessage = $"Cannot consume consumable {BuildItemLabel(removeConsumableId)}.";
		}
	}

	private void DrawBookRecordingSection()
	{
		if (!ImGui.CollapsingHeader("Book Recording", ImGuiTreeNodeFlags.DefaultOpen))
			return;

		DrawPotionBookRecordingControls();
		ImGui.Separator();
		DrawIngredientBookRecordingControls();
	}

	private void DrawPotionBookRecordingControls()
	{
		ImGui.Text("Potion Book");
		if (_bookPotionRecipeIds.Count == 0)
		{
			ImGui.Text("No authored potion recipes available.");
			return;
		}

		var safePotionIndex = ClampIndex(_selectedBookPotionIndex, _bookPotionRecipeIds.Count);
		var recipeId = _bookPotionRecipeIds[safePotionIndex];
		if (ImGui.BeginCombo("Authored Potion", BuildBookPotionLabel(recipeId)))
		{
			for (var i = 0; i < _bookPotionRecipeIds.Count; i++)
			{
				var candidateRecipeId = _bookPotionRecipeIds[i];
				var isSelected = i == safePotionIndex;
				if (ImGui.Selectable(BuildBookPotionLabel(candidateRecipeId), isSelected))
					_selectedBookPotionIndex = i;
				if (isSelected)
					ImGui.SetItemDefaultFocus();
			}
			ImGui.EndCombo();
		}

		var recorded = IsPotionRecipeRecordedInBook(recipeId);
		ImGui.Text(recorded ? "Current: Recorded" : "Current: Unknown");
		if (!ImGui.Checkbox("Recorded in potion book", ref recorded))
			return;

		SetPotionRecipeRecordedInBook(recipeId, recorded);
	}

	private void DrawIngredientBookRecordingControls()
	{
		ImGui.Text("Ingredient Book");
		if (_bookIngredientItemIds.Count == 0)
		{
			ImGui.Text("No authored ingredients available.");
			return;
		}

		var safeIngredientIndex = ClampIndex(_selectedBookIngredientIndex, _bookIngredientItemIds.Count);
		var ingredientId = _bookIngredientItemIds[safeIngredientIndex];
		if (ImGui.BeginCombo("Authored Ingredient", BuildItemLabel(ingredientId)))
		{
			for (var i = 0; i < _bookIngredientItemIds.Count; i++)
			{
				var candidateIngredientId = _bookIngredientItemIds[i];
				var isSelected = i == safeIngredientIndex;
				if (ImGui.Selectable(BuildItemLabel(candidateIngredientId), isSelected))
					_selectedBookIngredientIndex = i;
				if (isSelected)
					ImGui.SetItemDefaultFocus();
			}
			ImGui.EndCombo();
		}

		var recorded = _gameState.KnowsIngredient(ingredientId);
		ImGui.Text(recorded ? "Current: Recorded" : "Current: Unknown");
		if (ImGui.Checkbox("Recorded in ingredient book", ref recorded))
			SetIngredientRecordedInBook(ingredientId, recorded);

		ImGui.Separator();
		DrawIngredientTraitKnowledgeControls();
	}

	private void DrawIngredientTraitKnowledgeControls()
	{
		if (!ImGui.Button("Unlock All Ingredient Traits"))
			return;

		var items = new List<ItemDef>();
		items.AddRange(_dataDb.Items.Values);
		items.AddRange(_runtimeContentDb.Items.Values);
		_gameState.UnlockAllIngredientPreparations(items);
		_statusMessage = "Unlocked all ingredient preparation traits and risks.";
	}

	private bool IsPotionRecipeRecordedInBook(string recipeId)
	{
		if (string.IsNullOrWhiteSpace(recipeId))
			return false;

		return _gameState.KnowsPotion(recipeId) ||
			_gameState.KnowsPotion(PotionVariantIdBuilder.BuildPredefinedPotionItemId(recipeId));
	}

	private void SetPotionRecipeRecordedInBook(string recipeId, bool recorded)
	{
		if (string.IsNullOrWhiteSpace(recipeId))
			return;

		var potionItemId = PotionVariantIdBuilder.BuildPredefinedPotionItemId(recipeId);
		if (recorded)
		{
			_gameState.LearnPotion(potionItemId);
			_statusMessage = $"Recorded {BuildBookPotionLabel(recipeId)} in the potion book.";
			return;
		}

		_gameState.ForgetPotion(recipeId);
		_gameState.ForgetPotion(potionItemId);
		_statusMessage = $"Marked {BuildBookPotionLabel(recipeId)} unknown in the potion book.";
	}

	private void SetIngredientRecordedInBook(string ingredientId, bool recorded)
	{
		if (string.IsNullOrWhiteSpace(ingredientId))
			return;

		if (recorded)
		{
			_gameState.LearnIngredient(ingredientId);
			_statusMessage = $"Recorded {BuildItemLabel(ingredientId)} in the ingredient book.";
			return;
		}

		_gameState.ForgetIngredient(ingredientId);
		_statusMessage = $"Marked {BuildItemLabel(ingredientId)} unknown in the ingredient book.";
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

	private void DrawRuntimeCatalogSection()
	{
		if (!ImGui.CollapsingHeader("Runtime Catalog", ImGuiTreeNodeFlags.DefaultOpen))
			return;

		ImGui.TextWrapped("Create or update runtime item definitions while the game is running.");
		ImGui.TextWrapped("Traits and risks format: key=value,key=value");

		ImGui.InputText("Item Id", ref _runtimeItemIdInput, 96);
		ImGui.InputText("Item Name", ref _runtimeItemNameInput, 128);
		ImGui.InputText("Icon Path", ref _runtimeItemIconPathInput, 256);
		ImGui.InputText("Description", ref _runtimeItemDescriptionInput, 256);

		ImGui.InputInt("Base Price", ref _runtimeItemBasePrice);
		if (_runtimeItemBasePrice < 0)
			_runtimeItemBasePrice = 0;

		ImGui.InputInt("Quality", ref _runtimeItemQuality);
		_runtimeItemQuality = Math.Clamp(_runtimeItemQuality, 0, 100);

		ImGui.Checkbox("Tag: ingredient", ref _runtimeItemTagIngredient);
		ImGui.SameLine();
		ImGui.Checkbox("Tag: potion", ref _runtimeItemTagPotion);

		ImGui.InputText("Traits", ref _runtimeItemTraitsInput, 256);
		ImGui.InputText("Risks", ref _runtimeItemRisksInput, 256);
		ImGui.InputInt("Starting Qty", ref _runtimeItemStartingQuantity);
		if (_runtimeItemStartingQuantity < 0)
			_runtimeItemStartingQuantity = 0;

		if (ImGui.Button("Save Runtime Item"))
		{
			if (!TryBuildRuntimeItemFromInput(out var item, out var error))
			{
				_statusMessage = error;
				return;
			}

			if (!_runtimeContentDb.UpsertRuntimeItem(item))
			{
				_statusMessage = $"Failed to save runtime item '{item.Id}'.";
				return;
			}

			if (_runtimeItemStartingQuantity > 0)
				_gameState.AddItem(item.Id, _runtimeItemStartingQuantity);

			RebuildDebugCatalog();
			_statusMessage = _runtimeItemStartingQuantity > 0
				? $"Saved runtime item {BuildItemLabel(item.Id)} and added {_runtimeItemStartingQuantity} to inventory."
				: $"Saved runtime item {BuildItemLabel(item.Id)}.";
		}

		ImGui.SameLine();
		if (ImGui.SmallButton("Reset Runtime Form"))
			ResetRuntimeItemForm();
	}

	private bool TryBuildRuntimeItemFromInput(out ItemDef item, out string error)
	{
		item = new ItemDef();
		error = string.Empty;

		var itemId = (_runtimeItemIdInput ?? string.Empty).Trim();
		if (string.IsNullOrWhiteSpace(itemId))
		{
			error = "Runtime item Id is required.";
			return false;
		}

		if (!_runtimeItemTagIngredient && !_runtimeItemTagPotion)
		{
			error = "Select at least one tag: ingredient or potion.";
			return false;
		}

		if (!TryParseStatMap(_runtimeItemTraitsInput, out var traits, out var traitError))
		{
			error = $"Traits error: {traitError}";
			return false;
		}

		if (!TryParseStatMap(_runtimeItemRisksInput, out var risks, out var riskError))
		{
			error = $"Risks error: {riskError}";
			return false;
		}

		var tags = new List<string>();
		if (_runtimeItemTagIngredient)
			tags.Add("ingredient");
		if (_runtimeItemTagPotion)
			tags.Add("potion");

		item = new ItemDef
		{
			Id = itemId,
			Name = string.IsNullOrWhiteSpace(_runtimeItemNameInput) ? itemId : _runtimeItemNameInput.Trim(),
			IconPath = string.IsNullOrWhiteSpace(_runtimeItemIconPathInput) ? null : _runtimeItemIconPathInput.Trim(),
			Description = (_runtimeItemDescriptionInput ?? string.Empty).Trim(),
			BasePrice = Math.Max(0, _runtimeItemBasePrice),
			Quality = Math.Clamp(_runtimeItemQuality, 0, 100),
			Tags = tags,
			Traits = traits,
			Risks = risks
		};

		return true;
	}

	private static bool TryParseStatMap(string input, out Dictionary<string, int> values, out string error)
	{
		values = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		error = string.Empty;

		if (string.IsNullOrWhiteSpace(input))
			return true;

		var entries = input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		foreach (var entry in entries)
		{
			var keyValue = entry.Split('=', 2, StringSplitOptions.TrimEntries);
			if (keyValue.Length != 2 || string.IsNullOrWhiteSpace(keyValue[0]))
			{
				error = $"Invalid entry '{entry}'. Use key=value.";
				return false;
			}

			if (!int.TryParse(keyValue[1], out var amount))
			{
				error = $"Invalid number '{keyValue[1]}' for key '{keyValue[0]}'.";
				return false;
			}

			values[keyValue[0]] = amount;
		}

		return true;
	}

	private void ResetRuntimeItemForm()
	{
		_runtimeItemIdInput = string.Empty;
		_runtimeItemNameInput = string.Empty;
		_runtimeItemIconPathInput = string.Empty;
		_runtimeItemDescriptionInput = string.Empty;
		_runtimeItemTraitsInput = string.Empty;
		_runtimeItemRisksInput = string.Empty;
		_runtimeItemBasePrice = 10;
		_runtimeItemQuality = 50;
		_runtimeItemStartingQuantity = 1;
		_runtimeItemTagIngredient = true;
		_runtimeItemTagPotion = false;
	}

	private void OnRuntimeContentChanged()
	{
		RebuildDebugCatalog();
	}

	private bool TryCloseShopDay()
	{
		if (_dayController is null)
		{
			_statusMessage = "DayController is unavailable, so the shop day cannot be closed.";
			return false;
		}

		var applied = _dayController.TryCloseShopDayFromDebug();

		if (!applied)
		{
			_statusMessage = "The shop is already closed.";
			return false;
		}

		_statusMessage = "Shop closed and day summary shown.";
		return true;
	}

	private void RebuildDebugCatalog()
	{
		_potionItemIds.Clear();
		_consumableItemIds.Clear();
		_ingredientItemIds.Clear();
		_bookPotionRecipeIds.Clear();
		_bookIngredientItemIds.Clear();
		_traitNames.Clear();
		_traitToItemIds.Clear();
		_itemDisplayNames.Clear();
		_bookPotionDisplayNames.Clear();

		foreach (var recipe in _dataDb.PotionRecipes)
		{
			if (recipe is null || string.IsNullOrWhiteSpace(recipe.Id))
				continue;

			_bookPotionRecipeIds.Add(recipe.Id);
			_bookPotionDisplayNames[recipe.Id] = string.IsNullOrWhiteSpace(recipe.Name) ? recipe.Id : recipe.Name;
		}

		foreach (var pair in _dataDb.Items)
		{
			var item = pair.Value;
			if (!IsBookIngredient(item))
				continue;

			_bookIngredientItemIds.Add(item.Id);
		}

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
			var isConsumable = HasTag(item, "consumable");
			if (isPotion)
				_potionItemIds.Add(item.Id);
			if (isConsumable)
				_consumableItemIds.Add(item.Id);
			if (isIngredient)
				_ingredientItemIds.Add(item.Id);

			if (!isPotion && !isIngredient && !isConsumable)
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
		_consumableItemIds.Sort((a, b) => string.Compare(BuildItemLabel(a), BuildItemLabel(b), StringComparison.OrdinalIgnoreCase));
		_ingredientItemIds.Sort((a, b) => string.Compare(BuildItemLabel(a), BuildItemLabel(b), StringComparison.OrdinalIgnoreCase));
		_bookPotionRecipeIds.Sort((a, b) => string.Compare(BuildBookPotionLabel(a), BuildBookPotionLabel(b), StringComparison.OrdinalIgnoreCase));
		_bookIngredientItemIds.Sort((a, b) => string.Compare(BuildItemLabel(a), BuildItemLabel(b), StringComparison.OrdinalIgnoreCase));
		_traitNames.Sort((a, b) => string.Compare(a, b, StringComparison.OrdinalIgnoreCase));

		foreach (var pair in _traitToItemIds)
			pair.Value.Sort((a, b) => string.Compare(BuildItemLabel(a), BuildItemLabel(b), StringComparison.OrdinalIgnoreCase));

		_selectedPotionIndex = ClampIndex(_selectedPotionIndex, _potionItemIds.Count);
		_selectedConsumableIndex = ClampIndex(_selectedConsumableIndex, _consumableItemIds.Count);
		_selectedTraitIndex = ClampIndex(_selectedTraitIndex, _traitNames.Count);
		_selectedBookPotionIndex = ClampIndex(_selectedBookPotionIndex, _bookPotionRecipeIds.Count);
		_selectedBookIngredientIndex = ClampIndex(_selectedBookIngredientIndex, _bookIngredientItemIds.Count);
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

	private List<(string ItemId, int Quantity)> BuildConsumableInventorySnapshot()
	{
		var consumables = new List<(string ItemId, int Quantity)>();
		foreach (var stack in _gameState.Inventory)
		{
			if (stack.Value <= 0)
				continue;
			if (!IsConsumableId(stack.Key))
				continue;
			consumables.Add((stack.Key, stack.Value));
		}

		consumables.Sort((a, b) => string.Compare(BuildItemLabel(a.ItemId), BuildItemLabel(b.ItemId), StringComparison.OrdinalIgnoreCase));
		return consumables;
	}

	private bool IsPotionId(string itemId)
	{
		if (!ItemCatalog.TryGetItem(itemId, out var item))
			return false;

		return HasTag(item, "potion");
	}

	private bool IsConsumableId(string itemId)
	{
		if (!ItemCatalog.TryGetItem(itemId, out var item))
			return false;

		return HasTag(item, "consumable");
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

	private static bool IsBookIngredient(ItemDef? item)
	{
		if (item is null || string.IsNullOrWhiteSpace(item.Id) || item.Treatment is not null)
			return false;

		return HasTag(item, "ingredient");
	}

	private static bool IsBaseIngredient(ItemDef? item)
	{
		if (item is null || string.IsNullOrWhiteSpace(item.Id) || item.Treatment is not null || item.PreparedIngredient is not null)
			return false;

		return HasTag(item, ItemTags.Ingredient);
	}

	private string BuildItemLabel(string itemId)
	{
		if (_itemDisplayNames.TryGetValue(itemId, out var name))
			return $"{name} ({itemId})";

		return itemId;
	}

	private string BuildBookPotionLabel(string recipeId)
	{
		if (_bookPotionDisplayNames.TryGetValue(recipeId, out var name))
			return $"{name} ({recipeId})";

		return recipeId;
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
