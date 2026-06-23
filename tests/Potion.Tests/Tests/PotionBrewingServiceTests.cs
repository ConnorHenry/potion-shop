using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using OccultShop.Models;
using OccultShop.Systems;
using static ProjectFileTestHelper;
using static TestAssert;
using static UiReflectionTestHelper;

internal static class PotionBrewingServiceTests
{
    public static void Register(TestRunner runner)
    {
        runner.Run("Rejects empty ingredient lists", TestRejectsEmptyIngredients);
        runner.Run("Combines ingredient traits", TestCombinesIngredientTraits);
        runner.Run("Previews combined ingredient risk chances", TestPreviewsCombinedIngredientRiskChances);
        runner.Run("Rolls combined risks once and stores presence", TestRollsCombinedRisksOnceAndStoresPresence);
        runner.Run("Carried ingredient risks apply price penalty", TestCarriedIngredientRisksApplyPricePenalty);
        runner.Run("Failed ingredient risks do not apply price penalty", TestFailedIngredientRisksDoNotApplyPricePenalty);
        runner.Run("Clamps risk chances at ten", TestClampsRiskChancesAtTen);
        runner.Run("Failed carried risks do not affect scoring", TestFailedCarriedRisksDoNotAffectScoring);
        runner.Run("Ingredient effects boost the lowest other trait", TestIngredientEffectBoostsLowestOtherTrait);
        runner.Run("Ingredient effects reduce other risk chances before rolls", TestIngredientEffectHalvesOtherRiskChances);
        runner.Run("Ingredient effects can add volatile risk chances", TestIngredientEffectAddsVolatileRiskChance);
        runner.Run("Ingredient effects can suppress one carried risk", TestIngredientEffectSuppressesSingleCarriedRisk);
        runner.Run("Ingredient effects ignore ingredient queue order", TestIngredientEffectsIgnoreIngredientOrder);
        runner.Run("PotionRecipeLookup exact grams override base recipes", TestPotionRecipeLookupExactGrams);
        runner.Run("PotionRecipeLookup distinguishes preparation methods", TestPotionRecipeLookupPreparationMethods);
        runner.Run("Prepared ingredients use preparation traits and metadata", TestPreparedIngredientFactoryUsesPreparationData);
        runner.Run("Successful minigame prepared ingredients do not carry risks", TestSuccessfulMiniGamePreparedIngredientsDoNotCarryRisks);
        runner.Run("Failed boiled ingredients reduce traits and guarantee risk", TestFailedBoiledIngredientFactoryReducesTraitsAndGuaranteesRisk);
        runner.Run("Failed boiled risks survive risk modifiers", TestFailedBoiledRisksSurviveRiskModifiers);
        runner.Run("Scores a clean positive brew", TestPositiveBrew);
        runner.Run("Handles risk penalties", TestRiskPenaltyBrew);
    }

    private static void TestPotionRecipeLookupExactGrams()
    {
        var lookup = new PotionRecipeLookup();
        var recipes = new List<PotionRecipeDef>
        {
            new()
            {
                Id = "base_rest",
                Name = "Base Rest",
                IngredientIds = new List<string> { "heather", "elder", "rosemary" }
            },
            new()
            {
                Id = "exact_rest",
                Name = "Exact Rest",
                IngredientIds = new List<string> { "heather", "elder", "rosemary" },
                IngredientAmounts = new List<IngredientPortionDef>
                {
                    new() { IngredientId = "heather", Grams = 6 },
                    new() { IngredientId = "elder", Grams = 2 },
                    new() { IngredientId = "rosemary", Grams = 1 }
                }
            }
        };

        lookup.Rebuild(recipes, _ => true);

        var exactPortions = new List<IngredientPortionDef>
        {
            new() { IngredientId = "elder", Grams = 2 },
            new() { IngredientId = "rosemary", Grams = 1 },
            new() { IngredientId = "heather", Grams = 6 }
        };
        var exactMatched = lookup.TryGetRecipe(exactPortions, out var exactRecipe);
        AssertTrue("Exact measured recipe matches", exactMatched);
        AssertEqual("Exact recipe id", "exact_rest", exactRecipe.Id);
        AssertEqual(
            "Exact combination key",
            "elder@2g|heather@6g|rosemary@1g",
            PotionRecipeLookup.BuildCombinationKey(exactPortions));

        exactPortions[2].Grams = 7;
        var fallbackMatched = lookup.TryGetRecipe(exactPortions, out var fallbackRecipe);
        AssertTrue("Measured mismatch falls back to base recipe", fallbackMatched);
        AssertEqual("Fallback recipe id", "base_rest", fallbackRecipe.Id);

        var directPortions = new List<IngredientPortionDef>
        {
            new() { IngredientId = "elder" },
            new() { IngredientId = "rosemary" },
            new() { IngredientId = "heather" }
        };
        var directMatched = lookup.TryGetRecipe(directPortions, out var directRecipe);
        AssertTrue("Direct unmeasured recipe still matches base", directMatched);
        AssertEqual("Direct recipe id", "base_rest", directRecipe.Id);
    }

