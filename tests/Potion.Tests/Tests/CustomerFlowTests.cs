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
        runner.Run("CustomerPanel creates detached ingredient snapshots", TestCustomerPanelBuildPotionIngredientDef);
        runner.Run("Customer events randomize shop-day order", TestCustomerEventControllerRandomizesOrder);
        runner.Run("Shop summary clears customer presentation", TestShopSummaryClearsCustomerPresentation);
        runner.Run("Active customer request keeps shop front customer clickable", TestActiveCustomerRequestKeepsShopFrontCustomerClickable);
        runner.Run("Forced customer fallback resolves legacy ids deterministically", TestForcedCustomerFallbackResolvesLegacyIdsDeterministically);
        runner.Run("Customer events respect scheduling and story outcomes", TestCustomerEventSchedulingAndStoryOutcomes);
        runner.Run("Customer trait thresholds are enforced", TestCustomerTraitThresholdsAreEnforced);
        runner.Run("Customer trait ranges are enforced", TestCustomerTraitRangesAreEnforced);
        runner.Run("Active customer catalog includes trait threshold requests", TestActiveCustomerCatalogIncludesTraitThresholdRequests);
        runner.Run("Tiered customer data is a day-one bounded trait catalog", TestTieredCustomerDataIsDayOneFlexibleTraitCatalog);
        runner.Run("Customer dialogue markup converts safe syntax", TestCustomerDialogueMarkupConvertsSafeSyntax);
        runner.Run("Story customer dialogue trees support selling mode", TestStoryCustomerDialogueTreesSupportSellingMode);
        runner.Run("CustomerPanel renders dialogue node text as narration", TestCustomerPanelRendersDialogueNodeTextAsNarration);
        runner.Run("Customer dialogue uses narrative text presenter", TestCustomerDialogueUsesNarrativeTextPresenter);
        runner.Run("CustomerPanel shows draggable potion sale slots", TestCustomerPanelShowsDraggablePotionSaleSlots);
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
        var customerPanel = ReadProjectFile("Scripts/UI/CustomerPanel.cs");

        AssertTrue("CustomerEventController no longer uses a fixed index walk", !source.Contains("_nextCustomerIndex"));
        AssertTrue("CustomerEventController keeps a randomized order buffer", source.Contains("_customerOrder"));
        AssertTrue("CustomerEventController randomizes the customer order", source.Contains("_random.Next("));
        AssertTrue("CustomerEventController resets the order at the start of a shop day", source.Contains("BeginShopDay()"));
        AssertTrue("CustomerEventController makes Bridget the first normal new-game customer",
            source.Contains("NewGameWelcomeInteractionId = \"plot_bridget_visit_1\"") &&
            source.Contains("TryDrawNewGameWelcomeInteraction") &&
            source.Contains("GameState.BridgetWelcomePendingStoryFlag") &&
            source.Contains("state.RemoveStoryFlag(GameState.BridgetWelcomePendingStoryFlag)") &&
            ReadProjectFile("Scripts/Autoload/GameState.cs").Contains("StoryFlags.Add(BridgetWelcomePendingStoryFlag)") &&
            source.IndexOf("TryDrawForcedInteraction", StringComparison.Ordinal) < source.IndexOf("TryDrawNewGameWelcomeInteraction", StringComparison.Ordinal));
        AssertTrue("DayController resets customer order when the shop opens", dayController.Contains("_customerEventController.BeginShopDay();"));
        AssertTrue("DayController caps shop-day customer arrivals at three",
            dayController.Contains("MaxCustomersPerShopDay = 3") &&
            dayController.Contains("_customersArrived >= MaxCustomersPerShopDay"));
        AssertTrue("DayController counts customer arrivals when a customer is shown",
            dayController.Contains("_customersArrived += 1;"));
        AssertTrue("DayController closes the shop after the current final customer is resolved",
            dayController.Contains("ShouldCloseShopAfterCurrentCustomer()") &&
            dayController.Contains("CloseShopAndShowSummary();"));
        AssertTrue("CustomerPanel exposes active interaction state", customerPanel.Contains("HasActiveInteraction => _interaction is not null"));
        AssertTrue("CustomerPanel can switch the next button to Close Shop", customerPanel.Contains("SetCloseShopMode(bool closeShopMode)"));
        AssertTrue("CustomerPanel exposes close shop mode state", customerPanel.Contains("IsCloseShopMode => _closeShopMode"));
    }

    private static void TestShopSummaryClearsCustomerPresentation()
    {
        var dayController = ReadProjectFile("Scripts/Controllers/DayController.cs");
        var shopFloor = ReadProjectFile("Scripts/UI/ShopFloor.cs");
        var hideCustomerPresentationIndex = shopFloor.IndexOf(
            "private void HideCustomerPresentationForClosedShop()",
            StringComparison.Ordinal);
        var hideCustomerPresentationBody = string.Empty;
        if (hideCustomerPresentationIndex >= 0)
        {
            var nextMethodIndex = shopFloor.IndexOf(
                "private bool ShowPotionBrewingStation()",
                hideCustomerPresentationIndex,
                StringComparison.Ordinal);
            if (nextMethodIndex > hideCustomerPresentationIndex)
                hideCustomerPresentationBody = shopFloor.Substring(
                    hideCustomerPresentationIndex,
                    nextMethodIndex - hideCustomerPresentationIndex);
        }

        AssertTrue("DayController emits shop state changes after showing the day summary",
            dayController.Contains("_daySummaryPanel.ShowSummary(") &&
            dayController.Contains("EmitShopStateChanged();"));
        AssertTrue("ShopFloor listens for shop state changes to refresh customer presence",
            shopFloor.Contains("_dayController.ShopStateChanged += UpdateCustomerPresence;") &&
            shopFloor.Contains("_dayController.ShopStateChanged -= UpdateCustomerPresence;"));
        AssertTrue("ShopFloor keeps the retired shop floor hidden and hides customer presentation when the shop is closed",
            shopFloor.Contains("if (_dayController is not null && !_dayController.IsShopOpen)") &&
            shopFloor.Contains("HideCustomerPresentationForClosedShop();") &&
            hideCustomerPresentationBody.Contains("Visible = false;") &&
            hideCustomerPresentationBody.Contains("_customerCloseupView.Visible = false;") &&
            hideCustomerPresentationBody.Contains("_customerPanel.Visible = false;"));
    }

    private static void TestActiveCustomerRequestKeepsShopFrontCustomerClickable()
    {
        var shopFloor = ReadProjectFile("Scripts/UI/ShopFloor.cs");

        AssertTrue("ShopFloor observes active customer request changes",
            shopFloor.Contains("GameStatePath = new(AutoloadNodePaths.GameState)") &&
            shopFloor.Contains("_gameState = GetOptionalNode<GameState>(GameStatePath, nameof(GameStatePath));") &&
            shopFloor.Contains("_gameState.Changed += UpdateCustomerPresenceFromGameState;") &&
            shopFloor.Contains("_gameState.Changed -= UpdateCustomerPresenceFromGameState;"));
        AssertTrue("ShopFloor keeps the customer hotspot visible while a request is active",
            shopFloor.Contains("private void UpdateCustomerPresenceFromGameState()") &&
            shopFloor.Contains("UpdateCustomerPresence(hideClosedShopPresentation: false);") &&
            shopFloor.Contains("hasActiveCustomerRequest") &&
            shopFloor.Contains("_gameState.ActiveCustomerRequest is not null") &&
            shopFloor.Contains("hasActiveInteraction || hasActiveCustomerRequest") &&
            shopFloor.Contains("_customerButton.Disabled = !customerVisible;"));
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
        AssertTrue("Effects can confiscate one of each ingredient",
            effects.Contains("state.ConsumeEachIngredient(ingredientQty)") &&
            gameState.Contains("public int ConsumeEachIngredient(int qty)") &&
            dataDb.Contains("ConsumeEachIngredientQty = ReadNullableInt(entry, \"consumeEachIngredientQty\")"));
        AssertTrue("CustomerPanel applies success and failure effects", customerPanel.Contains("ApplyOutcomeEffects(isSuccess ? _interaction?.OnSuccessEffects : _interaction?.OnFailureEffects)"));
        AssertTrue("CustomerPanel applies skip effects", customerPanel.Contains("ApplyOutcomeEffects(_interaction.OnSkipEffects)"));
        AssertTrue("CustomerPanel records story sale outcomes", customerPanel.Contains("StoryCustomerOutcomeSuccess") && customerPanel.Contains("StoryCustomerOutcomeFailure"));
        AssertTrue("CustomerPanel records story skip outcomes", customerPanel.Contains("StoryCustomerOutcomeSkipped"));
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

    private static void TestActiveCustomerCatalogIncludesTraitThresholdRequests()
    {
        var customerDef = ReadProjectFile("Scripts/Models/CustomerInteractionDef.cs");
        var dataDb = ReadProjectFile("Scripts/Autoload/DataDb.cs");
        var saleRules = ReadProjectFile("Scripts/Systems/CustomerSaleRules.cs");
        var customerPanel = ReadProjectFile("Scripts/UI/CustomerPanel.cs");
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
        AssertTrue("Customer panel displays hard trait thresholds and ranges",
            customerPanel.Contains("BuildDesiredRequestText") &&
            customerPanel.Contains("BuildBadRequestText") &&
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

    private static void TestTieredCustomerDataIsDayOneFlexibleTraitCatalog()
    {
        var tieredCustomers = ReadProjectFile("Data/customers_tiered_test_data.tres");
        var catalog = ReadProjectFile("Data/customers_tiered_test_catalog.md");

        AssertTrue("Tiered customer data has no later-day gates",
            !tieredCustomers.Contains("\"dayMax\"") &&
            CountOccurrences(tieredCustomers, "\"requires\":") == CountOccurrences(tieredCustomers, "\"dayMin\": 1"));
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
        AssertTrue("Tiered customer data omits old unsupported traits and risks",
            !tieredCustomers.Contains("\"confusion\"") &&
            !tieredCustomers.Contains("\"nausea\"") &&
            !tieredCustomers.Contains("\"ward\"") &&
            !tieredCustomers.Contains("\"honesty\"") &&
            !tieredCustomers.Contains("\"reflexes\"") &&
            !tieredCustomers.Contains("\"empathy\"") &&
            !tieredCustomers.Contains("\"creativity\""));
        AssertTrue("Tiered customer catalog documents the day-one flexible request design",
            catalog.Contains("All entries in this catalog are available from day 1") &&
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
        var customerPanel = ReadProjectFile("Scripts/UI/CustomerPanel.cs");
        var customerTextFormatter = ReadProjectFile("Scripts/UI/CustomerDialogueTextFormatter.cs");
        var presenter = ReadProjectFile("Scripts/UI/Text/NarrativeTextPresenter.cs");
        var validator = ReadProjectFile("Scripts/Systems/AuthoredDataValidator.cs");
        var dayController = ReadProjectFile("Scripts/Controllers/DayController.cs");
        var gameState = ReadProjectFile("Scripts/Autoload/GameState.cs");
        var storyVisitState = ReadProjectFile("Scripts/Systems/StoryCustomerVisitState.cs");
        var storyVisit = ReadProjectFile("Scripts/Models/StoryCustomerVisitRecord.cs");
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

        AssertTrue("CustomerPanel starts story dialogue instead of sale pending state",
            customerPanel.Contains("TryShowDialogueStart()"));
        AssertTrue("CustomerPanel creates a dynamic vertical dialogue option list",
            customerPanel.Contains("_dialogueOptionsContainer") &&
            customerPanel.Contains("CustomerInteractionDef.MaxDialogueOptionsPerNode") &&
            customerPanel.Contains("TrySelectDialogueOption(optionIndex)"));
        AssertTrue("CustomerPanel renders visible dialogue options from authored labels",
            customerPanel.Contains("SetDialogueOptionButton") &&
            customerPanel.Contains("button.Text = option.Label"));
        AssertTrue("CustomerPanel records and greys repeatable seen dialogue options",
            customerPanel.Contains("RecordStoryCustomerDialogueOptionSelected") &&
            customerPanel.Contains("HasStoryCustomerDialogueOptionSelected") &&
            customerPanel.Contains("SeenDialogueOptionModulate"));
        AssertTrue("Story visit records persist selected dialogue option ids",
            storyVisit.Contains("SelectedDialogueOptionIds") &&
            storyVisitState.Contains("CloneSelectedDialogueOptionIds"));
        AssertTrue("CustomerPanel keeps full scrollable conversation history",
            customerPanel.Contains("_dialoguePresenter") &&
            presenter.Contains("AddHistoryLine") &&
            customerPanel.Contains("AppendCustomerLine") &&
            customerPanel.Contains("ScrollActive = true"));
        AssertTrue("CustomerPanel colors player and customer speaker names",
            presenter.Contains("CustomerDialogueTextFormatter.FormatSpeakerName") &&
            customerTextFormatter.Contains("PlayerSpeakerColorHex") &&
            customerTextFormatter.Contains("CustomerSpeakerColorHex") &&
            customerTextFormatter.Contains("FormatSpeakerName") &&
            customerTextFormatter.Contains("[b][color={colorHex}]{safeSpeaker}[/color][/b]"));
        AssertTrue("CustomerPanel reveals story dialogue one queued typed line at a time",
            customerPanel.Contains("DialogueTypewriterCharactersPerSecond") &&
            presenter.Contains("QueueLine") &&
            presenter.Contains("LineStarted") &&
            presenter.Contains("VisibleCharacters") &&
            customerPanel.Contains("PlayQueuedDialogueLines") &&
            customerPanel.Contains("AdvanceQueuedDialoguePresentation") &&
            customerPanel.Contains("MouseButton.Left"));
        AssertTrue("CustomerPanel can switch authored character portraits by dialogue line",
            customerPanel.Contains("SetCurrentCustomerImageKey") &&
            customerPanel.Contains("line.CharacterImageKey") &&
            customerPanel.Contains("CustomerImageChangedEventHandler") &&
            presenter.Contains("CharacterImageKey") &&
            validator.Contains("ValidateCharacterImageKeys"));
        AssertTrue("CustomerPanel disables potion drops during dialogue",
            customerPanel.Contains("SetDialogueOptionState") &&
            customerPanel.Contains("SetDropBoxEnabled(false);"));
        AssertTrue("CustomerPanel switches request reveal into selling mode",
            customerPanel.Contains("EnterPotionSellingMode") &&
            customerPanel.Contains("SetSellingModeState") &&
            customerPanel.Contains("OnReturnToDialoguePressed"));
        AssertTrue("CustomerPanel shows request traits immediately for normal customers and after reveal for story customers",
            customerPanel.Contains("ResolveRequestTraitsContainer") &&
            customerPanel.Contains("RefreshRequestTraitsVisibility") &&
            customerPanel.Contains("if (!HasActiveDialogueInteraction())") &&
            customerPanel.Contains("return _requestRevealed && _sellingMode;"));
        AssertTrue("CustomerPanel does not create a give-potion dialogue action",
            !customerPanel.Contains("GivePotion") &&
            !customerPanel.Contains("Give potion") &&
            !customerPanel.Contains("OnGivePotionPressed"));
        AssertTrue("CustomerPanel supports refusing requested plot potions",
            customerPanel.Contains("OnRefusePotionPressed") &&
            customerPanel.Contains("PotionRefusedText"));
        AssertTrue("CustomerPanel applies authored potion response rules",
            customerPanel.Contains("FindPotionResponse") &&
            customerPanel.Contains("PotionResponseMatches") &&
            customerPanel.Contains("ApplyOutcomeEffects(response?.Effects)"));
        AssertTrue("CustomerPanel renders structured customer dialogue lines",
            customerPanel.Contains("AppendAuthoredLines") &&
            customerPanel.Contains("QueueAuthoredLines") &&
            customerPanel.Contains("QueueDialogueOptionResponse") &&
            customerPanel.Contains("TryBuildStructuredOutcomeConversation") &&
            customerPanel.Contains("AddAuthoredDialogueLines"));
        AssertTrue("Authored data validation accepts structured option responses",
            validator.Contains("option.ResponseLines.Count == 0"));
        AssertTrue("CustomerPanel restores customer action labels for regular customers",
            customerPanel.Contains("_brewingStationButton.Text = \"Brewing Station\"") &&
            customerPanel.Contains("_sorryCantHelpYouButton.Text = \"Sorry can't help you\""));
        AssertTrue("CustomerPanel requests the brewing station without resolving the active customer",
            customerPanel.Contains("BrewingStationRequestedEventHandler") &&
            customerPanel.Contains("EmitSignal(SignalName.BrewingStationRequested)") &&
            customerPanel.Contains("private void OnBrewingStationPressed()"));
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
        AssertTrue("DayController closes final dialogue customers through the shared customer-limit path",
            dayController.Contains("private void OnCustomerDialogueResolved()") &&
            dayController.Contains("ShouldCloseShopAfterCurrentCustomer()") &&
            dayController.Contains("_awaitingSaleResultClose = true;"));
        AssertTrue("Tiered customer data includes a sample plot customer dialogue tree",
            tieredCustomers.Contains("\"id\": \"plot_bridget_visit_1\"") &&
            tieredCustomers.Contains("\"text\": \"Bridget welcomes you to the village and recognizes that you carry The Knowledge.\"") &&
            tieredCustomers.Contains("\"characterImagePaths\"") &&
            tieredCustomers.Contains("\"characterImageKey\": \"sad\"") &&
            tieredCustomers.Contains("\"potionResponses\""));
    }

    private static void TestCustomerPanelRendersDialogueNodeTextAsNarration()
    {
        var customerPanel = ReadProjectFile("Scripts/UI/CustomerPanel.cs");
        var formatter = ReadProjectFile("Scripts/UI/CustomerDialogueTextFormatter.cs");

        AssertTrue("Conversation formatter supports headerless narration",
            formatter.Contains("FormatNarrationLine") &&
            formatter.Contains("string.IsNullOrWhiteSpace(speaker)") &&
            formatter.Contains("return FormatNarrationLine(text);"));
        AssertTrue("CustomerPanel queues dialogue node text as narration",
            customerPanel.Contains("QueueAuthoredLines(node.Lines, node.Text, null);"));
        AssertTrue("CustomerPanel keeps legacy option responses under customer speaker",
            customerPanel.Contains("QueueDialogueOptionResponse(option);") &&
            customerPanel.Contains("option.ResponseText") &&
            customerPanel.Contains("CustomerDialogueTextFormatter.CustomerSpeakerName"));
    }

    private static void TestCustomerDialogueUsesNarrativeTextPresenter()
    {
        var customerPanel = ReadProjectFile("Scripts/UI/CustomerPanel.cs");
        var presenter = ReadProjectFile("Scripts/UI/Text/NarrativeTextPresenter.cs");
        var converter = ReadProjectFile("Scripts/UI/Text/CustomerDialogueMarkupConverter.cs");
        var debugPanel = ReadProjectFile("Scripts/Debug/RuntimeDebugImGui.cs");

        AssertTrue("CustomerPanel creates a narrative text presenter for dialogue",
            customerPanel.Contains("new NarrativeTextPresenter(this, _dialogue)") &&
            customerPanel.Contains("_dialoguePresenter.PlayQueued(completedAction)") &&
            customerPanel.Contains("_dialoguePresenter?.AdvanceQueuedPresentation()"));
        AssertTrue("CustomerPanel no longer slices authored text during reveal",
            !customerPanel.Contains("GetVisibleDialogueText(") &&
            !customerPanel.Contains("_activeDialogueTextCharacters") &&
            !customerPanel.Contains("_pendingDialogueLines"));
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
        var customerPanel = ReadProjectFile("Scripts/UI/CustomerPanel.cs");
        var dropBox = ReadProjectFile("Scripts/UI/CustomerSellDropBox.cs");

        AssertTrue("CustomerPanel enables dropbox during pending sale state",
            customerPanel.Contains("SetDropBoxEnabled(true);"));
        AssertTrue("CustomerPanel disables dropbox during resolved sale state",
            customerPanel.Contains("SetDropBoxEnabled(false);"));
        AssertTrue("CustomerPanel only highlights valid potion hover previews",
            customerPanel.Contains("_sellDropBox.SetHoverHighlight(true);") &&
            customerPanel.Contains("IsPotionItem(itemId)"));
        AssertTrue("CustomerPanel clears the sell drop highlight on hover exit",
            customerPanel.Contains("_sellDropBox.SetHoverHighlight(false);"));
        AssertTrue("CustomerPanel fades the sell box while the sale is resolved",
            customerPanel.Contains("_sellDropBox.SetDisabledVisual(!enabled);"));
        AssertTrue("CustomerPanel restores the sell box when a new customer is prepared",
            customerPanel.Contains("SetDropBoxEnabled(true);"));
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

    private static void TestCustomerPanelShowsDraggablePotionSaleSlots()
    {
        var customerPanel = ReadProjectFile("Scripts/UI/CustomerPanel.cs");
        var detailPanel = ReadProjectFile("Scripts/UI/StationItemDetailPanel.cs");
        var formatter = ReadProjectFile("Scripts/UI/CustomerDialogueTextFormatter.cs");
        var inventorySlot = ReadProjectFile("Scripts/UI/InventoryItemSlot.cs");
        var jarredSlot = ReadProjectFile("Scripts/UI/JarredInventorySlotView.cs");
        var layoutSettings = ReadProjectFile("Assets/UI/InventorySlotLayoutSettings.tres");

        AssertTrue("CustomerPanel defines exactly four customer potion slots",
            customerPanel.Contains("CustomerPotionSlotCount = 4") &&
            customerPanel.Contains("for (var index = 0; index < CustomerPotionSlotCount; index += 1)"));
        AssertTrue("CustomerPanel places a potion slot row above customer action buttons",
            customerPanel.Contains("Name = \"PotionSlots\"") &&
            customerPanel.Contains("customerVBox.MoveChild(_potionSlotsRow, _customerActions.GetIndex())"));
        AssertTrue("CustomerPanel uses inventory slots so customer potion slots drag item ids",
            customerPanel.Contains("InventoryItemSlot") &&
            inventorySlot.Contains("return Variant.CreateFrom(ItemId);"));
        AssertTrue("CustomerPanel exposes visible potion slots for tutorial highlighting without the removed inventory panel",
            customerPanel.Contains("public Control? GetVisiblePotionSlot(string itemId)") &&
            !customerPanel.Contains("InventoryPanelPath") &&
            !customerPanel.Contains("OpenItemDetail"));
        AssertTrue("CustomerPanel fills potion slots from current potion inventory",
            customerPanel.Contains("_gameState.Inventory") &&
            customerPanel.Contains("Where(stack => IsPotionItem(stack.Key) && stack.Value > 0)") &&
            customerPanel.Contains("SetPotionSlotContent(slotView.Button, displayName, itemId, quantity)"));
        AssertTrue("CustomerPanel leaves empty potion slots visible but disabled",
            customerPanel.Contains("ClearPotionSlot") &&
            customerPanel.Contains("slotView.Button.Disabled = true") &&
            customerPanel.Contains("ClearPotionSlotContent(slotView.Button)"));
        AssertTrue("CustomerPanel shows potion slot quantity badges",
            jarredSlot.Contains("Name = \"Quantity\"") &&
            jarredSlot.Contains("PotionLiquidView") &&
            customerPanel.Contains("InventorySlotLayoutKind.CustomerPotion") &&
            layoutSettings.Contains("CustomerPotionSlot = SubResource(\"Resource_customer_potion\")") &&
            layoutSettings.Contains("QuantityFontSize = 10") &&
            customerPanel.Contains("slotView.Button.TooltipText = $\"{displayName} x{quantity}\""));
        AssertTrue("CustomerPanel shows potion slots only when a potion can be sold",
            customerPanel.Contains("SetPotionSlotRowVisible(true)") &&
            customerPanel.Contains("SetPotionSlotRowVisible(false)") &&
            customerPanel.Contains("SetDialogueOptionState") &&
            customerPanel.Contains("SetSellingModeState"));
        AssertTrue("CustomerPanel refreshes potion slots when inventory changes",
            customerPanel.Contains("_gameState.Changed += RefreshPotionSlotRow") &&
            customerPanel.Contains("_gameState.Changed -= RefreshPotionSlotRow"));
        AssertTrue("CustomerPanel selects a potion slot and updates request comparison text",
            customerPanel.Contains("CustomerPotionDetailPanelPath") &&
            customerPanel.Contains("ResolveOrCreateCustomerPotionDetailPanel") &&
            customerPanel.Contains("slot.SlotActivated += OnPotionSlotActivated") &&
            customerPanel.Contains("SetSelectedPotionComparison") &&
            customerPanel.Contains("SetRequestTraits(request, brewResult.Traits, brewResult.Risks)") &&
            customerPanel.Contains("RestoreSelectedPotionComparisonOrRequest"));
        AssertTrue("CustomerPanel disables the old customer potion detail overlay on slot clicks",
            !customerPanel.Contains(".ShowCustomerPotionComparison("));
        AssertTrue("CustomerPanel highlights the selected customer potion slot",
            customerPanel.Contains("_selectedPotionComparisonItemId") &&
            customerPanel.Contains("SetPotionSlotSelectedVisual") &&
            customerPanel.Contains("SelectedPotionSlotBorderColor"));
        AssertTrue("Customer potion details reuse the station detail panel and compare request fit",
            detailPanel.Contains("CreateDefaultPanel") &&
            detailPanel.Contains("BuildCustomerPotionRequestComparisonText") &&
            detailPanel.Contains("SetCustomerComparisonLayout(true)") &&
            detailPanel.Contains("_meta.Visible = !active") &&
            detailPanel.Contains("TextServer.AutowrapMode.Off") &&
            formatter.Contains("BuildCustomerPotionRequestComparisonText"));
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
                AssertTrue(
                    $"{interaction.Id} desired trait '{desired.Key}' has a maximum",
                    desired.Value?.HasMax == true);

                if (desired.Value?.Min is int min && desired.Value.Max is int max)
                    AssertTrue($"{interaction.Id} desired trait '{desired.Key}' has a valid range", min <= max);
            }
        }
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
