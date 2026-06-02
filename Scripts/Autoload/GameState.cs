using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OccultShop.Models;
using OccultShop.Persistence;
using OccultShop.Tutorial;

namespace OccultShop.Autoload;

public partial class GameState : Node
{
	public const string StoryCustomerOutcomeArrived = "arrived";
	public const string StoryCustomerOutcomeSuccess = "success";
	public const string StoryCustomerOutcomeFailure = "failure";
	public const string StoryCustomerOutcomeSkipped = "skipped";

	[Export] public NodePath DataDbPath { get; set; } = new("/root/DataDb");
	[Export] public NodePath ItemCatalogPath { get; set; } = new("/root/ItemCatalog");

	public int Day { get; private set; } = 1;
	public int Gold { get; private set; } = 50000;
	public int Dread { get; private set; } = 0;
	public TutorialStatus TutorialProgressStatus { get; private set; } = TutorialStatus.NotStarted;
	public bool TutorialRequested => TutorialProgressStatus == TutorialStatus.InProgress;
	public bool TutorialCompleted => TutorialProgressStatus == TutorialStatus.Completed;
	public bool TutorialSkipped => TutorialProgressStatus == TutorialStatus.Skipped;
	public int TutorialStep { get; private set; }

	// itemId -> qty
	public Dictionary<string, int> Inventory { get; } = new();
	public HashSet<string> ActiveRules { get; } = new();
	public HashSet<string> StoryFlags { get; } = new(StringComparer.OrdinalIgnoreCase);
	public IReadOnlyDictionary<string, StoryCustomerVisitRecord> StoryCustomerVisits => _storyCustomerVisits;
	public HashSet<string> KnownPotions { get; } = new();
	public List<string> KnownPotionOrder { get; } = new();
	public Dictionary<string, string> PotionDisplayNames { get; } = new(StringComparer.OrdinalIgnoreCase);
	private static readonly (string ItemId, int Quantity)[] StartingInventory =
	{
		("grave_mint", 1),
		("obsidian_resin", 1),
		("iron_lullaby_root", 1)
	};
	private static readonly (string ItemId, int Quantity)[] NextCustomerTutorialInventory =
	{
		("grave_mint", 1),
		("obsidian_resin", 1),
		("iron_lullaby_root", 1),
		("black_ichor", 1),
		("lavender_ash", 1),
		("mooncap_mushroom", 1),
		("amber_nightshade", 1),
		("silver_thorn_bloom", 1),
		("moonwhisper_orchid", 1),
		("raven_ash_peony", 1)
	};
	private readonly Dictionary<string, int> _potionBasePrices = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, List<string>> _potionRecipes = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, string> _combinationPotionItems = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, Queue<List<string>>> _potionBatches = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, StoryCustomerVisitRecord> _storyCustomerVisits = new(StringComparer.OrdinalIgnoreCase);
	public CustomerRequestDef? ActiveCustomerRequest { get; private set; }

	public event Action? Changed;
	private ItemCatalogService _itemCatalog = default!;

	public override void _Ready()
	{
		var itemCatalog = GetNodeOrNull<ItemCatalogService>(ItemCatalogPath);
		if (itemCatalog is null)
		{
			GD.PushError($"GameState: ItemCatalog was not found at '{ItemCatalogPath}'.");
			return;
		}

		_itemCatalog = itemCatalog;
		ResetForNewGame();
	}

	public void ResetForNewGame()
	{
		Day = 1;
		Gold = 50000;
		Dread = 0;
		TutorialProgressStatus = TutorialStatus.NotStarted;
		TutorialStep = 0;
		Inventory.Clear();
		ActiveRules.Clear();
		StoryFlags.Clear();
		_storyCustomerVisits.Clear();
		KnownPotions.Clear();
		KnownPotionOrder.Clear();
		PotionDisplayNames.Clear();
		_potionBasePrices.Clear();
		_potionRecipes.Clear();
		_combinationPotionItems.Clear();
		_potionBatches.Clear();
		ActiveCustomerRequest = null;

		SeedStartingInventory();
		EmitChanged();
	}

