using System;
using Godot;
using OccultShop.Autoload;
using OccultShop.Systems;

namespace OccultShop.Controllers;

public partial class DayController : Node
{
	private const int MaxCustomersPerShopDay = 3;

	[Export] public NodePath CustomerEventControllerPath = default!;
	[Export] public NodePath StationCustomerPanelPath = default!;
	[Export] public NodePath BrewPanelPath = default!;
	[Export] public NodePath DaySummaryPanelPath = default!;
	[Export] public NodePath DataDbPath = new(AutoloadNodePaths.DataDb);
	[Export] public NodePath GameStatePath = new(AutoloadNodePaths.GameState);

	private CustomerEventController _customerEventController = default!;
	private UI.StationCustomerPanel _stationCustomerPanel = default!;
	private UI.BrewPanel _brewPanel = default!;
	private UI.DaySummaryPanel _daySummaryPanel = default!;
	private readonly ShopDayStats _shopDayStats = new();
	private DataDb _dataDb = default!;
	private GameState _gameState = default!;
	private int _customersArrived;
	private bool _closeShopAfterCurrentCustomer;

	public bool IsShopOpen { get; private set; }
	public int CustomersArrivedToday => _customersArrived;
	public int MaxCustomersPerDay => MaxCustomersPerShopDay;
	public event Action? ShopStateChanged;

	public override void _Ready()
	{
		_customerEventController = GetNode<CustomerEventController>(CustomerEventControllerPath);
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

		_stationCustomerPanel.SaleResolved += OnStationCustomerSaleResolved;
		_stationCustomerPanel.CustomerSkipped += OnStationCustomerSkipped;
		_stationCustomerPanel.CustomerResolved += OnStationCustomerResolved;
		_stationCustomerPanel.CustomerQueueEmptied += OnStationCustomerQueueEmptied;
		_daySummaryPanel.ContinuePressed += OnSummaryContinuePressed;
		_daySummaryPanel.HidePanel();
		EmitShopStateChanged();
	}

	public override void _ExitTree()
	{
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
		_stationCustomerPanel.ClearCustomers();
		_shopDayStats.Reset();
		_customersArrived = 0;
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
		_stationCustomerPanel.ClearCustomers();
		_brewPanel.HidePanel();

		_gameState.NextDay();
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

	private void CloseShopAndShowSummary()
	{
		IsShopOpen = false;
		_customersArrived = 0;
		_closeShopAfterCurrentCustomer = false;
		_stationCustomerPanel.ClearCustomers();
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
