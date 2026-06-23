using Godot;
using OccultShop.Autoload;
using OccultShop.Systems;

namespace OccultShop.UI;

public partial class TreatmentTray : Control
{
	[Export] public NodePath TrayDropBoxPath = default!;
	[Export] public NodePath HelperLabelPath = default!;
	[Export] public NodePath ApplyButtonPath = default!;
	[Export] public NodePath ClearButtonPath = default!;
	[Export] public NodePath RuntimeContentDbPath = new(AutoloadNodePaths.RuntimeContentDb);
	[Export] public NodePath GameStatePath = new(AutoloadNodePaths.GameState);
	[Export] public NodePath ItemCatalogPath = new(AutoloadNodePaths.ItemCatalog);

	private BrewDropBox _trayDropBox = default!;
	private Label _helperLabel = default!;
	private Button _applyButton = default!;
	private Button _clearButton = default!;
	private GameState _gameState = default!;
	private ItemCatalogService _itemCatalog = default!;
	private TreatmentService _treatmentService = default!;
	private string _selectedConsumableId = string.Empty;
	private string _selectedTargetId = string.Empty;

	public override void _Ready()
	{
		var gameState = GetNodeOrNull<GameState>(GameStatePath);
		if (gameState is null)
		{
			GD.PushError($"TreatmentTray: GameState was not found at '{GameStatePath}'.");
			return;
		}

		var itemCatalog = GetNodeOrNull<ItemCatalogService>(ItemCatalogPath);
		if (itemCatalog is null)
		{
			GD.PushError($"TreatmentTray: ItemCatalog was not found at '{ItemCatalogPath}'.");
			return;
		}

		var runtimeContentDb = GetNodeOrNull<RuntimeContentDb>(RuntimeContentDbPath);
		if (runtimeContentDb is null)
		{
			GD.PushError($"TreatmentTray: RuntimeContentDb was not found at '{RuntimeContentDbPath}'.");
			return;
		}

		_gameState = gameState;
		_itemCatalog = itemCatalog;
		_treatmentService = new TreatmentService(_gameState, _itemCatalog, runtimeContentDb);

		_trayDropBox = GetNode<BrewDropBox>(TrayDropBoxPath);
		_helperLabel = GetNode<Label>(HelperLabelPath);
		_applyButton = GetNode<Button>(ApplyButtonPath);
		_clearButton = GetNode<Button>(ClearButtonPath);

		_trayDropBox.ItemDropped += OnTrayItemDropped;
		_applyButton.Pressed += TryApplyTreatment;
		_clearButton.Pressed += ClearStagedItems;
		_gameState.Changed += OnGameStateChanged;

		Visible = false;
		Refresh();
	}

	public override void _ExitTree()
	{
		if (_trayDropBox is not null)
			_trayDropBox.ItemDropped -= OnTrayItemDropped;
		if (_applyButton is not null)
			_applyButton.Pressed -= TryApplyTreatment;
		if (_clearButton is not null)
			_clearButton.Pressed -= ClearStagedItems;
		if (_gameState is not null)
			_gameState.Changed -= OnGameStateChanged;
	}

	public void ClearStagedItems()
	{
		ReturnSelectedItems();
		_selectedConsumableId = string.Empty;
		_selectedTargetId = string.Empty;
		Refresh();
	}

	private void OnTrayItemDropped(string itemId)
	{
		if (TryReserveDroppedItem(itemId))
			Refresh();
	}

	private bool TryReserveDroppedItem(string itemId)
	{
		if (!_itemCatalog.IsConsumable(itemId))
		{
			if (_itemCatalog.IsIngredient(itemId) || _itemCatalog.IsPotion(itemId))
				return ReserveTargetItem(itemId);

			SetStatusText("Drop a consumable, ingredient, or potion.");
			return false;
		}

		return ReserveConsumableItem(itemId);
	}

	private bool ReserveConsumableItem(string itemId)
	{
		return ReserveSlotItem(itemId, ref _selectedConsumableId, "Could not take that consumable.");
	}

