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

internal static class SceneAndHudWiringTests
{
    public static void Register(TestRunner runner)
    {
        runner.Run("UI classes exist and keep expected base types", TestUiClassPresenceAndBaseTypes);
        runner.Run("Main menu exposes start and load flows", TestMainMenuLoadFlow);
        runner.Run("Load menu scene is wired for saved game browsing", TestLoadGameMenuScene);
        runner.Run("Game UI keeps the potion trait filter wired", TestGameUiKeepsPotionTraitFilterWired);
        runner.Run("Scenario debugger can set the shop stop timer", TestScenarioDebuggerStopTimerControls);
        runner.Run("Hud return-to-menu does not auto-save", TestHudReturnToMainMenuDoesNotAutoSave);
        runner.Run("Hud settings panel closes on outside click", TestHudSettingsPanelClosesOnOutsideClick);
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

    private static void TestHudReturnToMainMenuDoesNotAutoSave()
    {
        var source = ReadProjectFile("Scripts/UI/Hud.cs");

        AssertTrue("Hud return-to-menu handler exists", source.Contains("OnReturnToMainMenuPressed"));
        AssertTrue("Hud return-to-menu still changes scenes", source.Contains("ChangeSceneToFile(\"res://MainMenu.tscn\")"));
        AssertTrue("Hud return-to-menu no longer auto-saves", !source.Contains("Could not save before returning to main menu"));
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
}
