using Godot;

namespace OccultShop.UI;

public partial class StationBookController : Control
{
	[Export] public NodePath BookButtonPath = new("Book/BookHotspot");
	[Export] public NodePath PotionBookPanelPath = new("../BookOverlayLayer/PotionBookPanel");
	[Export] public NodePath IngredientBookPanelPath = new("../BookOverlayLayer/IngredientBookPanel");
	[Export] public NodePath BookDismissOverlayPath = new("../BookOverlayLayer/BookDismissOverlay");
	[Export] public NodePath PotionBookSwitchButtonPath = new("../BookOverlayLayer/PotionBookPanel/BookRow/BookPanel/BookSwitch");
	[Export] public NodePath IngredientBookSwitchButtonPath = new("../BookOverlayLayer/IngredientBookPanel/BookRow/BookPanel/BookSwitch");
	[Export] public NodePath PotionBookCloseButtonPath = new("../BookOverlayLayer/PotionBookPanel/BookRow/BookPanel/Margin/VBox/Header/Close");
	[Export] public NodePath IngredientBookCloseButtonPath = new("../BookOverlayLayer/IngredientBookPanel/BookRow/BookPanel/Margin/VBox/Header/Close");

	private Button? _bookButton;
	private Control? _bookDismissOverlay;
	private Button? _potionBookSwitchButton;
	private Button? _ingredientBookSwitchButton;
	private Button? _potionBookCloseButton;
	private Button? _ingredientBookCloseButton;
	private Control.GuiInputEventHandler? _bookDismissOverlayGuiInputHandler;
	private PotionBookPanel? _potionBookPanel;
	private IngredientBookPanel? _ingredientBookPanel;
	private BookPanelKind _activeBookPanelKind = BookPanelKind.Potion;

	public override void _Ready()
	{
		_bookButton = GetOptionalNode<Button>(BookButtonPath, nameof(BookButtonPath));
		_potionBookPanel = GetOptionalNode<PotionBookPanel>(PotionBookPanelPath, nameof(PotionBookPanelPath));
		_ingredientBookPanel = GetOptionalNode<IngredientBookPanel>(IngredientBookPanelPath, nameof(IngredientBookPanelPath));
		_bookDismissOverlay = GetOptionalNode<Control>(BookDismissOverlayPath, nameof(BookDismissOverlayPath));
		_potionBookSwitchButton = GetOptionalNode<Button>(PotionBookSwitchButtonPath, nameof(PotionBookSwitchButtonPath));
		_ingredientBookSwitchButton = GetOptionalNode<Button>(IngredientBookSwitchButtonPath, nameof(IngredientBookSwitchButtonPath));
		_potionBookCloseButton = GetOptionalNode<Button>(PotionBookCloseButtonPath, nameof(PotionBookCloseButtonPath));
		_ingredientBookCloseButton = GetOptionalNode<Button>(IngredientBookCloseButtonPath, nameof(IngredientBookCloseButtonPath));

		if (_bookButton is not null)
			_bookButton.Pressed += OnBookPressed;
		if (_bookDismissOverlay is not null)
		{
			_bookDismissOverlayGuiInputHandler = OnBookDismissOverlayGuiInput;
			_bookDismissOverlay.GuiInput += _bookDismissOverlayGuiInputHandler;
			SetBookDismissOverlayVisible(false);
		}
		if (_potionBookSwitchButton is not null)
			_potionBookSwitchButton.Pressed += OnPotionBookSwitchPressed;
		if (_ingredientBookSwitchButton is not null)
			_ingredientBookSwitchButton.Pressed += OnIngredientBookSwitchPressed;
		if (_potionBookCloseButton is not null)
			_potionBookCloseButton.Pressed += HideBookPanels;
		if (_ingredientBookCloseButton is not null)
			_ingredientBookCloseButton.Pressed += HideBookPanels;

		UpdateBookSwitchButtons();
	}

