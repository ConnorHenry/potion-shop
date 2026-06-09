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
        runner.Run("Potion brewing station links to bedroom view", TestPotionBrewingStationLinksBedroom);
        runner.Run("Potion brewing station owns diegetic shelf inventory", TestPotionBrewingStationShelfInventory);
        runner.Run("Potion brewing station owns separate potion inventory row", TestPotionBrewingStationPotionInventoryRow);
        runner.Run("Brew entry points open the potion brewing station", TestBrewEntryPointsOpenPotionBrewingStation);
        runner.Run("Scenario debugger can set the shop stop timer", TestScenarioDebuggerStopTimerControls);
        runner.Run("Scenario debugger can toggle book records", TestScenarioDebuggerBookRecordingControls);
        runner.Run("Persistent HUD owns global HUD visibility", TestPersistentHudOwnsGlobalHudVisibility);
        runner.Run("HUD map navigation is wired", TestHudMapNavigation);
        runner.Run("Hud return-to-menu does not auto-save", TestHudReturnToMainMenuDoesNotAutoSave);
        runner.Run("Hud settings panel closes on outside click", TestHudSettingsPanelClosesOnOutsideClick);
        runner.Run("Hud ambient rain settings are wired", TestHudAmbientRainSettingsAreWired);
        runner.Run("Hud active request alert is wired", TestHudActiveRequestAlertIsWired);
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
            ["OccultShop.UI.PotionInventoryRow"] = "Control",
            ["OccultShop.UI.Garden"] = "Control",
            ["OccultShop.UI.Map"] = "Control",
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
        var scenePaths = ReadProjectFile("Scripts/Infrastructure/ScenePaths.cs");

        AssertTrue("MainMenu has load button path", source.Contains("LoadButtonPath"));
        AssertTrue("MainMenu has new game button path", source.Contains("NewGameButtonPath"));
        AssertTrue("MainMenu continues the latest save", source.Contains("LoadLatestGameIfExists()"));
        AssertTrue("MainMenu falls back to a new game when no save exists", source.Contains("StartNewGame();"));
        AssertTrue("MainMenu hides continue until saves exist", source.Contains("Visible = _saveGameManager.HasSavedGames()"));
        AssertTrue("MainMenu opens load browser", source.Contains("ScenePaths.LoadGameMenu") && scenePaths.Contains("res://Scenes/UI/LoadGameMenu.tscn"));
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
        var scenePaths = ReadProjectFile("Scripts/Infrastructure/ScenePaths.cs");

        AssertTrue("LoadGameMenu reads save summaries", source.Contains("GetSavedGames()"));
        AssertTrue("LoadGameMenu loads selected save", source.Contains("LoadGame(save.FilePath)"));
        AssertTrue("LoadGameMenu deletes selected save", source.Contains("DeleteSaveGame(save.FilePath)"));
        AssertTrue("LoadGameMenu returns to main menu", source.Contains("ScenePaths.MainMenu") && scenePaths.Contains("res://MainMenu.tscn"));
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
            scene.Contains("path=\"res://Assets/ConceptArt/BrewingStation/brewing_station_background_clean.png\""));
        AssertTrue("GameUi defines the potion brewing station view",
            scene.Contains("[node name=\"PotionBrewingStationView\" type=\"Control\" parent=\".\"]"));
        AssertTrue("GameUi draws the potion brewing station image",
            scene.Contains("[node name=\"Background\" type=\"TextureRect\" parent=\"PotionBrewingStationView\"]") &&
            scene.Contains("texture = ExtResource(\"10_2eyk8\")"));
        AssertTrue("GameUi defines a compact visible return tab on the potion brewing station view",
            scene.Contains("[node name=\"ReturnHotspotLeft\" type=\"Button\" parent=\"PotionBrewingStationView\"]") &&
            scene.Contains("custom_minimum_size = Vector2(132, 52)") &&
            scene.Contains("theme_override_styles/normal = SubResource(\"StyleBoxFlat_navigation_hotspot_normal\")") &&
            scene.Contains("tooltip_text = \"Return to shop floor\"") &&
            scene.Contains("text = \"< Shop\""));
        AssertTrue("GameUi defines a compact visible shop-front tab to the potion brewing station",
            scene.Contains("[node name=\"InventoryShelf\" type=\"Button\" parent=\"ShopFloor/Hotspots\"]") &&
            scene.Contains("custom_minimum_size = Vector2(192, 52)") &&
            scene.Contains("tooltip_text = \"Potion brewing station\"") &&
            scene.Contains("text = \"Brewing Station >\""));
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

    private static void TestPotionBrewingStationLinksBedroom()
    {
        var scene = ReadProjectFile("Scenes/UI/GameUi.tscn");
        var shopFloor = ReadProjectFile("Scripts/UI/ShopFloor.cs");

        AssertTrue("GameUi references the bedroom concept art",
            scene.Contains("path=\"res://Assets/ConceptArt/Bedroom/rural_irish_bedroom_concept.png\""));
        AssertTrue("GameUi defines the bedroom view with its background",
            scene.Contains("[node name=\"BedroomView\" type=\"Control\" parent=\".\"]") &&
            scene.Contains("[node name=\"Background\" type=\"TextureRect\" parent=\"BedroomView\"]") &&
            scene.Contains("texture = ExtResource(\"42_bedroom\")"));
        AssertTrue("GameUi defines a compact visible bedroom tab on the brewing station view",
            scene.Contains("[node name=\"BedroomHotspotRight\" type=\"Button\" parent=\"PotionBrewingStationView\"]") &&
            scene.Contains("custom_minimum_size = Vector2(140, 52)") &&
            scene.Contains("anchor_left = 1.0") &&
            scene.Contains("anchor_right = 1.0") &&
            scene.Contains("tooltip_text = \"Bedroom\"") &&
            scene.Contains("text = \"Bedroom >\""));
        AssertTrue("GameUi defines a compact visible return tab on the bedroom view",
            scene.Contains("[node name=\"ReturnHotspotLeft\" type=\"Button\" parent=\"BedroomView\"]") &&
            scene.Contains("custom_minimum_size = Vector2(188, 52)") &&
            scene.Contains("tooltip_text = \"Return to potion brewing station\"") &&
            scene.Contains("text = \"< Brewing Station\""));
        AssertTrue("GameUi defines a compact visible bed interaction for ending the day",
            scene.Contains("[node name=\"EndDayHotspot\" type=\"Button\" parent=\"BedroomView\"]") &&
            scene.Contains("custom_minimum_size = Vector2(112, 48)") &&
            scene.Contains("anchor_left = 0.56") &&
            scene.Contains("anchor_top = 0.66") &&
            scene.Contains("anchor_right = 0.56") &&
            scene.Contains("anchor_bottom = 0.66") &&
            scene.Contains("tooltip_text = \"End day\"") &&
            scene.Contains("text = \"Sleep\""));
        AssertTrue("ShopFloor exposes bedroom navigation paths",
            shopFloor.Contains("BedroomButtonPath = new(\"../PotionBrewingStationView/BedroomHotspotRight\")") &&
            shopFloor.Contains("BedroomViewPath = new(\"../BedroomView\")") &&
            shopFloor.Contains("BedroomReturnButtonPath = new(\"../BedroomView/ReturnHotspotLeft\")") &&
            shopFloor.Contains("BedroomEndDayButtonPath = new(\"../BedroomView/EndDayHotspot\")"));
        AssertTrue("ShopFloor connects bedroom navigation buttons",
            shopFloor.Contains("ConnectButton(_bedroomButton, OnBedroomPressed)") &&
            shopFloor.Contains("ConnectButton(_bedroomReturnButton, OnReturnFromBedroomPressed)") &&
            shopFloor.Contains("ConnectButton(_bedroomEndDayButton, OnBedroomEndDayPressed)"));
        AssertTrue("ShopFloor swaps bedroom and potion station views",
            shopFloor.Contains("private void ShowBedroom()") &&
            shopFloor.Contains("_potionBrewingStationView.Visible = false;") &&
            shopFloor.Contains("_bedroomView.Visible = true;") &&
            shopFloor.Contains("private void ReturnFromBedroomToPotionBrewingStation()") &&
            shopFloor.Contains("_bedroomView.Visible = false;") &&
            shopFloor.Contains("_potionBrewingStationView.Visible = true;"));
        AssertTrue("ShopFloor keeps the bed end-day hotspot usable when the day controller is available",
            shopFloor.Contains("ShopStateChanged += UpdateBedroomEndDayHotspotState") &&
            shopFloor.Contains("_bedroomEndDayButton.Disabled = _dayController is null") &&
            !shopFloor.Contains("if (_dayController.IsShopOpen)"));
        AssertTrue("ShopFloor routes the bedroom bed hotspot through day end behavior",
            shopFloor.Contains("private void OnBedroomEndDayPressed()") &&
            shopFloor.Contains("_dayController.EndDayAndRunNight();"));
        AssertTrue("DaySummaryPanel comes to the front when the bed ends an active shop day",
            ReadProjectFile("Scripts/UI/DaySummaryPanel.cs").Contains("MoveToFront();"));
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
            scene.Contains("theme_override_constants/h_separation = 23") &&
            scene.Contains("theme_override_constants/v_separation = 11") &&
            shelf.Contains("IngredientDefaultVisibleSlots = 12") &&
            shelf.Contains("IngredientVisibleSlots"));
        AssertTrue("Station shelf right-click queues only ingredients through BrewPanel",
            shelf.Contains("slot.IngredientRequested += QueueIngredientFromShelf;") &&
            shelf.Contains("_itemCatalog.IsIngredient(itemId)") &&
            shelf.Contains("_brewPanel.TryQueueIngredient(itemId);"));
        AssertTrue("Station shelf left-click opens the shared inventory item detail panel",
            shelf.Contains("InventoryPanelPath = new(\"../../InventoryPanel\")") &&
            shelf.Contains("slot.SlotActivated += ShowItemDetail;") &&
            shelf.Contains("_inventoryPanel?.OpenItemDetail(itemId);"));
        AssertTrue("Station shelf slots keep button hover and pressed states visually neutral",
            shelf.Contains("var normalStyle = CreateSlotStyleBox") &&
            shelf.Contains("slot.AddThemeStyleboxOverride(\"hover\", normalStyle);") &&
            shelf.Contains("slot.AddThemeStyleboxOverride(\"pressed\", normalStyle);"));
        AssertTrue("Station shelf slots show a separate hover-only highlight overlay",
            shelf.Contains("var hoverOutline = new PanelContainer") &&
            shelf.Contains("hoverOutline.AddThemeStyleboxOverride(\"panel\", CreateHoverOutlineStyleBox());") &&
            shelf.Contains("slot.SetHoverOutline(hoverOutline);") &&
            shelf.Contains("slot.AddChild(hoverOutline);"));
        AssertTrue("Station shelf pages overflow instead of showing every item",
            shelf.Contains("ShowNextIngredientPage") &&
            shelf.Contains("ShowNextConsumablePage") &&
            shelf.Contains("UpdatePageButtons"));
    }

    private static void TestPotionBrewingStationPotionInventoryRow()
    {
        var scene = ReadProjectFile("Scenes/UI/GameUi.tscn");
        var row = ReadProjectFile("Scripts/UI/PotionInventoryRow.cs");
        var inventoryPanel = ReadProjectFile("Scripts/UI/InventoryPanel.cs");

        AssertTrue("GameUi defines a separate potion inventory row under the brewing station view",
            scene.Contains("[node name=\"PotionInventoryRow\" type=\"Control\" parent=\"PotionBrewingStationView\"]") &&
            scene.Contains("script = ExtResource(\"43_potion_row\")") &&
            !scene.Contains("[node name=\"PotionInventoryRow\" type=\"Control\" parent=\"PotionBrewingStationView/StationShelfInventory\"]"));
		AssertTrue("Potion row owns exactly one four-column slot grid",
			scene.Contains("PotionSlotsPath = NodePath(\"PotionSlots\")") &&
			scene.Contains("[node name=\"PotionSlots\" type=\"GridContainer\" parent=\"PotionBrewingStationView/PotionInventoryRow\"]") &&
			scene.Contains("columns = 4"));
		AssertTrue("Potion row resolves the root inventory panel from the brewing station view",
			!scene.Contains("InventoryPanelPath = NodePath(\"\")") &&
			row.Contains("InventoryPanelPath = new(\"../../InventoryPanel\")"));
		AssertTrue("Potion row renders only current potion stacks from inventory",
			row.Contains("foreach (var stack in _gameState.Inventory)") &&
			row.Contains("if (!IsPotion(item))") &&
            row.Contains("if (stacks.Count >= VisiblePotionSlots)") &&
            row.Contains("GameState.MaxUniquePotionInventoryQuantity") &&
            !row.Contains("OrderBy("));
        AssertTrue("Potion row left-click opens the inventory item detail panel",
            row.Contains("slot.SlotActivated += ShowPotionDetail;") &&
            row.Contains("_inventoryPanel.OpenItemDetail(itemId);") &&
            inventoryPanel.Contains("public void OpenItemDetail(string itemId)") &&
            inventoryPanel.Contains("ShowItemDetail(itemId);"));
        AssertTrue("Potion row keeps potion slot previews concise",
            row.Contains("UiIconLoader.LoadIcon(stack.IconPath)") &&
            row.Contains("stack.Quantity.ToString()") &&
            row.Contains("DisplayName(stack.Key, item.Name)") &&
            !row.Contains("GetItemPrice(stack.Key, item)") &&
            !row.Contains("TryGetPotionBasePrice(itemId, out var potionBasePrice)") &&
            !row.Contains("InventoryItemTextFormatter.BuildSlotTraitText(item)") &&
            !row.Contains("CreateSlotTraitTag") &&
            row.Contains("HasActiveRisk(item)") &&
            row.Contains("new Color(0.9f, 0.25f, 0.25f, 1.0f)"));
    }

    private static void TestBrewEntryPointsOpenPotionBrewingStation()
    {
        var shopFloor = ReadProjectFile("Scripts/UI/ShopFloor.cs");
        var hud = ReadProjectFile("Scripts/UI/Hud.cs");
        var hudScene = ReadProjectFile("Scenes/UI/Hud.tscn");
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
        AssertTrue("HUD no longer exposes a brew button",
            !hud.Contains("BrewPanelPath") &&
            !hud.Contains("ShopFloorPath") &&
            !hudScene.Contains("[node name=\"BrewPotion\"") &&
            !hudScene.Contains("text = \"Brew Potion\""));
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
        AssertTrue("Scenario debugger exposes shop timer pause controls",
            runtimeDebug.Contains("Pause Shop Timer") &&
            runtimeDebug.Contains("Resume Shop Timer"));
        AssertTrue("Scenario debugger applies the stop timer through DayController",
            runtimeDebug.Contains("TrySetShopTimerSecondsRemaining"));
        AssertTrue("Scenario debugger applies timer pause through DayController",
            runtimeDebug.Contains("TrySetDebugShopTimerPaused"));
        AssertTrue("DayController exposes a debug timer setter",
            dayController.Contains("public bool TrySetShopTimerSecondsRemaining(int secondsRemaining)"));
        AssertTrue("DayController exposes a debug timer pause toggle",
            dayController.Contains("public bool TrySetDebugShopTimerPaused(bool paused)") &&
            dayController.Contains("public bool IsShopTimerDebugPaused"));
        AssertTrue("DayController can force the stop timer to zero through the shared setter",
            dayController.Contains("ForceShopTimerToZeroForTutorial()") && dayController.Contains("TrySetShopTimerSecondsRemaining(0)"));
    }

    private static void TestScenarioDebuggerBookRecordingControls()
    {
        var runtimeDebug = ReadProjectFile("Scripts/Debug/RuntimeDebugImGui.cs");

        AssertTrue("Scenario debugger exposes book recording controls",
            runtimeDebug.Contains("Book Recording") &&
            runtimeDebug.Contains("Recorded in potion book") &&
            runtimeDebug.Contains("Recorded in ingredient book"));
        AssertTrue("Scenario debugger records and forgets potion book entries through GameState",
            runtimeDebug.Contains("LearnPotion(potionItemId)") &&
            runtimeDebug.Contains("ForgetPotion(recipeId)") &&
            runtimeDebug.Contains("ForgetPotion(potionItemId)"));
        AssertTrue("Scenario debugger records and forgets ingredient book entries through GameState",
            runtimeDebug.Contains("LearnIngredient(ingredientId)") &&
            runtimeDebug.Contains("ForgetIngredient(ingredientId)"));
        AssertTrue("Scenario debugger lists authored book entries",
            runtimeDebug.Contains("_dataDb.PotionRecipes") &&
            runtimeDebug.Contains("IsBookIngredient(item)"));
    }

    private static void TestPersistentHudOwnsGlobalHudVisibility()
    {
        var project = ReadProjectFile("project.godot");
        var autoload = ReadProjectFile("Scripts/Autoload/PersistentHud.cs");
        var visibility = ReadProjectFile("Scripts/UI/PersistentHudVisibility.cs");
        var hud = ReadProjectFile("Scripts/UI/Hud.cs");
        var shopFloor = ReadProjectFile("Scripts/UI/ShopFloor.cs");
        var main = ReadProjectFile("Main.tscn");
        var gameUi = ReadProjectFile("Scenes/UI/GameUi.tscn");
        var hudScene = ReadProjectFile("Scenes/UI/Hud.tscn");
        var gardenScene = ReadProjectFile("Scenes/Main/Garden.tscn");
        var mainMenu = ReadProjectFile("MainMenu.tscn");
        var loadMenu = ReadProjectFile("Scenes/UI/LoadGameMenu.tscn");

        AssertTrue("Project autoloads the persistent HUD", project.Contains("PersistentHud=\"*res://Scripts/Autoload/PersistentHud.cs\""));
        AssertTrue("PersistentHud loads the HUD scene once", autoload.Contains("res://Scenes/UI/Hud.tscn") && autoload.Contains("InstantiateOrNull<Hud>()"));
        AssertTrue("PersistentHud renders above drag previews", autoload.Contains("PersistentHudLayer = 2048") && main.Contains("layer = 1025"));
        AssertTrue("PersistentHud refreshes on root scene additions", autoload.Contains("NodeAdded += OnNodeAdded") && autoload.Contains("node == tree.CurrentScene || node.GetParent() == tree.Root"));
        AssertTrue("Scenes can opt out of the persistent HUD", visibility.Contains("public bool HudVisible") && autoload.Contains("FindVisibilityOverride"));
        AssertTrue("Hud resolves scene-local controls from the active scene", hud.Contains("public void RefreshSceneBindings()") && hud.Contains("GetTree().CurrentScene"));
        AssertTrue("Main tutorial controller points at the persistent HUD", main.Contains("HudPath = NodePath(\"/root/PersistentHud/Hud\")"));
        AssertTrue("GameUi no longer owns a local HUD instance", !gameUi.Contains("[node name=\"Hud\" parent=\".\""));
        AssertTrue("Main menu hides the persistent HUD", mainMenu.Contains("[node name=\"PersistentHudVisibility\" type=\"Node\" parent=\".\"]") && mainMenu.Contains("HudVisible = false"));
        AssertTrue("Load game menu hides the persistent HUD", loadMenu.Contains("[node name=\"PersistentHudVisibility\" type=\"Node\" parent=\".\"]") && loadMenu.Contains("HudVisible = false"));
        AssertTrue("ShopFloor no longer hides HUD for close-up views", !shopFloor.Contains("_hud.Visible = false") && !shopFloor.Contains("HudPath"));
        AssertTrue("HUD is a full-width black top bar capped at 50px",
            hudScene.Contains("custom_minimum_size = Vector2(0, 50)") &&
            hudScene.Contains("offset_bottom = 50.0") &&
            hudScene.Contains("[node name=\"Background\" type=\"ColorRect\" parent=\".\"]") &&
            hudScene.Contains("color = Color(0, 0, 0, 1)"));
        AssertTrue("HUD omits dread from the top bar",
            !hud.Contains("DreadLabelPath") &&
            !hudScene.Contains("[node name=\"Dread\"") &&
            !hudScene.Contains("text = \"Dread:\""));
        AssertTrue("Gameplay scenes reserve the HUD height",
            gameUi.Contains("[node name=\"ShopFloor\" type=\"Control\" parent=\".\"]") &&
            gameUi.Contains("offset_top = 50.0") &&
            gameUi.Contains("clip_contents = true") &&
            gardenScene.Contains("offset_top = 50.0") &&
            gardenScene.Contains("clip_contents = true"));
    }

    private static void TestHudMapNavigation()
    {
        var hud = ReadProjectFile("Scripts/UI/Hud.cs");
        var hudScene = ReadProjectFile("Scenes/UI/Hud.tscn");
        var map = ReadProjectFile("Scripts/UI/Map.cs");
        var mapScene = ReadProjectFile("Scenes/Main/Map.tscn");
        var scenePaths = ReadProjectFile("Scripts/Infrastructure/ScenePaths.cs");
        var gardenButtonIndex = hudScene.IndexOf("[node name=\"Garden\" type=\"Button\" parent=\"Content/Actions\"]", StringComparison.Ordinal);
        var mapButtonIndex = hudScene.IndexOf("[node name=\"Map\" type=\"Button\" parent=\"Content/Actions\"]", StringComparison.Ordinal);
        var menuButtonIndex = hudScene.IndexOf("[node name=\"MainMenu\" type=\"Button\" parent=\"Content/Actions\"]", StringComparison.Ordinal);

        AssertTrue("ScenePaths exposes the map scene",
            scenePaths.Contains("public const string Map") &&
            scenePaths.Contains("res://Scenes/Main/Map.tscn"));
        AssertTrue("HUD scene places Map between Garden and Menu",
            gardenButtonIndex >= 0 &&
            mapButtonIndex > gardenButtonIndex &&
            menuButtonIndex > mapButtonIndex &&
            hudScene.Contains("text = \"Map\""));
        AssertTrue("Hud resolves and connects the map button",
            hud.Contains("MapButtonPath = new(\"Content/Actions/Map\")") &&
            hud.Contains("_mapButton = GetNode<Button>(MapButtonPath)") &&
            hud.Contains("_mapButton.Pressed += OnMapPressed") &&
            hud.Contains("_mapButton.Pressed -= OnMapPressed"));
        AssertTrue("Hud opens the map scene and auto-saves first",
            hud.Contains("private void OnMapPressed()") &&
            hud.Contains("TryAutoSave(\"entering the map\")") &&
            hud.Contains("GetTree().ChangeSceneToFile(ScenePaths.Map)"));
        AssertTrue("Hud keeps the map button usable while the shop is open",
            hud.Contains("_mapButton.Disabled = GetTree().CurrentScene is Map;") &&
            !hud.Contains("_mapButton.Disabled = isShopOpen"));

        AssertTrue("Map script returns to main scene",
            map.Contains("public partial class Map : Control") &&
            map.Contains("ScenePaths.Main"));
        AssertTrue("Map script auto-saves on entry and exit",
            map.Contains("TryAutoSave(\"entering the map\")") &&
            map.Contains("TryAutoSave(\"leaving the map\")"));
        AssertTrue("Map scene uses the map script and wires the back button",
            mapScene.Contains("path=\"res://Scripts/UI/Map.cs\"") &&
            mapScene.Contains("BackButtonPath = NodePath(\"Root/Margin/Main/Header/Back\")") &&
            mapScene.Contains("[node name=\"Back\" type=\"Button\" parent=\"Root/Margin/Main/Header\"]"));
        AssertTrue("Map scene reserves the persistent HUD height",
            mapScene.Contains("offset_top = 50.0") &&
            mapScene.Contains("clip_contents = true"));
    }

    private static void TestHudReturnToMainMenuDoesNotAutoSave()
    {
        var source = ReadProjectFile("Scripts/UI/Hud.cs");
        var scenePaths = ReadProjectFile("Scripts/Infrastructure/ScenePaths.cs");

        AssertTrue("Hud return-to-menu handler exists", source.Contains("OnReturnToMainMenuPressed"));
        AssertTrue("Hud return-to-menu still changes scenes", source.Contains("ScenePaths.MainMenu") && scenePaths.Contains("res://MainMenu.tscn"));
        AssertTrue("Hud return-to-menu no longer auto-saves", !source.Contains("Could not save before returning to main menu"));
    }

    private static void TestHudSettingsPanelClosesOnOutsideClick()
    {
        var source = ReadProjectFile("Scripts/UI/Hud.cs");
        var scene = ReadProjectFile("Scenes/UI/GameUi.tscn");

        AssertTrue("Hud processes raw input for outside clicks", source.Contains("SetProcessInput(true);"));
        AssertTrue("Hud checks clicks against visible settings panel bounds", source.Contains("IsPointInsideVisibleControl(_settingsPanel, mouseButton.GlobalPosition)") && source.Contains("control.GetGlobalRect().HasPoint(point)"));
        AssertTrue("Hud closes settings on outside clicks", source.Contains("SetSettingsPanelVisible(false);"));
        AssertTrue("Hud consumes outside clicks so underlying UI does not steal them", source.Contains("AcceptEvent();"));
        AssertTrue("Hud keeps the settings panel on a dedicated z layer", source.Contains("SettingsPanelZIndex"));
        AssertTrue("Hud brings the settings panel to the front when it opens", source.Contains("_settingsPanel.MoveToFront();"));
        AssertTrue("Hud still toggles settings from the gear button", source.Contains("var shouldOpen = !_settingsPanel.Visible;") && source.Contains("SetSettingsPanelVisible(shouldOpen);"));
        AssertTrue("GameUi scene no longer adds a separate settings backdrop", !scene.Contains("SettingsBackdrop"));
    }

    private static void TestHudAmbientRainSettingsAreWired()
    {
        var source = ReadProjectFile("Scripts/UI/Hud.cs");
        var scene = ReadProjectFile("Scenes/UI/Hud.tscn");
        var persistentHud = ReadProjectFile("Scripts/Autoload/PersistentHud.cs");
        var audioPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "Assets",
            "Audio",
            "rain-sounds.mp3"));

        AssertTrue("Rain audio asset is in the project", File.Exists(audioPath));
        AssertTrue("Hud gear menu exposes Settings below Save Game",
            scene.Contains("[node name=\"SaveGame\" type=\"Button\" parent=\"SettingsPanel/Margin/VBox\"]") &&
            scene.Contains("[node name=\"OpenSettings\" type=\"Button\" parent=\"SettingsPanel/Margin/VBox\"]") &&
            scene.Contains("text = \"Settings\""));
        AssertTrue("Hud defines the Settings panel",
            scene.Contains("[node name=\"Settings\" type=\"PanelContainer\" parent=\".\"]"));
        AssertTrue("Settings panel exposes the ambient sounds toggle",
            scene.Contains("[node name=\"AmbientSounds\" type=\"CheckBox\" parent=\"Settings/Margin/VBox\"]") &&
            scene.Contains("text = \"ambient sounds\""));
        AssertTrue("Settings panel exposes the rainfall volume slider",
            scene.Contains("[node name=\"RainfallVolume\" type=\"HSlider\" parent=\"Settings/Margin/VBox/RainfallVolumeRow\"]") &&
            scene.Contains("max_value = 1.0") &&
            scene.Contains("step = 0.01"));
        AssertTrue("Hud owns an ambient rain player",
            scene.Contains("[node name=\"AmbientRainPlayer\" type=\"AudioStreamPlayer\" parent=\".\"]"));
        AssertTrue("Hud loads and persists ambient rain settings",
            source.Contains("res://Assets/Audio/rain-sounds.mp3") &&
            source.Contains("user://settings.cfg") &&
            source.Contains("ConfigFile") &&
            source.Contains("ambient_sounds_enabled") &&
            source.Contains("rainfall_volume"));
        AssertTrue("Hud loops rainfall using the player finished signal",
            source.Contains("_ambientRainPlayer.Finished += OnAmbientRainFinished") &&
            source.Contains("private void OnAmbientRainFinished()") &&
            source.Contains("_ambientRainPlayer.Play();"));
        AssertTrue("Persistent HUD starts and stops ambient playback with HUD visibility",
            persistentHud.Contains("SetAmbientPlaybackAllowed(shouldShowHud)") &&
            persistentHud.Contains("SetAmbientPlaybackAllowed(false)"));
    }

    private static void TestHudActiveRequestAlertIsWired()
    {
        var source = ReadProjectFile("Scripts/UI/Hud.cs");
        var scene = ReadProjectFile("Scenes/UI/Hud.tscn");
        var customerPanel = ReadProjectFile("Scripts/UI/CustomerPanel.cs");
        var formatter = ReadProjectFile("Scripts/UI/CustomerDialogueTextFormatter.cs");

        AssertTrue("Hud scene places the request alert beside the shop timer",
            scene.Contains("[node name=\"ShopTimer\" type=\"Label\" parent=\"Content/Status\"]") &&
            scene.Contains("[node name=\"RequestAlert\" type=\"Button\" parent=\"Content/Status\"]") &&
            scene.Contains("text = \"!\"") &&
            scene.Contains("theme_override_colors/font_color = Color(1, 0.86, 0.05, 1)"));
        AssertTrue("Hud scene defines a request popup under the alert",
            scene.Contains("[node name=\"RequestPanel\" type=\"PanelContainer\" parent=\".\"]") &&
            scene.Contains("custom_minimum_size = Vector2(340, 0)") &&
            scene.Contains("[node name=\"Description\" type=\"RichTextLabel\" parent=\"RequestPanel/Margin/VBox\"]") &&
            scene.Contains("[node name=\"DesiredTraits\" type=\"RichTextLabel\" parent=\"RequestPanel/Margin/VBox/Traits/DesiredColumn\"]") &&
            scene.Contains("[node name=\"BadTraits\" type=\"RichTextLabel\" parent=\"RequestPanel/Margin/VBox/Traits/BadColumn\"]"));
        AssertTrue("Hud drives the request alert from active customer request state",
            source.Contains("ActiveCustomerRequest") &&
            source.Contains("_requestAlertButton.Visible = true") &&
            source.Contains("_requestAlertButton.Visible = false") &&
            source.Contains("SetRequestPanelVisible(false);"));
        AssertTrue("Hud toggles the request panel from the alert button",
            source.Contains("OnRequestAlertPressed") &&
            source.Contains("SetRequestPanelVisible(!_requestPanel.Visible);"));
        AssertTrue("Hud sizes the request popup to its required content",
            source.Contains("ResizeAndPositionRequestPanelUnderAlert") &&
            source.Contains("_requestPanel.GetCombinedMinimumSize().Y") &&
            source.Contains("_requestPanel.Size = new Vector2(panelWidth, panelHeight);"));
        AssertTrue("Hud closes the request panel on outside click and scene refresh",
            source.Contains("IsPointInsideVisibleControl(_requestPanel, mouseButton.GlobalPosition)") &&
            source.Contains("IsPointInsideVisibleControl(_requestAlertButton, mouseButton.GlobalPosition)") &&
            source.Contains("SetRequestPanelVisible(false);") &&
            source.Contains("public void RefreshSceneBindings()"));
        AssertTrue("Hud and CustomerPanel share request detail formatting",
            source.Contains("CustomerDialogueTextFormatter.BuildDesiredRequestText") &&
            source.Contains("CustomerDialogueTextFormatter.BuildBadRequestText") &&
            customerPanel.Contains("CustomerDialogueTextFormatter.BuildDesiredRequestText") &&
            formatter.Contains("public static string BuildDesiredRequestText") &&
            formatter.Contains("public static string BuildBadRequestText"));
    }
}
