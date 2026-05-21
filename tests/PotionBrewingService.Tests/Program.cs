using System;
using System.Collections.Generic;
using OccultShop.Models;
using OccultShop.Systems;

static class Program
{
    private static int _failures;

    public static int Main()
    {
        Run("Rejects empty ingredient lists", TestRejectsEmptyIngredients);
        Run("Combines ingredient traits", TestCombinesIngredientTraits);
        Run("Keeps the top two ingredient risks", TestCombinesIngredientRisks);
        Run("Applies risk and trait gated synergies", TestRiskAndTraitSynergyRequirement);
        Run("Triggers healing_corruption from healing trait and corruption risk", TestHealingCorruptionFromTraitAndRisk);
        Run("Scores a clean positive brew", TestPositiveBrew);
        Run("Handles negative synergy and penalties", TestNegativeBrew);

        if (_failures > 0)
        {
            Console.Error.WriteLine($"Test run failed: {_failures} case(s) failed.");
            return 1;
        }

        Console.WriteLine("All PotionBrewingService tests passed.");
        return 0;
    }

    private static void Run(string name, Action test)
    {
        try
        {
            test();
            Console.WriteLine($"PASS: {name}");
        }
        catch (Exception ex)
        {
            _failures++;
            Console.Error.WriteLine($"FAIL: {name}");
            Console.Error.WriteLine(ex.Message);
        }
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

    private static void TestCombinesIngredientRisks()
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

        var result = service.BrewPotion(ingredients, null, new List<SynergyRule>());

        var risks = new List<KeyValuePair<string, int>>(result.Risks);

        AssertEqual("Risk count", 2, risks.Count);
        AssertEqual("First risk name", "nausea", risks[0].Key);
        AssertEqual("First risk strength", 6, risks[0].Value);
        AssertEqual("Second risk name", "corrosion", risks[1].Key);
        AssertEqual("Second risk strength", 4, risks[1].Value);
        AssertTrue("Lower risks removed", !result.Risks.ContainsKey("rot") && !result.Risks.ContainsKey("instability"));
    }

    private static void TestNegativeBrew()
    {
        var service = new PotionBrewingService();

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
        AssertEqual("EffectFitScore", 0, result.EffectFitScore);
        AssertEqual("StabilityScore", 80, result.StabilityScore);
        AssertEqual("PenaltyScore", 8, result.PenaltyScore);
        AssertEqual("FinalScore", 15.5f, result.FinalScore);
        AssertEqual("Grade", "F", result.Grade);
        AssertTrue("Triggered synergies includes healing_corruption", result.TriggeredSynergies.Contains("healing_corruption"));
        AssertTrue("Mutation removed from potion details", !result.Risks.ContainsKey("mutation"));
    }

    private static void TestRiskAndTraitSynergyRequirement()
    {
        var service = new PotionBrewingService();

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
        var service = new PotionBrewingService();

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
        AssertTrue("Mutation removed from potion details", !result.Risks.ContainsKey("mutation"));
        AssertTrue("synergy details include risk contribution", result.TriggeredSynergyDetails[0].ContributingRisks.ContainsKey("corruption"));
    }

    private static void AssertEqual<T>(string name, T expected, T actual) where T : IEquatable<T>
    {
        if (!expected.Equals(actual))
            throw new InvalidOperationException($"{name}: expected {expected}, got {actual}");
    }

    private static void AssertEqual(string name, float expected, float actual, float tolerance = 0.01f)
    {
        if (MathF.Abs(expected - actual) > tolerance)
            throw new InvalidOperationException($"{name}: expected {expected}, got {actual}");
    }

    private static void AssertTrue(string name, bool condition)
    {
        if (!condition)
            throw new InvalidOperationException($"{name}: expected condition to be true");
    }
}
