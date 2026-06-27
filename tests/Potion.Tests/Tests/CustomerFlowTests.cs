using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using OccultShop.Models;
using OccultShop.Systems;
using OccultShop.UI;
using static ProjectFileTestHelper;
using static TestAssert;
using static UiReflectionTestHelper;

internal static class CustomerFlowTests
{
    public static void Register(TestRunner runner)
    {
        runner.Run("Customer flow creates detached ingredient snapshots", TestCustomerPanelBuildPotionIngredientDef);
        runner.Run("Customer events randomize shop-day order", TestCustomerEventControllerRandomizesOrder);
        runner.Run("Shop summary clears station customer presentation", TestShopSummaryClearsCustomerPresentation);
        runner.Run("Shop day waits for End Day before showing summary", TestShopDayWaitsForEndDayBeforeSummary);
        runner.Run("Active customer request is owned by station panel", TestActiveCustomerRequestKeepsShopFrontCustomerClickable);
        runner.Run("Active customer request persists across scene reloads", TestActiveCustomerRequestPersistsAcrossSceneReloads);
        runner.Run("Opening mother customer starts after intro", TestOpeningMotherCustomerStartsAfterIntro);
        runner.Run("Forced customer fallback resolves legacy ids deterministically", TestForcedCustomerFallbackResolvesLegacyIdsDeterministically);
        runner.Run("Customer events respect scheduling and story outcomes", TestCustomerEventSchedulingAndStoryOutcomes);
        runner.Run("Customer trait thresholds are enforced", TestCustomerTraitThresholdsAreEnforced);
        runner.Run("Customer trait ranges are enforced", TestCustomerTraitRangesAreEnforced);
        runner.Run("Customer exact potion requirements are enforced", TestCustomerExactPotionRequirementsAreEnforced);
        runner.Run("Active customer catalog includes trait threshold requests", TestActiveCustomerCatalogIncludesTraitThresholdRequests);
        runner.Run("Tiered customer data is an early bounded trait catalog", TestTieredCustomerDataIsEarlyFlexibleTraitCatalog);
        runner.Run("Customer dialogue markup converts safe syntax", TestCustomerDialogueMarkupConvertsSafeSyntax);
        runner.Run("Story customer dialogue trees support selling mode", TestStoryCustomerDialogueTreesSupportSellingMode);
        runner.Run("Customer outcomes update reputation and relationships", TestCustomerOutcomesUpdateStoryState);
        runner.Run("Dialogue tree editor addon is registered", TestDialogueTreeEditorAddonIsRegistered);
        runner.Run("StationCustomerPanel renders dialogue node text as narration", TestCustomerPanelRendersDialogueNodeTextAsNarration);
        runner.Run("Customer dialogue uses narrative text presenter", TestCustomerDialogueUsesNarrativeTextPresenter);
        runner.Run("StationCustomerPanel exposes station potion sale slots", TestCustomerPanelShowsDraggablePotionSaleSlots);
        runner.Run("Customer request comparison text shows selected potion values", TestCustomerRequestComparisonTextShowsSelectedPotionValues);
        runner.Run("Customer drop box stays disabled until next customer", TestCustomerDropBoxDisablesAfterSale);
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
        var stationCustomerPanel = ReadProjectFile("Scripts/UI/StationCustomerPanel.cs");
        var customerDef = ReadProjectFile("Scripts/Models/CustomerInteractionDef.cs");
        var dataDb = ReadProjectFile("Scripts/Autoload/DataDb.cs");
        var customers = ReadProjectFile("Data/customers_data.tres");
        var tieredCustomers = ReadProjectFile("Data/customers_tiered_test_data.tres");

        AssertTrue("CustomerEventController no longer uses a fixed index walk", !source.Contains("_nextCustomerIndex"));
        AssertTrue("CustomerEventController keeps a randomized order buffer", source.Contains("_customerOrder"));
        AssertTrue("CustomerEventController randomizes the customer order", source.Contains("_random.Next("));
        AssertTrue("CustomerEventController resets the order at the start of a shop day", source.Contains("BeginShopDay()"));
        AssertTrue("Customer interactions expose authored scheduled story slots",
            customerDef.Contains("ScheduledStoryFlags") &&
            customerDef.Contains("ScheduledShopDay") &&
            customerDef.Contains("ScheduledArrivalIndex") &&
            customerDef.Contains("HasScheduledStorySlot"));
        AssertTrue("DataDb parses scheduled story slots from authored data",
            dataDb.Contains("ReadScheduledStoryFlags") &&
            dataDb.Contains("\"scheduledStoryFlags\"") &&
            dataDb.Contains("\"scheduledStoryFlag\"") &&
            dataDb.Contains("\"scheduledShopDay\"") &&
            dataDb.Contains("\"scheduledArrivalIndex\""));
        AssertTrue("CustomerEventController draws scheduled story customers from authored metadata",
            source.Contains("BuildScheduledCandidates") &&
            source.Contains("HasPendingScheduledStoryFlag") &&
            source.Contains("IsScheduledForCurrentShopSlot") &&
            source.Contains("ClearScheduledStoryFlags") &&
            source.Contains("interaction.ScheduledShopDay") &&
            source.Contains("interaction.ScheduledArrivalIndex") &&
            source.Contains("interaction.ScheduledStoryFlags") &&
            source.Contains("DrawScheduledStoryCustomerInteraction(DataDb db, GameState state, ShopSessionState shopSession)") &&
            source.Contains("TryDrawScheduledStoryCustomerInteraction(interactions, state, shopSession, out var scheduledStoryInteraction)") &&
            source.IndexOf("TryDrawForcedInteraction", StringComparison.Ordinal) < source.IndexOf("TryDrawScheduledStoryCustomerInteraction(interactions, state, shopSession, out var scheduledStoryInteraction)", StringComparison.Ordinal) &&
            source.IndexOf("TryDrawScheduledStoryCustomerInteraction(interactions, state, shopSession, out var scheduledStoryInteraction)", StringComparison.Ordinal) < source.IndexOf("var eligibleInteractions", StringComparison.Ordinal));
        AssertTrue("Authored customer data schedules the opening and day-two story customers",
            customers.Contains("\"scheduledStoryFlags\"") &&
            customers.Contains("\"scheduledShopDay\": 1") &&
            customers.Contains("\"scheduledShopDay\": 2") &&
            customers.Contains("\"scheduledArrivalIndex\": 0") &&
            customers.Contains("\"scheduledArrivalIndex\": 1") &&
            customers.Contains("\"scheduledArrivalIndex\": 2") &&
            customers.Contains("\"new_game_opening_customer_pending\"") &&
            customers.Contains("\"day_two_third_customer_pending\"") &&
            tieredCustomers.Contains("\"scheduledStoryFlags\"") &&
            tieredCustomers.Contains("\"scheduledShopDay\": 1") &&
            tieredCustomers.Contains("\"scheduledShopDay\": 2"));
        AssertTrue("DayController resets customer order when the shop opens", dayController.Contains("_customerEventController.BeginShopDay();"));
        AssertTrue("DayController caps shop-day customer arrivals at three",
            dayController.Contains("MaxCustomersPerShopDay = 3") &&
            dayController.Contains("_customersArrived >= MaxCustomersPerShopDay"));
        AssertTrue("DayController counts customer arrivals when a customer is shown",
            dayController.Contains("_shopSessionState.RecordShopDayCustomerArrived(interaction);") &&
            dayController.Contains("_customersArrived = _shopSessionState.ShopDayCustomersArrived;"));
        AssertTrue("DayController waits for explicit End Day after the final customer is resolved",
            dayController.Contains("ShouldCloseShopAfterCurrentCustomer()") &&
            dayController.Contains("MarkShopDayReadyToEnd();"));
        AssertTrue("StationCustomerPanel exposes active interaction state", stationCustomerPanel.Contains("HasActiveInteraction => ActiveCustomer is not null"));
        AssertTrue("StationCustomerPanel does not build visible customer queue controls",
            !stationCustomerPanel.Contains("Customer Queue") &&
            !stationCustomerPanel.Contains("QueueTitle") &&
            !stationCustomerPanel.Contains("Name = \"Queue\"") &&
            !stationCustomerPanel.Contains("BuildQueueLabel") &&
            !stationCustomerPanel.Contains("GetNextCustomerButton") &&
            !stationCustomerPanel.Contains("HasQueuedCustomers"));
        AssertTrue("DayController shows the next customer without preloading a station panel queue",
            dayController.Contains("TryShowNextCustomer()") &&
            !dayController.Contains("TryShowQueuedCustomers()"));
    }

    private static void TestShopSummaryClearsCustomerPresentation()
    {
        var dayController = ReadProjectFile("Scripts/Controllers/DayController.cs");
        var stationCustomerPanel = ReadProjectFile("Scripts/UI/StationCustomerPanel.cs");

        AssertTrue("DayController emits shop state changes after showing the day summary",
            dayController.Contains("_daySummaryPanel.ShowSummary(") &&
            dayController.Contains("EmitShopStateChanged();"));
        AssertTrue("DayController clears station customer state when the shop closes",
            dayController.Contains("_stationCustomerPanel.ClearCustomers();") &&
            dayController.Contains("_brewPanel.HidePanel();"));
        AssertTrue("StationCustomerPanel clears active request and serving controls when empty",
            stationCustomerPanel.Contains("_shopSessionState.ClearActiveCustomerRequest();") &&
            stationCustomerPanel.Contains("_title.Text = \"No customer waiting\"") &&
            stationCustomerPanel.Contains("SetServingControlsEnabled(false);"));
    }

    private static void TestShopDayWaitsForEndDayBeforeSummary()
    {
        var dayController = ReadProjectFile("Scripts/Controllers/DayController.cs");
        var hud = ReadProjectFile("Scripts/UI/Hud.cs");

        AssertTrue("DayController tracks a completed shop day without closing it immediately",
            dayController.Contains("public bool IsShopDayReadyToEnd => _isShopDayReadyToEnd;") &&
            dayController.Contains("private void MarkShopDayReadyToEnd()") &&
            dayController.Contains("IsPersistedShopDayReadyToEnd()") &&
            dayController.Contains("return _closeShopAfterCurrentCustomer;"));
        AssertTrue("Final customer resolution marks the shop day ready instead of showing the summary directly",
            dayController.Contains("_customersArrived >= MaxCustomersPerShopDay") &&
            dayController.Contains("MarkShopDayReadyToEnd();"));
        AssertTrue("HUD turns the shop button into the End Day action when the day is ready",
            hud.Contains("IsShopDayReadyToEnd") &&
            hud.Contains("\"End Day\"") &&
            hud.Contains("_dayController.EndDayAndRunNight();") &&
            hud.Contains("isShopOpen && !isShopDayReadyToEnd"));
    }

