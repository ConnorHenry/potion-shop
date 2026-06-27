using System;
using System.Collections.Generic;
using System.Linq;
using OccultShop.Autoload;
using OccultShop.Models;

namespace OccultShop.Systems;

public sealed class BrewWorkflowResult
{
	public bool Success { get; private init; }
	public string Error { get; private init; } = string.Empty;
	public string PotionItemId { get; private init; } = string.Empty;
	public string PotionDisplayName { get; private init; } = string.Empty;
	public PotionResult? BrewResult { get; private init; }

	public static BrewWorkflowResult Failure(string error)
	{
		return new BrewWorkflowResult
		{
			Success = false,
			Error = string.IsNullOrWhiteSpace(error) ? "Could not brew potion." : error
		};
	}

	public static BrewWorkflowResult Completed(string potionItemId, string potionDisplayName, PotionResult brewResult)
	{
		return new BrewWorkflowResult
		{
			Success = true,
			PotionItemId = potionItemId,
			PotionDisplayName = potionDisplayName,
			BrewResult = brewResult
		};
	}
}

public sealed class BrewWorkflowService
{
	private const int BrewedPotionOutputQuantity = 1;

	private readonly GameState _gameState;
	private readonly RuntimeContentDb _runtimeContentDb;
	private readonly ItemCatalogService _itemCatalog;
	private readonly PotionRecipeLookup _predefinedPotionRecipes;
	private readonly PotionInventoryBrewService _inventoryBrewService;
	private readonly PotionBrewingService _brewingService = new();

	public BrewWorkflowService(
		GameState gameState,
		RuntimeContentDb runtimeContentDb,
		ItemCatalogService itemCatalog,
		PotionRecipeLookup predefinedPotionRecipes)
	{
		_gameState = gameState;
		_runtimeContentDb = runtimeContentDb;
		_itemCatalog = itemCatalog;
		_predefinedPotionRecipes = predefinedPotionRecipes;
		_inventoryBrewService = new PotionInventoryBrewService(gameState, itemCatalog);
	}

	public bool TryPreviewQueuedPotion(
		IReadOnlyList<IngredientPortionDef> queuedIngredients,
		out PotionResult? previewResult)
	{
		previewResult = null;
		if (queuedIngredients is null || queuedIngredients.Count == 0)
			return false;
		if (!TryBuildIngredientDefs(queuedIngredients, out var ingredientDefs, out _, knownStatsOnly: true))
			return false;

		previewResult = _brewingService.PreviewPotion(ingredientDefs, null);
		return true;
	}

	public BrewWorkflowResult TryBrewQueuedPotion(
		IReadOnlyList<IngredientPortionDef> queuedIngredients,
		string requestedPotionDisplayName,
		string fallbackIconPath)
	{
		if (queuedIngredients is null || queuedIngredients.Count != 3)
			return BrewWorkflowResult.Failure("Brewing requires exactly 3 ingredients.");

		var combinationKey = PotionRecipeLookup.BuildCombinationKey(queuedIngredients);
		var hasPredefinedRecipe = _predefinedPotionRecipes.TryGetRecipe(queuedIngredients, out var predefinedRecipe);

		if (!TryBuildIngredientDefs(queuedIngredients, out var ingredientDefs, out var ingredientError))
			return BrewWorkflowResult.Failure(ingredientError);

		var brewResult = _brewingService.BrewPotion(ingredientDefs, null);
		var totalIngredientPrice = CalculateIngredientTotalPrice(queuedIngredients);
		var potionBasePrice = Math.Max(0, totalIngredientPrice - brewResult.RiskIngredientPricePenalty);
		var brewCost = BrewPricing.CalculateBrewCost(totalIngredientPrice, brewResult);
		if (_gameState.Gold < brewCost)
			return BrewWorkflowResult.Failure($"Need {brewCost} gold to brew this potion.");

		var potionDisplayName = hasPredefinedRecipe && predefinedRecipe is not null
			? predefinedRecipe.Name
			: requestedPotionDisplayName;
		var iconPath = fallbackIconPath;
		var potionTraits = BuildPotionTraitsForRegistration(brewResult, hasPredefinedRecipe ? predefinedRecipe : null);

		if (!_gameState.TryGetPotionForCombination(combinationKey, out var potionItemId))
		{
			potionItemId = hasPredefinedRecipe && predefinedRecipe is not null
				? PotionVariantIdBuilder.BuildPredefinedPotionItemId(predefinedRecipe.Id)
				: $"brew_{_gameState.PotionDisplayNames.Count + 1}";

			_runtimeContentDb.RegisterRuntimePotionItem(
				potionItemId,
				potionDisplayName,
				iconPath,
				potionBasePrice,
				brewResult.IngredientQualityScore,
				potionTraits,
				new Dictionary<string, int>(brewResult.Risks));

			_gameState.SetPotionForCombination(combinationKey, potionItemId);
			_gameState.SetPotionDisplayName(potionItemId, potionDisplayName);
		}
		else
		{
			if (!_itemCatalog.TryGetItem(potionItemId, out var basePotionItem))
				return BrewWorkflowResult.Failure("Known potion recipe is missing from the item catalog.");

			iconPath = string.IsNullOrWhiteSpace(basePotionItem.IconPath)
				? fallbackIconPath
				: basePotionItem.IconPath;

			if (!PotionVariantIdBuilder.RisksMatch(basePotionItem.Risks, brewResult.Risks))
			{
				var variantPotionItemId = PotionVariantIdBuilder.BuildRiskVariantItemId(potionItemId, brewResult.Risks);
				if (!_itemCatalog.TryGetItem(variantPotionItemId, out _))
				{
					_runtimeContentDb.RegisterRuntimePotionItem(
						variantPotionItemId,
						potionDisplayName,
						iconPath,
						potionBasePrice,
						brewResult.IngredientQualityScore,
						potionTraits,
						new Dictionary<string, int>(brewResult.Risks));
				}

				potionItemId = variantPotionItemId;
			}

			_gameState.SetPotionDisplayName(potionItemId, potionDisplayName);
		}

		if (!_inventoryBrewService.CanAddPotion(potionItemId, BrewedPotionOutputQuantity))
			return BrewWorkflowResult.Failure(PotionInventoryBrewService.PotionInventoryFullMessage);

		_gameState.AddGold(-brewCost);
		_gameState.RegisterPotionBasePrice(potionItemId, potionBasePrice);
		_runtimeContentDb.TrySetRuntimeItemBasePrice(potionItemId, potionBasePrice);
		_gameState.RecordPotionRecipe(potionItemId, BuildIngredientIdList(queuedIngredients));
		_gameState.RecordIngredientPreparationKnowledge(queuedIngredients);
		_gameState.AddItem(potionItemId, BrewedPotionOutputQuantity);
		_gameState.RecordPotionBatch(potionItemId, queuedIngredients);

		return BrewWorkflowResult.Completed(potionItemId, potionDisplayName, brewResult);
	}

