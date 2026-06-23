using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OccultShop.Models;
using OccultShop.Persistence;
using OccultShop.Systems;
using OccultShop.Tutorial;

namespace OccultShop.Autoload;

public partial class GameState : Node
{
	public const string StoryCustomerOutcomeArrived = "arrived";
	public const string StoryCustomerOutcomeSuccess = "success";
	public const string StoryCustomerOutcomeFailure = "failure";
	public const string StoryCustomerOutcomeSkipped = "skipped";
	public const string NewGameOpeningCustomerPendingStoryFlag = "new_game_opening_customer_pending";
	public const string NewGameSecondCustomerPendingStoryFlag = "new_game_second_customer_pending";
	public const string NewGameThirdCustomerPendingStoryFlag = "new_game_third_customer_pending";
	public const string DayTwoFirstCustomerPendingStoryFlag = "day_two_first_customer_pending";
	public const string DayTwoSecondCustomerPendingStoryFlag = "day_two_second_customer_pending";
	public const string DayTwoThirdCustomerPendingStoryFlag = "day_two_third_customer_pending";
	public const string GardenUnlockedStoryFlag = "garden_unlocked";
	// Legacy flag retained so old pre-shop saves route to the current opening request.
	public const string BridgetWelcomePendingStoryFlag = "bridget_welcome_pending";
	public const int StartingGardenPotCount = GardenState.StartingPotCount;
	public const int DefaultGardenHarvestYield = GardenState.DefaultHarvestYield;
	public const int MaxUniquePotionInventoryQuantity = 4;
	public const int MaxPotionStackQuantity = 10;
	public const int MaxUniqueConsumableInventoryQuantity = 4;
	public const int MaxConsumableStackQuantity = 10;

	[Export] public NodePath DataDbPath { get; set; } = new(AutoloadNodePaths.DataDb);
	[Export] public NodePath ItemCatalogPath { get; set; } = new(AutoloadNodePaths.ItemCatalog);

	public int Day { get; private set; } = 1;
	public int Gold { get; private set; } = 50000;
	public int Dread { get; private set; } = 0;
	public TutorialStatus TutorialProgressStatus => _tutorialProgressState.Status;
	public bool TutorialRequested => _tutorialProgressState.Requested;
	public bool TutorialCompleted => _tutorialProgressState.Completed;
	public bool TutorialSkipped => _tutorialProgressState.Skipped;
	public int TutorialStep => _tutorialProgressState.Step;
	public string PendingConsumableItemId => _inventoryState.PendingConsumableItemId;
	public int PendingConsumableQuantity => _inventoryState.PendingConsumableQuantity;
	public bool HasPendingConsumableGrant => _inventoryState.HasPendingConsumableGrant;
	public bool DebugSkipBoilingMiniGame { get; private set; }

	// itemId -> qty
	public Dictionary<string, int> Inventory { get; } = new();
	public HashSet<string> ActiveRules { get; } = new();
	public HashSet<string> StoryFlags { get; } = new(StringComparer.OrdinalIgnoreCase);
	public IReadOnlyDictionary<string, StoryCustomerVisitRecord> StoryCustomerVisits => _storyCustomerVisitState.Visits;
	public HashSet<string> KnownPotions { get; } = new();
	public List<string> KnownPotionOrder { get; } = new();
	public HashSet<string> KnownIngredients { get; } = new(StringComparer.OrdinalIgnoreCase);
	public List<string> KnownIngredientOrder { get; } = new();
	public HashSet<string> KnownIngredientPreparations { get; } = new(StringComparer.OrdinalIgnoreCase);
	public HashSet<string> DisabledIngredientPreparationMethods { get; } = new(StringComparer.OrdinalIgnoreCase);
	public Dictionary<string, string> PotionDisplayNames { get; } = new(StringComparer.OrdinalIgnoreCase);
	public IReadOnlyDictionary<string, int> SeedInventory => _gardenState.SeedInventory;
	public IReadOnlyList<GardenPotState> GardenPots => _gardenState.GardenPots;
	public IReadOnlyList<GardenCropDef> GardenCrops => _gardenState.GardenCrops;
	public bool IsGardenUnlocked => HasStoryFlag(GardenUnlockedStoryFlag);
	private static readonly (string ItemId, int Quantity)[] StartingInventory =
	{
		("mint", 1),
		("gorse", 1),
		("thyme", 1)
	};
	private static readonly (string ItemId, int Quantity)[] NextCustomerTutorialInventory =
	{
		("mint", 1),
		("gorse", 1),
		("thyme", 1),
		("elder", 1),
		("rosemary", 1),
		("heather", 1),
		("yarrow", 1),
		("willow", 1),
		("juniper", 1),
		("comfrey", 1)
	};
	private static readonly string[] NewGameDisabledIngredientPreparationMethods =
	{
		IngredientPreparationCatalog.SteepedPreparationId,
		IngredientPreparationCatalog.CrushedPreparationId,
		IngredientPreparationCatalog.BoiledPreparationId
	};
	private readonly PotionKnowledgeState _potionKnowledgeState;
	private readonly InventoryState _inventoryState;
	private readonly GardenState _gardenState;
	private readonly List<IngredientPortionDef> _queuedBrewIngredients = new();
	private readonly PotionBatchStore _potionBatchStore = new();
	private readonly StoryCustomerVisitState _storyCustomerVisitState = new();
	private readonly TutorialProgressState _tutorialProgressState = new();
	public CustomerRequestDef? ActiveCustomerRequest { get; private set; }
	public bool IsShopDayOpen { get; private set; }
	public int ShopDayCustomersArrived { get; private set; }
	public int ShopDayCustomersServed { get; private set; }
	public int ShopDaySuccessfulSales { get; private set; }
	public int ShopDayFailedSales { get; private set; }
	public int ShopDayGoldEarned { get; private set; }
	public int ShopDayDreadChange { get; private set; }
	public bool CloseShopAfterCurrentCustomer { get; private set; }
	public string ActiveCustomerInteractionId { get; private set; } = string.Empty;