    private static void TestActiveCustomerRequestKeepsShopFrontCustomerClickable()
    {
        var stationCustomerPanel = ReadProjectFile("Scripts/UI/StationCustomerPanel.cs");

        AssertTrue("StationCustomerPanel publishes normal customer requests to ShopSessionState",
            stationCustomerPanel.Contains("_shopSessionState.SetActiveCustomerRequest(request);") &&
            stationCustomerPanel.Contains("interaction.BuildRequest()"));
        AssertTrue("StationCustomerPanel clears active requests for dialogue and empty states",
            stationCustomerPanel.Contains("TryShowDialogueStart(interaction)") &&
            stationCustomerPanel.Contains("_shopSessionState.ClearActiveCustomerRequest();") &&
            stationCustomerPanel.Contains("EnterPotionSellingMode"));
        AssertTrue("StationCustomerPanel gates serving while plot dialogue is active",
            stationCustomerPanel.Contains("private bool CanServeActiveCustomer()") &&
            stationCustomerPanel.Contains("return !HasActiveDialogueInteraction() || _sellingMode;"));
    }

    private static void TestActiveCustomerRequestPersistsAcrossSceneReloads()
    {
        var shopSessionState = ReadProjectFile("Scripts/Autoload/ShopSessionState.cs");
        var saveData = ReadProjectFile("Scripts/Persistence/SaveData.cs");
        var saveManager = ReadProjectFile("Scripts/Autoload/SaveGameManager.cs");
        var dayController = ReadProjectFile("Scripts/Controllers/DayController.cs");
        var stationCustomerPanel = ReadProjectFile("Scripts/UI/StationCustomerPanel.cs");
        var project = ReadProjectFile("project.godot");

        AssertTrue("ShopSessionState is the autoloaded owner for active shop session state",
            project.Contains("ShopSessionState=\"*res://Scripts/Autoload/ShopSessionState.cs\"") &&
            shopSessionState.Contains("public partial class ShopSessionState : Node") &&
            shopSessionState.Contains("IsShopDayOpen") &&
            shopSessionState.Contains("ActiveCustomerInteractionId") &&
            shopSessionState.Contains("RecordShopDayCustomerArrived") &&
            shopSessionState.Contains("EnsureActiveShopCustomerForRequest(ActiveCustomerRequest)"));
        AssertTrue("ShopSessionState snapshots active shop-day counters for scene reloads and saves",
            shopSessionState.Contains("BuildSnapshot()") &&
            shopSessionState.Contains("ApplySnapshot(ShopSessionSnapshot? snapshot)") &&
            shopSessionState.Contains("ShopDayCustomersServed") &&
            shopSessionState.Contains("ShopDaySuccessfulSales") &&
            shopSessionState.Contains("ShopDayFailedSales") &&
            shopSessionState.Contains("ShopDayGoldEarned") &&
            shopSessionState.Contains("ShopDayDreadChange") &&
            saveData.Contains("public ShopSessionSnapshot ShopSession { get; set; } = new();") &&
            saveData.Contains("public sealed class ShopSessionSnapshot") &&
            saveData.Contains("ShopDayDreadChange"));
        AssertTrue("SaveGameManager persists and restores the shop session snapshot",
            saveManager.Contains("ShopSessionStatePath") &&
            saveManager.Contains("_shopSessionState.BuildSnapshot()") &&
            saveManager.Contains("_shopSessionState.ApplySnapshot(saveData.ShopSession)"));
        AssertTrue("DayController defers active shop restore until scene UI nodes finish ready",
            dayController.Contains("Callable.From(RestoreShopDayState).CallDeferred();") &&
            dayController.Contains("_stationCustomerPanel.RestoreActiveCustomer(interaction);") &&
            dayController.Contains("EmitShopStateChanged();") &&
            dayController.Contains("if (TryShowNextCustomer())") &&
            dayController.Contains("CloseShopAndShowSummary();"));
        AssertTrue("StationCustomerPanel does not clear global request state during scene initialization",
            CountOccurrences(stationCustomerPanel, "ShowEmptyCustomerPresentation(clearActiveRequest: false);") >= 2 &&
            stationCustomerPanel.Contains("public void RestoreActiveCustomer(CustomerInteractionDef customer)") &&
            stationCustomerPanel.Contains("TryRestorePublishedRequest(interaction, emitShownSignal)"));
        AssertTrue("Explicit customer clearing still clears active request state",
            stationCustomerPanel.Contains("public void ClearCustomers()") &&
            stationCustomerPanel.Contains("ShowEmptyCustomerPresentation(clearActiveRequest: true);"));
    }

    private static void TestOpeningMotherCustomerStartsAfterIntro()
    {
        var gameState = ReadProjectFile("Scripts/Autoload/GameState.cs");
        var introCutscene = ReadProjectFile("Scripts/UI/IntroCutscene.cs");
        var dayController = ReadProjectFile("Scripts/Controllers/DayController.cs");
        var tieredCustomers = ReadProjectFile("Data/customers_tiered_test_data.tres");
        var recipes = ReadProjectFile("Data/potion_recipes_data.tres");

        AssertTrue("Intro cutscene records completion before entering the main scene",
            gameState.Contains("IntroCutsceneCompletedStoryFlag = \"intro_cutscene_completed\"") &&
            gameState.Contains("public void RecordIntroCutsceneCompleted()") &&
            introCutscene.Contains("_gameState.RecordIntroCutsceneCompleted();"));
        AssertTrue("DayController auto-starts only the pending opening shop day after the intro",
            dayController.Contains("ShouldAutoStartOpeningShopDay()") &&
            dayController.Contains("_gameState.HasStoryFlag(GameState.IntroCutsceneCompletedStoryFlag)") &&
            dayController.Contains("_gameState.HasStoryFlag(GameState.NewGameOpeningCustomerPendingStoryFlag)") &&
            dayController.Contains("StartShopDay();"));
        AssertTrue("Opening customer is Mother with a blank desired trait request and exact potion requirement",
            tieredCustomers.Contains("\"id\": \"customer_requests_opening_gravekeepers_balm\"") &&
            tieredCustomers.Contains("\"speaker\": \"Mother\"") &&
            tieredCustomers.Contains("I'm feeling a bit frail today and can't reach the ingredients on top of the shelf. Could you reach for them and brew me a Minor Healing potion please?") &&
            tieredCustomers.Contains("\"desiredTraits\": {}") &&
            tieredCustomers.Contains("\"requiredPotionItemId\": \"potion_gravekeepers_balm\"") &&
            tieredCustomers.Contains("\"requiredPotionDisplayName\": \"Minor Healing Potion\"") &&
            tieredCustomers.Contains("res://Assets/Characters/mother_placeholder.svg"));
        AssertTrue("Minor Healing Potion rewords the current Mint, Gorse, and Thyme tutorial recipe",
            recipes.Contains("\"id\": \"gravekeepers_balm\"") &&
            recipes.Contains("\"ingredientIds\": [\"mint\", \"gorse\", \"thyme\"]") &&
            recipes.Contains("\"name\": \"Minor Healing Potion\""));
        AssertTrue("Mother placeholder portrait asset exists",
            ReadProjectFile("Assets/Characters/mother_placeholder.svg").Contains(">mother<"));
    }

    private static void TestForcedCustomerFallbackResolvesLegacyIdsDeterministically()
    {
        var customerController = ReadProjectFile("Scripts/Controllers/CustomerEventController.cs");
        var authoredData = ReadProjectFile("Data/authored_data.tres");
        var customers = ReadProjectFile("Data/customers_data.tres");
        var tieredCustomers = ReadProjectFile("Data/customers_tiered_test_data.tres");

        AssertTrue("Forced customer resolver keeps exact ids authoritative",
            customerController.Contains("string.Equals(candidate.Id, forcedInteractionId, StringComparison.OrdinalIgnoreCase)"));
        AssertTrue("Forced customer resolver supports legacy customer request ids",
            customerController.Contains("NormalizeForcedInteractionId") &&
            customerController.Contains("const string legacyPrefix = \"customer_requests_\""));
        AssertTrue("Forced customer suffix fallback chooses the shortest matching id",
            customerController.Contains("shortestMatchIdLength") &&
            customerController.Contains("candidateIdLength < shortestMatchIdLength"));
        AssertTrue("Forced customer suffix fallback fails loudly on tied shortest matches",
            customerController.Contains("shortestMatchCount > 1") &&
            customerController.Contains("matched multiple equally specific candidates"));
        AssertTrue("Customer data contains legacy-prefixed customer request ids",
            customers.Contains("\"id\": \"customer_requests_sleep_draught\""));
        AssertTrue("Authored data may use tiered customer interactions",
            authoredData.Contains("CustomerInteractionsPath = \"res://Data/customers_tiered_test_data.tres\"") ||
            authoredData.Contains("CustomerInteractionsPath = \"res://Data/customers_data.tres\""));
    }

