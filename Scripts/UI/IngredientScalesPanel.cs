using Godot;
using System.Collections.Generic;
using System.Text;
using OccultShop.Autoload;

namespace OccultShop.UI;

public partial class IngredientScalesPanel : Control
{
	private const string DefaultIngredientText = "Drop ingredient";
	private const string DefaultStatusText = "Weigh an ingredient.";

	[Export] public NodePath IngredientDropBoxPath = default!;
	[Export] public NodePath IngredientIconPath = default!;
	[Export] public NodePath IngredientNamePath = default!;
	[Export] public NodePath AvailableWeightsContainerPath = default!;
	[Export] public NodePath WeightTotalLabelPath = default!;
	[Export] public NodePath StatusLabelPath = default!;
	[Export] public NodePath ConfirmButtonPath = default!;
	[Export] public NodePath ClearButtonPath = default!;
	[Export] public NodePath BrewPanelPath = default!;
	[Export] public NodePath GameStatePath = new(AutoloadNodePaths.GameState);
	[Export] public NodePath ItemCatalogPath = new(AutoloadNodePaths.ItemCatalog);

	private ScalesDropBox _ingredientDropBox = default!;
	private TextureRect _ingredientIcon = default!;
	private Label _ingredientName = default!;
	private Node _availableWeightsContainer = default!;
	private Label _weightTotalLabel = default!;
	private Label _statusLabel = default!;
	private Button _confirmButton = default!;
	private Button _clearButton = default!;
	private BrewPanel _brewPanel = default!;
	private GameState _gameState = default!;
	private ItemCatalogService _itemCatalog = default!;
	private string _selectedIngredientId = string.Empty;
	private readonly List<int> _selectedWeights = new();

	public override void _Ready()
	{
		var itemCatalog = GetNodeOrNull<ItemCatalogService>(ItemCatalogPath);
		if (itemCatalog is null)
		{
			GD.PushError($"IngredientScalesPanel: ItemCatalog was not found at '{ItemCatalogPath}'.");
			return;
		}

		var brewPanel = GetNodeOrNull<BrewPanel>(BrewPanelPath);
		if (brewPanel is null)
		{
			GD.PushError($"IngredientScalesPanel: BrewPanel was not found at '{BrewPanelPath}'.");
			return;
		}

		var gameState = GetNodeOrNull<GameState>(GameStatePath);
		if (gameState is null)
		{
			GD.PushError($"IngredientScalesPanel: GameState was not found at '{GameStatePath}'.");
			return;
		}

		_itemCatalog = itemCatalog;
		_brewPanel = brewPanel;
		_gameState = gameState;
		_ingredientDropBox = GetNode<ScalesDropBox>(IngredientDropBoxPath);
		_ingredientIcon = GetNode<TextureRect>(IngredientIconPath);
		_ingredientName = GetNode<Label>(IngredientNamePath);
		_availableWeightsContainer = GetNode<Node>(AvailableWeightsContainerPath);
		_weightTotalLabel = GetNode<Label>(WeightTotalLabelPath);
		_statusLabel = GetNode<Label>(StatusLabelPath);
		_confirmButton = GetNode<Button>(ConfirmButtonPath);
		_clearButton = GetNode<Button>(ClearButtonPath);

		_ingredientDropBox.ItemDropped += OnIngredientDropped;
		_confirmButton.Pressed += ConfirmMeasuredIngredient;
		_clearButton.Pressed += ResetScaleToDefault;
		ConnectWeightButtons();

		MouseFilter = MouseFilterEnum.Stop;
		Refresh();
	}

	public override void _ExitTree()
	{
		if (_ingredientDropBox is not null)
			_ingredientDropBox.ItemDropped -= OnIngredientDropped;
		if (_confirmButton is not null)
			_confirmButton.Pressed -= ConfirmMeasuredIngredient;
		if (_clearButton is not null)
			_clearButton.Pressed -= ResetScaleToDefault;
		DisconnectWeightButtons();
	}

	public override bool _CanDropData(Vector2 atPosition, Variant data)
	{
		if (data.VariantType != Variant.Type.String)
			return false;

		var value = data.AsString();
		if (ScaleWeightButton.TryParseDragData(value, out _))
			return true;

		return _itemCatalog is not null && _itemCatalog.IsPreparedIngredient(value);
	}

	public override void _DropData(Vector2 atPosition, Variant data)
	{
		if (data.VariantType != Variant.Type.String)
			return;

		var value = data.AsString();
		if (ScaleWeightButton.TryParseDragData(value, out var grams))
		{
			OnWeightDropped(grams);
			return;
		}

		OnIngredientDropped(value);
	}

	private void OnIngredientDropped(string itemId)
	{
		if (!_itemCatalog.TryGetItem(itemId, out var item))
		{
			_statusLabel.Text = "That item is not recognized.";
			return;
		}

		if (!_itemCatalog.IsIngredient(itemId))
		{
			_statusLabel.Text = "The scales only accept ingredients.";
			return;
		}

		if (!_itemCatalog.IsPreparedIngredient(itemId))
		{
			_statusLabel.Text = "Prepare this ingredient before weighing it.";
			return;
		}

		if (!ReserveSelectedIngredient(itemId))
			return;

		_selectedIngredientId = itemId;
		_ingredientIcon.Texture = UiIconLoader.LoadIcon(item.IconPath);
		_statusLabel.Text = "Add weights, then confirm.";
		Refresh();
	}