	public event Action? Changed;
	private ItemCatalogService _itemCatalog = default!;
	private DataDb? _dataDb;

	public GameState()
	{
		_potionKnowledgeState = new PotionKnowledgeState(
			KnownPotions,
			KnownPotionOrder,
			KnownIngredients,
			KnownIngredientOrder,
			KnownIngredientPreparations,
			PotionDisplayNames,
			ResolveKnownIngredientIdOrNull);
		_inventoryState = new InventoryState(
			Inventory,
			ItemExists,
			IsPotionItem,
			IsConsumableItem,
			IsIngredientItem,
			PushGameStateError,
			MaxUniquePotionInventoryQuantity,
			MaxPotionStackQuantity,
			MaxUniqueConsumableInventoryQuantity,
			MaxConsumableStackQuantity);
		_gardenState = new GardenState(ItemExists, PushGameStateError);
	}

	public override void _Ready()
	{
		var itemCatalog = GetNodeOrNull<ItemCatalogService>(ItemCatalogPath);
		if (itemCatalog is null)
		{
			GD.PushError($"GameState: ItemCatalog was not found at '{ItemCatalogPath}'.");
			return;
		}

		_itemCatalog = itemCatalog;
		_dataDb = GetNodeOrNull<DataDb>(DataDbPath);
		if (_dataDb is null)
			GD.PushError($"GameState: DataDb was not found at '{DataDbPath}'. Ingredient book starting flags could not be seeded.");

		ResetForNewGame();
	}

	public void ResetForNewGame()
	{
		Day = 1;
		Gold = 50000;
		Dread = 0;
		_tutorialProgressState.Reset();
		_inventoryState.Clear();
		ActiveRules.Clear();
		ResetIngredientPreparationMethodLocksForNewGame();
		StoryFlags.Clear();
		StoryFlags.Add(NewGameOpeningCustomerPendingStoryFlag);
		StoryFlags.Add(NewGameSecondCustomerPendingStoryFlag);
		StoryFlags.Add(NewGameThirdCustomerPendingStoryFlag);
		StoryFlags.Add(DayTwoFirstCustomerPendingStoryFlag);
		StoryFlags.Add(DayTwoSecondCustomerPendingStoryFlag);
		StoryFlags.Add(DayTwoThirdCustomerPendingStoryFlag);
		_storyCustomerVisitState.Clear();
		_potionKnowledgeState.Clear();
		_queuedBrewIngredients.Clear();
		_potionBatchStore.Clear();
		_gardenState.InitializeNewGarden();
		ResetShopDayState();

		SeedStartingInventory();
		SeedStartingIngredientBookKnowledge();
		EmitChanged();
	}

	public void SeedNextCustomerTutorialInventory()
	{
		if (_itemCatalog is null)
		{
			GD.PushError("GameState: ItemCatalog is missing. Tutorial inventory could not be seeded.");
			return;
		}

		_inventoryState.Clear();
		foreach (var (itemId, qty) in NextCustomerTutorialInventory)
		{
			if (!_itemCatalog.TryGetItem(itemId, out _))
			{
				GD.PushError($"GameState: Cannot seed unknown tutorial item '{itemId}'.");
				continue;
			}

			AddStartingStack(itemId, qty);
		}

		EmitChanged();
	}

	public GameStateSnapshot BuildSnapshot()
	{
		var snapshot = new GameStateSnapshot
		{
			Day = Day,
			Gold = Gold,
			Dread = Dread,
			TutorialStatus = TutorialProgressStatus,
			TutorialStepIndex = TutorialStep,
			TutorialRequested = TutorialRequested,
			TutorialCompleted = TutorialCompleted,
			TutorialSkipped = TutorialSkipped,
			TutorialStep = TutorialStep,
			Inventory = _inventoryState.CloneInventory(),
			PendingConsumableItemId = PendingConsumableItemId,
			PendingConsumableQuantity = PendingConsumableQuantity,
			QueuedBrewIngredients = CloneIngredientPortions(_queuedBrewIngredients),
			ActiveRules = ActiveRules.ToList(),
			StoryFlags = StoryFlags.ToList(),
			KnownPotions = _potionKnowledgeState.BuildKnownPotionSnapshot(),
			KnownPotionOrder = _potionKnowledgeState.CloneKnownPotionOrder(),
			KnownIngredients = _potionKnowledgeState.BuildKnownIngredientSnapshot(),
			KnownIngredientOrder = _potionKnowledgeState.CloneKnownIngredientOrder(),
			KnownIngredientPreparations = _potionKnowledgeState.BuildKnownIngredientPreparationSnapshot(),
			DisabledIngredientPreparationMethods = BuildDisabledIngredientPreparationMethodSnapshot(),
			PotionDisplayNames = _potionKnowledgeState.ClonePotionDisplayNames(),
			PotionBasePrices = _potionKnowledgeState.ClonePotionBasePrices(),
			PotionRecipes = _potionKnowledgeState.ClonePotionRecipes(),
			CombinationPotionItems = _potionKnowledgeState.CloneCombinationPotionItems(),
			PotionBatches = _potionBatchStore.ClonePotionBatches(),
			PotionIngredientPortionBatches = _potionBatchStore.ClonePotionIngredientPortionBatches(),
			GardenInitialized = true,
			GardenPotCount = _gardenState.PotCount,
			SeedInventory = _gardenState.CloneSeedInventory(),
			GardenPots = _gardenState.CloneGardenPots(),
			StoryCustomerVisits = _storyCustomerVisitState.CloneStoryCustomerVisits(),
			IsShopDayOpen = IsShopDayOpen,
			ShopDayCustomersArrived = ShopDayCustomersArrived,
			ShopDayCustomersServed = ShopDayCustomersServed,
			ShopDaySuccessfulSales = ShopDaySuccessfulSales,
			ShopDayFailedSales = ShopDayFailedSales,
			ShopDayGoldEarned = ShopDayGoldEarned,
			ShopDayDreadChange = ShopDayDreadChange,
			CloseShopAfterCurrentCustomer = CloseShopAfterCurrentCustomer,
			ActiveCustomerInteractionId = ActiveCustomerInteractionId,
			ActiveCustomerRequest = CloneCustomerRequest(ActiveCustomerRequest)
		};

		return snapshot;
	}

