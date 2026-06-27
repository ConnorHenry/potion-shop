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
        runner.Run("Main menu collects player name before new game", TestMainMenuPlayerNameFlow);
        runner.Run("Intro cutscene runs after confirmed new game", TestIntroCutsceneNewGameFlow);
        runner.Run("Ten years later cutscene routes to juniper gathering", TestTenYearsLaterCutsceneFlow);
        runner.Run("Woman in green cutscene runs after story juniper gathering", TestWomanInGreenCutsceneFlow);
        runner.Run("Load menu scene is wired for saved game browsing", TestLoadGameMenuScene);
        runner.Run("Dialogue test scene exercises branching story state", TestDialogueTestScene);
        runner.Run("Game UI omits the removed inventory panel", TestGameUiOmitsInventoryPanel);
        runner.Run("Station customer panel uses authored customer image paths", TestCustomerCloseupUsesSplitArt);
        runner.Run("Potion brewing station is the primary game view", TestShopFloorShelfOpensPotionBrewingStation);
        runner.Run("Potion brewing station omits bedroom view", TestPotionBrewingStationOmitsBedroom);
        runner.Run("Potion brewing station owns diegetic shelf inventory", TestPotionBrewingStationShelfInventory);
        runner.Run("Potion brewing station owns separate potion inventory row", TestPotionBrewingStationPotionInventoryRow);
        runner.Run("Inventory slot layout editor plugin is wired", TestInventorySlotLayoutEditorPlugin);
        runner.Run("Brew entry points open the potion brewing station", TestBrewEntryPointsOpenPotionBrewingStation);
        runner.Run("Scenario debugger can close the active shop day", TestScenarioDebuggerShopDayControls);
        runner.Run("Scenario debugger can toggle book records", TestScenarioDebuggerBookRecordingControls);
        runner.Run("Scenario debugger fills base ingredient stacks only", TestScenarioDebuggerBaseIngredientFill);
        runner.Run("Persistent HUD owns global HUD visibility", TestPersistentHudOwnsGlobalHudVisibility);
        runner.Run("HUD map navigation is wired", TestHudMapNavigation);
        runner.Run("HUD calendar is wired", TestHudCalendarIsWired);
        runner.Run("Map scene builds coordinate grid and modal outcomes", TestMapSceneCoordinateGridAndModalOutcomes);
        runner.Run("F12 forest gathering scene is wired", TestF12ForestGatheringScene);
        runner.Run("K17 juniper gathering scene is wired", TestK17JuniperGatheringScene);
        runner.Run("Hud return-to-menu does not auto-save", TestHudReturnToMainMenuDoesNotAutoSave);
        runner.Run("Hud settings panel closes on outside click", TestHudSettingsPanelClosesOnOutsideClick);
        runner.Run("Hud ambient rain settings are wired", TestHudAmbientRainSettingsAreWired);
        runner.Run("Hud day counter replaces request alert", TestHudDayCounterReplacesRequestAlert);
    }

    private static void TestUiClassPresenceAndBaseTypes()
    {
        var expectedClasses = new Dictionary<string, string>
        {
            ["OccultShop.UI.BrewPanel"] = "Control",
            ["OccultShop.UI.BrewDropBox"] = "PanelContainer",
            ["OccultShop.UI.CustomerSellDropBox"] = "PanelContainer",
            ["OccultShop.UI.StationBookController"] = "Control",
            ["OccultShop.UI.StationCustomerPanel"] = "Control",
            ["OccultShop.UI.DraggablePanel"] = "PanelContainer",
            ["OccultShop.UI.Hud"] = "Control",
            ["OccultShop.UI.CalendarPanel"] = "PanelContainer",
            ["OccultShop.UI.LoadGameMenu"] = "Control",
            ["OccultShop.UI.DialogueTestScene"] = "Control",
            ["OccultShop.UI.InventoryItemSlot"] = "Button",
            ["OccultShop.UI.StationShelfInventory"] = "Control",
            ["OccultShop.UI.StationItemDetailPanel"] = "Control",
            ["OccultShop.UI.PotionInventoryRow"] = "Control",
            ["OccultShop.UI.Garden"] = "Control",
            ["OccultShop.UI.Map"] = "Control",
            ["OccultShop.UI.ForestGathering"] = "Control",
            ["OccultShop.UI.JuniperGathering"] = "Control",
            ["OccultShop.UI.IntroCutscene"] = "Control",
            ["OccultShop.UI.TenYearsLaterCutscene"] = "Control",
            ["OccultShop.UI.WomanInGreenCutscene"] = "Control",
            ["OccultShop.Autoload.SceneTransition"] = "CanvasLayer",
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
        AssertTrue("MainMenu has exit-to-desktop button path", source.Contains("ExitToDesktopButtonPath"));
        AssertTrue("MainMenu continues the latest save", source.Contains("LoadLatestGameIfExists()"));
        AssertTrue("MainMenu falls back to a new game when no save exists", source.Contains("StartNewGame();"));
        AssertTrue("MainMenu hides continue until saves exist", source.Contains("Visible = _saveGameManager.HasSavedGames()"));
        AssertTrue("MainMenu opens load browser", source.Contains("ScenePaths.LoadGameMenu") && scenePaths.Contains("res://Scenes/UI/LoadGameMenu.tscn"));
        AssertTrue("MainMenu exits to desktop", source.Contains("GetTree().Quit();"));
        AssertTrue("MainMenu scene has load button", scene.Contains("LoadButton"));
        AssertTrue("MainMenu scene has new game button", scene.Contains("NewGameButton"));
        AssertTrue("MainMenu scene has exit-to-desktop button", scene.Contains("ExitToDesktopButton"));
        AssertTrue("MainMenu scene labels the new game button", scene.Contains("text = \"New Game\""));
        AssertTrue("MainMenu scene labels the continue button", scene.Contains("text = \"Continue\""));
        AssertTrue("MainMenu scene labels the load button", scene.Contains("text = \"Load Game\""));
        AssertTrue("MainMenu scene labels the exit-to-desktop button", scene.Contains("text = \"Exit to desktop\""));
    }

    private static void TestLoadGameMenuScene()
    {
        var source = ReadProjectFile("Scripts/UI/LoadGameMenu.cs");
        var scene = ReadProjectFile("Scenes/UI/LoadGameMenu.tscn");
        var scenePaths = ReadProjectFile("Scripts/Infrastructure/ScenePaths.cs");
        var saveSummary = ReadProjectFile("Scripts/Persistence/SaveGameSummary.cs");

        AssertTrue("LoadGameMenu reads save summaries", source.Contains("GetSavedGames()"));
        AssertTrue("LoadGameMenu loads selected save", source.Contains("LoadGame(save.FilePath)"));
        AssertTrue("LoadGameMenu deletes selected save", source.Contains("DeleteSaveGame(save.FilePath)"));
        AssertTrue("LoadGameMenu returns to main menu", source.Contains("ScenePaths.MainMenu") && scenePaths.Contains("res://MainMenu.tscn"));
        AssertTrue("LoadGameMenu exposes a delete button", source.Contains("Text = \"Delete\""));
        AssertTrue("LoadGameMenu save rows use display text with player name",
            source.Contains("save.BuildDisplayText()") &&
            saveSummary.Contains("PlayerName") &&
            saveSummary.Contains("Unnamed Player"));
        AssertTrue("LoadGameMenu scene exposes a save list", scene.Contains("SaveList"));
        AssertTrue("LoadGameMenu scene exposes empty state", scene.Contains("No saved games found."));
        AssertTrue("LoadGameMenu scene exposes back button", scene.Contains("BackButton"));
    }

    private static void TestMainMenuPlayerNameFlow()
    {
        var source = ReadProjectFile("Scripts/UI/MainMenu.cs");
        var scene = ReadProjectFile("MainMenu.tscn");

        AssertTrue("MainMenu exposes player name popup node paths",
            source.Contains("NewGameNamePopupPath") &&
            source.Contains("PlayerNameInputPath") &&
            source.Contains("PlayerNamePreviewLabelPath") &&
            source.Contains("KeyboardRowsPath") &&
            source.Contains("NameConfirmPopupPath"));
        AssertTrue("MainMenu scene contains player name popup controls",
            scene.Contains("[node name=\"NewGameNamePopup\"") &&
            scene.Contains("[node name=\"NameInput\" type=\"LineEdit\"") &&
            scene.Contains("[node name=\"Preview\" type=\"Label\"") &&
            scene.Contains("[node name=\"KeyboardRows\" type=\"VBoxContainer\"") &&
            scene.Contains("[node name=\"NameConfirmPopup\""));
        AssertTrue("MainMenu tutorial choices open name entry before starting",
            source.Contains("ShowPlayerNamePrompt(startTutorial: true)") &&
            source.Contains("ShowPlayerNamePrompt(startTutorial: false)") &&
            source.Contains("_saveGameManager.StartNewGame(startTutorial, playerName);"));
        AssertTrue("MainMenu accepts physical keyboard typing and Enter submission",
            source.Contains("_playerNameInput.TextChanged += OnPlayerNameChanged;") &&
            source.Contains("_playerNameInput.TextSubmitted += OnPlayerNameSubmitted;") &&
            source.Contains("TryShowNameConfirmation();"));
        AssertTrue("MainMenu builds QWERTY on-screen keyboard",
            source.Contains("\"QWERTYUIOP\"") &&
            source.Contains("\"ASDFGHJKL\"") &&
            source.Contains("\"ZXCVBNM\"") &&
            source.Contains("AddKeyboardButton(commandRow, \"Space\"") &&
            source.Contains("AddKeyboardButton(commandRow, \"Backspace\"") &&
            source.Contains("AddKeyboardButton(commandRow, \"Clear\"") &&
            source.Contains("AddKeyboardButton(commandRow, \"Confirm\""));
        AssertTrue("MainMenu updates live name preview",
            source.Contains("UpdatePlayerNamePreview") &&
            source.Contains("_playerNamePreviewLabel.Text") &&
            source.Contains("Your name: {normalizedName}"));
        AssertTrue("MainMenu validates player names conservatively",
            source.Contains("PlayerNameMaxLength = 20") &&
            source.Contains("TryValidatePlayerName") &&
            source.Contains("char.IsLetterOrDigit(character)") &&
            source.Contains("character == ' '") &&
            source.Contains("character == '-'") &&
            source.Contains("apostrophes"));
        AssertTrue("MainMenu asks for final name confirmation",
            source.Contains("NameConfirmMessageLabelPath") &&
            source.Contains("Start game as") &&
            source.Contains("OnNameConfirmAccepted"));
        AssertTrue("MainMenu scene line edit limits names to 20 characters",
            scene.Contains("max_length = 20"));
    }

    private static void TestIntroCutsceneNewGameFlow()
    {
        var mainMenu = ReadProjectFile("Scripts/UI/MainMenu.cs");
        var project = ReadProjectFile("project.godot");
        var autoloadNodePaths = ReadProjectFile("Scripts/Autoload/AutoloadNodePaths.cs");
        var scenePaths = ReadProjectFile("Scripts/Infrastructure/ScenePaths.cs");
        var introSource = ReadProjectFile("Scripts/UI/IntroCutscene.cs");
        var introScene = ReadProjectFile("Scenes/UI/IntroCutscene.tscn");
        var transitionSource = ReadProjectFile("Scripts/Autoload/SceneTransition.cs");

        var startNewGameIndex = mainMenu.IndexOf("private void StartNewGame(bool startTutorial, string playerName)", StringComparison.Ordinal);
        var startNewGameBlock = startNewGameIndex >= 0
            ? mainMenu.Substring(startNewGameIndex, Math.Min(520, mainMenu.Length - startNewGameIndex))
            : string.Empty;
        var continueGameIndex = mainMenu.IndexOf("private void ContinueGame()", StringComparison.Ordinal);
        var continueGameBlock = continueGameIndex >= 0
            ? mainMenu.Substring(continueGameIndex, Math.Min(420, mainMenu.Length - continueGameIndex))
            : string.Empty;

        AssertTrue("ScenePaths exposes the intro cutscene scene",
            scenePaths.Contains("IntroCutscene") &&
            scenePaths.Contains("res://Scenes/UI/IntroCutscene.tscn"));
        AssertTrue("Confirmed new games route through the intro cutscene after saving tutorial choice and name",
            startNewGameBlock.Contains("_saveGameManager.StartNewGame(startTutorial, playerName);") &&
            startNewGameBlock.Contains("ScenePaths.IntroCutscene") &&
            !startNewGameBlock.Contains("ScenePaths.Main"));
        AssertTrue("Continue/load fallback still enters the main scene directly",
            continueGameBlock.Contains("LoadLatestGameIfExists()") &&
            continueGameBlock.Contains("StartNewGame();") &&
            continueGameBlock.Contains("ScenePaths.Main"));
        AssertTrue("Intro cutscene scene hides the persistent HUD",
            introScene.Contains("res://Scripts/UI/PersistentHudVisibility.cs") &&
            introScene.Contains("HudVisible = false"));
        AssertTrue("Intro cutscene scene exposes text, options, and rain player nodes",
            introScene.Contains("res://Scripts/UI/IntroCutscene.cs") &&
            introScene.Contains("[node name=\"Conversation\" type=\"RichTextLabel\"") &&
            introScene.Contains("[node name=\"Options\" type=\"VBoxContainer\"") &&
            introScene.Contains("[node name=\"OptionOne\" type=\"Button\"") &&
            introScene.Contains("[node name=\"OptionTwo\" type=\"Button\"") &&
            introScene.Contains("[node name=\"RainPlayer\" type=\"AudioStreamPlayer\""));
        AssertTrue("Intro cutscene uses the reusable dialogue and animated text runtime",
            introSource.Contains("NarrativeTextPresenter") &&
            introSource.Contains("DialogueSession") &&
            introSource.Contains("DialogueNarrativeLineBuilder.BuildNarrativeLines") &&
            introSource.Contains("AdvanceQueuedPresentation()"));
        AssertTrue("Intro cutscene contains the requested opening text and player choices",
            introSource.Contains("Hey, hey wake up {playerName}.") &&
            introSource.Contains("Your mother stands hunched over beside your bed, lit by only a candle. That and the sound of rain are the only senses you take in.") &&
            introSource.Contains("Is everything okay?") &&
            introSource.Contains("The sun isn't even up yet."));
        AssertTrue("Intro cutscene contains both requested mother responses",
            introSource.Contains("Yes, worry not, everything's fine. I just need your help with something.") &&
            introSource.Contains("I know dear but I just need your help with something."));
        AssertTrue("Intro cutscene records the selected player option before mother responds",
            introSource.Contains("QueuePlayerLine(option.Label);") &&
            introSource.Contains("QueueDialogueLines(option.ResponseLines, option.ResponseText, MotherSpeakerName);"));
        AssertTrue("Intro rain plays locally and always starts during the scene",
            introSource.Contains("res://Assets/Audio/rain-sounds.mp3") &&
            introSource.Contains("_rainPlayer.Stream = stream;") &&
            introSource.Contains("_rainPlayer.Play();") &&
            !introSource.Contains("ambient_sounds_enabled") &&
            !introSource.Contains("SetAmbientPlaybackAllowed"));
        AssertTrue("Intro cutscene waits for a final click before entering the main scene",
            introSource.Contains("AwaitingFinalClick") &&
            introSource.Contains("TransitionToMainScene();") &&
            introSource.Contains("ChangeSceneWithFade(ScenePaths.Main)") &&
            !introSource.Contains("GetTree().ChangeSceneToFile(ScenePaths.Main)"));
        AssertTrue("Scene transition autoload is wired for cross-scene fades",
            project.Contains("SceneTransition=\"*res://Scripts/Autoload/SceneTransition.cs\"") &&
            autoloadNodePaths.Contains("SceneTransition = \"/root/SceneTransition\"") &&
            introSource.Contains("SceneTransitionPath = new(AutoloadNodePaths.SceneTransition)") &&
            introSource.Contains("NodeLookup.TryGetRequiredNode<SceneTransition>"));
        AssertTrue("Scene transition fades out, changes scene, then fades in",
            transitionSource.Contains("TweenProperty(_fadeOverlay, \"modulate:a\"") &&
            transitionSource.Contains("OnFadeOutFinished") &&
            transitionSource.Contains("GetTree().ChangeSceneToFile(_pendingScenePath)") &&
            transitionSource.Contains("StartFade(HiddenAlpha, FinishTransition)") &&
            transitionSource.Contains("TransitionLayer = 8192"));
    }

    private static void TestDialogueTestScene()
    {
        var source = ReadProjectFile("Scripts/UI/DialogueTestScene.cs");
        var scene = ReadProjectFile("Scenes/UI/DialogueTestScene.tscn");

        AssertTrue("Dialogue test scene uses the neutral dialogue runtime",
            source.Contains("DialogueSession") &&
            source.Contains("DialogueGraph") &&
            source.Contains("IsOptionVisible"));
        AssertTrue("Dialogue test scene models the requested story state controls",
            source.Contains("StartingStoryScore = 50") &&
            source.Contains("QuestStatus.NotStarted") &&
            source.Contains("QuestStatus.InProgress") &&
            source.Contains("TrustFlagId") &&
            source.Contains("Math.Clamp"));
        AssertTrue("Dialogue test scene keeps seen options visible",
            source.Contains("_seenOptionIds") &&
            source.Contains("[seen]") &&
            source.Contains("Seen choices stay visible"));
        AssertTrue("Dialogue test scene is wired as a standalone UI scene",
            scene.Contains("res://Scripts/UI/DialogueTestScene.cs") &&
            scene.Contains("PersistentHudVisibility") &&
            scene.Contains("HudVisible = false") &&
            scene.Contains("Dialogue Test Scene") &&
            scene.Contains("Reputation +5") &&
            scene.Contains("Quest InProgress"));
    }

    private static void TestTenYearsLaterCutsceneFlow()
    {
        var scenePaths = ReadProjectFile("Scripts/Infrastructure/ScenePaths.cs");
        var source = ReadProjectFile("Scripts/UI/TenYearsLaterCutscene.cs");
        var scene = ReadProjectFile("Scenes/UI/TenYearsLaterCutscene.tscn");
        var gameState = ReadProjectFile("Scripts/Autoload/GameState.cs");

        AssertTrue("ScenePaths exposes the ten years later cutscene scene",
            scenePaths.Contains("TenYearsLaterCutscene") &&
            scenePaths.Contains("res://Scenes/UI/TenYearsLaterCutscene.tscn"));
        AssertTrue("Ten years later cutscene scene hides the persistent HUD",
            scene.Contains("res://Scripts/UI/PersistentHudVisibility.cs") &&
            scene.Contains("HudVisible = false"));
        AssertTrue("Ten years later cutscene scene exposes title, dialogue, and two choices",
            scene.Contains("res://Scripts/UI/TenYearsLaterCutscene.cs") &&
            scene.Contains("[node name=\"Title\" type=\"Label\"") &&
            scene.Contains("text = \"10 Years Later\"") &&
            scene.Contains("[node name=\"Conversation\" type=\"RichTextLabel\"") &&
            scene.Contains("[node name=\"Options\" type=\"VBoxContainer\"") &&
            scene.Contains("[node name=\"OptionOne\" type=\"Button\"") &&
            scene.Contains("[node name=\"OptionTwo\" type=\"Button\""));
        AssertTrue("Ten years later cutscene uses dark-screen dialogue runtime",
            source.Contains("NarrativeTextPresenter") &&
            source.Contains("DialogueSession") &&
            source.Contains("DialogueNarrativeLineBuilder.BuildNarrativeLines") &&
            source.Contains("TweenProperty(_title, \"modulate:a\"") &&
            scene.Contains("Color(0.0, 0.0, 0.0, 1)"));
        AssertTrue("Ten years later cutscene contains the requested text and choices",
            source.Contains("10 Years Later") &&
            source.Contains("Mother is brewing in the kitchen.") &&
            source.Contains("Come on {playerName}, we need to go juniper picking") &&
            source.Contains("Really?? Fun!") &&
            source.Contains("You've never let me come juniper picking before?"));
        AssertTrue("Ten years later cutscene records the selected player option without a mother response",
            source.Contains("QueuePlayerLine(option.Label);") &&
            source.Contains("PlayQueuedDialogueLines(TransitionToJuniperGathering)") &&
            !source.Contains("QueueDialogueLines(option.ResponseLines"));
        AssertTrue("Ten years later cutscene auto-saves at start and completion",
            source.Contains("RecordTenYearsLaterCutsceneStarted();") &&
            source.Contains("TryAutoSave(\"starting the ten years later cutscene\")") &&
            source.Contains("RecordTenYearsLaterCutsceneCompleted();") &&
            source.Contains("TryAutoSave(\"completing the ten years later cutscene\")") &&
            gameState.Contains("TenYearsLaterCutsceneStartedStoryFlag") &&
            gameState.Contains("TenYearsLaterCutsceneCompletedStoryFlag"));
        AssertTrue("Ten years later cutscene fades to juniper gathering",
            source.Contains("SceneTransitionPath = new(AutoloadNodePaths.SceneTransition)") &&
            source.Contains("ChangeSceneWithFade(ScenePaths.JuniperGathering)") &&
            !source.Contains("GetTree().ChangeSceneToFile(ScenePaths.JuniperGathering)"));
    }

    private static void TestWomanInGreenCutsceneFlow()
    {
        var scenePaths = ReadProjectFile("Scripts/Infrastructure/ScenePaths.cs");
        var source = ReadProjectFile("Scripts/UI/WomanInGreenCutscene.cs");
        var scene = ReadProjectFile("Scenes/UI/WomanInGreenCutscene.tscn");
        var gameState = ReadProjectFile("Scripts/Autoload/GameState.cs");
        var juniper = ReadProjectFile("Scripts/UI/JuniperGathering.cs");

        AssertTrue("ScenePaths exposes the woman in green cutscene scene",
            scenePaths.Contains("WomanInGreenCutscene") &&
            scenePaths.Contains("res://Scenes/UI/WomanInGreenCutscene.tscn"));
        AssertTrue("Woman in green cutscene scene hides the persistent HUD",
            scene.Contains("res://Scripts/UI/PersistentHudVisibility.cs") &&
            scene.Contains("HudVisible = false"));
        AssertTrue("Woman in green cutscene scene exposes a dark narrative surface",
            scene.Contains("res://Scripts/UI/WomanInGreenCutscene.cs") &&
            (scene.Contains("Color(0.0, 0.0, 0.0, 1)") || scene.Contains("Color(0, 0, 0, 1)")) &&
            scene.Contains("[node name=\"Conversation\" type=\"RichTextLabel\""));
        AssertTrue("Woman in green cutscene contains the requested river text",
            source.Contains("After picking the juniper berries, you both walk home.") &&
            source.Contains("By the river, you saw the woman in green.") &&
            source.Contains("washing a pale coat against the stones") &&
            source.Contains("Your mother's fingers closed around your wrist.") &&
            source.Contains("\\\"Do not speak to her.\\\"") &&
            source.Contains("You had never heard fear in your mother's voice before.") &&
            source.Contains("neither of you spoke until the shop lamps were lit again."));
        AssertTrue("Woman in green cutscene auto-saves at start and completion",
            source.Contains("RecordWomanInGreenCutsceneStarted();") &&
            source.Contains("TryAutoSave(\"starting the woman in green cutscene\")") &&
            source.Contains("RecordWomanInGreenCutsceneCompleted();") &&
            source.Contains("TryAutoSave(\"completing the woman in green cutscene\")") &&
            gameState.Contains("WomanInGreenCutsceneStartedStoryFlag") &&
            gameState.Contains("WomanInGreenCutsceneCompletedStoryFlag"));
        AssertTrue("Woman in green cutscene fades back to Main",
            source.Contains("SceneTransitionPath = new(AutoloadNodePaths.SceneTransition)") &&
            source.Contains("ChangeSceneWithFade(ScenePaths.Main)") &&
            !source.Contains("GetTree().ChangeSceneToFile(ScenePaths.Main)"));
        AssertTrue("Juniper gathering only routes story completion into the woman in green cutscene once",
            juniper.Contains("ShouldShowWomanInGreenCutscene()") &&
            juniper.Contains("TenYearsLaterCutsceneCompletedStoryFlag") &&
            juniper.Contains("WomanInGreenCutsceneStartedStoryFlag") &&
            juniper.Contains("WomanInGreenCutsceneCompletedStoryFlag") &&
            juniper.Contains("ChangeSceneWithFade(ScenePaths.WomanInGreenCutscene)") &&
            juniper.Contains("GetTree().ChangeSceneToFile(ScenePaths.Main)"));
    }

    private static void TestGameUiOmitsInventoryPanel()
    {
        var scene = ReadProjectFile("Scenes/UI/GameUi.tscn");
        var main = ReadProjectFile("Main.tscn");

        AssertTrue("GameUi no longer instances InventoryPanel", !scene.Contains("InventoryPanel"));
        AssertTrue("Main no longer exports InventoryPanel paths", !main.Contains("InventoryPanelPath"));
    }

    private static void TestCustomerCloseupUsesSplitArt()
    {
        var scene = ReadProjectFile("Scenes/UI/GameUi.tscn");
        var stationCustomerPanel = ReadProjectFile("Scripts/UI/StationCustomerPanel.cs");
        var tieredCustomers = ReadProjectFile("Data/customers_tiered_test_data.tres");

        AssertTrue("GameUi no longer defines the removed customer closeup surface",
            !scene.Contains("CustomerCloseupView") &&
            !scene.Contains("res://art/Customer-Closeup.png") &&
            !scene.Contains("res://art/Background - Evening.png"));
        AssertTrue("StationCustomerPanel builds an inline customer image frame",
            stationCustomerPanel.Contains("Name = \"CustomerImageFrame\"") &&
            stationCustomerPanel.Contains("Name = \"CustomerImage\""));
        var requestTextIndex = stationCustomerPanel.IndexOf("Name = \"RequestText\"", StringComparison.Ordinal);
        var requestTextBlock = requestTextIndex >= 0
            ? stationCustomerPanel.Substring(requestTextIndex, Math.Min(260, stationCustomerPanel.Length - requestTextIndex))
            : string.Empty;
        AssertTrue("StationCustomerPanel gives customer dialogue extra vertical room",
            scene.Contains("anchor_bottom = 0.825") &&
            requestTextBlock.Contains("SizeFlagsVertical = SizeFlags.ExpandFill"));
        AssertTrue("StationCustomerPanel loads customer portrait textures from customer data",
            stationCustomerPanel.Contains("RefreshCustomerImage(interaction)") &&
            stationCustomerPanel.Contains("RefreshCustomerImage(interaction, line.CharacterImageKey)") &&
            stationCustomerPanel.Contains("ResourceLoader.Load<Texture2D>(imagePath)"));
        AssertTrue("Tiered customer data includes Bridget's happy and sad portrait paths",
            tieredCustomers.Contains("\"characterImagePath\": \"res://Assets/Characters/old_rural_woman_sprite_happy.png\"") &&
            tieredCustomers.Contains("\"happy\": \"res://Assets/Characters/old_rural_woman_sprite_happy.png\"") &&
            tieredCustomers.Contains("\"sad\": \"res://Assets/Characters/old_rural_woman_sprite_sad.png\""));
    }

    private static void TestShopFloorShelfOpensPotionBrewingStation()
    {
        var scene = ReadProjectFile("Scenes/UI/GameUi.tscn");
        var main = ReadProjectFile("Main.tscn");
        var stationBookController = ReadProjectFile("Scripts/UI/StationBookController.cs");

        AssertTrue("GameUi references the potion brewing station art",
            scene.Contains("path=\"res://Assets/Art/BrewingStationBright/brewing_station_background2.png\""));
        AssertTrue("GameUi defines the potion brewing station view",
            scene.Contains("[node name=\"PotionBrewingStationView\" type=\"Control\" parent=\".\"]") &&
            scene.Contains("script = ExtResource(\"54_station_book\")"));
        AssertTrue("GameUi draws the potion brewing station image",
            scene.Contains("[node name=\"Background\" type=\"TextureRect\" parent=\"PotionBrewingStationView\"]") &&
            scene.Contains("texture = ExtResource(\"9_jopkj\")"));
        AssertTrue("GameUi removes the old shop-front and closeup surfaces",
            !scene.Contains("[node name=\"ShopFloor\"") &&
            !scene.Contains("CustomerCloseupView") &&
            !scene.Contains("PotionBookCloseupView") &&
            !scene.Contains("[node name=\"CustomerPanel\"") &&
            !scene.Contains("[node name=\"EventModal\""));
        AssertTrue("Station book controls open both book panels from the brewing station",
            scene.Contains("[node name=\"Book\" type=\"TextureRect\" parent=\"PotionBrewingStationView\"]") &&
            scene.Contains("[node name=\"BookHotspot\" type=\"Button\" parent=\"PotionBrewingStationView/Book\"]") &&
            !scene.Contains("[node name=\"BookSwitch\" type=\"Button\" parent=\"PotionBrewingStationView/Book\"]") &&
            scene.Contains("[node name=\"BookOverlayLayer\" type=\"CanvasLayer\" parent=\".\"]") &&
            scene.Contains("layer = 4096") &&
            scene.Contains("[node name=\"BookDismissOverlay\" type=\"Control\" parent=\"BookOverlayLayer\"]") &&
            scene.Contains("[node name=\"PotionBookPanel\" parent=\"BookOverlayLayer\" instance=ExtResource(\"18_potion_book\")]") &&
            scene.Contains("[node name=\"IngredientBookPanel\" parent=\"BookOverlayLayer\" instance=ExtResource(\"41_ingredient_book\")]") &&
            stationBookController.Contains("BookButtonPath = new(\"Book/BookHotspot\")") &&
            stationBookController.Contains("BookDismissOverlayPath = new(\"../BookOverlayLayer/BookDismissOverlay\")") &&
            stationBookController.Contains("OnBookDismissOverlayGuiInput") &&
            stationBookController.Contains("HideBookPanels();") &&
            stationBookController.Contains("_bookDismissOverlay?.AcceptEvent();") &&
            stationBookController.Contains("PotionBookSwitchButtonPath = new(\"../BookOverlayLayer/PotionBookPanel/BookRow/BookPanel/BookSwitch\")") &&
            stationBookController.Contains("IngredientBookSwitchButtonPath = new(\"../BookOverlayLayer/IngredientBookPanel/BookRow/BookPanel/BookSwitch\")") &&
            stationBookController.Contains("PotionBookPanelPath = new(\"../BookOverlayLayer/PotionBookPanel\")") &&
            stationBookController.Contains("IngredientBookPanelPath = new(\"../BookOverlayLayer/IngredientBookPanel\")"));
        AssertTrue("Main scene wires shop flow directly to the station customer panel",
            main.Contains("StationCustomerPanelPath = NodePath(\"../CanvasLayer/PotionBrewingStationView/StationCustomerPanel\")") &&
            !main.Contains("\nCustomerPanelPath = NodePath("));
    }

    private static void TestPotionBrewingStationOmitsBedroom()
    {
        var scene = ReadProjectFile("Scenes/UI/GameUi.tscn");

        AssertTrue("GameUi no longer references bedroom art or nodes",
            !scene.Contains("rural_irish_bedroom") &&
            !scene.Contains("BedroomView") &&
            !scene.Contains("BedroomHotspotRight") &&
            !scene.Contains("text = \"Bedroom >\"") &&
            !scene.Contains("text = \"Sleep\""));
        AssertTrue("GameUi no longer references shop-floor navigation nodes",
            !scene.Contains("ShopFloor") &&
            !scene.Contains("ReturnHotspot") &&
            !scene.Contains("text = \"< Shop\""));
        AssertTrue("DaySummaryPanel still comes to the front when shown",
            ReadProjectFile("Scripts/UI/DaySummaryPanel.cs").Contains("MoveToFront();"));
    }

    private static void TestPotionBrewingStationShelfInventory()
    {
        var scene = ReadProjectFile("Scenes/UI/GameUi.tscn");
        var shelf = ReadProjectFile("Scripts/UI/StationShelfInventory.cs");
        var jarredSlot = ReadProjectFile("Scripts/UI/JarredInventorySlotView.cs");
        var slotVisuals = ReadProjectFile("Scripts/UI/InventorySlotVisuals.cs");
        var layoutSettings = ReadProjectFile("Assets/UI/InventorySlotLayoutSettings.tres");
        var itemDetailPanel = ReadProjectFile("Scripts/UI/StationItemDetailPanel.cs");
        var outsideClickBoundsCheckIndex = itemDetailPanel.IndexOf("if (!GetGlobalRect().HasPoint(mouseButton.GlobalPosition))", StringComparison.Ordinal);
        var outsideClickBranch = outsideClickBoundsCheckIndex >= 0
            ? itemDetailPanel.Substring(outsideClickBoundsCheckIndex, Math.Min(180, itemDetailPanel.Length - outsideClickBoundsCheckIndex))
            : string.Empty;

        AssertTrue("GameUi defines the station shelf inventory under the brewing station view",
            scene.Contains("[node name=\"StationShelfInventory\" type=\"Control\" parent=\"PotionBrewingStationView\"]") &&
            scene.Contains("script = ExtResource(\"31_station_shelf\")"));
        AssertTrue("Station shelf separates ingredients and consumables",
            scene.Contains("IngredientSlotsPath = NodePath(\"IngredientSlots\")") &&
            scene.Contains("ConsumableSlotsPath = NodePath(\"ConsumableSlots\")") &&
            scene.Contains("[node name=\"ConsumableSlots\" type=\"GridContainer\" parent=\"PotionBrewingStationView/StationShelfInventory\"]"));
        AssertTrue("Station shelf keeps consumables on a limited bottom shelf",
            scene.Contains("offset_top = 640.0") &&
            shelf.Contains("ConsumableDefaultVisibleSlots = 4") &&
            shelf.Contains("ConsumableVisibleSlots"));
        AssertTrue("Station shelf keeps ingredients to a limited visible slot count",
            scene.Contains("theme_override_constants/h_separation = 23") &&
            scene.Contains("theme_override_constants/v_separation = 4") &&
            shelf.Contains("IngredientDefaultVisibleSlots = 10") &&
            shelf.Contains("IngredientVisibleSlots"));
        AssertTrue("Station shelf slots use the generated jar and plaque treatment",
            shelf.Contains("JarredInventorySlotView.CreateContent") &&
            jarredSlot.Contains("res://Assets/Art/BrewingStationBright/ingredient_jar_overlay_bright.png") &&
            jarredSlot.Contains("res://Assets/Art/BrewingStationBright/ingredient_label_overlay_bright.png") &&
            jarredSlot.Contains("Name = \"Quantity\""));
        AssertTrue("Station shelf preserves prepared ingredient methods on plaque labels",
            shelf.Contains("BuildShelfDisplayName") &&
            shelf.Contains("IngredientPreparationCatalog.GetDisplayName(preparationId)") &&
            shelf.Contains("InventorySlotLayoutKind.IngredientShelf") &&
            shelf.Contains("InventorySlotLayoutKind.ConsumableShelf") &&
            layoutSettings.Contains("PreserveParentheticalSuffix = true") &&
            layoutSettings.Contains("SingleLineCharacterLimit = 18") &&
            jarredSlot.Contains("PreserveParentheticalSuffix") &&
            jarredSlot.Contains("ResolveNameFontSize"));
        AssertTrue("Station shelf exposes a trait filter below the ingredient slots",
            shelf.Contains("IngredientTraitFilterPath = new(\"IngredientTraitFilterRow/TraitFilter\")") &&
            shelf.Contains("IngredientClearFilterButtonPath = new(\"IngredientTraitFilterRow/Clear\")") &&
            scene.Contains("[node name=\"IngredientTraitFilterRow\" type=\"HBoxContainer\" parent=\"PotionBrewingStationView/StationShelfInventory\"]") &&
            scene.Contains("[node name=\"TraitFilter\" type=\"OptionButton\" parent=\"PotionBrewingStationView/StationShelfInventory/IngredientTraitFilterRow\"]") &&
            scene.Contains("[node name=\"Clear\" type=\"Button\" parent=\"PotionBrewingStationView/StationShelfInventory/IngredientTraitFilterRow\"]"));
        AssertTrue("Station shelf trait filter is populated from known ingredient book entries",
            shelf.Contains("foreach (var knownIngredientId in _gameState.KnownIngredients)") &&
            shelf.Contains("AddIngredientBookTraitNames(item, traitNames)") &&
            shelf.Contains("_gameState.KnowsIngredientPreparation(item.Id, option.Id)") &&
            shelf.Contains("preparation.Traits") &&
            shelf.Contains("ItemFilterUtilities.RefreshFilterOptions(_ingredientTraitFilter, traitNames, \"Trait\", ref _activeIngredientTraitFilter)"));
        AssertTrue("Station shelf trait filter matches shelf items only against known book preparation traits",
            !shelf.Contains("ItemFilterUtilities.ItemHasTrait(itemId, _activeIngredientTraitFilter, _itemCatalog)") &&
            shelf.Contains("TryGetKnownIngredientBookItem(itemId, out var bookItem)") &&
            shelf.Contains("ItemHasIngredientBookTrait(bookItem, _activeIngredientTraitFilter)") &&
            shelf.Contains("_gameState.KnowsIngredientPreparation(item.Id, option.Id)") &&
            shelf.Contains("_gameState.KnowsIngredient(ingredientBookItemId)"));
        AssertTrue("Station shelf clear button is visible only while a trait filter is active",
            shelf.Contains("_ingredientClearFilterButton.Visible = hasActiveFilter") &&
            shelf.Contains("_ingredientClearFilterButton.Disabled = !hasActiveFilter") &&
            shelf.Contains("_ingredientClearFilterButton.MouseFilter = hasActiveFilter ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore"));
        AssertTrue("Station shelf right-click sends raw ingredients to prep tray and prepared ingredients to BrewPanel",
            shelf.Contains("slot.IngredientRequested += QueueIngredientFromShelf;") &&
            shelf.Contains("IngredientPreparationTrayPath = new(\"../IngredientPreparationTray\")") &&
            shelf.Contains("_ingredientPreparationTray.TrySelectIngredientFromInventory(itemId)") &&
            shelf.Contains("_itemCatalog.IsPreparedIngredient(itemId)") &&
            shelf.Contains("_brewPanel.TryQueueIngredient(itemId);"));
        AssertTrue("Station shelf left-clicks open the station item detail panel",
            scene.Contains("[node name=\"StationItemDetailPanel\" type=\"Control\" parent=\"PotionBrewingStationView\"]") &&
            scene.Contains("script = ExtResource(\"44_station_item_detail\")") &&
            shelf.Contains("ItemDetailPanelPath = new(\"../StationItemDetailPanel\")") &&
            shelf.Contains("_itemDetailPanel.ShowItem(itemId)") &&
            shelf.Contains("slot.SlotActivated += ShowItemDetail;"));
        AssertTrue("Station item detail panel can be dragged without stealing close button clicks",
            itemDetailPanel.Contains("SetProcessInput(true);") &&
            itemDetailPanel.Contains("GlobalPosition = mouseMotion.GlobalPosition - _dragOffset;") &&
            itemDetailPanel.Contains("IsPressOnInteractiveChildControl()") &&
            itemDetailPanel.Contains("hoveredControl is BaseButton"));
        AssertTrue("Station item detail panel closes on outside click",
            outsideClickBranch.Contains("HidePanel();") &&
            outsideClickBranch.Contains("AcceptEvent();"));
        AssertTrue("Station item detail panel masks locked ingredient preparation stats",
            itemDetailPanel.Contains("FormatKnownPreparationTraitRows") &&
            itemDetailPanel.Contains("FormatKnownPreparationRiskRows") &&
            itemDetailPanel.Contains("_gameState.TryResolveIngredientPreparation(itemId, out var ingredientId, out var preparationId)") &&
            itemDetailPanel.Contains("UnknownPreparationStatsLabel"));
        AssertTrue("Station shelf does not resolve the removed inventory panel",
            !shelf.Contains("InventoryPanelPath") &&
            !shelf.Contains("OpenItemDetail"));
        AssertTrue("Station shelf slots keep button hover and pressed states visually neutral",
            shelf.Contains("var normalStyle = InventorySlotVisuals.CreateSlotStyleBox") &&
            shelf.Contains("slot.AddThemeStyleboxOverride(\"hover\", normalStyle);") &&
            shelf.Contains("slot.AddThemeStyleboxOverride(\"pressed\", normalStyle);"));
        AssertTrue("Station shelf slots show a separate hover-only highlight overlay",
            shelf.Contains("InventorySlotVisuals.CreateHoverOutline") &&
            slotVisuals.Contains("hoverOutline.AddThemeStyleboxOverride(\"panel\", CreateSlotStyleBox(") &&
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
        var jarredSlot = ReadProjectFile("Scripts/UI/JarredInventorySlotView.cs");
        var layoutSettings = ReadProjectFile("Assets/UI/InventorySlotLayoutSettings.tres");

        AssertTrue("GameUi defines a separate potion inventory row under the brewing station view",
            scene.Contains("[node name=\"PotionInventoryRow\" type=\"Control\" parent=\"PotionBrewingStationView\"]") &&
            scene.Contains("script = ExtResource(\"43_potion_row\")") &&
            !scene.Contains("[node name=\"PotionInventoryRow\" type=\"Control\" parent=\"PotionBrewingStationView/StationShelfInventory\"]"));
		AssertTrue("Potion row owns exactly one four-column slot grid",
			scene.Contains("PotionSlotsPath = NodePath(\"PotionSlots\")") &&
			scene.Contains("[node name=\"PotionSlots\" type=\"GridContainer\" parent=\"PotionBrewingStationView/PotionInventoryRow\"]") &&
			scene.Contains("columns = 4"));
		AssertTrue("Potion row does not resolve the removed inventory panel",
			!scene.Contains("InventoryPanelPath") &&
			!row.Contains("InventoryPanelPath"));
		AssertTrue("Potion row renders only current potion stacks from inventory",
			row.Contains("foreach (var stack in _gameState.Inventory)") &&
			row.Contains("if (!IsPotion(item))") &&
            row.Contains("if (stacks.Count >= VisiblePotionSlots)") &&
            row.Contains("GameState.MaxUniquePotionInventoryQuantity") &&
            !row.Contains("OrderBy("));
        AssertTrue("Potion row left-clicks open the station item detail panel",
            scene.Contains("[node name=\"StationItemDetailPanel\" type=\"Control\" parent=\"PotionBrewingStationView\"]") &&
            row.Contains("ItemDetailPanelPath = new(\"../StationItemDetailPanel\")") &&
            row.Contains("_itemDetailPanel.ShowItem(itemId)") &&
            row.Contains("slot.SlotActivated += ShowItemDetail;") &&
            !row.Contains("OpenItemDetail"));
        AssertTrue("Potion row keeps potion slot previews bottled and concise",
            row.Contains("JarredInventorySlotView.CreatePotionContent") &&
            row.Contains("stack.Quantity") &&
            jarredSlot.Contains("res://Assets/Art/BrewingStationBright/potion_card_overlay_bright.png") &&
            jarredSlot.Contains("PotionLiquidView") &&
            row.Contains("DisplayName(stack.Key, item.Name)") &&
            !row.Contains("GetItemPrice(stack.Key, item)") &&
            !row.Contains("TryGetPotionBasePrice(itemId, out var potionBasePrice)") &&
            !row.Contains("InventoryItemTextFormatter.BuildSlotTraitText(item)") &&
            !row.Contains("CreateSlotTraitTag") &&
            row.Contains("HasActiveRisk(item)") &&
            row.Contains("new Color(0.58f, 0.05f, 0.04f, 1.0f)"));
        AssertTrue("Potion row centers readable live text inside the generated bottle label",
            row.Contains("InventorySlotLayoutKind.PotionInventory") &&
            row.Contains("profile.CreateJarredLayout(stack.HasActiveRisk") &&
            layoutSettings.Contains("SingleLineCharacterLimit = 10") &&
            layoutSettings.Contains("GeneratedLabelRectRatio = Rect2(0.03, 0.634, 0.94, 0.34)") &&
            layoutSettings.Contains("GeneratedNameRectRatio = Rect2(0.08, 0.657, 0.84, 0.2)") &&
            layoutSettings.Contains("GeneratedQuantityRectRatio = Rect2(0.36, 0.858, 0.28, 0.17)") &&
            jarredSlot.Contains("ResolveNameHorizontalInset") &&
            jarredSlot.Contains("TextOverrunBehavior = TextServer.OverrunBehavior.NoTrimming") &&
            jarredSlot.Contains("GetStringSize(text, HorizontalAlignment.Left, -1.0f, fontSize)") &&
            jarredSlot.Contains("NameFitSafetyPadding"));
    }

    private static void TestInventorySlotLayoutEditorPlugin()
    {
        var project = ReadProjectFile("project.godot");
        var pluginCfg = ReadProjectFile("addons/inventory_slot_layout_editor/plugin.cfg");
        var plugin = ReadProjectFile("addons/inventory_slot_layout_editor/plugin.gd");
        var dockScene = ReadProjectFile("addons/inventory_slot_layout_editor/inventory_slot_layout_editor_dock.tscn");
        var dock = ReadProjectFile("addons/inventory_slot_layout_editor/inventory_slot_layout_editor_dock.gd");
        var settings = ReadProjectFile("Scripts/UI/InventorySlotLayoutSettings.cs");
        var profile = ReadProjectFile("Scripts/UI/InventorySlotLayoutProfile.cs");
        var settingsResource = ReadProjectFile("Assets/UI/InventorySlotLayoutSettings.tres");
        var shelf = ReadProjectFile("Scripts/UI/StationShelfInventory.cs");
        var row = ReadProjectFile("Scripts/UI/PotionInventoryRow.cs");
        var stationCustomerPanel = ReadProjectFile("Scripts/UI/StationCustomerPanel.cs");

        AssertTrue("Inventory slot editor plugin is enabled",
            project.Contains("res://addons/inventory_slot_layout_editor/plugin.cfg") &&
            pluginCfg.Contains("Inventory Slot Layout Editor") &&
            pluginCfg.Contains("script=\"plugin.gd\""));
        AssertTrue("Inventory slot editor plugin adds a Godot editor dock scene",
            plugin.Contains("extends EditorPlugin") &&
            plugin.Contains("add_control_to_dock(DOCK_SLOT_RIGHT_UL, _dock)") &&
            dockScene.Contains("res://addons/inventory_slot_layout_editor/inventory_slot_layout_editor_dock.gd") &&
            !dockScene.Contains("res://Scripts/Editor/InventorySlotLayoutEditorDock.cs"));
        AssertTrue("Inventory slot editor dock previews and saves the shared layout resource",
            dock.Contains("@tool") &&
            dock.Contains("ResourceSaver.save(_settings, DEFAULT_SETTINGS_PATH)") &&
            dock.Contains("_set_profile_value(def") &&
            dock.Contains("Layout edits auto-save") &&
            dock.Contains("TabContainer.new()") &&
            dock.Contains("_create_profile_page(def)") &&
            dock.Contains("_reset_profile(def)"));
        AssertTrue("Generated rect X and Y values activate custom positioning without requiring custom size",
            profile.Contains("GeneratedLabelRectRatio") &&
            profile.Contains("GeneratedNameRectRatio") &&
            profile.Contains("GeneratedQuantityRectRatio") &&
            ReadProjectFile("Scripts/UI/JarredInventorySlotView.cs").Contains("rect.Position != Vector2.Zero || rect.Size != Vector2.Zero") &&
            ReadProjectFile("Scripts/UI/JarredInventorySlotView.cs").Contains("customSize.X > 0.0f ? customSize.X : defaultRatioRect.Size.X") &&
            ReadProjectFile("Scripts/UI/JarredInventorySlotView.cs").Contains("ResolveCustomRatioRect(layout.GeneratedNameRectRatio") &&
            dock.Contains("_resolve_custom_ratio_rect") &&
            dock.Contains("custom_rect.position == Vector2.ZERO and custom_rect.size == Vector2.ZERO"));
        AssertTrue("Inventory slot layout resource exposes all active generated slot families",
            settings.Contains("DefaultResourcePath = \"res://Assets/UI/InventorySlotLayoutSettings.tres\"") &&
            settings.Contains("IngredientShelfSlot") &&
            settings.Contains("ConsumableShelfSlot") &&
            settings.Contains("PotionInventorySlot") &&
            !settings.Contains("CustomerPotionSlot") &&
            profile.Contains("CreateJarredLayout"));
        AssertTrue("Default slot layout resource stores editable profile subresources",
            settingsResource.Contains("IngredientShelfSlot = SubResource(\"Resource_ingredient_shelf\")") &&
            settingsResource.Contains("ConsumableShelfSlot = SubResource(\"Resource_consumable_shelf\")") &&
            settingsResource.Contains("PotionInventorySlot = SubResource(\"Resource_potion_inventory\")") &&
            !settingsResource.Contains("CustomerPotionSlot = SubResource(\"Resource_customer_potion\")"));
        AssertTrue("Runtime inventory slot code loads the shared layout resource",
            shelf.Contains("SlotLayoutSettingsPath = InventorySlotLayoutSettings.DefaultResourcePath") &&
            row.Contains("SlotLayoutSettingsPath = InventorySlotLayoutSettings.DefaultResourcePath") &&
            shelf.Contains("InventorySlotLayoutSettings.Load(SlotLayoutSettingsPath, forceReload)") &&
            row.Contains("InventorySlotLayoutSettings.Load(SlotLayoutSettingsPath, forceReload)") &&
            !shelf.Contains("InventorySlotLayoutSettings? SlotLayoutSettings") &&
            !row.Contains("InventorySlotLayoutSettings? SlotLayoutSettings") &&
            stationCustomerPanel.Contains("_potionInventoryRow?.RefreshSlotLayoutSettings();"));
        var gameUiScene = ReadProjectFile("Scenes/UI/GameUi.tscn");
        AssertTrue("GameUi keeps slot layout data in the shared resource instead of inline subresources",
            !gameUiScene.Contains("SlotLayoutSettings = SubResource") &&
            !gameUiScene.Contains("InventorySlotLayoutProfile.cs") &&
            settingsResource.Contains("IconSizeRatio = 0.62"));
    }

    private static void TestBrewEntryPointsOpenPotionBrewingStation()
    {
        var hud = ReadProjectFile("Scripts/UI/Hud.cs");
        var hudScene = ReadProjectFile("Scenes/UI/Hud.tscn");
        var scene = ReadProjectFile("Scenes/UI/GameUi.tscn");
        var dayController = ReadProjectFile("Scripts/Controllers/DayController.cs");

        AssertTrue("GameUi starts directly on the potion brewing station view",
            scene.Contains("[node name=\"PotionBrewingStationView\" type=\"Control\" parent=\".\"]") &&
            !scene.Contains("[node name=\"ShopFloor\""));
        AssertTrue("DayController shows and hides the station brew panel for shop days",
            dayController.Contains("_brewPanel.ShowPanel();") &&
            dayController.Contains("_brewPanel.HidePanel();"));
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
			scene.Contains("offset_left = 877.0") &&
			scene.Contains("offset_right = 1192.0") &&
			scene.Contains("self_modulate = Color(1, 1, 1, 0)") &&
			scene.Contains("script = ExtResource(\"10_odm4o\")"));
    }

    private static void TestScenarioDebuggerShopDayControls()
    {
        var runtimeDebug = ReadProjectFile("Scripts/Debug/RuntimeDebugImGui.cs");
        var dayController = ReadProjectFile("Scripts/Controllers/DayController.cs");
        var customerController = ReadProjectFile("Scripts/Controllers/CustomerEventController.cs");
        var fastForwardService = ReadProjectFile("Scripts/Systems/ShopDayFastForwardService.cs");
        var customers = ReadProjectFile("Data/customers_data.tres");
        var shelf = ReadProjectFile("Scripts/UI/StationShelfInventory.cs");
        var row = ReadProjectFile("Scripts/UI/PotionInventoryRow.cs");
        var stationCustomerPanel = ReadProjectFile("Scripts/UI/StationCustomerPanel.cs");
        var layoutSettings = ReadProjectFile("Scripts/UI/InventorySlotLayoutSettings.cs");

        AssertTrue("Scenario debugger wires the day controller",
            runtimeDebug.Contains("DayControllerPath = new(\"../DayController\")"));
        AssertTrue("Scenario debugger exposes a close-shop shortcut",
            runtimeDebug.Contains("Close Shop Now"));
        AssertTrue("Scenario debugger shows the customer arrival cap",
            runtimeDebug.Contains("CustomersArrivedToday") &&
            runtimeDebug.Contains("MaxCustomersPerDay"));
        AssertTrue("Scenario debugger closes the active shop through DayController",
            runtimeDebug.Contains("TryCloseShopDay()") &&
            runtimeDebug.Contains("TryCloseShopDayFromDebug"));
        AssertTrue("Scenario debugger exposes a target-day fast-forward control",
            runtimeDebug.Contains("Forward To Day") &&
            runtimeDebug.Contains("TryFastForwardToDay(") &&
            runtimeDebug.Contains("TryFastForwardToDayFromDebug") &&
            !runtimeDebug.Contains("_gameState.NextDay();"));
        AssertTrue("DayController runs debug day fast-forward through the progression service",
            dayController.Contains("public ShopDayFastForwardResult TryFastForwardToDayFromDebug(int targetDay)") &&
            dayController.Contains("ShopDayFastForwardService.FastForwardToDay(") &&
            dayController.Contains("_stationCustomerPanel.ClearCustomers();") &&
            dayController.Contains("_brewPanel.HidePanel();"));
        AssertTrue("Debug day fast-forward uses scheduled story customers without random customer draws",
            fastForwardService.Contains("DrawScheduledStoryCustomerInteraction(dataDb, gameState, shopSessionState)") &&
            customerController.Contains("public CustomerInteractionDef? DrawScheduledStoryCustomerInteraction") &&
            customerController.Contains("TryDrawScheduledStoryCustomerInteraction(interactions, state, shopSession, out var scheduledStoryInteraction)") &&
            dayController.Contains("MaxCustomersPerShopDay") &&
            fastForwardService.Contains("maxCustomersPerShopDay"));
        AssertTrue("Debug day fast-forward applies authored progression effects and normal overnight advancement",
            fastForwardService.Contains("EffectApplier.Apply(gameState, successEffect)") &&
            fastForwardService.Contains("EffectsMatch(successEffect, failureEffect)") &&
            fastForwardService.Contains("shopSessionState.RecordShopDaySale(success: true, goldDelta: 0, dreadDelta: 0)") &&
            fastForwardService.Contains("gameState.NextDay();"));
        AssertTrue("Authored fast-forward examples remain data-driven",
            customers.Contains("\"addItemId\": \"comfrey\"") &&
            customers.Contains("\"restockItemId\": \"mint\"") &&
            customers.Contains("\"enableIngredientPreparationMethodId\": \"boiled\""));
        AssertTrue("Scenario debugger can refresh runtime slot layout state from the editor resource",
            runtimeDebug.Contains("Refresh State") &&
            runtimeDebug.Contains("RefreshDebugState()") &&
            runtimeDebug.Contains("RefreshInventorySlotLayoutViews()") &&
            runtimeDebug.Contains("stationShelfInventory.RefreshSlotLayoutSettings()") &&
            runtimeDebug.Contains("potionInventoryRow.RefreshSlotLayoutSettings()") &&
            runtimeDebug.Contains("stationCustomerPanel.RefreshSlotLayoutSettings()") &&
            shelf.Contains("public void RefreshSlotLayoutSettings()") &&
            row.Contains("public void RefreshSlotLayoutSettings()") &&
            stationCustomerPanel.Contains("public void RefreshSlotLayoutSettings()") &&
            layoutSettings.Contains("LoadDefault(bool forceReload = false)") &&
            layoutSettings.Contains("Load(string resourcePath, bool forceReload = false)") &&
            layoutSettings.Contains("ResourceLoader.CacheMode.Ignore"));
        AssertTrue("Scenario debugger no longer exposes timer controls",
            !runtimeDebug.Contains("Stop Timer Seconds") &&
            !runtimeDebug.Contains("Pause Shop Timer") &&
            !runtimeDebug.Contains("Resume Shop Timer"));
        AssertTrue("DayController exposes a debug close-shop helper",
            dayController.Contains("public bool TryCloseShopDayFromDebug()"));
        AssertTrue("DayController no longer exposes timer setters",
            !dayController.Contains("TrySetShopTimerSecondsRemaining") &&
            !dayController.Contains("TrySetDebugShopTimerPaused") &&
            !dayController.Contains("IsShopTimerDebugPaused"));
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
        AssertTrue("Scenario debugger can unlock all authored and runtime ingredient preparation traits",
            runtimeDebug.Contains("Unlock All Ingredient Traits") &&
            runtimeDebug.Contains("items.AddRange(_dataDb.Items.Values)") &&
            runtimeDebug.Contains("items.AddRange(_runtimeContentDb.Items.Values)") &&
            runtimeDebug.Contains("_gameState.UnlockAllIngredientPreparations(items)"));
        AssertTrue("Scenario debugger toggles non-Raw preparation methods",
            runtimeDebug.Contains("Non-Raw Prep Methods") &&
            runtimeDebug.Contains("Disable Non-Raw Prep Methods") &&
            runtimeDebug.Contains("Enable Non-Raw Prep Methods") &&
            runtimeDebug.Contains("AreNonRawIngredientPreparationMethodsEnabled()") &&
            runtimeDebug.Contains("SetNonRawIngredientPreparationMethodsEnabled(!nonRawPreparationsEnabled)"));
        AssertTrue("Scenario debugger can skip the boiling mini game",
            runtimeDebug.Contains("Skip Boiling Mini Game") &&
            runtimeDebug.Contains("DebugSkipBoilingMiniGame") &&
            runtimeDebug.Contains("SetDebugSkipBoilingMiniGame(skipBoilingMiniGame)"));
        AssertTrue("Scenario debugger lists authored book entries",
            runtimeDebug.Contains("_dataDb.PotionRecipes") &&
            runtimeDebug.Contains("IsBookIngredient(item)"));
    }

    private static void TestScenarioDebuggerBaseIngredientFill()
    {
        var runtimeDebug = ReadProjectFile("Scripts/Debug/RuntimeDebugImGui.cs");

        AssertTrue("Scenario debugger snapshots ingredient ids before runtime catalog changes",
            runtimeDebug.Contains("new List<string>(_ingredientItemIds)"));
        AssertTrue("Scenario debugger filters bulk adds to base ingredients",
            runtimeDebug.Contains("IsBaseIngredient(item)") &&
            runtimeDebug.Contains("item.PreparedIngredient is not null"));
        AssertTrue("Scenario debugger adds only the base ingredient item id",
            runtimeDebug.Contains("TryAddInventoryStack(item.Id, quantity)") &&
            !runtimeDebug.Contains("TryAddInventoryStack(preparedIngredient.Id, quantity)") &&
            !runtimeDebug.Contains("AddPreparedIngredientStacks"));
    }

    private static void TestPersistentHudOwnsGlobalHudVisibility()
    {
        var project = ReadProjectFile("project.godot");
        var autoload = ReadProjectFile("Scripts/Autoload/PersistentHud.cs");
        var visibility = ReadProjectFile("Scripts/UI/PersistentHudVisibility.cs");
        var hud = ReadProjectFile("Scripts/UI/Hud.cs");
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
        AssertTrue("PersistentHud does not stop audio during transient scene changes",
            autoload.Contains("if (currentScene is null)") &&
            autoload.Contains("return;") &&
            autoload.IndexOf("if (currentScene is null)", StringComparison.Ordinal) < autoload.IndexOf("SetAmbientPlaybackAllowed(shouldShowHud)", StringComparison.Ordinal));
        AssertTrue("GameUi no longer includes close-up views that hide the HUD",
            !gameUi.Contains("CustomerCloseupView") &&
            !gameUi.Contains("PotionBookCloseupView") &&
            !gameUi.Contains("ShopFloor"));
        AssertTrue("HUD is a full-width warm top bar capped at 50px",
            hudScene.Contains("custom_minimum_size = Vector2(0, 50)") &&
            hudScene.Contains("offset_bottom = 50.0") &&
            hudScene.Contains("[node name=\"Background\" type=\"ColorRect\" parent=\".\"]") &&
            hudScene.Contains("color = Color(0.125, 0.076, 0.035, 1)"));
        AssertTrue("HUD omits dread from the top bar",
            !hud.Contains("DreadLabelPath") &&
            !hudScene.Contains("[node name=\"Dread\"") &&
            !hudScene.Contains("text = \"Dread:\""));
        AssertTrue("Gameplay scenes reserve the HUD height",
            gameUi.Contains("[node name=\"PotionBrewingStationView\" type=\"Control\" parent=\".\"]") &&
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
        var hudNavigationService = ReadProjectFile("Scripts/UI/HudNavigationService.cs");
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
            hud.Contains("HudNavigationService.TryOpenMap(this)") &&
            hudNavigationService.Contains("ScenePaths.Map"));
        AssertTrue("Hud keeps the map button usable while the shop is open",
            hud.Contains("_mapButton.Disabled = navigationBlocked || GetTree().CurrentScene is Map;") &&
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

    private static void TestMapSceneCoordinateGridAndModalOutcomes()
    {
        var map = ReadProjectFile("Scripts/UI/Map.cs");
        var mapScene = ReadProjectFile("Scenes/Main/Map.tscn");
        var mapAssetPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "Assets",
            "Maps",
            "kerry_parchment_map_hires.png"));

        AssertTrue("Map uses a wide 30 column coordinate grid from A to Q",
            map.Contains("ColumnCount = 30") &&
            map.Contains("FirstRowLetter = 'A'") &&
            map.Contains("LastRowLetter = 'Q'"));
        AssertTrue("Map uses the new generated Kerry parchment texture instead of the old image asset",
            map.Contains("MapTexturePath = \"res://Assets/Maps/kerry_parchment_map_hires.png\"") &&
            map.Contains("LoadMapTexture") &&
            map.Contains("ResourceLoader.Load<Texture2D>(MapTexturePath)") &&
            File.Exists(mapAssetPath) &&
            !map.Contains("kerry_samuel_lewis_1844_lowres.jpg"));
        AssertTrue("Map fits coordinate cells to the artwork while keeping labels outside as headers",
            map.Contains("LayoutMapLayersToCanvas") &&
            map.Contains("TryCalculateMapLayoutRects") &&
            map.Contains("GridColumnSlotCount") &&
            map.Contains("GridRowSlotCount") &&
            map.Contains("_mapArtwork.Texture.GetSize()") &&
            map.Contains("var headerSize = new Vector2(artworkSize.X / ColumnCount, artworkSize.Y / RowCount)") &&
            map.Contains("SetControlRect(_mapArtwork, artworkRect)") &&
            map.Contains("SetControlRect(_mapGrid, gridRect)") &&
            map.Contains("SetControlRect(_gridLineOverlay, gridRect)") &&
            map.Contains("SetControlRect(sepiaTint, artworkRect)"));
        AssertTrue("Map scene wires the artwork, floating coordinate label, coordinate grid, and modal nodes",
            mapScene.Contains("HoveredCoordinateLabelPath = NodePath(\"HoveredCoordinate\")") &&
            mapScene.Contains("[node name=\"HoveredCoordinate\" type=\"Label\" parent=\".\"]") &&
            mapScene.Contains("MapArtworkPath = NodePath(\"Root/Margin/Main/MapArea/MapMargin/MapCenter/MapCanvas/MapArtwork\")") &&
            mapScene.Contains("[node name=\"MapArtwork\" type=\"TextureRect\" parent=\"Root/Margin/Main/MapArea/MapMargin/MapCenter/MapCanvas\"]") &&
            mapScene.Contains("MapGridPath = NodePath(\"Root/Margin/Main/MapArea/MapMargin/MapCenter/MapCanvas/MapGrid\")") &&
            mapScene.Contains("[node name=\"MapGrid\" type=\"GridContainer\"") &&
            mapScene.Contains("columns = 31") &&
            mapScene.Contains("ModalLayerPath = NodePath(\"ModalLayer\")") &&
            mapScene.Contains("ModalDialogPath = NodePath(\"ModalLayer/Dialog\")") &&
            mapScene.Contains("ModalTilePreviewPath = NodePath(\"ModalLayer/Dialog/Margin/Content/TilePreview\")") &&
            mapScene.Contains("[node name=\"TilePreview\" type=\"TextureRect\" parent=\"ModalLayer/Dialog/Margin/Content\"]") &&
            mapScene.Contains("[node name=\"Travel\" type=\"Button\" parent=\"ModalLayer/Dialog/Margin/Content/Text/VBox/Actions\"]"));
        AssertTrue("Map modal is a quarter-screen preview with text to the right of the tile image",
            mapScene.Contains("anchor_left = 0.25") &&
            mapScene.Contains("anchor_top = 0.25") &&
            mapScene.Contains("anchor_right = 0.75") &&
            mapScene.Contains("anchor_bottom = 0.75") &&
            !mapScene.Contains("scale = Vector2(0.29, 0.29)") &&
            mapScene.Contains("[node name=\"Content\" type=\"HBoxContainer\" parent=\"ModalLayer/Dialog/Margin\"]") &&
            mapScene.Contains("[node name=\"Text\" type=\"MarginContainer\" parent=\"ModalLayer/Dialog/Margin/Content\"]") &&
            mapScene.Contains("visible = false") &&
            mapScene.Contains("custom_minimum_size = Vector2(280, 0)") &&
            mapScene.Contains("stretch_mode = 5"));
        AssertTrue("Map scene lets the canvas fill the MapArea panel with padding around the screen edges",
            mapScene.Contains("[node name=\"MapCenter\" type=\"Control\" parent=\"Root/Margin/Main/MapArea/MapMargin\"]") &&
            mapScene.Contains("[node name=\"MapCanvas\" type=\"Control\" parent=\"Root/Margin/Main/MapArea/MapMargin/MapCenter\"]") &&
            mapScene.Contains("anchors_preset = 15") &&
            mapScene.Contains("size_flags_horizontal = 3") &&
            mapScene.Contains("theme_override_constants/margin_left = 18") &&
            mapScene.Contains("theme_override_constants/margin_right = 18"));
        AssertTrue("Map keeps F12 as the first point of interest with an assignable destination",
            map.Contains("DefaultPointOfInterestCoordinate = \"F12\"") &&
            map.Contains("F12ScenePath") &&
            map.Contains("ChangeSceneToFile(_pendingTravelScenePath)"));
        AssertTrue("Map empty coordinates show the requested modal message without travel",
            map.Contains("Nothing of interest here") &&
            map.Contains("HideModalTilePreview();") &&
            map.Contains("UseCompactModalLayout();") &&
            map.Contains("CompactModalHalfWidth = 190.0f") &&
            map.Contains("CompactModalHalfHeight = 94.0f") &&
            map.Contains("CompactModalMessageMinimumHeight = 58.0f") &&
            map.Contains("_modalDialog.AnchorLeft = 0.5f") &&
            map.Contains("_modalDialog.OffsetLeft = -CompactModalHalfWidth") &&
            map.Contains("_modalTilePreview.Visible = false") &&
            map.Contains("_modalTravelButton.Visible = false"));
        AssertTrue("Map modal shows point-of-interest previews without requiring images for every cell",
            map.Contains("F12PreviewTexturePath") &&
            map.Contains("K17PreviewTexturePath") &&
            map.Contains("ShowPointOfInterest(coordinate, pointOfInterest)") &&
            map.Contains("SetModalTilePreview(coordinate, pointOfInterest)") &&
            map.Contains("UsePreviewModalLayout();") &&
            map.Contains("PreviewModalMessageMinimumHeight = 200.0f") &&
            map.Contains("_modalDialog.AnchorLeft = 0.25f") &&
            map.Contains("_modalDialog.AnchorRight = 0.75f") &&
            map.Contains("PreviewTexturePath") &&
            map.Contains("ResourceLoader.Load<Texture2D>(previewTexturePath)") &&
            map.Contains("SetModalTilePreviewFromMapCrop(coordinate)") &&
            map.Contains("CalculateTileSourceRegion") &&
            map.Contains("new AtlasTexture") &&
            map.Contains("Atlas = _mapArtwork.Texture") &&
            map.Contains("Region = tileRegion") &&
            map.Contains("_modalTilePreview.Visible = true") &&
            map.Contains("coordinate.Column - 1") &&
            map.Contains("coordinate.Row - FirstRowLetter") &&
            map.Contains("textureSize.X / ColumnCount") &&
            map.Contains("textureSize.Y / RowCount"));
        AssertTrue("Map draws visible dotted grid lines over hoverable cells",
            map.Contains("MapGridLineOverlay") &&
            map.Contains("DrawDottedLine") &&
            map.Contains("DrawCircle") &&
            map.Contains("MouseFilterEnum.Ignore"));
        AssertTrue("Map shows a cursor-side coordinate readout over playable cells and highlights hovered cells",
            map.Contains("MouseEntered += () => SetHoveredCoordinate(coordinate)") &&
            map.Contains("MouseExited += () => ClearHoveredCoordinate(coordinate)") &&
            map.Contains("button.GuiInput += @event => OnMapCellGuiInput(coordinate, @event)") &&
            map.Contains("InputEventMouseMotion mouseMotion") &&
            map.Contains("_hoveredCoordinateLabel.Text = coordinate.Value.ToString();") &&
            map.Contains("HoveredCoordinateOffsetX = 18.0f") &&
            map.Contains("HoveredCoordinateOffsetY = 22.0f") &&
            map.Contains("_hoveredCoordinateLabel.Size = labelSize;") &&
            map.Contains("_hoveredCoordinateLabel.GlobalPosition = position") &&
            map.Contains("SetHoveredCoordinate(null);") &&
            !map.Contains("TooltipText = coordinate.ToString()") &&
            !mapScene.Contains("[node name=\"HoveredCoordinate\" type=\"Label\" parent=\"Root/Margin/Main/Header\"]") &&
            map.Contains("new Color(0.98f, 0.82f, 0.34f, 0.34f)"));
    }

    private static void TestF12ForestGatheringScene()
    {
        var scenePaths = ReadProjectFile("Scripts/Infrastructure/ScenePaths.cs");
        var map = ReadProjectFile("Scripts/UI/Map.cs");
        var mapScene = ReadProjectFile("Scenes/Main/Map.tscn");
        var gathering = ReadProjectFile("Scripts/UI/ForestGathering.cs");
        var gatheringCatalog = ReadProjectFile("Scripts/UI/ForestGatheringPlantCatalog.cs");
        var gatheringLayout = ReadProjectFile("Scripts/UI/ForestGatheringPlantLayout.cs");
        var gatheringFeedback = ReadProjectFile("Scripts/UI/ForestGatheringFeedbackFormatter.cs");
        var gatheringScene = ReadProjectFile("Scenes/Main/ForestGathering.tscn");
        var normalizedGatheringScene = gatheringScene.Replace("\r\n", "\n");
        var hud = ReadProjectFile("Scripts/UI/Hud.cs");
        var hudNavigationService = ReadProjectFile("Scripts/UI/HudNavigationService.cs");
        var plantSpriteDir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "Assets",
            "Gathering",
            "Plants"));
        var inspectionPlantSpriteDir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "Assets",
            "Gathering",
            "InspectionPlants"));
        var mintSketchPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "Assets",
            "Gathering",
            "Sketches",
            "mint_high_quality_pencil_sketch.png"));
        var magnifyingGlassCursorPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "Assets",
            "UI",
            "magnifying_glass_cursor.png"));

        AssertTrue("ScenePaths exposes the forest gathering scene",
            scenePaths.Contains("public const string ForestGathering") &&
            scenePaths.Contains("res://Scenes/Main/ForestGathering.tscn"));
        AssertTrue("Map F12 defaults to the forest gathering scene",
            map.Contains("F12ScenePath = ScenePaths.ForestGathering") &&
            map.Contains("damp forest edge"));
        AssertTrue("Map scene keeps the script F12 destination default",
            mapScene.Contains("path=\"res://Scripts/UI/Map.cs\"") &&
            !mapScene.Contains("F12ScenePath = \"\""));
        AssertTrue("Forest gathering scene uses the gathering script and reserves HUD space",
            gatheringScene.Contains("path=\"res://Scripts/UI/ForestGathering.cs\"") &&
            gatheringScene.Contains("offset_top = 50.0") &&
            gatheringScene.Contains("clip_contents = true"));
        AssertTrue("Forest gathering is configured for three mint selections",
            gathering.Contains("TargetItemId = \"mint\"") &&
            gathering.Contains("MaxActions = 3") &&
            gatheringScene.Contains("path=\"res://Scripts/UI/ForestGathering.cs\""));
        AssertTrue("Forest gathering clue sketch uses the high quality mint pencil drawing",
            File.Exists(mintSketchPath) &&
            new FileInfo(mintSketchPath).Length > 100_000 &&
            gathering.Contains("SketchTexturePath = \"res://Assets/Gathering/Sketches/mint_high_quality_pencil_sketch.png\"") &&
            gatheringScene.Contains("SketchPreviewOverlayPath = NodePath(\"Root/SketchPreviewOverlay\")") &&
            gatheringScene.Contains("SketchPreviewImagePath = NodePath(\"Root/SketchPreviewOverlay/PreviewFrame/Margin/SketchPreview\")") &&
            !gathering.Contains("SketchTexturePath = \"res://Assets/Items/mint.png\""));
        AssertTrue("Forest gathering keeps authored plant definitions over the forest art",
            gatheringCatalog.Contains("PlantDefinitions") &&
            gatheringCatalog.Contains("new(\"mint\", \"Specimen A\"") &&
            gatheringCatalog.Contains("new(\"thyme\", \"Specimen M\"") &&
            gatheringCatalog.Contains("new(\"yarrow\", \"Specimen S\"") &&
            gatheringCatalog.Contains("new(\"willow\", \"Specimen X\"") &&
            gatheringCatalog.Contains("new(\"thyme\", \"Specimen Y\"") &&
            gatheringCatalog.Contains("new(\"comfrey\", \"Specimen AI\"") &&
            gatheringCatalog.Contains("new(\"willow\", \"Specimen AJ\"") &&
            gatheringScene.Contains("PlantHotspotsPath = NodePath(\"Root/PlantHotspots\")") &&
            gatheringScene.Contains("forest_gathering_mint.png"));
        AssertEqual("Forest gathering keeps exactly three authored mint targets",
            3,
            gatheringCatalog.Split("new(\"mint\",").Length - 1);
        AssertTrue("Forest gathering randomizes the selectable plant layout on load",
            gathering.Contains("RandomizePlantLayout();") &&
            gathering.Contains("new RandomNumberGenerator()") &&
            gathering.Contains("random.Randomize();") &&
            gatheringLayout.Contains("FindPlantPlacement") &&
            gatheringLayout.Contains("PlacementAttemptsPerPlant") &&
            gatheringLayout.Contains("_placedEntries.Sort"));
        AssertTrue("Forest gathering renders randomized plant sprites above the forest art",
            gathering.Contains("CreatePlantVisuals();") &&
            gathering.Contains("new TextureRect") &&
            gathering.Contains("LoadPlantTexture(entry.TexturePath)") &&
            gathering.Contains("SetNormalizedRect(visual, entry.Center, entry.Size)") &&
            gathering.Contains("PlantVisualDepthRange") &&
            gathering.Contains("_plantHotspots.AddChild(visual)") &&
            gatheringCatalog.Contains("mint_decoy_hidden_bud.png") &&
            gatheringCatalog.Contains("mint_decoy_wrong_veins.png"));
        AssertTrue("Forest gathering keeps candidate debug borders disabled by default",
            gathering.Contains("ShowCandidateDebugBorders = false") &&
            gathering.Contains("CandidateDebugBorderZIndex = PlantVisualDepthRange + 1") &&
            gathering.Contains("new Panel") &&
            gathering.Contains("CreateCandidateDebugBorderStyleBox") &&
            gathering.Contains("CalculateCandidateBounds(entry, hotspotSize)") &&
            gathering.Contains("SetNormalizedRect(border, candidateBounds.Position + (candidateBounds.Size * 0.5f), candidateBounds.Size)") &&
            gathering.Contains("_plantHotspots.AddChild(border)") &&
            !gathering.Contains("ShowCandidateDebugBorders = true"));
        AssertTrue("Forest gathering keeps target mint highlights disabled by default",
            gathering.Contains("ShowTargetDebugHighlights = false") &&
            gathering.Contains("TargetDebugHighlightZIndex = CandidateDebugBorderZIndex + 1") &&
            gathering.Contains("CreateTargetDebugHighlightStyleBox") &&
            gathering.Contains("Name = $\"TargetDebugHighlight{index}\"") &&
            gathering.Contains("IsTargetEntry(entry)") &&
            gathering.Contains("_plantHotspots.AddChild(highlight)") &&
            gathering.Contains("_targetDebugHighlights.Add(highlight)") &&
            !gathering.Contains("ShowTargetDebugHighlights = true"));
        AssertTrue("Forest gathering narrows candidate hit areas to plant texture content",
            gathering.Contains("CalculateCandidateBounds(candidate, surfaceSize)") &&
            gathering.Contains("candidateBounds.HasPoint(normalizedPosition)") &&
            gathering.Contains("GetPlantContentTextureBounds(entry.TexturePath)") &&
            gathering.Contains("image.GetUsedRect()") &&
            gathering.Contains("CandidateBoundsPaddingPixels") &&
            !gathering.Contains("public bool Contains(Vector2 normalizedPosition)"));
        AssertTrue("Forest gathering keeps randomized plants below UI panels",
            gathering.Contains("ZIndex = Mathf.RoundToInt(entry.Center.Y * PlantVisualDepthRange)") &&
            normalizedGatheringScene.Contains("[node name=\"TopPanel\" type=\"PanelContainer\" parent=\"Root\"]\nz_index = 35") &&
            normalizedGatheringScene.Contains("[node name=\"FeedbackPanel\" type=\"PanelContainer\" parent=\"Root\"]\nz_index = 35") &&
            normalizedGatheringScene.Contains("[node name=\"InspectionPanel\" type=\"PanelContainer\" parent=\"Root\"]\nvisible = false\nz_index = 50") &&
            normalizedGatheringScene.Contains("[node name=\"CluePanel\" type=\"PanelContainer\" parent=\"Root\"]\nvisible = false\nz_index = 45") &&
            normalizedGatheringScene.Contains("[node name=\"SketchPreviewOverlay\" type=\"Control\" parent=\"Root\"]\nvisible = false\nz_index = 80") &&
            normalizedGatheringScene.Contains("[node name=\"ReturnPrompt\" type=\"Control\" parent=\".\"]\nvisible = false\nz_index = 100"));
        AssertTrue("Forest gathering sprite assets exist",
            File.Exists(Path.Combine(plantSpriteDir, "mint_target_a.png")) &&
            File.Exists(Path.Combine(plantSpriteDir, "mint_decoy_hidden_bud.png")) &&
            File.Exists(Path.Combine(plantSpriteDir, "mint_decoy_wrong_veins.png")) &&
            File.Exists(Path.Combine(plantSpriteDir, "forest_flowering_stems.png")));
        AssertTrue("Forest gathering key plant sprites keep high-detail source art",
            new FileInfo(Path.Combine(plantSpriteDir, "mint_target_a.png")).Length > 100_000 &&
            new FileInfo(Path.Combine(plantSpriteDir, "mint_decoy_hidden_bud.png")).Length > 100_000 &&
            new FileInfo(Path.Combine(plantSpriteDir, "mint_decoy_wrong_veins.png")).Length > 100_000 &&
            new FileInfo(Path.Combine(plantSpriteDir, "forest_flowering_stems.png")).Length > 100_000);
        AssertTrue("Forest gathering uses higher-resolution inspection-only plant art",
            Directory.Exists(inspectionPlantSpriteDir) &&
            File.Exists(Path.Combine(inspectionPlantSpriteDir, "inspection_mint_target_a.png")) &&
            File.Exists(Path.Combine(inspectionPlantSpriteDir, "inspection_mint_decoy_hidden_bud.png")) &&
            File.Exists(Path.Combine(inspectionPlantSpriteDir, "inspection_forest_slender_stems.png")) &&
            new FileInfo(Path.Combine(inspectionPlantSpriteDir, "inspection_mint_target_a.png")).Length > new FileInfo(Path.Combine(plantSpriteDir, "mint_target_a.png")).Length &&
            new FileInfo(Path.Combine(inspectionPlantSpriteDir, "inspection_mint_decoy_hidden_bud.png")).Length > new FileInfo(Path.Combine(plantSpriteDir, "mint_decoy_hidden_bud.png")).Length &&
            new FileInfo(Path.Combine(inspectionPlantSpriteDir, "inspection_forest_slender_stems.png")).Length > new FileInfo(Path.Combine(plantSpriteDir, "forest_slender_stems.png")).Length &&
            gatheringCatalog.Contains("InspectionPlantTexturePathPrefix = \"res://Assets/Gathering/InspectionPlants/\"") &&
            gatheringLayout.Contains("BuildInspectionTexturePath(definition.TexturePath)") &&
            gathering.Contains("entry.InspectionTexturePath"));
        AssertTrue("Forest gathering accepts free clicks to inspect plants instead of grid cells",
            gathering.Contains("_plantHotspots.GuiInput += _plantHotspotsGuiInputHandler") &&
            gathering.Contains("OnPlantHotspotsGuiInput") &&
            gathering.Contains("HandleGatheringClick(mouseButton.GlobalPosition)") &&
            gathering.Contains("ShowInspection(plantIndex, entry)") &&
            gathering.Contains("TryGetPlantEntryAtGlobalPosition") &&
            gathering.Contains("TryGetPlantEntryAtNormalizedPosition") &&
            gathering.Contains("No clear candidate there.") &&
            gathering.Contains("That plant has already been harvested.") &&
            gathering.Contains("_plantHotspots.MouseFilter = MouseFilterEnum.Stop") &&
            gatheringScene.Contains("[node name=\"ForestBackground\" type=\"TextureRect\" parent=\"Root\"]") &&
            gatheringScene.Contains("[node name=\"PlantHotspots\" type=\"Control\" parent=\"Root\"]") &&
            !gatheringScene.Contains("GridContainer") &&
            !gathering.Contains("CreatePlantHotspot") &&
            !gathering.Contains("SetAnchorsAndOffsets("));
        AssertTrue("Forest gathering keeps correctness tied to authored plant identity",
            gathering.Contains("IsTargetPlant(plantIndex)") &&
            gathering.Contains("IsTargetEntry(_activePlantEntries[plantIndex])") &&
            gathering.Contains("string.Equals(entry.ItemId, TargetItemId") &&
            gathering.Contains("TargetItemId") &&
            !gathering.Contains("_targetPlantIndexes") &&
            !gathering.Contains("SelectTargetPlants"));
        AssertTrue("Forest gathering inspection panel is visual-only and confirms harvests",
            gatheringScene.Contains("InspectionPanelPath = NodePath(\"Root/InspectionPanel\")") &&
            gatheringScene.Contains("custom_minimum_size = Vector2(720, 680)") &&
            gatheringScene.Contains("anchors_preset = 8") &&
            gatheringScene.Contains("InspectionImagePath = NodePath(\"Root/InspectionPanel/Margin/VBox/Body/InspectionImageFrame/InspectionImage\")") &&
            gatheringScene.Contains("custom_minimum_size = Vector2(664, 540)") &&
            gatheringScene.Contains("mouse_filter = 0") &&
            gatheringScene.Contains("[node name=\"Harvest\" type=\"Button\" parent=\"Root/InspectionPanel/Margin/VBox/Actions\"]") &&
            gatheringScene.Contains("[node name=\"Remove\" type=\"Button\" parent=\"Root/InspectionPanel/Margin/VBox/Actions\"]") &&
            gatheringScene.Contains("[node name=\"KeepLooking\" type=\"Button\" parent=\"Root/InspectionPanel/Margin/VBox/Actions\"]") &&
            gathering.Contains("RefreshInspectionImage") &&
            gathering.Contains("_inspectionSourceTexture = texture") &&
            gathering.Contains("OnHarvestPressed") &&
            !gathering.Contains("TargetInspectionDetail") &&
            !gathering.Contains("FalseInspectionDetail") &&
            !gatheringScene.Contains("InspectionDetailLabelPath") &&
            !gatheringScene.Contains("[node name=\"Detail\""));
        AssertTrue("Forest gathering inspection supports close-up zoom with a magnifying cursor",
            File.Exists(magnifyingGlassCursorPath) &&
            gathering.Contains("MagnifyingGlassCursorPath = \"res://Assets/UI/magnifying_glass_cursor.png\"") &&
            gathering.Contains("LoadMagnifyingGlassCursor") &&
            gathering.Contains("Input.SetCustomMouseCursor") &&
            gathering.Contains("public override void _Input(InputEvent @event)") &&
            gathering.Contains("_inspectionImage.GuiInput += _inspectionImageGuiInputHandler") &&
            gathering.Contains("private void OnInspectionImageGuiInput(InputEvent @event)") &&
            gathering.Contains("ToggleInspectionZoom(mouseButton.GlobalPosition)") &&
            gathering.Contains("_inspectionZoomEnabled = false") &&
            gathering.Contains("DefaultInspectionZoomScale = 2.8f") &&
            gathering.Contains("InspectionZoomWheelStep = 0.35f") &&
            gathering.Contains("AdjustInspectionZoom(mouseButton.ButtonIndex, mouseButton.GlobalPosition)") &&
            gathering.Contains("buttonIndex == MouseButton.WheelUp || buttonIndex == MouseButton.WheelDown") &&
            gathering.Contains("_inspectionZoomScale = Math.Clamp(") &&
            gathering.Contains("RefreshInspectionMagnifierForMousePosition") &&
            gathering.Contains("ShouldUseInspectionMagnifier") &&
            gathering.Contains("UpdateInspectionZoomFromGlobalPosition") &&
            gathering.Contains("TryMapInspectionGlobalPositionToSource") &&
            gathering.Contains("TryMapInspectionImagePositionToSource(localPosition, true, out sourcePosition)") &&
            gathering.Contains("BuildInspectionZoomCrop") &&
            gathering.Contains("ImageTexture.CreateFromImage(crop)") &&
            gathering.Contains("RestoreFullInspectionImage") &&
            gathering.Contains("OnInspectionActionButtonMouseEntered") &&
            gathering.Contains("Math.Clamp(globalPosition.X - imageRect.Position.X") &&
            !gathering.Contains("InspectionZoomAlphaThreshold") &&
            !gathering.Contains("private const float InspectionZoomScale"));
        AssertTrue("Forest gathering clue panel is toggled from the header and draggable",
            gatheringScene.Contains("ClueToggleButtonPath = NodePath(\"Root/TopPanel/Margin/Row/ClueToggle\")") &&
            gatheringScene.Contains("[node name=\"ClueToggle\" type=\"Button\" parent=\"Root/TopPanel/Margin/Row\"]") &&
            gatheringScene.Contains("text = \"!\"") &&
            gatheringScene.Contains("[node name=\"CluePanel\" type=\"PanelContainer\" parent=\"Root\"]") &&
            gatheringScene.Contains("visible = false") &&
            gatheringScene.Contains("custom_minimum_size = Vector2(340, 318)") &&
            gatheringScene.Contains("offset_top = 52.0") &&
            gatheringScene.Contains("offset_bottom = 370.0") &&
            gatheringScene.Contains("[node name=\"Description\" type=\"Label\" parent=\"Root/CluePanel/Margin/VBox\"]") &&
            gatheringScene.Contains("custom_minimum_size = Vector2(0, 74)") &&
            gatheringScene.Contains("[node name=\"SketchFrame\" type=\"PanelContainer\" parent=\"Root/CluePanel/Margin/VBox\"]") &&
            gatheringScene.Contains("custom_minimum_size = Vector2(0, 178)") &&
            gatheringScene.Contains("[node name=\"Sketch\" type=\"TextureRect\" parent=\"Root/CluePanel/Margin/VBox/SketchFrame\"]") &&
            gatheringScene.Contains("custom_minimum_size = Vector2(0, 176)") &&
            gatheringScene.Contains("path=\"res://Scripts/UI/DraggablePanel.cs\"") &&
            gatheringScene.Contains("DragHandlePath = NodePath(\"Margin/VBox/TargetName\")") &&
            gathering.Contains("ConfigureCluePanelLayout();") &&
            gathering.Contains("CluePanelSize = new(340.0f, 318.0f)") &&
            gathering.Contains("CluePanelTopRightOffset = new(-358.0f, 52.0f)") &&
            gathering.Contains("_cluePanel.Size = CluePanelSize") &&
            gathering.Contains("_sketchTextureRect.CustomMinimumSize = ClueSketchSize") &&
            gathering.Contains("_clueToggleButton.Pressed += OnClueTogglePressed") &&
            gathering.Contains("private void OnClueTogglePressed()") &&
            gathering.Contains("_cluePanel.Visible = shouldShow"));
        AssertTrue("Forest gathering opens a centered sketch preview from the clue sketch",
            gatheringScene.Contains("[node name=\"Sketch\" type=\"TextureRect\" parent=\"Root/CluePanel/Margin/VBox/SketchFrame\"]") &&
            gatheringScene.Contains("[node name=\"SketchPreviewOverlay\" type=\"Control\" parent=\"Root\"]") &&
            gatheringScene.Contains("[node name=\"PreviewFrame\" type=\"PanelContainer\" parent=\"Root/SketchPreviewOverlay\"]") &&
            gatheringScene.Contains("custom_minimum_size = Vector2(620, 860)") &&
            gatheringScene.Contains("[node name=\"SketchPreview\" type=\"TextureRect\" parent=\"Root/SketchPreviewOverlay/PreviewFrame/Margin\"]") &&
            gatheringScene.Contains("custom_minimum_size = Vector2(592, 832)") &&
            gathering.Contains("_sketchTextureRect.GuiInput += _sketchGuiInputHandler") &&
            gathering.Contains("_sketchPreviewOverlay.GuiInput += _sketchPreviewOverlayGuiInputHandler") &&
            gathering.Contains("private void OnSketchGuiInput(InputEvent @event)") &&
            gathering.Contains("private void OnSketchPreviewOverlayGuiInput(InputEvent @event)") &&
            gathering.Contains("ShowSketchPreview();") &&
            gathering.Contains("HideSketchPreview();") &&
            gathering.Contains("!_sketchPreviewImage.GetGlobalRect().HasPoint(mouseButton.GlobalPosition)") &&
            gathering.Contains("_sketchPreviewImage.Texture = texture"));
        AssertTrue("Correct selection stages the target ingredient until return",
            gathering.Contains("_pendingTargetQuantity += RewardQuantityPerCorrectSelection") &&
            gathering.Contains("Correct. Marked") &&
            gathering.Contains("CollectTargetPlant()"));
        AssertTrue("Harvest confirmations consume attempts and explain wrong harvests",
            gathering.Contains("private void OnHarvestPressed()") &&
            gathering.Contains("_remainingActions -= 1") &&
            gathering.Contains("BuildWrongPlantFeedback(_activePlantEntries[plantIndex])") &&
            gatheringFeedback.Contains("TryGetMintDecoyClueName") &&
            gatheringFeedback.Contains("decoyClueName = clueName.Replace('_', ' ')") &&
            gatheringFeedback.Contains("\"rounder leaf\"") &&
            gatheringFeedback.Contains("the leaves are too wide for {targetName}") &&
            gatheringFeedback.Contains("That was {plantName}, not {targetName}.") &&
            gathering.Contains("Harvests remaining: {_remainingActions}"));
        AssertTrue("Inspection can remove plants without staging rewards",
            gatheringScene.Contains("InspectionRemoveButtonPath = NodePath(\"Root/InspectionPanel/Margin/VBox/Actions/Remove\")") &&
            gatheringScene.Contains("text = \"Remove\"") &&
            gathering.Contains("_inspectionRemoveButton.Pressed += OnRemovePressed") &&
            gathering.Contains("private void OnRemovePressed()") &&
            gathering.Contains("_removedPlantIndexes.Add(plantIndex)") &&
            gathering.Contains("RemovePlantFromArea(plantIndex)") &&
            gathering.Contains("Removed this plant from the area.") &&
            gathering.Contains("private bool HasSelectablePlants()") &&
            gathering.Contains("if (!HasSelectablePlants())") &&
            gathering.Contains("FinishGathering();"));
        AssertTrue("Perfect gathering stages a mint seed through garden seed inventory",
            gathering.Contains("_correctSelections == MaxActions") &&
            gathering.Contains("StagePerfectGatheringSeedReward") &&
            gathering.Contains("_pendingSeedQuantity = 1"));
        AssertTrue("Forest gathering prompts return to the house with a reward summary",
            gathering.Contains("ReturnPromptPath") &&
            gathering.Contains("BuildReturnSummary") &&
            gathering.Contains("Return to the house to add:") &&
            gathering.Contains("GetTree().ChangeSceneToFile(ScenePaths.Main)") &&
            gatheringScene.Contains("[node name=\"ReturnPrompt\" type=\"Control\" parent=\".\"]") &&
            gatheringScene.Contains("text = \"Return\""));
        AssertTrue("Forest gathering commits staged rewards only when returning",
            gathering.Contains("private void CommitGatheredRewards()") &&
            gathering.Contains("if (_rewardsCommitted)") &&
            gathering.Contains("_gameState.AddItem(TargetItemId, _pendingTargetQuantity)") &&
            gathering.Contains("_gameState.AddSeed(GameState.BuildSeedId(TargetItemId), _pendingSeedQuantity)") &&
            gathering.Contains("CommitGatheredRewards();"));
        AssertTrue("HUD blocks navigation while the gathering scene is active",
            hudNavigationService.Contains("tree.CurrentScene is ForestGathering or JuniperGathering") &&
            hud.Contains("private bool IsSceneNavigationBlocked()") &&
            hud.Contains("HudNavigationService.IsNavigationBlocked(GetTree())") &&
            hud.Contains("if (IsSceneNavigationBlocked())") &&
            hud.Contains("_settingsButton.Disabled = navigationBlocked") &&
            hud.Contains("_mapButton.Disabled = navigationBlocked || GetTree().CurrentScene is Map"));
    }

    private static void TestK17JuniperGatheringScene()
    {
        var scenePaths = ReadProjectFile("Scripts/Infrastructure/ScenePaths.cs");
        var map = ReadProjectFile("Scripts/UI/Map.cs");
        var gathering = ReadProjectFile("Scripts/UI/JuniperGathering.cs");
        var gatheringScene = ReadProjectFile("Scenes/Main/JuniperGathering.tscn");
        var hud = ReadProjectFile("Scripts/UI/Hud.cs");
        var hudNavigationService = ReadProjectFile("Scripts/UI/HudNavigationService.cs");
        var items = ReadProjectFile("Data/items_data.tres");

        AssertTrue("ScenePaths exposes the juniper gathering scene",
            scenePaths.Contains("public const string JuniperGathering") &&
            scenePaths.Contains("res://Scenes/Main/JuniperGathering.tscn"));
        AssertTrue("Map K17 defaults to the juniper gathering scene",
            map.Contains("JuniperPointOfInterestCoordinate = \"K17\"") &&
            map.Contains("K17ScenePath = ScenePaths.JuniperGathering") &&
            map.Contains("K17PointOfInterestMessage") &&
            map.Contains("_pointsOfInterest[JuniperPointOfInterestCoordinate]"));
        AssertTrue("Juniper gathering uses the authored juniper item id",
            gathering.Contains("TargetItemId = \"juniper\"") &&
            items.Contains("\"id\": \"juniper\"") &&
            items.Contains("\"name\": \"Juniper\""));
        AssertTrue("Juniper gathering scene uses the gathering script and reserves HUD space",
            gatheringScene.Contains("path=\"res://Scripts/UI/JuniperGathering.cs\"") &&
            gatheringScene.Contains("offset_top = 50.0") &&
            gatheringScene.Contains("clip_contents = true"));
        AssertTrue("Juniper gathering scene wires the playfield and result prompt nodes",
            gatheringScene.Contains("PlayAreaPath = NodePath(\"Root/PlayArea\")") &&
            gatheringScene.Contains("BushPath = NodePath(\"Root/PlayArea/Bush\")") &&
            gatheringScene.Contains("BasketPath = NodePath(\"Root/PlayArea/Basket\")") &&
            gatheringScene.Contains("CatchLinePath = NodePath(\"Root/PlayArea/CatchLine\")") &&
            gatheringScene.Contains("ResultPromptPath = NodePath(\"ResultPrompt\")") &&
            gatheringScene.Contains("[node name=\"Return\" type=\"Button\" parent=\"ResultPrompt/Dialog/Margin/VBox\"]"));
        AssertTrue("Juniper gathering shakes the bush to release falling berries",
            gathering.Contains("_bush.GuiInput += OnBushGuiInput") &&
            gathering.Contains("ShakeDistanceForBurst") &&
            gathering.Contains("SpawnBerryBurst") &&
            gathering.Contains("BerriesPerBurst") &&
            gathering.Contains("berry.Y += berry.Speed * deltaSeconds"));
        AssertTrue("Juniper gathering moves a bottom basket and freezes it after wrong catches",
            gathering.Contains("_basket.GuiInput += OnBasketGuiInput") &&
            gathering.Contains("MoveBasketToMouse") &&
            gathering.Contains("ClampBasketX") &&
            gathering.Contains("ClampBasketY") &&
            gathering.Contains("FreezeDurationSeconds = 2.0f") &&
            gathering.Contains("_basketFreezeRemaining = FreezeDurationSeconds") &&
            gathering.Contains("_basketDragActive = false"));
        AssertTrue("Juniper gathering catches berries only when their bounds touch the basket",
            gathering.Contains("IsBerryTouchingBasket") &&
            gathering.Contains("berryRight >= basketLeft") &&
            gathering.Contains("berryLeft <= basketRight") &&
            gathering.Contains("berryBottom >= basketTop") &&
            gathering.Contains("berryTop <= basketBottom") &&
            !gathering.Contains("IsBerryCaughtByBasket"));
        AssertTrue("Juniper gathering only rewards dark blue ripe berries",
            gathering.Contains("RipeBerryTexturePath") &&
            gathering.Contains("juniper_berry_ripe.png") &&
            gathering.Contains("WrongBerryRedTexturePath") &&
            gathering.Contains("WrongBerryAmberTexturePath") &&
            gathering.Contains("WrongBerryGreenTexturePath") &&
            gathering.Contains("WrongBerryPaleBlueTexturePath") &&
            gathering.Contains("_wrongBerryTextures") &&
            gathering.Contains("if (berry.IsRipe)") &&
            gathering.Contains("_ripeCaught += 1") &&
            gathering.Contains("_wrongCaught += 1"));
        AssertTrue("Juniper gathering keeps the requested reward thresholds and completes after 15 ripe berries",
            gathering.Contains("GatheringDurationSeconds = 30.0f") &&
            gathering.Contains("private const int RipeBerryCompletionCount = 15") &&
            gathering.Contains("if (berry.IsRipe && _ripeCaught >= RipeBerryCompletionCount)") &&
            gathering.Contains("if (_ripeCaught >= RipeBerryCompletionCount)") &&
            gathering.Contains("return 3") &&
            gathering.Contains("if (_ripeCaught >= 10)") &&
            gathering.Contains("return 2") &&
            gathering.Contains("if (_ripeCaught >= 5)") &&
            gathering.Contains("return 1"));
        AssertTrue("Juniper gathering stages rewards until the result return button",
            gathering.Contains("FinishGathering") &&
            gathering.Contains("Return to the house to add:") &&
            gathering.Contains("private void CommitGatheredRewards()") &&
            gathering.Contains("if (_rewardsCommitted)") &&
            gathering.Contains("_gameState.AddItem(TargetItemId, rewardQuantity)") &&
            gathering.Contains("GetTree().ChangeSceneToFile(ScenePaths.Main)") &&
            gathering.Contains("ShouldShowWomanInGreenCutscene()") &&
            gathering.Contains("ScenePaths.WomanInGreenCutscene"));
        AssertTrue("HUD blocks navigation while either gathering scene is active",
            hudNavigationService.Contains("tree.CurrentScene is ForestGathering or JuniperGathering") &&
            hud.Contains("HudNavigationService.IsNavigationBlocked(GetTree())") &&
            hud.Contains("_settingsButton.Disabled = navigationBlocked") &&
            hud.Contains("_mapButton.Disabled = navigationBlocked || GetTree().CurrentScene is Map"));
    }

    private static void TestHudReturnToMainMenuDoesNotAutoSave()
    {
        var source = ReadProjectFile("Scripts/UI/Hud.cs");
        var scenePaths = ReadProjectFile("Scripts/Infrastructure/ScenePaths.cs");
        var hudNavigationService = ReadProjectFile("Scripts/UI/HudNavigationService.cs");

        AssertTrue("Hud return-to-menu handler exists", source.Contains("OnReturnToMainMenuPressed"));
        AssertTrue("Hud return-to-menu still changes scenes",
            source.Contains("HudNavigationService.TryOpenMainMenu(this)") &&
            hudNavigationService.Contains("ScenePaths.MainMenu") &&
            scenePaths.Contains("res://MainMenu.tscn"));
        AssertTrue("Hud return-to-menu no longer auto-saves", !source.Contains("Could not save before returning to main menu"));
    }

    private static void TestHudCalendarIsWired()
    {
        var hudSource = ReadProjectFile("Scripts/UI/Hud.cs");
        var hudScene = ReadProjectFile("Scenes/UI/Hud.tscn");
        var calendarPanel = ReadProjectFile("Scripts/UI/CalendarPanel.cs");
        var calendarScene = ReadProjectFile("Scenes/UI/CalendarPanel.tscn");
        var calendarData = ReadProjectFile("Data/calendar_events_data.tres");
        var authoredData = ReadProjectFile("Data/authored_data.tres");

        AssertTrue("HUD date uses the existing status path as a button",
            hudScene.Contains("[node name=\"Day\" type=\"Button\" parent=\"Content/Status\"]") &&
            hudScene.Contains("text = \"26/03 Y1\"") &&
            hudSource.Contains("DateButtonPath = new(\"Content/Status/Day\")") &&
            hudSource.Contains("_dateButton.Text = GameCalendar.ToDate(_gameState.Day).ToHudText();"));
        AssertTrue("HUD instances the calendar panel",
            hudScene.Contains("path=\"res://Scenes/UI/CalendarPanel.tscn\"") &&
            hudScene.Contains("[node name=\"CalendarPanel\" parent=\".\" instance=ExtResource(\"3_calendar\")]") &&
            hudSource.Contains("CalendarPanelPath = new(\"CalendarPanel\")") &&
            hudSource.Contains("_calendarPanel.TogglePanel();"));
        AssertTrue("Calendar panel shows the current month and selected day details",
            calendarScene.Contains("[node name=\"DayGrid\" type=\"GridContainer\"") &&
            calendarScene.Contains("columns = 7") &&
            calendarScene.Contains("[node name=\"EventDetails\" type=\"RichTextLabel\"") &&
            calendarPanel.Contains("GameCalendar.DaysPerMonth") &&
            calendarPanel.Contains("SelectDay(capturedDay)") &&
            calendarPanel.Contains("GetVisibleEventsOnDate"));
        AssertTrue("Calendar panel lists known upcoming events",
            calendarPanel.Contains("GetVisibleUpcomingEvents") &&
            calendarPanel.Contains("No known upcoming events.") &&
            calendarPanel.Contains("UpcomingEventHorizonDays = GameCalendar.DaysPerYear"));
        AssertTrue("Calendar closes from button, outside click, and Escape",
            calendarScene.Contains("[node name=\"Close\" type=\"Button\"") &&
            calendarPanel.Contains("_closeButton.Pressed += HidePanel") &&
            hudSource.Contains("keyEvent.Keycode == Key.Escape") &&
            hudSource.Contains("IsPointInsideVisibleControl(_calendarPanel, mouseButton.GlobalPosition)") &&
            hudSource.Contains("HideHudPopups();"));
        AssertTrue("Authored calendar data is wired with the requested example event",
            authoredData.Contains("CalendarEventsPath = \"res://Data/calendar_events_data.tres\"") &&
            calendarData.Contains("\"id\": \"example_april_market_notice\"") &&
            calendarData.Contains("\"day\": 2") &&
            calendarData.Contains("\"month\": 4") &&
            calendarData.Contains("\"year\": 1"));
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
        var audioSettingsStore = ReadProjectFile("Scripts/UI/HudAudioSettingsStore.cs");
        var scene = ReadProjectFile("Scenes/UI/Hud.tscn");
        var persistentHud = ReadProjectFile("Scripts/Autoload/PersistentHud.cs");
        var saveGameButtonIndex = scene.IndexOf("[node name=\"SaveGame\" type=\"Button\" parent=\"SettingsPanel/Margin/VBox\"]", StringComparison.Ordinal);
        var openSettingsButtonIndex = scene.IndexOf("[node name=\"OpenSettings\" type=\"Button\" parent=\"SettingsPanel/Margin/VBox\"]", StringComparison.Ordinal);
        var toggleDebugPanelButtonIndex = scene.IndexOf("[node name=\"ToggleDebugPanel\" type=\"Button\" parent=\"SettingsPanel/Margin/VBox\"]", StringComparison.Ordinal);
        var returnToMainMenuButtonIndex = scene.IndexOf("[node name=\"ReturnToMainMenu\" type=\"Button\" parent=\"SettingsPanel/Margin/VBox\"]", StringComparison.Ordinal);
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
        const string soundtrackDirectory = "postcards_from_ireland_celtic_lofi_beats";
        const string soundtrackPathPrefix = "res://Assets/Audio/Music/postcards_from_ireland_celtic_lofi_beats/";
        var musicAssetNames = new[]
        {
            "01_moonera_and_lucie_cravero_cliffs_of_eire.mp3",
            "02_fugee_the_troublemakers.mp3",
            "03_bogar_waking_the_strings.mp3",
            "04_innigma_and_courtney_pinski_irish_sunset.mp3",
            "05_hotpotatoes_and_scott_munro_ode_to_a_broken_mandolin.mp3",
            "06_moonera_and_folkturia_fianna.mp3",
            "07_stabilisers_and_hoxde_where_lambs_once_grazed.mp3",
            "08_c4c_and_nathaniel_e_young_patrick_s_story.mp3",
            "09_detuned_cafe_the_road_back_home.mp3",
            "10_alpacca_an_old_story_told.mp3",
            "11_atamatoki_moonera_and_courtney_pinski_selkie_s_song.mp3",
            "12_pueblo_vista_whispers_of_a_home_barely_forgotten.mp3",
            "13_eva_gomi_tenshi_and_prithvi_a_long_expected_party.mp3",
            "14_moonera_and_joshua_hoe_where_we_began.mp3",
            "15_atamatoki_and_early_garden_lash_the_kettle_on.mp3",
            "16_stabilisers_and_myceliumbug_green_hills_of_home.mp3",
            "17_odem_medo_honeycomb.mp3",
            "18_weviis_and_atamatoki_river_shannon.mp3",
            "19_moonera_outlander.mp3",
        };
        var removedMusicAssetNames = new[]
        {
            "almost_bliss.mp3",
            "danse_morialta.mp3",
            "dream_culture.mp3",
            "easy_lemon.mp3",
            "healing.mp3",
            "immersed.mp3",
            "light_thought_var_1.mp3",
            "silver_blue_light.mp3",
            "southern_gothic.mp3",
            "wet_riffs.mp3",
            "when_the_wind_blows.mp3",
            "windswept.mp3",
            "backed_vibes.mp3",
            "bass_vibes.mp3",
            "cattails.mp3",
            "chill_wave.mp3",
            "clear_air.mp3",
            "clear_waters.mp3",
            "enchanted_valley.mp3",
            "fireflies_and_stardust.mp3",
            "lobby_time.mp3",
            "rainbows.mp3",
            "rains_will_fall.mp3",
            "river_fire.mp3",
            "summer_day.mp3",
            "walking_along.mp3",
        };
        var musicPaths = musicAssetNames.Select(name => Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "Assets",
            "Audio",
            "Music",
            soundtrackDirectory,
            name)));
        var removedMusicPaths = removedMusicAssetNames.Select(name => Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "Assets",
            "Audio",
            "Music",
            name)));

        AssertTrue("Rain audio asset is in the project", File.Exists(audioPath));
        AssertTrue("Soundtrack audio assets are in the project", musicPaths.All(File.Exists));
        AssertTrue("Removed soundtrack audio assets are no longer in the project", removedMusicPaths.All(path => !File.Exists(path)));
        AssertTrue("Hud gear menu exposes Settings below Save Game",
            scene.Contains("[node name=\"SaveGame\" type=\"Button\" parent=\"SettingsPanel/Margin/VBox\"]") &&
            scene.Contains("[node name=\"OpenSettings\" type=\"Button\" parent=\"SettingsPanel/Margin/VBox\"]") &&
            scene.Contains("text = \"Settings\""));
        AssertTrue("Hud gear menu keeps Return to Main Menu at the bottom",
            saveGameButtonIndex >= 0 &&
            openSettingsButtonIndex > saveGameButtonIndex &&
            toggleDebugPanelButtonIndex > openSettingsButtonIndex &&
            returnToMainMenuButtonIndex > toggleDebugPanelButtonIndex);
        AssertTrue("Hud defines the Settings panel",
            scene.Contains("[node name=\"Settings\" type=\"PanelContainer\" parent=\".\"]"));
        AssertTrue("Settings panel exposes the ambient sounds toggle",
            scene.Contains("[node name=\"AmbientSounds\" type=\"CheckBox\" parent=\"Settings/Margin/VBox\"]") &&
            scene.Contains("text = \"ambient sounds\""));
        AssertTrue("Settings panel exposes the rainfall volume slider",
            scene.Contains("[node name=\"RainfallVolume\" type=\"HSlider\" parent=\"Settings/Margin/VBox/RainfallVolumeRow\"]") &&
            scene.Contains("max_value = 1.0") &&
            scene.Contains("step = 0.01"));
        AssertTrue("Settings panel exposes music controls beside ambient settings",
            scene.Contains("[node name=\"Music\" type=\"CheckBox\" parent=\"Settings/Margin/VBox\"]") &&
            scene.Contains("text = \"music\"") &&
            scene.Contains("[node name=\"MusicVolume\" type=\"HSlider\" parent=\"Settings/Margin/VBox/MusicVolumeRow\"]") &&
            scene.Contains("text = \"Music volume\""));
        AssertTrue("Hud owns an ambient rain player",
            scene.Contains("[node name=\"AmbientRainPlayer\" type=\"AudioStreamPlayer\" parent=\".\"]"));
        AssertTrue("Hud owns a music player and next track HUD button",
            scene.Contains("[node name=\"MusicPlayer\" type=\"AudioStreamPlayer\" parent=\".\"]") &&
            scene.Contains("[node name=\"MusicFadeOutTimer\" type=\"Timer\" parent=\".\"]") &&
            scene.Contains("one_shot = true") &&
            scene.Contains("[node name=\"NextTrack\" type=\"Button\" parent=\"Content/Actions\"]") &&
            scene.Contains("text = \"Next track\""));
        AssertTrue("Hud loads and persists ambient rain settings",
            source.Contains("res://Assets/Audio/rain-sounds.mp3") &&
            source.Contains("HudAudioSettingsStore.Load()") &&
            source.Contains("HudAudioSettingsStore.Save(new HudAudioSettings(") &&
            source.Contains("HudAudioSettingsStore.GetVolumeDb(_rainfallVolume)") &&
            source.Contains("HudAudioSettingsStore.ClampNormalizedVolume(value)") &&
            audioSettingsStore.Contains("user://settings.cfg") &&
            audioSettingsStore.Contains("ConfigFile") &&
            audioSettingsStore.Contains("ambient_sounds_enabled") &&
            audioSettingsStore.Contains("rainfall_volume"));
        AssertTrue("Hud loads and persists music settings",
            musicAssetNames.All(name => source.Contains($"{soundtrackPathPrefix}{name}")) &&
            removedMusicAssetNames.All(name => !source.Contains($"res://Assets/Audio/Music/{name}")) &&
            audioSettingsStore.Contains("music_enabled") &&
            audioSettingsStore.Contains("music_volume"));
        string rainImport = ReadProjectFile(Path.Combine("Assets", "Audio", "rain-sounds.mp3.import"));
        AssertTrue("Hud loops rainfall using the stream import setting",
            rainImport.Contains("loop=true") &&
            !source.Contains("_ambientRainPlayer.Finished += OnAmbientRainFinished") &&
            !source.Contains("private void OnAmbientRainFinished()"));
        AssertTrue("Hud shuffles the soundtrack cycle and advances without polling",
            source.Contains("StartNewSoundtrackCycle();") &&
            source.Contains("_soundtrackRandom.Next") &&
            source.Contains("_musicPlayer.Finished += OnMusicFinished") &&
            source.Contains("private void OnNextTrackPressed()") &&
            source.Contains("PlayNextSoundtrackTrack();") &&
            source.Contains("StartNewSoundtrackCycle(previousTrackIndex);") &&
            source.Contains("soundtrackShouldRestart"));
        AssertTrue("Hud fades music tracks without fading ambient rain",
            source.Contains("MusicFadeSeconds = 5.0") &&
            source.Contains("SilentMusicVolumeDb = -80.0f") &&
            source.Contains("_musicFadeOutTimer.Timeout += OnMusicFadeOutTimerTimeout") &&
            source.Contains("_musicPlayer.Stream.GetLength()") &&
            source.Contains("CreateTween();") &&
            source.Contains("TweenProperty(_musicPlayer, \"volume_db\"") &&
            !source.Contains("TweenProperty(_ambientRainPlayer"));
        AssertTrue("Persistent HUD gates audio after scene visibility resolves",
            persistentHud.Contains("SetAmbientPlaybackAllowed(shouldShowHud)") &&
            !persistentHud.Contains("SetAmbientPlaybackAllowed(false)"));
    }

    private static void TestHudDayCounterReplacesRequestAlert()
    {
        var source = ReadProjectFile("Scripts/UI/Hud.cs");
        var scene = ReadProjectFile("Scenes/UI/Hud.tscn");

        AssertTrue("Hud scene replaces the request alert with a day counter in the status row",
            !scene.Contains("[node name=\"ShopTimer\"") &&
            !scene.Contains("[node name=\"RequestAlert\"") &&
            !scene.Contains("text = \"!\"") &&
            scene.Contains("[node name=\"DayCounter\" type=\"Label\" parent=\"Content/Status\"]") &&
            scene.Contains("text = \"Day 1\""));
        AssertTrue("Hud scene no longer defines the request popup",
            !scene.Contains("[node name=\"RequestPanel\"") &&
            !scene.Contains("Current Request") &&
            !scene.Contains("parent=\"RequestPanel"));
        AssertTrue("Hud drives the day counter from GameState.Day",
            source.Contains("DayCounterLabelPath = new(\"Content/Status/DayCounter\")") &&
            source.Contains("_dayCounter = GetNode<Label>(DayCounterLabelPath);") &&
            source.Contains("_dayCounter.Text = $\"Day {_gameState.Day}\";"));
        AssertTrue("Hud no longer carries request alert popup code",
            !source.Contains("RequestAlertButtonPath") &&
            !source.Contains("RequestPanelPath") &&
            !source.Contains("OnRequestAlertPressed") &&
            !source.Contains("RefreshRequestAlert") &&
            !source.Contains("SetRequestPanelVisible") &&
            !source.Contains("ResizeAndPositionRequestPanelUnderAlert") &&
            !source.Contains("CustomerDialogueTextFormatter.BuildDesiredRequestText") &&
            !source.Contains("CustomerDialogueTextFormatter.BuildBadRequestText"));
    }
}
