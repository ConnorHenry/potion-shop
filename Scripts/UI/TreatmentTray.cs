using Godot;
using OccultShop.Autoload;
using OccultShop.Systems;

namespace OccultShop.UI;

public partial class TreatmentTray : Control
{
	[Export] public NodePath ConsumableDropBoxPath = default!;
	[Export] public NodePath TargetDropBoxPath = default!;
	[Export] public NodePath ConsumableIconPath = default!;
	[Export] public NodePath TargetIconPath = default!;
	[Export] public NodePath ConsumableNamePath = default!;
	[Export] public NodePath TargetNamePath = default!;
	[Export] public NodePath StatusLabelPath = default!;
	[Export] public NodePath ApplyButtonPath = default!;
	[Export] public NodePath ClearButtonPath = default!;
	[Export] public Vector2 TraySize { get; set; } = new(430.0f, 286.0f);
	[Export] public NodePath RuntimeContentDbPath = new("/root/RuntimeContentDb");
	[Export] public NodePath GameStatePath = new("/root/GameState");
	[Export] public NodePath ItemCatalogPath = new("/root/ItemCatalog");

	private BrewDropBox _consumableDropBox = default!;
	private BrewDropBox _targetDropBox = default!;
	private TextureRect _consumableIcon = default!;
	private TextureRect _targetIcon = default!;
	private Label _consumableName = default!;
	private Label _targetName = default!;
	private Label _statusLabel = default!;
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

		_consumableDropBox = GetNode<BrewDropBox>(ConsumableDropBoxPath);
		_targetDropBox = GetNode<BrewDropBox>(TargetDropBoxPath);
		_consumableIcon = GetNode<TextureRect>(ConsumableIconPath);
		_targetIcon = GetNode<TextureRect>(TargetIconPath);
		_consumableName = GetNode<Label>(ConsumableNamePath);
		_targetName = GetNode<Label>(TargetNamePath);
		_statusLabel = GetNode<Label>(StatusLabelPath);
		_applyButton = GetNode<Button>(ApplyButtonPath);
		_clearButton = GetNode<Button>(ClearButtonPath);

		_consumableDropBox.ItemDropped += OnConsumableDropped;
		_targetDropBox.ItemDropped += OnTargetDropped;
		_applyButton.Pressed += TryApplyTreatment;
		_clearButton.Pressed += ClearSelections;
		_gameState.Changed += OnGameStateChanged;

		Visible = false;
		Refresh();
	}

	public override void _ExitTree()
	{
		if (_consumableDropBox is not null)
			_consumableDropBox.ItemDropped -= OnConsumableDropped;
		if (_targetDropBox is not null)
			_targetDropBox.ItemDropped -= OnTargetDropped;
		if (_applyButton is not null)
			_applyButton.Pressed -= TryApplyTreatment;
		if (_clearButton is not null)
			_clearButton.Pressed -= ClearSelections;
		if (_gameState is not null)
			_gameState.Changed -= OnGameStateChanged;
	}

	public void Toggle()
	{
		if (Visible)
		{
			HidePanel();
			return;
		}

		ShowPanel();
	}

	public void ShowPanel()
	{
		ApplyTrayGeometry();
		Visible = true;
		MoveToFront();
		Refresh();
	}

	public void HidePanel()
	{
		ClearSelections();
		Visible = false;
	}

	private void OnConsumableDropped(string itemId)
	{
		if (!_itemCatalog.IsConsumable(itemId))
		{
			_statusLabel.Text = "First slot only accepts consumables.";
			return;
		}

		if (!ReserveSlotItem(itemId, ref _selectedConsumableId, "Could not take that consumable."))
			return;

		Refresh();
	}

	private void OnTargetDropped(string itemId)
	{
		if (_itemCatalog.IsConsumable(itemId))
		{
			_statusLabel.Text = "Second slot accepts ingredients or potions.";
			return;
		}

		if (!_itemCatalog.IsIngredient(itemId) && !_itemCatalog.IsPotion(itemId))
		{
			_statusLabel.Text = "Second slot only accepts ingredients or potions.";
			return;
		}

		if (!ReserveSlotItem(itemId, ref _selectedTargetId, "Could not take that item."))
			return;

		Refresh();
	}

	private void TryApplyTreatment()
	{
		if (!_treatmentService.TryApplyReservedTreatment(_selectedConsumableId, _selectedTargetId, out var treatedItemId, out var error))
		{
			_statusLabel.Text = error;
			UpdateApplyState();
			return;
		}

		var treatedName = ItemName(treatedItemId);
		_selectedConsumableId = string.Empty;
		_selectedTargetId = string.Empty;
		Refresh();
		_statusLabel.Text = $"Created {treatedName}.";
	}

	private void ClearSelections()
	{
		ReturnSelectedItems();
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
			_statusLabel.Text = "Not enough stock for that item.";
			return false;
		}

		var previousItemId = selectedItemId;
		if (!_gameState.ConsumeItem(itemId, 1))
		{
			_statusLabel.Text = failureText;
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
		RefreshSlot(_selectedConsumableId, _consumableIcon, _consumableName, "Drop consumable");
		RefreshSlot(_selectedTargetId, _targetIcon, _targetName, "Drop item");
		UpdateApplyState();
	}

	private void ApplyTrayGeometry()
	{
		var normalizedTraySize = new Vector2(
			Mathf.Max(360.0f, TraySize.X),
			Mathf.Max(240.0f, TraySize.Y));
		CustomMinimumSize = normalizedTraySize;
		Size = normalizedTraySize;

		var panel = GetNodeOrNull<Control>("Panel");
		if (panel is null)
		{
			GD.PushError("TreatmentTray: Panel node is missing.");
			return;
		}

		panel.AnchorLeft = 0.0f;
		panel.AnchorTop = 0.0f;
		panel.AnchorRight = 0.0f;
		panel.AnchorBottom = 0.0f;
		panel.Position = Vector2.Zero;
		panel.CustomMinimumSize = normalizedTraySize;
		panel.Size = normalizedTraySize;
	}

	private void RefreshSlot(string itemId, TextureRect icon, Label label, string emptyText)
	{
		if (string.IsNullOrWhiteSpace(itemId) || !_itemCatalog.TryGetItem(itemId, out var item))
		{
			icon.Texture = null;
			label.Text = emptyText;
			return;
		}

		icon.Texture = UiIconLoader.LoadIcon(item.IconPath);
		label.Text = ItemName(itemId);
	}

	private void UpdateApplyState()
	{
		if (_applyButton is null)
			return;

		var canApply = _treatmentService.CanApplyReservedTreatment(_selectedConsumableId, _selectedTargetId, out var error);
		_applyButton.Disabled = !canApply;
		_applyButton.TooltipText = canApply ? "Apply this treatment." : error;

		if (!string.IsNullOrWhiteSpace(error))
			_statusLabel.Text = error;
		else if (canApply)
			_statusLabel.Text = "Ready to apply treatment.";
		else
			_statusLabel.Text = "Choose a consumable and an item.";
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
