using System;
using System.Collections.Generic;
using OccultShop.Models;
using OccultShop.Persistence;
using OccultShop.Systems;
using OccultShop.Tutorial;
using static TestAssert;

internal static class CoreSystemContractTests
{
    public static void Register(TestRunner runner)
    {
        runner.Run("Brewing copies the strongest other trait", TestBrewingCopiesStrongestOtherTrait);
        runner.Run("Brewing reduces the highest possible risk", TestBrewingReducesHighestRisk);
        runner.Run("Brewing tempers strongest and weakest traits", TestBrewingTempersTraitExtremes);
        runner.Run("Brewing no-risk effects wait for actual risk rolls", TestNoRiskEffectsWaitForActualRiskRolls);
        runner.Run("Brewing risk-carried effects add conditional traits", TestRiskCarriedEffectsAddConditionalTraits);
        runner.Run("Potion item evaluation normalizes carried risks", TestPotionItemEvaluationNormalizesCarriedRisks);
        runner.Run("Customer sale rules allow one miss on larger desired sets", TestCustomerDesiredTraitTolerance);
        runner.Run("Customer potion responses match score grade and trait counts", TestCustomerPotionResponsesMatchContracts);
        runner.Run("Inventory state enforces potion and consumable caps", TestInventoryStateCaps);
        runner.Run("Inventory state consumes each ingredient stack only", TestInventoryStateConsumesEachIngredientOnly);
        runner.Run("Potion batch store preserves measured FIFO batches", TestPotionBatchStoreMeasuredFifo);
        runner.Run("Potion batch store backfills legacy unmeasured batches", TestPotionBatchStoreLegacyBackfill);
        runner.Run("Brew pricing keeps floor and quality bonus", TestBrewPricing);
        runner.Run("Garden state plants grows and harvests deterministically", TestGardenStatePlantGrowHarvest);
        runner.Run("Tutorial progress snapshot compatibility is behavioral", TestTutorialProgressSnapshotCompatibility);
    }

    private static void TestBrewingCopiesStrongestOtherTrait()
    {
        var service = new PotionBrewingService();
        var result = service.PreviewPotion(
            new List<IngredientDef>
            {
                Ingredient(
                    "echo_moss",
                    effects: new List<IngredientEffectDef>
                    {
                        new()
                        {
                            Kind = IngredientEffectDef.CopyStrongestOtherTraitKind,
                            Name = "Moon Echo"
                        }
                    }),
                Ingredient("dream_bloom", traits: new Dictionary<string, int> { ["dream"] = 5 }),
                Ingredient("rest_leaf", traits: new Dictionary<string, int> { ["rest"] = 3 })
            },
            null);

        AssertEqual("Dream copied half rounded up", 8, result.Traits["dream"]);
        AssertEqual("Rest remains", 3, result.Traits["rest"]);
        AssertEqual("Triggered effect", "Moon Echo", result.TriggeredIngredientEffects[0].EffectName);
    }

    private static void TestBrewingReducesHighestRisk()
    {
        var service = new PotionBrewingService();
        var result = service.PreviewPotion(
            new List<IngredientDef>
            {
                Ingredient(
                    "silver_thread",
                    effects: new List<IngredientEffectDef>
                    {
                        new()
                        {
                            Kind = IngredientEffectDef.ReduceHighestRiskKind,
                            Name = "Silver Binding",
                            Amount = 2
                        }
                    }),
                Ingredient("nightshade", risks: new Dictionary<string, int> { ["nausea"] = 5 }),
                Ingredient("spore", risks: new Dictionary<string, int> { ["rot"] = 3 })
            },
            null);

        AssertEqual("Highest risk reduced", 3, result.PossibleRisks["nausea"]);
        AssertEqual("Other risk remains", 3, result.PossibleRisks["rot"]);
        AssertEqual("Triggered effect count", 1, result.TriggeredIngredientEffects.Count);
    }

