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

internal static class RuntimeContentAndDataDbTests
{
    public static void Register(TestRunner runner)
    {
        runner.Run("RuntimeContentDb stores generated items separately", TestRuntimeContentDbSeparatesRuntimeItems);
        runner.Run("DataDb does not expose runtime registration", TestDataDbDoesNotExposeRuntimeRegistration);
        runner.Run("DataDb reloads authored resource catalogs only", TestDataDbReloadsAuthoredResourceCatalogsOnly);
        runner.Run("Authored ingredient preparations expose two trait prep contract", TestAuthoredIngredientPreparationsExposeTwoTraitPrepContract);
        runner.Run("UI lookup uses the runtime-first item catalog", TestUiLookupUsesRuntimeFirstCatalog);
    }

    private static void TestRuntimeContentDbSeparatesRuntimeItems()
    {
        var runtimeDbType = GetTypeFromUiAssembly("OccultShop.Autoload.RuntimeContentDb");
        var registerMethod = runtimeDbType.GetMethod("RegisterRuntimePotionItem", BindingFlags.Public | BindingFlags.Instance);
        var clearMethod = runtimeDbType.GetMethod("ClearRuntimeItems", BindingFlags.Public | BindingFlags.Instance);
        var itemsProperty = runtimeDbType.GetProperty("Items", BindingFlags.Public | BindingFlags.Instance);
        var changedEvent = runtimeDbType.GetEvent("Changed", BindingFlags.Public | BindingFlags.Instance);

        AssertTrue("RuntimeContentDb exposes runtime registration", registerMethod is not null);
        AssertTrue("RuntimeContentDb exposes runtime clearing", clearMethod is not null);
        AssertTrue("RuntimeContentDb exposes item registry", itemsProperty is not null);
        AssertTrue("RuntimeContentDb exposes change notification", changedEvent is not null);

        AssertEqual("Runtime registration return type", "OccultShop.Models.ItemDef", registerMethod!.ReturnType.FullName ?? registerMethod.ReturnType.Name);
        AssertTrue("Runtime item registry is IReadOnlyDictionary",
            itemsProperty!.PropertyType.IsGenericType &&
            itemsProperty.PropertyType.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>));