	public void SeedNextCustomerTutorialInventory()
	{
		if (_itemCatalog is null)
		{
			GD.PushError("GameState: ItemCatalog is missing. Tutorial inventory could not be seeded.");
			return;
		}

		Inventory.Clear();
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
			Inventory = new Dictionary<string, int>(Inventory),
			ActiveRules = ActiveRules.ToList(),
			StoryFlags = StoryFlags.ToList(),
			KnownPotions = KnownPotionOrder.Count > 0 ? new List<string>(KnownPotionOrder) : KnownPotions.ToList(),
			KnownPotionOrder = new List<string>(KnownPotionOrder),
			PotionDisplayNames = new Dictionary<string, string>(PotionDisplayNames, StringComparer.OrdinalIgnoreCase),
			PotionBasePrices = new Dictionary<string, int>(_potionBasePrices, StringComparer.OrdinalIgnoreCase),
			PotionRecipes = ClonePotionRecipes(),
			CombinationPotionItems = new Dictionary<string, string>(_combinationPotionItems, StringComparer.OrdinalIgnoreCase),
			PotionBatches = ClonePotionBatches(),
			StoryCustomerVisits = CloneStoryCustomerVisits(),
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
		TutorialProgressStatus = ResolveTutorialStatus(snapshot);
		var restoredStep = snapshot.TutorialStepIndex > 0
			? snapshot.TutorialStepIndex
			: snapshot.TutorialStep;
		TutorialStep = Math.Max(0, restoredStep);

		Inventory.Clear();
		if (snapshot.Inventory is not null)
		{
			foreach (var pair in snapshot.Inventory)
			{
				if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value <= 0)
					continue;
				if (!_itemCatalog.TryGetItem(pair.Key, out _))
					continue;

				Inventory[pair.Key] = pair.Value;
			}
		}

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

		_storyCustomerVisits.Clear();
		if (snapshot.StoryCustomerVisits is not null)
		{
			foreach (var visit in snapshot.StoryCustomerVisits)
				RestoreStoryCustomerVisit(visit);
		}

		KnownPotions.Clear();
		if (snapshot.KnownPotions is not null)
		{
			foreach (var potionId in snapshot.KnownPotions)
			{
				if (!string.IsNullOrWhiteSpace(potionId))
					KnownPotions.Add(potionId);
			}
		}

		KnownPotionOrder.Clear();
		var potionOrderSource = snapshot.KnownPotionOrder is { Count: > 0 }
			? snapshot.KnownPotionOrder
			: snapshot.KnownPotions;
		if (potionOrderSource is not null)
		{
			foreach (var potionId in potionOrderSource)
			{
				if (string.IsNullOrWhiteSpace(potionId))
					continue;
				if (!KnownPotions.Contains(potionId))
					continue;
				if (KnownPotionOrder.Contains(potionId))
					continue;

				KnownPotionOrder.Add(potionId);
			}
		}

		foreach (var potionId in KnownPotions)
		{
			if (KnownPotionOrder.Contains(potionId))
				continue;

			KnownPotionOrder.Add(potionId);
		}

		PotionDisplayNames.Clear();
		if (snapshot.PotionDisplayNames is not null)
		{
			foreach (var pair in snapshot.PotionDisplayNames)
			{
				if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
					continue;

				PotionDisplayNames[pair.Key] = pair.Value;
			}
		}

