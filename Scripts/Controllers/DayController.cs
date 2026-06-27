using System;
using Godot;
using OccultShop.Autoload;
using OccultShop.Infrastructure;
using OccultShop.Systems;

namespace OccultShop.Controllers;

public partial class DayController : Node
{
	private const int MaxCustomersPerShopDay = 3;
	private const string OpeningMotherPotionItemId = "potion_gravekeepers_balm";

	[Export] public NodePath CustomerEventControllerPath = default!;
	[Export] public NodePath StationCustomerPanelPath = default!;
	[Export] public NodePath BrewPanelPath = default!;
	[Export] public NodePath DaySummaryPanelPath = default!;
	[Export] public NodePath DataDbPath = new(AutoloadNodePaths.DataDb);
	[Export] public NodePath GameStatePath = new(AutoloadNodePaths.GameState);
	[Export] public NodePath SceneTransitionPath = new(AutoloadNodePaths.SceneTransition);
	[Export] public NodePath ShopSessionStatePath = new(AutoloadNodePaths.ShopSessionState);

	private CustomerEventController _customerEventController = default!;
	private UI.StationCustomerPanel _stationCustomerPanel = default!;
	private UI.BrewPanel _brewPanel = default!;
	private UI.DaySummaryPanel _daySummaryPanel = default!;
	private readonly ShopDayStats _shopDayStats = new();
	private DataDb _dataDb = default!;
	private GameState _gameState = default!;
	private ShopSessionState _shopSessionState = default!;
	private SceneTransition? _sceneTransition;
	private int _customersArrived;
	private bool _closeShopAfterCurrentCustomer;
	private bool _isShopDayReadyToEnd;
	private bool _openingMotherServeSucceededForCutscene;
	private bool _tenYearsLaterCutsceneTransitionStarted;

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
		if (!NodeLookup.TryGetRequiredNode<ShopSessionState>(
			this,
			ShopSessionStatePath,
			nameof(DayController),
			nameof(ShopSessionStatePath),
			out _shopSessionState))
		{
			return;
		}

		_sceneTransition = GetNodeOrNull<SceneTransition>(SceneTransitionPath);
		if (_sceneTransition is null)
			GD.PushError($"DayController: SceneTransition was not found at '{SceneTransitionPath}'.");

