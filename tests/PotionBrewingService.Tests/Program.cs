using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
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
        Run("Draggable panel whole-panel drag respects child action buttons", TestDraggablePanelWholePanelDragRespectsChildButtons);
        Run("InventoryPanel splits inventory labels predictably", TestInventoryPanelSplitInventoryName);
        Run("InventoryPanel dictionary formatting is stable", TestInventoryPanelFormatDictionary);
        Run("InventoryPanel top-traits formatting is stable", TestInventoryPanelFormatTopTraits);
        Run("InventoryPanel potion filter uses only top traits", TestInventoryPanelPotionFilterUsesOnlyTopTraits);
        Run("InventoryPanel closes detail after successful right-click queue of same ingredient", TestInventoryPanelRightClickQueueClosesMatchingDetail);
        Run("InventoryPanel risk filter is wired", TestInventoryPanelRiskFilterIsWired);
        Run("InventoryPanel clear buttons reserve layout space until filters are active", TestInventoryPanelClearButtonsReserveLayoutSpaceUntilFiltersAreActive);
        Run("InventoryPanel ingredient type filter is populated and fixed", TestInventoryPanelTypeFilterIsPopulatedAndFixed);
        Run("RecipeBookPanel dictionary formatting is stable", TestRecipeBookPanelFormatDictionary);
        Run("RecipeBookPanel top-traits formatting is stable", TestRecipeBookPanelFormatTopTraits);
        Run("RecipeBookPanel entry shows traits and risks to the right of ingredients", TestRecipeBookPanelEntryShowsTraitsAndRisksToTheRightOfIngredients);
        Run("PotionBookPanel appends learned runtime potions to the end", TestPotionBookPanelAppendsLearnedRuntimePotionsToTheEnd);
        Run("BrewPanel ingredient tag detection is case-insensitive", TestBrewPanelIsIngredient);
        Run("BrewPanel previews potion names before brewing", TestBrewPanelPreviewNameIsWired);
        Run("Brew and inventory price wiring stays intact", TestBrewAndInventoryPriceWiring);
        Run("Potion base price survives snapshot round-trips", TestPotionBasePriceSnapshotRoundTrip);
        Run("ItemDef price converter accepts price fields", TestItemDefPriceConverterSupportsPriceFields);
        Run("CustomerPanel creates detached ingredient snapshots", TestCustomerPanelBuildPotionIngredientDef);
        Run("Customer events randomize shop-day order", TestCustomerEventControllerRandomizesOrder);
        Run("Customer events respect scheduling and story outcomes", TestCustomerEventSchedulingAndStoryOutcomes);
        Run("Story customer dialogue options replace skip actions", TestStoryCustomerDialogueOptionsReplaceSkipActions);
        Run("Customer drop box stays disabled until next customer", TestCustomerDropBoxDisablesAfterSale);
        Run("RuntimeContentDb stores generated items separately", TestRuntimeContentDbSeparatesRuntimeItems);
        Run("DataDb does not expose runtime registration", TestDataDbDoesNotExposeRuntimeRegistration);
        Run("DataDb reloads authored resource catalogs only", TestDataDbReloadsAuthoredResourceCatalogsOnly);
        Run("UI lookup uses the runtime-first item catalog", TestUiLookupUsesRuntimeFirstCatalog);
        Run("Main menu exposes start and load flows", TestMainMenuLoadFlow);
        Run("Load menu scene is wired for saved game browsing", TestLoadGameMenuScene);
        Run("Game UI keeps the potion trait filter wired", TestGameUiKeepsPotionTraitFilterWired);
        Run("Recipe book filters are wired", TestRecipeBookFiltersAreWired);
        Run("Recipe book clear button is wired", TestRecipeBookClearButtonIsWired);
        Run("SaveGameManager stores saves in a dedicated directory", TestSaveGameManagerUsesSaveDirectory);
        Run("GameState seeds only the starter potion ingredients", TestStartingInventorySeedsOnlyTutorialRecipeItems);
        Run("Garden crop definitions cover authored ingredients", TestGardenCropDefinitionsCoverAuthoredIngredients);
        Run("Garden state persists seeds and pots", TestGardenStatePersistenceWiring);
        Run("Garden scene and HUD navigation are wired", TestGardenSceneAndHudNavigation);
        Run("Tutorial game state transitions stay stable", TestTutorialGameStateTransitions);
        Run("Tutorial snapshot round-trip stays stable", TestTutorialSnapshotRoundTrip);
        Run("Main scene wires tutorial controller", TestMainSceneWiresTutorialController);
        Run("Tutorial overlay scene wiring stays intact", TestTutorialOverlaySceneWiring);
        Run("Tutorial architecture extraction stays intact", TestTutorialArchitectureExtraction);
        Run("Tutorial next-customer inventory seed stays curated", TestTutorialNextCustomerInventorySeedIsCurated);
        Run("Tutorial sale review feedback uses request wording", TestTutorialSaleReviewFeedbackUsesRequestWording);
        Run("Tutorial overlay keeps one dimming strategy", TestTutorialOverlayUsesDynamicCutoutsOnly);
        Run("Scenario debugger can set the shop stop timer", TestScenarioDebuggerStopTimerControls);
        Run("Hud return-to-menu does not auto-save", TestHudReturnToMainMenuDoesNotAutoSave);
        Run("Hud settings panel closes on outside click", TestHudSettingsPanelClosesOnOutsideClick);
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
            ["OccultShop.UI.Garden"] = "Control",
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
        var method = type.GetMethod("SplitInventoryName", BindingFlags.NonPublic | BindingFlags.Static);
        AssertTrue("SplitInventoryName method exists", method is not null);
        if (method is null)
            return;

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

    private static void TestDraggablePanelWholePanelDragRespectsChildButtons()
    {
        var draggablePanel = ReadProjectFile("Scripts/UI/DraggablePanel.cs");
        var inventoryPanel = ReadProjectFile("Scripts/UI/InventoryPanel.cs");

        AssertTrue("DraggablePanel inspects hovered GUI control before drag",
            draggablePanel.Contains("GuiGetHoveredControl()"));
        AssertTrue("DraggablePanel prevents whole-panel drag when a child button is hovered",
            draggablePanel.Contains("hoveredControl is BaseButton"));
        AssertTrue("DraggablePanel only applies button guard to its own children",
            draggablePanel.Contains("IsAncestorOf(hoveredControl)"));
        AssertTrue("InventoryPanel close button remains wired to hide detail",
            inventoryPanel.Contains("_itemDetailCloseButton.Pressed += HideItemDetail;"));
        AssertTrue("InventoryPanel add-to-brew button remains wired",
            inventoryPanel.Contains("_itemDetailBrewButton.Pressed += TryUseSelectedItem;"));
    }

    private static void TestInventoryPanelFormatDictionary()
    {
        var values = new Dictionary<string, int>
        {
            ["zeta"] = 2,
            ["beta"] = 4,
            ["alpha"] = 4
        };

        var formatted = InvokePrivateStatic<string>("OccultShop.UI.InventoryPanel", "FormatTopStats", values, 3, "None");
        AssertEqual("Inventory dictionary order", "Alpha +4\nBeta +4\nZeta +2", formatted);

        var empty = InvokePrivateStatic<string>("OccultShop.UI.InventoryPanel", "FormatTopStats", new Dictionary<string, int>(), 3, "None");
        AssertEqual("Inventory dictionary empty", "None\n\n", empty);

        var nullValue = InvokePrivateStatic<string>("OccultShop.UI.InventoryPanel", "FormatTopStats", (object?)null, 3, "None");
        AssertEqual("Inventory dictionary null", "None\n\n", nullValue);
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

        var formatted = InvokePrivateStatic<string>("OccultShop.UI.InventoryPanel", "FormatTopStats", values, 2, "None");
        AssertEqual("Inventory top traits order", "Focus +5\nSleep +5", formatted);

        var empty = InvokePrivateStatic<string>("OccultShop.UI.InventoryPanel", "FormatTopStats", new Dictionary<string, int>(), 3, "None");
        AssertEqual("Inventory top traits empty", "None\n\n", empty);
    }

    private static void TestInventoryPanelPotionFilterUsesOnlyTopTraits()
    {
        var inventoryPanel = ReadProjectFile("Scripts/UI/InventoryPanel.cs");

        AssertTrue("InventoryPanel builds potion trait names from the top three traits only",
            inventoryPanel.Contains("ItemFilterUtilities.BuildTopTraitNames(potionStacks.Select(x => x.Key), 3, _itemCatalog)"));
        AssertTrue("InventoryPanel keeps ingredient trait names unchanged",
            inventoryPanel.Contains("ItemFilterUtilities.BuildTraitNames(ingredientStacks.Select(x => x.Key), _itemCatalog)"));
        AssertTrue("InventoryPanel top-trait helper limits the selected traits",
            ReadProjectFile("Scripts/UI/ItemFilterUtilities.cs").Contains(".Take(maxCount)"));
    }

    private static void TestInventoryPanelRiskFilterIsWired()
    {
        var inventoryPanel = ReadProjectFile("Scripts/UI/InventoryPanel.cs");
        var scene = ReadProjectFile("Scenes/UI/InventoryPanel.tscn");

        AssertTrue("InventoryPanel exports a potion risk filter path",
            inventoryPanel.Contains("PotionsRiskFilterPath"));
        AssertTrue("InventoryPanel exports an ingredient type filter path",
            inventoryPanel.Contains("IngredientsTypeFilterPath"));
        AssertTrue("InventoryPanel exports an ingredient risk filter path",
            inventoryPanel.Contains("IngredientsRiskFilterPath"));
        AssertTrue("InventoryPanel keeps fixed ingredient type options",
            inventoryPanel.Contains("IngredientTypeFilterOptions"));
        AssertTrue("InventoryPanel filters ingredients by selected type",
            inventoryPanel.Contains("ItemHasIngredientType(stack.Key, _activeIngredientTypeFilter)"));
        AssertTrue("InventoryPanel builds risk names",
            inventoryPanel.Contains("ItemFilterUtilities.BuildRiskNames(potionStacks.Select(x => x.Key), _itemCatalog)"));
        AssertTrue("InventoryPanel checks potion risks",
            inventoryPanel.Contains("ItemFilterUtilities.ItemHasRisk(stack.Key, _activePotionRiskFilter, _itemCatalog)"));
        AssertTrue("InventoryPanel checks ingredient risks",
            inventoryPanel.Contains("ItemFilterUtilities.ItemHasRisk(stack.Key, _activeIngredientRiskFilter, _itemCatalog)"));
        AssertTrue("InventoryPanel defines potion risk filter in the scene",
            scene.Contains("PotionsRiskFilterPath = NodePath(\"Panel/Margin/VBox/PotionsHeaderRow/RiskFilter\")"));
        AssertTrue("InventoryPanel type filter wiring is provided by scene path or fallback lookup",
            scene.Contains("IngredientsTypeFilterPath = NodePath(\"Panel/Margin/VBox/IngredientsHeaderRow/TypeFilter\")")
            || inventoryPanel.Contains("IngredientsTypeFilterPath.IsEmpty"));
        AssertTrue("InventoryPanel defines ingredient risk filter in the scene",
            scene.Contains("IngredientsRiskFilterPath = NodePath(\"Panel/Margin/VBox/IngredientsHeaderRow/RiskFilter\")"));
        AssertTrue("InventoryPanel scene places potion risk filter to the right of trait filter",
            scene.Contains("[node name=\"RiskFilter\" type=\"OptionButton\" parent=\"Panel/Margin/VBox/PotionsHeaderRow\"]"));
        AssertTrue("InventoryPanel scene places ingredient risk filter to the right of trait filter",
            scene.Contains("[node name=\"RiskFilter\" type=\"OptionButton\" parent=\"Panel/Margin/VBox/IngredientsHeaderRow\"]"));
        AssertTrue("InventoryPanel scene includes ingredient type filter",
            scene.Contains("[node name=\"TypeFilter\" type=\"OptionButton\" parent=\"Panel/Margin/VBox/IngredientsHeaderRow\"]"));
    }

    private static void TestInventoryPanelClearButtonsReserveLayoutSpaceUntilFiltersAreActive()
    {
        var source = ReadProjectFile("Scripts/UI/InventoryPanel.cs");
        var scene = ReadProjectFile("Scenes/UI/InventoryPanel.tscn");
        var potionClearButtonReservesSpace =
            scene.Contains($"[node name=\"Clear\" type=\"Button\" parent=\"Panel/Margin/VBox/PotionsHeaderRow\"]{Environment.NewLine}visible = false{Environment.NewLine}custom_minimum_size = Vector2(64, 0)") ||
            scene.Contains("[node name=\"Clear\" type=\"Button\" parent=\"Panel/Margin/VBox/PotionsHeaderRow\"]\nvisible = false\ncustom_minimum_size = Vector2(64, 0)");
        var ingredientClearButtonReservesSpace =
            scene.Contains($"[node name=\"Clear\" type=\"Button\" parent=\"Panel/Margin/VBox/IngredientsHeaderRow\"]{Environment.NewLine}visible = false{Environment.NewLine}custom_minimum_size = Vector2(64, 0)") ||
            scene.Contains("[node name=\"Clear\" type=\"Button\" parent=\"Panel/Margin/VBox/IngredientsHeaderRow\"]\nvisible = false\ncustom_minimum_size = Vector2(64, 0)");

        AssertTrue("InventoryPanel keeps potion clear button layout stable from filter state",
            source.Contains("UpdateClearFilterButtonVisibility();") &&
            source.Contains("ApplyClearFilterButtonState(_potionsClearFilterButton, hasActivePotionFilter)") &&
            source.Contains("_activePotionTraitFilter") &&
            source.Contains("_activePotionRiskFilter"));
        AssertTrue("InventoryPanel keeps ingredient clear button layout stable from filter state",
            source.Contains("ApplyClearFilterButtonState(_ingredientsClearFilterButton, hasActiveIngredientFilter)") &&
            source.Contains("_activeIngredientTypeFilter") &&
            source.Contains("_activeIngredientTraitFilter") &&
            source.Contains("_activeIngredientRiskFilter"));
        AssertTrue("InventoryPanel reserves width for the potion clear button", potionClearButtonReservesSpace);
        AssertTrue("InventoryPanel reserves width for the ingredient clear button", ingredientClearButtonReservesSpace);
        AssertTrue("InventoryPanel inactive clear buttons stay in layout but non-interactive",
            source.Contains("button.Visible = true") &&
            source.Contains("button.Disabled = !isActive") &&
            source.Contains("button.MouseFilter = isActive ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore") &&
            source.Contains("button.Modulate = isActive ? Colors.White : new Color(1f, 1f, 1f, 0f)"));
    }

    private static void TestInventoryPanelRightClickQueueClosesMatchingDetail()
    {
        var source = ReadProjectFile("Scripts/UI/InventoryPanel.cs");
        var brewPanel = ReadProjectFile("Scripts/UI/BrewPanel.cs");

        AssertTrue("InventoryPanel records quantity before right-click queue attempt",
            source.Contains("var quantityBeforeQueue = _gameState.Inventory.GetValueOrDefault(itemId);"));
        AssertTrue("InventoryPanel records quantity after right-click queue attempt",
            source.Contains("var quantityAfterQueue = _gameState.Inventory.GetValueOrDefault(itemId);"));
        AssertTrue("InventoryPanel only treats queue as success when inventory decreases",
            source.Contains("var queuedSuccessfully = quantityAfterQueue < quantityBeforeQueue;"));
        AssertTrue("InventoryPanel opens the brew panel before queueing a right-click ingredient",
            source.Contains("_brewPanel.ShowPanel();"));
        AssertTrue("BrewPanel exposes an explicit show method for ingredient adds",
            brewPanel.Contains("public void ShowPanel()"));
        AssertTrue("InventoryPanel only closes detail when same item is currently selected",
            source.Contains("string.Equals(_currentItemId, itemId, System.StringComparison.OrdinalIgnoreCase)"));
        AssertTrue("InventoryPanel hides detail after successful matching queue",
            source.Contains("HideItemDetail();"));
    }

    private static void TestInventoryPanelTypeFilterIsPopulatedAndFixed()
    {
        var source = ReadProjectFile("Scripts/UI/InventoryPanel.cs");

        AssertTrue("InventoryPanel keeps a fixed type options list", source.Contains("IngredientTypeFilterOptions"));
        AssertTrue("InventoryPanel includes Herb option", source.Contains("\"Herb\""));
        AssertTrue("InventoryPanel includes Liquid option", source.Contains("\"Liquid\""));
        AssertTrue("InventoryPanel includes Catalyst option", source.Contains("\"Catalyst\""));
        AssertTrue("InventoryPanel refreshes ingredient type options explicitly",
            source.Contains("RefreshIngredientTypeFilterOptions();"));
        AssertTrue("InventoryPanel uses TypeFilter fallback lookup when exported path is empty",
            source.Contains("IngredientsTypeFilterPath.IsEmpty"));
        AssertTrue("InventoryPanel fallback path targets the ingredients type filter node",
            source.Contains("Panel/Margin/VBox/IngredientsHeaderRow/TypeFilter"));
        AssertTrue("InventoryPanel applies the selected type filter to ingredient stacks",
            source.Contains("ItemHasIngredientType(stack.Key, _activeIngredientTypeFilter)"));
    }

    private static void TestRecipeBookPanelFormatDictionary()
    {
        var normalized = InvokePrivateStatic<string>("OccultShop.UI.RecipeBookPanel", "ToDisplayStatName", "alpha_beta");
        AssertEqual("Recipe stat formatter keeps stable title casing", "Alpha_Beta", normalized);

        var empty = InvokePrivateStatic<string>("OccultShop.UI.RecipeBookPanel", "ToDisplayStatName", "");
        AssertEqual("Recipe stat formatter handles empty names", "Unknown", empty);
    }

    private static void TestRecipeBookPanelFormatTopTraits()
    {
        var uppercase = InvokePrivateStatic<string>("OccultShop.UI.RecipeBookPanel", "ToDisplayStatName", "SLEEP");
        AssertEqual("Recipe stat formatter lowers then title-cases uppercase names", "Sleep", uppercase);

        var spaced = InvokePrivateStatic<string>("OccultShop.UI.RecipeBookPanel", "ToDisplayStatName", "moon dust");
        AssertEqual("Recipe stat formatter preserves multi-word title casing", "Moon Dust", spaced);
    }

    private static void TestRecipeBookPanelEntryShowsTraitsAndRisksToTheRightOfIngredients()
    {
        var source = ReadProjectFile("Scripts/UI/RecipeBookPanel.cs");

        AssertTrue("RecipeBookPanel builds a top header row with icon, title, and brew action",
            source.Contains("var topRow = new HBoxContainer"));
        AssertTrue("RecipeBookPanel builds a details row beneath the header row",
            source.Contains("var detailsRow = new HBoxContainer"));
        AssertTrue("RecipeBookPanel keeps ingredient rendering in a dedicated helper",
            source.Contains("CreateIngredientLines(availabilityEntries)"));
        AssertTrue("RecipeBookPanel keeps trait rendering in a dedicated helper",
            source.Contains("BuildStatLines(item.Traits"));
        AssertTrue("RecipeBookPanel keeps risk rendering in a dedicated helper",
            source.Contains("BuildStatLines(item.Risks"));
        AssertTrue("RecipeBookPanel uses explicit column builder helpers",
            source.Contains("CreateDetailsColumn("));
        AssertTrue("RecipeBookPanel inserts separators between ingredients, traits, and risks",
            source.Contains("CreateVerticalSeparator()"));
        AssertTrue("RecipeBookPanel keeps the ingredients column wider",
            source.Contains("3.0f"));
        AssertTrue("RecipeBookPanel keeps stat columns narrower than ingredients",
            source.Contains("1.5f"));
        AssertTrue("RecipeBookPanel exposes brewability status as a dedicated tag",
            source.Contains("CreateStatusTag(isBrewable, missingCount)"));
        AssertTrue("RecipeBookPanel disables brew when ingredients are missing",
            source.Contains("Disabled = !isBrewable"));
        AssertTrue("RecipeBookPanel uses clear ingredient availability markers",
            source.Contains("var prefix = entry.IsAvailable ? \"v\" : \"X\""));
        AssertTrue("RecipeBookPanel keeps the yellow missing status label",
            source.Contains("Missing {missingCount}"));
    }

    private static void TestPotionBookPanelAppendsLearnedRuntimePotionsToTheEnd()
    {
        var source = ReadProjectFile("Scripts/UI/PotionBookPanel.cs");
        var gameStateSource = ReadProjectFile("Scripts/Autoload/GameState.cs");
        var saveDataSource = ReadProjectFile("Scripts/Persistence/SaveData.cs");

        AssertTrue("PotionBookPanel resolves GameState through an exported path",
            source.Contains("GameStatePath = new(\"/root/GameState\")"));
        AssertTrue("PotionBookPanel subscribes to GameState changes",
            source.Contains("_gameState.Changed += OnGameStateChanged"));
        AssertTrue("PotionBookPanel reads learned potion order from GameState",
            source.Contains("foreach (var potionId in _gameState.KnownPotionOrder)"));
        AssertTrue("PotionBookPanel skips authored potion item ids when appending learned entries",
            source.Contains("if (authoredPotionIds.Contains(potionId))"));
        AssertTrue("PotionBookPanel registers both recipe ids and potion item ids as authored",
            source.Contains("authoredPotionIds.Add(BuildPredefinedPotionItemId(recipe.Id));"));
        AssertTrue("PotionBookPanel exports a brew button path",
            source.Contains("BrewButtonPath"));
        AssertTrue("PotionBookPanel wires the brew button press",
            source.Contains("_brewButton.Pressed += TryBrewCurrentPagePotion"));
        AssertTrue("PotionBookPanel only enables brewing for known potion item ids",
            source.Contains("_gameState.KnowsPotion(candidatePotionItemId)"));
        AssertTrue("PotionBookPanel uses the shared inventory brew service",
            source.Contains("PotionInventoryBrewService"));
        AssertTrue("PotionBookPanel scene defines the brew button path",
            ReadProjectFile("Scenes/UI/PotionBookPanel.tscn").Contains("BrewButtonPath = NodePath(\"BookRow/BookPanel/Margin/VBox/RecipeContent/Brew\")"));
        AssertTrue("PotionBookPanel inspects hovered GUI controls before dragging",
            source.Contains("GuiGetHoveredControl()"));
        AssertTrue("PotionBookPanel blocks whole-panel drag when a child button is hovered",
            source.Contains("hoveredControl is BaseButton"));
        AssertTrue("PotionBookPanel converts centered anchors to absolute positioning for dragging",
            source.Contains("Convert from centered anchors to absolute positioning so the book can be dragged freely."));
        AssertTrue("PotionBookPanel updates its position from mouse motion while dragging",
            source.Contains("Position = mouseMotion.GlobalPosition - _dragOffset;"));
        AssertTrue("GameState tracks learned potion order",
            gameStateSource.Contains("public List<string> KnownPotionOrder { get; } = new();"));
        AssertTrue("GameState appends newly learned potions to the order list",
            gameStateSource.Contains("KnownPotionOrder.Add(potionId)"));
        AssertTrue("Save data persists learned potion order",
            saveDataSource.Contains("KnownPotionOrder"));
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

        AssertTrue("BrewPanel still prevents duplicate queue entries",
            source.Contains("Each ingredient can only be used once per potion."));
        AssertTrue("BrewPanel blocks duplicate ingredient types with a specific message",
            source.Contains("Cannot add duplicate type: {newIngredientType} (need one herb, one liquid, one catalyst)"));
        AssertTrue("BrewPanel requires one of each ingredient type before brewing",
            source.Contains("Brewing requires one herb, one liquid, and one catalyst."));
        AssertTrue("BrewPanel resolves item types from tags",
            source.Contains("TryGetIngredientType(ItemDef item, out string ingredientType)"));
        AssertTrue("BrewPanel queue remains list-based without stack counting",
            source.Contains("private readonly List<string> _queuedIngredients = new();"));
        AssertTrue("Inventory drag/drop still routes through TryQueueIngredient",
            ReadProjectFile("Scripts/UI/InventoryPanel.cs").Contains("_brewPanel.TryQueueIngredient(itemId);"));
        AssertTrue("Brew drop box still emits dragged item ids",
            ReadProjectFile("Scripts/UI/BrewDropBox.cs").Contains("EmitSignal(SignalName.ItemDropped, data.AsString());"));
    }

    private static void TestBrewPanelPreviewNameIsWired()
    {
        var source = ReadProjectFile("Scripts/UI/BrewPanel.cs");
        var scene = ReadProjectFile("Scenes/UI/GameUi.tscn");

        AssertTrue("BrewPanel exports a preview name label path",
            source.Contains("PotionNamePreviewLabelPath"));
        AssertTrue("BrewPanel caches the current preview combination",
            source.Contains("_previewPotionCombinationKey"));
        AssertTrue("BrewPanel caches the current preview name",
            source.Contains("_previewPotionName"));
        AssertTrue("BrewPanel resolves the preview name before brewing",
            source.Contains("var potionDisplayName = GetPreviewPotionName(combinationKey);"));
        AssertTrue("BrewPanel regenerates preview names from the combination key",
            source.Contains("GetPreviewPotionName(string combinationKey)"));
        AssertTrue("BrewPanel scene wires the live preview name label",
            scene.Contains("PotionNamePreviewLabelPath = NodePath(\"Panel/Margin/VBox/BrewRow/Preview/Identity/TopRow/TextColumn/NameFrame/NameMargin/Name\")"));
        AssertTrue("BrewPanel scene labels the brew button like the mockup",
            scene.Contains("text = \"Brew Potion\""));
        AssertTrue("BrewPanel scene labels the clear button like the mockup",
            scene.Contains("text = \"Clear Ingredients\""));
    }

    private static void TestBrewAndInventoryPriceWiring()
    {
        var brewPanel = ReadProjectFile("Scripts/UI/BrewPanel.cs");
        AssertTrue("BrewPanel calculates potion price from ingredient totals",
            brewPanel.Contains("CalculateIngredientTotalPrice(_queuedIngredients)"));
        AssertTrue("BrewPanel renders the mockup price label",
            brewPanel.Contains("Estimated Sell Price: \\u00A3"));
        AssertTrue("BrewPanel stores the potion base price in state",
            brewPanel.Contains("RegisterPotionBasePrice(potionItemId, potionBasePrice)"));
        AssertTrue("BrewPanel sums ingredient BasePrice values",
            brewPanel.Contains("totalPrice += Math.Max(0, item.BasePrice);"));

        var inventoryPanel = ReadProjectFile("Scripts/UI/InventoryPanel.cs");
        AssertTrue("InventoryPanel resolves stored potion prices",
            inventoryPanel.Contains("TryGetPotionBasePrice(itemId, out _)"));
        AssertTrue("InventoryPanel shows potion price in the detail panel",
            inventoryPanel.Contains("GetItemPrice(_currentItemId, item)"));
        AssertTrue("InventoryPanel shows item prices on the slot icon",
            inventoryPanel.Contains("GetItemPrice(itemId, item)"));
    }

    private static void TestPotionBasePriceSnapshotRoundTrip()
    {
        var gameStateSource = ReadProjectFile("Scripts/Autoload/GameState.cs");
        var saveDataSource = ReadProjectFile("Scripts/Persistence/SaveData.cs");

        AssertTrue("GameState tracks potion base prices in a dedicated map",
            gameStateSource.Contains("_potionBasePrices"));
        AssertTrue("GameState registers potion base prices once per potion",
            gameStateSource.Contains("if (_potionBasePrices.ContainsKey(potionId))"));
        AssertTrue("GameState snapshot exports potion base prices",
            gameStateSource.Contains("PotionBasePrices = new Dictionary<string, int>(_potionBasePrices, StringComparer.OrdinalIgnoreCase)"));
        AssertTrue("GameState snapshot restores potion base prices",
            gameStateSource.Contains("if (snapshot.PotionBasePrices is not null)"));
        AssertTrue("GameState exposes a lookup for potion base prices",
            gameStateSource.Contains("TryGetPotionBasePrice(string potionId, out int basePrice)"));
        AssertTrue("Save data persists potion base prices",
            saveDataSource.Contains("PotionBasePrices"));
    }

    private static void TestItemDefPriceConverterSupportsPriceFields()
    {
        var itemDefType = GetTypeFromUiAssembly("OccultShop.Models.ItemDef");

        var authoredJson = "{\"id\":\"brew_moon_draught\",\"name\":\"Moon Draught\",\"price\":42,\"quality\":88}";
        var authoredItem = JsonSerializer.Deserialize(authoredJson, itemDefType)
            ?? throw new InvalidOperationException("Could not deserialize authored ItemDef JSON.");
        AssertEqual("Authored price populates BasePrice", 42, GetProperty<int>(authoredItem, "BasePrice"));

        var serialized = JsonSerializer.Serialize(authoredItem, itemDefType);
        AssertTrue("Serialized item uses the price field", serialized.Contains("\"price\":42"));
        AssertTrue("Serialized item does not write BasePrice", !serialized.Contains("BasePrice"));

        var legacyJson = "{\"id\":\"brew_legacy\",\"name\":\"Legacy Brew\",\"BasePrice\":19}";
        var legacyItem = JsonSerializer.Deserialize(legacyJson, itemDefType)
            ?? throw new InvalidOperationException("Could not deserialize legacy ItemDef JSON.");
        AssertEqual("Legacy BasePrice still loads", 19, GetProperty<int>(legacyItem, "BasePrice"));
    }

    private static void TestCustomerPanelBuildPotionIngredientDef()
    {
        var itemDefType = GetTypeFromUiAssembly("OccultShop.Models.ItemDef");
        var ingredientDefType = GetTypeFromUiAssembly("OccultShop.Models.IngredientDef");
        var ingredientFactoryType = GetTypeFromUiAssembly("OccultShop.Systems.IngredientDefFactory");
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

        var method = ingredientFactoryType.GetMethod("FromItemDef", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("IngredientDefFactory.FromItemDef was not found.");
        var result = method.Invoke(null, new[] { item })
            ?? throw new InvalidOperationException("IngredientDefFactory.FromItemDef returned null.");

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

    private static void TestCustomerEventControllerRandomizesOrder()
    {
        var source = ReadProjectFile("Scripts/Controllers/CustomerEventController.cs");
        var dayController = ReadProjectFile("Scripts/Controllers/DayController.cs");
        var customerPanel = ReadProjectFile("Scripts/UI/CustomerPanel.cs");

        AssertTrue("CustomerEventController no longer uses a fixed index walk", !source.Contains("_nextCustomerIndex"));
        AssertTrue("CustomerEventController keeps a randomized order buffer", source.Contains("_customerOrder"));
        AssertTrue("CustomerEventController randomizes the customer order", source.Contains("_random.Next("));
        AssertTrue("CustomerEventController resets the order at the start of a shop day", source.Contains("BeginShopDay()"));
        AssertTrue("DayController resets customer order when the shop opens", dayController.Contains("_customerEventController.BeginShopDay();"));
        AssertTrue("DayController tracks when the shop should close after the current customer", dayController.Contains("_shopClosingPending"));
        AssertTrue("DayController keeps the shop open while the current customer is active at zero seconds", dayController.Contains("_customerPanel.HasActiveInteraction || _awaitingSaleResultClose"));
        AssertTrue("CustomerPanel exposes active interaction state", customerPanel.Contains("HasActiveInteraction => _interaction is not null"));
        AssertTrue("CustomerPanel can switch the next button to Close Shop", customerPanel.Contains("SetCloseShopMode(bool closeShopMode)"));
        AssertTrue("CustomerPanel exposes close shop mode state", customerPanel.Contains("IsCloseShopMode => _closeShopMode"));
    }

    private static void TestCustomerEventSchedulingAndStoryOutcomes()
    {
        var customerController = ReadProjectFile("Scripts/Controllers/CustomerEventController.cs");
        var dataDb = ReadProjectFile("Scripts/Autoload/DataDb.cs");
        var gameState = ReadProjectFile("Scripts/Autoload/GameState.cs");
        var saveData = ReadProjectFile("Scripts/Persistence/SaveData.cs");
        var requirements = ReadProjectFile("Scripts/Systems/Requirements.cs");
        var effects = ReadProjectFile("Scripts/Systems/EffectApplier.cs");
        var customerPanel = ReadProjectFile("Scripts/UI/CustomerPanel.cs");
        var storyVisit = ReadProjectFile("Scripts/Models/StoryCustomerVisitRecord.cs");
        var customers = ReadProjectFile("Data/customers_data.tres");

        AssertTrue("Customer draws filter by requirements", customerController.Contains("Requirements.Met(state, interaction.Requires)"));
        AssertTrue("Customer draws use weighted selection", customerController.Contains("PickWeightedIndex"));
        AssertTrue("DataDb parses customer difficulty", dataDb.Contains("Difficulty = Math.Max(1, ReadInt(entry, \"difficulty\", 1))"));
        AssertTrue("DataDb parses customer outcome effects", dataDb.Contains("OnSuccessEffects = ParseEffects(ReadArray(entry, \"onSuccessEffects\"))"));
        AssertTrue("DataDb parses day requirements", dataDb.Contains("DayMin = ReadNullableInt(entry, \"dayMin\")"));
        AssertTrue("DataDb parses story flag requirements", dataDb.Contains("HasStoryFlag = ReadNullableString(entry, \"hasStoryFlag\")"));
        AssertTrue("GameState stores story flags", gameState.Contains("HashSet<string> StoryFlags"));
        AssertTrue("GameState stores story customer visit records", gameState.Contains("StoryCustomerVisits"));
        AssertTrue("GameState records story customer arrivals", gameState.Contains("RecordStoryCustomerArrived"));
        AssertTrue("GameState records story customer outcomes", gameState.Contains("RecordStoryCustomerInteractionOutcome"));
        AssertTrue("Customer draws exclude story visits that already arrived", customerController.Contains("HasStoryCustomerVisitArrived(interaction)"));
        AssertTrue("Customer draws mark story customer arrivals", customerController.Contains("RecordStoryCustomerArrived(interaction)"));
        AssertTrue("Story customer visits persist in save snapshots", saveData.Contains("List<StoryCustomerVisitRecord> StoryCustomerVisits"));
        AssertTrue("Story customer visit records track arrival and outcome", storyVisit.Contains("HasArrived") && storyVisit.Contains("LastOutcome"));
        AssertTrue("Requirements check story flags", requirements.Contains("state.HasStoryFlag(req.HasStoryFlag!)"));
        AssertTrue("Effects can add story flags", effects.Contains("state.AddStoryFlag(e.AddStoryFlag!)"));
        AssertTrue("CustomerPanel applies success and failure effects", customerPanel.Contains("ApplyOutcomeEffects(isSuccess ? _interaction?.OnSuccessEffects : _interaction?.OnFailureEffects)"));
        AssertTrue("CustomerPanel applies skip effects", customerPanel.Contains("ApplyOutcomeEffects(_interaction.OnSkipEffects)"));
        AssertTrue("CustomerPanel records story sale outcomes", customerPanel.Contains("StoryCustomerOutcomeSuccess") && customerPanel.Contains("StoryCustomerOutcomeFailure"));
        AssertTrue("CustomerPanel records story skip outcomes", customerPanel.Contains("StoryCustomerOutcomeSkipped"));
        AssertTrue("Authored customer data includes early pool", customers.Contains("\"pool\": \"early\""));
        AssertTrue("Authored customer data gates early customers by day", customers.Contains("\"dayMax\": 4"));
        AssertTrue("Authored customer data includes recipe pool", customers.Contains("\"pool\": \"recipe\""));
    }

    private static void TestStoryCustomerDialogueOptionsReplaceSkipActions()
    {
        var customerDef = ReadProjectFile("Scripts/Models/CustomerInteractionDef.cs");
        var dataDb = ReadProjectFile("Scripts/Autoload/DataDb.cs");
        var customerPanel = ReadProjectFile("Scripts/UI/CustomerPanel.cs");
        var dayController = ReadProjectFile("Scripts/Controllers/DayController.cs");
        var customers = ReadProjectFile("Data/customers_data.tres");

        AssertTrue("Customer interaction stores a dialogue start node",
            customerDef.Contains("DialogueStartNodeId"));
        AssertTrue("Customer interaction stores dialogue nodes",
            customerDef.Contains("List<CustomerDialogueNodeDef> DialogueNodes"));
        AssertTrue("Customer interaction exposes dialogue tree presence",
            customerDef.Contains("HasDialogueTree"));
        AssertTrue("Dialogue nodes store player options",
            customerDef.Contains("public sealed class CustomerDialogueNodeDef") &&
            customerDef.Contains("List<CustomerDialogueOptionDef> Options"));
        AssertTrue("Dialogue options can advance or end the tree",
            customerDef.Contains("NextNodeId") &&
            customerDef.Contains("EndsInteraction"));
        AssertTrue("Dialogue options can apply authored effects",
            customerDef.Contains("List<EffectDef> Effects"));

        AssertTrue("DataDb parses customer dialogue start node",
            dataDb.Contains("DialogueStartNodeId = ReadString(entry, \"dialogueStartNodeId\")"));
        AssertTrue("DataDb parses customer dialogue nodes",
            dataDb.Contains("ParseCustomerDialogueNodes(ReadArray(entry, \"dialogueNodes\"))"));
        AssertTrue("DataDb parses customer dialogue options",
            dataDb.Contains("ParseCustomerDialogueOptions(ReadArray(entry, \"options\"))"));
        AssertTrue("DataDb parses dialogue option effects",
            dataDb.Contains("Effects = ParseEffects(ReadArray(entry, \"effects\"))"));

        AssertTrue("CustomerPanel starts story dialogue instead of sale pending state",
            customerPanel.Contains("TryShowDialogueStart()"));
        AssertTrue("CustomerPanel routes first action button through dialogue option zero",
            customerPanel.Contains("OnFirstCustomerActionPressed") &&
            customerPanel.Contains("TrySelectDialogueOption(0)"));
        AssertTrue("CustomerPanel routes second action button through dialogue option one",
            customerPanel.Contains("OnSecondCustomerActionPressed") &&
            customerPanel.Contains("TrySelectDialogueOption(1)"));
        AssertTrue("CustomerPanel replaces skip button text with dialogue option labels",
            customerPanel.Contains("SetDialogueOptionButton") &&
            customerPanel.Contains("button.Text = node.Options[optionIndex].Label"));
        AssertTrue("CustomerPanel disables potion drops during dialogue",
            customerPanel.Contains("SetDialogueOptionState") &&
            customerPanel.Contains("_sellDropBox.SetAcceptDrops(false);"));
        AssertTrue("CustomerPanel restores normal skip button labels for regular customers",
            customerPanel.Contains("_comeBackTomorrowButton.Text = \"Come back tomorrow\"") &&
            customerPanel.Contains("_sorryCantHelpYouButton.Text = \"Sorry can't help you\""));
        AssertTrue("CustomerPanel records terminal dialogue choices as story outcomes",
            customerPanel.Contains("RecordStoryCustomerInteractionOutcome(_interaction, outcome)") &&
            customerPanel.Contains("dialogue:"));
        AssertTrue("CustomerPanel signals completed dialogue flow",
            customerPanel.Contains("DialogueResolvedEventHandler") &&
            customerPanel.Contains("EmitSignal(SignalName.DialogueResolved)"));
        AssertTrue("DayController waits for completed dialogue to be closed",
            dayController.Contains("_customerPanel.DialogueResolved += OnCustomerDialogueResolved") &&
            dayController.Contains("private void OnCustomerDialogueResolved()") &&
            dayController.Contains("_awaitingSaleResultClose = true;"));
        AssertTrue("Authored data includes the day-two police constable story customer",
            customers.Contains("\"id\": \"police_constable_day_2_warning\"") &&
            customers.Contains("\"storyCharacterId\": \"police_constable\"") &&
            customers.Contains("\"visitId\": \"day_2_warning\"") &&
            customers.Contains("\"dayExact\": 2"));
        AssertTrue("Police constable dialogue offers positive and negative replies",
            customers.Contains("\"id\": \"positive_reply\"") &&
            customers.Contains("\"id\": \"negative_reply\"") &&
            customers.Contains("\"label\": \"Answer politely\"") &&
            customers.Contains("\"label\": \"Answer sharply\""));
        AssertTrue("Police constable dialogue records the player's reply tone",
            customers.Contains("\"addStoryFlag\": \"police_constable_positive_reply\"") &&
            customers.Contains("\"addStoryFlag\": \"police_constable_negative_reply\""));
    }

    private static void TestCustomerDropBoxDisablesAfterSale()
    {
        var customerPanel = ReadProjectFile("Scripts/UI/CustomerPanel.cs");
        var dropBox = ReadProjectFile("Scripts/UI/CustomerSellDropBox.cs");

        AssertTrue("CustomerPanel enables dropbox during pending sale state",
            customerPanel.Contains("_sellDropBox.SetAcceptDrops(true);"));
        AssertTrue("CustomerPanel disables dropbox during resolved sale state",
            customerPanel.Contains("_sellDropBox.SetAcceptDrops(false);"));
        AssertTrue("CustomerPanel only highlights valid potion hover previews",
            customerPanel.Contains("_sellDropBox.SetHoverHighlight(true);") &&
            customerPanel.Contains("IsPotionItem(itemId)"));
        AssertTrue("CustomerPanel clears the sell drop highlight on hover exit",
            customerPanel.Contains("_sellDropBox.SetHoverHighlight(false);"));
        AssertTrue("CustomerPanel fades the sell box while the sale is resolved",
            customerPanel.Contains("_sellDropBox.SetDisabledVisual(true);"));
        AssertTrue("CustomerPanel restores the sell box when a new customer is prepared",
            customerPanel.Contains("_sellDropBox.SetDisabledVisual(false);"));
        var closeSaleResultBody = string.Empty;
        var closeSaleResultIndex = customerPanel.IndexOf("private void OnSaleResultClosePressed()", StringComparison.Ordinal);
        if (closeSaleResultIndex >= 0)
        {
            var nextMethodIndex = customerPanel.IndexOf("private string BuildOutcomeText", closeSaleResultIndex, StringComparison.Ordinal);
            if (nextMethodIndex > closeSaleResultIndex)
                closeSaleResultBody = customerPanel.Substring(closeSaleResultIndex, nextMethodIndex - closeSaleResultIndex);
        }
        AssertTrue("CustomerPanel keeps the sell box faded until the next customer appears",
            closeSaleResultBody.Contains("HideSaleResult();") &&
            closeSaleResultBody.Contains("_interaction = null;") &&
            !closeSaleResultBody.Contains("SetSalePendingState();"));
        AssertTrue("CustomerSellDropBox exposes an explicit accept-drops toggle",
            dropBox.Contains("SetAcceptDrops(bool acceptDrops)"));
        AssertTrue("CustomerSellDropBox exposes a hover highlight toggle",
            dropBox.Contains("SetHoverHighlight(bool active)"));
        AssertTrue("CustomerSellDropBox exposes a disabled visual toggle",
            dropBox.Contains("SetDisabledVisual(bool disabled)"));
        AssertTrue("CustomerSellDropBox refuses hover previews while disabled",
            dropBox.Contains("if (!_acceptDrops)") && dropBox.Contains("return false;"));
        AssertTrue("CustomerSellDropBox refuses drops while disabled",
            dropBox.Contains("if (!_acceptDrops)") && dropBox.Contains("return;"));
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

    private static void TestDataDbReloadsAuthoredResourceCatalogsOnly()
    {
        var source = ReadProjectFile("Scripts/Autoload/DataDb.cs");
        var resource = ReadProjectFile("Data/authored_data.tres");
        AssertTrue("DataDb reload entry point exists", source.Contains("public override void _Ready()"));
        AssertTrue("DataDb reloads on ready", source.Contains("ReloadAll();"));
        AssertTrue("DataDb loads authored data resource", source.Contains("ResourceLoader.Load<AuthoredDataResource>"));
        AssertTrue("DataDb references the authored data resource path", source.Contains("AuthoredDataPath"));
        AssertTrue("Authored data resource file exists", resource.Contains("script_class=\"AuthoredDataResource\""));
        AssertTrue("Authored data resource stores item catalog", resource.Contains("ItemsPath = \"res://Data/items_data.tres\""));
        AssertTrue("Authored data resource stores rule catalog", resource.Contains("RulesPath = \"res://Data/rules_data.tres\""));
        AssertTrue("Authored data resource stores event catalog", resource.Contains("EventsPath = \"res://Data/events_data.tres\""));
        AssertTrue("Authored data resource stores customer catalog", resource.Contains("CustomerInteractionsPath = \"res://Data/customers_data.tres\""));
        AssertTrue("Authored data resource stores synergy catalog", resource.Contains("SynergiesPath = \"res://Data/synergies_data.tres\""));
        AssertTrue("DataDb does not register runtime items", !source.Contains("RegisterRuntimePotionItem"));
        AssertTrue("DataDb does not reference runtime catalog", !source.Contains("RuntimeContentDb"));
    }

    private static void TestUiLookupUsesRuntimeFirstCatalog()
    {
        var itemCatalog = ReadProjectFile("Scripts/Autoload/ItemCatalog.cs");
        var itemCatalogService = ReadProjectFile("Scripts/Autoload/ItemCatalogService.cs");
        AssertTrue("ItemCatalog static wrapper delegates to the service", itemCatalog.Contains("Service.TryGetItem(itemId, out item)"));
        AssertTrue("ItemCatalogService checks runtime first", itemCatalogService.Contains("_runtimeContentDb.TryGetItem(itemId, out item)"));
        AssertTrue("ItemCatalogService falls back to DataDb", itemCatalogService.Contains("_dataDb.TryGetItem(itemId, out item)"));

        var brewPanel = ReadProjectFile("Scripts/UI/BrewPanel.cs");
        var inventoryPanel = ReadProjectFile("Scripts/UI/InventoryPanel.cs");
        var recipeBookPanel = ReadProjectFile("Scripts/UI/RecipeBookPanel.cs");
        var customerPanel = ReadProjectFile("Scripts/UI/CustomerPanel.cs");
        var brewService = ReadProjectFile("Scripts/Systems/PotionInventoryBrewService.cs");

        AssertTrue("BrewPanel resolves ItemCatalogService through an exported path", brewPanel.Contains("GetNodeOrNull<ItemCatalogService>(ItemCatalogPath)"));
        AssertTrue("InventoryPanel resolves ItemCatalogService through an exported path", inventoryPanel.Contains("GetNodeOrNull<ItemCatalogService>(ItemCatalogPath)"));
        AssertTrue("InventoryPanel exposes item type tag path for detail view", ReadProjectFile("Scenes/UI/InventoryPanel.tscn").Contains("ItemDetailTypeTagPath = NodePath(\"../InventoryItemDetail/Panel/Margin/VBox/TopRow/Identity/TypeTag\")"));
        AssertTrue("InventoryPanel uses player-visible tag rules for item type text", inventoryPanel.Contains("ItemTagDisplayRules"));
        AssertTrue("RecipeBookPanel resolves ItemCatalogService through an exported path", recipeBookPanel.Contains("GetNodeOrNull<ItemCatalogService>(ItemCatalogPath)"));
        AssertTrue("CustomerPanel resolves ItemCatalogService through an exported path", customerPanel.Contains("GetNodeOrNull<ItemCatalogService>(ItemCatalogPath)"));
        AssertTrue("PotionInventoryBrewService uses constructor-injected ItemCatalogService", brewService.Contains("PotionInventoryBrewService(GameState gameState, ItemCatalogService itemCatalog)"));
        AssertTrue("BrewPanel still registers runtime potions separately", brewPanel.Contains("RegisterRuntimePotionItem"));
    }

    private static void TestMainMenuLoadFlow()
    {
        var source = ReadProjectFile("Scripts/UI/MainMenu.cs");
        var scene = ReadProjectFile("MainMenu.tscn");

        AssertTrue("MainMenu has load button path", source.Contains("LoadButtonPath"));
        AssertTrue("MainMenu has new game button path", source.Contains("NewGameButtonPath"));
        AssertTrue("MainMenu continues the latest save", source.Contains("LoadLatestGameIfExists()"));
        AssertTrue("MainMenu falls back to a new game when no save exists", source.Contains("StartNewGame();"));
        AssertTrue("MainMenu hides continue until saves exist", source.Contains("Visible = _saveGameManager.HasSavedGames()"));
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

    private static void TestGameUiKeepsPotionTraitFilterWired()
    {
        var scene = ReadProjectFile("Scenes/UI/GameUi.tscn");

        AssertTrue("GameUi keeps potion trait filter path wired", !scene.Contains("PotionsTraitFilterPath = null"));
        AssertTrue("GameUi keeps potion clear filter path wired", !scene.Contains("PotionsClearFilterButtonPath = null"));
        AssertTrue("InventoryPanel scene defines potion trait filter", ReadProjectFile("Scenes/UI/InventoryPanel.tscn").Contains("PotionsTraitFilterPath = NodePath(\"Panel/Margin/VBox/PotionsHeaderRow/TraitFilter\")"));
        AssertTrue("InventoryPanel scene defines potion clear filter", ReadProjectFile("Scenes/UI/InventoryPanel.tscn").Contains("PotionsClearFilterButtonPath = NodePath(\"Panel/Margin/VBox/PotionsHeaderRow/Clear\")"));
    }

    private static void TestRecipeBookFiltersAreWired()
    {
        var source = ReadProjectFile("Scripts/UI/RecipeBookPanel.cs");
        var scene = ReadProjectFile("Scenes/UI/GameUi.tscn");

        AssertTrue("RecipeBookPanel exports a reset button path", source.Contains("ResetButtonPath"));
        AssertTrue("RecipeBookPanel exports a search input path", source.Contains("SearchInputPath"));
        AssertTrue("RecipeBookPanel exports a sort filter path", source.Contains("SortFilterPath"));
        AssertTrue("RecipeBookPanel exports a trait filter path", source.Contains("TraitFilterPath"));
        AssertTrue("RecipeBookPanel exports a risk filter path", source.Contains("RiskFilterPath"));
        AssertTrue("RecipeBookPanel wires the reset button handler", source.Contains("_resetButton.Pressed += ClearFilters"));
        AssertTrue("RecipeBookPanel reset button clears filters", source.Contains("private void ClearFilters()"));
        AssertTrue("RecipeBookPanel builds trait filter options from learned potions", source.Contains("ItemFilterUtilities.BuildTopTraitNames"));
        AssertTrue("RecipeBookPanel builds risk filter options from learned potions", source.Contains("ItemFilterUtilities.BuildRiskNames"));
        AssertTrue("RecipeBookPanel filters by traits", source.Contains("ItemFilterUtilities.ItemHasTrait"));
        AssertTrue("RecipeBookPanel filters by risks", source.Contains("ItemFilterUtilities.ItemHasRisk"));
        AssertTrue("RecipeBookPanel scene wires reset button path", scene.Contains("ResetButtonPath = NodePath(\"Panel/Margin/VBox/Header/SearchRow/ResetFilters\")"));
        AssertTrue("RecipeBookPanel scene wires search input path", scene.Contains("SearchInputPath = NodePath(\"Panel/Margin/VBox/Header/SearchRow/SearchInput\")"));
        AssertTrue("RecipeBookPanel scene wires sort filter path", scene.Contains("SortFilterPath = NodePath(\"Panel/Margin/VBox/Header/FilterRow/SortFilter\")"));
        AssertTrue("RecipeBookPanel scene wires trait filter path", scene.Contains("TraitFilterPath = NodePath(\"Panel/Margin/VBox/Header/FilterRow/TraitFilter\")"));
        AssertTrue("RecipeBookPanel scene wires risk filter path", scene.Contains("RiskFilterPath = NodePath(\"Panel/Margin/VBox/Header/FilterRow/RiskFilter\")"));
        AssertTrue("RecipeBookPanel scene places search input in the search row", scene.Contains("[node name=\"SearchInput\" type=\"LineEdit\" parent=\"RecipeBookPanel/Panel/Margin/VBox/Header/SearchRow\"]"));
        AssertTrue("RecipeBookPanel scene places sort filter in the filter row", scene.Contains("[node name=\"SortFilter\" type=\"OptionButton\" parent=\"RecipeBookPanel/Panel/Margin/VBox/Header/FilterRow\"]"));
        AssertTrue("RecipeBookPanel scene places reset button in the search row", scene.Contains("[node name=\"ResetFilters\" type=\"Button\" parent=\"RecipeBookPanel/Panel/Margin/VBox/Header/SearchRow\"]"));
        AssertTrue("RecipeBookPanel scene includes a trait filter OptionButton", scene.Contains("[node name=\"TraitFilter\" type=\"OptionButton\" parent=\"RecipeBookPanel/Panel/Margin/VBox/Header/FilterRow\"]"));
        AssertTrue("RecipeBookPanel scene includes a risk filter OptionButton", scene.Contains("[node name=\"RiskFilter\" type=\"OptionButton\" parent=\"RecipeBookPanel/Panel/Margin/VBox/Header/FilterRow\"]"));
    }

    private static void TestRecipeBookClearButtonIsWired()
    {
        var source = ReadProjectFile("Scripts/UI/RecipeBookPanel.cs");

        AssertTrue("RecipeBookPanel reset button field exists", source.Contains("private Button? _resetButton;"));
        AssertTrue("RecipeBookPanel reset button is resolved from the scene", source.Contains("_resetButton = GetNodeOrNull<Button>(ResetButtonPath);"));
        AssertTrue("RecipeBookPanel reset button subscribes on ready", source.Contains("_resetButton.Pressed += ClearFilters;"));
        AssertTrue("RecipeBookPanel reset button unsubscribes on exit", source.Contains("_resetButton.Pressed -= ClearFilters;"));
        AssertTrue("RecipeBookPanel reset button clears the active filters", source.Contains("_activeTraitFilter = null;") && source.Contains("_activeRiskFilter = null;"));
        AssertTrue("RecipeBookPanel reset button clears search text", source.Contains("_searchInput.Text = string.Empty;"));
        AssertTrue("RecipeBookPanel reset button resets filter selections", source.Contains("_traitFilter.Selected = 0;") && source.Contains("_riskFilter.Selected = 0;"));
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

    private static void TestStartingInventorySeedsOnlyTutorialRecipeItems()
    {
        var source = ReadProjectFile("Scripts/Autoload/GameState.cs");

        AssertTrue("GameState defines a curated starter inventory",
            source.Contains("private static readonly (string ItemId, int Quantity)[] StartingInventory"));
        AssertTrue("GameState starts with Grave Mint",
            source.Contains("(\"grave_mint\", 1)"));
        AssertTrue("GameState starts with Obsidian Resin",
            source.Contains("(\"obsidian_resin\", 1)"));
        AssertTrue("GameState starts with Iron Lullaby Root",
            source.Contains("(\"iron_lullaby_root\", 1)"));
        AssertTrue("GameState seeds only the curated list instead of every ingredient",
            source.Contains("foreach (var (itemId, qty) in StartingInventory)") &&
            !source.Contains("AddStartingStack(item.Id, 10);") &&
            !source.Contains("IsIngredient(item)"));
    }

    private static void TestGardenCropDefinitionsCoverAuthoredIngredients()
    {
        var source = ReadProjectFile("Scripts/Autoload/GameState.cs");

        AssertTrue("GameState defines three starting garden pots", source.Contains("public const int StartingGardenPotCount = 3;"));
        AssertTrue("Garden harvest yield starts fixed at two", source.Contains("public const int DefaultGardenHarvestYield = 2;"));
        AssertTrue("GameState defines garden crop definitions", source.Contains("private static readonly GardenCropDef[] GardenCropDefinitions"));

        var expectedCrops = new Dictionary<string, int>
        {
            ["amber_nightshade"] = 1,
            ["obsidian_resin"] = 2,
            ["iron_lullaby_root"] = 3,
            ["mooncap_mushroom"] = 1,
            ["grave_mint"] = 2,
            ["black_ichor"] = 1,
            ["lavender_ash"] = 3,
            ["silver_thorn_bloom"] = 2,
            ["moonwhisper_orchid"] = 3,
            ["raven_ash_peony"] = 1
        };

        foreach (var crop in expectedCrops)
        {
            AssertTrue($"{crop.Key} crop definition exists",
                source.Contains($"CreateGardenCrop(\"{crop.Key}\", growthDays: {crop.Value})"));
            AssertTrue($"{crop.Key} authored item exists",
                ReadProjectFile("Data/items_data.tres").Contains($"\"id\": \"{crop.Key}\""));
        }

        AssertTrue("Starter seed inventory includes amber nightshade",
            source.Contains("(\"seed_amber_nightshade\", 1)"));
        AssertTrue("Starter seed inventory includes obsidian resin",
            source.Contains("(\"seed_obsidian_resin\", 1)"));
        AssertTrue("Starter seed inventory includes iron lullaby root",
            source.Contains("(\"seed_iron_lullaby_root\", 1)"));
    }

    private static void TestGardenStatePersistenceWiring()
    {
        var gameStateSource = ReadProjectFile("Scripts/Autoload/GameState.cs");
        var saveDataSource = ReadProjectFile("Scripts/Persistence/SaveData.cs");
        var saveManagerSource = ReadProjectFile("Scripts/Autoload/SaveGameManager.cs");
        var cropDefSource = ReadProjectFile("Scripts/Models/GardenCropDef.cs");
        var potStateSource = ReadProjectFile("Scripts/Models/GardenPotState.cs");

        AssertTrue("Save files use version two for garden state", saveDataSource.Contains("public int Version { get; set; } = 2;"));
        AssertTrue("Save manager accepts garden save version", saveManagerSource.Contains("private const int CurrentSaveVersion = 2;"));
        AssertTrue("Snapshot includes garden initialization marker", saveDataSource.Contains("public bool GardenInitialized { get; set; }"));
        AssertTrue("Snapshot includes garden pot count", saveDataSource.Contains("public int GardenPotCount { get; set; }"));
        AssertTrue("Snapshot includes seed inventory", saveDataSource.Contains("public Dictionary<string, int> SeedInventory"));
        AssertTrue("Snapshot includes garden pots", saveDataSource.Contains("public List<GardenPotState> GardenPots"));

        AssertTrue("GameState exposes seed inventory", gameStateSource.Contains("public IReadOnlyDictionary<string, int> SeedInventory"));
        AssertTrue("GameState exposes garden pots", gameStateSource.Contains("public IReadOnlyList<GardenPotState> GardenPots"));
        AssertTrue("GameState seeds starting garden pots", gameStateSource.Contains("EnsureGardenPotCount(StartingGardenPotCount);"));
        AssertTrue("GameState seeds starter seed inventory", gameStateSource.Contains("SeedStartingSeedInventory();"));
        AssertTrue("GameState migrates old saves into a garden state", gameStateSource.Contains("if (snapshot.GardenInitialized)") && gameStateSource.Contains("else") && gameStateSource.Contains("SeedStartingSeedInventory();"));
        AssertTrue("GameState snapshots garden state", gameStateSource.Contains("GardenInitialized = true") && gameStateSource.Contains("GardenPots = CloneGardenPots()"));
        AssertTrue("GameState advances garden growth on next day", gameStateSource.Contains("public void NextDay()") && gameStateSource.Contains("AdvanceGardenGrowth();"));
        AssertTrue("GameState can plant seeds", gameStateSource.Contains("public bool TryPlantSeed(int potIndex, string seedId, out string error)"));
        AssertTrue("GameState can harvest garden pots", gameStateSource.Contains("public bool TryHarvestGardenPot(int potIndex, out string error)"));
        AssertTrue("Harvest adds ingredient and returns seed", gameStateSource.Contains("Inventory[pot.IngredientId]") && gameStateSource.Contains("AddSeedStack(pot.SeedId, 1);"));
        AssertTrue("Garden pot upgrades are supported", gameStateSource.Contains("public void SetUnlockedGardenPotCount(int potCount)"));

        AssertTrue("Crop def stores yield range", cropDefSource.Contains("HarvestYieldMin") && cropDefSource.Contains("HarvestYieldMax"));
        AssertTrue("Pot state stores growth progress", potStateSource.Contains("DaysGrown") && potStateSource.Contains("RequiredGrowthDays"));
        AssertTrue("Pot state exposes ready status", potStateSource.Contains("public bool IsReady"));
    }

    private static void TestGardenSceneAndHudNavigation()
    {
        var hudSource = ReadProjectFile("Scripts/UI/Hud.cs");
        var hudScene = ReadProjectFile("Scenes/UI/Hud.tscn");
        var gardenSource = ReadProjectFile("Scripts/UI/Garden.cs");
        var gardenScene = ReadProjectFile("Scenes/Main/Garden.tscn");

        AssertTrue("Hud points to the garden scene", hudSource.Contains("res://Scenes/Main/Garden.tscn"));
        AssertTrue("Hud has a garden button field", hudSource.Contains("private Button _gardenButton"));
        AssertTrue("Hud resolves the garden button", hudSource.Contains("GetNode<Button>(\"Garden\")"));
        AssertTrue("Hud disables garden while shop is open", hudSource.Contains("_gardenButton.Disabled = isShopOpen;"));
        AssertTrue("Hud autosaves before entering garden", hudSource.Contains("TryAutoSave(\"entering the garden\")"));
        AssertTrue("Hud scene includes Garden button", hudScene.Contains("[node name=\"Garden\" type=\"Button\" parent=\".\"]"));
        AssertTrue("Garden button is beside potion book", hudScene.Contains("text = \"Potion Book\"") && hudScene.Contains("text = \"Garden\""));

        AssertTrue("Garden script exists", gardenSource.Contains("public partial class Garden : Control"));
        AssertTrue("Garden script returns to main scene", gardenSource.Contains("res://Main.tscn"));
        AssertTrue("Garden autosaves on entry", gardenSource.Contains("TryAutoSave(\"entering the garden\")"));
        AssertTrue("Garden autosaves after planting", gardenSource.Contains("TryAutoSave(\"planting a seed\")"));
        AssertTrue("Garden autosaves after harvesting", gardenSource.Contains("TryAutoSave(\"harvesting a crop\")"));
        AssertTrue("Garden autosaves before leaving", gardenSource.Contains("TryAutoSave(\"leaving the garden\")"));
        AssertTrue("Garden scene uses the garden script", gardenScene.Contains("path=\"res://Scripts/UI/Garden.cs\""));
        AssertTrue("Garden scene wires pots container", gardenScene.Contains("PotsContainerPath = NodePath(\"Root/Margin/Main/Content/PotsColumn/Pots\")"));
        AssertTrue("Garden scene wires seeds container", gardenScene.Contains("SeedsContainerPath = NodePath(\"Root/Margin/Main/Content/SeedsColumn/Seeds\")"));
        AssertTrue("Garden scene wires back button", gardenScene.Contains("BackButtonPath = NodePath(\"Root/Margin/Main/Header/Back\")"));
    }

    private static void TestTutorialGameStateTransitions()
    {
        var source = ReadProjectFile("Scripts/Autoload/GameState.cs");

        AssertTrue("GameState exposes explicit tutorial status", source.Contains("public TutorialStatus TutorialProgressStatus { get; private set; }"));
        AssertTrue("GameState keeps requested compatibility view", source.Contains("public bool TutorialRequested => TutorialProgressStatus == TutorialStatus.InProgress;"));
        AssertTrue("GameState keeps completed compatibility view", source.Contains("public bool TutorialCompleted => TutorialProgressStatus == TutorialStatus.Completed;"));
        AssertTrue("GameState keeps skipped compatibility view", source.Contains("public bool TutorialSkipped => TutorialProgressStatus == TutorialStatus.Skipped;"));
        AssertTrue("GameState exposes tutorial step", source.Contains("public int TutorialStep { get; private set; }"));

        AssertTrue("RequestTutorial exists", source.Contains("public void RequestTutorial()"));
        AssertTrue("RequestTutorial sets status to in progress", source.Contains("TutorialProgressStatus = TutorialStatus.InProgress;"));

        AssertTrue("SkipTutorial exists", source.Contains("public void SkipTutorial()"));
        AssertTrue("SkipTutorial sets status to skipped", source.Contains("TutorialProgressStatus = TutorialStatus.Skipped;"));

        AssertTrue("CompleteTutorial exists", source.Contains("public void CompleteTutorial()"));
        AssertTrue("CompleteTutorial sets status to completed", source.Contains("TutorialProgressStatus = TutorialStatus.Completed;"));

        AssertTrue("SetTutorialStep exists", source.Contains("public void SetTutorialStep(int step)"));
        AssertTrue("SetTutorialStep clamps to zero or above", source.Contains("Math.Max(0, step)"));
    }

    private static void TestTutorialSnapshotRoundTrip()
    {
        var gameStateSource = ReadProjectFile("Scripts/Autoload/GameState.cs");
        var saveDataSource = ReadProjectFile("Scripts/Persistence/SaveData.cs");

        AssertTrue("Save snapshot includes TutorialStatus", saveDataSource.Contains("public TutorialStatus? TutorialStatus { get; set; }"));
        AssertTrue("Save snapshot includes TutorialStepIndex", saveDataSource.Contains("public int TutorialStepIndex { get; set; }"));
        AssertTrue("Save snapshot includes TutorialRequested", saveDataSource.Contains("public bool TutorialRequested { get; set; }"));
        AssertTrue("Save snapshot includes TutorialCompleted", saveDataSource.Contains("public bool TutorialCompleted { get; set; }"));
        AssertTrue("Save snapshot includes TutorialSkipped", saveDataSource.Contains("public bool TutorialSkipped { get; set; }"));
        AssertTrue("Save snapshot includes TutorialStep", saveDataSource.Contains("public int TutorialStep { get; set; }"));

        AssertTrue("BuildSnapshot exports explicit TutorialStatus", gameStateSource.Contains("TutorialStatus = TutorialProgressStatus"));
        AssertTrue("BuildSnapshot exports TutorialStepIndex", gameStateSource.Contains("TutorialStepIndex = TutorialStep"));
        AssertTrue("BuildSnapshot exports TutorialRequested", gameStateSource.Contains("TutorialRequested = TutorialRequested"));
        AssertTrue("BuildSnapshot exports TutorialCompleted", gameStateSource.Contains("TutorialCompleted = TutorialCompleted"));
        AssertTrue("BuildSnapshot exports TutorialSkipped", gameStateSource.Contains("TutorialSkipped = TutorialSkipped"));
        AssertTrue("BuildSnapshot exports TutorialStep", gameStateSource.Contains("TutorialStep = TutorialStep"));

        AssertTrue("ApplySnapshot resolves tutorial status from explicit or legacy fields", gameStateSource.Contains("TutorialProgressStatus = ResolveTutorialStatus(snapshot);"));
        AssertTrue("ApplySnapshot restores step with new step index fallback", gameStateSource.Contains("var restoredStep = snapshot.TutorialStepIndex > 0"));
        AssertTrue("ApplySnapshot clamps tutorial step", gameStateSource.Contains("TutorialStep = Math.Max(0, restoredStep);"));
    }

    private static void TestMainSceneWiresTutorialController()
    {
        var source = ReadProjectFile("Main.tscn");

        AssertTrue("Main scene references TutorialController script", source.Contains("path=\"res://Scripts/Controllers/TutorialController.cs\""));
        AssertTrue("Main scene includes TutorialController node", source.Contains("[node name=\"TutorialController\" type=\"Node\" parent=\".\"]"));
        AssertTrue("TutorialController wires overlay path", source.Contains("TutorialOverlayPath = NodePath(\"../CanvasLayer/TutorialOverlay\")"));
        AssertTrue("TutorialController wires HUD path", source.Contains("HudPath = NodePath(\"../CanvasLayer/Hud\")"));
        AssertTrue("TutorialController wires DayController path", source.Contains("DayControllerPath = NodePath(\"../DayController\")"));
    }

    private static void TestTutorialOverlaySceneWiring()
    {
        var scene = ReadProjectFile("Scenes/UI/TutorialOverlay.tscn");

        AssertTrue("Tutorial overlay scene references script", scene.Contains("path=\"res://Scripts/UI/TutorialOverlay.cs\""));
        AssertTrue("Tutorial overlay root is Control", scene.Contains("[node name=\"TutorialOverlay\" type=\"Control\"]"));
        AssertTrue("Tutorial overlay has skip button", scene.Contains("[node name=\"SkipButton\" type=\"Button\" parent=\"Panel/Margin/VBox/Actions\"]"));
        AssertTrue("Tutorial overlay has next button", scene.Contains("[node name=\"NextButton\" type=\"Button\" parent=\"Panel/Margin/VBox/Actions\"]"));
        AssertTrue("Tutorial overlay exports skip path", scene.Contains("SkipButtonPath = NodePath(\"Panel/Margin/VBox/Actions/SkipButton\")"));
        AssertTrue("Tutorial overlay exports next path", scene.Contains("NextButtonPath = NodePath(\"Panel/Margin/VBox/Actions/NextButton\")"));
    }

    private static void TestTutorialArchitectureExtraction()
    {
        var controller = ReadProjectFile("Scripts/Controllers/TutorialController.cs");
        var stateMachine = ReadProjectFile("Scripts/Tutorial/TutorialStateMachine.cs");
        var tutorialContent = ReadProjectFile("Scripts/Tutorial/TutorialContentResource.cs");
        var tutorialStepContent = ReadProjectFile("Scripts/Tutorial/TutorialStepContentResource.cs");
        var presenter = ReadProjectFile("Scripts/Tutorial/Presentation/TutorialOverlayPresenter.cs");
        var interactionGate = ReadProjectFile("Scripts/Tutorial/Presentation/TutorialInteractionGate.cs");
        var brewPanel = ReadProjectFile("Scripts/UI/BrewPanel.cs");

        AssertTrue("TutorialController uses extracted state machine", controller.Contains("private TutorialStateMachine _stateMachine"));
        AssertTrue("TutorialController uses extracted overlay presenter", controller.Contains("private TutorialOverlayPresenter _overlayPresenter"));
        AssertTrue("TutorialController uses extracted interaction gate", controller.Contains("private readonly TutorialInteractionGate _interactionGate"));
        AssertTrue("TutorialController consumes tutorial content resource", controller.Contains("[Export] public TutorialContentResource TutorialContent"));
        AssertTrue("TutorialController uses potion sold events for the sale review step", controller.Contains("_customerPanel.PotionSold += OnPotionSold;"));
        AssertTrue("TutorialController no longer caches sale score details for tutorial feedback", !controller.Contains("_lastTutorialSaleScore") && !controller.Contains("_lastTutorialSaleGrade"));
        AssertTrue("TutorialController resolves step-specific button locks", controller.Contains("UpdateTutorialButtonLock("));
        AssertTrue("TutorialController includes the close shop tutorial step", controller.Contains("TutorialStepId.CloseShop"));
        AssertTrue("TutorialController highlights the close shop button", controller.Contains("case TutorialStepId.CloseShop") && controller.Contains("GetNextCustomerButton()"));
        AssertTrue("TutorialController waits for close shop visibility before advancing", controller.Contains("EvaluateCloseShopPrompt("));
        AssertTrue("TutorialController caches HUD day label for tutorial highlighting", controller.Contains("_hudDayLabel = GetOptionalHudLabel(\"Day\")"));
        AssertTrue("TutorialController caches HUD shop timer label for tutorial highlighting", controller.Contains("_hudShopTimerLabel = GetOptionalHudLabel(\"ShopTimer\")"));
        AssertTrue("TutorialController highlights ingredient queue steps with the brew panel", controller.Contains("ShowIngredientQueueStep(stepContent, _tutorialContent.GraveMintId)") && controller.Contains("ShowForTargets(") && controller.Contains("FocusTutorialBrewPanel()"));
        AssertTrue("TutorialController routes the sale review popup through the customer panel", controller.Contains("ShowForTarget(") && controller.Contains("_customerPanel,") && controller.Contains("BuildSaleResultBody("));
        AssertTrue("TutorialController seeds the next-customer tutorial inventory", controller.Contains("SeedNextCustomerTutorialInventory()"));
        AssertTrue("TutorialController highlights status step with a combined HUD rect", controller.Contains("ShowForHighlightRect(stepContent, statusHighlightRect)"));
        AssertTrue("TutorialController builds a combined status highlight rectangle", controller.Contains("TryGetStatusHighlightRect(out var statusHighlightRect)"));
        AssertTrue("TutorialController forces the shop timer to zero before the final tutorial ingredient step", controller.Contains("AddTwoMoreSleepIngredients") && controller.Contains("ForceShopTimerToZeroForTutorial()"));
        AssertTrue("TutorialOverlayPresenter supports direct highlight rectangles", presenter.Contains("ShowForHighlightRect("));

        AssertTrue("TutorialStateMachine is a pure class", stateMachine.Contains("public sealed class TutorialStateMachine"));
        AssertTrue("TutorialStateMachine clamps tutorial step", stateMachine.Contains("public TutorialStepId ClampStep(int rawStep)"));
        AssertTrue("TutorialStateMachine includes close shop prompt transition", stateMachine.Contains("EvaluateCloseShopPrompt("));
        AssertTrue("TutorialStateMachine completes when the shop closes on the close shop step", stateMachine.Contains("step == TutorialStepId.CloseShop && !isShopOpen"));
        AssertTrue("DayController exposes a tutorial-only timer reset helper", ReadProjectFile("Scripts/Controllers/DayController.cs").Contains("public void ForceShopTimerToZeroForTutorial()"));
        AssertTrue("TutorialContentResource exists", tutorialContent.Contains("public partial class TutorialContentResource : Resource"));
        AssertTrue("TutorialContentResource includes the close shop step copy", tutorialContent.Contains("StepId = (int)TutorialStepId.CloseShop"));
        AssertTrue("TutorialContentResource tells the player to close the shop at night", tutorialContent.Contains("It is night time. Close the shop to end the day."));
        AssertTrue("Tutorial step content can lock other buttons", tutorialStepContent.Contains("public bool LockOtherButtons { get; set; }"));
        AssertTrue("Tutorial overlay presenter exists", presenter.Contains("public sealed class TutorialOverlayPresenter"));
        AssertTrue("Tutorial interaction gate exists", interactionGate.Contains("public sealed class TutorialInteractionGate"));
        AssertTrue("Tutorial interaction gate restores previous button state before reapplying", interactionGate.Contains("Restore();"));
        AssertTrue("BrewPanel exposes its brew button for tutorial locks", brewPanel.Contains("public Button? GetBrewButton()"));
    }

    private static void TestTutorialNextCustomerInventorySeedIsCurated()
    {
        var source = ReadProjectFile("Scripts/Autoload/GameState.cs");

        AssertTrue("GameState defines a curated next-customer tutorial inventory",
            source.Contains("private static readonly (string ItemId, int Quantity)[] NextCustomerTutorialInventory"));
        AssertTrue("Next-customer inventory includes the rest trait ingredient",
            source.Contains("(\"black_ichor\", 1)"));
        AssertTrue("Next-customer inventory includes the calm trait ingredient",
            source.Contains("(\"mooncap_mushroom\", 1)"));
        AssertTrue("Next-customer inventory includes the dreams trait ingredient",
            source.Contains("(\"lavender_ash\", 1)"));
        AssertTrue("Next-customer inventory is seeded through a dedicated helper",
            source.Contains("public void SeedNextCustomerTutorialInventory()"));
        AssertTrue("Next-customer inventory clears the inventory before seeding",
            source.Contains("Inventory.Clear();"));
        AssertTrue("Next-customer inventory seeds exactly the curated ingredient list",
            source.Contains("foreach (var (itemId, qty) in NextCustomerTutorialInventory)"));
    }

    private static void TestTutorialSaleReviewFeedbackUsesRequestWording()
    {
        var controller = ReadProjectFile("Scripts/Controllers/TutorialController.cs");
        var tutorialContent = ReadProjectFile("Scripts/Tutorial/TutorialContentResource.cs");

        AssertTrue("Sale review step is titled as a review", tutorialContent.Contains("Title = \"Sale Review\""));
        AssertTrue("Sale review step keeps a continue button label", tutorialContent.Contains("NextButtonText = \"Continue\""));
        AssertTrue("TutorialContentResource exposes request-only sale feedback", tutorialContent.Contains("public string BuildSaleResultBody(bool saleSucceeded)"));
        AssertTrue("TutorialContentResource explains success in customer-request terms", tutorialContent.Contains("You used the ingredients the customer wanted."));
        AssertTrue("TutorialContentResource explains failure in customer-request terms", tutorialContent.Contains("You need to read the customer request more carefully next time."));
        AssertTrue("TutorialContentResource no longer references score values", !tutorialContent.Contains("finalScore") && !tutorialContent.Contains("grade"));
        AssertTrue("Close shop step is titled explicitly", tutorialContent.Contains("Title = \"Close the Shop\""));
        AssertTrue("TutorialController uses the request-only sale feedback builder", controller.Contains("BuildSaleResultBody(_lastTutorialSaleSucceeded)"));
    }

    private static void TestTutorialOverlayUsesDynamicCutoutsOnly()
    {
        var overlaySource = ReadProjectFile("Scripts/UI/TutorialOverlay.cs");
        var overlayScene = ReadProjectFile("Scenes/UI/TutorialOverlay.tscn");

        AssertTrue("TutorialOverlay keeps dynamic cutout method", overlaySource.Contains("UpdateDimCutouts"));
        AssertTrue("TutorialOverlay removed legacy single-cutout method", !overlaySource.Contains("UpdateDimCutout("));
        AssertTrue("TutorialOverlay removed legacy optional dim-rect lookup", !overlaySource.Contains("GetOptionalDimRect("));
        AssertTrue("TutorialOverlay scene removed legacy DimTop", !overlayScene.Contains("[node name=\"DimTop\""));
        AssertTrue("TutorialOverlay scene removed legacy DimBottom", !overlayScene.Contains("[node name=\"DimBottom\""));
        AssertTrue("TutorialOverlay scene removed legacy DimLeft", !overlayScene.Contains("[node name=\"DimLeft\""));
        AssertTrue("TutorialOverlay scene removed legacy DimRight", !overlayScene.Contains("[node name=\"DimRight\""));
    }

    private static void TestHudReturnToMainMenuDoesNotAutoSave()
    {
        var source = ReadProjectFile("Scripts/UI/Hud.cs");

        AssertTrue("Hud return-to-menu handler exists", source.Contains("OnReturnToMainMenuPressed"));
        AssertTrue("Hud return-to-menu still changes scenes", source.Contains("ChangeSceneToFile(\"res://MainMenu.tscn\")"));
        AssertTrue("Hud return-to-menu no longer auto-saves", !source.Contains("Could not save before returning to main menu"));
    }

    private static void TestScenarioDebuggerStopTimerControls()
    {
        var runtimeDebug = ReadProjectFile("Scripts/Debug/RuntimeDebugImGui.cs");
        var dayController = ReadProjectFile("Scripts/Controllers/DayController.cs");

        AssertTrue("Scenario debugger wires the day controller",
            runtimeDebug.Contains("DayControllerPath = new(\"../DayController\")"));
        AssertTrue("Scenario debugger exposes a stop timer input",
            runtimeDebug.Contains("Stop Timer Seconds"));
        AssertTrue("Scenario debugger exposes an end-day shortcut",
            runtimeDebug.Contains("End Day Now"));
        AssertTrue("Scenario debugger applies the stop timer through DayController",
            runtimeDebug.Contains("TrySetShopTimerSecondsRemaining"));
        AssertTrue("DayController exposes a debug timer setter",
            dayController.Contains("public bool TrySetShopTimerSecondsRemaining(int secondsRemaining)"));
        AssertTrue("DayController can force the stop timer to zero through the shared setter",
            dayController.Contains("ForceShopTimerToZeroForTutorial()") && dayController.Contains("TrySetShopTimerSecondsRemaining(0)"));
    }

    private static void TestHudSettingsPanelClosesOnOutsideClick()
    {
        var source = ReadProjectFile("Scripts/UI/Hud.cs");
        var scene = ReadProjectFile("Scenes/UI/GameUi.tscn");

        AssertTrue("Hud processes raw input for outside clicks", source.Contains("SetProcessInput(true);"));
        AssertTrue("Hud checks clicks against the settings panel bounds", source.Contains("_settingsPanel.GetGlobalRect().HasPoint(mouseButton.GlobalPosition)"));
        AssertTrue("Hud closes settings on outside clicks", source.Contains("SetSettingsPanelVisible(false);"));
        AssertTrue("Hud consumes outside clicks so underlying UI does not steal them", source.Contains("AcceptEvent();"));
        AssertTrue("Hud keeps the settings panel on a dedicated z layer", source.Contains("SettingsPanelZIndex"));
        AssertTrue("Hud brings the settings panel to the front when it opens", source.Contains("_settingsPanel.MoveToFront();"));
        AssertTrue("Hud still toggles settings from the gear button", source.Contains("SetSettingsPanelVisible(!_settingsPanel.Visible);"));
        AssertTrue("GameUi scene no longer adds a separate settings backdrop", !scene.Contains("SettingsBackdrop"));
    }

    private static void TestPersistenceBoundaryIsDocumented()
    {
        var persistenceBoundary = ReadProjectFile("PERSISTENCE_BOUNDARY.md");
        AssertTrue("Persistence boundary note exists", persistenceBoundary.Contains("runtime save/load system"));
        AssertTrue("Persistence boundary documents save directory", persistenceBoundary.Contains("user://saves/"));
        AssertTrue("Authored data reload rule documented", persistenceBoundary.Contains("Authored data: always reload from `res://Data/authored_data.tres`"));
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

    private static object? InvokeInstance(object target, string methodName, params object?[] args)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Missing method {target.GetType().Name}.{methodName}.");

        return method.Invoke(target, args);
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