    private static void TestCustomerEventSchedulingAndStoryOutcomes()
    {
        var customerController = ReadProjectFile("Scripts/Controllers/CustomerEventController.cs");
        var dataDb = ReadProjectFile("Scripts/Autoload/DataDb.cs");
        var gameState = ReadProjectFile("Scripts/Autoload/GameState.cs");
        var saveData = ReadProjectFile("Scripts/Persistence/SaveData.cs");
        var requirements = ReadProjectFile("Scripts/Systems/Requirements.cs");
        var effects = ReadProjectFile("Scripts/Systems/EffectApplier.cs");
        var stationCustomerPanel = ReadProjectFile("Scripts/UI/StationCustomerPanel.cs");
        var saleService = ReadProjectFile("Scripts/Systems/CustomerSaleService.cs");
        var storyVisit = ReadProjectFile("Scripts/Models/StoryCustomerVisitRecord.cs");
        var customers = ReadProjectFile("Data/customers_data.tres");

        AssertTrue("Customer draws filter by requirements", customerController.Contains("Requirements.Met(state, interaction.Requires)"));
        AssertTrue("Customer draws use weighted selection", customerController.Contains("PickWeightedIndex"));
        AssertTrue("DataDb parses customer difficulty", dataDb.Contains("Difficulty = Math.Max(1, ReadInt(entry, \"difficulty\", 1))"));
        AssertTrue("DataDb parses customer outcome effects", dataDb.Contains("OnSuccessEffects = ParseEffects(ReadArray(entry, \"onSuccessEffects\"))"));
        AssertTrue("DataDb parses customer arrival effects", dataDb.Contains("OnArrivalEffects = ParseEffects(ReadArray(entry, \"onArrivalEffects\"))"));
        AssertTrue("DataDb parses item restock effects", dataDb.Contains("RestockItemId = ReadNullableString(entry, \"restockItemId\")"));
        AssertTrue("DataDb parses ingredient preparation method unlock effects", dataDb.Contains("EnableIngredientPreparationMethodId"));
        AssertTrue("DataDb parses hidden customer request details", dataDb.Contains("HideRequestDetails = ReadBool(entry, \"hideRequestDetails\")"));
        AssertTrue("DataDb parses day requirements", dataDb.Contains("DayMin = ReadNullableInt(entry, \"dayMin\")"));
        AssertTrue("DataDb parses story flag requirements", dataDb.Contains("HasStoryFlag = ReadNullableString(entry, \"hasStoryFlag\")"));
        AssertTrue("GameState stores story flags", gameState.Contains("HashSet<string> StoryFlags"));
        AssertTrue("GameState stores story customer visit records", gameState.Contains("StoryCustomerVisits"));
        AssertTrue("GameState records story customer arrivals", gameState.Contains("RecordStoryCustomerArrived"));
        AssertTrue("GameState records story customer outcomes", gameState.Contains("RecordStoryCustomerInteractionOutcome"));
        AssertTrue("Customer draws exclude story visits that already arrived", customerController.Contains("HasStoryCustomerVisitArrived(interaction)"));
        AssertTrue("Customer draws mark story customer arrivals", customerController.Contains("RecordStoryCustomerArrived(interaction)"));
        AssertTrue("Customer draws apply arrival effects", customerController.Contains("ApplyArrivalEffects(interaction, state)") && customerController.Contains("EffectApplier.Apply(state, effect)"));
        AssertTrue("Story customer visits persist in save snapshots", saveData.Contains("List<StoryCustomerVisitRecord> StoryCustomerVisits"));
        AssertTrue("Story customer visit records track arrival and outcome", storyVisit.Contains("HasArrived") && storyVisit.Contains("LastOutcome"));
        AssertTrue("Requirements check story flags", requirements.Contains("state.HasStoryFlag(req.HasStoryFlag!)"));
        AssertTrue("Effects can add story flags", effects.Contains("state.AddStoryFlag(e.AddStoryFlag!)"));
        AssertTrue("Effects can confiscate one of each ingredient",
            effects.Contains("state.ConsumeEachIngredient(ingredientQty)") &&
            gameState.Contains("public int ConsumeEachIngredient(int qty)") &&
            dataDb.Contains("ConsumeEachIngredientQty = ReadNullableInt(entry, \"consumeEachIngredientQty\")"));
        AssertTrue("Effects can restock items to a minimum quantity",
            effects.Contains("state.RestockItemToMinimum(e.RestockItemId!, e.RestockItemQty ?? 1)") &&
            gameState.Contains("public void RestockItemToMinimum(string itemId, int qty)") &&
            ReadProjectFile("Scripts/Systems/InventoryState.cs").Contains("RestockItemToMinimum(string itemId, int minimumQuantity)"));
        AssertTrue("Effects can enable a single ingredient preparation method",
            effects.Contains("state.SetIngredientPreparationMethodEnabled(e.EnableIngredientPreparationMethodId!, true)") &&
            gameState.Contains("public void SetIngredientPreparationMethodEnabled(string preparationId, bool enabled)") &&
            dataDb.Contains("enableIngredientPreparationMethodId"));
        AssertTrue("Preparation unlock effects reveal that preparation for current inventory ingredients",
            effects.Contains("state.UnlockIngredientPreparationForCurrentInventory(e.EnableIngredientPreparationMethodId!)") &&
            gameState.Contains("public void UnlockIngredientPreparationForCurrentInventory(string preparationId)") &&
            gameState.Contains("TryGetPreparationForCurrentInventoryItem(pair.Key, normalizedPreparationId, out var ingredientId)"));
        AssertTrue("Authored data validates arrival effects", ReadProjectFile("Scripts/Systems/AuthoredDataValidator.cs").Contains("interaction.OnArrivalEffects"));
        AssertTrue("Authored data validates ingredient preparation method unlock effects",
            ReadProjectFile("Scripts/Systems/AuthoredDataValidator.cs").Contains("effect.EnableIngredientPreparationMethodId") &&
            ReadProjectFile("Scripts/Systems/AuthoredDataValidator.cs").Contains("IngredientPreparationCatalog.IsKnownPreparationId"));
        AssertTrue("CustomerSaleService applies success and failure effects", saleService.Contains("ApplyOutcomeEffects(isSuccess ? interaction.OnSuccessEffects : interaction.OnFailureEffects)"));
        AssertTrue("CustomerSaleService applies refusal effects", saleService.Contains("interaction.OnPotionRefusedEffects.Count > 0") && saleService.Contains("interaction.OnSkipEffects"));
        AssertTrue("CustomerSaleService records story sale outcomes", saleService.Contains("StoryCustomerOutcomeSuccess") && saleService.Contains("StoryCustomerOutcomeFailure"));
        AssertTrue("StationCustomerPanel records terminal dialogue choices as story outcomes", stationCustomerPanel.Contains("RecordStoryCustomerInteractionOutcome(interaction, outcome)"));
        AssertTrue("Authored customer data includes early pool", customers.Contains("\"pool\": \"early\""));
        AssertTrue("Authored customer data gates early customers by day", customers.Contains("\"dayMax\": 4"));
        AssertTrue("Authored customer data includes recipe pool", customers.Contains("\"pool\": \"recipe\""));
    }

    private static void TestCustomerTraitThresholdsAreEnforced()
    {
        var request = new CustomerRequestDef
        {
            DesiredTraits = new Dictionary<string, CustomerTraitRangeDef>
            {
                ["mend"] = Range(min: 9)
            },
            RequiredMinTraits = new Dictionary<string, int>
            {
                ["mend"] = 9
            },
            RequiredMaxTraits = new Dictionary<string, int>
            {
                ["vigor"] = 1
            }
        };

        var passingResult = new PotionResult
        {
            Traits = new Dictionary<string, int>
            {
                ["mend"] = 9,
                ["vigor"] = 1
            }
        };
        AssertTrue("Potion satisfies min and max thresholds",
            CustomerSaleRules.IsRequestSatisfiedByPotion(request, passingResult, true));

        var weakResult = new PotionResult
        {
            Traits = new Dictionary<string, int>
            {
                ["mend"] = 8,
                ["vigor"] = 1
            }
        };
        AssertTrue("Potion below min threshold fails",
            !CustomerSaleRules.IsRequestSatisfiedByPotion(request, weakResult, true));

        var overactiveResult = new PotionResult
        {
            Traits = new Dictionary<string, int>
            {
                ["mend"] = 9,
                ["vigor"] = 2
            }
        };
        AssertTrue("Potion above max threshold fails",
            !CustomerSaleRules.IsRequestSatisfiedByPotion(request, overactiveResult, true));

        var quietResult = new PotionResult
        {
            Traits = new Dictionary<string, int>
            {
                ["mend"] = 9
            }
        };
        AssertTrue("Missing max-threshold trait counts as zero",
            CustomerSaleRules.IsRequestSatisfiedByPotion(request, quietResult, true));
    }

    private static void TestCustomerTraitRangesAreEnforced()
    {
        var request = new CustomerRequestDef
        {
            DesiredTraits = new Dictionary<string, CustomerTraitRangeDef>
            {
                ["calming"] = Range(min: 2, max: 4),
                ["clarity"] = Range(min: 1)
            },
            BadTraits = new Dictionary<string, CustomerTraitRangeDef>
            {
                ["drowsiness"] = Range(max: 1),
                ["confusion"] = Range(max: 0)
            }
        };

        var passingResult = new PotionResult
        {
            Traits = new Dictionary<string, int>
            {
                ["calming"] = 3,
                ["clarity"] = 1
            },
            Risks = new Dictionary<string, int>
            {
                ["drowsiness"] = 1
            }
        };
        AssertTrue("Potion inside desired and bad ranges succeeds",
            CustomerSaleRules.IsRequestSatisfiedByPotion(request, passingResult, true));

        var weakResult = new PotionResult
        {
            Traits = new Dictionary<string, int>
            {
                ["calming"] = 1,
                ["clarity"] = 1
            }
        };
        AssertTrue("Potion below desired min fails",
            !CustomerSaleRules.IsRequestSatisfiedByPotion(request, weakResult, true));

        var overStrongResult = new PotionResult
        {
            Traits = new Dictionary<string, int>
            {
                ["calming"] = 5,
                ["clarity"] = 1
            }
        };
        AssertTrue("Potion above desired max fails",
            !CustomerSaleRules.IsRequestSatisfiedByPotion(request, overStrongResult, true));

        var drowsyResult = new PotionResult
        {
            Traits = new Dictionary<string, int>
            {
                ["calming"] = 3,
                ["clarity"] = 1
            },
            Risks = new Dictionary<string, int>
            {
                ["drowsiness"] = 2
            }
        };
        AssertTrue("Potion above bad max fails",
            !CustomerSaleRules.IsRequestSatisfiedByPotion(request, drowsyResult, true));

        var confusedResult = new PotionResult
        {
            Traits = new Dictionary<string, int>
            {
                ["calming"] = 3,
                ["clarity"] = 1
            },
            Risks = new Dictionary<string, int>
            {
                ["confusion"] = 1
            }
        };
        AssertTrue("Potion with zero-tolerance bad risk fails",
            !CustomerSaleRules.IsRequestSatisfiedByPotion(request, confusedResult, true));
    }

