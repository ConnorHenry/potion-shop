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
        runner.Run("Customer closeup uses split art and customer data image paths", TestCustomerCloseupUsesSplitArt);
        runner.Run("Shop floor shelf opens potion brewing station view", TestShopFloorShelfOpensPotionBrewingStation);
        runner.Run("Potion brewing station owns diegetic shelf inventory", TestPotionBrewingStationShelfInventory);
        runner.Run("Brew entry points open the potion brewing station", TestBrewEntryPointsOpenPotionBrewingStation);
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
            ["OccultShop.UI.StationShelfInventory"] = "Control",
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

    private static void TestCustomerCloseupUsesSplitArt()
    {
        var scene = ReadProjectFile("Scenes/UI/GameUi.tscn");
        var shopFloor = ReadProjectFile("Scripts/UI/ShopFloor.cs");
        var customerPanel = ReadProjectFile("Scripts/UI/CustomerPanel.cs");
        var tieredCustomers = ReadProjectFile("Data/customers_tiered_test_data.tres");

        AssertTrue("Customer closeup no longer uses the combined customer/background image",
            !scene.Contains("res://art/Customer-Closeup.png"));
        AssertTrue("Customer closeup uses the evening background as a separate image",
            scene.Contains("path=\"res://art/Background - Evening.png\"") &&
            scene.Contains("texture = ExtResource(\"22_shop_background\")"));
        AssertTrue("Customer closeup has a separate customer texture node",
            scene.Contains("[node name=\"Customer\" type=\"TextureRect\" parent=\"CustomerCloseupView\"]") &&
            scene.Contains("texture = ExtResource(\"24_shop_customer\")"));
        AssertTrue("ShopFloor exposes the customer closeup image node path",
            shopFloor.Contains("CustomerCloseupCustomerImagePath"));
        AssertTrue("ShopFloor loads the customer closeup texture from customer data",
            shopFloor.Contains("CurrentCustomerImagePath") &&
            shopFloor.Contains("ResourceLoader.Load<Texture2D>(imagePath)"));
        AssertTrue("CustomerPanel exposes the active customer image path",
            customerPanel.Contains("CurrentCustomerImagePath => _interaction?.CharacterImagePath"));
        AssertTrue("Tiered customer data includes the middleclass woman art path",
            tieredCustomers.Contains("\"characterImagePath\": \"res://art/Middleclass-Woman.png\""));
    }

    private static void TestShopFloorShelfOpensPotionBrewingStation()
    {
        var scene = ReadProjectFile("Scenes/UI/GameUi.tscn");
        var shopFloor = ReadProjectFile("Scripts/UI/ShopFloor.cs");

        AssertTrue("GameUi references the potion brewing station art",
            scene.Contains("path=\"res://art/Potion-Brewing-Station.png\""));
        AssertTrue("GameUi defines the potion brewing station view",
            scene.Contains("[node name=\"PotionBrewingStationView\" type=\"Control\" parent=\".\"]"));
        AssertTrue("GameUi draws the potion brewing station image",
            scene.Contains("[node name=\"Background\" type=\"TextureRect\" parent=\"PotionBrewingStationView\"]") &&
            scene.Contains("texture = ExtResource(\"28_brewing_station\")"));
        AssertTrue("GameUi defines a left return hotspot on the potion brewing station view",
            scene.Contains("[node name=\"ReturnHotspotLeft\" type=\"Button\" parent=\"PotionBrewingStationView\"]") &&
            scene.Contains("anchor_right = 0.18") &&
            scene.Contains("tooltip_text = \"Return to shop floor\""));
        AssertTrue("ShopFloor maps the right shelf hotspot to the potion brewing station",
            shopFloor.Contains("PotionBrewingStationButtonPath = new(\"Hotspots/InventoryShelf\")"));
        AssertTrue("ShopFloor connects the shelf hotspot to the station handler",
            shopFloor.Contains("ConnectButton(_potionBrewingStationButton, OnPotionBrewingStationPressed)"));
        AssertTrue("ShopFloor connects the station return hotspot to shop floor return",
            shopFloor.Contains("PotionBrewingStationReturnButtonPath") &&
            shopFloor.Contains("ConnectButton(_potionBrewingStationReturnButton, OnReturnToShopFloorPressed)"));
        AssertTrue("ShopFloor hides the potion brewing station view when returning",
            shopFloor.Contains("_potionBrewingStationView.Visible = false"));
    }

    private static void TestPotionBrewingStationShelfInventory()
    {
        var scene = ReadProjectFile("Scenes/UI/GameUi.tscn");
        var shelf = ReadProjectFile("Scripts/UI/StationShelfInventory.cs");

        AssertTrue("GameUi defines the station shelf inventory under the brewing station view",
            scene.Contains("[node name=\"StationShelfInventory\" type=\"Control\" parent=\"PotionBrewingStationView\"]") &&
            scene.Contains("script = ExtResource(\"31_station_shelf\")"));
        AssertTrue("Station shelf separates ingredients and consumables",
            scene.Contains("IngredientSlotsPath = NodePath(\"IngredientSlots\")") &&
            scene.Contains("ConsumableSlotsPath = NodePath(\"ConsumableSlots\")") &&
            scene.Contains("[node name=\"ConsumableSlots\" type=\"GridContainer\" parent=\"PotionBrewingStationView/StationShelfInventory\"]"));
        AssertTrue("Station shelf keeps consumables on a limited bottom shelf",
            scene.Contains("offset_top = 677.0") &&
            shelf.Contains("ConsumableDefaultVisibleSlots = 4") &&
            shelf.Contains("ConsumableVisibleSlots"));
		AssertTrue("Station shelf keeps ingredients to a limited visible slot count",
            scene.Contains("theme_override_constants/h_separation = 33") &&
            scene.Contains("theme_override_constants/v_separation = 41") &&
            shelf.Contains("IngredientDefaultVisibleSlots = 12") &&
            shelf.Contains("IngredientVisibleSlots"));
        AssertTrue("Station shelf right-click queues only ingredients through BrewPanel",
            shelf.Contains("slot.IngredientRequested += QueueIngredientFromShelf;") &&
            shelf.Contains("_itemCatalog.IsIngredient(itemId)") &&
            shelf.Contains("_brewPanel.TryQueueIngredient(itemId);"));
        AssertTrue("Station shelf pages overflow instead of showing every item",
            shelf.Contains("ShowNextIngredientPage") &&
            shelf.Contains("ShowNextConsumablePage") &&
            shelf.Contains("UpdatePageButtons"));
    }

    private static void TestBrewEntryPointsOpenPotionBrewingStation()
    {
        var shopFloor = ReadProjectFile("Scripts/UI/ShopFloor.cs");
        var hud = ReadProjectFile("Scripts/UI/Hud.cs");
        var scene = ReadProjectFile("Scenes/UI/GameUi.tscn");

        AssertTrue("ShopFloor exposes an explicit station open method",
            shopFloor.Contains("public void OpenPotionBrewingStation()") &&
            shopFloor.Contains("ShowPotionBrewingStation();"));
        AssertTrue("ShopFloor counter brew hotspot opens the station",
            shopFloor.Contains("private void OnBrewPressed()") &&
            shopFloor.Contains("OpenPotionBrewingStation();"));
        AssertTrue("ShopFloor shows the brew panel on the station without hiding queued ingredients on return",
            shopFloor.Contains("_brewPanel.ShowPanel();") &&
            shopFloor.Contains("_brewPanel.Visible = _brewWasVisible;"));
        AssertTrue("Hud brew button routes through ShopFloor when available",
            hud.Contains("ShopFloorPath = new(\"../ShopFloor\")") &&
            hud.Contains("_shopFloor.OpenPotionBrewingStation();"));
		AssertTrue("Cauldron drop target is visible and transparent on the station overlay",
			scene.Contains("[node name=\"BrewPanel\" type=\"Control\" parent=\"PotionBrewingStationView\"]") &&
			scene.Contains("BrewBoxPath = NodePath(\"BrewBox\")") &&
			scene.Contains("[node name=\"BrewBox\" type=\"PanelContainer\" parent=\"PotionBrewingStationView/BrewPanel\"]") &&
			!scene.Contains("[node name=\"BrewBox\" type=\"PanelContainer\" parent=\"PotionBrewingStationView/BrewPanel\"]\nvisible = false") &&
			scene.Contains("anchor_left = 0.38") &&
			scene.Contains("anchor_right = 0.58") &&
			scene.Contains("self_modulate = Color(1, 1, 1, 0)") &&
			scene.Contains("script = ExtResource(\"10_odm4o\")"));
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
