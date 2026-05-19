using Godot;
using OccultShop.Autoload;
using OccultShop.Systems;

namespace OccultShop.Controllers;

public partial class DayController : Node
{
	[Export] public NodePath EventModalPath = default!;
	[Export] public NodePath EventControllerPath = default!;
	[Export] public NodePath CustomerEventControllerPath = default!;
	[Export] public NodePath InventoryPanelPath = default!;
	[Export] public NodePath CustomerPanelPath = default!;
	[Export] public NodePath BrewPanelPath = default!;

	private UI.EventModal _eventModal = default!;
	private EventController _eventController = default!;
	private CustomerEventController _customerEventController = default!;
	private UI.InventoryPanel _inventoryPanel = default!;
	private UI.CustomerPanel _customerPanel = default!;
	private UI.BrewPanel _brewPanel = default!;

	public override void _Ready()
	{
		_eventModal = GetNode<UI.EventModal>(EventModalPath);
		_eventController = GetNode<EventController>(EventControllerPath);
		_customerEventController = GetNode<CustomerEventController>(CustomerEventControllerPath);
		_inventoryPanel = GetNode<UI.InventoryPanel>(InventoryPanelPath);
		_customerPanel = GetNode<UI.CustomerPanel>(CustomerPanelPath);
		_brewPanel = GetNode<UI.BrewPanel>(BrewPanelPath);
	}

	public void ServeCustomer()
	{
		var interaction = _customerEventController.DrawCustomerInteraction(DataDb, GameState);
		if (interaction is null)
		{
			_customerPanel.HidePanel();
			return;
		}

		_customerPanel.ShowInteraction(interaction);
	}

	public void EndDayAndRunNight()
	{
		_customerPanel.HidePanel();
		_brewPanel.HidePanel();

		// Example of an escalating rule that modifies events.
		if (GameState.ActiveRules.Contains("thin_veil"))
			GameState.AddDread(2);

		var card = _eventController.DrawNightEvent(DataDb, GameState);
		if (card is null)
		{
			GameState.AddDread(1);
			GameState.NextDay();
			return;
		}

		_eventModal.ShowCard(card);
	}

	// Convenience accessors to autoloads
	private static DataDb DataDb => (DataDb)((SceneTree)Engine.GetMainLoop()).Root.GetNode("/root/DataDb");
	private static GameState GameState => (GameState)((SceneTree)Engine.GetMainLoop()).Root.GetNode("/root/GameState");
}
