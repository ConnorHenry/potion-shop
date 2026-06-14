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

internal static class GameStateTests
{
    public static void Register(TestRunner runner)
    {
        runner.Run("GameState seeds only the starter potion ingredients", TestStartingInventorySeedsOnlyTutorialRecipeItems);
        runner.Run("GameState persists and backfills known ingredient book entries", TestKnownIngredientBookEntriesPersistAndBackfill);
        runner.Run("GameState persists ingredient preparation knowledge separately", TestIngredientPreparationKnowledgePersistsSeparately);
        runner.Run("GameState can forget book records for debug toggles", TestBookRecordsCanBeForgottenForDebugToggles);
    }

    private static void TestStartingInventorySeedsOnlyTutorialRecipeItems()
    {
        var source = ReadProjectFile("Scripts/Autoload/GameState.cs");

        AssertTrue("GameState defines a curated starter inventory",
            source.Contains("private static readonly (string ItemId, int Quantity)[] StartingInventory"));
        AssertTrue("GameState starts with Mint",
            source.Contains("(\"mint\", 1)"));
        AssertTrue("GameState starts with Gorse",
            source.Contains("(\"gorse\", 1)"));
        AssertTrue("GameState starts with Thyme",
            source.Contains("(\"thyme\", 1)"));
        AssertTrue("GameState seeds only the curated list instead of every ingredient",
            source.Contains("foreach (var (itemId, qty) in StartingInventory)") &&
            !source.Contains("AddStartingStack(item.Id, 10);"));
    }

    private static void TestKnownIngredientBookEntriesPersistAndBackfill()
    {
        var gameStateSource = ReadProjectFile("Scripts/Autoload/GameState.cs");
        var inventoryState = ReadProjectFile("Scripts/Systems/InventoryState.cs");
        var potionKnowledgeState = ReadProjectFile("Scripts/Systems/PotionKnowledgeState.cs");
        var saveDataSource = ReadProjectFile("Scripts/Persistence/SaveData.cs");
        var itemDefSource = ReadProjectFile("Scripts/Models/ItemDef.cs");
        var dataDbSource = ReadProjectFile("Scripts/Autoload/DataDb.cs");
        var itemsData = ReadProjectFile("Data/items_data.tres");

        AssertTrue("Item definitions expose the ingredient book starting flag",
            itemDefSource.Contains("StartsKnownInIngredientBook"));
        AssertTrue("DataDb parses startsKnownInIngredientBook from authored item data",
            dataDbSource.Contains("StartsKnownInIngredientBook = ReadBool(entry, \"startsKnownInIngredientBook\")"));
        AssertTrue("Authored ingredient data marks entries with startsKnownInIngredientBook",
            itemsData.Contains("\"startsKnownInIngredientBook\": true") &&
            itemsData.Contains("\"startsKnownInIngredientBook\": false"));
        AssertTrue("GameState tracks known ingredients and their display order",
            gameStateSource.Contains("public HashSet<string> KnownIngredients") &&
            gameStateSource.Contains("public List<string> KnownIngredientOrder"));
        AssertTrue("Save data persists known ingredients and their order",
            saveDataSource.Contains("KnownIngredients") &&
            saveDataSource.Contains("KnownIngredientOrder"));
        AssertTrue("GameState seeds known ingredients from authored starting flags",
            gameStateSource.Contains("SeedStartingIngredientBookKnowledge()") &&
            gameStateSource.Contains("item.StartsKnownInIngredientBook"));
        AssertTrue("GameState learns ingredients when items enter inventory",
            gameStateSource.Contains("AddKnownIngredient(itemId, emitChanged: false);") &&
            inventoryState.Contains("_inventory[itemId] = _inventory.GetValueOrDefault(itemId) + quantityToAdd;"));
        AssertTrue("GameState learns ingredients from planted and harvested garden pots",
            gameStateSource.Contains("AddKnownIngredient(plantedIngredientId, emitChanged: false);") &&
            gameStateSource.Contains("AddKnownIngredient(harvest.IngredientId, emitChanged: false);"));
        AssertTrue("GameState learns recipe ingredients from recorded known recipes",
            potionKnowledgeState.Contains("foreach (var ingredientId in ingredientIds)") &&
            potionKnowledgeState.Contains("changed |= AddKnownIngredient(ingredientId);"));
        AssertTrue("GameState backfills old saves from inventory, garden pots, and known recipes",
            gameStateSource.Contains("BackfillKnownIngredientsFromInventory();") &&
            gameStateSource.Contains("BackfillKnownIngredientsFromGardenPots();") &&
            gameStateSource.Contains("_potionKnowledgeState.BackfillKnownIngredientsFromKnownRecipes();"));
    }

    private static void TestIngredientPreparationKnowledgePersistsSeparately()
    {
        var gameStateSource = ReadProjectFile("Scripts/Autoload/GameState.cs");
        var potionKnowledgeState = ReadProjectFile("Scripts/Systems/PotionKnowledgeState.cs");
        var saveDataSource = ReadProjectFile("Scripts/Persistence/SaveData.cs");

        AssertTrue("Save data persists known ingredient preparations separately from ingredient discovery",
            saveDataSource.Contains("KnownIngredientPreparations") &&
            gameStateSource.Contains("public HashSet<string> KnownIngredientPreparations"));
        AssertTrue("GameState snapshots and restores preparation knowledge without seeding it for starting ingredients",
            gameStateSource.Contains("KnownIngredientPreparations = _potionKnowledgeState.BuildKnownIngredientPreparationSnapshot()") &&
            potionKnowledgeState.Contains("RestoreKnownIngredientPreparations(snapshot.KnownIngredientPreparations)") &&
            !gameStateSource.Contains("SeedStartingIngredientPreparationKnowledge"));
        AssertTrue("GameState exposes preparation knowledge APIs",
            gameStateSource.Contains("LearnIngredientPreparation") &&
            gameStateSource.Contains("KnowsIngredientPreparation") &&
            gameStateSource.Contains("RecordIngredientPreparationKnowledge") &&
            gameStateSource.Contains("UnlockAllIngredientPreparations"));
        AssertTrue("Preparation knowledge keys are scoped by ingredient and normalized preparation id",
            potionKnowledgeState.Contains("IngredientPreparationCatalog.NormalizePreparationId(preparationId)") &&
            potionKnowledgeState.Contains("::{normalizedPreparationId}"));
    }

    private static void TestBookRecordsCanBeForgottenForDebugToggles()
    {
        var gameStateSource = ReadProjectFile("Scripts/Autoload/GameState.cs");
        var potionKnowledgeState = ReadProjectFile("Scripts/Systems/PotionKnowledgeState.cs");

        AssertTrue("GameState can forget potions from the potion book",
            gameStateSource.Contains("public void ForgetPotion(string potionId)") &&
            potionKnowledgeState.Contains("_knownPotions.RemoveWhere") &&
            potionKnowledgeState.Contains("_knownPotionOrder.RemoveAll"));
        AssertTrue("GameState can forget ingredients from the ingredient book",
            gameStateSource.Contains("public void ForgetIngredient(string ingredientId)") &&
            potionKnowledgeState.Contains("_knownIngredients.RemoveWhere") &&
            potionKnowledgeState.Contains("_knownIngredientOrder.RemoveAll"));
    }
}