        var registryArgs = itemsProperty.PropertyType.GetGenericArguments();
        AssertEqual("Runtime item registry key type", typeof(string).FullName ?? string.Empty, registryArgs[0].FullName ?? string.Empty);
        AssertEqual("Runtime item registry value type", "OccultShop.Models.ItemDef", registryArgs[1].FullName ?? registryArgs[1].Name);
    }

    private static void TestDataDbDoesNotExposeRuntimeRegistration()
    {
        var dataDbType = GetTypeFromUiAssembly("OccultShop.Autoload.DataDb");
        var method = dataDbType.GetMethod("RegisterRuntimePotionItem", BindingFlags.Public | BindingFlags.Instance);
        AssertTrue("DataDb runtime registration removed", method is null);
    }

    private static void TestDataDbReloadsAuthoredResourceCatalogsOnly()
    {
        var source = ReadProjectFile("Scripts/Autoload/DataDb.cs");
        var resource = ReadProjectFile("Data/authored_data.tres");
        AssertTrue("DataDb reload entry point exists", source.Contains("public override void _Ready()"));
        AssertTrue("DataDb reloads on ready", source.Contains("ReloadAll();"));
        AssertTrue("DataDb loads authored data resource", source.Contains("ResourceLoader.Load<AuthoredDataResource>"));
        AssertTrue("DataDb references the authored data resource path", source.Contains("AuthoredDataPath"));
        AssertTrue("Authored data resource file exists", resource.Contains("script_class=\"AuthoredDataResource\""));
        AssertTrue("Authored data resource stores item catalog", resource.Contains("ItemsPath = \"res://Data/items_data.tres\""));
        AssertTrue("Authored data resource stores rule catalog", resource.Contains("RulesPath = \"res://Data/rules_data.tres\""));
        AssertTrue("Authored data resource no longer stores night event catalog", !resource.Contains("\nEventsPath =") && !source.Contains("AuthoredEventsResource"));
        AssertTrue("Authored data resource stores calendar event catalog", resource.Contains("CalendarEventsPath = \"res://Data/calendar_events_data.tres\""));
        AssertTrue("Authored data resource stores customer catalog",
            resource.Contains("CustomerInteractionsPath = \"res://Data/customers_data.tres\"") ||
            resource.Contains("CustomerInteractionsPath = \"res://Data/customers_tiered_test_data.tres\""));
        AssertTrue("DataDb loads authored calendar events",
            source.Contains("LoadSection<AuthoredCalendarEventsResource>") &&
            source.Contains("CalendarEvents => _calendarEvents") &&
            source.Contains("ParseCalendarEvents("));
        AssertTrue("DataDb does not register runtime items", !source.Contains("RegisterRuntimePotionItem"));
        AssertTrue("DataDb does not reference runtime catalog", !source.Contains("RuntimeContentDb"));
    }

    private static void TestAuthoredIngredientPreparationsExposeTwoTraitPrepContract()
    {
        var items = ReadAuthoredItems();
        var preparationIds = IngredientPreparationCatalog.AllOptions.Select(x => x.Id).ToList();

        foreach (var item in items.Where(IsBaseIngredient))
        {
            AssertEqual($"{item.Id} preparation count", preparationIds.Count, item.Preparations.Count);

            foreach (var preparationId in preparationIds)
                AssertTrue($"{item.Id} defines {preparationId}", item.Preparations.TryGetValue(preparationId, out var preparation));

            var rawTrait = GetSinglePreparationTrait(item, IngredientPreparationCatalog.RawPreparationId);
            var steepedTrait = GetSinglePreparationTrait(item, IngredientPreparationCatalog.SteepedPreparationId);
            var crushedTrait = GetSinglePreparationTrait(item, IngredientPreparationCatalog.CrushedPreparationId);
            var boiledTrait = GetSinglePreparationTrait(item, IngredientPreparationCatalog.BoiledPreparationId);

            AssertTrue($"{item.Id} raw low trait value", rawTrait.Value >= 2 && rawTrait.Value <= 4);
            AssertTrue($"{item.Id} steeped high trait value", steepedTrait.Value >= 5 && steepedTrait.Value <= 6);
            AssertTrue($"{item.Id} crushed low trait value", crushedTrait.Value >= 2 && crushedTrait.Value <= 4);
            AssertTrue($"{item.Id} boiled high trait value", boiledTrait.Value >= 5 && boiledTrait.Value <= 6);

            AssertEqual($"{item.Id} raw/steeped trait match", rawTrait.Key.ToLowerInvariant(), steepedTrait.Key.ToLowerInvariant());
            AssertEqual($"{item.Id} crushed/boiled trait match", crushedTrait.Key.ToLowerInvariant(), boiledTrait.Key.ToLowerInvariant());

            var traitIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                rawTrait.Key,
                steepedTrait.Key,
                crushedTrait.Key,
                boiledTrait.Key
            };
            AssertEqual($"{item.Id} has exactly two preparation traits", 2, traitIds.Count);

            AssertEqual($"{item.Id} raw risk count", 0, CountPositiveRisks(item.Preparations[IngredientPreparationCatalog.RawPreparationId]));
            AssertEqual($"{item.Id} crushed risk count", 0, CountPositiveRisks(item.Preparations[IngredientPreparationCatalog.CrushedPreparationId]));

            var steepedRiskCount = CountPositiveRisks(item.Preparations[IngredientPreparationCatalog.SteepedPreparationId]);
            var boiledRiskCount = CountPositiveRisks(item.Preparations[IngredientPreparationCatalog.BoiledPreparationId]);
            AssertEqual($"{item.Id} has exactly one risky high prep", 1, (steepedRiskCount > 0 ? 1 : 0) + (boiledRiskCount > 0 ? 1 : 0));
            AssertTrue($"{item.Id} steeped risk count is singular", steepedRiskCount <= 1);
            AssertTrue($"{item.Id} boiled risk count is singular", boiledRiskCount <= 1);
        }
    }

    private static KeyValuePair<string, int> GetSinglePreparationTrait(ItemDef item, string preparationId)
    {
        AssertTrue($"{item.Id} defines {preparationId}", item.Preparations.TryGetValue(preparationId, out var preparation));
        if (!item.Preparations.TryGetValue(preparationId, out preparation))
            return default;

        AssertEqual($"{item.Id} {preparationId} trait count", 1, preparation.Traits.Count);
        return preparation.Traits.First();
    }

    private static int CountPositiveRisks(IngredientPreparationDef preparation)
    {
        var count = 0;
        foreach (var risk in preparation.Risks)
        {
            if (!string.IsNullOrWhiteSpace(risk.Key) && risk.Value > 0)
                count += 1;
        }

        return count;
    }

    private static void TestUiLookupUsesRuntimeFirstCatalog()
    {
        var itemCatalog = ReadProjectFile("Scripts/Autoload/ItemCatalog.cs");
        var itemCatalogService = ReadProjectFile("Scripts/Autoload/ItemCatalogService.cs");
        AssertTrue("ItemCatalog static wrapper delegates to the service", itemCatalog.Contains("Service.TryGetItem(itemId, out item)"));
        AssertTrue("ItemCatalogService checks runtime first", itemCatalogService.Contains("_runtimeContentDb.TryGetItem(itemId, out item)"));
        AssertTrue("ItemCatalogService falls back to DataDb", itemCatalogService.Contains("_dataDb.TryGetItem(itemId, out item)"));

        var brewPanel = ReadProjectFile("Scripts/UI/BrewPanel.cs");
        var stationShelf = ReadProjectFile("Scripts/UI/StationShelfInventory.cs");
        var stationCustomerPanel = ReadProjectFile("Scripts/UI/StationCustomerPanel.cs");
        var brewService = ReadProjectFile("Scripts/Systems/PotionInventoryBrewService.cs");

        AssertTrue("BrewPanel resolves ItemCatalogService through an exported path", brewPanel.Contains("GetNodeOrNull<ItemCatalogService>(ItemCatalogPath)"));
        AssertTrue("StationShelfInventory resolves ItemCatalogService through an exported path", stationShelf.Contains("GetNodeOrNull<ItemCatalogService>(ItemCatalogPath)"));
        AssertTrue("StationCustomerPanel resolves ItemCatalogService through an exported path", stationCustomerPanel.Contains("GetNodeOrNull<ItemCatalogService>(ItemCatalogPath)"));
        AssertTrue("PotionInventoryBrewService uses constructor-injected ItemCatalogService", brewService.Contains("PotionInventoryBrewService(GameState gameState, ItemCatalogService itemCatalog)"));
        AssertTrue("BrewPanel still registers runtime potions separately", brewPanel.Contains("RegisterRuntimePotionItem"));
    }

    private static List<ItemDef> ReadAuthoredItems()
    {
        var source = ReadProjectFile("Data/items_data.tres");
        const string marker = "Entries = ";
        var start = source.IndexOf(marker, StringComparison.Ordinal);
        AssertTrue("Items data contains an Entries array", start >= 0);

        var json = source[(start + marker.Length)..].Trim();
        return JsonSerializer.Deserialize<List<ItemDef>>(json)
            ?? throw new InvalidOperationException("Could not parse authored items.");
    }

    private static bool IsBaseIngredient(ItemDef item)
    {
        return item.Tags.Contains(ItemTags.Ingredient, StringComparer.OrdinalIgnoreCase) &&
            item.Treatment is null &&
            item.PreparedIngredient is null;
    }
}
