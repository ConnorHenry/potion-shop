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
        runner.Run("GameState can forget book records for debug toggles", TestBookRecordsCanBeForgottenForDebugToggles);
    }

    private static void TestStartingInventorySeedsOnlyTutorialRecipeItems()
    {
        var source = ReadProjectFile("Scripts/Autoload/GameState.cs");

        AssertTrue("GameState defines a curated starter inventory",
            source.Contains("private static readonly (string ItemId, int Quantity)[] StartingInventory"));
        AssertTrue("GameState starts with Grave Mint",
            source.Contains("(\"grave_mint\", 1)"));
        AssertTrue("GameState starts with Obsidian Resin",
            source.Contains("(\"obsidian_resin\", 1)"));
        AssertTrue("GameState starts with Iron Lullaby Root",
            source.Contains("(\"iron_lullaby_root\", 1)"));
        AssertTrue("GameState seeds only the curated list instead of every ingredient",
            source.Contains("foreach (var (itemId, qty) in StartingInventory)") &&
            !source.Contains("AddStartingStack(item.Id, 10);"));
    }

    private static void TestKnownIngredientBookEntriesPersistAndBackfill()
    {
        var gameStateSource = ReadProjectFile("Scripts/Autoload/GameState.cs");
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
            gameStateSource.Contains("Inventory[itemId] = Inventory.GetValueOrDefault(itemId) + quantityToAdd;"));
        AssertTrue("GameState learns ingredients from planted and harvested garden pots",
            gameStateSource.Contains("AddKnownIngredient(crop.IngredientId, emitChanged: false);") &&
            gameStateSource.Contains("AddKnownIngredient(pot.IngredientId, emitChanged: false);"));
        AssertTrue("GameState learns recipe ingredients from recorded known recipes",
            gameStateSource.Contains("foreach (var ingredientId in ingredientIds)") &&
            gameStateSource.Contains("changed |= AddKnownIngredient(ingredientId, emitChanged: false);"));
        AssertTrue("GameState backfills old saves from inventory, garden pots, and known recipes",
            gameStateSource.Contains("BackfillKnownIngredientsFromInventory();") &&
            gameStateSource.Contains("BackfillKnownIngredientsFromGardenPots();") &&
            gameStateSource.Contains("BackfillKnownIngredientsFromKnownRecipes();"));
    }

    private static void TestBookRecordsCanBeForgottenForDebugToggles()
    {
        var gameStateSource = ReadProjectFile("Scripts/Autoload/GameState.cs");

        AssertTrue("GameState can forget potions from the potion book",
            gameStateSource.Contains("public void ForgetPotion(string potionId)") &&
            gameStateSource.Contains("KnownPotions.RemoveWhere") &&
            gameStateSource.Contains("KnownPotionOrder.RemoveAll"));
        AssertTrue("GameState can forget ingredients from the ingredient book",
            gameStateSource.Contains("public void ForgetIngredient(string ingredientId)") &&
            gameStateSource.Contains("KnownIngredients.RemoveWhere") &&
            gameStateSource.Contains("KnownIngredientOrder.RemoveAll"));
    }
}
