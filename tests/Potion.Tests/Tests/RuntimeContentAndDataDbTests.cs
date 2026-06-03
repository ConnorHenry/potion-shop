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
        AssertTrue("Authored data resource stores event catalog", resource.Contains("EventsPath = \"res://Data/events_data.tres\""));
        AssertTrue("Authored data resource stores customer catalog",
            resource.Contains("CustomerInteractionsPath = \"res://Data/customers_data.tres\"") ||
            resource.Contains("CustomerInteractionsPath = \"res://Data/customers_tiered_test_data.tres\""));
        AssertTrue("Authored data resource stores synergy catalog", resource.Contains("SynergiesPath = \"res://Data/synergies_data.tres\""));
        AssertTrue("DataDb does not register runtime items", !source.Contains("RegisterRuntimePotionItem"));
        AssertTrue("DataDb does not reference runtime catalog", !source.Contains("RuntimeContentDb"));
    }

    private static void TestUiLookupUsesRuntimeFirstCatalog()
    {
        var itemCatalog = ReadProjectFile("Scripts/Autoload/ItemCatalog.cs");
        var itemCatalogService = ReadProjectFile("Scripts/Autoload/ItemCatalogService.cs");
        AssertTrue("ItemCatalog static wrapper delegates to the service", itemCatalog.Contains("Service.TryGetItem(itemId, out item)"));
        AssertTrue("ItemCatalogService checks runtime first", itemCatalogService.Contains("_runtimeContentDb.TryGetItem(itemId, out item)"));
        AssertTrue("ItemCatalogService falls back to DataDb", itemCatalogService.Contains("_dataDb.TryGetItem(itemId, out item)"));

        var brewPanel = ReadProjectFile("Scripts/UI/BrewPanel.cs");
        var inventoryPanel = ReadProjectFile("Scripts/UI/InventoryPanel.cs");
        var recipeBookPanel = ReadProjectFile("Scripts/UI/RecipeBookPanel.cs");
        var customerPanel = ReadProjectFile("Scripts/UI/CustomerPanel.cs");
        var brewService = ReadProjectFile("Scripts/Systems/PotionInventoryBrewService.cs");

        AssertTrue("BrewPanel resolves ItemCatalogService through an exported path", brewPanel.Contains("GetNodeOrNull<ItemCatalogService>(ItemCatalogPath)"));
        AssertTrue("InventoryPanel resolves ItemCatalogService through an exported path", inventoryPanel.Contains("GetNodeOrNull<ItemCatalogService>(ItemCatalogPath)"));
        AssertTrue("InventoryPanel exposes item type tag path for detail view", ReadProjectFile("Scenes/UI/InventoryPanel.tscn").Contains("ItemDetailTypeTagPath = NodePath(\"../InventoryItemDetail/Panel/Margin/VBox/TopRow/Identity/TypeTag\")"));
        AssertTrue("InventoryPanel uses player-visible tag rules for item type text", inventoryPanel.Contains("ItemTagDisplayRules"));
        AssertTrue("RecipeBookPanel resolves ItemCatalogService through an exported path", recipeBookPanel.Contains("GetNodeOrNull<ItemCatalogService>(ItemCatalogPath)"));
        AssertTrue("CustomerPanel resolves ItemCatalogService through an exported path", customerPanel.Contains("GetNodeOrNull<ItemCatalogService>(ItemCatalogPath)"));
        AssertTrue("PotionInventoryBrewService uses constructor-injected ItemCatalogService", brewService.Contains("PotionInventoryBrewService(GameState gameState, ItemCatalogService itemCatalog)"));
        AssertTrue("BrewPanel still registers runtime potions separately", brewPanel.Contains("RegisterRuntimePotionItem"));
    }
}
