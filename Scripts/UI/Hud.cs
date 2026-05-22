using Godot;
using OccultShop.Autoload;
using OccultShop.Controllers;

namespace OccultShop.UI;

public partial class Hud : Control
{
	[Export] public NodePath GoldLabelPath = default!;
	[Export] public NodePath DreadLabelPath = default!;
	[Export] public NodePath DayLabelPath = default!;

	private Label _gold = default!;
	private Label _dread = default!;
	private Label _day = default!;
	private Button _endDayButton = default!;
	private Button _serveCustomerButton = default!;
	private Button _brewPotionButton = default!;
	private Button _recipeBookButton = default!;
	private DayController _dayController = default!;
	private Control _brewPanel = default!;
	private Control _recipeBookPanel = default!;

	public override void _Ready()
	{
		_gold = GetNode<Label>(GoldLabelPath);
		_dread = GetNode<Label>(DreadLabelPath);
		_day = GetNode<Label>(DayLabelPath);

		_endDayButton = GetNode<Button>("EndDay");
		_serveCustomerButton = GetNode<Button>("ServeCustomer");
		_brewPotionButton = GetNode<Button>("BrewPotion");
		_recipeBookButton = GetNode<Button>("RecipeBook");

		_endDayButton.Pressed += OnEndDayPressed;
		_serveCustomerButton.Pressed += OnServeCustomerPressed;
		_brewPotionButton.Pressed += OnBrewPotionPressed;
		_recipeBookButton.Pressed += OnRecipeBookPressed;

		GameState.Changed += Refresh;
		Refresh();
	}

	public override void _ExitTree()
	{
		GameState.Changed -= Refresh;
		if (_endDayButton != null)
			_endDayButton.Pressed -= OnEndDayPressed;
		if (_serveCustomerButton != null)
			_serveCustomerButton.Pressed -= OnServeCustomerPressed;
		if (_brewPotionButton != null)
			_brewPotionButton.Pressed -= OnBrewPotionPressed;
		if (_recipeBookButton != null)
			_recipeBookButton.Pressed -= OnRecipeBookPressed;
	}

	private void Refresh()
	{
		_gold.Text = $"Gold: {GameState.Gold}";
		_dread.Text = $"Dread: {GameState.Dread}";
		_day.Text = $"Day: {GameState.Day}";
	}

	private void OnEndDayPressed()
	{
		DayController.EndDayAndRunNight();
	}

	private void OnServeCustomerPressed()
	{
		DayController.ServeCustomer();
	}

	private void OnBrewPotionPressed()
	{
		var brewPanel = BrewPanel;
		brewPanel.Visible = !brewPanel.Visible;
	}

	private void OnRecipeBookPressed()
	{
		var recipeBookPanel = RecipeBookPanel;
		recipeBookPanel.Visible = !recipeBookPanel.Visible;
	}

	private static GameState GameState => ((SceneTree)Engine.GetMainLoop()).Root.GetNode<GameState>("GameState");
	private static DayController DayController => ((SceneTree)Engine.GetMainLoop()).Root.GetNode<DayController>("Main/DayController");
	private static Control BrewPanel => ((SceneTree)Engine.GetMainLoop()).Root.GetNode<Control>("Main/CanvasLayer/BrewPanel");
	private static Control RecipeBookPanel => ((SceneTree)Engine.GetMainLoop()).Root.GetNode<Control>("Main/CanvasLayer/RecipeBookPanel");
}
