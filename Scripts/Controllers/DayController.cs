using System;
using Godot;
using OccultShop.Autoload;
using OccultShop.Systems;

namespace OccultShop.Controllers;

public partial class DayController : Node
{
	private const int MaxCustomersPerShopDay = 3;

	[Export] public NodePath EventModalPath = default!;
	[Export] public NodePath EventControllerPath = default!;
	[Export] public NodePath CustomerEventControllerPath = default!;
	[Export] public NodePath CustomerPanelPath = default!;
	[Export] public NodePath StationCustomerPanelPath = default!;
	[Export] public NodePath BrewPanelPath = default!;
	[Export] public NodePath DaySummaryPanelPath = default!;
	[Export] public NodePath DataDbPath = new(AutoloadNodePaths.DataDb);
	[Export] public NodePath GameStatePath = new(AutoloadNodePaths.GameState);

	private UI.EventModal _eventModal = default!;
	private EventController _eventController = default!;
	private CustomerEventController _customerEventController = default!;
	private UI.CustomerPanel _customerPanel = default!;
	private UI.StationCustomerPanel _stationCustomerPanel = default!;
	private UI.BrewPanel _brewPanel = default!;
	private UI.DaySummaryPanel _daySummaryPanel = default!;
	private readonly ShopDayStats _shopDayStats = new();
	private DataDb _dataDb = default!;
	private GameState _gameState = default!;
	private int _customersArrived;
	private bool _awaitingSaleResultClose;
	private bool _closeShopAfterCurrentCustomer;

	public bool IsShopOpen { get; private set; }
	public int CustomersArrivedToday => _customersArrived;
	public int MaxCustomersPerDay => MaxCustomersPerShopDay;
	public event Action? ShopStateChanged;

	public override void _Ready()
	{
		_eventModal = GetNode<UI.EventModal>(EventModalPath);
		_eventController = GetNode<EventController>(EventControllerPath);
		_customerEventController = GetNode<CustomerEventController>(CustomerEventControllerPath);
		_customerPanel = GetNode<UI.CustomerPanel>(CustomerPanelPath);
		_stationCustomerPanel = GetNode<UI.StationCustomerPanel>(StationCustomerPanelPath);
		_brewPanel = GetNode<UI.BrewPanel>(BrewPanelPath);
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

		_customerPanel.SaleResolved += OnCustomerSaleResolved;
		_customerPanel.SaleResultClosed += OnCustomerSaleResultClosed;
		_customerPanel.DialogueResolved += OnCustomerDialogueResolved;
		_customerPanel.CustomerSkipped += OnCustomerSkipped;
		_stationCustomerPanel.SaleResolved += OnStationCustomerSaleResolved;
		_stationCustomerPanel.CustomerSkipped += OnStationCustomerSkipped;
		_stationCustomerPanel.CustomerResolved += OnStationCustomerResolved;
		_stationCustomerPanel.CustomerQueueEmptied += OnStationCustomerQueueEmptied;
		_daySummaryPanel.ContinuePressed += OnSummaryContinuePressed;
		_daySummaryPanel.HidePanel();
		_customerPanel.SuppressSaleResultPanel = false;
		_customerPanel.SetCloseShopMode(false);
		EmitShopStateChanged();
	}

	public override void _ExitTree()
	{
		if (_customerPanel != null)
			_customerPanel.CustomerSkipped -= OnCustomerSkipped;
		if (_customerPanel != null)
			_customerPanel.SaleResolved -= OnCustomerSaleResolved;
		if (_customerPanel != null)
			_customerPanel.SaleResultClosed -= OnCustomerSaleResultClosed;
		if (_customerPanel != null)
			_customerPanel.DialogueResolved -= OnCustomerDialogueResolved;
		if (_stationCustomerPanel != null)
		{
			_stationCustomerPanel.SaleResolved -= OnStationCustomerSaleResolved;
			_stationCustomerPanel.CustomerSkipped -= OnStationCustomerSkipped;
			_stationCustomerPanel.CustomerResolved -= OnStationCustomerResolved;
			_stationCustomerPanel.CustomerQueueEmptied -= OnStationCustomerQueueEmptied;
		}
		if (_daySummaryPanel != null)
			_daySummaryPanel.ContinuePressed -= OnSummaryContinuePressed;
	}

	public void StartShopDay()
	{
		if (IsShopOpen)
			return;

		_daySummaryPanel.HidePanel();
		_customerPanel.HidePanel();
		_stationCustomerPanel.ClearCustomers();
		_customerPanel.SuppressSaleResultPanel = false;
		_customerPanel.SetCloseShopMode(false);
		_shopDayStats.Reset();
		_customersArrived = 0;
		_awaitingSaleResultClose = false;
		_closeShopAfterCurrentCustomer = false;
		IsShopOpen = true;
		EmitShopStateChanged();
		_customerEventController.BeginShopDay();

		if (!TryShowQueuedCustomers())
		{
			CloseShopAndShowSummary();
			return;
		}
	}

