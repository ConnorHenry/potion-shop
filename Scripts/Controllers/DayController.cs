using System;
using Godot;
using OccultShop.Autoload;
using OccultShop.Systems;

namespace OccultShop.Controllers;

public partial class DayController : Node
{
	private const int ShopDurationSeconds = 60;

	[Export] public NodePath EventModalPath = default!;
	[Export] public NodePath EventControllerPath = default!;
	[Export] public NodePath CustomerEventControllerPath = default!;
	[Export] public NodePath InventoryPanelPath = default!;
	[Export] public NodePath CustomerPanelPath = default!;
	[Export] public NodePath BrewPanelPath = default!;
	[Export] public NodePath RecipeBookPanelPath = default!;
	[Export] public NodePath DaySummaryPanelPath = default!;

	private UI.EventModal _eventModal = default!;
	private EventController _eventController = default!;
	private CustomerEventController _customerEventController = default!;
	private UI.InventoryPanel _inventoryPanel = default!;
	private UI.CustomerPanel _customerPanel = default!;
	private UI.BrewPanel _brewPanel = default!;
	private UI.RecipeBookPanel _recipeBookPanel = default!;
	private UI.DaySummaryPanel _daySummaryPanel = default!;
	private Godot.Timer _shopTimer = default!;
	private readonly ShopDayStats _shopDayStats = new();
	private int _secondsRemaining;

	public bool IsShopOpen { get; private set; }
	public int SecondsRemaining => _secondsRemaining;
	public event Action? ShopStateChanged;

	public override void _Ready()
	{
		_eventModal = GetNode<UI.EventModal>(EventModalPath);
		_eventController = GetNode<EventController>(EventControllerPath);
		_customerEventController = GetNode<CustomerEventController>(CustomerEventControllerPath);
		_inventoryPanel = GetNode<UI.InventoryPanel>(InventoryPanelPath);
		_customerPanel = GetNode<UI.CustomerPanel>(CustomerPanelPath);
		_brewPanel = GetNode<UI.BrewPanel>(BrewPanelPath);
		_recipeBookPanel = GetNode<UI.RecipeBookPanel>(RecipeBookPanelPath);
		_daySummaryPanel = GetNode<UI.DaySummaryPanel>(DaySummaryPanelPath);

		_shopTimer = new Godot.Timer
		{
			WaitTime = 1.0,
			OneShot = false,
			Autostart = false
		};
		AddChild(_shopTimer);
		_shopTimer.Timeout += OnShopTimerTick;

		_customerPanel.SaleResolved += OnCustomerSaleResolved;
		_daySummaryPanel.ContinuePressed += OnSummaryContinuePressed;
		_daySummaryPanel.HidePanel();
		_customerPanel.SuppressSaleResultPanel = false;
		EmitShopStateChanged();
	}

	public override void _ExitTree()
	{
		if (_shopTimer != null)
			_shopTimer.Timeout -= OnShopTimerTick;
		if (_customerPanel != null)
			_customerPanel.SaleResolved -= OnCustomerSaleResolved;
		if (_daySummaryPanel != null)
			_daySummaryPanel.ContinuePressed -= OnSummaryContinuePressed;
	}

	public void StartShopDay()
	{
		if (IsShopOpen)
			return;

		_daySummaryPanel.HidePanel();
		_customerPanel.HidePanel();
		_customerPanel.SuppressSaleResultPanel = true;
		_shopDayStats.Reset();
		_secondsRemaining = ShopDurationSeconds;
		IsShopOpen = true;
		EmitShopStateChanged();

		if (!TryShowNextCustomer())
		{
			CloseShopAndShowSummary();
			return;
		}

		_shopTimer.Start();
	}

	public void ServeCustomer()
	{
		var interaction = _customerEventController.DrawShopDayCustomerInteraction(DataDb, GameState);
		if (interaction is null)
		{
			_customerPanel.HidePanel();
			return;
		}

		_customerPanel.ShowInteraction(interaction);
	}

	public void EndDayAndRunNight()
	{
		if (IsShopOpen)
		{
			CloseShopAndShowSummary();
			return;
		}

		_daySummaryPanel.HidePanel();
		_customerPanel.HidePanel();
		_brewPanel.HidePanel();
		_recipeBookPanel.HidePanel();

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

	private void OnShopTimerTick()
	{
		if (!IsShopOpen)
			return;

		_secondsRemaining = Math.Max(0, _secondsRemaining - 1);
		EmitShopStateChanged();

		if (_secondsRemaining <= 0)
			CloseShopAndShowSummary();
	}

	private void OnCustomerSaleResolved(bool success, int goldDelta, int dreadDelta, float finalScore, string grade)
	{
		if (!IsShopOpen)
			return;

		_shopDayStats.CustomersServed += 1;
		_shopDayStats.GoldEarned += goldDelta;
		_shopDayStats.DreadChange += dreadDelta;
		if (success)
			_shopDayStats.SuccessfulSales += 1;
		else
			_shopDayStats.FailedSales += 1;

		if (_secondsRemaining <= 0)
		{
			CloseShopAndShowSummary();
			return;
		}

		if (!TryShowNextCustomer())
			CloseShopAndShowSummary();
	}

	private bool TryShowNextCustomer()
	{
		if (!IsShopOpen)
			return false;

		var interaction = _customerEventController.DrawShopDayCustomerInteraction(DataDb, GameState);
		if (interaction is null)
		{
			_customerPanel.HidePanel();
			return false;
		}

		_customerPanel.ShowInteraction(interaction);
		return true;
	}

	private void CloseShopAndShowSummary()
	{
		if (_shopTimer.IsStopped() == false)
			_shopTimer.Stop();

		IsShopOpen = false;
		_secondsRemaining = 0;
		_customerPanel.HidePanel();
		_customerPanel.SuppressSaleResultPanel = false;
		_brewPanel.HidePanel();
		_recipeBookPanel.HidePanel();
		_daySummaryPanel.ShowSummary(
			GameState.Day,
			_shopDayStats.CustomersServed,
			_shopDayStats.SuccessfulSales,
			_shopDayStats.FailedSales,
			_shopDayStats.GoldEarned,
			_shopDayStats.DreadChange,
			GameState.Gold,
			GameState.Dread);

		EmitShopStateChanged();
	}

	private void OnSummaryContinuePressed()
	{
		_daySummaryPanel.HidePanel();
		EndDayAndRunNight();
	}

	private void EmitShopStateChanged()
	{
		ShopStateChanged?.Invoke();
	}

	// Convenience accessors to autoloads
	private static DataDb DataDb => (DataDb)((SceneTree)Engine.GetMainLoop()).Root.GetNode("/root/DataDb");
	private static GameState GameState => (GameState)((SceneTree)Engine.GetMainLoop()).Root.GetNode("/root/GameState");

	private sealed class ShopDayStats
	{
		public int CustomersServed { get; set; }
		public int SuccessfulSales { get; set; }
		public int FailedSales { get; set; }
		public int GoldEarned { get; set; }
		public int DreadChange { get; set; }

		public void Reset()
		{
			CustomersServed = 0;
			SuccessfulSales = 0;
			FailedSales = 0;
			GoldEarned = 0;
			DreadChange = 0;
		}
	}
}