    private static void TestCustomerExactPotionRequirementsAreEnforced()
    {
        var request = new CustomerRequestDef
        {
            RequiredPotionItemId = "potion_gravekeepers_balm",
            RequiredPotionDisplayName = "Minor Healing Potion"
        };
        var result = new PotionResult
        {
            Traits = new Dictionary<string, int>()
        };

        AssertTrue("Exact required potion succeeds",
            CustomerSaleRules.IsRequestSatisfiedByPotion("potion_gravekeepers_balm", request, result, true));
        AssertTrue("Different potion fails exact requirement",
            !CustomerSaleRules.IsRequestSatisfiedByPotion("potion_sleep_draught", request, result, true));

        var comparison = CustomerDialogueTextFormatter.BuildCustomerPotionRequestComparisonText(
            request,
            result.Traits,
            result.Risks,
            null,
            "potion_sleep_draught");
        AssertTrue("Exact potion requirement is visible in customer comparison",
            comparison.Contains("[color=#E64040]No match[/color] Required potion: Minor Healing Potion"));
    }

    private static void TestActiveCustomerCatalogIncludesTraitThresholdRequests()
    {
        var customerDef = ReadProjectFile("Scripts/Models/CustomerInteractionDef.cs");
        var dataDb = ReadProjectFile("Scripts/Autoload/DataDb.cs");
        var saleRules = ReadProjectFile("Scripts/Systems/CustomerSaleRules.cs");
        var stationCustomerPanel = ReadProjectFile("Scripts/UI/StationCustomerPanel.cs");
        var stationPresentation = ReadProjectFile("Scripts/UI/StationCustomerPotionPresentation.cs");
        var formatter = ReadProjectFile("Scripts/UI/CustomerDialogueTextFormatter.cs");
        var customers = ReadProjectFile("Data/customers_data.tres");
        var tieredCustomers = ReadProjectFile("Data/customers_tiered_test_data.tres");

        AssertTrue("Customer requests store hard trait thresholds",
            customerDef.Contains("RequiredMinTraits") &&
            customerDef.Contains("RequiredMaxTraits") &&
            customerDef.Contains("CustomerTraitRangeDef"));
        AssertTrue("Customer requests store desired and bad trait ranges",
            customerDef.Contains("Dictionary<string, CustomerTraitRangeDef> DesiredTraits") &&
            customerDef.Contains("Dictionary<string, CustomerTraitRangeDef> BadTraits"));
        AssertTrue("Customer requests can hide authored request details",
            customerDef.Contains("HideRequestDetails") &&
            formatter.Contains("HiddenRequestText"));
        AssertTrue("Customer requests can require an exact potion item without desired traits",
            customerDef.Contains("RequiredPotionItemId") &&
            customerDef.Contains("RequiredPotionDisplayName") &&
            dataDb.Contains("RequiredPotionItemId = ReadString(entry, \"requiredPotionItemId\")") &&
            saleRules.Contains("IsRequiredPotionSatisfied") &&
            formatter.Contains("AddRequiredPotionComparisonLine"));
        AssertTrue("DataDb parses desired and bad trait ranges",
            dataDb.Contains("ReadTraitRangeDictionary(entry, \"desiredTraits\", legacyIntIsMinimum: true)") &&
            dataDb.Contains("ReadTraitRangeDictionary(entry, \"badTraits\", legacyIntIsMinimum: false)"));
        AssertTrue("DataDb parses hard trait thresholds",
            dataDb.Contains("ReadStringIntDictionary(entry, \"requiredMinTraits\")") &&
            dataDb.Contains("ReadStringIntDictionary(entry, \"requiredMaxTraits\")"));
        AssertTrue("Sale rules enforce hard trait thresholds",
            saleRules.Contains("AreRequiredTraitThresholdsSatisfied") &&
            saleRules.Contains("AreRequiredMinTraitsSatisfied") &&
            saleRules.Contains("AreRequiredMaxTraitsSatisfied") &&
            saleRules.Contains("AreBadTraitRangesSatisfied"));
        AssertTrue("StationCustomerPanel displays hard trait thresholds and ranges through the shared formatter",
            stationCustomerPanel.Contains("StationCustomerPotionPresentation.BuildRequestFitText") &&
            stationPresentation.Contains("BuildCustomerPotionRequestComparisonText") &&
            formatter.Contains("FormatTraitRange") &&
            formatter.Contains("FormatBadTraitListWithViolations") &&
            formatter.Contains("FormatMinTraitThresholdsWithMatches") &&
            formatter.Contains("FormatMaxTraitThresholdsWithViolations"));
        AssertTrue("Active customer data includes six threshold customers",
            customers.Contains("\"id\": \"customer_requests_obsidian_stitch_draught\"") &&
            customers.Contains("\"id\": \"customer_requests_chapel_ward_ink\"") &&
            customers.Contains("\"id\": \"customer_requests_glass_truth_tonic\"") &&
            customers.Contains("\"id\": \"customer_requests_quick_hand_philter\"") &&
            customers.Contains("\"id\": \"customer_requests_mercy_tincture\"") &&
            customers.Contains("\"id\": \"customer_requests_muse_cordial\""));
        AssertTrue("Threshold customer data uses min and max trait gates",
            customers.Contains("\"requiredMinTraits\"") &&
            customers.Contains("\"requiredMaxTraits\"") &&
            customers.Contains("\"mend\": { \"min\": 9, \"max\": 12 }") &&
            customers.Contains("\"vigor\": 1"));
        AssertAllDesiredTraitRangesAreBounded("Data/customers_data.tres");
        AssertTrue("Customer data includes preparation puzzle requests in both catalogs",
            customers.Contains("\"id\": \"customer_requests_counterfeit_calm\"") &&
            customers.Contains("\"id\": \"customer_requests_grave_stitch_poultice\"") &&
            customers.Contains("\"id\": \"customer_requests_stage_door_spark\"") &&
            customers.Contains("\"id\": \"customer_requests_bitter_wake_cure\"") &&
            customers.Contains("\"id\": \"customer_requests_lantern_wash\"") &&
            tieredCustomers.Contains("\"id\": \"customer_requests_counterfeit_calm\"") &&
            tieredCustomers.Contains("\"id\": \"customer_requests_grave_stitch_poultice\"") &&
            tieredCustomers.Contains("\"id\": \"customer_requests_stage_door_spark\"") &&
            tieredCustomers.Contains("\"id\": \"customer_requests_bitter_wake_cure\"") &&
            tieredCustomers.Contains("\"id\": \"customer_requests_lantern_wash\""));
        AssertTrue("Customer request text can display preparation requirements",
            formatter.Contains("FormatIngredientPortionRequirement") &&
            formatter.Contains("IngredientPreparationCatalog.GetDisplayName") &&
            formatter.Contains("prep"));
    }

