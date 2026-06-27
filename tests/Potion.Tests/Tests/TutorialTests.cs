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

internal static class TutorialTests
{
    public static void Register(TestRunner runner)
    {
        runner.Run("Tutorial game state transitions stay stable", TestTutorialGameStateTransitions);
        runner.Run("Tutorial snapshot round-trip stays stable", TestTutorialSnapshotRoundTrip);
        runner.Run("Main scene wires tutorial controller", TestMainSceneWiresTutorialController);
        runner.Run("Tutorial overlay scene wiring stays intact", TestTutorialOverlaySceneWiring);
        runner.Run("Tutorial architecture extraction stays intact", TestTutorialArchitectureExtraction);
        runner.Run("Tutorial Mother post-serve dialogue replaces next customer", TestTutorialMotherPostServeDialogueReplacesNextCustomer);
        runner.Run("Tutorial raw prep button stays enabled", TestTutorialRawPrepButtonStaysEnabled);
        runner.Run("Tutorial overlay keeps one dimming strategy", TestTutorialOverlayUsesDynamicCutoutsOnly);
    }

    private static void TestTutorialGameStateTransitions()
    {
        var source = ReadProjectFile("Scripts/Autoload/GameState.cs");
        var tutorialProgressState = ReadProjectFile("Scripts/Systems/TutorialProgressState.cs");

        AssertTrue("GameState exposes explicit tutorial status", source.Contains("public TutorialStatus TutorialProgressStatus => _tutorialProgressState.Status;"));
        AssertTrue("GameState keeps requested compatibility view", source.Contains("public bool TutorialRequested => _tutorialProgressState.Requested;") && tutorialProgressState.Contains("public bool Requested => Status == TutorialStatus.InProgress;"));
        AssertTrue("GameState keeps completed compatibility view", source.Contains("public bool TutorialCompleted => _tutorialProgressState.Completed;") && tutorialProgressState.Contains("public bool Completed => Status == TutorialStatus.Completed;"));
        AssertTrue("GameState keeps skipped compatibility view", source.Contains("public bool TutorialSkipped => _tutorialProgressState.Skipped;") && tutorialProgressState.Contains("public bool Skipped => Status == TutorialStatus.Skipped;"));
        AssertTrue("GameState exposes tutorial step", source.Contains("public int TutorialStep => _tutorialProgressState.Step;"));

        AssertTrue("RequestTutorial exists", source.Contains("public void RequestTutorial()"));
        AssertTrue("RequestTutorial sets status to in progress", source.Contains("_tutorialProgressState.Request();") && tutorialProgressState.Contains("Status = TutorialStatus.InProgress;"));

        AssertTrue("SkipTutorial exists", source.Contains("public void SkipTutorial()"));
        AssertTrue("SkipTutorial sets status to skipped", source.Contains("_tutorialProgressState.Skip();") && tutorialProgressState.Contains("Status = TutorialStatus.Skipped;"));

        AssertTrue("CompleteTutorial exists", source.Contains("public void CompleteTutorial()"));
        AssertTrue("CompleteTutorial sets status to completed", source.Contains("_tutorialProgressState.Complete();") && tutorialProgressState.Contains("Status = TutorialStatus.Completed;"));

        AssertTrue("SetTutorialStep exists", source.Contains("public void SetTutorialStep(int step)"));
        AssertTrue("SetTutorialStep clamps to zero or above", source.Contains("_tutorialProgressState.SetStep(step)") && tutorialProgressState.Contains("Math.Max(0, step)"));
        AssertTrue("GameState tracks ten years later cutscene start and completion",
            source.Contains("TenYearsLaterCutsceneStartedStoryFlag = \"ten_years_later_cutscene_started\"") &&
            source.Contains("TenYearsLaterCutsceneCompletedStoryFlag = \"ten_years_later_cutscene_completed\"") &&
            source.Contains("RecordTenYearsLaterCutsceneStarted()") &&
            source.Contains("RecordTenYearsLaterCutsceneCompleted()") &&
            source.Contains("WomanInGreenCutsceneStartedStoryFlag = \"woman_in_green_cutscene_started\"") &&
            source.Contains("WomanInGreenCutsceneCompletedStoryFlag = \"woman_in_green_cutscene_completed\"") &&
            source.Contains("RecordWomanInGreenCutsceneStarted()") &&
            source.Contains("RecordWomanInGreenCutsceneCompleted()"));
    }

