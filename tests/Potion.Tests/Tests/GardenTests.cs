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

internal static class GardenTests
{
    public static void Register(TestRunner runner)
    {
        runner.Run("Garden crop definitions cover authored ingredients", TestGardenCropDefinitionsCoverAuthoredIngredients);
        runner.Run("Garden state persists seeds and pots", TestGardenStatePersistenceWiring);
        runner.Run("Garden scene and HUD navigation are wired", TestGardenSceneAndHudNavigation);
    }

    private static void TestGardenCropDefinitionsCoverAuthoredIngredients()
    {
        var source = ReadProjectFile("Scripts/Autoload/GameState.cs");
        var gardenState = ReadProjectFile("Scripts/Systems/GardenState.cs");

        AssertTrue("GameState defines three starting garden pots", source.Contains("public const int StartingGardenPotCount = GardenState.StartingPotCount;") && gardenState.Contains("public const int StartingPotCount = 3;"));
        AssertTrue("Garden harvest yield starts fixed at two", source.Contains("public const int DefaultGardenHarvestYield = GardenState.DefaultHarvestYield;") && gardenState.Contains("public const int DefaultHarvestYield = 2;"));
        AssertTrue("GardenState defines garden crop definitions", gardenState.Contains("private static readonly GardenCropDef[] CropDefinitions"));

        var expectedCrops = new Dictionary<string, int>
        {
            ["yarrow"] = 1,
            ["gorse"] = 2,
            ["thyme"] = 3,
            ["heather"] = 1,
            ["mint"] = 2,
            ["elder"] = 1,
            ["rosemary"] = 3,
            ["willow"] = 2,
            ["juniper"] = 3,
            ["comfrey"] = 1
        };

        foreach (var crop in expectedCrops)
        {
            AssertTrue($"{crop.Key} crop definition exists",
                gardenState.Contains($"CreateGardenCrop(\"{crop.Key}\", growthDays: {crop.Value})"));
            AssertTrue($"{crop.Key} authored item exists",
                ReadProjectFile("Data/items_data.tres").Contains($"\"id\": \"{crop.Key}\""));
        }

        AssertTrue("Starter seed inventory includes yarrow",
            gardenState.Contains("(\"seed_yarrow\", 1)"));
        AssertTrue("Starter seed inventory includes gorse",
            gardenState.Contains("(\"seed_gorse\", 1)"));
        AssertTrue("Starter seed inventory includes thyme",
            gardenState.Contains("(\"seed_thyme\", 1)"));
    }

    private static void TestGardenStatePersistenceWiring()
    {
        var gameStateSource = ReadProjectFile("Scripts/Autoload/GameState.cs");
        var gardenState = ReadProjectFile("Scripts/Systems/GardenState.cs");
        var saveDataSource = ReadProjectFile("Scripts/Persistence/SaveData.cs");
        var saveManagerSource = ReadProjectFile("Scripts/Autoload/SaveGameManager.cs");
        var cropDefSource = ReadProjectFile("Scripts/Models/GardenCropDef.cs");
        var potStateSource = ReadProjectFile("Scripts/Models/GardenPotState.cs");

        AssertTrue("Save files use version two for garden state", saveDataSource.Contains("public int Version { get; set; } = 2;"));
        AssertTrue("Save manager accepts garden save version", saveManagerSource.Contains("private const int CurrentSaveVersion = 2;"));
        AssertTrue("Snapshot includes garden initialization marker", saveDataSource.Contains("public bool GardenInitialized { get; set; }"));
        AssertTrue("Snapshot includes garden pot count", saveDataSource.Contains("public int GardenPotCount { get; set; }"));
        AssertTrue("Snapshot includes seed inventory", saveDataSource.Contains("public Dictionary<string, int> SeedInventory"));
        AssertTrue("Snapshot includes garden pots", saveDataSource.Contains("public List<GardenPotState> GardenPots"));

        AssertTrue("GameState exposes seed inventory", gameStateSource.Contains("public IReadOnlyDictionary<string, int> SeedInventory"));
        AssertTrue("GameState exposes garden pots", gameStateSource.Contains("public IReadOnlyList<GardenPotState> GardenPots"));
        AssertTrue("GameState seeds starting garden pots", gameStateSource.Contains("_gardenState.InitializeNewGarden();") && gardenState.Contains("EnsurePotCount(StartingPotCount);"));
        AssertTrue("GameState seeds starter seed inventory", gardenState.Contains("SeedStartingSeedInventory();"));
        AssertTrue("GameState migrates old saves into a garden state", gameStateSource.Contains("_gardenState.Restore(snapshot.GardenInitialized") && gardenState.Contains("if (gardenInitialized)") && gardenState.Contains("InitializeNewGarden();"));
        AssertTrue("GameState snapshots garden state", gameStateSource.Contains("GardenInitialized = true") && gameStateSource.Contains("GardenPots = _gardenState.CloneGardenPots()"));
        AssertTrue("GameState advances garden growth on next day", gameStateSource.Contains("public void NextDay()") && gameStateSource.Contains("_gardenState.AdvanceGrowth();"));
        AssertTrue("GameState can plant seeds", gameStateSource.Contains("public bool TryPlantSeed(int potIndex, string seedId, out string error)"));
        AssertTrue("GameState can harvest garden pots", gameStateSource.Contains("public bool TryHarvestGardenPot(int potIndex, out string error)"));
        AssertTrue("Harvest adds ingredient and returns seed", gameStateSource.Contains("_inventoryState.AddRawStack(harvest.IngredientId, harvest.Quantity)") && gardenState.Contains("AddSeedStack(pot.SeedId, 1);"));
        AssertTrue("Garden pot upgrades are supported", gameStateSource.Contains("public void SetUnlockedGardenPotCount(int potCount)"));

        AssertTrue("Crop def stores yield range", cropDefSource.Contains("HarvestYieldMin") && cropDefSource.Contains("HarvestYieldMax"));
        AssertTrue("Pot state stores growth progress", potStateSource.Contains("DaysGrown") && potStateSource.Contains("RequiredGrowthDays"));
        AssertTrue("Pot state exposes ready status", potStateSource.Contains("public bool IsReady"));
    }

