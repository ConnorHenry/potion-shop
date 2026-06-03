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

internal static class PersistenceTests
{
    public static void Register(TestRunner runner)
    {
        runner.Run("Potion base price survives snapshot round-trips", TestPotionBasePriceSnapshotRoundTrip);
        runner.Run("Consumable and treatment metadata survive persistence", TestConsumableTreatmentMetadataPersists);
        runner.Run("ItemDef price converter accepts price fields", TestItemDefPriceConverterSupportsPriceFields);
        runner.Run("SaveGameManager stores saves in a dedicated directory", TestSaveGameManagerUsesSaveDirectory);
        runner.Run("Persistence boundary stays separated", TestPersistenceBoundaryIsDocumented);
    }

    private static void TestPotionBasePriceSnapshotRoundTrip()
    {
        var gameStateSource = ReadProjectFile("Scripts/Autoload/GameState.cs");
        var saveDataSource = ReadProjectFile("Scripts/Persistence/SaveData.cs");

        AssertTrue("GameState tracks potion base prices in a dedicated map",
            gameStateSource.Contains("_potionBasePrices"));
        AssertTrue("GameState registers potion base prices once per potion",
            gameStateSource.Contains("if (_potionBasePrices.ContainsKey(potionId))"));
        AssertTrue("GameState snapshot exports potion base prices",
            gameStateSource.Contains("PotionBasePrices = new Dictionary<string, int>(_potionBasePrices, StringComparer.OrdinalIgnoreCase)"));
        AssertTrue("GameState snapshot restores potion base prices",
            gameStateSource.Contains("if (snapshot.PotionBasePrices is not null)"));
        AssertTrue("GameState exposes a lookup for potion base prices",
            gameStateSource.Contains("TryGetPotionBasePrice(string potionId, out int basePrice)"));
        AssertTrue("Save data persists potion base prices",
            saveDataSource.Contains("PotionBasePrices"));
    }

    private static void TestConsumableTreatmentMetadataPersists()
    {
        var itemDef = ReadProjectFile("Scripts/Models/ItemDef.cs");
        var converter = ReadProjectFile("Scripts/Models/ItemDefJsonConverter.cs");
        var resource = ReadProjectFile("Scripts/Models/ItemDefResource.cs");
        var dataDb = ReadProjectFile("Scripts/Autoload/DataDb.cs");
        var saveData = ReadProjectFile("Scripts/Persistence/SaveData.cs");
        var gameState = ReadProjectFile("Scripts/Autoload/GameState.cs");

        AssertTrue("ItemDef stores consumable and treatment metadata",
            itemDef.Contains("ConsumableEffectDef? ConsumableEffect") &&
            itemDef.Contains("ConsumableGateDef? ConsumableGate") &&
            itemDef.Contains("ItemTreatmentDef? Treatment"));
        AssertTrue("ItemDef JSON converter reads and writes consumable metadata",
            converter.Contains("case \"consumableEffect\":") &&
            converter.Contains("case \"consumableGate\":") &&
            converter.Contains("case \"treatment\":") &&
            converter.Contains("writer.WritePropertyName(\"consumableEffect\")") &&
            converter.Contains("writer.WritePropertyName(\"treatment\")"));
        AssertTrue("ItemDefResource mirrors consumable and treatment metadata",
            resource.Contains("ConsumableEffectKind") &&
            resource.Contains("ConsumableAllowedTargetTags") &&
            resource.Contains("TreatmentBaseItemId") &&
            resource.Contains("item.Treatment = new ItemTreatmentDef"));
        AssertTrue("DataDb parses authored consumable and treatment metadata",
            dataDb.Contains("ParseConsumableEffect(ReadDictionary(entry, \"consumableEffect\"))") &&
            dataDb.Contains("ParseConsumableGate(ReadDictionary(entry, \"consumableGate\"))") &&
            dataDb.Contains("ParseTreatment(ReadDictionary(entry, \"treatment\"))"));
        AssertTrue("Pending consumable grants are saved in GameState snapshots",
            saveData.Contains("PendingConsumableItemId") &&
            saveData.Contains("PendingConsumableQuantity") &&
            gameState.Contains("PendingConsumableItemId = PendingConsumableItemId") &&
            gameState.Contains("RestorePendingConsumableGrant(snapshot.PendingConsumableItemId, snapshot.PendingConsumableQuantity)"));
    }

    private static void TestItemDefPriceConverterSupportsPriceFields()
    {
        var itemDefType = GetTypeFromUiAssembly("OccultShop.Models.ItemDef");

        var authoredJson = "{\"id\":\"brew_moon_draught\",\"name\":\"Moon Draught\",\"price\":42,\"quality\":88}";
        var authoredItem = JsonSerializer.Deserialize(authoredJson, itemDefType)
            ?? throw new InvalidOperationException("Could not deserialize authored ItemDef JSON.");
        AssertEqual("Authored price populates BasePrice", 42, GetProperty<int>(authoredItem, "BasePrice"));

        var serialized = JsonSerializer.Serialize(authoredItem, itemDefType);
        AssertTrue("Serialized item uses the price field", serialized.Contains("\"price\":42"));
        AssertTrue("Serialized item does not write BasePrice", !serialized.Contains("BasePrice"));

        var legacyJson = "{\"id\":\"brew_legacy\",\"name\":\"Legacy Brew\",\"BasePrice\":19}";
        var legacyItem = JsonSerializer.Deserialize(legacyJson, itemDefType)
            ?? throw new InvalidOperationException("Could not deserialize legacy ItemDef JSON.");
        AssertEqual("Legacy BasePrice still loads", 19, GetProperty<int>(legacyItem, "BasePrice"));
    }

    private static void TestSaveGameManagerUsesSaveDirectory()
    {
        var source = ReadProjectFile("Scripts/Autoload/SaveGameManager.cs");

        AssertTrue("SaveGameManager uses a save directory", source.Contains("user://saves"));
        AssertTrue("SaveGameManager can enumerate saved games", source.Contains("GetSavedGames()"));
        AssertTrue("SaveGameManager can load an explicit save", source.Contains("LoadGame(string saveFilePath)"));
        AssertTrue("SaveGameManager can load the latest save", source.Contains("LoadLatestGameIfExists()"));
        AssertTrue("SaveGameManager can delete save files", source.Contains("DeleteSaveGame(string saveFilePath)"));
        AssertTrue("SaveGameManager generates separate save files", source.Contains("BuildUniqueSaveFilePath"));
        AssertTrue("SaveGameManager remembers the active save file", source.Contains("_activeSaveFilePath"));
        AssertTrue("SaveGameManager overwrites the active save file", source.Contains("string.IsNullOrWhiteSpace(_activeSaveFilePath)"));
    }

    private static void TestPersistenceBoundaryIsDocumented()
    {
        var persistenceBoundary = ReadProjectFile("PERSISTENCE_BOUNDARY.md");
        AssertTrue("Persistence boundary note exists", persistenceBoundary.Contains("runtime save/load system"));
        AssertTrue("Persistence boundary documents save directory", persistenceBoundary.Contains("user://saves/"));
        AssertTrue("Authored data reload rule documented", persistenceBoundary.Contains("Authored data: always reload from `res://Data/authored_data.tres`"));
        AssertTrue("Runtime catalog save rule documented", persistenceBoundary.Contains("Runtime-generated item catalog: persist separately from authored data"));
        AssertTrue("Player state save rule documented", persistenceBoundary.Contains("Player state: save independently"));
    }
}
