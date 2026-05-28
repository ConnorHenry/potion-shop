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
	[Export] public NodePath DataDbPath = new("/root/DataDb");
	[Export] public NodePath GameStatePath = new("/root/GameState");

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
	private DataDb _dataDb = default!;
	private GameState _gameState = default!;
	private int _secondsRemaining;
	private bool _awaitingSaleResultClose;

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
		var dataDb = GetNodeOrNull<DataDb>(DataDbPath);
		if (dataDb is null)
		{
			GD.PushError($"DayController: DataDb was not found at '{DataDbPath}'.");
			return;
		}

		var gameState = GetNodeOrNull<GameState>(GameStatePath);
		if (gameState is null)
		{
			GD.PushError($"DayController: GameState was not found at '{GameStatePath}'.");
			return;
		}

		_dataDb = dataDb;
		_gameState = gameState;

		_shopTimer = new Godot.Timer
		{
			WaitTime = 1.0,
			OneShot = false,
			Autostart = false
		};
		AddChild(_shopTimer);
		_shopTimer.Timeout += OnShopTimerTick;

		_customerPanel.SaleResolved += OnCustomerSaleResolved;
		_customerPanel.SaleResultClosed += OnCustomerSaleResultClosed;
		_customerPanel.CustomerSkipped += OnCustomerSkipped;
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
			_customerPanel.CustomerSkipped -= OnCustomerSkipped;
		if (_customerPanel != null)
			_customerPanel.SaleResolved -= OnCustomerSaleResolved;
		if (_customerPanel != null)
			_customerPanel.SaleResultClosed -= OnCustomerSaleResultClosed;
		if (_daySummaryPanel != null)
			_daySummaryPanel.ContinuePressed -= OnSummaryContinuePressed;
	}

	public void StartShopDay()
	{
		if (IsShopOpen)
			return;

		_daySummaryPanel.HidePanel();
		_customerPanel.HidePanel();
		_customerPanel.SuppressSaleResultPanel = false;
		_shopDayStats.Reset();
		_secondsRemaining = ShopDurationSeconds;
		_awaitingSaleResultClose = false;
		IsShopOpen = true;
		EmitShopStateChanged();
		_customerEventController.BeginShopDay();

		if (!TryShowNextCustomer())
		{
			CloseShopAndShowSummary();
			return;
		}

		_shopTimer.Start();
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
		if (_gameState.ActiveRules.Contains("thin_veil"))
			_gameState.AddDread(2);

		var card = _eventController.DrawNightEvent(_dataDb, _gameState);
		if (card is null)
		{
			_gameState.AddDread(1);
			_gameState.NextDay();
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

		if (_customerPanel.SuppressSaleResultPanel)
		{
			if (_secondsRemaining <= 0)
			{
				CloseShopAndShowSummary();
				return;
			}

			if (!TryShowNextCustomer())
				CloseShopAndShowSummary();
			return;
		}

		if (!_shopTimer.IsStopped())
			_shopTimer.Stop();

		_awaitingSaleResultClose = true;
		EmitShopStateChanged();
	}

	private void OnCustomerSaleResultClosed()
	{
		if (!IsShopOpen)
			return;

		if (!_awaitingSaleResultClose)
			return;

		_awaitingSaleResultClose = false;

		if (_secondsRemaining <= 0)
		{
			CloseShopAndShowSummary();
			return;
		}

		if (!TryShowNextCustomer())
		{
			CloseShopAndShowSummary();
			return;
		}

		if (_shopTimer.IsStopped())
			_shopTimer.Start();

		EmitShopStateChanged();
	}

	private bool TryShowNextCustomer()
	{
		if (!IsShopOpen)
			return false;

		var interaction = _customerEventController.DrawShopDayCustomerInteraction(_dataDb, _gameState);
		if (interaction is null)
		{
			_customerPanel.HidePanel();
			return false;
		}

		_customerPanel.ShowInteraction(interaction);
		return true;
	}

	private void OnCustomerSkipped()
	{
		if (!IsShopOpen)
			return;

		if (_secondsRemaining <= 0)
		{
			CloseShopAndShowSummary();
			return;
		}

		if (!TryShowNextCustomer())
			CloseShopAndShowSummary();
	}

	private void CloseShopAndShowSummary()
	{
		if (_shopTimer.IsStopped() == false)
			_shopTimer.Stop();

		IsShopOpen = false;
		_secondsRemaining = 0;
		_awaitingSaleResultClose = false;
		_customerPanel.HidePanel();
		_customerPanel.SuppressSaleResultPanel = false;
		_brewPanel.HidePanel();
		_recipeBookPanel.HidePanel();
		_daySummaryPanel.ShowSummary(
			_gameState.Day,
			_shopDayStats.CustomersServed,
			_shopDayStats.SuccessfulSales,
			_shopDayStats.FailedSales,
			_shopDayStats.GoldEarned,
			_shopDayStats.DreadChange,
			_gameState.Gold,
			_gameState.Dread);

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
