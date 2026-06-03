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
        runner.Run("Failed carried risks do not affect synergies or scoring", TestFailedCarriedRisksDoNotAffectSynergiesOrScoring);
        runner.Run("Synergy-added risks roll before reaching the potion", TestSynergyAddedRisksRollBeforeReachingPotion);
        runner.Run("Applies risk and trait gated synergies", TestRiskAndTraitSynergyRequirement);
        runner.Run("Triggers healing_corruption from healing trait and corruption risk", TestHealingCorruptionFromTraitAndRisk);
        runner.Run("Scores a clean positive brew", TestPositiveBrew);
        runner.Run("Handles negative synergy and penalties", TestNegativeBrew);
    }

    private static void TestRejectsEmptyIngredients()
    {
        var service = new PotionBrewingService();
        var result = service.BrewPotion(new List<IngredientDef>(), null, new List<SynergyRule>());

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
                Id = "mooncap_mushroom",
                Name = "Mooncap Mushroom",
                Quality = 40,
                Traits = new Dictionary<string, int>
                {
                    ["sleep"] = 4,
                    ["dream"] = 3
                }
            },
            new()
            {
                Id = "grave_mint",
                Name = "Grave Mint",
                Quality = 40,
                Traits = new Dictionary<string, int>
                {
                    ["calm"] = 4,
                    ["memory"] = 2
                }
            }
        };

        var result = service.BrewPotion(ingredients, null, new List<SynergyRule>());

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

        var result = service.PreviewPotion(ingredients, null, new List<SynergyRule>());

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

        var result = service.BrewPotion(ingredients, null, new List<SynergyRule>());

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
                Id = "amber_nightshade",
                Name = "Amber Nightshade",
                Quality = 40,
                BasePrice = 12,
                Risks = new Dictionary<string, int>
                {
                    ["insomnia"] = 1
                }
            },
            new()
            {
                Id = "black_ichor",
                Name = "Black Ichor",
                Quality = 40,
                BasePrice = 18
            },
            new()
            {
                Id = "grave_mint",
                Name = "Grave Mint",
                Quality = 40,
                BasePrice = 8
            }
        };

        var result = service.BrewPotion(ingredients, null, new List<SynergyRule>());

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
                Id = "amber_nightshade",
                Name = "Amber Nightshade",
                Quality = 40,
                BasePrice = 12,
                Risks = new Dictionary<string, int>
                {
                    ["insomnia"] = 1
                }
            }
        };

        var result = service.BrewPotion(ingredients, null, new List<SynergyRule>());

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

        var result = service.BrewPotion(ingredients, null, new List<SynergyRule>());

        AssertEqual("Fever chance clamped", 10, result.PossibleRisks["fever"]);
        AssertTrue("Zero chance risk ignored", !result.PossibleRisks.ContainsKey("ignored"));
        AssertEqual("Clamped risk always carries", 1, result.Risks["fever"]);
    }

    private static void TestFailedCarriedRisksDoNotAffectSynergiesOrScoring()
    {
        var service = new PotionBrewingService(() => 0.99f);
        var ingredients = new List<IngredientDef>
        {
            new()
            {
                Id = "mooncap_mushroom",
                Name = "Mooncap Mushroom",
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
            DesiredTraits = new Dictionary<string, int>
            {
                ["healing"] = 2
            },
            BadTraits = new Dictionary<string, int>
            {
                ["corruption"] = 1
            }
        };

        var rules = new List<SynergyRule>
        {
            new()
            {
                Id = "healing_corruption",
                RequiredTraits = new List<string> { "healing" },
                RequiredRisks = new List<string> { "corruption" },
                Modifier = -20,
                ResultTrait = "unstable_regeneration"
            }
        };

        var result = service.BrewPotion(ingredients, request, rules);

        AssertEqual("Possible corruption chance", 1, result.PossibleRisks["corruption"]);
        AssertEqual("Carried risk count", 0, result.Risks.Count);
        AssertEqual("SynergyScore", 0, result.SynergyScore);
        AssertTrue("Risk-gated synergy did not trigger", !result.TriggeredSynergies.Contains("healing_corruption"));
        AssertTrue("Risk result trait not added", !result.Traits.ContainsKey("unstable_regeneration"));
        AssertEqual("EffectFitScore ignores failed risk", 100, result.EffectFitScore);
    }

    private static void TestSynergyAddedRisksRollBeforeReachingPotion()
    {
        var service = new PotionBrewingService(() => 0.49f);
        var ingredients = new List<IngredientDef>
        {
            new()
            {
                Id = "healing_bloom",
                Name = "Healing Bloom",
                Quality = 50,
                Traits = new Dictionary<string, int>
                {
                    ["healing"] = 3,
                    ["corruption"] = 2
                }
            }
        };

        var rules = new List<SynergyRule>
        {
            new()
            {
                Id = "healing_corruption",
                RequiredTraits = new List<string> { "healing", "corruption" },
                Modifier = -20,
                AddedRisk = "mutation",
                AddedRiskStrength = 5
            }
        };

        var result = service.BrewPotion(ingredients, null, rules);

        AssertTrue("healing_corruption triggered", result.TriggeredSynergies.Contains("healing_corruption"));
        AssertEqual("Possible mutation chance", 5, result.PossibleRisks["mutation"]);
        AssertEqual("Mutation is stored as presence", 1, result.Risks["mutation"]);
    }

    private static void TestRiskAndTraitSynergyRequirement()
    {
        var service = new PotionBrewingService(() => 0.0f);

        var ingredients = new List<IngredientDef>
        {
            new()
            {
                Id = "frost_mint",
                Name = "Frost Mint",
                Quality = 65,
                Traits = new Dictionary<string, int>
                {
                    ["calm"] = 3
                },
                Risks = new Dictionary<string, int>
                {
                    ["chill"] = 2
                }
            },
            new()
            {
                Id = "night_pollen",
                Name = "Night Pollen",
                Quality = 65,
                Traits = new Dictionary<string, int>
                {
                    ["sleep"] = 2
                }
            }
        };

        var rules = new List<SynergyRule>
        {
            new()
            {
                Id = "cold_slumber",
                RequiredTraits = new List<string> { "sleep", "calm" },
                RequiredRisks = new List<string> { "chill" },
                Modifier = 8,
                ResultTrait = "deep_rest"
            },
            new()
            {
                Id = "missing_risk_gate",
                RequiredTraits = new List<string> { "sleep" },
                RequiredRisks = new List<string> { "burn" },
                Modifier = 20,
                ResultTrait = "should_not_trigger"
            }
        };

        var result = service.BrewPotion(ingredients, null, rules);

        AssertEqual("SynergyScore", 8, result.SynergyScore);
        AssertTrue("Triggered includes risk-gated synergy", result.TriggeredSynergies.Contains("cold_slumber"));
        AssertTrue("Missing-risk rule does not trigger", !result.TriggeredSynergies.Contains("missing_risk_gate"));
        AssertTrue("Result trait added", result.Traits.ContainsKey("deep_rest"));
    }

    private static void TestHealingCorruptionFromTraitAndRisk()
    {
        var service = new PotionBrewingService(() => 0.0f);

        var ingredients = new List<IngredientDef>
        {
            new()
            {
                Id = "mooncap_mushroom",
                Name = "Mooncap Mushroom",
                Quality = 85,
                Traits = new Dictionary<string, int>
                {
                    ["healing"] = 2
                }
            },
            new()
            {
                Id = "lavender_ash",
                Name = "Lavender Ash",
                Quality = 80,
                Risks = new Dictionary<string, int>
                {
                    ["corruption"] = 1
                }
            }
        };

        var rules = new List<SynergyRule>
        {
            new()
            {
                Id = "healing_corruption",
                RequiredTraits = new List<string> { "healing" },
                RequiredRisks = new List<string> { "corruption" },
                Modifier = -20,
                ResultTrait = "unstable_regeneration",
                AddedRisk = "mutation",
                AddedRiskStrength = 4
            }
        };

        var result = service.BrewPotion(ingredients, null, rules);

        AssertTrue("healing_corruption triggered", result.TriggeredSynergies.Contains("healing_corruption"));
        AssertEqual("Mutation is stored as presence", 1, result.Risks["mutation"]);
        AssertTrue("synergy details include risk contribution", result.TriggeredSynergyDetails[0].ContributingRisks.ContainsKey("corruption"));
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
            DesiredTraits = new Dictionary<string, int>
            {
                ["sleep"] = 5,
                ["calm"] = 4,
                ["peaceful_sedation"] = 2
            }
        };

        var synergyRules = new List<SynergyRule>
        {
            new()
            {
                Id = "sleep_calm",
                RequiredTraits = new List<string> { "sleep", "calm" },
                Modifier = 10,
                ResultTrait = "peaceful_sedation",
                Description = "Sleep and calm combine into a smooth sedative effect."
            }
        };

        var result = service.BrewPotion(ingredients, request, synergyRules);

        AssertEqual("IngredientQualityScore", 70, result.IngredientQualityScore);
        AssertEqual("EffectFitScore", 100, result.EffectFitScore);
        AssertEqual("SynergyScore", 10, result.SynergyScore);
        AssertEqual("StabilityScore", 100, result.StabilityScore);
        AssertEqual("PenaltyScore", 0, result.PenaltyScore);
        AssertEqual("FinalScore", 70.0f, result.FinalScore);
        AssertEqual("Grade", "B-", result.Grade);
        AssertTrue("Triggered synergies includes sleep_calm", result.TriggeredSynergies.Contains("sleep_calm"));
        AssertTrue("Result trait added", result.Traits.ContainsKey("peaceful_sedation"));
    }

    private static void TestNegativeBrew()
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
                }
            },
            new()
            {
                Id = "corrupt_root",
                Name = "Corrupt Root",
                Quality = 50,
                Traits = new Dictionary<string, int>
                {
                    ["corruption"] = 2
                }
            }
        };

        var request = new CustomerRequestDef
        {
            Id = "anti_mutation",
            Description = "The customer wants healing without corruption.",
            DesiredTraits = new Dictionary<string, int>
            {
                ["healing"] = 3
            },
            BadTraits = new Dictionary<string, int>
            {
                ["mutation"] = 4
            }
        };

        var synergyRules = new List<SynergyRule>
        {
            new()
            {
                Id = "healing_corruption",
                RequiredTraits = new List<string> { "healing", "corruption" },
                Modifier = -20,
                ResultTrait = "unstable_regeneration",
                AddedRisk = "mutation",
                AddedRiskStrength = 4,
                Description = "Healing mixed with corruption creates mutation risk."
            }
        };

        var result = service.BrewPotion(ingredients, request, synergyRules);

        AssertEqual("IngredientQualityScore", 50, result.IngredientQualityScore);
        AssertEqual("SynergyScore", -20, result.SynergyScore);
        AssertEqual("EffectFitScore", 75, result.EffectFitScore);
        AssertEqual("StabilityScore", 92, result.StabilityScore);
        AssertEqual("PenaltyScore", 7, result.PenaltyScore);
        AssertEqual("FinalScore", 41.4f, result.FinalScore);
        AssertEqual("Grade", "F", result.Grade);
        AssertTrue("Triggered synergies includes healing_corruption", result.TriggeredSynergies.Contains("healing_corruption"));
        AssertEqual("Mutation is stored as presence", 1, result.Risks["mutation"]);
    }
}