    private static void TestBrewingTempersTraitExtremes()
    {
        var service = new PotionBrewingService();
        var result = service.PreviewPotion(
            new List<IngredientDef>
            {
                Ingredient(
                    "silver_measure",
                    effects: new List<IngredientEffectDef>
                    {
                        new()
                        {
                            Kind = IngredientEffectDef.TemperTraitsKind,
                            Name = "Silver Measure",
                            Amount = 3
                        }
                    }),
                Ingredient("clear_root", traits: new Dictionary<string, int> { ["clarity"] = 8 }),
                Ingredient("soft_leaf", traits: new Dictionary<string, int> { ["calm"] = 2 })
            },
            null);

        AssertEqual("Strongest trait reduced", 5, result.Traits["clarity"]);
        AssertEqual("Weakest trait boosted", 5, result.Traits["calm"]);
    }

    private static void TestNoRiskEffectsWaitForActualRiskRolls()
    {
        var service = new PotionBrewingService(() => 0.99f);
        var ingredients = new List<IngredientDef>
        {
            Ingredient(
                "quiet_bell",
                effects: new List<IngredientEffectDef>
                {
                    new()
                    {
                        Kind = IngredientEffectDef.BoostLowestTraitIfNoRiskCarriesKind,
                        Name = "Quiet Bell",
                        Amount = 2
                    }
                }),
            Ingredient(
                "unstable_leaf",
                traits: new Dictionary<string, int> { ["clarity"] = 3 },
                risks: new Dictionary<string, int> { ["nausea"] = 2 })
        };

        var preview = service.PreviewPotion(ingredients, null);
        var brewed = service.BrewPotion(ingredients, null);

        AssertEqual("Preview keeps possible risk visible", 2, preview.PossibleRisks["nausea"]);
        AssertEqual("Preview does not apply no-risk effect before a roll", 3, preview.Traits["clarity"]);
        AssertEqual("Brew carries no risk after failed roll", 0, brewed.Risks.Count);
        AssertEqual("Brew applies no-risk effect after failed roll", 5, brewed.Traits["clarity"]);
    }

    private static void TestRiskCarriedEffectsAddConditionalTraits()
    {
        var service = new PotionBrewingService(() => 0.0f);
        var result = service.BrewPotion(
            new List<IngredientDef>
            {
                Ingredient(
                    "fever_bloom",
                    risks: new Dictionary<string, int> { ["fever"] = 2 },
                    effects: new List<IngredientEffectDef>
                    {
                        new()
                        {
                            Kind = IngredientEffectDef.AddTraitIfRiskCarriesKind,
                            Name = "Fever Lesson",
                            TraitId = "resilience",
                            Amount = 4
                        }
                    })
            },
            null);

        AssertEqual("Fever carried as presence", 1, result.Risks["fever"]);
        AssertEqual("Conditional trait added", 4, result.Traits["resilience"]);
    }

    private static void TestPotionItemEvaluationNormalizesCarriedRisks()
    {
        var service = new PotionBrewingService(() => throw new InvalidOperationException("EvaluatePotionItem must not roll risks."));
        var potion = new ItemDef
        {
            Id = "brew_clear_sleep",
            Name = "Clear Sleep",
            Quality = 150,
            Traits = new Dictionary<string, int> { ["sleep"] = 3 },
            Risks = new Dictionary<string, int>
            {
                ["nausea"] = 7,
                ["ignored"] = 0
            }
        };
        var request = new CustomerRequestDef
        {
            DesiredTraits = new Dictionary<string, CustomerTraitRangeDef>
            {
                ["sleep"] = new() { Min = 3 }
            },
            BadTraits = new Dictionary<string, CustomerTraitRangeDef>
            {
                ["nausea"] = new() { Max = 0 }
            }
        };

        var result = service.EvaluatePotionItem(potion, request);

        AssertEqual("Quality is clamped", 100, result.IngredientQualityScore);
        AssertEqual("Active risk normalized to presence", 1, result.Risks["nausea"]);
        AssertTrue("Zero risk removed", !result.Risks.ContainsKey("ignored"));
        AssertEqual("Possible risks mirror active potion risks", 1, result.PossibleRisks["nausea"]);
        AssertEqual("Bad carried risk affects fit", 0, result.EffectFitScore);
    }