    private static void TestTieredCustomerDataIsEarlyFlexibleTraitCatalog()
    {
        var tieredCustomers = ReadProjectFile("Data/customers_tiered_test_data.tres");
        var catalog = ReadProjectFile("Data/customers_tiered_test_catalog.md");
        var interactions = ReadAuthoredCustomerInteractions("Data/customers_tiered_test_data.tres");

        var dayOneGateCount = CountOccurrences(tieredCustomers, "\"dayMin\": 1");
        var dayTwoGateCount = CountOccurrences(tieredCustomers, "\"dayExact\": 2");
        AssertTrue("Tiered customer data only gates scripted requests to day one or scripted day-two customers",
            !tieredCustomers.Contains("\"dayMax\"") &&
            dayTwoGateCount == 3 &&
            CountOccurrences(tieredCustomers, "\"requires\":") == dayOneGateCount + dayTwoGateCount);
        AssertTrue("Tiered customer data avoids hard ingredient and prep locks",
            !tieredCustomers.Contains("\"requiredIngredientAmounts\"") &&
            !tieredCustomers.Contains("\"requiredMinTraits\"") &&
            !tieredCustomers.Contains("\"requiredMaxTraits\""));
        AssertAllDesiredTraitRangesAreBounded("Data/customers_tiered_test_data.tres");
        AssertTrue("Tiered customer data uses current ingredient traits",
            tieredCustomers.Contains("\"calm\"") &&
            tieredCustomers.Contains("\"dream\"") &&
            tieredCustomers.Contains("\"soothe\"") &&
            tieredCustomers.Contains("\"cleanse\"") &&
            tieredCustomers.Contains("\"rest\"") &&
            tieredCustomers.Contains("\"vigor\"") &&
            tieredCustomers.Contains("\"mend\"") &&
            tieredCustomers.Contains("\"charm\"") &&
            tieredCustomers.Contains("\"clarity\"") &&
            tieredCustomers.Contains("\"courage\""));
        AssertTrue("Tiered customer data uses current prep risks",
            tieredCustomers.Contains("\"drowsiness\"") &&
            tieredCustomers.Contains("\"melancholy\"") &&
            tieredCustomers.Contains("\"corruption\"") &&
            tieredCustomers.Contains("\"insomnia\""));
        AssertTrue("Tiered customer data includes the deterministic opening request",
            tieredCustomers.Contains("\"id\": \"customer_requests_opening_gravekeepers_balm\"") &&
            tieredCustomers.Contains("\"title\": \"Mother\"") &&
            tieredCustomers.Contains("\"desiredTraits\": {}") &&
            tieredCustomers.Contains("\"requiredPotionItemId\": \"potion_gravekeepers_balm\"") &&
            tieredCustomers.Contains("\"requiredPotionDisplayName\": \"Minor Healing Potion\"") &&
            catalog.Contains("Deterministic first shop customer"));
        AssertTrue("Tiered customer data includes the deterministic second request and arrival grant",
            tieredCustomers.Contains("\"id\": \"customer_requests_opening_silver_focus_tonic\"") &&
            HasDesiredTraitRange(interactions, "customer_requests_opening_silver_focus_tonic", "clarity", 4, 4) &&
            HasDesiredTraitRange(interactions, "customer_requests_opening_silver_focus_tonic", "courage", 3, 3) &&
            HasDesiredTraitRange(interactions, "customer_requests_opening_silver_focus_tonic", "vigor", 3, 3) &&
            tieredCustomers.Contains("\"onArrivalEffects\"") &&
            tieredCustomers.Contains("\"addItemId\": \"comfrey\"") &&
            tieredCustomers.Contains("\"addItemId\": \"willow\"") &&
            tieredCustomers.Contains("\"addItemId\": \"yarrow\"") &&
            catalog.Contains("Deterministic second shop customer"));
        AssertTrue("Tiered customer data includes the deterministic third request and restock grant",
            tieredCustomers.Contains("\"id\": \"customer_requests_opening_clean_vigor_tonic\"") &&
            HasDesiredTraitRange(interactions, "customer_requests_opening_clean_vigor_tonic", "cleanse", 4, 4) &&
            HasDesiredTraitRange(interactions, "customer_requests_opening_clean_vigor_tonic", "soothe", 4, 4) &&
            HasDesiredTraitRange(interactions, "customer_requests_opening_clean_vigor_tonic", "vigor", 3, 3) &&
            tieredCustomers.Contains("\"restockItemId\": \"mint\"") &&
            tieredCustomers.Contains("\"restockItemId\": \"gorse\"") &&
            tieredCustomers.Contains("\"restockItemId\": \"thyme\"") &&
            tieredCustomers.Contains("\"restockItemId\": \"comfrey\"") &&
            tieredCustomers.Contains("\"restockItemId\": \"willow\"") &&
            tieredCustomers.Contains("\"restockItemId\": \"yarrow\"") &&
            tieredCustomers.Contains("\"restockItemQty\": 5") &&
            catalog.Contains("Deterministic third shop customer"));
        AssertTrue("Tiered customer data includes the deterministic day-two first request",
            tieredCustomers.Contains("\"id\": \"customer_requests_day_two_charmed_focus_tonic\"") &&
            tieredCustomers.Contains("\"dayExact\": 2") &&
            tieredCustomers.Contains("\"hasStoryFlag\": \"day_two_first_customer_pending\"") &&
            HasDesiredTraitRange(interactions, "customer_requests_day_two_charmed_focus_tonic", "charm", 4, 4) &&
            HasDesiredTraitRange(interactions, "customer_requests_day_two_charmed_focus_tonic", "courage", 3, 3) &&
            HasDesiredTraitRange(interactions, "customer_requests_day_two_charmed_focus_tonic", "vigor", 3, 3) &&
            catalog.Contains("Deterministic first customer on day 2"));
        AssertTrue("Tiered customer data includes the hidden deterministic day-two second request",
            tieredCustomers.Contains("\"id\": \"customer_requests_day_two_crowded_head_tonic\"") &&
            tieredCustomers.Contains("Please help me. My head feels crowded, my stomach feels off, and I can't focus.") &&
            tieredCustomers.Contains("\"dayExact\": 2") &&
            tieredCustomers.Contains("\"hasStoryFlag\": \"day_two_second_customer_pending\"") &&
            tieredCustomers.Contains("\"hideRequestDetails\": true") &&
            HasDesiredTraitRange(interactions, "customer_requests_day_two_crowded_head_tonic", "cleanse", 7, 7) &&
            HasDesiredTraitRange(interactions, "customer_requests_day_two_crowded_head_tonic", "soothe", 5, 5) &&
            HasDesiredTraitRange(interactions, "customer_requests_day_two_crowded_head_tonic", "clarity", 4, 4) &&
            CountOccurrences(tieredCustomers, "\"enableIngredientPreparationMethodId\": \"boiled\"") >= 2 &&
            catalog.Contains("Enables Boiled prep method when served") &&
            catalog.Contains("desired request details display as `?????`"));
        AssertTrue("Tiered customer data includes the deterministic day-two third rest memory clarity request",
            tieredCustomers.Contains("\"id\": \"customer_requests_day_two_rest_memory_clarity\"") &&
            tieredCustomers.Contains("\"hasStoryFlag\": \"day_two_third_customer_pending\"") &&
            HasDesiredTraitRange(interactions, "customer_requests_day_two_rest_memory_clarity", "rest", 5, null) &&
            HasDesiredTraitRange(interactions, "customer_requests_day_two_rest_memory_clarity", "memory", 5, null) &&
            HasDesiredTraitRange(interactions, "customer_requests_day_two_rest_memory_clarity", "clarity", 4, null) &&
            HasBadTraitRange(interactions, "customer_requests_day_two_rest_memory_clarity", "melancholy", null, 0) &&
            HasBadTraitRange(interactions, "customer_requests_day_two_rest_memory_clarity", "insomnia", null, 0) &&
            catalog.Contains("Deterministic third customer on day 2"));
        AssertTrue("Tiered customer data omits old unsupported traits and risks",
            !tieredCustomers.Contains("\"confusion\"") &&
            !tieredCustomers.Contains("\"nausea\"") &&
            !tieredCustomers.Contains("\"ward\"") &&
            !tieredCustomers.Contains("\"honesty\"") &&
            !tieredCustomers.Contains("\"reflexes\"") &&
            !tieredCustomers.Contains("\"empathy\"") &&
            !tieredCustomers.Contains("\"creativity\""));
        AssertTrue("Tiered customer catalog documents the early flexible request design",
            catalog.Contains("Most entries in this catalog are available from day 1") &&
            catalog.Contains("deterministic day-two opener") &&
            catalog.Contains("requests avoid hard `requiredIngredientAmounts`") &&
            catalog.Contains("multiple successful recipes"));
    }

    private static void TestCustomerDialogueMarkupConvertsSafeSyntax()
    {
        var converted = CustomerDialogueMarkupConverter.ConvertToBbCode(
            "Her {i|hand} is {shake|unsteady}. {pause:0.4}{speed:20}{color:gold|Listen.}");

        AssertTrue("Safe italic syntax converts to BBCode", converted.BbCode.Contains("[i]hand[/i]"));
        AssertTrue("Safe shake syntax converts to BBCode", converted.BbCode.Contains("[shake rate=18.0 level=3 connected=1]unsteady[/shake]"));
        AssertTrue("Named color syntax converts to BBCode", converted.BbCode.Contains("[color=#F5D76E]Listen.[/color]"));
        AssertEqual("Markup plain text strips style commands", "Her hand is unsteady. Listen.", converted.PlainText);
        AssertEqual("Markup conversion records pause and speed commands", 2, converted.Commands.Count);
        AssertTrue("First command is pause", converted.Commands[0].Kind == NarrativeTextCommandKind.Pause);
        AssertTrue("Second command is speed", converted.Commands[1].Kind == NarrativeTextCommandKind.Speed);

        var escaped = CustomerDialogueMarkupConverter.ConvertToBbCode("[shake]literal[/shake]");
        AssertEqual("Raw BBCode is escaped", "[lb]shake[rb]literal[lb]/shake[rb]", escaped.BbCode);

        var unknown = CustomerDialogueMarkupConverter.ConvertToBbCode("A {ghost|word} stays plain.");
        AssertTrue("Unknown styles are preserved as visible text", unknown.PlainText.Contains("{ghost|word}"));
        AssertTrue("Unknown styles produce a warning", unknown.Warnings.Count > 0);
    }