    private static void TestPotionRecipeLookupPreparationMethods()
    {
        var lookup = new PotionRecipeLookup();
        var recipes = new List<PotionRecipeDef>
        {
            new()
            {
                Id = "steeped_rest",
                Name = "Steeped Rest",
                IngredientIds = new List<string> { "heather", "elder", "rosemary" },
                IngredientAmounts = new List<IngredientPortionDef>
                {
                    new() { IngredientId = "heather", PreparationId = "steeped" },
                    new() { IngredientId = "elder", PreparationId = "boiled" },
                    new() { IngredientId = "rosemary", PreparationId = "raw" }
                }
            }
        };

        lookup.Rebuild(recipes, _ => true);

        var matchingPortions = new List<IngredientPortionDef>
        {
            new() { IngredientId = "rosemary", PreparationId = "raw" },
            new() { IngredientId = "heather", PreparationId = "steeped" },
            new() { IngredientId = "elder", PreparationId = "boiled" }
        };

        AssertTrue("Prepared recipe matches exact preparation ids",
            lookup.TryGetRecipe(matchingPortions, out var matchedRecipe));
        AssertEqual("Prepared recipe id", "steeped_rest", matchedRecipe.Id);
        AssertEqual(
            "Prepared combination key",
            "elder#boiled|heather#steeped|rosemary#raw",
            PotionRecipeLookup.BuildCombinationKey(matchingPortions));

        matchingPortions[1].PreparationId = "crushed";
        AssertTrue("Wrong preparation does not match prepared recipe",
            !lookup.TryGetRecipe(matchingPortions, out _));
    }

    private static void TestPreparedIngredientFactoryUsesPreparationData()
    {
        var baseIngredient = new ItemDef
        {
            Id = "mint",
            Name = "Mint",
            IconPath = "res://Assets/Items/mint.png",
            BasePrice = 7,
            Quality = 70,
            Tags = new List<string> { ItemTags.Ingredient, ItemTags.Herb },
            Traits = new Dictionary<string, int>(),
            Risks = new Dictionary<string, int>(),
            Preparations = new Dictionary<string, IngredientPreparationDef>(StringComparer.OrdinalIgnoreCase)
            {
                ["crushed"] = new()
                {
                    Traits = new Dictionary<string, int> { ["cleanse"] = 5 },
                    Risks = new Dictionary<string, int> { ["melancholy"] = 1 }
                }
            }
        };

        var built = PreparedIngredientFactory.TryBuildPreparedIngredient(
            baseIngredient,
            "crushed",
            out var preparedIngredient,
            out var error);

        AssertTrue($"Prepared ingredient builds without error: {error}", built);
        AssertEqual("Prepared item id", "mint__prep_crushed", preparedIngredient.Id);
        AssertEqual("Prepared item name", "Mint (Crushed)", preparedIngredient.Name);
        AssertEqual("Prepared trait", 5, preparedIngredient.Traits["cleanse"]);
        AssertEqual("Prepared risk", 1, preparedIngredient.Risks["melancholy"]);
        AssertEqual("Prepared metadata base", "mint", preparedIngredient.PreparedIngredient?.BaseIngredientId ?? "");
        AssertEqual("Prepared metadata prep", "crushed", preparedIngredient.PreparedIngredient?.PreparationId ?? "");
        AssertTrue("Prepared tag is present", preparedIngredient.Tags.Contains(ItemTags.PreparedIngredient));
    }