	private bool ReserveTargetItem(string itemId)
	{
		if (!_itemCatalog.IsIngredient(itemId) && !_itemCatalog.IsPotion(itemId))
		{
			SetStatusText("Drop an ingredient or potion to treat.");
			return false;
		}

		return ReserveSlotItem(itemId, ref _selectedTargetId, "Could not take that item.");
	}

	private void TryApplyTreatment()
	{
		if (!_treatmentService.TryApplyReservedTreatment(_selectedConsumableId, _selectedTargetId, out _, out var error))
		{
			SetStatusText(error);
			UpdateApplyState();
			return;
		}

		_selectedConsumableId = string.Empty;
		_selectedTargetId = string.Empty;
		Refresh();
	}

	private void OnGameStateChanged()
	{
		Refresh();
	}

	private bool ReserveSlotItem(string itemId, ref string selectedItemId, string failureText)
	{
		if (string.Equals(selectedItemId, itemId, System.StringComparison.OrdinalIgnoreCase))
			return true;

		if (!_gameState.HasItem(itemId, 1))
		{
			SetStatusText("Not enough stock for that item.");
			return false;
		}

		var previousItemId = selectedItemId;
		if (!_gameState.ConsumeItem(itemId, 1))
		{
			SetStatusText(failureText);
			return false;
		}

		if (!string.IsNullOrWhiteSpace(previousItemId))
			_gameState.AddItem(previousItemId, 1);

		selectedItemId = itemId;
		return true;
	}

	private void ReturnSelectedItems()
	{
		if (!string.IsNullOrWhiteSpace(_selectedConsumableId))
			_gameState.AddItem(_selectedConsumableId, 1);
		if (!string.IsNullOrWhiteSpace(_selectedTargetId))
			_gameState.AddItem(_selectedTargetId, 1);
	}

	private void Refresh()
	{
		UpdateApplyState();
	}

	private void UpdateApplyState()
	{
		if (_applyButton is null || _clearButton is null)
			return;

		var hasConsumable = !string.IsNullOrWhiteSpace(_selectedConsumableId);
		var hasTarget = !string.IsNullOrWhiteSpace(_selectedTargetId);
		var hasAnySelection = hasConsumable || hasTarget;

		_clearButton.Visible = hasAnySelection;
		_clearButton.Disabled = !hasAnySelection;

		if (!hasConsumable || !hasTarget)
		{
			var prompt = BuildWaitingPrompt(hasConsumable, hasTarget);
			_applyButton.Visible = false;
			_applyButton.Disabled = true;
			_applyButton.TooltipText = prompt;
			SetStatusText(prompt);
			return;
		}

		_applyButton.Visible = true;
		var canApply = _treatmentService.CanApplyReservedTreatment(_selectedConsumableId, _selectedTargetId, out var error);
		_applyButton.Disabled = !canApply;
		_applyButton.TooltipText = canApply ? "Apply this treatment." : error;

		if (!string.IsNullOrWhiteSpace(error))
			SetStatusText(error);
		else if (canApply)
			SetStatusText("Ready to apply treatment.");
		else
			SetStatusText("Drop a consumable and an item onto the tray.");
	}

	private string BuildWaitingPrompt(bool hasConsumable, bool hasTarget)
	{
		if (!hasConsumable && !hasTarget)
			return "Drop a consumable and an item onto the tray.";

		if (hasConsumable)
			return $"{ItemName(_selectedConsumableId)} placed. Drop an ingredient or potion, or clear the tray.";

		return $"{ItemName(_selectedTargetId)} placed. Drop a consumable, or clear the tray.";
	}

	private void SetStatusText(string text)
	{
		if (_helperLabel is not null)
			_helperLabel.Text = text;
	}

	private string ItemName(string itemId)
	{
		if (_itemCatalog.IsPotion(itemId))
		{
			var displayName = _gameState.GetPotionDisplayName(itemId);
			if (!string.IsNullOrWhiteSpace(displayName))
				return displayName;
		}

		return _itemCatalog.GetItemName(itemId);
	}
}