	public void ApplySnapshot(GameStateSnapshot? snapshot)
	{
		if (snapshot is null)
		{
			GD.PushError("GameState: Cannot apply a null snapshot.");
			return;
		}

		Day = Math.Max(1, snapshot.Day);
		Gold = Math.Max(0, snapshot.Gold);
		Dread = Math.Clamp(snapshot.Dread, 0, 100);
		_tutorialProgressState.ApplySnapshot(snapshot);

		_inventoryState.Restore(snapshot.Inventory, snapshot.PendingConsumableItemId, snapshot.PendingConsumableQuantity);
		RestoreQueuedBrewIngredients(snapshot.QueuedBrewIngredients);

		ActiveRules.Clear();
		if (snapshot.ActiveRules is not null)
		{
			foreach (var ruleId in snapshot.ActiveRules)
			{
				if (!string.IsNullOrWhiteSpace(ruleId))
					ActiveRules.Add(ruleId);
			}
		}

		RestoreDisabledIngredientPreparationMethods(snapshot.DisabledIngredientPreparationMethods);

		StoryFlags.Clear();
		if (snapshot.StoryFlags is not null)
		{
			foreach (var storyFlag in snapshot.StoryFlags)
			{
				if (!string.IsNullOrWhiteSpace(storyFlag))
					StoryFlags.Add(storyFlag);
			}
		}

		_storyCustomerVisitState.Restore(snapshot.StoryCustomerVisits, StoryCustomerOutcomeArrived);

		_potionKnowledgeState.Restore(snapshot);

		_potionBatchStore.Restore(snapshot.PotionBatches, snapshot.PotionIngredientPortionBatches);

		_gardenState.Restore(snapshot.GardenInitialized, snapshot.SeedInventory, snapshot.GardenPots, snapshot.GardenPotCount);

		IsShopDayOpen = snapshot.IsShopDayOpen;
		ShopDayCustomersArrived = Math.Max(0, snapshot.ShopDayCustomersArrived);
		ShopDayCustomersServed = Math.Max(0, snapshot.ShopDayCustomersServed);
		ShopDaySuccessfulSales = Math.Max(0, snapshot.ShopDaySuccessfulSales);
		ShopDayFailedSales = Math.Max(0, snapshot.ShopDayFailedSales);
		ShopDayGoldEarned = snapshot.ShopDayGoldEarned;
		ShopDayDreadChange = snapshot.ShopDayDreadChange;
		CloseShopAfterCurrentCustomer = snapshot.CloseShopAfterCurrentCustomer;
		ActiveCustomerInteractionId = string.IsNullOrWhiteSpace(snapshot.ActiveCustomerInteractionId)
			? string.Empty
			: snapshot.ActiveCustomerInteractionId.Trim();
		ActiveCustomerRequest = CloneCustomerRequest(snapshot.ActiveCustomerRequest);
		if (string.IsNullOrWhiteSpace(ActiveCustomerInteractionId) && ActiveCustomerRequest is not null)
			ActiveCustomerInteractionId = ActiveCustomerRequest.Id.Trim();
		if (!IsShopDayOpen && !string.IsNullOrWhiteSpace(ActiveCustomerInteractionId))
			IsShopDayOpen = true;
		if (IsShopDayOpen && ShopDayCustomersArrived == 0 && !string.IsNullOrWhiteSpace(ActiveCustomerInteractionId))
			ShopDayCustomersArrived = 1;
		BackfillKnownIngredients();
		EmitChanged();
	}

	public void NextDay()
	{
		_gardenState.AdvanceGrowth();
		Day += 1;
		if (Day == 2)
		{
			StoryFlags.Add(DayTwoFirstCustomerPendingStoryFlag);
			StoryFlags.Add(DayTwoSecondCustomerPendingStoryFlag);
			StoryFlags.Add(DayTwoThirdCustomerPendingStoryFlag);
		}
		EmitChanged();
	}

	public void AddGold(int amount)
	{
		Gold = Math.Max(0, Gold + amount);
		EmitChanged();
	}

	public void AddDread(int amount)
	{
		Dread = Math.Clamp(Dread + amount, 0, 100);
		EmitChanged();
	}

	public void AddRule(string ruleId)
	{
		if (string.IsNullOrWhiteSpace(ruleId)) return;
		ActiveRules.Add(ruleId);
		EmitChanged();
	}

	public bool HasStoryFlag(string storyFlag)
	{
		return !string.IsNullOrWhiteSpace(storyFlag) && StoryFlags.Contains(storyFlag);
	}

	public void AddStoryFlag(string storyFlag)
	{
		if (string.IsNullOrWhiteSpace(storyFlag))
			return;

		if (StoryFlags.Add(storyFlag))
			EmitChanged();
	}

	public void RemoveStoryFlag(string storyFlag)
	{
		if (string.IsNullOrWhiteSpace(storyFlag))
			return;

		if (StoryFlags.Remove(storyFlag))
			EmitChanged();
	}

