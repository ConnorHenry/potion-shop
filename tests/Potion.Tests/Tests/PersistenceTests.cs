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
        runner.Run("Ingredient effect metadata survives item conversion", TestIngredientEffectMetadataPersists);
        runner.Run("Prepared ingredient metadata survives item conversion", TestPreparedIngredientMetadataPersists);
        runner.Run("ItemDef price converter accepts price fields", TestItemDefPriceConverterSupportsPriceFields);
        runner.Run("Customer request trait ranges support legacy JSON", TestCustomerRequestTraitRangeJsonCompatibility);
        runner.Run("Player name survives persistence and save summaries", TestPlayerNamePersistence);
        runner.Run("SaveGameManager stores saves in a dedicated directory", TestSaveGameManagerUsesSaveDirectory);
        runner.Run("Persistence boundary stays separated", TestPersistenceBoundaryIsDocumented);
    }

    private static void TestPotionBasePriceSnapshotRoundTrip()
    {
        var gameStateSource = ReadProjectFile("Scripts/Autoload/GameState.cs");
        var potionKnowledgeState = ReadProjectFile("Scripts/Systems/PotionKnowledgeState.cs");
        var saveDataSource = ReadProjectFile("Scripts/Persistence/SaveData.cs");

        AssertTrue("GameState tracks potion base prices in a dedicated map",
            potionKnowledgeState.Contains("_potionBasePrices"));
        AssertTrue("GameState registers potion base prices once per potion",
            potionKnowledgeState.Contains("if (_potionBasePrices.ContainsKey(potionId))"));
        AssertTrue("GameState snapshot exports potion base prices",
            gameStateSource.Contains("PotionBasePrices = _potionKnowledgeState.ClonePotionBasePrices()") &&
            potionKnowledgeState.Contains("new Dictionary<string, int>(_potionBasePrices, StringComparer.OrdinalIgnoreCase)"));
        AssertTrue("GameState snapshot restores potion base prices",
            gameStateSource.Contains("_potionKnowledgeState.Restore(snapshot)") &&
            potionKnowledgeState.Contains("RestorePotionBasePrices(snapshot.PotionBasePrices)"));
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
        var inventoryState = ReadProjectFile("Scripts/Systems/InventoryState.cs");

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
            gameState.Contains("_inventoryState.Restore(snapshot.Inventory, snapshot.PendingConsumableItemId, snapshot.PendingConsumableQuantity)") &&
            inventoryState.Contains("RestorePendingConsumableGrant(pendingConsumableItemId, pendingConsumableQuantity)"));
    }

    private static void TestIngredientEffectMetadataPersists()
    {
        var itemDef = ReadProjectFile("Scripts/Models/ItemDef.cs");
        var converter = ReadProjectFile("Scripts/Models/ItemDefJsonConverter.cs");
        var resource = ReadProjectFile("Scripts/Models/ItemDefResource.cs");
        var dataDb = ReadProjectFile("Scripts/Autoload/DataDb.cs");
        var runtimeDb = ReadProjectFile("Scripts/Autoload/RuntimeContentDb.cs");

        AssertTrue("ItemDef stores ingredient effect metadata",
            itemDef.Contains("List<IngredientEffectDef> IngredientEffects"));
        AssertTrue("ItemDef JSON converter reads and writes ingredient effects",
            converter.Contains("case \"ingredientEffects\":") &&
            converter.Contains("writer.WritePropertyName(\"ingredientEffects\")"));
        AssertTrue("ItemDefResource mirrors ingredient effect metadata",
            resource.Contains("IngredientEffects") &&
            resource.Contains("ParseIngredientEffects") &&
            resource.Contains("BuildIngredientEffectArray"));
        AssertTrue("DataDb parses authored ingredient effects",
            dataDb.Contains("ParseIngredientEffects(ReadArray(entry, \"ingredientEffects\"))"));
        AssertTrue("RuntimeContentDb clones ingredient effects",
            runtimeDb.Contains("IngredientEffects = CloneIngredientEffects(item.IngredientEffects)"));

        var json = "{\"id\":\"test_root\",\"name\":\"Test Root\",\"ingredientEffects\":[{\"kind\":\"boost_lowest_other_trait\",\"name\":\"Echo\",\"amount\":2}]}";
        var item = JsonSerializer.Deserialize<ItemDef>(json)
            ?? throw new InvalidOperationException("Could not deserialize ItemDef with ingredient effects.");
        AssertEqual("Effect count", 1, item.IngredientEffects.Count);
        AssertEqual("Effect kind", IngredientEffectDef.BoostLowestOtherTraitKind, item.IngredientEffects[0].Kind);
        AssertEqual("Effect amount", 2, item.IngredientEffects[0].Amount);

        var serialized = JsonSerializer.Serialize(item);
        AssertTrue("Serialized item includes ingredient effects",
            serialized.Contains("\"ingredientEffects\"") &&
            serialized.Contains("boost_lowest_other_trait"));
    }

    private static void TestPreparedIngredientMetadataPersists()
    {
        var itemDef = ReadProjectFile("Scripts/Models/ItemDef.cs");
        var converter = ReadProjectFile("Scripts/Models/ItemDefJsonConverter.cs");
        var resource = ReadProjectFile("Scripts/Models/ItemDefResource.cs");
        var dataDb = ReadProjectFile("Scripts/Autoload/DataDb.cs");
        var runtimeDb = ReadProjectFile("Scripts/Autoload/RuntimeContentDb.cs");

        AssertTrue("ItemDef stores preparation and prepared ingredient metadata",
            itemDef.Contains("Dictionary<string, IngredientPreparationDef> Preparations") &&
            itemDef.Contains("PreparedIngredientDef? PreparedIngredient"));
        AssertTrue("ItemDef JSON converter reads and writes preparation metadata",
            converter.Contains("case \"preparations\":") &&
            converter.Contains("case \"preparedIngredient\":") &&
            converter.Contains("writer.WritePropertyName(\"preparations\")") &&
            converter.Contains("writer.WritePropertyName(\"preparedIngredient\")"));
        AssertTrue("ItemDefResource mirrors prepared ingredient metadata",
            resource.Contains("PreparedIngredientBaseItemId") &&
            resource.Contains("PreparedIngredientPreparationId") &&
            resource.Contains("item.PreparedIngredient = new PreparedIngredientDef"));
        AssertTrue("DataDb parses authored preparation metadata",
            dataDb.Contains("ParsePreparations(ReadDictionary(entry, \"preparations\"))") &&
            dataDb.Contains("ParsePreparedIngredient(ReadDictionary(entry, \"preparedIngredient\"))"));
        AssertTrue("RuntimeContentDb clones preparation metadata",
            runtimeDb.Contains("Preparations = ClonePreparations(item.Preparations)") &&
            runtimeDb.Contains("PreparedIngredient = item.PreparedIngredient is null"));

        var json = "{\"id\":\"mint__prep_crushed\",\"name\":\"Mint (Crushed)\",\"preparedIngredient\":{\"baseIngredientId\":\"mint\",\"preparationId\":\"crushed\"}}";
        var item = JsonSerializer.Deserialize<ItemDef>(json)
            ?? throw new InvalidOperationException("Could not deserialize prepared ingredient ItemDef.");
        AssertEqual("Prepared metadata base", "mint", item.PreparedIngredient?.BaseIngredientId ?? "");
        AssertEqual("Prepared metadata method", "crushed", item.PreparedIngredient?.PreparationId ?? "");

        var serialized = JsonSerializer.Serialize(item);
        AssertTrue("Serialized item includes prepared ingredient metadata",
            serialized.Contains("\"preparedIngredient\"") &&
            serialized.Contains("\"baseIngredientId\":\"mint\"") &&
            serialized.Contains("\"preparationId\":\"crushed\""));
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

    private static void TestCustomerRequestTraitRangeJsonCompatibility()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var legacyJson = "{\"id\":\"nerves\",\"desiredTraits\":{\"calming\":2},\"badTraits\":{\"confusion\":0}}";
        var legacyRequest = JsonSerializer.Deserialize<CustomerRequestDef>(legacyJson, options)
            ?? throw new InvalidOperationException("Could not deserialize legacy CustomerRequestDef JSON.");

        AssertEqual("Legacy desired integer becomes min", 2, legacyRequest.DesiredTraits["calming"].Min ?? -1);
        AssertTrue("Legacy desired integer has no max", legacyRequest.DesiredTraits["calming"].Max is null);
        AssertTrue("Legacy bad integer has no min", legacyRequest.BadTraits["confusion"].Min is null);
        AssertEqual("Legacy bad integer becomes max", 0, legacyRequest.BadTraits["confusion"].Max ?? -1);

        var rangedJson = "{\"id\":\"nerves\",\"desiredTraits\":{\"calming\":{\"min\":2,\"max\":4},\"clarity\":{\"min\":1}},\"badTraits\":{\"drowsiness\":{\"max\":1}}}";
        var rangedRequest = JsonSerializer.Deserialize<CustomerRequestDef>(rangedJson, options)
            ?? throw new InvalidOperationException("Could not deserialize ranged CustomerRequestDef JSON.");

        AssertEqual("Ranged desired min loads", 2, rangedRequest.DesiredTraits["calming"].Min ?? -1);
        AssertEqual("Ranged desired max loads", 4, rangedRequest.DesiredTraits["calming"].Max ?? -1);
        AssertEqual("Ranged bad max loads", 1, rangedRequest.BadTraits["drowsiness"].Max ?? -1);

        var serialized = JsonSerializer.Serialize(rangedRequest, options);
        AssertTrue("Serialized request writes min and max range fields",
            serialized.Contains("\"min\":2") &&
            serialized.Contains("\"max\":4") &&
            !serialized.Contains("HasMin") &&
            !serialized.Contains("HasMax"));
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

    private static void TestPlayerNamePersistence()
    {
        var gameState = ReadProjectFile("Scripts/Autoload/GameState.cs");
        var saveData = ReadProjectFile("Scripts/Persistence/SaveData.cs");
        var saveManager = ReadProjectFile("Scripts/Autoload/SaveGameManager.cs");
        var saveSummary = ReadProjectFile("Scripts/Persistence/SaveGameSummary.cs");

        AssertTrue("GameState exposes player name",
            gameState.Contains("public string PlayerName { get; private set; }"));
        AssertTrue("GameState resets and sets player name explicitly",
            gameState.Contains("PlayerName = \"\";") &&
            gameState.Contains("public void SetPlayerName(string playerName)") &&
            gameState.Contains("playerName.Trim()"));
        AssertTrue("GameState snapshot exports and restores player name",
            gameState.Contains("PlayerName = PlayerName") &&
            gameState.Contains("SetPlayerName(snapshot.PlayerName, emitChanged: false)") &&
            saveData.Contains("public string PlayerName { get; set; } = \"\";"));
        AssertTrue("SaveGameManager accepts player name during new game",
            saveManager.Contains("StartNewGame(bool startTutorial, string playerName)") &&
            saveManager.Contains("_gameState.SetPlayerName(playerName);"));
        AssertTrue("SaveGameManager copies player name to summaries",
            saveManager.Contains("summary.PlayerName = saveData.GameState.PlayerName"));
        AssertTrue("Save summary display includes player name",
            saveSummary.Contains("public string PlayerName { get; set; } = \"\";") &&
            saveSummary.Contains("PlayerName.Trim()") &&
            saveSummary.Contains("Unnamed Player"));
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
