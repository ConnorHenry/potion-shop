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
            !source.Contains("AddStartingStack(item.Id, 10);") &&
            !source.Contains("IsIngredient(item)"));
    }
}