    private static void TestTutorialSnapshotRoundTrip()
    {
        var gameStateSource = ReadProjectFile("Scripts/Autoload/GameState.cs");
        var tutorialProgressState = ReadProjectFile("Scripts/Systems/TutorialProgressState.cs");
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

        AssertTrue("ApplySnapshot resolves tutorial status from explicit or legacy fields", gameStateSource.Contains("_tutorialProgressState.ApplySnapshot(snapshot);") && tutorialProgressState.Contains("Status = ResolveTutorialStatus(snapshot);"));
        AssertTrue("ApplySnapshot restores step with new step index fallback", tutorialProgressState.Contains("var restoredStep = snapshot.TutorialStepIndex > 0"));
        AssertTrue("ApplySnapshot clamps tutorial step", tutorialProgressState.Contains("Step = Math.Max(0, restoredStep);"));
    }

    private static void TestMainSceneWiresTutorialController()
    {
        var source = ReadProjectFile("Main.tscn");

        AssertTrue("Main scene references TutorialController script", source.Contains("path=\"res://Scripts/Controllers/TutorialController.cs\""));
        AssertTrue("Main scene includes TutorialController node", source.Contains("[node name=\"TutorialController\" type=\"Node\" parent=\".\"]"));
        AssertTrue("TutorialController wires overlay path", source.Contains("TutorialOverlayPath = NodePath(\"../CanvasLayer/TutorialOverlay\")"));
        AssertTrue("TutorialController wires HUD path", source.Contains("HudPath = NodePath(\"/root/PersistentHud/Hud\")"));
        AssertTrue("TutorialController wires station shelf path", source.Contains("StationShelfInventoryPath = NodePath(\"../CanvasLayer/PotionBrewingStationView/StationShelfInventory\")"));
        AssertTrue("TutorialController wires ingredient preparation tray path", source.Contains("IngredientPreparationTrayPath = NodePath(\"../CanvasLayer/PotionBrewingStationView/IngredientPreparationTray\")"));
        AssertTrue("TutorialController wires day summary panel path", source.Contains("DaySummaryPanelPath = NodePath(\"../CanvasLayer/DaySummaryPanel\")"));
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
        var stationShelf = ReadProjectFile("Scripts/UI/StationShelfInventory.cs");
        var preparationTray = ReadProjectFile("Scripts/UI/IngredientPreparationTray.cs");
        var stationCustomerPanel = ReadProjectFile("Scripts/UI/StationCustomerPanel.cs");
        var dayControllerSource = ReadProjectFile("Scripts/Controllers/DayController.cs");

        AssertTrue("TutorialController uses extracted state machine", controller.Contains("private TutorialStateMachine _stateMachine"));
        AssertTrue("TutorialController uses extracted overlay presenter", controller.Contains("private TutorialOverlayPresenter _overlayPresenter"));
        AssertTrue("TutorialController uses extracted interaction gate", controller.Contains("private readonly TutorialInteractionGate _interactionGate"));
        AssertTrue("TutorialController consumes tutorial content resource", controller.Contains("[Export] public TutorialContentResource TutorialContent"));
        AssertTrue("TutorialController uses station potion sold events for the sale review step", controller.Contains("_stationCustomerPanel.PotionSold += OnPotionSold;"));
        AssertTrue("TutorialController no longer caches sale score details for tutorial feedback", !controller.Contains("_lastTutorialSaleScore") && !controller.Contains("_lastTutorialSaleGrade"));
        AssertTrue("TutorialController resolves step-specific button locks", controller.Contains("UpdateTutorialButtonLock("));
        AssertTrue("TutorialController includes the close shop tutorial step", controller.Contains("TutorialStepId.CloseShop"));
        AssertTrue("TutorialController highlights the station customer panel for the close shop step",
            controller.Contains("case TutorialStepId.CloseShop") &&
            controller.Contains("_overlayPresenter.ShowForTarget(stepContent, _stationCustomerPanel);") &&
            !controller.Contains("GetNextCustomerButton()"));
        AssertTrue("TutorialController forces the final tutorial customer to end the shop day", controller.Contains("ForceCloseShopAfterCurrentCustomerForTutorial()"));
        AssertTrue("TutorialController caches HUD date control for tutorial highlighting", controller.Contains("HudDateControlPath = new(\"Content/Status/Day\")") && controller.Contains("_hudDateControl = GetOptionalHudControl(HudDateControlPath"));
        AssertTrue("TutorialController does not cache a HUD shop timer label", !controller.Contains("HudShopTimerLabelPath") && !controller.Contains("_hudShopTimerLabel"));
        AssertTrue("TutorialController starts with the brewing station view already available", !controller.Contains("OpenBrewPanelButtonPath") && !controller.Contains("_openBrewPanelButton"));
        AssertTrue("TutorialController points ingredient steps from shelf to preparation tray",
            controller.Contains("ShowIngredientSelectionStep(stepContent, _tutorialContent.MintId") &&
            controller.Contains("FocusIngredientShelfSlot(itemId)") &&
            controller.Contains("FocusPreparationDropBox()") &&
            controller.Contains("ShowForTargetsWithArrow(stepContent, ingredientTarget, preparationTarget)"));
        AssertTrue("TutorialController points Raw preparation steps at the Raw button",
            controller.Contains("ShowRawPreparationStep(stepContent") &&
            controller.Contains("FocusRawPreparationButton()") &&
            controller.Contains("IngredientPreparationCatalog.RawPreparationId"));
        AssertTrue("TutorialController points brewed potion serving through the serving slot and Serve button",
            controller.Contains("ShowServingDropStep(stepContent)") &&
            controller.Contains("ShowServeButtonStep(stepContent)") &&
            controller.Contains("FocusServingDropBox()") &&
            controller.Contains("GetServeButton()"));
        AssertTrue("TutorialController adds Mother lines to the customer panel",
            controller.Contains("ShowMotherLineForStep") &&
            controller.Contains("Let's start with the Mint.") &&
            controller.Contains("Great job {GetPlayerNameForMotherLine()}. Now bring it over here."));
        AssertTrue("TutorialController routes the post-serve beat through Mother dialogue in the station customer panel",
            controller.Contains("MotherPostServeDialogueResolved += OnMotherPostServeDialogueResolved") &&
            controller.Contains("EvaluateMotherPostServeDialogueResolved(CurrentStep())") &&
            controller.Contains("TutorialStepId.PostServeMotherDialogue") &&
            controller.Contains("ForceNextCustomerInteraction(string.Empty)") &&
            !controller.Contains("ForceNextCustomerInteraction(_tutorialContent.AmbiguousTutorialCustomerId)"));
        AssertTrue("DayController sends successful opening Mother completion into the time skip cutscene",
            dayControllerSource.Contains("MotherPostServeDialogueResolved += OnMotherPostServeDialogueResolved") &&
            dayControllerSource.Contains("OpeningMotherPotionItemId = \"potion_gravekeepers_balm\"") &&
            dayControllerSource.Contains("TenYearsLaterCutsceneStartedStoryFlag") &&
            dayControllerSource.Contains("TenYearsLaterCutsceneCompletedStoryFlag") &&
            dayControllerSource.Contains("CloseShopDayForStoryCutscene()") &&
            dayControllerSource.Contains("ChangeSceneWithFade(ScenePaths.TenYearsLaterCutscene)"));
        AssertTrue("TutorialController advances if the tutorial customer is already active after the intro",
            controller.Contains("_gameState.ActiveCustomerRequest?.Id ?? string.Empty") &&
            controller.Contains("EvaluateCustomerInteractionShown("));
        AssertTrue("TutorialController highlights status step with a combined HUD rect", controller.Contains("ShowForHighlightRect(stepContent, statusHighlightRect)"));
        AssertTrue("TutorialController builds a combined status highlight rectangle", controller.Contains("TryGetStatusHighlightRect(out var statusHighlightRect)"));
        AssertTrue("TutorialController requests shop close before Mother resolves so no next customer enters",
            controller.Contains("TutorialStepId.PostServeMotherDialogue") &&
            controller.Contains("ForceCloseShopAfterCurrentCustomerForTutorial()"));
        AssertTrue("TutorialController listens for day summary continue", controller.Contains("_daySummaryPanel.ContinuePressed += OnDaySummaryContinuePressed;"));
        AssertTrue("TutorialController highlights the day summary panel", controller.Contains("case TutorialStepId.DaySummary") && controller.Contains("_overlayPresenter.ShowForTarget(stepContent, _daySummaryPanel)"));
        AssertTrue("TutorialController allows the day summary continue button", controller.Contains("TutorialStepId.DaySummary => new BaseButton?[] { _daySummaryPanel?.GetContinueButton() }"));
        AssertTrue("TutorialController includes shelf, prep tray, station customer, and day summary panels in button locks",
            controller.Contains("new Node?[] { _hud, _brewPanel, _stationShelfInventory, _ingredientPreparationTray, _stationCustomerPanel, _daySummaryPanel }"));
        AssertTrue("TutorialOverlayPresenter supports direct highlight rectangles and arrows",
            presenter.Contains("ShowForHighlightRect(") &&
            presenter.Contains("ShowForTargetsWithArrow("));

        AssertTrue("TutorialStateMachine is a pure class", stateMachine.Contains("public sealed class TutorialStateMachine"));
        AssertTrue("TutorialStateMachine clamps tutorial step", stateMachine.Contains("public TutorialStepId ClampStep(int rawStep)"));
        AssertTrue("TutorialStateMachine advances opening potion through selected and raw-prepared ingredient events",
            stateMachine.Contains("EvaluateIngredientSelected(") &&
            stateMachine.Contains("EvaluateIngredientPrepared(") &&
            stateMachine.Contains("TutorialStepId.PrepareMintRaw") &&
            stateMachine.Contains("TutorialStepId.PrepareGorseRaw") &&
            stateMachine.Contains("TutorialStepId.PrepareThymeRaw"));
        AssertTrue("TutorialStateMachine moves the Serve click into the post-serve Mother dialogue",
            stateMachine.Contains("EvaluatePotionSelectedForServing(") &&
            stateMachine.Contains("TutorialStepId.ConfirmServe") &&
            stateMachine.Contains("TutorialTransition.To(TutorialStepId.PostServeMotherDialogue)"));
        AssertTrue("TutorialStateMachine completes after the post-serve Mother dialogue resolves",
            stateMachine.Contains("EvaluateMotherPostServeDialogueResolved(") &&
            stateMachine.Contains("step == TutorialStepId.PostServeMotherDialogue") &&
            stateMachine.Contains("TutorialTransition.Complete()"));
        AssertTrue("TutorialStateMachine removed the timer-driven close shop prompt", !stateMachine.Contains("EvaluateCloseShopPrompt("));
        AssertTrue("TutorialStateMachine advances from the final customer to day summary when the shop closes", stateMachine.Contains("step == TutorialStepId.AddTwoMoreSleepIngredients || step == TutorialStepId.CloseShop") && stateMachine.Contains("TutorialTransition.To(TutorialStepId.DaySummary)"));
        AssertTrue("TutorialStateMachine completes after continuing from the day summary", stateMachine.Contains("EvaluateDaySummaryContinued(") && stateMachine.Contains("step == TutorialStepId.DaySummary"));
        AssertTrue("DayController exposes a tutorial-only close-after-current-customer helper", dayControllerSource.Contains("public void ForceCloseShopAfterCurrentCustomerForTutorial()"));
        AssertTrue("DayController can close the shop for the story cutscene without a summary",
            dayControllerSource.Contains("public void CloseShopDayForStoryCutscene()") &&
            dayControllerSource.Contains("_daySummaryPanel.HidePanel();") &&
            dayControllerSource.Contains("_gameState.CloseShopDayState();"));
        AssertTrue("TutorialContentResource exists", tutorialContent.Contains("public partial class TutorialContentResource : Resource"));
        AssertTrue("TutorialContentResource uses the opening Mother customer and Minor Healing Potion copy",
            tutorialContent.Contains("TutorialCustomerId { get; set; } = \"customer_requests_opening_gravekeepers_balm\"") &&
            tutorialContent.Contains("Minor Healing Potion") &&
            !tutorialContent.Contains("Gravekeeper's Balm"));
        AssertTrue("TutorialContentResource teaches preparation tray and Raw prep before brewing",
            tutorialContent.Contains("Right click or drag an ingredient to the preparation tray.") &&
            tutorialContent.Contains("Ingredients can be prepared in different ways. For this potion, choose Raw.") &&
            tutorialContent.Contains("StepId = (int)TutorialStepId.ConfirmServe"));
        AssertTrue("TutorialContentResource includes the post-serve Mother dialogue step without overlay locking",
            tutorialContent.Contains("StepId = (int)TutorialStepId.PostServeMotherDialogue") &&
            tutorialContent.Contains("Answer Mother in the customer dialog box.") &&
            tutorialContent.Contains("LockOtherButtons = false"));
        AssertTrue("TutorialContentResource includes the close shop step copy", tutorialContent.Contains("StepId = (int)TutorialStepId.CloseShop"));
        AssertTrue("TutorialContentResource tells the player to close the shop without night events", tutorialContent.Contains("Close the shop to end the day.") && !tutorialContent.Contains("It is night time."));
        AssertTrue("TutorialContentResource includes the day summary step copy", tutorialContent.Contains("StepId = (int)TutorialStepId.DaySummary") && tutorialContent.Contains("Click Continue to start the next day."));
        AssertTrue("DaySummaryPanel exposes the continue button for tutorial locks", ReadProjectFile("Scripts/UI/DaySummaryPanel.cs").Contains("public Button? GetContinueButton()"));
        AssertTrue("Tutorial step content can lock other buttons", tutorialStepContent.Contains("public bool LockOtherButtons { get; set; }"));
        AssertTrue("Tutorial overlay presenter exists", presenter.Contains("public sealed class TutorialOverlayPresenter"));
        AssertTrue("Tutorial interaction gate exists", interactionGate.Contains("public sealed class TutorialInteractionGate"));
        AssertTrue("Tutorial interaction gate restores previous button state before reapplying", interactionGate.Contains("Restore();"));
        AssertTrue("BrewPanel exposes its brew button for tutorial locks", brewPanel.Contains("public Button? GetBrewButton()"));
        AssertTrue("Station shelf exposes visible ingredient slots for tutorial arrows", stationShelf.Contains("public Control? GetVisibleIngredientSlot(string itemId)"));
        AssertTrue("Ingredient preparation tray exposes selection, preparation, and Raw button hooks",
            preparationTray.Contains("IngredientSelectedEventHandler") &&
            preparationTray.Contains("IngredientPreparedEventHandler") &&
            preparationTray.Contains("public Button? GetPreparationButton(string preparationId)"));
        AssertTrue("Station customer panel exposes serving hooks and Mother tutorial lines",
            stationCustomerPanel.Contains("PotionSelectedForServingEventHandler") &&
            stationCustomerPanel.Contains("public Control? GetServingDropBox()") &&
            stationCustomerPanel.Contains("public Button? GetServeButton()") &&
            stationCustomerPanel.Contains("ShowTutorialMotherLine") &&
            stationCustomerPanel.Contains("MotherPostServeDialogueResolvedEventHandler"));
    }