	public override void _ExitTree()
	{
		if (_bookButton is not null)
			_bookButton.Pressed -= OnBookPressed;
		if (_bookDismissOverlay is not null && _bookDismissOverlayGuiInputHandler is not null)
			_bookDismissOverlay.GuiInput -= _bookDismissOverlayGuiInputHandler;
		if (_potionBookSwitchButton is not null)
			_potionBookSwitchButton.Pressed -= OnPotionBookSwitchPressed;
		if (_ingredientBookSwitchButton is not null)
			_ingredientBookSwitchButton.Pressed -= OnIngredientBookSwitchPressed;
		if (_potionBookCloseButton is not null)
			_potionBookCloseButton.Pressed -= HideBookPanels;
		if (_ingredientBookCloseButton is not null)
			_ingredientBookCloseButton.Pressed -= HideBookPanels;
	}

	private void OnBookPressed()
	{
		ShowBookPanel(_activeBookPanelKind);
	}

	private void OnPotionBookSwitchPressed()
	{
		ShowBookPanel(BookPanelKind.Ingredient);
	}

	private void OnIngredientBookSwitchPressed()
	{
		ShowBookPanel(BookPanelKind.Potion);
	}

	private void ShowBookPanel(BookPanelKind bookPanelKind)
	{
		if (bookPanelKind == BookPanelKind.Potion)
		{
			if (_potionBookPanel is null)
			{
				GD.PushError("StationBookController: PotionBookPanel was not found.");
				return;
			}

			if (_ingredientBookPanel is not null)
				_ingredientBookPanel.Visible = false;
			SetBookDismissOverlayVisible(true);
			_potionBookPanel.ShowPanel();
			_potionBookPanel.MoveToFront();
			SetActiveBookPanelKind(BookPanelKind.Potion);
			return;
		}

		if (_ingredientBookPanel is null)
		{
			GD.PushError("StationBookController: IngredientBookPanel was not found.");
			return;
		}

		if (_potionBookPanel is not null)
			_potionBookPanel.Visible = false;
		SetBookDismissOverlayVisible(true);
		_ingredientBookPanel.ShowPanel();
		_ingredientBookPanel.MoveToFront();
		SetActiveBookPanelKind(BookPanelKind.Ingredient);
	}

	private void OnBookDismissOverlayGuiInput(InputEvent @event)
	{
		if (@event is not InputEventMouseButton mouseButton ||
			mouseButton.ButtonIndex != MouseButton.Left ||
			!mouseButton.Pressed)
			return;

		HideBookPanels();
		_bookDismissOverlay?.AcceptEvent();
	}

	private void HideBookPanels()
	{
		if (_potionBookPanel is not null)
			_potionBookPanel.Visible = false;
		if (_ingredientBookPanel is not null)
			_ingredientBookPanel.Visible = false;
		SetBookDismissOverlayVisible(false);
	}

	private void SetBookDismissOverlayVisible(bool visible)
	{
		if (_bookDismissOverlay is null)
			return;

		_bookDismissOverlay.Visible = visible;
		_bookDismissOverlay.MouseFilter = visible ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
	}

	private void SetActiveBookPanelKind(BookPanelKind bookPanelKind)
	{
		_activeBookPanelKind = bookPanelKind;
		UpdateBookSwitchButtons();
	}

	private void UpdateBookSwitchButtons()
	{
		if (_potionBookSwitchButton is not null)
		{
			_potionBookSwitchButton.Text = "Ingredients";
			_potionBookSwitchButton.TooltipText = "Open ingredient book";
			_potionBookSwitchButton.Disabled = false;
		}

		if (_ingredientBookSwitchButton is not null)
		{
			_ingredientBookSwitchButton.Text = "Potions";
			_ingredientBookSwitchButton.TooltipText = "Open potion book";
			_ingredientBookSwitchButton.Disabled = false;
		}
	}

	private TNode? GetOptionalNode<TNode>(NodePath path, string exportName) where TNode : Node
	{
		var node = GetNodeOrNull<TNode>(path);
		if (node is null)
			GD.PushError($"StationBookController: {exportName} was not found at '{path}'.");

		return node;
	}

	private enum BookPanelKind
	{
		Potion,
		Ingredient
	}
}