	public bool HasStoryCustomerVisitArrived(CustomerInteractionDef interaction)
	{
		return _storyCustomerVisitState.HasStoryCustomerVisitArrived(interaction);
	}

	public void RecordStoryCustomerArrived(CustomerInteractionDef interaction)
	{
		if (_storyCustomerVisitState.RecordStoryCustomerArrived(interaction, Day, StoryCustomerOutcomeArrived))
			EmitChanged();
	}

	public void RecordStoryCustomerInteractionOutcome(CustomerInteractionDef interaction, string outcome)
	{
		if (_storyCustomerVisitState.RecordStoryCustomerInteractionOutcome(interaction, outcome, Day, StoryCustomerOutcomeArrived))
			EmitChanged();
	}

	public bool HasStoryCustomerDialogueOptionSelected(CustomerInteractionDef interaction, string optionId)
	{
		return _storyCustomerVisitState.HasStoryCustomerDialogueOptionSelected(interaction, optionId);
	}

	public void RecordStoryCustomerDialogueOptionSelected(CustomerInteractionDef interaction, string optionId)
	{
		if (_storyCustomerVisitState.RecordStoryCustomerDialogueOptionSelected(interaction, optionId, Day))
			EmitChanged();
	}

	public void RequestTutorial()
	{
		_tutorialProgressState.Request();
		EmitChanged();
	}

	public void SkipTutorial()
	{
		_tutorialProgressState.Skip();
		EmitChanged();
	}

	public void CompleteTutorial()
	{
		_tutorialProgressState.Complete();
		EmitChanged();
	}

	public void SetTutorialStep(int step)
	{
		if (_tutorialProgressState.SetStep(step))
			EmitChanged();
	}

	public bool HasItem(string itemId, int qty)
		=> _inventoryState.HasItem(itemId, qty);

	public void AddItem(string itemId, int qty)
	{
		var result = _inventoryState.AddItem(itemId, qty);
		if (result.AddedQuantity > 0)
			AddKnownIngredient(itemId, emitChanged: false);
		if (result.Changed)
			EmitChanged();
	}

	public void RestockItemToMinimum(string itemId, int qty)
	{
		var result = _inventoryState.RestockItemToMinimum(itemId, qty);
		if (result.AddedQuantity > 0)
			AddKnownIngredient(itemId, emitChanged: false);
		if (result.Changed)
			EmitChanged();
	}

	public bool ConsumeItem(string itemId, int qty)
	{
		if (qty <= 0)
			return true;
		if (!_inventoryState.ConsumeItem(itemId, qty))
			return false;

		_potionBatchStore.ConsumePotionBatches(itemId, qty);
		EmitChanged();
		return true;
	}

	public bool TryAcceptPendingConsumableByDiscarding(string discardItemId, out string error)
	{
		var result = _inventoryState.TryAcceptPendingConsumableByDiscarding(discardItemId, out error);
		if (result.Changed)
			EmitChanged();
		return result.Accepted;
	}

	public void DeclinePendingConsumableGrant()
	{
		if (_inventoryState.DeclinePendingConsumableGrant())
			EmitChanged();
	}

	public int ConsumeEachIngredient(int qty)
	{
		var consumedCount = _inventoryState.ConsumeEachIngredient(qty);
		if (consumedCount > 0)
			EmitChanged();

		return consumedCount;
	}

	public List<IngredientPortionDef> CloneQueuedBrewIngredients()
	{
		return CloneIngredientPortions(_queuedBrewIngredients);
	}

	public void SetQueuedBrewIngredients(IReadOnlyList<IngredientPortionDef> ingredientPortions)
	{
		_queuedBrewIngredients.Clear();
		_queuedBrewIngredients.AddRange(CloneIngredientPortions(ingredientPortions));
		EmitChanged();
	}

	public void ClearQueuedBrewIngredients()
	{
		if (_queuedBrewIngredients.Count == 0)
			return;

		_queuedBrewIngredients.Clear();
		EmitChanged();
	}

	public static string BuildSeedId(string ingredientId)
	{
		return GardenState.BuildSeedId(ingredientId);
	}

	public bool TryGetGardenCropBySeedId(string seedId, out GardenCropDef crop)
	{
		return _gardenState.TryGetCropBySeedId(seedId, out crop);
	}

	public bool TryGetGardenCropByIngredientId(string ingredientId, out GardenCropDef crop)
	{
		return _gardenState.TryGetCropByIngredientId(ingredientId, out crop);
	}

	public int GetSeedQuantity(string seedId)
	{
		return _gardenState.GetSeedQuantity(seedId);
	}

	public void AddSeed(string seedId, int quantity)
	{
		if (quantity <= 0 || string.IsNullOrWhiteSpace(seedId))
			return;
		if (!_gardenState.IsKnownSeed(seedId))
		{
			GD.PushError($"GameState: Cannot add unknown garden seed '{seedId}'.");
			return;
		}

		if (_gardenState.AddSeed(seedId, quantity))
			EmitChanged();
	}

	public bool TryPlantSeed(int potIndex, string seedId, out string error)
	{
		if (!_gardenState.TryPlantSeed(potIndex, seedId, Day, out var plantedIngredientId, out error))
			return false;

		AddKnownIngredient(plantedIngredientId, emitChanged: false);
		EmitChanged();
		return true;
	}

	public bool TryHarvestGardenPot(int potIndex, out string error)
	{
		if (!_gardenState.TryHarvestGardenPot(potIndex, out var harvest, out error))
			return false;

		_inventoryState.AddRawStack(harvest.IngredientId, harvest.Quantity);
		AddKnownIngredient(harvest.IngredientId, emitChanged: false);

		EmitChanged();
		return true;
	}

	public void SetUnlockedGardenPotCount(int potCount)
	{
		if (_gardenState.SetUnlockedPotCount(potCount))
			EmitChanged();
	}

