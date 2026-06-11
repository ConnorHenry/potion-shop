using Godot;
using OccultShop.Autoload;
using OccultShop.Models;
using OccultShop.Systems;

namespace OccultShop.UI;

public partial class StationItemDetailPanel : Control
{
	[Export] public NodePath IconPath = new("Panel/Margin/VBox/Header/Icon");
	[Export] public NodePath NamePath = new("Panel/Margin/VBox/Header/NameBlock/Name");
	[Export] public NodePath TypeTagPath = new("Panel/Margin/VBox/Header/NameBlock/Type");
	[Export] public NodePath OwnedPath = new("Panel/Margin/VBox/Meta/Owned");
	[Export] public NodePath PricePath = new("Panel/Margin/VBox/Meta/Price");
	[Export] public NodePath TraitsHeaderPath = new("Panel/Margin/VBox/Stats/TraitsColumn/Header");
	[Export] public NodePath TraitsPath = new("Panel/Margin/VBox/Stats/TraitsColumn/Values");
	[Export] public NodePath RisksHeaderPath = new("Panel/Margin/VBox/Stats/RisksColumn/Header");
	[Export] public NodePath RisksPath = new("Panel/Margin/VBox/Stats/RisksColumn/Values");
	[Export] public NodePath DescriptionPath = new("Panel/Margin/VBox/Description");
	[Export] public NodePath CloseButtonPath = new("Panel/Margin/VBox/Actions/Close");
	[Export] public NodePath GameStatePath = new(AutoloadNodePaths.GameState);
	[Export] public NodePath ItemCatalogPath = new(AutoloadNodePaths.ItemCatalog);

	private TextureRect _icon = default!;
	private Label _name = default!;
	private Label _typeTag = default!;
	private Label _owned = default!;
	private Label _price = default!;
	private Label _traitsHeader = default!;
	private RichTextLabel _traits = default!;
	private Label _risksHeader = default!;
	private RichTextLabel _risks = default!;
	private RichTextLabel _description = default!;
	private Button _closeButton = default!;
	private GameState _gameState = default!;
	private ItemCatalogService _itemCatalog = default!;
	private PotionInventoryBrewService _brewService = default!;
	private string? _currentItemId;
	private bool _dragging;
	private Vector2 _dragOffset;

	public override void _Ready()
	{
		var gameState = GetNodeOrNull<GameState>(GameStatePath);
		if (gameState is null)
		{
			GD.PushError($"StationItemDetailPanel: GameState was not found at '{GameStatePath}'.");
			return;
		}

		var itemCatalog = GetNodeOrNull<ItemCatalogService>(ItemCatalogPath);
		if (itemCatalog is null)
		{
			GD.PushError($"StationItemDetailPanel: ItemCatalog was not found at '{ItemCatalogPath}'.");
			return;
		}

		_gameState = gameState;
		_itemCatalog = itemCatalog;
		_brewService = new PotionInventoryBrewService(_gameState, _itemCatalog);
		_icon = GetNode<TextureRect>(IconPath);
		_name = GetNode<Label>(NamePath);
		_typeTag = GetNode<Label>(TypeTagPath);
		_owned = GetNode<Label>(OwnedPath);
		_price = GetNode<Label>(PricePath);
		_traitsHeader = GetNode<Label>(TraitsHeaderPath);
		_traits = GetNode<RichTextLabel>(TraitsPath);
		_risksHeader = GetNode<Label>(RisksHeaderPath);
		_risks = GetNode<RichTextLabel>(RisksPath);
		_description = GetNode<RichTextLabel>(DescriptionPath);
		_closeButton = GetNode<Button>(CloseButtonPath);
		_traits.BbcodeEnabled = true;
		_risks.BbcodeEnabled = true;
		_description.BbcodeEnabled = true;
		_closeButton.Pressed += HidePanel;
		_gameState.Changed += RefreshCurrentItemDetail;

		MouseFilter = MouseFilterEnum.Stop;
		SetProcessInput(true);
		Visible = false;
	}

	public override void _ExitTree()
	{
		if (_closeButton is not null)
			_closeButton.Pressed -= HidePanel;
		if (_gameState is not null)
			_gameState.Changed -= RefreshCurrentItemDetail;
	}

	public override void _Input(InputEvent @event)
	{
		if (!IsVisibleInTree())
			return;

		if (@event is InputEventMouseButton mouseButton && mouseButton.ButtonIndex == MouseButton.Left)
		{
			if (mouseButton.Pressed)
			{
				if (!GetGlobalRect().HasPoint(mouseButton.GlobalPosition))
				{
					HidePanel();
					AcceptEvent();
					return;
				}
				if (IsPressOnInteractiveChildControl())
					return;

				_dragging = true;
				_dragOffset = mouseButton.GlobalPosition - GlobalPosition;
				MoveToFront();
				AcceptEvent();
				return;
			}

			if (!_dragging)
				return;

			_dragging = false;
			AcceptEvent();
			return;
		}

		if (_dragging && @event is InputEventMouseMotion mouseMotion)
		{
			GlobalPosition = mouseMotion.GlobalPosition - _dragOffset;
			AcceptEvent();
		}
	}

	public void ShowItem(string itemId)
	{
		if (string.IsNullOrWhiteSpace(itemId))
			return;

		if (Visible && string.Equals(_currentItemId, itemId, System.StringComparison.OrdinalIgnoreCase))
		{
			HidePanel();
			return;
		}

		if (_itemCatalog is null || !_itemCatalog.TryGetItem(itemId, out _))
		{
			GD.PushError($"StationItemDetailPanel: Item '{itemId}' was not found in the item catalog.");
			return;
		}

		_currentItemId = itemId;
		RefreshCurrentItemDetail();
		Visible = true;
		MoveToFront();
	}