    private static void TestCustomerDesiredTraitTolerance()
    {
        var request = new CustomerRequestDef
        {
            DesiredTraits = new Dictionary<string, CustomerTraitRangeDef>
            {
                ["sleep"] = new() { Min = 2 },
                ["calm"] = new() { Min = 2 },
                ["dream"] = new() { Min = 2 }
            }
        };

        AssertEqual("Three desired traits allow one miss", 2, CustomerSaleRules.GetRequiredDesiredTraitMatchCount(3));
        AssertTrue(
            "Two of three desired traits satisfy the flexible request",
            CustomerSaleRules.HasAllDesiredTraitsPresent(
                request,
                new Dictionary<string, int> { ["sleep"] = 2, ["calm"] = 2 }));
        AssertTrue(
            "One of three desired traits does not satisfy the flexible request",
            !CustomerSaleRules.HasAllDesiredTraitsPresent(
                request,
                new Dictionary<string, int> { ["sleep"] = 2 }));
        AssertTrue(
            "Ingredient amount requirements remain mandatory",
            !CustomerSaleRules.IsRequestSatisfiedByPotion(
                request,
                new PotionResult { Traits = new Dictionary<string, int> { ["sleep"] = 2, ["calm"] = 2 } },
                ingredientAmountRequirementsMet: false));
    }

    private static void TestCustomerPotionResponsesMatchContracts()
    {
        var request = new CustomerRequestDef
        {
            DesiredTraits = new Dictionary<string, CustomerTraitRangeDef>
            {
                ["sleep"] = new() { Min = 2 },
                ["calm"] = new() { Min = 2 },
                ["dream"] = new() { Min = 2 }
            },
            BadTraits = new Dictionary<string, CustomerTraitRangeDef>
            {
                ["nausea"] = new() { Max = 0 }
            }
        };
        var result = new PotionResult
        {
            Grade = "A",
            FinalScore = 92.5f,
            Traits = new Dictionary<string, int>
            {
                ["sleep"] = 2,
                ["calm"] = 2
            },
            Risks = new Dictionary<string, int>
            {
                ["nausea"] = 1
            }
        };
        var response = new CustomerPotionResponseDef
        {
            Success = true,
            PotionItemId = "brew_rest",
            Grade = "A",
            MinFinalScore = 90,
            MaxFinalScore = 95,
            MinMatchedDesiredTraits = 2,
            MaxMatchedBadTraits = 1
        };

        AssertTrue(
            "Response matches item score grade and trait counts",
            CustomerSaleRules.PotionResponseMatches(response, "BREW_REST", request, result, isSuccess: true));

        response.MaxMatchedBadTraits = 0;
        AssertTrue(
            "Response rejects too many bad traits",
            !CustomerSaleRules.PotionResponseMatches(response, "brew_rest", request, result, isSuccess: true));
    }

    private static void TestInventoryStateCaps()
    {
        var inventory = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();
        var state = CreateInventoryState(inventory, errors, maxUniquePotions: 2, maxPotionStack: 3, maxUniqueConsumables: 2, maxConsumableStack: 3);

        var firstPotionAdd = state.AddItem("potion_a", 2);
        var cappedPotionAdd = state.AddItem("potion_a", 5);
        state.AddItem("potion_b", 1);
        var blockedPotionAdd = state.AddItem("potion_c", 1);

        AssertEqual("Initial potion add", 2, firstPotionAdd.AddedQuantity);
        AssertEqual("Potion stack capped add", 1, cappedPotionAdd.AddedQuantity);
        AssertEqual("Potion A capped quantity", 3, inventory["potion_a"]);
        AssertEqual("Blocked unique potion add", 0, blockedPotionAdd.AddedQuantity);

        state.AddItem("consumable_a", 1);
        state.AddItem("consumable_b", 1);
        var pendingConsumableAdd = state.AddItem("consumable_c", 5);

        AssertEqual("Blocked consumable add stored as pending", 0, pendingConsumableAdd.AddedQuantity);
        AssertTrue("Consumable pending grant changes state", pendingConsumableAdd.Changed);
        AssertEqual("Pending consumable item", "consumable_c", state.PendingConsumableItemId);
        AssertEqual("Pending consumable quantity", 5, state.PendingConsumableQuantity);

        var accepted = state.TryAcceptPendingConsumableByDiscarding("consumable_a", out var error);
        AssertTrue($"Pending grant accepted: {error}", accepted.Accepted);
        AssertTrue("Discarded consumable removed", !inventory.ContainsKey("consumable_a"));
        AssertEqual("Pending grant stack capped", 3, inventory["consumable_c"]);
        AssertTrue("Pending grant cleared", !state.HasPendingConsumableGrant);
        AssertTrue("Cap errors are observable", errors.Count >= 3);
    }

