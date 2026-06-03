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

internal static class RecipeAndPotionBookTests
{
    public static void Register(TestRunner runner)
    {
        runner.Run("RecipeBookPanel dictionary formatting is stable", TestRecipeBookPanelFormatDictionary);
        runner.Run("RecipeBookPanel top-traits formatting is stable", TestRecipeBookPanelFormatTopTraits);
        runner.Run("RecipeBookPanel entry shows traits and risks to the right of ingredients", TestRecipeBookPanelEntryShowsTraitsAndRisksToTheRightOfIngredients);
        runner.Run("PotionBookPanel appends learned runtime potions to the end", TestPotionBookPanelAppendsLearnedRuntimePotionsToTheEnd);
        runner.Run("Potion book views skip tainted learned potions", TestPotionBookViewsSkipTaintedLearnedPotions);
        runner.Run("Recipe book filters are wired", TestRecipeBookFiltersAreWired);
        runner.Run("Recipe book clear button is wired", TestRecipeBookClearButtonIsWired);
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

    private static void TestPotionBookViewsSkipTaintedLearnedPotions()
    {
        var potionBookSource = ReadProjectFile("Scripts/UI/PotionBookPanel.cs");
        var recipeBookSource = ReadProjectFile("Scripts/UI/RecipeBookPanel.cs");

        AssertTrue("PotionBookPanel skips learned potion pages with active risks",
            potionBookSource.Contains("if (HasActiveRisk(potion))") &&
            potionBookSource.Contains("private static bool HasActiveRisk(ItemDef item)"));
        AssertTrue("RecipeBookPanel excludes active-risk potions from learned recipe entries",
            recipeBookSource.Contains("return ItemCatalogService.HasTag(item, \"potion\") && !HasActiveRisk(item);") &&
            recipeBookSource.Contains("private static bool HasActiveRisk(ItemDef item)"));
        AssertTrue("Potion book active risk checks only positive risk values",
            potionBookSource.Contains("risk.Value > 0"));
        AssertTrue("Recipe book active risk checks only positive risk values",
            recipeBookSource.Contains("risk.Value > 0"));
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
}