	private bool ReserveSelectedIngredient(string itemId)
	{
		if (string.Equals(_selectedIngredientId, itemId, System.StringComparison.OrdinalIgnoreCase))
			return true;

		if (!_gameState.HasItem(itemId, 1))
		{
			_statusLabel.Text = "Not enough stock for that ingredient.";
			return false;
		}

		if (!_gameState.ConsumeItem(itemId, 1))
		{
			_statusLabel.Text = "Could not take that ingredient.";
			return false;
		}

		ReturnSelectedIngredient();
		return true;
	}

	private void OnWeightDropped(int grams)
	{
		if (grams <= 0)
			return;

		_selectedWeights.Add(grams);
		_statusLabel.Text = $"{GetTotalGrams()}g measured.";
		Refresh();
	}

	private void ConnectWeightButtons()
	{
		foreach (var child in _availableWeightsContainer.GetChildren())
		{
			if (child is ScaleWeightButton weightButton)
				weightButton.WeightActivated += OnWeightDropped;
		}
	}

	private void DisconnectWeightButtons()
	{
		if (_availableWeightsContainer is null)
			return;

		foreach (var child in _availableWeightsContainer.GetChildren())
		{
			if (child is ScaleWeightButton weightButton)
				weightButton.WeightActivated -= OnWeightDropped;
		}
	}

	private void ConfirmMeasuredIngredient()
	{
		var totalGrams = GetTotalGrams();
		if (string.IsNullOrWhiteSpace(_selectedIngredientId))
		{
			_statusLabel.Text = "Choose an ingredient.";
			UpdateConfirmState();
			return;
		}

		if (totalGrams <= 0)
		{
			_statusLabel.Text = "Add at least one weight.";
			UpdateConfirmState();
			return;
		}

		if (!_brewPanel.TryQueueReservedMeasuredIngredient(_selectedIngredientId, totalGrams))
		{
			_statusLabel.Text = "Could not add measured ingredient.";
			UpdateConfirmState();
			return;
		}

		ResetScaleToDefault(returnIngredient: false);
	}

	private void ResetScaleToDefault()
	{
		ResetScaleToDefault(returnIngredient: true);
	}

	private void ResetScaleToDefault(bool returnIngredient)
	{
		if (returnIngredient)
			ReturnSelectedIngredient();

		_selectedIngredientId = string.Empty;
		_selectedWeights.Clear();
		_ingredientIcon.Texture = null;
		_ingredientName.Text = DefaultIngredientText;
		_weightTotalLabel.Text = "0g";
		_weightTotalLabel.TooltipText = "No weights added.";
		_statusLabel.Text = DefaultStatusText;
		UpdateConfirmState();
	}

	private void ReturnSelectedIngredient()
	{
		if (!string.IsNullOrWhiteSpace(_selectedIngredientId))
			_gameState.AddItem(_selectedIngredientId, 1);
	}

	private int GetTotalGrams()
	{
		var total = 0;
		foreach (var grams in _selectedWeights)
			total += grams;
		return total;
	}

	private void Refresh()
	{
		if (string.IsNullOrWhiteSpace(_selectedIngredientId) ||
			!_itemCatalog.TryGetItem(_selectedIngredientId, out var item))
		{
			_ingredientIcon.Texture = null;
			_ingredientName.Text = DefaultIngredientText;
		}
		else
		{
			_ingredientIcon.Texture = UiIconLoader.LoadIcon(item.IconPath);
			_ingredientName.Text = item.Name;
		}

		var totalGrams = GetTotalGrams();
		var totalText = totalGrams > 0 ? $"{totalGrams}g" : "0g";
		_weightTotalLabel.Text = totalText;
		if (_selectedWeights.Count > 0)
			_weightTotalLabel.TooltipText = BuildWeightTooltip();
		else
			_weightTotalLabel.TooltipText = "No weights added.";

		UpdateConfirmState();
	}

	private string BuildWeightTooltip()
	{
		var tooltip = new StringBuilder();
		for (var i = 0; i < _selectedWeights.Count; i++)
		{
			if (i > 0)
				tooltip.Append(" + ");
			tooltip.Append(_selectedWeights[i]);
			tooltip.Append('g');
		}

		return tooltip.ToString();
	}

	private void UpdateConfirmState()
	{
		if (_confirmButton is null)
			return;

		var canConfirm = !string.IsNullOrWhiteSpace(_selectedIngredientId) && GetTotalGrams() > 0;
		_confirmButton.Disabled = !canConfirm;
		_confirmButton.TooltipText = canConfirm
			? "Add measured ingredient to the brew."
			: "Drop an ingredient and at least one weight.";
	}

}