    private static void TestSuccessfulMiniGamePreparedIngredientsDoNotCarryRisks()
    {
        var baseIngredient = new ItemDef
        {
            Id = "gorse",
            Name = "Gorse",
            IconPath = "res://Assets/Items/gorse.png",
            BasePrice = 8,
            Quality = 75,
            Tags = new List<string> { ItemTags.Ingredient, ItemTags.Herb },
            Preparations = new Dictionary<string, IngredientPreparationDef>(StringComparer.OrdinalIgnoreCase)
            {
                ["boiled"] = new()
                {
                    Traits = new Dictionary<string, int> { ["soothe"] = 6 },
                    Risks = new Dictionary<string, int> { ["melancholy"] = 4 },
                    BoilingGame = new BoilingMiniGameDef
                    {
                        FailureRiskId = "melancholy"
                    }
                }
            }
        };

        var built = PreparedIngredientFactory.TryBuildPreparedIngredient(
            baseIngredient,
            IngredientPreparationCatalog.BoiledPreparationId,
            out var preparedIngredient,
            out var error);

        AssertTrue($"Successful boiled ingredient builds without error: {error}", built);
        AssertEqual("Successful minigame output has no stored risks", 0, preparedIngredient.Risks.Count);
        AssertEqual("Successful boiled trait remains", 6, preparedIngredient.Traits["soothe"]);

        preparedIngredient.Risks["melancholy"] = 4;
        var ingredientDef = IngredientDefFactory.FromItemDef(preparedIngredient);
        var service = new PotionBrewingService(() => 0.0f);
        var preview = service.PreviewPotion(new List<IngredientDef> { ingredientDef }, null);
        var brewed = service.BrewPotion(new List<IngredientDef> { ingredientDef }, null);

        AssertEqual("Stale successful minigame risk is removed for preview", 0, preview.PossibleRisks.Count);
        AssertEqual("Successful minigame risk does not carry to potion", 0, brewed.Risks.Count);
    }

    private static void TestFailedBoiledIngredientFactoryReducesTraitsAndGuaranteesRisk()
    {
        var baseIngredient = new ItemDef
        {
            Id = "mint",
            Name = "Mint",
            IconPath = "res://Assets/Items/mint.png",
            BasePrice = 7,
            Quality = 70,
            Tags = new List<string> { ItemTags.Ingredient, ItemTags.Herb },
            Preparations = new Dictionary<string, IngredientPreparationDef>(StringComparer.OrdinalIgnoreCase)
            {
                ["boiled"] = new()
                {
                    Traits = new Dictionary<string, int> { ["cleanse"] = 6, ["soothe"] = 1 },
                    Risks = new Dictionary<string, int>(),
                    BoilingGame = new BoilingMiniGameDef
                    {
                        FailureRiskId = "melancholy"
                    }
                }
            }
        };

        var built = PreparedIngredientFactory.TryBuildFailedBoiledIngredient(
            baseIngredient,
            baseIngredient.Preparations["boiled"].BoilingGame!,
            out var failedIngredient,
            out var error);

        AssertTrue($"Failed boiled ingredient builds without error: {error}", built);
        AssertEqual("Failed boiled item id", "mint__prep_boiled__boil_failed", failedIngredient.Id);
        AssertEqual("Reduced high trait", 2, failedIngredient.Traits["cleanse"]);
        AssertEqual("Reduced minimum trait", 1, failedIngredient.Traits["soothe"]);
        AssertEqual("Failure risk is guaranteed", 10, failedIngredient.Risks["melancholy"]);
        AssertEqual("Failed metadata base", "mint", failedIngredient.PreparedIngredient?.BaseIngredientId ?? "");
        AssertEqual("Failed metadata prep still matches recipes", "boiled", failedIngredient.PreparedIngredient?.PreparationId ?? "");
        AssertTrue("Failed boiling tag is present", failedIngredient.Tags.Contains(ItemTags.FailedBoiling));
    }