	public void LearnPotion(string potionId)
	{
		if (_potionKnowledgeState.LearnPotion(potionId))
			EmitChanged();
	}

	public bool KnowsPotion(string potionId) => _potionKnowledgeState.KnowsPotion(potionId);

	public void ForgetPotion(string potionId)
	{
		if (_potionKnowledgeState.ForgetPotion(potionId))
			EmitChanged();
	}

	public void LearnIngredient(string ingredientId)
	{
		AddKnownIngredient(ingredientId, emitChanged: true);
	}

	public bool KnowsIngredient(string ingredientId)
	{
		return _potionKnowledgeState.KnowsIngredient(ingredientId);
	}

	public void LearnIngredientPreparation(string ingredientId, string preparationId)
	{
		if (_potionKnowledgeState.AddKnownIngredientPreparation(ingredientId, preparationId))
			EmitChanged();
	}

	public bool KnowsIngredientPreparation(string ingredientId, string preparationId)
	{
		return _potionKnowledgeState.KnowsIngredientPreparation(ingredientId, preparationId);
	}

	public bool KnowsAnyIngredientPreparation(string ingredientId)
	{
		return _potionKnowledgeState.KnowsAnyIngredientPreparation(ingredientId);
	}

	public bool IsIngredientPreparationMethodEnabled(string preparationId)
	{
		var normalizedPreparationId = IngredientPreparationCatalog.NormalizePreparationId(preparationId);
		if (string.IsNullOrWhiteSpace(normalizedPreparationId))
			return false;
		if (IsRawPreparation(normalizedPreparationId))
			return true;
		if (!IngredientPreparationCatalog.IsKnownPreparationId(normalizedPreparationId))
			return false;

		return !DisabledIngredientPreparationMethods.Contains(normalizedPreparationId);
	}

	public bool AreNonRawIngredientPreparationMethodsEnabled()
	{
		foreach (var option in IngredientPreparationCatalog.AllOptions)
		{
			if (IsRawPreparation(option.Id))
				continue;
			if (DisabledIngredientPreparationMethods.Contains(option.Id))
				return false;
		}

		return true;
	}

	public void SetNonRawIngredientPreparationMethodsEnabled(bool enabled)
	{
		var changed = false;
		foreach (var option in IngredientPreparationCatalog.AllOptions)
		{
			if (IsRawPreparation(option.Id))
				continue;

			changed |= enabled
				? DisabledIngredientPreparationMethods.Remove(option.Id)
				: DisabledIngredientPreparationMethods.Add(option.Id);
		}

		if (changed)
			EmitChanged();
	}

	public void SetIngredientPreparationMethodEnabled(string preparationId, bool enabled)
	{
		var normalizedPreparationId = IngredientPreparationCatalog.NormalizePreparationId(preparationId);
		if (string.IsNullOrWhiteSpace(normalizedPreparationId))
			return;
		if (IsRawPreparation(normalizedPreparationId))
			return;
		if (!IngredientPreparationCatalog.IsKnownPreparationId(normalizedPreparationId))
		{
			GD.PushError($"GameState: Cannot toggle unknown ingredient preparation method '{preparationId}'.");
			return;
		}

		var changed = enabled
			? DisabledIngredientPreparationMethods.Remove(normalizedPreparationId)
			: DisabledIngredientPreparationMethods.Add(normalizedPreparationId);

		if (changed)
			EmitChanged();
	}

	public void SetDebugSkipBoilingMiniGame(bool enabled)
	{
		if (DebugSkipBoilingMiniGame == enabled)
			return;

		DebugSkipBoilingMiniGame = enabled;
		EmitChanged();
	}

	public bool KnowsItemIngredientPreparation(string itemId)
	{
		return TryResolveIngredientPreparation(itemId, out var ingredientId, out var preparationId) &&
			KnowsIngredientPreparation(ingredientId, preparationId);
	}

	public bool KnowsAnyItemIngredientPreparation(string itemId)
	{
		return TryResolveKnownIngredientId(itemId, out var ingredientId) &&
			KnowsAnyIngredientPreparation(ingredientId);
	}

	public void RecordIngredientPreparationKnowledge(IEnumerable<IngredientPortionDef> ingredientPortions)
	{
		if (ingredientPortions is null)
			return;

		var changed = false;
		foreach (var portion in ingredientPortions)
		{
			if (portion is null)
				continue;

			var ingredientId = string.IsNullOrWhiteSpace(portion.IngredientId)
				? portion.InventoryItemId
				: portion.IngredientId;
			changed |= _potionKnowledgeState.AddKnownIngredientPreparation(ingredientId, portion.PreparationId);
		}

		if (changed)
			EmitChanged();
	}

	public void RecordIngredientPreparationKnowledge(IEnumerable<string> itemIds)
	{
		if (itemIds is null)
			return;

		var changed = false;
		foreach (var itemId in itemIds)
		{
			if (!TryResolveIngredientPreparation(itemId, out var ingredientId, out var preparationId))
				continue;

			changed |= _potionKnowledgeState.AddKnownIngredientPreparation(ingredientId, preparationId);
		}

		if (changed)
			EmitChanged();
	}

	public void UnlockIngredientPreparationForCurrentInventory(string preparationId)
	{
		var normalizedPreparationId = IngredientPreparationCatalog.NormalizePreparationId(preparationId);
		if (string.IsNullOrWhiteSpace(normalizedPreparationId))
			return;
		if (!IngredientPreparationCatalog.IsKnownPreparationId(normalizedPreparationId))
		{
			GD.PushError($"GameState: Cannot unlock unknown ingredient preparation knowledge '{preparationId}'.");
			return;
		}

		var changed = false;
		foreach (var pair in Inventory)
		{
			if (pair.Value <= 0)
				continue;
			if (!TryGetPreparationForCurrentInventoryItem(pair.Key, normalizedPreparationId, out var ingredientId))
				continue;

			changed |= _potionKnowledgeState.AddKnownIngredientPreparation(ingredientId, normalizedPreparationId);
		}

		if (changed)
			EmitChanged();
	}