	public void ForceCloseShopAfterCurrentCustomerForTutorial()
	{
		if (!IsShopOpen)
			return;

		_closeShopAfterCurrentCustomer = true;
		EmitShopStateChanged();
	}

	public bool TryCloseShopDayFromDebug()
	{
		if (!IsShopOpen)
			return false;

		CloseShopAndShowSummary();
		return true;
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
		_stationCustomerPanel.ClearCustomers();
		_brewPanel.HidePanel();

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

		if (ShouldCloseShopAfterCurrentCustomer())
		{
			CloseShopAndShowSummary();
			return;
		}

		if (_customerPanel.SuppressSaleResultPanel)
		{
			if (!TryShowNextCustomer())
				CloseShopAndShowSummary();
			return;
		}

		_awaitingSaleResultClose = true;
		EmitShopStateChanged();
	}

	private void OnStationCustomerSaleResolved(bool success, int goldDelta, int dreadDelta, float finalScore, string grade)
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

		EmitShopStateChanged();
	}

	private void OnStationCustomerSkipped()
	{
		if (!IsShopOpen)
			return;

		EmitShopStateChanged();
	}

	private void OnStationCustomerResolved()
	{
		if (!IsShopOpen)
			return;

		if (_closeShopAfterCurrentCustomer || !_stationCustomerPanel.HasQueuedCustomers)
		{
			CloseShopAndShowSummary();
			return;
		}

		EmitShopStateChanged();
	}

	private void OnStationCustomerQueueEmptied()
	{
		if (!IsShopOpen)
			return;

		CloseShopAndShowSummary();
	}

	private void OnCustomerSaleResultClosed()
	{
		if (!IsShopOpen)
			return;

		if (!_awaitingSaleResultClose)
			return;

		_awaitingSaleResultClose = false;

		if (!TryShowNextCustomer())
		{
			CloseShopAndShowSummary();
			return;
		}

		EmitShopStateChanged();
	}

	private void OnCustomerDialogueResolved()
	{
		if (!IsShopOpen)
			return;

		if (ShouldCloseShopAfterCurrentCustomer())
		{
			CloseShopAndShowSummary();
			return;
		}

		_awaitingSaleResultClose = true;
		EmitShopStateChanged();
	}

	private bool TryShowNextCustomer()
	{
		if (!IsShopOpen)
			return false;

		if (_customersArrived >= MaxCustomersPerShopDay)
		{
			_customerPanel.HidePanel();
			return false;
		}

		var interaction = _customerEventController.DrawShopDayCustomerInteraction(_dataDb, _gameState);
		if (interaction is null)
		{
			_customerPanel.HidePanel();
			return false;
		}

		_customersArrived += 1;

		if (_customerPanel.Visible)
			_customerPanel.ShowInteraction(interaction);
		else
			_customerPanel.PrepareInteraction(interaction);

		EmitShopStateChanged();
		return true;
	}

	private bool TryShowQueuedCustomers()
	{
		if (!IsShopOpen)
			return false;

		var customers = new System.Collections.Generic.List<OccultShop.Models.CustomerInteractionDef>();
		while (_customersArrived < MaxCustomersPerShopDay)
		{
			var interaction = _customerEventController.DrawShopDayCustomerInteraction(_dataDb, _gameState);
			if (interaction is null)
				break;

			customers.Add(interaction);
			_customersArrived += 1;
		}

		if (customers.Count == 0)
		{
			_stationCustomerPanel.ClearCustomers();
			return false;
		}

		_stationCustomerPanel.SetCustomers(customers);
		_brewPanel.ShowPanel();
		EmitShopStateChanged();
		return true;
	}

	private void OnCustomerSkipped()
	{
		if (!IsShopOpen)
			return;

		if (ShouldCloseShopAfterCurrentCustomer())
		{
			CloseShopAndShowSummary();
			return;
		}

		if (!TryShowNextCustomer())
			CloseShopAndShowSummary();
	}

	private void CloseShopAndShowSummary()
	{
		IsShopOpen = false;
		_customersArrived = 0;
		_awaitingSaleResultClose = false;
		_closeShopAfterCurrentCustomer = false;
		_customerPanel.HidePanel();
		_stationCustomerPanel.ClearCustomers();
		_customerPanel.SuppressSaleResultPanel = false;
		_customerPanel.SetCloseShopMode(false);
		_brewPanel.HidePanel();
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

	private bool ShouldCloseShopAfterCurrentCustomer()
	{
		return _closeShopAfterCurrentCustomer || _customersArrived >= MaxCustomersPerShopDay;
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
