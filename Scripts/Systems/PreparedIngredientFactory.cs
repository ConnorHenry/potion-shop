using System;
using System.Collections.Generic;
using System.Linq;
using OccultShop.Models;

namespace OccultShop.Systems;

public static class PreparedIngredientFactory
{
	private const string FailedBoilingItemIdSuffix = "__boil_failed";
	private const float FailedBoilingTraitMultiplier = 0.33f;
	private const int GuaranteedFailureRiskChance = 10;

	public static bool TryBuildPreparedIngredient(
		ItemDef baseIngredient,
		string preparationId,
		out ItemDef preparedIngredient,
		out string error)
	{
		preparedIngredient = default!;
		error = string.Empty;

		if (baseIngredient is null || string.IsNullOrWhiteSpace(baseIngredient.Id))
		{
			error = "Ingredient is not recognized.";
			return false;
		}

		var normalizedPreparationId = IngredientPreparationCatalog.NormalizePreparationId(preparationId);
		if (!IngredientPreparationCatalog.IsKnownPreparationId(normalizedPreparationId))
		{
			error = "Preparation method is not recognized.";
			return false;
		}

		if (!IngredientPreparationCatalog.TryGetPreparation(baseIngredient, normalizedPreparationId, out var preparation))
		{
			error = $"{baseIngredient.Name} cannot be {IngredientPreparationCatalog.GetDisplayName(normalizedPreparationId).ToLowerInvariant()}.";
			return false;
		}

		var tags = CloneTags(baseIngredient.Tags);
		if (!tags.Any(tag => string.Equals(tag, ItemTags.PreparedIngredient, StringComparison.OrdinalIgnoreCase)))
			tags.Add(ItemTags.PreparedIngredient);

		var preparationName = IngredientPreparationCatalog.GetDisplayName(normalizedPreparationId);
		preparedIngredient = new ItemDef
		{
			Id = IngredientPreparationCatalog.BuildPreparedItemId(baseIngredient.Id, normalizedPreparationId),
			Name = $"{baseIngredient.Name} ({preparationName})",
			IconPath = baseIngredient.IconPath,
			Description = baseIngredient.Description,
			StartsKnownInIngredientBook = baseIngredient.StartsKnownInIngredientBook,
			Tags = tags,
			Quality = baseIngredient.Quality,
			Traits = CloneStatMap(preparation.Traits),
			Risks = BuildSuccessfulPreparationRisks(preparation),
			IngredientEffects = CloneIngredientEffects(baseIngredient.IngredientEffects),
			BasePrice = baseIngredient.BasePrice,
			PreparedIngredient = new PreparedIngredientDef
			{
				BaseIngredientId = baseIngredient.Id,
				PreparationId = normalizedPreparationId
			}
		};

		return true;
	}

	private static Dictionary<string, int> BuildSuccessfulPreparationRisks(IngredientPreparationDef preparation)
	{
		return PreparationHasMiniGame(preparation)
			? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
			: CloneStatMap(preparation.Risks);
	}

	private static bool PreparationHasMiniGame(IngredientPreparationDef preparation)
	{
		return preparation.BoilingGame is not null;
	}

	public static bool TryBuildFailedBoiledIngredient(
		ItemDef baseIngredient,
		BoilingMiniGameDef boilingGame,
		out ItemDef failedIngredient,
		out string error)
	{
		failedIngredient = default!;
		error = string.Empty;

		if (boilingGame is null || string.IsNullOrWhiteSpace(boilingGame.FailureRiskId))
		{
			error = "Boiling failure risk is not configured.";
			return false;
		}

		if (!TryBuildPreparedIngredient(
			baseIngredient,
			IngredientPreparationCatalog.BoiledPreparationId,
			out var preparedIngredient,
			out error))
		{
			return false;
		}

		preparedIngredient.Id = BuildFailedBoilingItemId(baseIngredient.Id);
		preparedIngredient.Name = $"{baseIngredient.Name} (Boiled, Failed)";
		preparedIngredient.Traits = ReduceTraitsForFailedBoil(preparedIngredient.Traits);
		preparedIngredient.Risks = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
		{
			[boilingGame.FailureRiskId.Trim()] = GuaranteedFailureRiskChance
		};
		if (!preparedIngredient.Tags.Any(tag => string.Equals(tag, ItemTags.FailedBoiling, StringComparison.OrdinalIgnoreCase)))
			preparedIngredient.Tags.Add(ItemTags.FailedBoiling);

		failedIngredient = preparedIngredient;
		return true;
	}

	public static string BuildFailedBoilingItemId(string baseIngredientId)
	{
		return $"{IngredientPreparationCatalog.BuildPreparedItemId(baseIngredientId, IngredientPreparationCatalog.BoiledPreparationId)}{FailedBoilingItemIdSuffix}";
	}

	private static List<string> CloneTags(List<string>? tags)
	{
		var clone = new List<string>();
		if (tags is null)
			return clone;

		foreach (var tag in tags)
		{
			if (string.IsNullOrWhiteSpace(tag))
				continue;
			if (clone.Any(existing => string.Equals(existing, tag, StringComparison.OrdinalIgnoreCase)))
				continue;

			clone.Add(tag);
		}

		return clone;
	}

	private static Dictionary<string, int> CloneStatMap(Dictionary<string, int>? values)
	{
		return values is null
			? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
			: new Dictionary<string, int>(values, StringComparer.OrdinalIgnoreCase);
	}

	private static Dictionary<string, int> ReduceTraitsForFailedBoil(Dictionary<string, int>? traits)
	{
		var reducedTraits = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		if (traits is null)
			return reducedTraits;

		foreach (var trait in traits)
		{
			if (string.IsNullOrWhiteSpace(trait.Key) || trait.Value <= 0)
				continue;

			reducedTraits[trait.Key] = Math.Max(1, (int)MathF.Round(trait.Value * FailedBoilingTraitMultiplier));
		}

		return reducedTraits;
	}

	private static List<IngredientEffectDef> CloneIngredientEffects(List<IngredientEffectDef>? effects)
	{
		var clones = new List<IngredientEffectDef>();
		if (effects is null)
			return clones;

		foreach (var effect in effects)
		{
			if (effect is null)
				continue;

			clones.Add(new IngredientEffectDef
			{
				Kind = effect.Kind,
				Family = effect.Family,
				Name = effect.Name,
				Description = effect.Description,
				Amount = effect.Amount,
				SecondaryAmount = effect.SecondaryAmount,
				TraitId = effect.TraitId,
				RiskId = effect.RiskId
			});
		}

		return clones;
	}
}