    private static void TestTutorialMotherPostServeDialogueReplacesNextCustomer()
    {
        var controller = ReadProjectFile("Scripts/Controllers/TutorialController.cs");
        var stateMachine = ReadProjectFile("Scripts/Tutorial/TutorialStateMachine.cs");
        var stepIds = ReadProjectFile("Scripts/Tutorial/TutorialStepId.cs");
        var tutorialContent = ReadProjectFile("Scripts/Tutorial/TutorialContentResource.cs");
        var stationCustomerPanel = ReadProjectFile("Scripts/UI/StationCustomerPanel.cs");
        var dayController = ReadProjectFile("Scripts/Controllers/DayController.cs");
        var scenePaths = ReadProjectFile("Scripts/Infrastructure/ScenePaths.cs");
        var cutscene = ReadProjectFile("Scripts/UI/TenYearsLaterCutscene.cs");
        var womanInGreenCutscene = ReadProjectFile("Scripts/UI/WomanInGreenCutscene.cs");
        var juniperGathering = ReadProjectFile("Scripts/UI/JuniperGathering.cs");

        AssertTrue("Tutorial defines an explicit post-serve Mother dialogue step",
            stepIds.Contains("PostServeMotherDialogue = 22") &&
            tutorialContent.Contains("StepId = (int)TutorialStepId.PostServeMotherDialogue") &&
            tutorialContent.Contains("Answer Mother in the customer dialog box."));
        AssertTrue("Tutorial state machine moves from serving Mother into the Mother dialogue",
            stateMachine.Contains("TutorialTransition.To(TutorialStepId.PostServeMotherDialogue)") &&
            stateMachine.Contains("EvaluateMotherPostServeDialogueResolved(") &&
            stateMachine.Contains("step == TutorialStepId.PostServeMotherDialogue") &&
            stateMachine.Contains("TutorialTransition.Complete()"));
        AssertTrue("Day controller replaces the post-serve completion with the ten years later cutscene",
            dayController.Contains("ShouldStartTenYearsLaterCutscene()") &&
            dayController.Contains("_openingMotherServeSucceededForCutscene") &&
            dayController.Contains("OpeningMotherPotionItemId = \"potion_gravekeepers_balm\"") &&
            dayController.Contains("CloseShopDayForStoryCutscene()") &&
            dayController.Contains("ChangeSceneWithFade(ScenePaths.TenYearsLaterCutscene)") &&
            scenePaths.Contains("res://Scenes/UI/TenYearsLaterCutscene.tscn"));
        AssertTrue("Tutorial no longer drives the ambiguous next-customer branch",
            !controller.Contains("SeedNextCustomerTutorialInventory()") &&
            !controller.Contains("ForceNextCustomerInteraction(_tutorialContent.AmbiguousTutorialCustomerId)") &&
            !stateMachine.Contains("IsCustomerInteractionMatch(interactionId, _ambiguousCustomerId)") &&
            !stateMachine.Contains("IsCustomerInteractionMatch(activeCustomerRequestId ?? string.Empty, _ambiguousCustomerId)"));
        AssertTrue("Station customer panel owns the requested Mother post-serve dialogue copy",
            stationCustomerPanel.Contains("MotherPostServeDialogueResolvedEventHandler") &&
            stationCustomerPanel.Contains("Thank you so much {GetPlayerNameForMotherDialogue()}.") &&
            stationCustomerPanel.Contains("Are you going to tell me what's wrong?") &&
            stationCustomerPanel.Contains("It's okay Ma. Here you need to get back to bed and rest.") &&
            stationCustomerPanel.Contains("I told you not to worry about it. Everything is fine") &&
            stationCustomerPanel.Contains("Thank you dear."));
        AssertTrue("Station customer panel delays resolving Mother until the post-serve dialogue finishes",
            stationCustomerPanel.Contains("TryBeginMotherPostServeDialogue(interaction, saleResult.IsSuccess)") &&
            stationCustomerPanel.Contains("FinishMotherPostServeDialogue") &&
            stationCustomerPanel.Contains("EmitSignal(SignalName.MotherPostServeDialogueResolved)") &&
            stationCustomerPanel.Contains("BeginResolveActiveCustomer();"));
        AssertTrue("DayController closes the shop after opening Mother so no next customer enters",
            dayController.Contains("_stationCustomerPanel.PotionSold += OnStationPotionSold;") &&
            dayController.Contains("OpeningMotherInteractionId = \"customer_requests_opening_gravekeepers_balm\"") &&
            dayController.Contains("_gameState.ActiveCustomerInteractionId") &&
            dayController.Contains("RequestCloseShopAfterCurrentCustomer();") &&
            dayController.Contains("CloseShopDayForStoryCutscene()"));
        AssertTrue("Ten years later cutscene owns the requested bridge into juniper picking",
            cutscene.Contains("10 Years Later") &&
            cutscene.Contains("Mother is brewing in the kitchen.") &&
            cutscene.Contains("Really?? Fun!") &&
            cutscene.Contains("You've never let me come juniper picking before?") &&
            cutscene.Contains("RecordTenYearsLaterCutsceneStarted();") &&
            cutscene.Contains("RecordTenYearsLaterCutsceneCompleted();") &&
            cutscene.Contains("ChangeSceneWithFade(ScenePaths.JuniperGathering)"));
        AssertTrue("Juniper gathering bridges the time-skip path into the woman in green cutscene",
            juniperGathering.Contains("ShouldShowWomanInGreenCutscene()") &&
            juniperGathering.Contains("TenYearsLaterCutsceneCompletedStoryFlag") &&
            juniperGathering.Contains("WomanInGreenCutsceneCompletedStoryFlag") &&
            juniperGathering.Contains("ChangeSceneWithFade(ScenePaths.WomanInGreenCutscene)"));
        AssertTrue("Woman in green cutscene owns the requested post-picking story beat",
            womanInGreenCutscene.Contains("After picking the juniper berries, you both walk home.") &&
            womanInGreenCutscene.Contains("By the river, you saw the woman in green.") &&
            womanInGreenCutscene.Contains("\\\"Do not speak to her.\\\"") &&
            womanInGreenCutscene.Contains("RecordWomanInGreenCutsceneStarted();") &&
            womanInGreenCutscene.Contains("RecordWomanInGreenCutsceneCompleted();") &&
            womanInGreenCutscene.Contains("ChangeSceneWithFade(ScenePaths.Main)"));
    }