    private static void TestStoryCustomerDialogueTreesSupportSellingMode()
    {
        var customerDef = ReadProjectFile("Scripts/Models/CustomerInteractionDef.cs");
        var dataDb = ReadProjectFile("Scripts/Autoload/DataDb.cs");
        var stationCustomerPanel = ReadProjectFile("Scripts/UI/StationCustomerPanel.cs");
        var customerTextFormatter = ReadProjectFile("Scripts/UI/CustomerDialogueTextFormatter.cs");
        var presenter = ReadProjectFile("Scripts/UI/Text/NarrativeTextPresenter.cs");
        var validator = ReadProjectFile("Scripts/Systems/AuthoredDataValidator.cs");
        var dayController = ReadProjectFile("Scripts/Controllers/DayController.cs");
        var saleService = ReadProjectFile("Scripts/Systems/CustomerSaleService.cs");
        var gameState = ReadProjectFile("Scripts/Autoload/GameState.cs");
        var storyVisitState = ReadProjectFile("Scripts/Systems/StoryCustomerVisitState.cs");
        var storyVisit = ReadProjectFile("Scripts/Models/StoryCustomerVisitRecord.cs");
        var dialogueSession = ReadProjectFile("Scripts/Dialogue/DialogueSession.cs");
        var dialogueGraph = ReadProjectFile("Scripts/Dialogue/DialogueGraph.cs");
        var customerDialogueAdapter = ReadProjectFile("Scripts/Systems/CustomerDialogueAdapter.cs");
        var customerDialogueGraphBuilder = ReadProjectFile("Scripts/Systems/CustomerDialogueGraphBuilder.cs");
        var tieredCustomers = ReadProjectFile("Data/customers_tiered_test_data.tres");

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
        AssertTrue("Dialogue options can reveal a request and return to dialogue",
            customerDef.Contains("RevealsRequest") &&
            customerDef.Contains("ReturnNodeId"));
        AssertTrue("Dialogue options can apply authored effects",
            customerDef.Contains("List<EffectDef> Effects"));
        AssertTrue("Customer interactions define authored potion responses",
            customerDef.Contains("List<CustomerPotionResponseDef> PotionResponses") &&
            customerDef.Contains("MinMatchedDesiredTraits") &&
            customerDef.Contains("MaxMatchedBadTraits"));
        AssertTrue("Customer authored text supports structured narration and named speakers",
            customerDef.Contains("public sealed class CustomerDialogueLineDef") &&
            customerDef.Contains("public string Speaker") &&
            customerDef.Contains("public string CharacterImageKey") &&
            customerDef.Contains("CharacterImagePaths") &&
            customerDef.Contains("List<CustomerDialogueLineDef> Lines") &&
            customerDef.Contains("List<CustomerDialogueLineDef> ResponseLines") &&
            customerDef.Contains("List<CustomerDialogueLineDef> PotionRefusedLines"));

        AssertTrue("DataDb parses customer dialogue start node",
            dataDb.Contains("DialogueStartNodeId = ReadString(entry, \"dialogueStartNodeId\")"));
        AssertTrue("DataDb parses customer dialogue nodes",
            dataDb.Contains("ParseCustomerDialogueNodes(ReadArray(entry, \"dialogueNodes\"))"));
        AssertTrue("DataDb parses customer dialogue options",
            dataDb.Contains("ParseCustomerDialogueOptions(ReadArray(entry, \"options\"))"));
        AssertTrue("DataDb parses dialogue option effects",
            dataDb.Contains("Effects = ParseEffects(ReadArray(entry, \"effects\"))"));
        AssertTrue("DataDb parses request reveal and potion response fields",
            dataDb.Contains("RevealsRequest = ReadBool(entry, \"revealsRequest\")") &&
            dataDb.Contains("ReturnNodeId = ReadString(entry, \"returnNodeId\")") &&
            dataDb.Contains("ParseCustomerPotionResponses(ReadArray(entry, \"potionResponses\"))"));
        AssertTrue("DataDb parses structured customer dialogue lines",
            dataDb.Contains("ParseCustomerDialogueLines(ReadArray(entry, \"lines\"))") &&
            dataDb.Contains("CharacterImagePaths = ReadStringStringDictionary(entry, \"characterImagePaths\")") &&
            dataDb.Contains("CharacterImageKey = ReadString(entry, \"characterImageKey\")") &&
            dataDb.Contains("ReadAuthoredLineArray(entry, \"responseLines\", \"lines\")") &&
            dataDb.Contains("PotionRefusedLines = ParseCustomerDialogueLines") &&
            dataDb.Contains("Lines = lines"));
        AssertTrue("Dialogue runtime flow is not customer-coupled",
            dialogueSession.Contains("namespace OccultShop.Dialogue") &&
            dialogueSession.Contains("Func<DialogueOption, bool>") &&
            dialogueGraph.Contains("TryGetStartNode") &&
            !dialogueSession.Contains("CustomerInteractionDef") &&
            !dialogueSession.Contains("GameState") &&
            !dialogueSession.Contains("Potion"));
        AssertTrue("Customer dialogue adapter maps authored customers into neutral dialogue",
            customerDialogueAdapter.Contains("CustomerDialogueAdapter") &&
            customerDialogueAdapter.Contains("DialogueGraph") &&
            customerDialogueGraphBuilder.Contains("CustomerDialogueGraphBuilder") &&
            customerDialogueGraphBuilder.Contains("new DialogueNode") &&
            customerDialogueGraphBuilder.Contains("new DialogueOption"));

        AssertTrue("StationCustomerPanel starts story dialogue instead of serving controls",
            stationCustomerPanel.Contains("TryShowDialogueStart(interaction)") &&
            stationCustomerPanel.Contains("SetServingControlsVisible(false);"));
        AssertTrue("StationCustomerPanel creates a dynamic vertical dialogue option list",
            stationCustomerPanel.Contains("_dialogueOptionsContainer") &&
            stationCustomerPanel.Contains("CustomerInteractionDef.MaxDialogueOptionsPerNode") &&
            stationCustomerPanel.Contains("TrySelectDialogueOption(optionIndex)"));
        AssertTrue("StationCustomerPanel renders visible dialogue options from authored labels",
            stationCustomerPanel.Contains("SetDialogueOptionButton") &&
            stationCustomerPanel.Contains("button.Text = option.Label"));
        AssertTrue("StationCustomerPanel records and greys repeatable seen dialogue options",
            customerDialogueAdapter.Contains("RecordStoryCustomerDialogueOptionSelected") &&
            customerDialogueAdapter.Contains("HasStoryCustomerDialogueOptionSelected") &&
            stationCustomerPanel.Contains("_customerDialogueAdapter?.HasOptionBeenSelected(option)") &&
            stationCustomerPanel.Contains("SeenDialogueOptionModulate"));
        AssertTrue("Story visit records persist selected dialogue option ids",
            storyVisit.Contains("SelectedDialogueOptionIds") &&
            storyVisitState.Contains("CloneSelectedDialogueOptionIds"));
        AssertTrue("StationCustomerPanel keeps full scrollable conversation history",
            stationCustomerPanel.Contains("_dialoguePresenter") &&
            presenter.Contains("AddHistoryLine") &&
            stationCustomerPanel.Contains("BuildAuthoredNarrativeLines") &&
            stationCustomerPanel.Contains("ScrollActive = true"));
        AssertTrue("StationCustomerPanel colors player and customer speaker names",
            presenter.Contains("CustomerDialogueTextFormatter.FormatSpeakerName") &&
            customerTextFormatter.Contains("PlayerSpeakerColorHex") &&
            customerTextFormatter.Contains("CustomerSpeakerColorHex") &&
            customerTextFormatter.Contains("FormatSpeakerName") &&
            customerTextFormatter.Contains("[b][color={colorHex}]{safeSpeaker}[/color][/b]"));
        AssertTrue("StationCustomerPanel reveals story dialogue one queued typed line at a time",
            stationCustomerPanel.Contains("DialogueTypewriterCharactersPerSecond") &&
            presenter.Contains("QueueLine") &&
            presenter.Contains("LineStarted") &&
            presenter.Contains("VisibleCharacters") &&
            stationCustomerPanel.Contains("PlayQueuedDialogueLines") &&
            stationCustomerPanel.Contains("AdvanceQueuedDialoguePresentation") &&
            stationCustomerPanel.Contains("MouseButton.Left"));
        AssertTrue("StationCustomerPanel can switch authored character portraits by dialogue line",
            stationCustomerPanel.Contains("RefreshCustomerImage(interaction, line.CharacterImageKey)") &&
            stationCustomerPanel.Contains("line.CharacterImageKey") &&
            presenter.Contains("CharacterImageKey") &&
            validator.Contains("ValidateCharacterImageKeys"));
        AssertTrue("StationCustomerPanel disables potion drops during dialogue",
            stationCustomerPanel.Contains("SetDialogueOptionState") &&
            stationCustomerPanel.Contains("SetServingControlsEnabled(false);") &&
            stationCustomerPanel.Contains("_servingDropBox.SetAcceptDrops(enabled);"));
        AssertTrue("StationCustomerPanel switches request reveal into selling mode",
            stationCustomerPanel.Contains("EnterPotionSellingMode") &&
            stationCustomerPanel.Contains("SetSellingModeState") &&
            stationCustomerPanel.Contains("OnReturnToDialoguePressed"));
        AssertTrue("StationCustomerPanel shows request details immediately for normal customers and after reveal for story customers",
            stationCustomerPanel.Contains("_shopSessionState.SetActiveCustomerRequest(request);") &&
            stationCustomerPanel.Contains("_shopSessionState.SetActiveCustomerRequest(interaction.BuildRequest());") &&
            stationCustomerPanel.Contains("private bool CanServeActiveCustomer()") &&
            stationCustomerPanel.Contains("return !HasActiveDialogueInteraction() || _sellingMode;"));
        AssertTrue("StationCustomerPanel does not create a give-potion dialogue action",
            !stationCustomerPanel.Contains("GivePotion") &&
            !stationCustomerPanel.Contains("Give potion") &&
            !stationCustomerPanel.Contains("OnGivePotionPressed"));
        AssertTrue("StationCustomerPanel supports refusing requested plot potions",
            stationCustomerPanel.Contains("OnRefusePressed") &&
            saleService.Contains("PotionRefusedText") &&
            saleService.Contains("ApplyRefusal"));
        AssertTrue("CustomerSaleService applies authored potion response rules",
            saleService.Contains("FindPotionResponse") &&
            saleService.Contains("CustomerSaleRules.PotionResponseMatches") &&
            saleService.Contains("ApplyOutcomeEffects(response?.Effects)"));
        AssertTrue("StationCustomerPanel renders structured customer dialogue lines",
            stationCustomerPanel.Contains("BuildAuthoredNarrativeLines") &&
            stationCustomerPanel.Contains("QueueAuthoredLines") &&
            stationCustomerPanel.Contains("QueueDialogueLines") &&
            stationCustomerPanel.Contains("QueueDialogueOptionResponse") &&
            saleService.Contains("FormatPlainAuthoredLine"));
        AssertTrue("Authored data validation accepts structured option responses",
            validator.Contains("option.ResponseLines.Count == 0"));
        AssertTrue("StationCustomerPanel keeps regular customer serving controls visible",
            stationCustomerPanel.Contains("SetServingControlsVisible(true);") &&
            stationCustomerPanel.Contains("SetServingControlsEnabled(true);"));
        AssertTrue("StationCustomerPanel accepts station potion quick-serve requests",
            stationCustomerPanel.Contains("PotionQuickServeRequested += OnPotionQuickServeRequested") &&
            stationCustomerPanel.Contains("private void OnPotionQuickServeRequested(string itemId)") &&
            stationCustomerPanel.Contains("TrySelectPotion(itemId);"));
        AssertTrue("StationCustomerPanel records terminal dialogue choices as story outcomes",
            stationCustomerPanel.Contains("RecordStoryCustomerInteractionOutcome(interaction, outcome)") &&
            customerDialogueAdapter.Contains("dialogue:") &&
            stationCustomerPanel.Contains("CompleteDialogueInteraction(adapter.BuildOutcome(option))"));
        AssertTrue("StationCustomerPanel signals and resolves completed dialogue flow",
            stationCustomerPanel.Contains("DialogueResolvedEventHandler") &&
            stationCustomerPanel.Contains("EmitSignal(SignalName.DialogueResolved)") &&
            stationCustomerPanel.Contains("BeginResolveActiveCustomer();"));
        AssertTrue("DayController closes resolved dialogue customers through the station queue path",
            dayController.Contains("_stationCustomerPanel.CustomerResolved += OnStationCustomerResolved") &&
            dayController.Contains("_stationCustomerPanel.CustomerQueueEmptied += OnStationCustomerQueueEmptied") &&
            dayController.Contains("CloseShopAndShowSummary();"));
        AssertTrue("Tiered customer data includes a sample plot customer dialogue tree",
            tieredCustomers.Contains("\"id\": \"plot_bridget_visit_1\"") &&
            tieredCustomers.Contains("\"text\": \"Bridget welcomes you to the village and recognizes that you carry The Knowledge.\"") &&
            tieredCustomers.Contains("\"characterImagePaths\"") &&
            tieredCustomers.Contains("\"characterImageKey\": \"sad\"") &&
            tieredCustomers.Contains("\"potionResponses\""));
    }