    private static void TestFailedBoiledRisksSurviveRiskModifiers()
    {
        var service = new PotionBrewingService(() => 0.99f);
        var failedBoiledIngredient = new IngredientDef
        {
            Id = "mint__prep_boiled__boil_failed",
            Name = "Mint (Boiled, Failed)",
            Quality = 70,
            BasePrice = 7,
            Traits = new Dictionary<string, int> { ["cleanse"] = 2 },
            Risks = new Dictionary<string, int> { ["melancholy"] = 10 },
            Tags = new List<string> { ItemTags.Ingredient, ItemTags.PreparedIngredient, ItemTags.FailedBoiling }
        };
        var riskReducer = new IngredientDef
        {
            Id = "gorse",
            Name = "Gorse",
            Quality = 80,
            Traits = new Dictionary<string, int> { ["soothe"] = 2 },
            IngredientEffects = new List<IngredientEffectDef>
            {
                new()
                {
                    Kind = IngredientEffectDef.ReduceHighestRiskKind,
                    Name = "Risk reducer",
                    Amount = 10
                }
            }
        };
        var riskSuppressor = new IngredientDef
        {
            Id = "thyme",
            Name = "Thyme",
            Quality = 80,
            Traits = new Dictionary<string, int> { ["rest"] = 2 },
            IngredientEffects = new List<IngredientEffectDef>
            {
                new()
                {
                    Kind = IngredientEffectDef.SuppressSingleCarriedRiskKind,
                    Name = "Risk suppressor"
                }
            }
        };

        var result = service.BrewPotion(
            new List<IngredientDef> { failedBoiledIngredient, riskReducer, riskSuppressor },
            null);

        AssertEqual("Failed risk remains a guaranteed possible risk", 10, result.PossibleRisks["melancholy"]);
        AssertEqual("Failed risk carries despite modifiers", 1, result.Risks["melancholy"]);
    }

    private static void TestRejectsEmptyIngredients()
    {
        var service = new PotionBrewingService();
        var result = service.BrewPotion(new List<IngredientDef>(), null);

        AssertEqual("Grade", "F", result.Grade);
        AssertEqual("FinalScore", 0.0f, result.FinalScore);
        AssertEqual("PenaltyScore", 100, result.PenaltyScore);
        AssertTrue("Notes mention invalid input", result.Notes.Exists(x => x.Contains("No valid ingredients", StringComparison.OrdinalIgnoreCase)));
    }

    private static void TestCombinesIngredientTraits()
    {
        var service = new PotionBrewingService();

        var ingredients = new List<IngredientDef>
        {
            new()
            {
                Id = "heather",
                Name = "Heather",
                Quality = 40,
                Traits = new Dictionary<string, int>
                {
                    ["sleep"] = 4,
                    ["dream"] = 3
                }
            },
            new()
            {
                Id = "mint",
                Name = "Mint",
                Quality = 40,
                Traits = new Dictionary<string, int>
                {
                    ["calm"] = 4,
                    ["memory"] = 2
                }
            }
        };

        var result = service.BrewPotion(ingredients, null);

        AssertEqual("Trait count", 4, result.Traits.Count);
        AssertEqual("sleep", 4, result.Traits["sleep"]);
        AssertEqual("dream", 3, result.Traits["dream"]);
        AssertEqual("calm", 4, result.Traits["calm"]);
        AssertEqual("memory", 2, result.Traits["memory"]);
    }