	public void HidePanel()
	{
		_dragging = false;
		_currentItemId = null;
		ClearPanel();
		Visible = false;
	}

	private bool IsPressOnInteractiveChildControl()
	{
		var hoveredControl = GetViewport().GuiGetHoveredControl();
		if (hoveredControl is null)
			return false;
		if (hoveredControl == this)
			return false;
		if (!IsAncestorOf(hoveredControl))
			return false;

		return hoveredControl is BaseButton;
	}

	private void RefreshCurrentItemDetail()
	{
		if (string.IsNullOrWhiteSpace(_currentItemId))
			return;
		if (_gameState is null || _itemCatalog is null)
			return;
		if (!_itemCatalog.TryGetItem(_currentItemId, out var item))
		{
			HidePanel();
			return;
		}
		if (!_gameState.HasItem(_currentItemId, 1))
		{
			HidePanel();
			return;
		}

		_icon.Texture = UiIconLoader.LoadIcon(item.IconPath);
		_name.Text = InventoryItemTextFormatter.FormatItemDetailName(DisplayName(_currentItemId, item.Name));
		SetItemTypeTag(item);
		_owned.Text = $"Owned: {_gameState.Inventory.GetValueOrDefault(_currentItemId)}";
		_price.Text = $"Value: {GetItemPrice(_currentItemId, item)} gold";

		if (_itemCatalog.IsConsumable(_currentItemId))
		{
			_traitsHeader.Text = "EFFECT";
			_risksHeader.Text = "CAN USE ON";
			_traits.Text = InventoryItemTextFormatter.BuildConsumableEffectText(item);
			_risks.Text = InventoryItemTextFormatter.BuildConsumableGateText(item);
			SetDescriptionText(InventoryItemTextFormatter.BuildItemDetailDescription(item));
			return;
		}

		_traitsHeader.Text = "TRAITS";
		_risksHeader.Text = "RISKS";
		if (_itemCatalog.IsIngredient(_currentItemId))
		{
			_traits.Text = FormatIngredientTraitText(item);
			_risks.Text = FormatIngredientRiskText(item);
		}
		else
		{
			_traits.Text = InventoryItemTextFormatter.FormatTopStats(item.Traits, 3);
			_risks.Text = _itemCatalog.IsPotion(_currentItemId)
				? InventoryItemTextFormatter.FormatTopStatNames(item.Risks, 3, "None")
				: InventoryItemTextFormatter.FormatTopStats(item.Risks, 3, "None");
		}

		SetDescriptionText(BuildDescription(_currentItemId, item));
	}

	private void SetItemTypeTag(ItemDef item)
	{
		var displayTypeTag = InventoryItemTextFormatter.TryGetVisibleTypeTag(item);
		_typeTag.Text = displayTypeTag;
		_typeTag.Visible = !string.IsNullOrWhiteSpace(displayTypeTag);
	}

	private string BuildDescription(string itemId, ItemDef item)
	{
		if (_itemCatalog.IsPotion(itemId) && item.Treatment is null)
			return _brewService.BuildPotionDescriptionText(itemId, item.Description);
		if (_itemCatalog.IsIngredient(itemId))
			return InventoryItemTextFormatter.BuildDescriptionWithIngredientEffects(item);

		return InventoryItemTextFormatter.BuildItemDetailDescription(item);
	}

	private void SetDescriptionText(string text)
	{
		_description.Text = text;
		_description.Visible = !string.IsNullOrWhiteSpace(text);
	}

	private static string FormatIngredientTraitText(ItemDef item)
	{
		return HasPositiveStats(item.Traits)
			? InventoryItemTextFormatter.FormatTopStats(item.Traits, 3)
			: InventoryItemTextFormatter.FormatPreparationTraitNames(item.Preparations, 3);
	}

	private static string FormatIngredientRiskText(ItemDef item)
	{
		return HasPositiveStats(item.Risks)
			? InventoryItemTextFormatter.FormatTopStats(item.Risks, 3, "None")
			: InventoryItemTextFormatter.FormatPreparationRiskNames(item.Preparations, 3);
	}

	private static bool HasPositiveStats(Dictionary<string, int>? stats)
	{
		if (stats is null)
			return false;

		foreach (var stat in stats)
		{
			if (!string.IsNullOrWhiteSpace(stat.Key) && stat.Value > 0)
				return true;
		}

		return false;
	}

	private string DisplayName(string itemId, string fallbackName)
	{
		if (_itemCatalog.IsPotion(itemId))
		{
			var customName = _gameState.GetPotionDisplayName(itemId);
			if (!string.IsNullOrWhiteSpace(customName))
				return customName;
		}

		return fallbackName;
	}

	private int GetItemPrice(string itemId, ItemDef item)
	{
		return _gameState.TryGetPotionBasePrice(itemId, out var potionBasePrice)
			? potionBasePrice
			: item.BasePrice;
	}

	private void ClearPanel()
	{
		if (_icon is not null)
			_icon.Texture = null;
		if (_name is not null)
			_name.Text = "";
		if (_typeTag is not null)
		{
			_typeTag.Text = "";
			_typeTag.Visible = false;
		}
		if (_owned is not null)
			_owned.Text = "";
		if (_price is not null)
			_price.Text = "";
		if (_traits is not null)
			_traits.Text = "";
		if (_risks is not null)
			_risks.Text = "";
		if (_description is not null)
			SetDescriptionText(string.Empty);
	}
}