    private static void TestTutorialRawPrepButtonStaysEnabled()
    {
        var controller = ReadProjectFile("Scripts/Controllers/TutorialController.cs");
        var preparationTray = ReadProjectFile("Scripts/UI/IngredientPreparationTray.cs").Replace("\r\n", "\n");
        var methodStart = preparationTray.IndexOf("public bool TrySelectIngredientFromInventory(string itemId)", StringComparison.Ordinal);
        var methodEnd = preparationTray.IndexOf("\n\tpublic Control? GetPreparationDropBox()", methodStart, StringComparison.Ordinal);
        var methodBody = methodStart >= 0 && methodEnd > methodStart
            ? preparationTray[methodStart..methodEnd]
            : string.Empty;

        var statusIndex = methodBody.IndexOf("SetStatus(DefaultStatusText);", StringComparison.Ordinal);
        var emitIndex = methodBody.IndexOf("EmitSignal(SignalName.IngredientSelected, itemId);", StringComparison.Ordinal);
        var refreshIndex = methodBody.IndexOf("Refresh();", emitIndex >= 0 ? emitIndex : 0, StringComparison.Ordinal);

        AssertTrue("IngredientPreparationTray refreshes preparation buttons after tutorial selection locks advance",
            statusIndex >= 0 &&
            emitIndex > statusIndex &&
            refreshIndex > emitIndex);
        AssertTrue("IngredientPreparationTray never disables the Raw preparation button",
            preparationTray.Contains("IngredientPreparationCatalog.RawPreparationId") &&
            preparationTray.Contains("button.Disabled = isRawPreparation ? false : !hasSelection || !preparationEnabled;"));
        AssertTrue("TutorialController keeps Raw enabled through tutorial button locks",
            controller.Contains("BuildAllowedButtonsWithRawPreparation(allowedButtons)") &&
            controller.Contains("KeepRawPreparationButtonEnabled();") &&
            controller.Contains("rawButton.Disabled = false;"));
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
}