	public void UnlockAllIngredientPreparations(IEnumerable<ItemDef> items)
	{
		if (items is null)
			return;

		var changed = false;
		foreach (var item in items)
		{
			if (item is null || item.Treatment is not null || !IsIngredient(item))
				continue;
			if (item.Preparations is null || item.Preparations.Count == 0)
				continue;

			changed |= AddKnownIngredient(item.Id, emitChanged: false);
			foreach (var preparationId in item.Preparations.Keys)
				changed |= _potionKnowledgeState.AddKnownIngredientPreparation(item.Id, preparationId);
		}

		if (changed)
			EmitChanged();
	}

	public void ForgetIngredient(string ingredientId)
	{
		if (_potionKnowledgeState.ForgetIngredient(ingredientId))
			EmitChanged();
	}

	public void RecordPotionRecipe(string potionItemId, IReadOnlyList<string> ingredientIds)
	{
		if (_potionKnowledgeState.RecordPotionRecipe(potionItemId, ingredientIds))
			EmitChanged();
	}

	public bool TryGetPotionRecipe(string potionItemId, out List<string> ingredientIds)
	{
		return _potionKnowledgeState.TryGetPotionRecipe(potionItemId, out ingredientIds);
	}

	public void SetPotionDisplayName(string potionId, string displayName)
	{
		if (_potionKnowledgeState.SetPotionDisplayName(potionId, displayName))
			EmitChanged();
	}

	public void RegisterPotionBasePrice(string potionId, int basePrice)
	{
		if (_potionKnowledgeState.RegisterPotionBasePrice(potionId, basePrice))
			EmitChanged();
	}

	public bool TryGetPotionBasePrice(string potionId, out int basePrice)
	{
		return _potionKnowledgeState.TryGetPotionBasePrice(potionId, out basePrice);
	}

	public string? GetPotionDisplayName(string potionId)
	{
		return _potionKnowledgeState.GetPotionDisplayName(potionId);
	}

	public bool TryGetPotionForCombination(string combinationKey, out string potionItemId)
	{
		return _potionKnowledgeState.TryGetPotionForCombination(combinationKey, out potionItemId);
	}

	public void SetPotionForCombination(string combinationKey, string potionItemId)
	{
		_potionKnowledgeState.SetPotionForCombination(combinationKey, potionItemId);
	}

	public void RecordPotionBatch(string potionItemId, IReadOnlyList<string> ingredientIds)
	{
		_potionBatchStore.RecordPotionBatch(potionItemId, ingredientIds);
	}

	public void RecordPotionBatch(string potionItemId, IReadOnlyList<IngredientPortionDef> ingredientPortions)
	{
		_potionBatchStore.RecordPotionBatch(potionItemId, ingredientPortions);
	}

	public bool TryPeekPotionBatch(string potionItemId, out List<string> ingredientIds)
	{
		return _potionBatchStore.TryPeekPotionBatch(potionItemId, out ingredientIds);
	}

	public bool TryPeekPotionIngredientPortionBatch(string potionItemId, out List<IngredientPortionDef> ingredientPortions)
	{
		return _potionBatchStore.TryPeekPotionIngredientPortionBatch(potionItemId, out ingredientPortions);
	}

	public void BeginShopDayState()
	{
		IsShopDayOpen = true;
		ShopDayCustomersArrived = 0;
		ShopDayCustomersServed = 0;
		ShopDaySuccessfulSales = 0;
		ShopDayFailedSales = 0;
		ShopDayGoldEarned = 0;
		ShopDayDreadChange = 0;
		CloseShopAfterCurrentCustomer = false;
		ActiveCustomerInteractionId = string.Empty;
		ActiveCustomerRequest = null;
		EmitChanged();
	}

	public void CloseShopDayState()
	{
		if (!IsShopDayOpen &&
			ShopDayCustomersArrived == 0 &&
			string.IsNullOrWhiteSpace(ActiveCustomerInteractionId) &&
			ActiveCustomerRequest is null)
		{
			return;
		}

		ResetShopDayState();
		EmitChanged();
	}

	public void RecordShopDayCustomerArrived(CustomerInteractionDef interaction)
	{
		if (interaction is null || string.IsNullOrWhiteSpace(interaction.Id))
		{
			GD.PushError("GameState: Cannot record an active shop customer without an interaction id.");
			return;
		}

		ActiveCustomerInteractionId = interaction.Id.Trim();
		ShopDayCustomersArrived += 1;
		EmitChanged();
	}

	public void ClearActiveShopCustomer()
	{
		if (string.IsNullOrWhiteSpace(ActiveCustomerInteractionId))
			return;

		ActiveCustomerInteractionId = string.Empty;
		EmitChanged();
	}

	public void RecordShopDaySale(bool success, int goldDelta, int dreadDelta)
	{
		ShopDayCustomersServed += 1;
		if (success)
			ShopDaySuccessfulSales += 1;
		else
			ShopDayFailedSales += 1;

		ShopDayGoldEarned += goldDelta;
		ShopDayDreadChange += dreadDelta;
		UnlockGardenIfReady();
		EmitChanged();
	}

	public void RequestCloseShopAfterCurrentCustomer()
	{
		if (CloseShopAfterCurrentCustomer)
			return;

		CloseShopAfterCurrentCustomer = true;
		EmitChanged();
	}

	public void SetActiveCustomerRequest(CustomerRequestDef? request)
	{
		ActiveCustomerRequest = request;
		EnsureActiveShopCustomerForRequest(request);
		EmitChanged();
	}

