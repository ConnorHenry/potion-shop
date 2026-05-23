using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using OccultShop.Models;
using OccultShop.Systems;

static class Program
{
    private static int _failures;
    private static readonly Lazy<Assembly> UiAssembly = new(LoadUiAssembly);
    private static bool _resolverRegistered;

    public static int Main()
    {
        Run("Rejects empty ingredient lists", TestRejectsEmptyIngredients);
        Run("Combines ingredient traits", TestCombinesIngredientTraits);
        Run("Keeps the top two ingredient risks", TestCombinesIngredientRisks);
        Run("Applies risk and trait gated synergies", TestRiskAndTraitSynergyRequirement);
        Run("Triggers healing_corruption from healing trait and corruption risk", TestHealingCorruptionFromTraitAndRisk);
        Run("Scores a clean positive brew", TestPositiveBrew);
        Run("Handles negative synergy and penalties", TestNegativeBrew);
        Run("UI classes exist and keep expected base types", TestUiClassPresenceAndBaseTypes);
        Run("InventoryPanel splits inventory labels predictably", TestInventoryPanelSplitInventoryName);
        Run("InventoryPanel dictionary formatting is stable", TestInventoryPanelFormatDictionary);
        Run("InventoryPanel top-traits formatting is stable", TestInventoryPanelFormatTopTraits);
        Run("RecipeBookPanel dictionary formatting is stable", TestRecipeBookPanelFormatDictionary);
        Run("RecipeBookPanel top-traits formatting is stable", TestRecipeBookPanelFormatTopTraits);
        Run("BrewPanel ingredient tag detection is case-insensitive", TestBrewPanelIsIngredient);
        Run("BrewPanel rejects duplicate queued ingredients", TestBrewPanelRejectsDuplicateQueuedIngredients);
        Run("BrewPanel base price calculation is stable", TestBrewPanelCalculatePotionBasePrice);
        Run("CustomerPanel creates detached ingredient snapshots", TestCustomerPanelBuildPotionIngredientDef);
        Run("RuntimeContentDb stores generated items separately", TestRuntimeContentDbSeparatesRuntimeItems);
        Run("DataDb does not expose runtime registration", TestDataDbDoesNotExposeRuntimeRegistration);
        Run("DataDb reloads authored JSON only", TestDataDbReloadsAuthoredJsonOnly);
        Run("UI lookup uses the runtime-first item catalog", TestUiLookupUsesRuntimeFirstCatalog);
        Run("Main menu exposes start and load flows", TestMainMenuLoadFlow);
        Run("Load menu scene is wired for saved game browsing", TestLoadGameMenuScene);
        Run("SaveGameManager stores saves in a dedicated directory", TestSaveGameManagerUsesSaveDirectory);
        Run("Hud return-to-menu does not auto-save", TestHudReturnToMainMenuDoesNotAutoSave);
        Run("Persistence boundary stays separated", TestPersistenceBoundaryIsDocumented);

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

    private static void TestUiClassPresenceAndBaseTypes()
    {
        var expectedClasses = new Dictionary<string, string>
        {
            ["OccultShop.UI.BrewPanel"] = "Control",
            ["OccultShop.UI.BrewDropBox"] = "PanelContainer",
            ["OccultShop.UI.CustomerPanel"] = "Control",
            ["OccultShop.UI.CustomerSellDropBox"] = "PanelContainer",
            ["OccultShop.UI.DraggablePanel"] = "PanelContainer",
            ["OccultShop.UI.EventModal"] = "Control",
            ["OccultShop.UI.Hud"] = "Control",
            ["OccultShop.UI.LoadGameMenu"] = "Control",
            ["OccultShop.UI.InventoryItemSlot"] = "Button",
            ["OccultShop.UI.InventoryPanel"] = "Control",
            ["OccultShop.UI.RecipeBookPanel"] = "Control",
            ["MainMenu"] = "Control"
        };

        foreach (var expected in expectedClasses)
        {
            var type = GetTypeFromUiAssembly(expected.Key);
            var baseTypeName = type.BaseType?.Name ?? string.Empty;
            AssertEqual($"{expected.Key} base type", expected.Value, baseTypeName);
        }
    }

    private static void TestInventoryPanelSplitInventoryName()
    {
        var type = GetTypeFromUiAssembly("OccultShop.UI.InventoryPanel");
        var method = type.GetMethod("SplitInventoryName", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Missing method InventoryPanel.SplitInventoryName.");

        var splitArgs = new object?[] { "Moon Dust", null, null };
        method.Invoke(null, splitArgs);
        AssertEqual("Split first line", "Moon", splitArgs[1] as string ?? string.Empty);
        AssertEqual("Split second line", "Dust", splitArgs[2] as string ?? string.Empty);

        var singleWordArgs = new object?[] { "Elixir", null, null };
        method.Invoke(null, singleWordArgs);
        AssertEqual("Single word first line", "Elixir", singleWordArgs[1] as string ?? string.Empty);
        AssertEqual("Single word second line", string.Empty, singleWordArgs[2] as string ?? string.Empty);

        var emptyArgs = new object?[] { string.Empty, null, null };
        method.Invoke(null, emptyArgs);
        AssertEqual("Empty first line", string.Empty, emptyArgs[1] as string ?? string.Empty);
        AssertEqual("Empty second line", string.Empty, emptyArgs[2] as string ?? string.Empty);
    }

    private static void TestInventoryPanelFormatDictionary()
    {
        var values = new Dictionary<string, int>
        {
            ["zeta"] = 2,
            ["beta"] = 4,
            ["alpha"] = 4
        };

        var formatted = InvokePrivateStatic<string>("OccultShop.UI.InventoryPanel", "FormatDictionary", values);
        AssertEqual("Inventory dictionary order", "alpha: 4\nbeta: 4\nzeta: 2", formatted);

        var empty = InvokePrivateStatic<string>("OccultShop.UI.InventoryPanel", "FormatDictionary", new Dictionary<string, int>());
        AssertEqual("Inventory dictionary empty", "None", empty);

        var nullValue = InvokePrivateStatic<string>("OccultShop.UI.InventoryPanel", "FormatDictionary", (object?)null);
        AssertEqual("Inventory dictionary null", "None", nullValue);
    }

    private static void TestInventoryPanelFormatTopTraits()
    {
        var values = new Dictionary<string, int>
        {
            ["chaos"] = 1,
            ["sleep"] = 5,
            ["focus"] = 5,
            ["calm"] = 2
        };

        var formatted = InvokePrivateStatic<string>("OccultShop.UI.InventoryPanel", "FormatTopTraits", values, 2);
        AssertEqual("Inventory top traits order", "focus: 5\nsleep: 5", formatted);

        var empty = InvokePrivateStatic<string>("OccultShop.UI.InventoryPanel", "FormatTopTraits", new Dictionary<string, int>(), 3);
        AssertEqual("Inventory top traits empty", "None", empty);
    }

    private static void TestRecipeBookPanelFormatDictionary()
    {
        var values = new Dictionary<string, int>
        {
            ["zeta"] = 2,
            ["beta"] = 4,
            ["alpha"] = 4
        };

        var formatted = InvokePrivateStatic<string>("OccultShop.UI.RecipeBookPanel", "FormatDictionary", values);
        AssertEqual("Recipe dictionary order", "alpha: 4, beta: 4, zeta: 2", formatted);

        var empty = InvokePrivateStatic<string>("OccultShop.UI.RecipeBookPanel", "FormatDictionary", new Dictionary<string, int>());
        AssertEqual("Recipe dictionary empty", "None", empty);
    }

    private static void TestRecipeBookPanelFormatTopTraits()
    {
        var values = new Dictionary<string, int>
        {
            ["chaos"] = 1,
            ["sleep"] = 5,
            ["focus"] = 5,
            ["calm"] = 2
        };

        var formatted = InvokePrivateStatic<string>("OccultShop.UI.RecipeBookPanel", "FormatTopTraits", values, 2);
        AssertEqual("Recipe top traits order", "focus: 5, sleep: 5", formatted);

        var empty = InvokePrivateStatic<string>("OccultShop.UI.RecipeBookPanel", "FormatTopTraits", new Dictionary<string, int>(), 3);
        AssertEqual("Recipe top traits empty", "None", empty);
    }

    private static void TestBrewPanelIsIngredient()
    {
        var itemDefType = GetTypeFromUiAssembly("OccultShop.Models.ItemDef");
        var ingredientItem = Activator.CreateInstance(itemDefType)
            ?? throw new InvalidOperationException("Failed to create ItemDef instance.");
        var nonIngredientItem = Activator.CreateInstance(itemDefType)
            ?? throw new InvalidOperationException("Failed to create ItemDef instance.");

        SetProperty(ingredientItem, "Tags", new List<string> { "ingredient", "rare" });
        SetProperty(nonIngredientItem, "Tags", new List<string> { "potion" });

        var ingredientResult = InvokePrivateStatic<bool>("OccultShop.UI.BrewPanel", "IsIngredient", ingredientItem);
        var nonIngredientResult = InvokePrivateStatic<bool>("OccultShop.UI.BrewPanel", "IsIngredient", nonIngredientItem);

        AssertTrue("Ingredient tag recognized", ingredientResult);
        AssertTrue("Non-ingredient rejected", !nonIngredientResult);
    }

    private static void TestBrewPanelRejectsDuplicateQueuedIngredients()
    {
        var source = ReadProjectFile("Scripts/UI/BrewPanel.cs");

        AssertTrue("BrewPanel prevents duplicate queue entries",
            source.Contains("Each ingredient can only be used once per potion."));
        AssertTrue("BrewPanel checks the current queue before consuming inventory",
            source.Contains("_queuedIngredients.Any(x => string.Equals(x, itemId, System.StringComparison.OrdinalIgnoreCase))"));
        AssertTrue("BrewPanel queue summary shows unique ingredients without stack counts",
            source.Contains("string.Join(\", \", _queuedIngredients.Select(ItemName))"));
        AssertTrue("Inventory drag/drop still routes through TryQueueIngredient",
            ReadProjectFile("Scripts/UI/InventoryPanel.cs").Contains("_brewPanel.TryQueueIngredient(itemId);"));
        AssertTrue("Brew drop box still emits dragged item ids",
            ReadProjectFile("Scripts/UI/BrewDropBox.cs").Contains("EmitSignal(SignalName.ItemDropped, data.AsString());"));
    }

    private static void TestBrewPanelCalculatePotionBasePrice()
    {
        var potionResultType = GetTypeFromUiAssembly("PotionResult");

        var highQualityResult = Activator.CreateInstance(potionResultType)
            ?? throw new InvalidOperationException("Failed to create PotionResult instance.");
        SetProperty(highQualityResult, "IngredientQualityScore", 80);

        var lowQualityResult = Activator.CreateInstance(potionResultType)
            ?? throw new InvalidOperationException("Failed to create PotionResult instance.");
        SetProperty(lowQualityResult, "IngredientQualityScore", 10);

        var highQualityPrice = InvokePrivateStatic<int>("OccultShop.UI.BrewPanel", "CalculatePotionBasePrice", 10, highQualityResult);
        var minimumPrice = InvokePrivateStatic<int>("OccultShop.UI.BrewPanel", "CalculatePotionBasePrice", 0, lowQualityResult);

        AssertEqual("High quality base price", 50, highQualityPrice);
        AssertEqual("Minimum base price", 1, minimumPrice);
    }

    private static void TestCustomerPanelBuildPotionIngredientDef()
    {
        var itemDefType = GetTypeFromUiAssembly("OccultShop.Models.ItemDef");
        var ingredientDefType = GetTypeFromUiAssembly("IngredientDef");
        var item = Activator.CreateInstance(itemDefType)
            ?? throw new InvalidOperationException("Failed to create ItemDef instance.");

        var sourceTraits = new Dictionary<string, int> { ["sleep"] = 3 };
        var sourceRisks = new Dictionary<string, int> { ["nausea"] = 2 };
        var sourceTags = new List<string> { "ingredient", "night" };

        SetProperty(item, "Id", "moon_leaf");
        SetProperty(item, "Name", "Moon Leaf");
        SetProperty(item, "Quality", 77);
        SetProperty(item, "Traits", sourceTraits);
        SetProperty(item, "Risks", sourceRisks);
        SetProperty(item, "Tags", sourceTags);

        var result = InvokePrivateStatic("OccultShop.UI.CustomerPanel", "BuildPotionIngredientDef", item)
            ?? throw new InvalidOperationException("BuildPotionIngredientDef returned null.");

        AssertEqual("Returned type", ingredientDefType.FullName ?? "IngredientDef", result.GetType().FullName ?? string.Empty);
        AssertEqual("Ingredient id", "moon_leaf", GetProperty<string>(result, "Id"));
        AssertEqual("Ingredient name", "Moon Leaf", GetProperty<string>(result, "Name"));
        AssertEqual("Ingredient quality", 77, GetProperty<int>(result, "Quality"));

        var traits = GetProperty<Dictionary<string, int>>(result, "Traits");
        var risks = GetProperty<Dictionary<string, int>>(result, "Risks");
        var tags = GetProperty<List<string>>(result, "Tags");

        AssertEqual("Trait value copied", 3, traits["sleep"]);
        AssertEqual("Risk value copied", 2, risks["nausea"]);
        AssertEqual("Tag count copied", 2, tags.Count);
        AssertTrue("Traits dictionary cloned", !ReferenceEquals(sourceTraits, traits));
        AssertTrue("Risks dictionary cloned", !ReferenceEquals(sourceRisks, risks));
        AssertTrue("Tags list cloned", !ReferenceEquals(sourceTags, tags));
    }

    private static void TestRuntimeContentDbSeparatesRuntimeItems()
    {
        var runtimeDbType = GetTypeFromUiAssembly("OccultShop.Autoload.RuntimeContentDb");
        var registerMethod = runtimeDbType.GetMethod("RegisterRuntimePotionItem", BindingFlags.Public | BindingFlags.Instance);
        var clearMethod = runtimeDbType.GetMethod("ClearRuntimeItems", BindingFlags.Public | BindingFlags.Instance);
        var itemsProperty = runtimeDbType.GetProperty("Items", BindingFlags.Public | BindingFlags.Instance);
        var changedEvent = runtimeDbType.GetEvent("Changed", BindingFlags.Public | BindingFlags.Instance);

        AssertTrue("RuntimeContentDb exposes runtime registration", registerMethod is not null);
        AssertTrue("RuntimeContentDb exposes runtime clearing", clearMethod is not null);
        AssertTrue("RuntimeContentDb exposes item registry", itemsProperty is not null);
        AssertTrue("RuntimeContentDb exposes change notification", changedEvent is not null);

        AssertEqual("Runtime registration return type", "OccultShop.Models.ItemDef", registerMethod!.ReturnType.FullName ?? registerMethod.ReturnType.Name);
        AssertTrue("Runtime item registry is IReadOnlyDictionary",
            itemsProperty!.PropertyType.IsGenericType &&
            itemsProperty.PropertyType.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>));

        var registryArgs = itemsProperty.PropertyType.GetGenericArguments();
        AssertEqual("Runtime item registry key type", typeof(string).FullName ?? string.Empty, registryArgs[0].FullName ?? string.Empty);
        AssertEqual("Runtime item registry value type", "OccultShop.Models.ItemDef", registryArgs[1].FullName ?? registryArgs[1].Name);
    }

    private static void TestDataDbDoesNotExposeRuntimeRegistration()
    {
        var dataDbType = GetTypeFromUiAssembly("OccultShop.Autoload.DataDb");
        var method = dataDbType.GetMethod("RegisterRuntimePotionItem", BindingFlags.Public | BindingFlags.Instance);
        AssertTrue("DataDb runtime registration removed", method is null);
    }

    private static void TestDataDbReloadsAuthoredJsonOnly()
    {
        var source = ReadProjectFile("Scripts/Autoload/DataDb.cs");
        AssertTrue("DataDb reload entry point exists", source.Contains("public override void _Ready()"));
        AssertTrue("DataDb reloads on ready", source.Contains("ReloadAll();"));
        AssertTrue("DataDb loads authored items", source.Contains("LoadArray<ItemDef>(\"res://Data/items.json\")"));
        AssertTrue("DataDb loads authored rules", source.Contains("LoadArray<RuleDef>(\"res://Data/rules.json\")"));
        AssertTrue("DataDb loads authored events", source.Contains("LoadArray<EventCardDef>(\"res://Data/events.json\")"));
        AssertTrue("DataDb loads authored customers", source.Contains("LoadArray<CustomerInteractionDef>(\"res://Data/customers.json\")"));
        AssertTrue("DataDb loads authored synergies", source.Contains("LoadArray<SynergyRule>(\"res://Data/synergies.json\")"));
        AssertTrue("DataDb does not register runtime items", !source.Contains("RegisterRuntimePotionItem"));
        AssertTrue("DataDb does not reference runtime catalog", !source.Contains("RuntimeContentDb"));
    }

    private static void TestUiLookupUsesRuntimeFirstCatalog()
    {
        var itemCatalog = ReadProjectFile("Scripts/Autoload/ItemCatalog.cs");
        AssertTrue("ItemCatalog checks runtime first", itemCatalog.Contains("RuntimeContentDb.TryGetItem(itemId, out item)"));
        AssertTrue("ItemCatalog falls back to DataDb", itemCatalog.Contains("DataDb.TryGetItem(itemId, out item)"));

        var brewPanel = ReadProjectFile("Scripts/UI/BrewPanel.cs");
        var inventoryPanel = ReadProjectFile("Scripts/UI/InventoryPanel.cs");
        var recipeBookPanel = ReadProjectFile("Scripts/UI/RecipeBookPanel.cs");
        var customerPanel = ReadProjectFile("Scripts/UI/CustomerPanel.cs");
        var brewService = ReadProjectFile("Scripts/Systems/PotionInventoryBrewService.cs");

        AssertTrue("BrewPanel resolves through ItemCatalog", brewPanel.Contains("ItemCatalog.TryGetItem") && brewPanel.Contains("ItemCatalog.GetItemName"));
        AssertTrue("InventoryPanel resolves through ItemCatalog", inventoryPanel.Contains("ItemCatalog.TryGetItem") && inventoryPanel.Contains("ItemCatalog.GetItemName"));
        AssertTrue("RecipeBookPanel resolves through ItemCatalog", recipeBookPanel.Contains("ItemCatalog.TryGetItem") && recipeBookPanel.Contains("ItemCatalog.GetItemName"));
        AssertTrue("CustomerPanel resolves through ItemCatalog", customerPanel.Contains("ItemCatalog.TryGetItem") && customerPanel.Contains("ItemCatalog.GetItemName"));
        AssertTrue("PotionInventoryBrewService resolves through ItemCatalog", brewService.Contains("ItemCatalog.GetItemName"));
        AssertTrue("BrewPanel still registers runtime potions separately", brewPanel.Contains("RuntimeContentDb.RegisterRuntimePotionItem"));
    }

    private static void TestMainMenuLoadFlow()
    {
        var source = ReadProjectFile("Scripts/UI/MainMenu.cs");
        var scene = ReadProjectFile("MainMenu.tscn");

        AssertTrue("MainMenu has load button path", source.Contains("LoadButtonPath"));
        AssertTrue("MainMenu has new game button path", source.Contains("NewGameButtonPath"));
        AssertTrue("MainMenu continues the latest save", source.Contains("LoadLatestGameIfExists()"));
        AssertTrue("MainMenu falls back to a new game when no save exists", source.Contains("SaveGameManager.StartNewGame();"));
        AssertTrue("MainMenu hides continue until saves exist", source.Contains("Visible = SaveGameManager.HasSavedGames()"));
        AssertTrue("MainMenu opens load browser", source.Contains("Scenes/UI/LoadGameMenu.tscn"));
        AssertTrue("MainMenu scene has load button", scene.Contains("LoadButton"));
        AssertTrue("MainMenu scene has new game button", scene.Contains("NewGameButton"));
        AssertTrue("MainMenu scene labels the new game button", scene.Contains("text = \"New Game\""));
        AssertTrue("MainMenu scene labels the continue button", scene.Contains("text = \"Continue\""));
        AssertTrue("MainMenu scene labels the load button", scene.Contains("text = \"Load Game\""));
    }

    private static void TestLoadGameMenuScene()
    {
        var source = ReadProjectFile("Scripts/UI/LoadGameMenu.cs");
        var scene = ReadProjectFile("Scenes/UI/LoadGameMenu.tscn");

        AssertTrue("LoadGameMenu reads save summaries", source.Contains("GetSavedGames()"));
        AssertTrue("LoadGameMenu loads selected save", source.Contains("LoadGame(save.FilePath)"));
        AssertTrue("LoadGameMenu deletes selected save", source.Contains("DeleteSaveGame(save.FilePath)"));
        AssertTrue("LoadGameMenu returns to main menu", source.Contains("ChangeSceneToFile(\"res://MainMenu.tscn\")"));
        AssertTrue("LoadGameMenu exposes a delete button", source.Contains("Text = \"Delete\""));
        AssertTrue("LoadGameMenu scene exposes a save list", scene.Contains("SaveList"));
        AssertTrue("LoadGameMenu scene exposes empty state", scene.Contains("No saved games found."));
        AssertTrue("LoadGameMenu scene exposes back button", scene.Contains("BackButton"));
    }

    private static void TestSaveGameManagerUsesSaveDirectory()
    {
        var source = ReadProjectFile("Scripts/Autoload/SaveGameManager.cs");

        AssertTrue("SaveGameManager uses a save directory", source.Contains("user://saves"));
        AssertTrue("SaveGameManager can enumerate saved games", source.Contains("GetSavedGames()"));
        AssertTrue("SaveGameManager can load an explicit save", source.Contains("LoadGame(string saveFilePath)"));
        AssertTrue("SaveGameManager can load the latest save", source.Contains("LoadLatestGameIfExists()"));
        AssertTrue("SaveGameManager can delete save files", source.Contains("DeleteSaveGame(string saveFilePath)"));
        AssertTrue("SaveGameManager generates separate save files", source.Contains("BuildUniqueSaveFilePath"));
        AssertTrue("SaveGameManager remembers the active save file", source.Contains("_activeSaveFilePath"));
        AssertTrue("SaveGameManager overwrites the active save file", source.Contains("string.IsNullOrWhiteSpace(_activeSaveFilePath)"));
    }

    private static void TestHudReturnToMainMenuDoesNotAutoSave()
    {
        var source = ReadProjectFile("Scripts/UI/Hud.cs");

        AssertTrue("Hud return-to-menu handler exists", source.Contains("OnReturnToMainMenuPressed"));
        AssertTrue("Hud return-to-menu still changes scenes", source.Contains("ChangeSceneToFile(\"res://MainMenu.tscn\")"));
        AssertTrue("Hud return-to-menu no longer auto-saves", !source.Contains("Could not save before returning to main menu"));
    }

    private static void TestPersistenceBoundaryIsDocumented()
    {
        var persistenceBoundary = ReadProjectFile("PERSISTENCE_BOUNDARY.md");
        AssertTrue("Persistence boundary note exists", persistenceBoundary.Contains("runtime save/load system"));
        AssertTrue("Persistence boundary documents save directory", persistenceBoundary.Contains("user://saves/"));
        AssertTrue("Authored data reload rule documented", persistenceBoundary.Contains("Authored data: always reload from `res://Data/*.json`"));
        AssertTrue("Runtime catalog save rule documented", persistenceBoundary.Contains("Runtime-generated item catalog: persist separately from authored data"));
        AssertTrue("Player state save rule documented", persistenceBoundary.Contains("Player state: save independently"));
    }

    private static Type GetTypeFromUiAssembly(string typeName)
    {
        var assembly = UiAssembly.Value;
        var type = assembly.GetType(typeName)
            ?? assembly.GetTypes().FirstOrDefault(x => string.Equals(x.Name, typeName, StringComparison.Ordinal));

        if (type is null)
            throw new InvalidOperationException($"Type '{typeName}' not found in OccultShop assembly.");

        return type;
    }

    private static string ReadProjectFile(string relativePath)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", relativePath);
        path = Path.GetFullPath(path);

        if (!File.Exists(path))
            throw new InvalidOperationException($"Missing project file: {relativePath}");

        return File.ReadAllText(path);
    }

