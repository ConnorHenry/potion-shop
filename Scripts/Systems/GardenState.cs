using System;
using System.Collections.Generic;
using System.Linq;
using OccultShop.Models;

namespace OccultShop.Systems;

public readonly struct GardenHarvestResult
{
	public GardenHarvestResult(string ingredientId, string seedId, int quantity)
	{
		IngredientId = ingredientId;
		SeedId = seedId;
		Quantity = quantity;
	}

	public string IngredientId { get; }
	public string SeedId { get; }
	public int Quantity { get; }
}

public sealed class GardenState
{
	public const int StartingPotCount = 3;
	public const int DefaultHarvestYield = 2;

	private static readonly GardenCropDef[] CropDefinitions =
	{
		CreateGardenCrop("yarrow", growthDays: 1),
		CreateGardenCrop("gorse", growthDays: 2),
		CreateGardenCrop("thyme", growthDays: 3),
		CreateGardenCrop("heather", growthDays: 1),
		CreateGardenCrop("mint", growthDays: 2),
		CreateGardenCrop("elder", growthDays: 1),
		CreateGardenCrop("rosemary", growthDays: 3),
		CreateGardenCrop("willow", growthDays: 2),
		CreateGardenCrop("juniper", growthDays: 3),
		CreateGardenCrop("comfrey", growthDays: 1)
	};
	private static readonly Dictionary<string, GardenCropDef> CropsByIngredientId = CropDefinitions
		.ToDictionary(x => x.IngredientId, x => x, StringComparer.OrdinalIgnoreCase);
	private static readonly Dictionary<string, GardenCropDef> CropsBySeedId = CropDefinitions
		.ToDictionary(x => x.SeedId, x => x, StringComparer.OrdinalIgnoreCase);
	private static readonly (string SeedId, int Quantity)[] StartingSeedInventory =
	{
		("seed_yarrow", 1),
		("seed_gorse", 1),
		("seed_thyme", 1)
	};

	private readonly Func<string, bool> _itemExists;
	private readonly Action<string> _pushError;
	private readonly Dictionary<string, int> _seedInventory = new(StringComparer.OrdinalIgnoreCase);
	private readonly List<GardenPotState> _gardenPots = new();
	private readonly Random _gardenYieldRandom = new();

	public GardenState(Func<string, bool> itemExists, Action<string> pushError)
	{
		_itemExists = itemExists;
		_pushError = pushError;
	}

	public IReadOnlyDictionary<string, int> SeedInventory => _seedInventory;
	public IReadOnlyList<GardenPotState> GardenPots => _gardenPots;
	public IReadOnlyList<GardenCropDef> GardenCrops => CropDefinitions;
	public int PotCount => _gardenPots.Count;

	public static string BuildSeedId(string ingredientId)
	{
		return string.IsNullOrWhiteSpace(ingredientId)
			? string.Empty
			: $"seed_{ingredientId.Trim()}";
	}

	public bool TryGetCropBySeedId(string seedId, out GardenCropDef crop)
	{
		return CropsBySeedId.TryGetValue(seedId, out crop!);
	}

	public bool TryGetCropByIngredientId(string ingredientId, out GardenCropDef crop)
	{
		return CropsByIngredientId.TryGetValue(ingredientId, out crop!);
	}

	public void InitializeNewGarden()
	{
		Clear();
		EnsurePotCount(StartingPotCount);
		SeedStartingSeedInventory();
	}

	public void Clear()
	{
		_seedInventory.Clear();
		_gardenPots.Clear();
	}

	public Dictionary<string, int> CloneSeedInventory()
	{
		return new Dictionary<string, int>(_seedInventory, StringComparer.OrdinalIgnoreCase);
	}

	public List<GardenPotState> CloneGardenPots()
	{
		var pots = new List<GardenPotState>(_gardenPots.Count);
		foreach (var pot in _gardenPots)
			pots.Add(CloneGardenPot(pot));

		return pots;
	}

	public void Restore(bool gardenInitialized, Dictionary<string, int>? seedInventory, List<GardenPotState>? gardenPots, int gardenPotCount)
	{
		Clear();
		if (gardenInitialized)
		{
			RestoreSeedInventory(seedInventory);
			RestoreGardenPots(gardenPots, gardenPotCount);
			return;
		}

		InitializeNewGarden();
	}