    private static void TestPreviewsCombinedIngredientRiskChances()
    {
        var service = new PotionBrewingService();

        var ingredients = new List<IngredientDef>
        {
            new()
            {
                Id = "night_bloom",
                Name = "Night Bloom",
                Quality = 40,
                Risks = new Dictionary<string, int>
                {
                    ["nausea"] = 5,
                    ["instability"] = 1
                }
            },
            new()
            {
                Id = "ash_root",
                Name = "Ash Root",
                Quality = 40,
                Risks = new Dictionary<string, int>
                {
                    ["nausea"] = 1,
                    ["corrosion"] = 4
                }
            },
            new()
            {
                Id = "spore_leaf",
                Name = "Spore Leaf",
                Quality = 40,
                Risks = new Dictionary<string, int>
                {
                    ["rot"] = 3
                }
            }
        };

        var result = service.PreviewPotion(ingredients, null);

        AssertEqual("Actual carried risk count", 0, result.Risks.Count);
        AssertEqual("Possible risk count", 4, result.PossibleRisks.Count);
        AssertEqual("nausea chance", 6, result.PossibleRisks["nausea"]);
        AssertEqual("corrosion chance", 4, result.PossibleRisks["corrosion"]);
        AssertEqual("rot chance", 3, result.PossibleRisks["rot"]);
        AssertEqual("instability chance", 1, result.PossibleRisks["instability"]);
    }

    private static void TestRollsCombinedRisksOnceAndStoresPresence()
    {
        var rollCount = 0;
        var service = new PotionBrewingService(() =>
        {
            rollCount += 1;
            return 0.59f;
        });

        var ingredients = new List<IngredientDef>
        {
            new()
            {
                Id = "night_bloom",
                Name = "Night Bloom",
                Quality = 40,
                Risks = new Dictionary<string, int>
                {
                    ["nausea"] = 1
                }
            },
            new()
            {
                Id = "ash_root",
                Name = "Ash Root",
                Quality = 40,
                Risks = new Dictionary<string, int>
                {
                    ["nausea"] = 5
                }
            }
        };

        var result = service.BrewPotion(ingredients, null);

        AssertEqual("Combined risk rolled once", 1, rollCount);
        AssertEqual("Possible nausea chance", 6, result.PossibleRisks["nausea"]);
        AssertEqual("Carried risk count", 1, result.Risks.Count);
        AssertEqual("Nausea is stored as presence", 1, result.Risks["nausea"]);
    }

    private static void TestCarriedIngredientRisksApplyPricePenalty()
    {
        var service = new PotionBrewingService(() => 0.0f);
        var ingredients = new List<IngredientDef>
        {
            new()
            {
                Id = "yarrow",
                Name = "Yarrow",
                Quality = 40,
                BasePrice = 12,
                Risks = new Dictionary<string, int>
                {
                    ["insomnia"] = 1
                }
            },
            new()
            {
                Id = "elder",
                Name = "Elder",
                Quality = 40,
                BasePrice = 18
            },
            new()
            {
                Id = "mint",
                Name = "Mint",
                Quality = 40,
                BasePrice = 8
            }
        };

        var result = service.BrewPotion(ingredients, null);

        AssertEqual("Insomnia is carried", 1, result.Risks["insomnia"]);
        AssertEqual("Risk ingredient price penalty", 12, result.RiskIngredientPricePenalty);
    }

    private static void TestFailedIngredientRisksDoNotApplyPricePenalty()
    {
        var service = new PotionBrewingService(() => 0.99f);
        var ingredients = new List<IngredientDef>
        {
            new()
            {
                Id = "yarrow",
                Name = "Yarrow",
                Quality = 40,
                BasePrice = 12,
                Risks = new Dictionary<string, int>
                {
                    ["insomnia"] = 1
                }
            }
        };

        var result = service.BrewPotion(ingredients, null);

        AssertEqual("No carried risk", 0, result.Risks.Count);
        AssertEqual("No risk ingredient price penalty", 0, result.RiskIngredientPricePenalty);
    }