    private static void TestInventoryStateConsumesEachIngredientOnly()
    {
        var inventory = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["ingredient_mint"] = 3,
            ["ingredient_gorse"] = 1,
            ["potion_sleep"] = 2,
            ["consumable_salt"] = 1
        };
        var state = CreateInventoryState(inventory, new List<string>(), maxUniquePotions: 4, maxPotionStack: 10, maxUniqueConsumables: 4, maxConsumableStack: 10);

        var consumed = state.ConsumeEachIngredient(2);

        AssertEqual("Consumed from ingredient stacks only", 3, consumed);
        AssertEqual("Partial ingredient stack remains", 1, inventory["ingredient_mint"]);
        AssertTrue("Fully consumed ingredient stack removed", !inventory.ContainsKey("ingredient_gorse"));
        AssertEqual("Potion stack untouched", 2, inventory["potion_sleep"]);
        AssertEqual("Consumable stack untouched", 1, inventory["consumable_salt"]);
    }

    private static void TestPotionBatchStoreMeasuredFifo()
    {
        var store = new PotionBatchStore();
        store.RecordPotionBatch(
            "brew_rest",
            new List<IngredientPortionDef>
            {
                new() { IngredientId = "heather", ItemId = "heather__prep_steeped", PreparationId = "steeped", Grams = 6 },
                new() { IngredientId = "elder", ItemId = "elder__prep_raw", PreparationId = "raw", Grams = 2 },
                new() { IngredientId = "rosemary", ItemId = "rosemary__prep_boiled", PreparationId = "boiled", Grams = 1 }
            });
        store.RecordPotionBatch(
            "brew_rest",
            new List<IngredientPortionDef>
            {
                new() { IngredientId = "mint", ItemId = "mint", Grams = 0 },
                new() { IngredientId = "gorse", ItemId = "gorse", Grams = 0 },
                new() { IngredientId = "thyme", ItemId = "thyme", Grams = 0 }
            });

        AssertTrue("Measured batch peeks", store.TryPeekPotionIngredientPortionBatch("BREW_REST", out var firstBatch));
        firstBatch[0].Grams = 99;
        AssertTrue("Measured batch peeks again", store.TryPeekPotionIngredientPortionBatch("brew_rest", out var firstBatchAgain));
        AssertEqual("Peek returns clones", 6, firstBatchAgain[0].Grams);

        store.ConsumePotionBatches("brew_rest", 1);
        AssertTrue("Second batch remains after consuming first", store.TryPeekPotionBatch("brew_rest", out var secondIngredientIds));
        AssertEqual("FIFO second batch first ingredient", "mint", secondIngredientIds[0]);
    }

    private static void TestPotionBatchStoreLegacyBackfill()
    {
        var store = new PotionBatchStore();
        store.Restore(
            new Dictionary<string, List<List<string>>>(StringComparer.OrdinalIgnoreCase)
            {
                ["legacy_brew"] = new()
                {
                    new List<string> { "mint", "gorse", "thyme" }
                }
            },
            null);

        AssertTrue("Legacy potion portions are backfilled", store.TryPeekPotionIngredientPortionBatch("legacy_brew", out var portions));
        AssertEqual("Legacy portion ingredient id", "mint", portions[0].IngredientId);
        AssertEqual("Legacy portion item id", "mint", portions[0].ItemId);
        AssertEqual("Legacy portion grams", 0, portions[0].Grams);
    }

    private static void TestBrewPricing()
    {
        AssertEqual(
            "Brew cost has minimum floor",
            5,
            BrewPricing.CalculateBrewCost(2, new PotionResult { IngredientQualityScore = 50 }));
        AssertEqual(
            "Brew cost includes ingredient percentage and quality bonus",
            15,
            BrewPricing.CalculateBrewCost(40, new PotionResult { IngredientQualityScore = 80 }));
    }

    private static void TestGardenStatePlantGrowHarvest()
    {
        var errors = new List<string>();
        var garden = new GardenState(itemId => string.Equals(itemId, "yarrow", StringComparison.OrdinalIgnoreCase), errors.Add);

        garden.InitializeNewGarden();
        AssertEqual("Starting pot count", GardenState.StartingPotCount, garden.PotCount);
        AssertEqual("Starting yarrow seed", 1, garden.GetSeedQuantity("seed_yarrow"));

        AssertTrue("Plant yarrow seed", garden.TryPlantSeed(0, "seed_yarrow", day: 1, out var plantedIngredientId, out var plantError));
        AssertEqual($"Plant error: {plantError}", "yarrow", plantedIngredientId);
        AssertEqual("Seed consumed", 0, garden.GetSeedQuantity("seed_yarrow"));
        AssertTrue(
            "Cannot harvest before growth",
            !garden.TryHarvestGardenPot(0, out _, out var earlyHarvestError) &&
            earlyHarvestError.Contains("still growing", StringComparison.OrdinalIgnoreCase));

        garden.AdvanceGrowth();
        AssertTrue("Harvest ready yarrow", garden.TryHarvestGardenPot(0, out var harvest, out var harvestError));
        AssertEqual($"Harvest error: {harvestError}", "yarrow", harvest.IngredientId);
        AssertEqual("Harvest quantity", GardenState.DefaultHarvestYield, harvest.Quantity);
        AssertEqual("Seed returned", 1, garden.GetSeedQuantity("seed_yarrow"));
        AssertTrue("Pot is empty after harvest", garden.GardenPots[0].IsEmpty);
        AssertEqual("No harvest errors pushed", 0, errors.Count);
    }

    private static void TestTutorialProgressSnapshotCompatibility()
    {
        var progress = new TutorialProgressState();
        progress.ApplySnapshot(new GameStateSnapshot
        {
            TutorialStatus = TutorialStatus.Completed,
            TutorialRequested = true,
            TutorialSkipped = true,
            TutorialStepIndex = -1,
            TutorialStep = 5
        });

        AssertEqual("Explicit status wins over legacy flags", TutorialStatus.Completed.ToString(), progress.Status.ToString());
        AssertEqual("Legacy tutorial step fallback restored", 5, progress.Step);

        progress.ApplySnapshot(new GameStateSnapshot
        {
            TutorialRequested = true,
            TutorialCompleted = true,
            TutorialStepIndex = 7,
            TutorialStep = 2
        });

        AssertEqual("Legacy completed wins over requested", TutorialStatus.Completed.ToString(), progress.Status.ToString());
        AssertEqual("Tutorial step index wins when positive", 7, progress.Step);

        progress.Skip();
        AssertEqual("Skip clears step", 0, progress.Step);
        AssertEqual("Skip status", TutorialStatus.Skipped.ToString(), progress.Status.ToString());
    }

    private static InventoryState CreateInventoryState(
        Dictionary<string, int> inventory,
        List<string> errors,
        int maxUniquePotions,
        int maxPotionStack,
        int maxUniqueConsumables,
        int maxConsumableStack)
    {
        return new InventoryState(
            inventory,
            itemId => !string.IsNullOrWhiteSpace(itemId),
            itemId => itemId.StartsWith("potion_", StringComparison.OrdinalIgnoreCase),
            itemId => itemId.StartsWith("consumable_", StringComparison.OrdinalIgnoreCase),
            itemId => itemId.StartsWith("ingredient_", StringComparison.OrdinalIgnoreCase),
            errors.Add,
            maxUniquePotions,
            maxPotionStack,
            maxUniqueConsumables,
            maxConsumableStack);
    }

    private static IngredientDef Ingredient(
        string id,
        Dictionary<string, int>? traits = null,
        Dictionary<string, int>? risks = null,
        List<IngredientEffectDef>? effects = null)
    {
        return new IngredientDef
        {
            Id = id,
            Name = id,
            Quality = 80,
            Traits = traits ?? new Dictionary<string, int>(),
            Risks = risks ?? new Dictionary<string, int>(),
            IngredientEffects = effects ?? new List<IngredientEffectDef>()
        };
    }
}