	public void AdvanceGrowth()
	{
		foreach (var pot in _gardenPots)
		{
			if (pot.IsEmpty)
				continue;
			if (pot.DaysGrown >= pot.RequiredGrowthDays)
				continue;

			pot.DaysGrown += 1;
		}
	}

	public int GetSeedQuantity(string seedId)
	{
		return _seedInventory.TryGetValue(seedId, out var quantity) ? quantity : 0;
	}

	public bool AddSeed(string seedId, int quantity)
	{
		if (quantity <= 0 || string.IsNullOrWhiteSpace(seedId))
			return false;
		if (!CropsBySeedId.ContainsKey(seedId))
			return false;

		AddSeedStack(seedId, quantity);
		return true;
	}

	public bool IsKnownSeed(string seedId)
	{
		return CropsBySeedId.ContainsKey(seedId);
	}

	public bool TryPlantSeed(int potIndex, string seedId, int day, out string plantedIngredientId, out string error)
	{
		plantedIngredientId = string.Empty;
		error = string.Empty;
		if (!TryGetPot(potIndex, out var pot))
		{
			error = "Garden pot is missing.";
			return false;
		}

		if (!pot.IsEmpty)
		{
			error = "Garden pot is already planted.";
			return false;
		}

		if (!CropsBySeedId.TryGetValue(seedId, out var crop))
		{
			error = "Seed cannot be planted.";
			return false;
		}

		if (GetSeedQuantity(seedId) <= 0)
		{
			error = "No seeds available.";
			return false;
		}

		ConsumeSeed(seedId, 1);
		pot.SeedId = crop.SeedId;
		pot.IngredientId = crop.IngredientId;
		pot.PlantedDay = day;
		pot.DaysGrown = 0;
		pot.RequiredGrowthDays = Math.Max(1, crop.GrowthDays);
		pot.HarvestYieldMin = Math.Max(1, crop.HarvestYieldMin);
		pot.HarvestYieldMax = Math.Max(pot.HarvestYieldMin, crop.HarvestYieldMax);
		plantedIngredientId = crop.IngredientId;
		return true;
	}

	public bool TryHarvestGardenPot(int potIndex, out GardenHarvestResult harvest, out string error)
	{
		harvest = default;
		error = string.Empty;
		if (!TryGetPot(potIndex, out var pot))
		{
			error = "Garden pot is missing.";
			return false;
		}

		if (pot.IsEmpty)
		{
			error = "Garden pot is empty.";
			return false;
		}

		if (!pot.IsReady)
		{
			error = "Ingredient is still growing.";
			return false;
		}

		if (!_itemExists(pot.IngredientId))
		{
			error = "Harvest ingredient is missing from the item catalog.";
			_pushError($"GameState: Cannot harvest unknown ingredient '{pot.IngredientId}'.");
			return false;
		}

		var harvestYield = ResolveHarvestYield(pot);
		harvest = new GardenHarvestResult(pot.IngredientId, pot.SeedId, harvestYield);
		AddSeedStack(pot.SeedId, 1);
		ClearGardenPot(pot);
		return true;
	}

	public bool SetUnlockedPotCount(int potCount)
	{
		var normalizedPotCount = Math.Max(StartingPotCount, potCount);
		if (_gardenPots.Count >= normalizedPotCount)
			return false;

		EnsurePotCount(normalizedPotCount);
		return true;
	}

	private static GardenCropDef CreateGardenCrop(string ingredientId, int growthDays)
	{
		return new GardenCropDef
		{
			IngredientId = ingredientId,
			SeedId = BuildSeedId(ingredientId),
			GrowthDays = Math.Max(1, growthDays),
			HarvestYieldMin = DefaultHarvestYield,
			HarvestYieldMax = DefaultHarvestYield
		};
	}

	private void SeedStartingSeedInventory()
	{
		foreach (var (seedId, quantity) in StartingSeedInventory)
			AddSeedStack(seedId, quantity);
	}

	private void AddSeedStack(string seedId, int quantity)
	{
		if (quantity <= 0 || string.IsNullOrWhiteSpace(seedId))
			return;
		if (!CropsBySeedId.ContainsKey(seedId))
			return;

		_seedInventory[seedId] = _seedInventory.GetValueOrDefault(seedId) + quantity;
	}

	private bool ConsumeSeed(string seedId, int quantity)
	{
		if (quantity <= 0)
			return true;
		if (!_seedInventory.TryGetValue(seedId, out var existing) || existing < quantity)
			return false;

		var remaining = existing - quantity;
		if (remaining <= 0)
			_seedInventory.Remove(seedId);
		else
			_seedInventory[seedId] = remaining;

		return true;
	}