    private static void TestClampsRiskChancesAtTen()
    {
        var service = new PotionBrewingService(() => 0.99f);
        var ingredients = new List<IngredientDef>
        {
            new()
            {
                Id = "fever_root",
                Name = "Fever Root",
                Quality = 40,
                Risks = new Dictionary<string, int>
                {
                    ["fever"] = 12,
                    ["ignored"] = 0
                }
            }
        };

        var result = service.BrewPotion(ingredients, null);

        AssertEqual("Fever chance clamped", 10, result.PossibleRisks["fever"]);
        AssertTrue("Zero chance risk ignored", !result.PossibleRisks.ContainsKey("ignored"));
        AssertEqual("Clamped risk always carries", 1, result.Risks["fever"]);
    }

    private static void TestFailedCarriedRisksDoNotAffectScoring()
    {
        var service = new PotionBrewingService(() => 0.99f);
        var ingredients = new List<IngredientDef>
        {
            new()
            {
                Id = "heather",
                Name = "Heather",
                Quality = 80,
                Traits = new Dictionary<string, int>
                {
                    ["healing"] = 2
                },
                Risks = new Dictionary<string, int>
                {
                    ["corruption"] = 1
                }
            }
        };

        var request = new CustomerRequestDef
        {
            Id = "clean_healing",
            DesiredTraits = new Dictionary<string, CustomerTraitRangeDef>
            {
                ["healing"] = Range(min: 2)
            },
            BadTraits = new Dictionary<string, CustomerTraitRangeDef>
            {
                ["corruption"] = Range(max: 0)
            }
        };

        var result = service.BrewPotion(ingredients, request);

        AssertEqual("Possible corruption chance", 1, result.PossibleRisks["corruption"]);
        AssertEqual("Carried risk count", 0, result.Risks.Count);
        AssertEqual("EffectFitScore ignores failed risk", 100, result.EffectFitScore);
        AssertEqual("PenaltyScore ignores failed risk", 0, result.PenaltyScore);
    }

    private static void TestIngredientEffectBoostsLowestOtherTrait()
    {
        var service = new PotionBrewingService();
        var ingredients = new List<IngredientDef>
        {
            new()
            {
                Id = "rosemary",
                Name = "Rosemary",
                Quality = 80,
                Traits = new Dictionary<string, int>
                {
                    ["dream"] = 5
                },
                IngredientEffects = new List<IngredientEffectDef>
                {
                    new()
                    {
                        Kind = IngredientEffectDef.BoostLowestOtherTraitKind,
                        Name = "Soft Bloom",
                        Amount = 2
                    }
                }
            },
            new()
            {
                Id = "heather",
                Name = "Heather",
                Quality = 80,
                Traits = new Dictionary<string, int>
                {
                    ["calm"] = 3
                }
            },
            new()
            {
                Id = "elder",
                Name = "Elder",
                Quality = 80,
                Traits = new Dictionary<string, int>
                {
                    ["rest"] = 5
                }
            }
        };

        var result = service.PreviewPotion(ingredients, null);

        AssertEqual("Lowest other trait boosted", 5, result.Traits["calm"]);
        AssertEqual("Source trait remains", 5, result.Traits["dream"]);
        AssertEqual("Ingredient effect recorded", 1, result.TriggeredIngredientEffects.Count);
        AssertEqual("Effect name", "Soft Bloom", result.TriggeredIngredientEffects[0].EffectName);
    }

    private static void TestIngredientEffectHalvesOtherRiskChances()
    {
        var service = new PotionBrewingService();
        var ingredients = new List<IngredientDef>
        {
            new()
            {
                Id = "gorse",
                Name = "Gorse",
                Quality = 80,
                IngredientEffects = new List<IngredientEffectDef>
                {
                    new()
                    {
                        Kind = IngredientEffectDef.HalveOtherRisksKind,
                        Name = "Gorse Binding"
                    }
                }
            },
            new()
            {
                Id = "elder",
                Name = "Elder",
                Quality = 80,
                Risks = new Dictionary<string, int>
                {
                    ["nausea"] = 3
                }
            },
            new()
            {
                Id = "yarrow",
                Name = "Yarrow",
                Quality = 80,
                Risks = new Dictionary<string, int>
                {
                    ["nausea"] = 1,
                    ["insomnia"] = 2
                }
            }
        };

        var result = service.PreviewPotion(ingredients, null);

        AssertEqual("Nausea chance halved", 2, result.PossibleRisks["nausea"]);
        AssertEqual("Insomnia chance halved", 1, result.PossibleRisks["insomnia"]);
        AssertEqual("Risk binder recorded", 1, result.TriggeredIngredientEffects.Count);
    }