    private static void TestCustomerPanelRendersDialogueNodeTextAsNarration()
    {
        var stationCustomerPanel = ReadProjectFile("Scripts/UI/StationCustomerPanel.cs");
        var formatter = ReadProjectFile("Scripts/UI/CustomerDialogueTextFormatter.cs");
        var dialogueNarrativeLineBuilder = ReadProjectFile("Scripts/UI/Text/DialogueNarrativeLineBuilder.cs");

        AssertTrue("Conversation formatter supports headerless narration",
            formatter.Contains("FormatNarrationLine") &&
            formatter.Contains("string.IsNullOrWhiteSpace(speaker)") &&
            formatter.Contains("return FormatNarrationLine(text);"));
        AssertTrue("StationCustomerPanel queues dialogue node text as narration",
            stationCustomerPanel.Contains("QueueDialogueLines(node.Lines, node.Text, null);") &&
            dialogueNarrativeLineBuilder.Contains("string.IsNullOrWhiteSpace(line.Speaker) ? fallbackSpeaker : line.Speaker"));
        AssertTrue("StationCustomerPanel keeps legacy option responses under customer speaker",
            stationCustomerPanel.Contains("QueueDialogueOptionResponse(option);") &&
            stationCustomerPanel.Contains("option.ResponseText") &&
            stationCustomerPanel.Contains("CustomerDialogueTextFormatter.CustomerSpeakerName"));
    }

    private static void TestCustomerOutcomesUpdateStoryState()
    {
        var saleService = ReadProjectFile("Scripts/Systems/CustomerSaleService.cs");

        AssertTrue("Customer sale service defines automatic reputation deltas",
            saleService.Contains("SuccessReputationChange = 2") &&
            saleService.Contains("FailureReputationChange = -3") &&
            saleService.Contains("RefusalReputationChange = -1"));
        AssertTrue("Story customer sale service defines automatic relationship deltas",
            saleService.Contains("StoryCustomerSuccessRelationshipChange = 5") &&
            saleService.Contains("StoryCustomerFailureRelationshipChange = -5") &&
            saleService.Contains("StoryCustomerRefusalRelationshipChange = -2"));
        AssertTrue("Sales apply automatic outcome changes before authored effects",
            saleService.Contains("ApplyAutomaticSaleOutcome(interaction, isSuccess);") &&
            saleService.IndexOf("ApplyAutomaticSaleOutcome(interaction, isSuccess);", StringComparison.Ordinal) <
            saleService.IndexOf("ApplyOutcomeEffects(isSuccess ? interaction.OnSuccessEffects : interaction.OnFailureEffects);", StringComparison.Ordinal));
        AssertTrue("Refusals apply the milder automatic outcome changes",
            saleService.Contains("ApplyAutomaticRefusalOutcome(interaction);") &&
            saleService.Contains("_gameState.AddReputation(RefusalReputationChange)") &&
            saleService.Contains("_gameState.AddRelationship(interaction.StoryCharacterId, StoryCustomerRefusalRelationshipChange)"));
    }

    private static void TestDialogueTreeEditorAddonIsRegistered()
    {
        var project = ReadProjectFile("project.godot");
        var plugin = ReadProjectFile("addons/dialogue_tree_editor/plugin.gd");
        var dock = ReadProjectFile("addons/dialogue_tree_editor/dialogue_tree_editor_dock.gd");

        AssertTrue("Dialogue tree editor plugin is enabled",
            project.Contains("res://addons/dialogue_tree_editor/plugin.cfg"));
        AssertTrue("Dialogue tree editor is installed as an editor dock",
            plugin.Contains("extends EditorPlugin") &&
            plugin.Contains("add_control_to_dock") &&
            plugin.Contains("remove_control_from_docks"));
        AssertTrue("Dialogue tree editor loads the authored customer resource",
            dock.Contains("AUTHORED_DATA_PATH") &&
            dock.Contains("CustomerInteractionsPath") &&
            dock.Contains("ResourceSaver.save(_customer_resource, _customer_path)"));
        AssertTrue("Dialogue tree editor exposes graph editing and story-state authoring",
            dock.Contains("GraphEdit") &&
            dock.Contains("connection_request") &&
            dock.Contains("reputationMin") &&
            dock.Contains("questStatus") &&
            dock.Contains("relationshipCharacterId") &&
            dock.Contains("hasStoryFlag"));
        AssertTrue("Dialogue tree editor shows selected interactions instead of a blank graph",
            dock.Contains("_selected_summary_label") &&
            dock.Contains("No dialogue tree") &&
            dock.Contains("Add first node") &&
            dock.Contains("Selected %s."));
        AssertTrue("Dialogue tree editor uses a narrow tabbed layout for bottom docks",
            dock.Contains("custom_minimum_size = Vector2(360.0, 280.0)") &&
            dock.Contains("_root.anchor_right = 1.0") &&
            dock.Contains("_root.anchor_bottom = 1.0") &&
            dock.Contains("TabContainer.new()") &&
            dock.Contains("interactions_panel.name = \"Interactions\"") &&
            dock.Contains("graph_panel.name = \"Graph\"") &&
            dock.Contains("inspector_panel.name = \"Inspector\""));
        AssertTrue("Dialogue tree editor graph defaults to a readable floating-dock scale",
            dock.Contains("GRAPH_READABLE_ZOOM := 1.5") &&
            dock.Contains("GRAPH_MIN_VISIBLE_HEIGHT := 420.0") &&
            dock.Contains("_graph.zoom = GRAPH_READABLE_ZOOM") &&
            dock.Contains("_graph.show_zoom_label = true") &&
            dock.Contains("call_deferred(\"_apply_graph_readability\")") &&
            dock.Contains("option_button.custom_minimum_size = Vector2(280.0, 34.0)"));
        AssertTrue("Dialogue tree editor exposes option creation from the graph tab",
            dock.Contains("graph_actions.add_child(_make_button(\"Add Option\", _add_option_to_selected_node))") &&
            dock.Contains("add_option_button.text = \"Add option\"") &&
            dock.Contains("add_option_button.pressed.connect(_add_option_to_node.bind(node_id))") &&
            dock.Contains("Select a node before adding an option."));
    }

    private static void TestCustomerDialogueUsesNarrativeTextPresenter()
    {
        var stationCustomerPanel = ReadProjectFile("Scripts/UI/StationCustomerPanel.cs");
        var presenter = ReadProjectFile("Scripts/UI/Text/NarrativeTextPresenter.cs");
        var converter = ReadProjectFile("Scripts/UI/Text/CustomerDialogueMarkupConverter.cs");
        var debugPanel = ReadProjectFile("Scripts/Debug/RuntimeDebugImGui.cs");

        AssertTrue("StationCustomerPanel creates a narrative text presenter for dialogue",
            stationCustomerPanel.Contains("new NarrativeTextPresenter(this, _dialogue)") &&
            stationCustomerPanel.Contains("_dialoguePresenter.PlayQueued(completedAction)") &&
            stationCustomerPanel.Contains("_dialoguePresenter?.AdvanceQueuedPresentation()"));
        AssertTrue("StationCustomerPanel no longer slices authored text during reveal",
            !stationCustomerPanel.Contains("GetVisibleDialogueText(") &&
            !stationCustomerPanel.Contains("_activeDialogueTextCharacters") &&
            !stationCustomerPanel.Contains("_pendingDialogueLines"));
        AssertTrue("NarrativeTextPresenter uses RichTextLabel visible character reveal",
            presenter.Contains("_label.VisibleCharacters") &&
            presenter.Contains("TotalVisibleCharacters") &&
            presenter.Contains("InitialVisibleCharacters"));
        AssertTrue("NarrativeTextPresenter supports current-line skip without draining the queue",
            presenter.Contains("CompleteActiveLine();") &&
            presenter.Contains("if (_pendingLines.Count > 0)") &&
            !presenter.Contains("while (_pendingLines.Count > 0)"));
        AssertTrue("Narrative markup converter supports safe writer syntax",
            converter.Contains("CustomerDialogueMarkupConverter") &&
            converter.Contains("TryBuildStyleTags") &&
            converter.Contains("TryAppendInlineCommand") &&
            converter.Contains("ConvertPlainText"));
        AssertTrue("Runtime debug panel exposes a narrative preview tool",
            debugPanel.Contains("DrawNarrativeTextPreviewSection") &&
            debugPanel.Contains("PlayNarrativePreview") &&
            debugPanel.Contains("NarrativeTextPreviewLayer") &&
            debugPanel.Contains("InputTextMultiline"));
    }

