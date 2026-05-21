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
        Run("Combines ingredient risks", TestCombinesIngredientRisks);
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
                    ["nausea"] = 2,
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
                    ["nausea"] = 3,
                    ["corrosion"] = 4
                }
            }
        };

        var result = service.BrewPotion(ingredients, null, new List<SynergyRule>());

        AssertEqual("Risk count", 3, result.Risks.Count);
        AssertEqual("nausea", 5, result.Risks["nausea"]);
        AssertEqual("instability", 1, result.Risks["instability"]);
        AssertEqual("corrosion", 4, result.Risks["corrosion"]);
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
        AssertTrue("Added risk present", result.Risks.ContainsKey("mutation"));
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