		_stationCustomerPanel.SaleResolved += OnStationCustomerSaleResolved;
		_stationCustomerPanel.PotionSold += OnStationPotionSold;
		_stationCustomerPanel.CustomerSkipped += OnStationCustomerSkipped;
		_stationCustomerPanel.CustomerResolved += OnStationCustomerResolved;
		_stationCustomerPanel.CustomerQueueEmptied += OnStationCustomerQueueEmptied;
		_stationCustomerPanel.MotherPostServeDialogueResolved += OnMotherPostServeDialogueResolved;
		_daySummaryPanel.ContinuePressed += OnSummaryContinuePressed;
		_daySummaryPanel.HidePanel();
		Callable.From(RestoreShopDayState).CallDeferred();
	}

	public override void _ExitTree()
	{
		if (_stationCustomerPanel != null)
		{
			_stationCustomerPanel.SaleResolved -= OnStationCustomerSaleResolved;
			_stationCustomerPanel.PotionSold -= OnStationPotionSold;
			_stationCustomerPanel.CustomerSkipped -= OnStationCustomerSkipped;
			_stationCustomerPanel.CustomerResolved -= OnStationCustomerResolved;
			_stationCustomerPanel.CustomerQueueEmptied -= OnStationCustomerQueueEmptied;
			_stationCustomerPanel.MotherPostServeDialogueResolved -= OnMotherPostServeDialogueResolved;
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
		_shopSessionState.BeginShopDayState();
		_openingMotherServeSucceededForCutscene = false;
		_tenYearsLaterCutsceneTransitionStarted = false;
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

		RequestCloseShopAfterCurrentCustomer();
	}

	public void CloseShopDayForStoryCutscene()
	{
		if (!IsShopOpen && !_shopSessionState.IsShopDayOpen)
			return;

		IsShopOpen = false;
		_customersArrived = 0;
		_closeShopAfterCurrentCustomer = false;
		_isShopDayReadyToEnd = false;
		_openingMotherServeSucceededForCutscene = false;
		_stationCustomerPanel.ClearCustomers();
		_brewPanel.HidePanel();
		_daySummaryPanel.HidePanel();
		_shopSessionState.CloseShopDayState();
		EmitShopStateChanged();
	}

	private void RequestCloseShopAfterCurrentCustomer()
	{
		if (_closeShopAfterCurrentCustomer && _shopSessionState.CloseShopAfterCurrentCustomer)
			return;

		_closeShopAfterCurrentCustomer = true;
		_shopSessionState.RequestCloseShopAfterCurrentCustomer();
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
			_shopSessionState,
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

		_shopSessionState.RecordShopDaySale(success, goldDelta, dreadDelta);
		_gameState.TryUnlockGardenAfterShopSales(_shopSessionState.ShopDayCustomersServed);
		EmitShopStateChanged();
	}

	private void OnStationPotionSold(string itemId, bool success)
	{
		if (!IsShopOpen)
			return;
		if (!string.Equals(_shopSessionState.ActiveCustomerInteractionId, MotherPostServeDialogueFlow.OpeningMotherInteractionId, StringComparison.OrdinalIgnoreCase))
			return;

		_openingMotherServeSucceededForCutscene =
			success &&
			string.Equals(itemId, OpeningMotherPotionItemId, StringComparison.OrdinalIgnoreCase);
		RequestCloseShopAfterCurrentCustomer();
	}

	private void OnMotherPostServeDialogueResolved()
	{
		if (!ShouldStartTenYearsLaterCutscene())
			return;

		StartTenYearsLaterCutscene();
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

		_shopSessionState.ClearActiveShopCustomer();
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

		var interaction = _customerEventController.DrawShopDayCustomerInteraction(_dataDb, _gameState, _shopSessionState);
		if (interaction is null)
		{
			_stationCustomerPanel.ClearCustomers();
			return false;
		}

		var customers = new System.Collections.Generic.List<OccultShop.Models.CustomerInteractionDef> { interaction };
		_shopSessionState.RecordShopDayCustomerArrived(interaction);
		_customersArrived = _shopSessionState.ShopDayCustomersArrived;
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
		_openingMotherServeSucceededForCutscene = false;
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
		_shopSessionState.CloseShopDayState();

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

	private bool ShouldStartTenYearsLaterCutscene()
	{
		return _openingMotherServeSucceededForCutscene &&
			!_tenYearsLaterCutsceneTransitionStarted &&
			!_gameState.HasStoryFlag(GameState.TenYearsLaterCutsceneStartedStoryFlag) &&
			!_gameState.HasStoryFlag(GameState.TenYearsLaterCutsceneCompletedStoryFlag);
	}

	private void StartTenYearsLaterCutscene()
	{
		if (_sceneTransition is null)
		{
			GD.PushError("DayController: SceneTransition is missing; cannot start the ten years later cutscene.");
			return;
		}

		_tenYearsLaterCutsceneTransitionStarted = true;
		_openingMotherServeSucceededForCutscene = false;
		CloseShopDayForStoryCutscene();
		_sceneTransition.ChangeSceneWithFade(ScenePaths.TenYearsLaterCutscene);
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
		IsShopOpen = _shopSessionState.IsShopDayOpen;
		_customersArrived = _shopSessionState.ShopDayCustomersArrived;
		_closeShopAfterCurrentCustomer = _shopSessionState.CloseShopAfterCurrentCustomer;
		_isShopDayReadyToEnd = false;
		_shopDayStats.Restore(
			_shopSessionState.ShopDayCustomersServed,
			_shopSessionState.ShopDaySuccessfulSales,
			_shopSessionState.ShopDayFailedSales,
			_shopSessionState.ShopDayGoldEarned,
			_shopSessionState.ShopDayDreadChange);

		if (!IsShopOpen)
		{
			if (ShouldAutoStartOpeningShopDay())
			{
				StartShopDay();
				return;
			}

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
			string.IsNullOrWhiteSpace(_shopSessionState.ActiveCustomerInteractionId) &&
			_shopSessionState.ActiveCustomerRequest is null;
	}

	private bool TryResolveActiveCustomerInteraction(out OccultShop.Models.CustomerInteractionDef? interaction)
	{
		interaction = null;
		var interactionId = _shopSessionState.ActiveCustomerInteractionId;
		if (string.IsNullOrWhiteSpace(interactionId))
			interactionId = _shopSessionState.ActiveCustomerRequest?.Id ?? string.Empty;
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

	private bool ShouldAutoStartOpeningShopDay()
	{
		return _gameState.Day == 1 &&
			_gameState.HasStoryFlag(GameState.IntroCutsceneCompletedStoryFlag) &&
			_gameState.HasStoryFlag(GameState.NewGameOpeningCustomerPendingStoryFlag) &&
			string.IsNullOrWhiteSpace(_shopSessionState.ActiveCustomerInteractionId) &&
			_shopSessionState.ActiveCustomerRequest is null;
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