	private bool TryGetPot(int potIndex, out GardenPotState pot)
	{
		pot = default!;
		if (potIndex < 0 || potIndex >= _gardenPots.Count)
			return false;

		pot = _gardenPots[potIndex];
		return true;
	}

	private static void ClearGardenPot(GardenPotState pot)
	{
		pot.SeedId = string.Empty;
		pot.IngredientId = string.Empty;
		pot.PlantedDay = 0;
		pot.DaysGrown = 0;
		pot.RequiredGrowthDays = 0;
		pot.HarvestYieldMin = 0;
		pot.HarvestYieldMax = 0;
	}

	private int ResolveHarvestYield(GardenPotState pot)
	{
		var minYield = Math.Max(1, pot.HarvestYieldMin);
		var maxYield = Math.Max(minYield, pot.HarvestYieldMax);
		return minYield == maxYield ? minYield : _gardenYieldRandom.Next(minYield, maxYield + 1);
	}

	private void EnsurePotCount(int potCount)
	{
		var targetCount = Math.Max(StartingPotCount, potCount);
		while (_gardenPots.Count < targetCount)
		{
			_gardenPots.Add(new GardenPotState
			{
				PotIndex = _gardenPots.Count
			});
		}
	}

	private void RestoreSeedInventory(Dictionary<string, int>? seedInventory)
	{
		if (seedInventory is null)
			return;

		foreach (var pair in seedInventory)
		{
			if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value <= 0)
				continue;
			if (!CropsBySeedId.ContainsKey(pair.Key))
				continue;

			_seedInventory[pair.Key] = pair.Value;
		}
	}

	private void RestoreGardenPots(List<GardenPotState>? gardenPots, int savedPotCount)
	{
		var targetPotCount = Math.Max(StartingPotCount, savedPotCount);
		if (gardenPots is not null && gardenPots.Count > targetPotCount)
			targetPotCount = gardenPots.Count;

		EnsurePotCount(targetPotCount);
		if (gardenPots is null)
			return;

		foreach (var savedPot in gardenPots)
		{
			if (savedPot is null)
				continue;
			if (savedPot.PotIndex < 0 || savedPot.PotIndex >= _gardenPots.Count)
				continue;

			_gardenPots[savedPot.PotIndex] = NormalizeGardenPot(savedPot);
		}
	}

	private static GardenPotState NormalizeGardenPot(GardenPotState savedPot)
	{
		var pot = new GardenPotState
		{
			PotIndex = Math.Max(0, savedPot.PotIndex)
		};

		if (string.IsNullOrWhiteSpace(savedPot.IngredientId))
			return pot;

		if (!CropsByIngredientId.TryGetValue(savedPot.IngredientId, out var crop))
			return pot;

		pot.SeedId = string.IsNullOrWhiteSpace(savedPot.SeedId) || !CropsBySeedId.ContainsKey(savedPot.SeedId)
			? crop.SeedId
			: savedPot.SeedId;
		pot.IngredientId = crop.IngredientId;
		pot.PlantedDay = Math.Max(1, savedPot.PlantedDay);
		pot.DaysGrown = Math.Max(0, savedPot.DaysGrown);
		pot.RequiredGrowthDays = Math.Max(1, savedPot.RequiredGrowthDays > 0 ? savedPot.RequiredGrowthDays : crop.GrowthDays);
		pot.HarvestYieldMin = Math.Max(1, savedPot.HarvestYieldMin > 0 ? savedPot.HarvestYieldMin : crop.HarvestYieldMin);
		var savedMax = savedPot.HarvestYieldMax > 0 ? savedPot.HarvestYieldMax : crop.HarvestYieldMax;
		pot.HarvestYieldMax = Math.Max(pot.HarvestYieldMin, savedMax);
		return pot;
	}

	private static GardenPotState CloneGardenPot(GardenPotState pot)
	{
		return new GardenPotState
		{
			PotIndex = pot.PotIndex,
			SeedId = pot.SeedId,
			IngredientId = pot.IngredientId,
			PlantedDay = pot.PlantedDay,
			DaysGrown = pot.DaysGrown,
			RequiredGrowthDays = pot.RequiredGrowthDays,
			HarvestYieldMin = pot.HarvestYieldMin,
			HarvestYieldMax = pot.HarvestYieldMax
		};
	}
}
