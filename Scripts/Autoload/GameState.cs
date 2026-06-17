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
	public Dictionary<string, string> PotionDisplayNames { get; } = new(StringComparer.OrdinalIgnoreCase);
	public IReadOnlyDictionary<string, int> SeedInventory => _gardenState.SeedInventory;
	public IReadOnlyList<GardenPotState> GardenPots => _gardenState.GardenPots;
	public IReadOnlyList<GardenCropDef> GardenCrops => _gardenState.GardenCrops;
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
	private readonly PotionKnowledgeState _potionKnowledgeState;
	private readonly InventoryState _inventoryState;
	private readonly GardenState _gardenState;
	private readonly PotionBatchStore _potionBatchStore = new();
	private readonly StoryCustomerVisitState _storyCustomerVisitState = new();
	private readonly TutorialProgressState _tutorialProgressState = new();
	public CustomerRequestDef? ActiveCustomerRequest { get; private set; }

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
		StoryFlags.Clear();
		StoryFlags.Add(BridgetWelcomePendingStoryFlag);
		_storyCustomerVisitState.Clear();
		_potionKnowledgeState.Clear();
		_potionBatchStore.Clear();
		_gardenState.InitializeNewGarden();
		ActiveCustomerRequest = null;

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
			ActiveRules = ActiveRules.ToList(),
			StoryFlags = StoryFlags.ToList(),
			KnownPotions = _potionKnowledgeState.BuildKnownPotionSnapshot(),
			KnownPotionOrder = _potionKnowledgeState.CloneKnownPotionOrder(),
			KnownIngredients = _potionKnowledgeState.BuildKnownIngredientSnapshot(),
			KnownIngredientOrder = _potionKnowledgeState.CloneKnownIngredientOrder(),
			KnownIngredientPreparations = _potionKnowledgeState.BuildKnownIngredientPreparationSnapshot(),
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

		ActiveRules.Clear();
		if (snapshot.ActiveRules is not null)
		{
			foreach (var ruleId in snapshot.ActiveRules)
			{
				if (!string.IsNullOrWhiteSpace(ruleId))
					ActiveRules.Add(ruleId);
			}
		}

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

		ActiveCustomerRequest = CloneCustomerRequest(snapshot.ActiveCustomerRequest);
		BackfillKnownIngredients();
		EmitChanged();
	}

	public void NextDay()
	{
		_gardenState.AdvanceGrowth();
		Day += 1;
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

	public void SetActiveCustomerRequest(CustomerRequestDef? request)
	{
		ActiveCustomerRequest = request;
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

	private static CustomerRequestDef? CloneCustomerRequest(CustomerRequestDef? request)
	{
		if (request is null)
			return null;

		return new CustomerRequestDef
		{
			Id = request.Id,
			Description = request.Description,
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