	private bool TryBuildIngredientDefs(
		IReadOnlyList<IngredientPortionDef> queuedIngredients,
		out List<IngredientDef> ingredientDefs,
		out string error,
		bool knownStatsOnly = false)
	{
		ingredientDefs = new List<IngredientDef>(queuedIngredients.Count);
		error = string.Empty;

		foreach (var ingredient in queuedIngredients)
		{
			var itemId = ingredient.InventoryItemId;
			if (!_itemCatalog.TryGetItem(itemId, out var item))
			{
				error = $"Unknown ingredient '{itemId}'.";
				return false;
			}

			var ingredientDef = IngredientDefFactory.FromItemDef(item);
			if (knownStatsOnly &&
				IngredientPreparationCatalog.TryGetPreparedIngredientInfo(item, out var baseIngredientId, out var preparationId) &&
				!_gameState.KnowsIngredientPreparation(baseIngredientId, preparationId))
			{
				ingredientDef.Traits.Clear();
				ingredientDef.Risks.Clear();
				ingredientDef.IngredientEffects.Clear();
			}

			ingredientDefs.Add(ingredientDef);
		}

		return true;
	}

	private int CalculateIngredientTotalPrice(IReadOnlyList<IngredientPortionDef> ingredients)
	{
		var total = 0;
		foreach (var ingredient in ingredients)
		{
			if (!_itemCatalog.TryGetItem(ingredient.InventoryItemId, out var item))
				continue;

			total += Math.Max(0, item.BasePrice);
		}

		return total;
	}

	private static Dictionary<string, int> BuildPotionTraitsForRegistration(PotionResult brewResult, PotionRecipeDef? predefinedRecipe)
	{
		if (predefinedRecipe is null || predefinedRecipe.Traits is null || predefinedRecipe.Traits.Count == 0)
			return new Dictionary<string, int>(brewResult.Traits);

		var traits = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		foreach (var trait in predefinedRecipe.Traits)
		{
			if (string.IsNullOrWhiteSpace(trait))
				continue;
			if (!brewResult.Traits.TryGetValue(trait, out var strength))
				continue;

			traits[trait] = strength;
		}

		if (traits.Count > 0)
			return traits;

		return new Dictionary<string, int>(brewResult.Traits);
	}

	private static List<string> BuildIngredientIdList(IReadOnlyList<IngredientPortionDef> ingredients)
	{
		var itemIds = new List<string>(ingredients.Count);
		foreach (var ingredient in ingredients)
		{
			var itemId = ingredient.InventoryItemId;
			if (string.IsNullOrWhiteSpace(itemId))
				continue;

			itemIds.Add(itemId);
		}

		return itemIds;
	}
}