    private static void TestCustomerDropBoxDisablesAfterSale()
    {
        var stationCustomerPanel = ReadProjectFile("Scripts/UI/StationCustomerPanel.cs");
        var dropBox = ReadProjectFile("Scripts/UI/CustomerSellDropBox.cs");

        AssertTrue("StationCustomerPanel enables dropbox during serving state",
            stationCustomerPanel.Contains("SetServingControlsEnabled(true);"));
        AssertTrue("StationCustomerPanel disables dropbox while dialogue or resolution is active",
            stationCustomerPanel.Contains("SetServingControlsEnabled(false);") &&
            stationCustomerPanel.Contains("_servingDropBox.SetAcceptDrops(false);"));
        AssertTrue("StationCustomerPanel only highlights valid potion hover previews",
            stationCustomerPanel.Contains("_servingDropBox.SetHoverHighlight(true);") &&
            stationCustomerPanel.Contains("_itemCatalog.IsPotion(itemId)"));
        AssertTrue("StationCustomerPanel clears the sell drop highlight on hover exit",
            stationCustomerPanel.Contains("_servingDropBox.SetHoverHighlight(false);"));
        AssertTrue("StationCustomerPanel fades the sell box while drops are disabled",
            stationCustomerPanel.Contains("_servingDropBox.SetDisabledVisual(!enabled);"));
        AssertTrue("StationCustomerPanel restores the sell box when a new customer is prepared",
            stationCustomerPanel.Contains("RefreshActiveCustomer(emitShownSignal:") &&
            stationCustomerPanel.Contains("SetServingControlsEnabled(true);"));
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

    private static void TestCustomerPanelShowsDraggablePotionSaleSlots()
    {
        var stationCustomerPanel = ReadProjectFile("Scripts/UI/StationCustomerPanel.cs");
        var potionRow = ReadProjectFile("Scripts/UI/PotionInventoryRow.cs");
        var formatter = ReadProjectFile("Scripts/UI/CustomerDialogueTextFormatter.cs");
        var stationPresentation = ReadProjectFile("Scripts/UI/StationCustomerPotionPresentation.cs");
        var inventorySlot = ReadProjectFile("Scripts/UI/InventoryItemSlot.cs");
        var jarredSlot = ReadProjectFile("Scripts/UI/JarredInventorySlotView.cs");
        var layoutSettings = ReadProjectFile("Assets/UI/InventorySlotLayoutSettings.tres");

        AssertTrue("PotionInventoryRow caps visible station potion slots to inventory capacity",
            potionRow.Contains("VisiblePotionSlots = GameState.MaxUniquePotionInventoryQuantity") &&
            potionRow.Contains("if (stacks.Count >= VisiblePotionSlots)"));
        AssertTrue("PotionInventoryRow uses inventory slots so station potion slots drag item ids",
            potionRow.Contains("InventoryItemSlot") &&
            inventorySlot.Contains("return Variant.CreateFrom(ItemId);"));
        AssertTrue("StationCustomerPanel exposes visible potion slots for tutorial highlighting without the removed inventory panel",
            stationCustomerPanel.Contains("public Control? GetVisiblePotionSlot(string itemId)") &&
            stationCustomerPanel.Contains("return _potionInventoryRow?.GetVisiblePotionSlot(itemId);") &&
            !stationCustomerPanel.Contains("InventoryPanelPath") &&
            !stationCustomerPanel.Contains("OpenItemDetail"));
        AssertTrue("PotionInventoryRow fills potion slots from current potion inventory",
            potionRow.Contains("foreach (var stack in _gameState.Inventory)") &&
            potionRow.Contains("if (!IsPotion(item))") &&
            potionRow.Contains("CreatePotionSlot(stack)"));
        AssertTrue("PotionInventoryRow shows potion slot quantity badges",
            jarredSlot.Contains("Name = \"Quantity\"") &&
            jarredSlot.Contains("PotionLiquidView") &&
            potionRow.Contains("InventorySlotLayoutKind.PotionInventory") &&
            layoutSettings.Contains("PotionInventorySlot = SubResource(\"Resource_potion_inventory\")") &&
            potionRow.Contains("Quantity = stack.Quantity"));
        AssertTrue("StationCustomerPanel accepts quick-serve slot requests only when a potion can be sold",
            stationCustomerPanel.Contains("PotionQuickServeRequested += OnPotionQuickServeRequested") &&
            stationCustomerPanel.Contains("TrySelectPotion(itemId)") &&
            stationCustomerPanel.Contains("private bool CanServeActiveCustomer()"));
        AssertTrue("PotionInventoryRow refreshes potion slots when inventory changes",
            potionRow.Contains("_gameState.Changed += Refresh") &&
            potionRow.Contains("_gameState.Changed -= Refresh"));
        AssertTrue("StationCustomerPanel updates request comparison text for selected potion values",
            stationCustomerPanel.Contains("SetRequestFitText") &&
            stationCustomerPanel.Contains("StationCustomerPotionPresentation.BuildRequestFitText") &&
            stationCustomerPanel.Contains("_saleService.GetPotionIngredientPortions(potionItemId)") &&
            stationPresentation.Contains("BuildCustomerPotionRequestComparisonText") &&
            formatter.Contains("BuildCustomerPotionRequestComparisonText"));
        AssertTrue("StationCustomerPanel hides potion fit feedback for hidden requests",
            stationCustomerPanel.Contains("request.HideRequestDetails") &&
            stationCustomerPanel.Contains("StationCustomerPotionPresentation.BuildHiddenRequestFitText") &&
            stationPresentation.Contains("CustomerDialogueTextFormatter.HiddenRequestText") &&
            stationCustomerPanel.Contains("ResolveSale(_selectedPotionItemId, _selectedPotionResult);"));
    }

    private static void TestCustomerRequestComparisonTextShowsSelectedPotionValues()
    {
        var request = new CustomerRequestDef
        {
            DesiredTraits = new Dictionary<string, CustomerTraitRangeDef>
            {
                ["charm"] = Range(min: 4, max: 7),
                ["vigor"] = Range(min: 3, max: 6)
            },
            BadTraits = new Dictionary<string, CustomerTraitRangeDef>
            {
                ["insomnia"] = Range(max: 1),
                ["corruption"] = Range(max: 0)
            },
            RequiredMinTraits = new Dictionary<string, int>
            {
                ["calm"] = 2
            },
            RequiredMaxTraits = new Dictionary<string, int>
            {
                ["melancholy"] = 1
            }
        };

        var producedTraits = new Dictionary<string, int>
        {
            ["charm"] = 5,
            ["vigor"] = 2,
            ["calm"] = 1,
            ["melancholy"] = 2
        };
        var producedRisks = new Dictionary<string, int>
        {
            ["insomnia"] = 1,
            ["corruption"] = 2
        };

        var desiredText = CustomerDialogueTextFormatter.BuildDesiredRequestText(request, producedTraits);
        AssertTrue("Matched desired trait row is green and shows potion value",
            desiredText.Contains("[color=#59D959]charm: 4-7 (5)[/color]"));
        AssertTrue("Unmatched desired trait row is red and shows potion value",
            desiredText.Contains("[color=#E64040]vigor: 3-6 (2)[/color]"));
        AssertTrue("Unmatched required min trait row is red and shows potion value",
            desiredText.Contains("[color=#E64040]calm >= 2 (1)[/color]"));

        var badText = CustomerDialogueTextFormatter.BuildBadRequestText(request, producedTraits, producedRisks);
        AssertTrue("Safe bad trait row is green and shows potion value",
            badText.Contains("[color=#59D959]insomnia: <= 1 (1)[/color]"));
        AssertTrue("Violated bad trait row is red and shows potion value",
            badText.Contains("[color=#E64040]corruption: <= 0 (2)[/color]"));
        AssertTrue("Violated required max trait row is red and shows potion value",
            badText.Contains("[color=#E64040]melancholy <= 1 (2)[/color]"));

        var hiddenRequest = new CustomerRequestDef
        {
            HideRequestDetails = true,
            DesiredTraits = request.DesiredTraits,
            BadTraits = request.BadTraits,
            RequiredMinTraits = request.RequiredMinTraits,
            RequiredMaxTraits = request.RequiredMaxTraits
        };
        AssertEqual("Hidden desired request text", "?????", CustomerDialogueTextFormatter.BuildDesiredRequestText(hiddenRequest, producedTraits));
        AssertEqual("Hidden bad request text", "?????", CustomerDialogueTextFormatter.BuildBadRequestText(hiddenRequest, producedTraits, producedRisks));
        AssertEqual("Hidden potion comparison text", "?????", CustomerDialogueTextFormatter.BuildCustomerPotionRequestComparisonText(hiddenRequest, producedTraits, producedRisks, null));
        AssertEqual("Hidden brew checklist text", "?????", CustomerDialogueTextFormatter.BuildBrewingRequestChecklistText(hiddenRequest, producedTraits, producedRisks, null));
    }

    private static CustomerTraitRangeDef Range(int? min = null, int? max = null)
    {
        return new CustomerTraitRangeDef
        {
            Min = min,
            Max = max
        };
    }

    private static void AssertAllDesiredTraitRangesAreBounded(string projectPath)
    {
        var interactions = ReadAuthoredCustomerInteractions(projectPath);
        foreach (var interaction in interactions)
        {
            foreach (var desired in interaction.DesiredTraits)
            {
                AssertTrue(
                    $"{interaction.Id} desired trait '{desired.Key}' has a minimum",
                    desired.Value?.HasMin == true);
                if (!AllowsOpenEndedDesiredTraitRanges(interaction.Id))
                {
                    AssertTrue(
                        $"{interaction.Id} desired trait '{desired.Key}' has a maximum",
                        desired.Value?.HasMax == true);
                }

                if (desired.Value?.Min is int min && desired.Value.Max is int max)
                    AssertTrue($"{interaction.Id} desired trait '{desired.Key}' has a valid range", min <= max);
            }
        }
    }

    private static bool AllowsOpenEndedDesiredTraitRanges(string interactionId)
    {
        return string.Equals(
            interactionId,
            "customer_requests_day_two_rest_memory_clarity",
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasDesiredTraitRange(
        IReadOnlyList<CustomerInteractionDef> interactions,
        string interactionId,
        string traitId,
        int? min,
        int? max)
    {
        return HasTraitRange(interactions, interactionId, traitId, min, max, useBadTraits: false);
    }

    private static bool HasBadTraitRange(
        IReadOnlyList<CustomerInteractionDef> interactions,
        string interactionId,
        string traitId,
        int? min,
        int? max)
    {
        return HasTraitRange(interactions, interactionId, traitId, min, max, useBadTraits: true);
    }

    private static bool HasTraitRange(
        IReadOnlyList<CustomerInteractionDef> interactions,
        string interactionId,
        string traitId,
        int? min,
        int? max,
        bool useBadTraits)
    {
        foreach (var interaction in interactions)
        {
            if (!string.Equals(interaction.Id, interactionId, StringComparison.OrdinalIgnoreCase))
                continue;

            var ranges = useBadTraits ? interaction.BadTraits : interaction.DesiredTraits;
            if (!ranges.TryGetValue(traitId, out var range) || range is null)
                return false;

            return range.Min == min && range.Max == max;
        }

        return false;
    }

    private static List<CustomerInteractionDef> ReadAuthoredCustomerInteractions(string projectPath)
    {
        var source = ReadProjectFile(projectPath);
        const string marker = "Entries = ";
        var start = source.IndexOf(marker, StringComparison.Ordinal);
        AssertTrue($"{projectPath} contains an Entries array", start >= 0);

        var json = source[(start + marker.Length)..].Trim();
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        return JsonSerializer.Deserialize<List<CustomerInteractionDef>>(json, options)
            ?? throw new InvalidOperationException($"Could not parse authored customers from {projectPath}.");
    }

    private static int CountOccurrences(string text, string value)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(value))
            return 0;

        var count = 0;
        var startIndex = 0;
        while (true)
        {
            var index = text.IndexOf(value, startIndex, StringComparison.Ordinal);
            if (index < 0)
                return count;

            count += 1;
            startIndex = index + value.Length;
        }
    }
}
