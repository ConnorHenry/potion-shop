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

internal static class CustomerFlowTests
{
    public static void Register(TestRunner runner)
    {
        runner.Run("CustomerPanel creates detached ingredient snapshots", TestCustomerPanelBuildPotionIngredientDef);
        runner.Run("Customer events randomize shop-day order", TestCustomerEventControllerRandomizesOrder);
        runner.Run("Forced customer fallback resolves legacy ids deterministically", TestForcedCustomerFallbackResolvesLegacyIdsDeterministically);
        runner.Run("Customer events respect scheduling and story outcomes", TestCustomerEventSchedulingAndStoryOutcomes);
        runner.Run("Story customer dialogue options replace skip actions", TestStoryCustomerDialogueOptionsReplaceSkipActions);
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
        AssertTrue("DayController resets customer order when the shop opens", dayController.Contains("_customerEventController.BeginShopDay();"));
        AssertTrue("DayController tracks when the shop should close after the current customer", dayController.Contains("_shopClosingPending"));
        AssertTrue("DayController keeps the shop open while the current customer is active at zero seconds", dayController.Contains("_customerPanel.HasActiveInteraction || _awaitingSaleResultClose"));
        AssertTrue("CustomerPanel exposes active interaction state", customerPanel.Contains("HasActiveInteraction => _interaction is not null"));
        AssertTrue("CustomerPanel can switch the next button to Close Shop", customerPanel.Contains("SetCloseShopMode(bool closeShopMode)"));
        AssertTrue("CustomerPanel exposes close shop mode state", customerPanel.Contains("IsCloseShopMode => _closeShopMode"));
    }

    private static void TestForcedCustomerFallbackResolvesLegacyIdsDeterministically()
    {
        var customerController = ReadProjectFile("Scripts/Controllers/CustomerEventController.cs");
        var authoredData = ReadProjectFile("Data/authored_data.tres");
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
        AssertTrue("Tiered customer data contains overlapping sleep draught suffixes",
            tieredCustomers.Contains("\"id\": \"tier1_sleep_draught\"") &&
            tieredCustomers.Contains("\"id\": \"tier2_clean_sleep_draught\""));
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

    private static void TestStoryCustomerDialogueOptionsReplaceSkipActions()
    {
        var customerDef = ReadProjectFile("Scripts/Models/CustomerInteractionDef.cs");
        var dataDb = ReadProjectFile("Scripts/Autoload/DataDb.cs");
        var customerPanel = ReadProjectFile("Scripts/UI/CustomerPanel.cs");
        var dayController = ReadProjectFile("Scripts/Controllers/DayController.cs");

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
}