    private static void TestGardenSceneAndHudNavigation()
    {
        var hudSource = ReadProjectFile("Scripts/UI/Hud.cs");
        var hudScene = ReadProjectFile("Scenes/UI/Hud.tscn");
        var gardenSource = ReadProjectFile("Scripts/UI/Garden.cs");
        var gardenScene = ReadProjectFile("Scenes/Main/Garden.tscn");
        var scenePaths = ReadProjectFile("Scripts/Infrastructure/ScenePaths.cs");

        AssertTrue("Hud points to the garden scene", hudSource.Contains("ScenePaths.Garden") && scenePaths.Contains("res://Scenes/Main/Garden.tscn"));
        AssertTrue("Hud has a garden button field", hudSource.Contains("private Button _gardenButton"));
        AssertTrue("Hud resolves the garden button", hudSource.Contains("GetNode<Button>(GardenButtonPath)"));
        AssertTrue("Hud disables garden while shop is open", hudSource.Contains("_gardenButton.Disabled = navigationBlocked || isShopOpen;"));
        AssertTrue("Hud autosaves before entering garden", hudSource.Contains("TryAutoSave(\"entering the garden\")"));
        AssertTrue("Hud scene includes Garden button", hudScene.Contains("[node name=\"Garden\" type=\"Button\" parent=\"Content/Actions\"]"));
        AssertTrue("Garden button stays in the HUD menu", hudScene.Contains("text = \"Garden\""));

        AssertTrue("Garden script exists", gardenSource.Contains("public partial class Garden : Control"));
        AssertTrue("Garden script returns to main scene", gardenSource.Contains("ScenePaths.Main") && scenePaths.Contains("res://Main.tscn"));
        AssertTrue("Garden autosaves on entry", gardenSource.Contains("TryAutoSave(\"entering the garden\")"));
        AssertTrue("Garden autosaves after planting", gardenSource.Contains("TryAutoSave(\"planting a seed\")"));
        AssertTrue("Garden autosaves after harvesting", gardenSource.Contains("TryAutoSave(\"harvesting a crop\")"));
        AssertTrue("Garden autosaves before leaving", gardenSource.Contains("TryAutoSave(\"leaving the garden\")"));
        AssertTrue("Garden scene uses the garden script", gardenScene.Contains("path=\"res://Scripts/UI/Garden.cs\""));
        AssertTrue("Garden scene wires pots container", gardenScene.Contains("PotsContainerPath = NodePath(\"Root/Margin/Main/Content/PotsColumn/Pots\")"));
        AssertTrue("Garden scene wires seeds container", gardenScene.Contains("SeedsContainerPath = NodePath(\"Root/Margin/Main/Content/SeedsColumn/Seeds\")"));
        AssertTrue("Garden scene wires back button", gardenScene.Contains("BackButtonPath = NodePath(\"Root/Margin/Main/Header/Back\")"));
    }
}