		_potionBasePrices.Clear();
		if (snapshot.PotionBasePrices is not null)
		{
			foreach (var pair in snapshot.PotionBasePrices)
			{
				if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value < 0)
					continue;

				_potionBasePrices[pair.Key] = pair.Value;
			}
		}

		_potionRecipes.Clear();
		if (snapshot.PotionRecipes is not null)
		{
			foreach (var pair in snapshot.PotionRecipes)
			{
				if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value is null || pair.Value.Count == 0)
					continue;

				_potionRecipes[pair.Key] = new List<string>(pair.Value);
			}
		}

		_combinationPotionItems.Clear();
		if (snapshot.CombinationPotionItems is not null)
		{
			foreach (var pair in snapshot.CombinationPotionItems)
			{
				if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
					continue;

				_combinationPotionItems[pair.Key] = pair.Value;
			}
		}

		_potionBatches.Clear();
		if (snapshot.PotionBatches is not null)
		{
			foreach (var pair in snapshot.PotionBatches)
			{
				if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value is null || pair.Value.Count == 0)
					continue;

				var queue = new Queue<List<string>>();
				foreach (var batch in pair.Value)
				{
					if (batch is null || batch.Count == 0)
						continue;

					queue.Enqueue(new List<string>(batch));
				}

				if (queue.Count > 0)
					_potionBatches[pair.Key] = queue;
			}
		}

		ActiveCustomerRequest = CloneCustomerRequest(snapshot.ActiveCustomerRequest);
		EmitChanged();
	}

	public void NextDay()
	{
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
		var visitKey = BuildStoryCustomerVisitKey(interaction);
		return !string.IsNullOrWhiteSpace(visitKey) &&
			_storyCustomerVisits.TryGetValue(visitKey, out var visit) &&
			visit.HasArrived;
	}

	public void RecordStoryCustomerArrived(CustomerInteractionDef interaction)
	{
		var visitKey = BuildStoryCustomerVisitKey(interaction);
		if (string.IsNullOrWhiteSpace(visitKey))
			return;

		var visit = GetOrCreateStoryCustomerVisit(interaction, visitKey);
		visit.HasArrived = true;
		if (visit.ArrivalDay <= 0)
			visit.ArrivalDay = Day;
		if (string.IsNullOrWhiteSpace(visit.LastOutcome))
			visit.LastOutcome = StoryCustomerOutcomeArrived;

		EmitChanged();
	}

	public void RecordStoryCustomerInteractionOutcome(CustomerInteractionDef interaction, string outcome)
	{
		var visitKey = BuildStoryCustomerVisitKey(interaction);
		if (string.IsNullOrWhiteSpace(visitKey))
			return;

		var normalizedOutcome = NormalizeStoryCustomerOutcome(outcome);
		var visit = GetOrCreateStoryCustomerVisit(interaction, visitKey);
		visit.HasArrived = true;
		if (visit.ArrivalDay <= 0)
			visit.ArrivalDay = Day;
		visit.LastOutcome = normalizedOutcome;
		visit.OutcomeDay = Day;

		EmitChanged();
	}

	public void RequestTutorial()
	{
		TutorialProgressStatus = TutorialStatus.InProgress;
		TutorialStep = 0;
		EmitChanged();
	}

	public void SkipTutorial()
	{
		TutorialProgressStatus = TutorialStatus.Skipped;
		TutorialStep = 0;
		EmitChanged();
	}

	public void CompleteTutorial()
	{
		TutorialProgressStatus = TutorialStatus.Completed;
		TutorialStep = 0;
		EmitChanged();
	}

	public void SetTutorialStep(int step)
	{
		var normalizedStep = Math.Max(0, step);
		if (TutorialStep == normalizedStep)
			return;

		TutorialStep = normalizedStep;
		EmitChanged();
	}

	private static TutorialStatus ResolveTutorialStatus(GameStateSnapshot snapshot)
	{
		if (snapshot.TutorialStatus is TutorialStatus explicitStatus)
			return NormalizeTutorialStatus(explicitStatus);

		if (snapshot.TutorialCompleted)
			return TutorialStatus.Completed;
		if (snapshot.TutorialSkipped)
			return TutorialStatus.Skipped;
		if (snapshot.TutorialRequested)
			return TutorialStatus.InProgress;

		return TutorialStatus.NotStarted;
	}

	private static TutorialStatus NormalizeTutorialStatus(TutorialStatus status)
	{
		return status switch
		{
			TutorialStatus.InProgress => TutorialStatus.InProgress,
			TutorialStatus.Completed => TutorialStatus.Completed,
			TutorialStatus.Skipped => TutorialStatus.Skipped,
			_ => TutorialStatus.NotStarted
		};
	}

	public bool HasItem(string itemId, int qty)
		=> Inventory.TryGetValue(itemId, out var have) && have >= qty;

	public void AddItem(string itemId, int qty)
	{
		if (qty <= 0 || string.IsNullOrWhiteSpace(itemId))
			return;
		if (!_itemCatalog.TryGetItem(itemId, out _))
		{
			GD.PushError($"GameState: Cannot add unknown item '{itemId}' to inventory.");
			return;
		}

		Inventory[itemId] = Inventory.GetValueOrDefault(itemId) + qty;
		EmitChanged();
	}

	public bool ConsumeItem(string itemId, int qty)
	{
		if (qty <= 0) return true;
		if (!HasItem(itemId, qty)) return false;
		Inventory[itemId] -= qty;
		ConsumePotionBatches(itemId, qty);
		if (Inventory[itemId] <= 0) Inventory.Remove(itemId);
		EmitChanged();
		return true;
	}

	public void LearnPotion(string potionId)
	{
		if (string.IsNullOrWhiteSpace(potionId))
			return;

		var knownPotionAdded = KnownPotions.Add(potionId);
		var orderAdded = false;
		if (!KnownPotionOrder.Contains(potionId))
		{
			KnownPotionOrder.Add(potionId);
			orderAdded = true;
		}

		if (knownPotionAdded || orderAdded)
			EmitChanged();
	}

	public bool KnowsPotion(string potionId) => KnownPotions.Contains(potionId);

	public void RecordPotionRecipe(string potionItemId, IReadOnlyList<string> ingredientIds)
	{
		if (string.IsNullOrWhiteSpace(potionItemId) || ingredientIds is null || ingredientIds.Count == 0)
			return;

		if (!_potionRecipes.ContainsKey(potionItemId))
			_potionRecipes[potionItemId] = new List<string>(ingredientIds);

		LearnPotion(potionItemId);
	}

	public bool TryGetPotionRecipe(string potionItemId, out List<string> ingredientIds)
	{
		ingredientIds = new List<string>();
		if (!_potionRecipes.TryGetValue(potionItemId, out var stored))
			return false;

		ingredientIds = new List<string>(stored);
		return true;
	}

	public void SetPotionDisplayName(string potionId, string displayName)
	{
		if (string.IsNullOrWhiteSpace(potionId) || string.IsNullOrWhiteSpace(displayName))
			return;

		PotionDisplayNames[potionId] = displayName;
		EmitChanged();
	}

	public void RegisterPotionBasePrice(string potionId, int basePrice)
	{
		if (string.IsNullOrWhiteSpace(potionId) || basePrice < 0)
			return;

		if (_potionBasePrices.ContainsKey(potionId))
			return;

		_potionBasePrices[potionId] = basePrice;
		EmitChanged();
	}

	public bool TryGetPotionBasePrice(string potionId, out int basePrice)
	{
		return _potionBasePrices.TryGetValue(potionId, out basePrice);
	}

	public string? GetPotionDisplayName(string potionId)
	{
		return PotionDisplayNames.TryGetValue(potionId, out var displayName) ? displayName : null;
	}

	public bool TryGetPotionForCombination(string combinationKey, out string potionItemId)
	{
		return _combinationPotionItems.TryGetValue(combinationKey, out potionItemId!);
	}

	public void SetPotionForCombination(string combinationKey, string potionItemId)
	{
		if (string.IsNullOrWhiteSpace(combinationKey) || string.IsNullOrWhiteSpace(potionItemId))
			return;

		_combinationPotionItems[combinationKey] = potionItemId;
	}

	public void RecordPotionBatch(string potionItemId, IReadOnlyList<string> ingredientIds)
	{
		if (string.IsNullOrWhiteSpace(potionItemId) || ingredientIds is null || ingredientIds.Count == 0)
			return;

		if (!_potionBatches.TryGetValue(potionItemId, out var queue))
		{
			queue = new Queue<List<string>>();
			_potionBatches[potionItemId] = queue;
		}

		queue.Enqueue(new List<string>(ingredientIds));
	}

	public bool TryPeekPotionBatch(string potionItemId, out List<string> ingredientIds)
	{
		ingredientIds = new List<string>();
		if (!_potionBatches.TryGetValue(potionItemId, out var queue) || queue.Count == 0)
			return false;

		ingredientIds = new List<string>(queue.Peek());
		return true;
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

		Inventory[itemId] = Inventory.GetValueOrDefault(itemId) + qty;
	}

	private static bool IsIngredient(ItemDef item)
	{
		if (item.Tags is null)
			return false;

		return item.Tags.Any(tag => string.Equals(tag, "ingredient", StringComparison.OrdinalIgnoreCase));
	}

	private Dictionary<string, List<string>> ClonePotionRecipes()
	{
		var copy = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
		foreach (var pair in _potionRecipes)
			copy[pair.Key] = new List<string>(pair.Value);

		return copy;
	}

	private Dictionary<string, List<List<string>>> ClonePotionBatches()
	{
		var copy = new Dictionary<string, List<List<string>>>(StringComparer.OrdinalIgnoreCase);
		foreach (var pair in _potionBatches)
			copy[pair.Key] = pair.Value.Select(batch => new List<string>(batch)).ToList();

		return copy;
	}

	private List<StoryCustomerVisitRecord> CloneStoryCustomerVisits()
	{
		var visits = new List<StoryCustomerVisitRecord>(_storyCustomerVisits.Count);
		foreach (var visit in _storyCustomerVisits.Values)
			visits.Add(CloneStoryCustomerVisit(visit));

		return visits;
	}

	private static StoryCustomerVisitRecord CloneStoryCustomerVisit(StoryCustomerVisitRecord visit)
	{
		return new StoryCustomerVisitRecord
		{
			VisitKey = visit.VisitKey,
			StoryCharacterId = visit.StoryCharacterId,
			VisitId = visit.VisitId,
			InteractionId = visit.InteractionId,
			ScheduledDay = visit.ScheduledDay,
			HasArrived = visit.HasArrived,
			ArrivalDay = visit.ArrivalDay,
			LastOutcome = visit.LastOutcome,
			OutcomeDay = visit.OutcomeDay
		};
	}

	private void RestoreStoryCustomerVisit(StoryCustomerVisitRecord? visit)
	{
		if (visit is null)
			return;

		var visitKey = string.IsNullOrWhiteSpace(visit.VisitKey)
			? BuildStoryCustomerVisitKey(visit.StoryCharacterId, visit.VisitId, visit.InteractionId)
			: visit.VisitKey;
		if (string.IsNullOrWhiteSpace(visitKey))
			return;

		_storyCustomerVisits[visitKey] = new StoryCustomerVisitRecord
		{
			VisitKey = visitKey,
			StoryCharacterId = visit.StoryCharacterId,
			VisitId = string.IsNullOrWhiteSpace(visit.VisitId) ? visit.InteractionId : visit.VisitId,
			InteractionId = visit.InteractionId,
			ScheduledDay = Math.Max(0, visit.ScheduledDay),
			HasArrived = visit.HasArrived,
			ArrivalDay = Math.Max(0, visit.ArrivalDay),
			LastOutcome = NormalizeStoryCustomerOutcome(visit.LastOutcome),
			OutcomeDay = Math.Max(0, visit.OutcomeDay)
		};
	}

	private StoryCustomerVisitRecord GetOrCreateStoryCustomerVisit(CustomerInteractionDef interaction, string visitKey)
	{
		if (_storyCustomerVisits.TryGetValue(visitKey, out var visit))
			return visit;

		visit = new StoryCustomerVisitRecord
		{
			VisitKey = visitKey,
			StoryCharacterId = interaction.StoryCharacterId,
			VisitId = interaction.GetStoryVisitId(),
			InteractionId = interaction.Id,
			ScheduledDay = ResolveStoryCustomerScheduledDay(interaction)
		};
		_storyCustomerVisits[visitKey] = visit;
		return visit;
	}

	private int ResolveStoryCustomerScheduledDay(CustomerInteractionDef interaction)
	{
		if (interaction.Requires?.DayExact is int dayExact)
			return Math.Max(1, dayExact);
		if (interaction.Requires?.DayMin is int dayMin)
			return Math.Max(1, dayMin);

		return Day;
	}

	private static string BuildStoryCustomerVisitKey(CustomerInteractionDef interaction)
	{
		if (!interaction.IsStoryInteraction)
			return string.Empty;

		return BuildStoryCustomerVisitKey(interaction.StoryCharacterId, interaction.GetStoryVisitId(), interaction.Id);
	}

	private static string BuildStoryCustomerVisitKey(string storyCharacterId, string visitId, string interactionId)
	{
		if (string.IsNullOrWhiteSpace(storyCharacterId))
			return string.Empty;

		var resolvedVisitId = string.IsNullOrWhiteSpace(visitId) ? interactionId : visitId;
		if (string.IsNullOrWhiteSpace(resolvedVisitId))
			return string.Empty;

		return $"{storyCharacterId.Trim()}:{resolvedVisitId.Trim()}";
	}

	private static string NormalizeStoryCustomerOutcome(string? outcome)
	{
		if (string.IsNullOrWhiteSpace(outcome))
			return StoryCustomerOutcomeArrived;

		return outcome.Trim().ToLowerInvariant();
	}

	private static CustomerRequestDef? CloneCustomerRequest(CustomerRequestDef? request)
	{
		if (request is null)
			return null;

		return new CustomerRequestDef
		{
			Id = request.Id,
			Description = request.Description,
			DesiredTraits = request.DesiredTraits is null ? new Dictionary<string, int>() : new Dictionary<string, int>(request.DesiredTraits),
			BadTraits = request.BadTraits is null ? new Dictionary<string, int>() : new Dictionary<string, int>(request.BadTraits)
		};
	}

	private void ConsumePotionBatches(string itemId, int qty)
	{
		if (!_potionBatches.TryGetValue(itemId, out var queue) || queue.Count == 0)
			return;

		for (var i = 0; i < qty && queue.Count > 0; i++)
			queue.Dequeue();

		if (queue.Count == 0)
			_potionBatches.Remove(itemId);
	}
}