	public void ClearActiveCustomerRequest()
	{
		if (ActiveCustomerRequest is null)
			return;

		ActiveCustomerRequest = null;
		EmitChanged();
	}

	private void EmitChanged() => Changed?.Invoke();

	private void UnlockGardenIfReady()
	{
		if (Day != 2 || ShopDayCustomersServed < 3)
			return;

		StoryFlags.Add(GardenUnlockedStoryFlag);
	}

	private void SeedStartingInventory()
	{
		if (_itemCatalog is null)
		{
			GD.PushError("GameState: ItemCatalog is missing. Starting inventory could not be seeded.");
			return;
		}

		foreach (var (itemId, qty) in StartingInventory)
		{
			if (!_itemCatalog.TryGetItem(itemId, out _))
			{
				GD.PushError($"GameState: Cannot seed unknown starting item '{itemId}'.");
				continue;
			}

			AddStartingStack(itemId, qty);
		}
	}

	private void AddStartingStack(string itemId, int qty)
	{
		if (qty <= 0 || string.IsNullOrWhiteSpace(itemId))
			return;

		if (_inventoryState.AddRawStack(itemId, qty))
			AddKnownIngredient(itemId, emitChanged: false);
	}

	private void SeedStartingIngredientBookKnowledge()
	{
		if (_dataDb is null)
			return;

		foreach (var item in _dataDb.Items.Values)
		{
			if (item is null || !item.StartsKnownInIngredientBook)
				continue;
			if (!IsIngredient(item) || item.Treatment is not null)
				continue;

			AddKnownIngredient(item.Id, emitChanged: false);
		}
	}

	private void BackfillKnownIngredients()
	{
		SeedStartingIngredientBookKnowledge();
		BackfillKnownIngredientsFromInventory();
		BackfillKnownIngredientsFromGardenPots();
		_potionKnowledgeState.BackfillKnownIngredientsFromKnownRecipes();
	}

	private void ResetIngredientPreparationMethodLocksForNewGame()
	{
		DisabledIngredientPreparationMethods.Clear();
		foreach (var preparationId in NewGameDisabledIngredientPreparationMethods)
			DisabledIngredientPreparationMethods.Add(preparationId);
	}