    private static void TestIngredientEffectAddsVolatileRiskChance()
    {
        var service = new PotionBrewingService(() => 0.0f);
        var ingredients = new List<IngredientDef>
        {
            new()
            {
                Id = "elder",
                Name = "Elder",
                Quality = 80,
                Traits = new Dictionary<string, int>
                {
                    ["rest"] = 5
                },
                IngredientEffects = new List<IngredientEffectDef>
                {
                    new()
                    {
                        Kind = IngredientEffectDef.BoostStrongestTraitAddRiskKind,
                        Name = "Dark Solvent",
                        Amount = 2,
                        SecondaryAmount = 2,
                        RiskId = "corruption"
                    }
                }
            },
            new()
            {
                Id = "heather",
                Name = "Heather",
                Quality = 80,
                Traits = new Dictionary<string, int>
                {
                    ["calm"] = 1
                }
            }
        };

        var result = service.BrewPotion(ingredients, null);

        AssertEqual("Strongest trait boosted", 7, result.Traits["rest"]);
        AssertEqual("Possible corruption chance", 2, result.PossibleRisks["corruption"]);
        AssertEqual("Corruption carried", 1, result.Risks["corruption"]);
    }

    private static void TestIngredientEffectSuppressesSingleCarriedRisk()
    {
        var service = new PotionBrewingService(() => 0.0f);
        var ingredients = new List<IngredientDef>
        {
            new()
            {
                Id = "elder",
                Name = "Elder",
                Quality = 80,
                Risks = new Dictionary<string, int>
                {
                    ["nausea"] = 2
                }
            },
            new()
            {
                Id = "thyme",
                Name = "Thyme",
                Quality = 80,
                IngredientEffects = new List<IngredientEffectDef>
                {
                    new()
                    {
                        Kind = IngredientEffectDef.SuppressSingleCarriedRiskKind,
                        Name = "Iron Ward"
                    }
                }
            }
        };

        var result = service.BrewPotion(ingredients, null);

        AssertEqual("Possible risk remains visible", 2, result.PossibleRisks["nausea"]);
        AssertEqual("Carried risk suppressed", 0, result.Risks.Count);
        AssertEqual("Suppressing effect recorded", "Iron Ward", result.TriggeredIngredientEffects[0].EffectName);
    }

    private static void TestIngredientEffectsIgnoreIngredientOrder()
    {
        var service = new PotionBrewingService();
        var darkCatalyst = new IngredientDef
        {
            Id = "a_dark_catalyst",
            Name = "Dark Catalyst",
            Quality = 80,
            Traits = new Dictionary<string, int>
            {
                ["rest"] = 1
            },
            IngredientEffects = new List<IngredientEffectDef>
            {
                new()
                {
                    Kind = IngredientEffectDef.BoostStrongestTraitAddRiskKind,
                    Name = "Dark Solvent",
                    Amount = 2
                }
            }
        };
        var dreamBloom = new IngredientDef
        {
            Id = "m_dream_bloom",
            Name = "Dream Bloom",
            Quality = 80,
            Traits = new Dictionary<string, int>
            {
                ["dream"] = 5
            }
        };
        var silverTemper = new IngredientDef
        {
            Id = "z_silver_temper",
            Name = "Silver Temper",
            Quality = 80,
            Traits = new Dictionary<string, int>
            {
                ["clarity"] = 5
            },
            IngredientEffects = new List<IngredientEffectDef>
            {
                new()
                {
                    Kind = IngredientEffectDef.TemperTraitsKind,
                    Name = "Silver Measure",
                    Amount = 1
                }
            }
        };

        var firstResult = service.PreviewPotion(
            new List<IngredientDef> { darkCatalyst, dreamBloom, silverTemper },
            null);
        var reversedResult = service.PreviewPotion(
            new List<IngredientDef> { silverTemper, dreamBloom, darkCatalyst },
            null);

        AssertEqual("Clarity does not depend on queue order", firstResult.Traits["clarity"], reversedResult.Traits["clarity"]);
        AssertEqual("Dream does not depend on queue order", firstResult.Traits["dream"], reversedResult.Traits["dream"]);
        AssertEqual("Rest does not depend on queue order", firstResult.Traits["rest"], reversedResult.Traits["rest"]);
        AssertEqual("Effect count does not depend on queue order", firstResult.TriggeredIngredientEffects.Count, reversedResult.TriggeredIngredientEffects.Count);
    }

