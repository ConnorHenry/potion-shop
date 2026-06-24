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
	private bool _isShopDayReadyToEnd;

	public bool IsShopOpen { get; private set; }
	public bool IsShopDayReadyToEnd => _isShopDayReadyToEnd;
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
		Callable.From(RestoreShopDayState).CallDeferred();
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
		_isShopDayReadyToEnd = false;
		IsShopOpen = true;
		_gameState.BeginShopDayState();
		EmitShopStateChanged();
		_customerEventController.BeginShopDay();

		if (!TryShowNextCustomer())
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
		_gameState.RequestCloseShopAfterCurrentCustomer();
		EmitShopStateChanged();
	}

	public bool TryCloseShopDayFromDebug()
	{
		if (!IsShopOpen)
			return false;

		CloseShopAndShowSummary();
		return true;
	}

	public ShopDayFastForwardResult TryFastForwardToDayFromDebug(int targetDay)
	{
		var result = ShopDayFastForwardService.FastForwardToDay(
			_dataDb,
			_gameState,
			_customerEventController,
			targetDay,
			MaxCustomersPerShopDay);
		if (!result.Applied)
			return result;

		IsShopOpen = false;
		_customersArrived = 0;
		_closeShopAfterCurrentCustomer = false;
		_isShopDayReadyToEnd = false;
		_shopDayStats.Reset();
		_daySummaryPanel.HidePanel();
		_stationCustomerPanel.ClearCustomers();
		_brewPanel.HidePanel();
		EmitShopStateChanged();
		return result;
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

		_gameState.RecordShopDaySale(success, goldDelta, dreadDelta);
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

		_gameState.ClearActiveShopCustomer();
		if (ShouldCloseShopAfterCurrentCustomer())
		{
			CloseShopAndShowSummary();
			return;
		}

		if (_customersArrived >= MaxCustomersPerShopDay)
		{
			MarkShopDayReadyToEnd();
			return;
		}

		if (!TryShowNextCustomer())
			CloseShopAndShowSummary();
	}

	private void OnStationCustomerQueueEmptied()
	{
		if (!IsShopOpen || _isShopDayReadyToEnd)
			return;

		if (ShouldCloseShopAfterCurrentCustomer())
		{
			CloseShopAndShowSummary();
			return;
		}

		if (_customersArrived >= MaxCustomersPerShopDay)
		{
			MarkShopDayReadyToEnd();
			return;
		}

		CloseShopAndShowSummary();
	}

	private bool TryShowNextCustomer()
	{
		if (!IsShopOpen)
			return false;

		if (_customersArrived >= MaxCustomersPerShopDay)
		{
			_stationCustomerPanel.ClearCustomers();
			return false;
		}

		var interaction = _customerEventController.DrawShopDayCustomerInteraction(_dataDb, _gameState);
		if (interaction is null)
		{
			_stationCustomerPanel.ClearCustomers();
			return false;
		}

		var customers = new System.Collections.Generic.List<OccultShop.Models.CustomerInteractionDef> { interaction };
		_gameState.RecordShopDayCustomerArrived(interaction);
		_customersArrived = _gameState.ShopDayCustomersArrived;
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
		_isShopDayReadyToEnd = false;
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
		_gameState.CloseShopDayState();

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
		return _closeShopAfterCurrentCustomer;
	}

	private void MarkShopDayReadyToEnd()
	{
		if (_isShopDayReadyToEnd)
			return;

		_isShopDayReadyToEnd = true;
		_stationCustomerPanel.ClearCustomers();
		_brewPanel.HidePanel();
		EmitShopStateChanged();
	}

	private void RestoreShopDayState()
	{
		IsShopOpen = _gameState.IsShopDayOpen;
		_customersArrived = _gameState.ShopDayCustomersArrived;
		_closeShopAfterCurrentCustomer = _gameState.CloseShopAfterCurrentCustomer;
		_isShopDayReadyToEnd = false;
		_shopDayStats.Restore(
			_gameState.ShopDayCustomersServed,
			_gameState.ShopDaySuccessfulSales,
			_gameState.ShopDayFailedSales,
			_gameState.ShopDayGoldEarned,
			_gameState.ShopDayDreadChange);

		if (!IsShopOpen)
		{
			_brewPanel.HidePanel();
			EmitShopStateChanged();
			return;
		}

		if (IsPersistedShopDayReadyToEnd())
		{
			MarkShopDayReadyToEnd();
			return;
		}

		_brewPanel.ShowPanel();
		if (TryResolveActiveCustomerInteraction(out var interaction) && interaction is not null)
		{
			_stationCustomerPanel.RestoreActiveCustomer(interaction);
			EmitShopStateChanged();
			return;
		}

		GD.PushError("DayController: Shop day was open but no active customer could be restored. Advancing to the next customer.");
		if (TryShowNextCustomer())
			return;

		CloseShopAndShowSummary();
	}

	private bool IsPersistedShopDayReadyToEnd()
	{
		return _customersArrived >= MaxCustomersPerShopDay &&
			string.IsNullOrWhiteSpace(_gameState.ActiveCustomerInteractionId) &&
			_gameState.ActiveCustomerRequest is null;
	}

	private bool TryResolveActiveCustomerInteraction(out OccultShop.Models.CustomerInteractionDef? interaction)
	{
		interaction = null;
		var interactionId = _gameState.ActiveCustomerInteractionId;
		if (string.IsNullOrWhiteSpace(interactionId))
			interactionId = _gameState.ActiveCustomerRequest?.Id ?? string.Empty;
		if (string.IsNullOrWhiteSpace(interactionId))
			return false;

		foreach (var candidate in _dataDb.CustomerInteractions)
		{
			if (!string.Equals(candidate.Id, interactionId, StringComparison.OrdinalIgnoreCase))
				continue;

			interaction = candidate;
			return true;
		}

		GD.PushError($"DayController: Active customer interaction '{interactionId}' was not found in authored data.");
		return false;
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

		public void Restore(int customersServed, int successfulSales, int failedSales, int goldEarned, int dreadChange)
		{
			CustomersServed = Math.Max(0, customersServed);
			SuccessfulSales = Math.Max(0, successfulSales);
			FailedSales = Math.Max(0, failedSales);
			GoldEarned = goldEarned;
			DreadChange = dreadChange;
		}
	}
}
