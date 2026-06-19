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
        runner.Run("Tutorial next-customer inventory seed stays curated", TestTutorialNextCustomerInventorySeedIsCurated);
        runner.Run("Tutorial next-customer step accepts tiered customer ids", TestTutorialNextCustomerStepAcceptsTieredCustomerIds);
        runner.Run("Tutorial sale review feedback uses request wording", TestTutorialSaleReviewFeedbackUsesRequestWording);
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

        AssertTrue("TutorialController uses extracted state machine", controller.Contains("private TutorialStateMachine _stateMachine"));
        AssertTrue("TutorialController uses extracted overlay presenter", controller.Contains("private TutorialOverlayPresenter _overlayPresenter"));
        AssertTrue("TutorialController uses extracted interaction gate", controller.Contains("private readonly TutorialInteractionGate _interactionGate"));
        AssertTrue("TutorialController consumes tutorial content resource", controller.Contains("[Export] public TutorialContentResource TutorialContent"));
        AssertTrue("TutorialController uses station potion sold events for the sale review step", controller.Contains("_stationCustomerPanel.PotionSold += OnPotionSold;"));
        AssertTrue("TutorialController no longer caches sale score details for tutorial feedback", !controller.Contains("_lastTutorialSaleScore") && !controller.Contains("_lastTutorialSaleGrade"));
        AssertTrue("TutorialController resolves step-specific button locks", controller.Contains("UpdateTutorialButtonLock("));
        AssertTrue("TutorialController includes the close shop tutorial step", controller.Contains("TutorialStepId.CloseShop"));
        AssertTrue("TutorialController highlights the close shop button", controller.Contains("case TutorialStepId.CloseShop") && controller.Contains("GetNextCustomerButton()"));
        AssertTrue("TutorialController forces the final tutorial customer to end the shop day", controller.Contains("ForceCloseShopAfterCurrentCustomerForTutorial()"));
        AssertTrue("TutorialController caches HUD date control for tutorial highlighting", controller.Contains("HudDateControlPath = new(\"Content/Status/Day\")") && controller.Contains("_hudDateControl = GetOptionalHudControl(HudDateControlPath"));
        AssertTrue("TutorialController does not cache a HUD shop timer label", !controller.Contains("HudShopTimerLabelPath") && !controller.Contains("_hudShopTimerLabel"));
        AssertTrue("TutorialController starts with the brewing station view already available", !controller.Contains("OpenBrewPanelButtonPath") && !controller.Contains("_openBrewPanelButton"));
        AssertTrue("TutorialController highlights ingredient queue steps with the brew panel", controller.Contains("ShowIngredientQueueStep(stepContent, _tutorialContent.MintId)") && controller.Contains("ShowForTargets(") && controller.Contains("FocusTutorialBrewPanel()"));
        AssertTrue("TutorialController routes the sale review popup through the station customer panel", controller.Contains("ShowForTarget(") && controller.Contains("_stationCustomerPanel,") && controller.Contains("BuildSaleResultBody("));
        AssertTrue("TutorialController seeds the next-customer tutorial inventory", controller.Contains("SeedNextCustomerTutorialInventory()"));
        AssertTrue("TutorialController highlights status step with a combined HUD rect", controller.Contains("ShowForHighlightRect(stepContent, statusHighlightRect)"));
        AssertTrue("TutorialController builds a combined status highlight rectangle", controller.Contains("TryGetStatusHighlightRect(out var statusHighlightRect)"));
        AssertTrue("TutorialController marks the final tutorial customer before the final ingredient step", controller.Contains("AddTwoMoreSleepIngredients") && controller.Contains("ForceCloseShopAfterCurrentCustomerForTutorial()"));
        AssertTrue("TutorialController listens for day summary continue", controller.Contains("_daySummaryPanel.ContinuePressed += OnDaySummaryContinuePressed;"));
        AssertTrue("TutorialController highlights the day summary panel", controller.Contains("case TutorialStepId.DaySummary") && controller.Contains("_overlayPresenter.ShowForTarget(stepContent, _daySummaryPanel)"));
        AssertTrue("TutorialController allows the day summary continue button", controller.Contains("TutorialStepId.DaySummary => new BaseButton?[] { _daySummaryPanel?.GetContinueButton() }"));
        AssertTrue("TutorialController includes station customer and day summary panels in button locks", controller.Contains("new Node?[] { _hud, _brewPanel, _stationCustomerPanel, _daySummaryPanel }"));
        AssertTrue("TutorialOverlayPresenter supports direct highlight rectangles", presenter.Contains("ShowForHighlightRect("));

        AssertTrue("TutorialStateMachine is a pure class", stateMachine.Contains("public sealed class TutorialStateMachine"));
        AssertTrue("TutorialStateMachine clamps tutorial step", stateMachine.Contains("public TutorialStepId ClampStep(int rawStep)"));
        AssertTrue("TutorialStateMachine removed the timer-driven close shop prompt", !stateMachine.Contains("EvaluateCloseShopPrompt("));
        AssertTrue("TutorialStateMachine advances from the final customer to day summary when the shop closes", stateMachine.Contains("step == TutorialStepId.AddTwoMoreSleepIngredients || step == TutorialStepId.CloseShop") && stateMachine.Contains("TutorialTransition.To(TutorialStepId.DaySummary)"));
        AssertTrue("TutorialStateMachine completes after continuing from the day summary", stateMachine.Contains("EvaluateDaySummaryContinued(") && stateMachine.Contains("step == TutorialStepId.DaySummary"));
        AssertTrue("DayController exposes a tutorial-only close-after-current-customer helper", ReadProjectFile("Scripts/Controllers/DayController.cs").Contains("public void ForceCloseShopAfterCurrentCustomerForTutorial()"));
        AssertTrue("TutorialContentResource exists", tutorialContent.Contains("public partial class TutorialContentResource : Resource"));
        AssertTrue("TutorialContentResource includes the close shop step copy", tutorialContent.Contains("StepId = (int)TutorialStepId.CloseShop"));
        AssertTrue("TutorialContentResource tells the player to close the shop without night events", tutorialContent.Contains("Close the shop to end the day.") && !tutorialContent.Contains("It is night time."));
        AssertTrue("TutorialContentResource includes the day summary step copy", tutorialContent.Contains("StepId = (int)TutorialStepId.DaySummary") && tutorialContent.Contains("Click Continue to start the next day."));
        AssertTrue("DaySummaryPanel exposes the continue button for tutorial locks", ReadProjectFile("Scripts/UI/DaySummaryPanel.cs").Contains("public Button? GetContinueButton()"));
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
            source.Contains("(\"elder\", 1)"));
        AssertTrue("Next-customer inventory includes the calm trait ingredient",
            source.Contains("(\"heather\", 1)"));
        AssertTrue("Next-customer inventory includes the dreams trait ingredient",
            source.Contains("(\"rosemary\", 1)"));
        AssertTrue("Next-customer inventory is seeded through a dedicated helper",
            source.Contains("public void SeedNextCustomerTutorialInventory()"));
        AssertTrue("Next-customer inventory clears the inventory before seeding",
            source.Contains("_inventoryState.Clear();"));
        AssertTrue("Next-customer inventory seeds exactly the curated ingredient list",
            source.Contains("foreach (var (itemId, qty) in NextCustomerTutorialInventory)"));
    }

    private static void TestTutorialNextCustomerStepAcceptsTieredCustomerIds()
    {
        var stateMachine = ReadProjectFile("Scripts/Tutorial/TutorialStateMachine.cs");

        AssertTrue("Next-customer transition uses customer interaction matching",
            stateMachine.Contains("EvaluateCustomerInteractionShown") &&
            stateMachine.Contains("IsCustomerInteractionMatch(interactionId, _ambiguousCustomerId)"));
        AssertTrue("Next-customer active request fallback uses customer interaction matching",
            stateMachine.Contains("EvaluateAmbiguousCustomerState") &&
            stateMachine.Contains("IsCustomerInteractionMatch(activeCustomerRequestId ?? string.Empty, _ambiguousCustomerId)"));
        AssertTrue("Tutorial customer matching accepts tiered suffix ids for legacy tutorial ids",
            stateMachine.Contains("NormalizeLegacyCustomerRequestId") &&
            stateMachine.Contains("const string legacyPrefix = \"customer_requests_\"") &&
            stateMachine.Contains("actualInteractionId.EndsWith(\"_\" + normalizedExpectedId, StringComparison.OrdinalIgnoreCase)"));
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
}