    private static void TestPositiveBrew()
    {
        var service = new PotionBrewingService();

        var ingredients = new List<IngredientDef>
        {
            new()
            {
                Id = "sleeping_herb",
                Name = "Sleeping Herb",
                Quality = 80,
                Traits = new Dictionary<string, int>
                {
                    ["sleep"] = 4,
                    ["calm"] = 2
                }
            },
            new()
            {
                Id = "moon_leaf",
                Name = "Moon Leaf",
                Quality = 60,
                Traits = new Dictionary<string, int>
                {
                    ["sleep"] = 1,
                    ["calm"] = 2
                }
            }
        };

        var request = new CustomerRequestDef
        {
            Id = "rest_request",
            Description = "A potion that calms and induces rest.",
            DesiredTraits = new Dictionary<string, CustomerTraitRangeDef>
            {
                ["sleep"] = Range(min: 5),
                ["calm"] = Range(min: 4)
            }
        };

        var result = service.BrewPotion(ingredients, request);

        AssertEqual("IngredientQualityScore", 70, result.IngredientQualityScore);
        AssertEqual("EffectFitScore", 100, result.EffectFitScore);
        AssertEqual("StabilityScore", 100, result.StabilityScore);
        AssertEqual("PenaltyScore", 0, result.PenaltyScore);
        AssertEqual("FinalScore", 91.0f, result.FinalScore);
        AssertEqual("Grade", "A", result.Grade);
    }

    private static void TestRiskPenaltyBrew()
    {
        var service = new PotionBrewingService(() => 0.0f);

        var ingredients = new List<IngredientDef>
        {
            new()
            {
                Id = "healing_bloom",
                Name = "Healing Bloom",
                Quality = 50,
                Traits = new Dictionary<string, int>
                {
                    ["healing"] = 3
                },
                Risks = new Dictionary<string, int>
                {
                    ["mutation"] = 4
                }
            }
        };

        var request = new CustomerRequestDef
        {
            Id = "anti_mutation",
            Description = "The customer wants healing without corruption.",
            DesiredTraits = new Dictionary<string, CustomerTraitRangeDef>
            {
                ["healing"] = Range(min: 3)
            },
            BadTraits = new Dictionary<string, CustomerTraitRangeDef>
            {
                ["mutation"] = Range(max: 0)
            }
        };

        var result = service.BrewPotion(ingredients, request);

        AssertEqual("IngredientQualityScore", 50, result.IngredientQualityScore);
        AssertEqual("EffectFitScore", 0, result.EffectFitScore);
        AssertEqual("StabilityScore", 98, result.StabilityScore);
        AssertEqual("PenaltyScore", 1, result.PenaltyScore);
        AssertEqual("FinalScore", 38.5f, result.FinalScore);
        AssertEqual("Grade", "F", result.Grade);
        AssertEqual("Mutation is stored as presence", 1, result.Risks["mutation"]);
    }

    private static CustomerTraitRangeDef Range(int? min = null, int? max = null)
    {
        return new CustomerTraitRangeDef
        {
            Min = min,
            Max = max
        };
    }
}