	private List<string> BuildDisabledIngredientPreparationMethodSnapshot()
	{
		return DisabledIngredientPreparationMethods
			.OrderBy(preparationId => preparationId, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	private void RestoreDisabledIngredientPreparationMethods(IEnumerable<string>? disabledPreparationMethods)
	{
		DisabledIngredientPreparationMethods.Clear();
		if (disabledPreparationMethods is null)
			return;

		foreach (var preparationId in disabledPreparationMethods)
		{
			var normalizedPreparationId = IngredientPreparationCatalog.NormalizePreparationId(preparationId);
			if (string.IsNullOrWhiteSpace(normalizedPreparationId))
				continue;
			if (IsRawPreparation(normalizedPreparationId))
				continue;
			if (!IngredientPreparationCatalog.IsKnownPreparationId(normalizedPreparationId))
				continue;

			DisabledIngredientPreparationMethods.Add(normalizedPreparationId);
		}
	}

	private void RestoreQueuedBrewIngredients(IReadOnlyList<IngredientPortionDef>? ingredientPortions)
	{
		_queuedBrewIngredients.Clear();
		_queuedBrewIngredients.AddRange(CloneIngredientPortions(ingredientPortions));
	}

	private static List<IngredientPortionDef> CloneIngredientPortions(IReadOnlyList<IngredientPortionDef>? ingredientPortions)
	{
		var clones = new List<IngredientPortionDef>();
		if (ingredientPortions is null)
			return clones;

		foreach (var ingredientPortion in ingredientPortions)
		{
			if (ingredientPortion is null)
				continue;

			var ingredientId = ingredientPortion.IngredientId?.Trim() ?? string.Empty;
			var itemId = ingredientPortion.ItemId?.Trim() ?? string.Empty;
			if (string.IsNullOrWhiteSpace(ingredientId) && string.IsNullOrWhiteSpace(itemId))
				continue;

			clones.Add(new IngredientPortionDef
			{
				IngredientId = string.IsNullOrWhiteSpace(ingredientId) ? itemId : ingredientId,
				ItemId = itemId,
				PreparationId = IngredientPreparationCatalog.NormalizePreparationId(ingredientPortion.PreparationId),
				Grams = Math.Max(0, ingredientPortion.Grams)
			});
		}

		return clones;
	}

	private void BackfillKnownIngredientsFromInventory()
	{
		foreach (var pair in Inventory)
		{
			if (pair.Value <= 0)
				continue;

			AddKnownIngredient(pair.Key, emitChanged: false);
		}
	}

	private void BackfillKnownIngredientsFromGardenPots()
	{
		foreach (var pot in GardenPots)
		{
			if (pot is null || pot.IsEmpty)
				continue;

			AddKnownIngredient(pot.IngredientId, emitChanged: false);
		}
	}

	private bool AddKnownIngredient(string ingredientId, bool emitChanged)
	{
		var changed = _potionKnowledgeState.AddKnownIngredient(ingredientId);
		if (changed && emitChanged)
			EmitChanged();

		return changed;
	}

	private string? ResolveKnownIngredientIdOrNull(string itemId)
	{
		return TryResolveKnownIngredientId(itemId, out var knownIngredientId) ? knownIngredientId : null;
	}

	private bool TryResolveKnownIngredientId(string itemId, out string knownIngredientId)
	{
		knownIngredientId = string.Empty;
		if (string.IsNullOrWhiteSpace(itemId))
			return false;
		if (_itemCatalog is null || !_itemCatalog.TryGetItem(itemId, out var item))
			return false;
		if (IngredientPreparationCatalog.TryGetPreparedIngredientInfo(item, out var preparedBaseIngredientId, out _))
		{
			knownIngredientId = preparedBaseIngredientId;
			return true;
		}

		if (IsIngredient(item) && item.Treatment is null)
		{
			knownIngredientId = item.Id;
			return true;
		}

		var baseItemId = item.Treatment?.BaseItemId;
		if (string.IsNullOrWhiteSpace(baseItemId))
			return false;
		if (!_itemCatalog.TryGetItem(baseItemId, out var baseItem))
			return false;
		if (IngredientPreparationCatalog.TryGetPreparedIngredientInfo(baseItem, out var treatedPreparedBaseIngredientId, out _))
		{
			knownIngredientId = treatedPreparedBaseIngredientId;
			return true;
		}

		if (!IsIngredient(baseItem) || baseItem.Treatment is not null)
			return false;

		knownIngredientId = baseItem.Id;
		return true;
	}

	private bool TryGetPreparationForCurrentInventoryItem(
		string itemId,
		string preparationId,
		out string ingredientId)
	{
		ingredientId = string.Empty;
		if (!TryResolveKnownIngredientId(itemId, out var knownIngredientId))
			return false;
		if (_itemCatalog is null || !_itemCatalog.TryGetItem(knownIngredientId, out var ingredient))
			return false;
		if (!IsIngredient(ingredient) || ingredient.Treatment is not null)
			return false;
		if (!IngredientPreparationCatalog.TryGetPreparation(ingredient, preparationId, out _))
			return false;

		ingredientId = ingredient.Id;
		return true;
	}

	public bool TryResolveIngredientPreparation(
		string itemId,
		out string ingredientId,
		out string preparationId)
	{
		ingredientId = string.Empty;
		preparationId = string.Empty;
		if (string.IsNullOrWhiteSpace(itemId))
			return false;
		if (_itemCatalog is null || !_itemCatalog.TryGetItem(itemId, out var item))
			return false;
		if (IngredientPreparationCatalog.TryGetPreparedIngredientInfo(item, out ingredientId, out preparationId))
			return true;

		var baseItemId = item.Treatment?.BaseItemId;
		if (string.IsNullOrWhiteSpace(baseItemId))
			return false;
		if (!_itemCatalog.TryGetItem(baseItemId, out var baseItem))
			return false;

		return IngredientPreparationCatalog.TryGetPreparedIngredientInfo(baseItem, out ingredientId, out preparationId);
	}

	public int CountOwnedUniquePotions()
	{
		return _inventoryState.CountPotionStacks();
	}

	public int CountOwnedUniqueConsumables()
	{
		return _inventoryState.CountConsumableStacks();
	}

	private static bool IsIngredient(ItemDef item)
	{
		if (item.Tags is null)
			return false;

		return item.Tags.Any(tag => string.Equals(tag, ItemTags.Ingredient, StringComparison.OrdinalIgnoreCase));
	}

	private static bool IsRawPreparation(string preparationId)
	{
		return string.Equals(
			IngredientPreparationCatalog.NormalizePreparationId(preparationId),
			IngredientPreparationCatalog.RawPreparationId,
			StringComparison.OrdinalIgnoreCase);
	}

	private bool ItemExists(string itemId)
	{
		return _itemCatalog.TryGetItem(itemId, out _);
	}

	private bool IsPotionItem(string itemId)
	{
		return _itemCatalog.IsPotion(itemId);
	}

	private bool IsConsumableItem(string itemId)
	{
		return _itemCatalog.IsConsumable(itemId);
	}

	private bool IsIngredientItem(string itemId)
	{
		return _itemCatalog.IsIngredient(itemId);
	}

	private static void PushGameStateError(string message)
	{
		GD.PushError(message);
	}

	private void ResetShopDayState()
	{
		IsShopDayOpen = false;
		ShopDayCustomersArrived = 0;
		ShopDayCustomersServed = 0;
		ShopDaySuccessfulSales = 0;
		ShopDayFailedSales = 0;
		ShopDayGoldEarned = 0;
		ShopDayDreadChange = 0;
		CloseShopAfterCurrentCustomer = false;
		ActiveCustomerInteractionId = string.Empty;
		ActiveCustomerRequest = null;
	}

	private void EnsureActiveShopCustomerForRequest(CustomerRequestDef? request)
	{
		if (request is null || string.IsNullOrWhiteSpace(request.Id))
			return;

		var requestId = request.Id.Trim();
		IsShopDayOpen = true;
		if (ShopDayCustomersArrived == 0)
			ShopDayCustomersArrived = 1;

		if (!string.Equals(ActiveCustomerInteractionId, requestId, StringComparison.OrdinalIgnoreCase))
			ActiveCustomerInteractionId = requestId;
	}

	private static CustomerRequestDef? CloneCustomerRequest(CustomerRequestDef? request)
	{
		if (request is null)
			return null;

		return new CustomerRequestDef
		{
			Id = request.Id,
			Description = request.Description,
			HideRequestDetails = request.HideRequestDetails,
			DesiredTraits = CustomerTraitRangeDef.CloneDictionary(request.DesiredTraits),
			BadTraits = CustomerTraitRangeDef.CloneDictionary(request.BadTraits),
			RequiredMinTraits = request.RequiredMinTraits is null ? new Dictionary<string, int>() : new Dictionary<string, int>(request.RequiredMinTraits),
			RequiredMaxTraits = request.RequiredMaxTraits is null ? new Dictionary<string, int>() : new Dictionary<string, int>(request.RequiredMaxTraits),
			RequiredIngredientAmounts = request.RequiredIngredientAmounts is null
				? new List<IngredientPortionDef>()
				: request.RequiredIngredientAmounts.Select(x => x.Clone()).ToList()
		};
	}

}