    private static T InvokePrivateStatic<T>(string typeName, string methodName, params object?[] args)
    {
        var result = InvokePrivateStatic(typeName, methodName, args);
        if (result is T typed)
            return typed;

        throw new InvalidOperationException($"Method {typeName}.{methodName} did not return {typeof(T).Name}.");
    }

    private static object? InvokePrivateStatic(string typeName, string methodName, params object?[] args)
    {
        var type = GetTypeFromUiAssembly(typeName);
        var method = type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"Missing method {typeName}.{methodName}.");

        return method.Invoke(null, args);
    }

    private static void SetProperty(object target, string propertyName, object? value)
    {
        var property = target.GetType().GetProperty(propertyName)
            ?? throw new InvalidOperationException($"Property '{propertyName}' not found on {target.GetType().Name}.");
        property.SetValue(target, value);
    }

    private static T GetProperty<T>(object target, string propertyName)
    {
        var property = target.GetType().GetProperty(propertyName)
            ?? throw new InvalidOperationException($"Property '{propertyName}' not found on {target.GetType().Name}.");
        var value = property.GetValue(target);

        if (value is T typed)
            return typed;

        throw new InvalidOperationException($"Property '{propertyName}' on {target.GetType().Name} is not {typeof(T).Name}.");
    }

    private static Assembly LoadUiAssembly()
    {
        RegisterAssemblyResolver();

        var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var assemblyPath = Path.Combine(projectRoot, ".godot", "mono", "temp", "bin", "Debug", "OccultShop.dll");

        if (!File.Exists(assemblyPath))
            throw new InvalidOperationException($"OccultShop assembly not found at '{assemblyPath}'. Build OccultShop first.");

        return Assembly.LoadFrom(assemblyPath);
    }

    private static void RegisterAssemblyResolver()
    {
        if (_resolverRegistered)
            return;

        AssemblyLoadContext.Default.Resolving += ResolveFromNuGetPackages;
        _resolverRegistered = true;
    }

    private static Assembly? ResolveFromNuGetPackages(AssemblyLoadContext context, AssemblyName assemblyName)
    {
        if (string.IsNullOrWhiteSpace(assemblyName.Name))
            return null;

        var assemblyFileName = $"{assemblyName.Name}.dll";
        foreach (var packageRoot in GetNuGetPackageRoots())
        {
            var packageDirectory = Path.Combine(packageRoot, assemblyName.Name.ToLowerInvariant());
            if (!Directory.Exists(packageDirectory))
                continue;

            foreach (var versionDirectory in Directory.GetDirectories(packageDirectory).OrderByDescending(x => x, StringComparer.Ordinal))
            {
                var candidatePath = Path.Combine(versionDirectory, "lib", "net8.0", assemblyFileName);
                if (File.Exists(candidatePath))
                    return context.LoadFromAssemblyPath(candidatePath);
            }
        }

        return null;
    }

    private static IEnumerable<string> GetNuGetPackageRoots()
    {
        var roots = new List<string>();
        var configuredRoot = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        if (!string.IsNullOrWhiteSpace(configuredRoot))
            roots.Add(configuredRoot);

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
            roots.Add(Path.Combine(userProfile, ".nuget", "packages"));

        return roots
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase);
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
