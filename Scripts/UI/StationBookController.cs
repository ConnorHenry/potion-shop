using Godot;

namespace OccultShop.UI;

public partial class StationBookController : Control
{
	[Export] public NodePath BookButtonPath = new("Book/BookHotspot");
	[Export] public NodePath BookSwitchButtonPath = new("Book/BookSwitch");
	[Export] public NodePath PotionBookPanelPath = new("../PotionBookPanel");
	[Export] public NodePath IngredientBookPanelPath = new("../IngredientBookPanel");

	private Button? _bookButton;
	private Button? _bookSwitchButton;
	private PotionBookPanel? _potionBookPanel;
	private IngredientBookPanel? _ingredientBookPanel;
	private BookPanelKind _activeBookPanelKind = BookPanelKind.Potion;

	public override void _Ready()
	{
		_bookButton = GetOptionalNode<Button>(BookButtonPath, nameof(BookButtonPath));
		_bookSwitchButton = GetOptionalNode<Button>(BookSwitchButtonPath, nameof(BookSwitchButtonPath));
		_potionBookPanel = GetOptionalNode<PotionBookPanel>(PotionBookPanelPath, nameof(PotionBookPanelPath));
		_ingredientBookPanel = GetOptionalNode<IngredientBookPanel>(IngredientBookPanelPath, nameof(IngredientBookPanelPath));

		if (_bookButton is not null)
			_bookButton.Pressed += OnBookPressed;
		if (_bookSwitchButton is not null)
			_bookSwitchButton.Pressed += OnBookSwitchPressed;

		UpdateBookSwitchButton();
	}

	public override void _ExitTree()
	{
		if (_bookButton is not null)
			_bookButton.Pressed -= OnBookPressed;
		if (_bookSwitchButton is not null)
			_bookSwitchButton.Pressed -= OnBookSwitchPressed;
	}

	private void OnBookPressed()
	{
		ShowBookPanel(_activeBookPanelKind);
	}

	private void OnBookSwitchPressed()
	{
		ShowBookPanel(GetOppositeBookPanelKind(_activeBookPanelKind));
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
		_ingredientBookPanel.ShowPanel();
		_ingredientBookPanel.MoveToFront();
		SetActiveBookPanelKind(BookPanelKind.Ingredient);
	}

	private void SetActiveBookPanelKind(BookPanelKind bookPanelKind)
	{
		_activeBookPanelKind = bookPanelKind;
		UpdateBookSwitchButton();
	}

	private void UpdateBookSwitchButton()
	{
		if (_bookSwitchButton is null)
			return;

		var targetBookPanelKind = GetOppositeBookPanelKind(_activeBookPanelKind);
		_bookSwitchButton.Text = GetBookSwitchButtonText(targetBookPanelKind);
		_bookSwitchButton.TooltipText = GetBookSwitchButtonTooltipText(targetBookPanelKind);
		_bookSwitchButton.Disabled = false;
	}

	private static BookPanelKind GetOppositeBookPanelKind(BookPanelKind bookPanelKind)
	{
		return bookPanelKind == BookPanelKind.Potion
			? BookPanelKind.Ingredient
			: BookPanelKind.Potion;
	}

	private static string GetBookSwitchButtonText(BookPanelKind targetBookPanelKind)
	{
		return targetBookPanelKind == BookPanelKind.Potion ? "Potions" : "Ingredients";
	}

	private static string GetBookSwitchButtonTooltipText(BookPanelKind targetBookPanelKind)
	{
		return targetBookPanelKind == BookPanelKind.Potion ? "Open potion book" : "Open ingredient book";
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
