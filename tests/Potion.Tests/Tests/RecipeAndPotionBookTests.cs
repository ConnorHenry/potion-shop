using static ProjectFileTestHelper;
using static TestAssert;

internal static class RecipeAndPotionBookTests
{
    public static void Register(TestRunner runner)
    {
        runner.Run("PotionBookPanel appends learned runtime potions to the end", TestPotionBookPanelAppendsLearnedRuntimePotionsToTheEnd);
        runner.Run("Potion book views skip tainted learned potions", TestPotionBookViewsSkipTaintedLearnedPotions);
        runner.Run("IngredientBookPanel shows known entries before unknown pages", TestIngredientBookPanelShowsKnownEntriesBeforeUnknownPages);
    }

    private static void TestPotionBookPanelAppendsLearnedRuntimePotionsToTheEnd()
    {
        var source = ReadProjectFile("Scripts/UI/PotionBookPanel.cs");
        var potionBookScene = ReadProjectFile("Scenes/UI/PotionBookPanel.tscn");
        var gameStateSource = ReadProjectFile("Scripts/Autoload/GameState.cs");
        var potionKnowledgeState = ReadProjectFile("Scripts/Systems/PotionKnowledgeState.cs");
        var saveDataSource = ReadProjectFile("Scripts/Persistence/SaveData.cs");

        AssertTrue("PotionBookPanel resolves GameState through an exported path",
            source.Contains("GameStatePath = new(\"/root/GameState\")"));
        AssertTrue("PotionBookPanel subscribes to GameState changes",
            source.Contains("_gameState.Changed += OnGameStateChanged"));
        AssertTrue("PotionBookPanel reads learned potion order from GameState",
            source.Contains("foreach (var potionId in _gameState.KnownPotionOrder)"));
        AssertTrue("PotionBookPanel skips authored potion item ids when appending learned entries",
            source.Contains("if (authoredPotionIds.Contains(potionId))"));
        AssertTrue("PotionBookPanel registers both recipe ids and potion item ids as authored",
            source.Contains("authoredPotionIds.Add(BuildPredefinedPotionItemId(recipe.Id));"));
        AssertTrue("PotionBookPanel exports left and right brew button paths",
            source.Contains("LeftBrewButtonPath") &&
            source.Contains("RightBrewButtonPath"));
        AssertTrue("PotionBookPanel exports left and right page number paths",
            source.Contains("LeftPageNumberLabelPath") &&
            source.Contains("RightPageNumberLabelPath"));
        AssertTrue("PotionBookPanel exports left and right contents paths",
            source.Contains("LeftContentsPath") &&
            source.Contains("RightContentsPath"));
        AssertTrue("PotionBookPanel wires both spread brew buttons",
            source.Contains("_leftPage.BrewButton.Pressed += OnLeftBrewPressed") &&
            source.Contains("_rightPage.BrewButton.Pressed += OnRightBrewPressed"));
        AssertTrue("PotionBookPanel exports invisible page turn hotspot paths",
            source.Contains("LeftPageHotspotPath") &&
            source.Contains("RightPageHotspotPath"));
        AssertTrue("PotionBookPanel wires page turn hotspots",
            source.Contains("_leftPageHotspot.Pressed += OnPreviousPagePressed") &&
            source.Contains("_rightPageHotspot.Pressed += OnNextPagePressed"));
        AssertTrue("PotionBookPanel only enables brewing for known potion item ids",
            source.Contains("_gameState.KnowsPotion(candidatePotionItemId)"));
        AssertTrue("PotionBookPanel uses the shared inventory brew service",
            source.Contains("PotionInventoryBrewService"));
        AssertTrue("PotionBookPanel turns book spreads two pages at a time",
            source.Contains("private const int PagesPerSpread = 2") &&
            source.Contains("_currentPageIndex + PagesPerSpread"));
        AssertTrue("PotionBookPanel builds clickable contents pages from authored recipes only",
            source.Contains("RebuildContentsEntries") &&
            source.Contains("if (!entry.IsAuthored)") &&
            source.Contains("button.Pressed += () => OpenPage(targetPageIndex)"));
        AssertTrue("PotionBookPanel hides unknown authored potions in contents and page view",
            source.Contains("UnknownContentsLabel = \"???????\"") &&
            source.Contains("IsKnownAuthoredPotion") &&
            source.Contains("ShowUnknownRecipePage"));
        AssertTrue("PotionBookPanel scene defines side-by-side pages",
            potionBookScene.Contains("[node name=\"LeftPage\" type=\"VBoxContainer\" parent=\"BookRow/BookPanel/Margin/VBox/Pages\"]") &&
            potionBookScene.Contains("[node name=\"CenterFold\" type=\"ColorRect\" parent=\"BookRow/BookPanel/Margin/VBox/Pages\"]") &&
            potionBookScene.Contains("[node name=\"RightPage\" type=\"VBoxContainer\" parent=\"BookRow/BookPanel/Margin/VBox/Pages\"]"));
        AssertTrue("PotionBookPanel scene defines both brew button paths",
            potionBookScene.Contains("LeftBrewButtonPath = NodePath(\"BookRow/BookPanel/Margin/VBox/Pages/LeftPage/RecipeContent/Brew\")") &&
            potionBookScene.Contains("RightBrewButtonPath = NodePath(\"BookRow/BookPanel/Margin/VBox/Pages/RightPage/RecipeContent/Brew\")"));
        AssertTrue("PotionBookPanel scene defines page number labels on both pages",
            potionBookScene.Contains("LeftPageNumberLabelPath = NodePath(\"BookRow/BookPanel/Margin/VBox/Pages/LeftPage/PageNumber\")") &&
            potionBookScene.Contains("RightPageNumberLabelPath = NodePath(\"BookRow/BookPanel/Margin/VBox/Pages/RightPage/PageNumber\")") &&
            potionBookScene.Contains("[node name=\"PageNumber\" type=\"Label\" parent=\"BookRow/BookPanel/Margin/VBox/Pages/LeftPage\"]") &&
            potionBookScene.Contains("[node name=\"PageNumber\" type=\"Label\" parent=\"BookRow/BookPanel/Margin/VBox/Pages/RightPage\"]"));
        AssertTrue("PotionBookPanel scene defines contents containers on both pages",
            potionBookScene.Contains("LeftContentsPath = NodePath(\"BookRow/BookPanel/Margin/VBox/Pages/LeftPage/Contents\")") &&
            potionBookScene.Contains("RightContentsPath = NodePath(\"BookRow/BookPanel/Margin/VBox/Pages/RightPage/Contents\")") &&
            potionBookScene.Contains("[node name=\"Contents\" type=\"VBoxContainer\" parent=\"BookRow/BookPanel/Margin/VBox/Pages/LeftPage\"]") &&
            potionBookScene.Contains("[node name=\"Contents\" type=\"VBoxContainer\" parent=\"BookRow/BookPanel/Margin/VBox/Pages/RightPage\"]"));
        AssertTrue("PotionBookPanel scene uses invisible side hotspots instead of visible arrow buttons",
            potionBookScene.Contains("[node name=\"LeftPageHotspot\" type=\"Button\" parent=\"BookRow/BookPanel\"]") &&
            potionBookScene.Contains("[node name=\"RightPageHotspot\" type=\"Button\" parent=\"BookRow/BookPanel\"]") &&
            potionBookScene.Contains("modulate = Color(1, 1, 1, 0)") &&
            !potionBookScene.Contains("[node name=\"LeftArrow\"") &&
            !potionBookScene.Contains("[node name=\"RightArrow\""));
        AssertTrue("PotionBookPanel uses hovered GUI controls for wheel paging",
            source.Contains("GuiGetHoveredControl()"));
        AssertTrue("PotionBookPanel does not handle whole-panel drag input",
            !source.Contains("HandleWholePanelDragInput"));
        AssertTrue("PotionBookPanel keeps the anchored book position instead of drag state",
            !source.Contains("_dragOffset") &&
            !source.Contains("Position = mouseMotion.GlobalPosition"));
        AssertTrue("GameState tracks learned potion order",
            gameStateSource.Contains("public List<string> KnownPotionOrder { get; } = new();"));
        AssertTrue("GameState appends newly learned potions to the order list",
            potionKnowledgeState.Contains("_knownPotionOrder.Add(potionId)"));
        AssertTrue("Save data persists learned potion order",
            saveDataSource.Contains("KnownPotionOrder"));
    }

    private static void TestPotionBookViewsSkipTaintedLearnedPotions()
    {
        var potionBookSource = ReadProjectFile("Scripts/UI/PotionBookPanel.cs");

        AssertTrue("PotionBookPanel skips learned potion pages with active risks",
            potionBookSource.Contains("if (HasActiveRisk(potion))") &&
            potionBookSource.Contains("private static bool HasActiveRisk(ItemDef item)"));
        AssertTrue("Potion book active risk checks only positive risk values",
            potionBookSource.Contains("risk.Value > 0"));
    }

    private static void TestIngredientBookPanelShowsKnownEntriesBeforeUnknownPages()
    {
        var source = ReadProjectFile("Scripts/UI/IngredientBookPanel.cs");
        var scene = ReadProjectFile("Scenes/UI/IngredientBookPanel.tscn");
        var gameUiScene = ReadProjectFile("Scenes/UI/GameUi.tscn");
        var stationBookController = ReadProjectFile("Scripts/UI/StationBookController.cs");

        AssertTrue("IngredientBookPanel is a Control with explicit book page exports",
            source.Contains("public partial class IngredientBookPanel : Control") &&
            source.Contains("LeftPageHotspotPath") &&
            source.Contains("RightPageHotspotPath"));
        AssertTrue("IngredientBookPanel reads authored base ingredient pages from DataDb",
            source.Contains("_dataDb.Items.Values") &&
            source.Contains("IsBaseAuthoredIngredient"));
        AssertTrue("IngredientBookPanel uses GameState known ingredient state",
            source.Contains("_gameState.KnowsIngredient(item.Id)") &&
            source.Contains("_gameState.Changed += OnGameStateChanged"));
        AssertTrue("IngredientBookPanel appends unknown ingredient pages after known entries",
            source.Contains("foreach (var item in knownIngredients)") &&
            source.Contains("foreach (var item in unknownIngredients)") &&
            source.Contains("new IngredientBookEntry(item, false)"));
        AssertTrue("IngredientBookPanel hides unknown ingredient details behind placeholder text",
            source.Contains("ShowUnknownIngredientPage") &&
            source.Contains("Unknown Ingredient") &&
            source.Contains("page.UnknownIcon.Visible = true") &&
            source.Contains("This ingredient has not been discovered yet."));
        AssertTrue("IngredientBookPanel masks locked preparation stats and reveals known prep rows",
            source.Contains("FormatKnownPreparationTraitRows") &&
            source.Contains("FormatKnownPreparationRiskRows") &&
            source.Contains("_gameState.KnowsIngredientPreparation(item.Id, preparationId)") &&
            source.Contains("_gameState.KnowsAnyIngredientPreparation(item.Id)"));
        AssertTrue("IngredientBookPanel formats per-page numbers as current over total",
            source.Contains("LeftPageNumberLabelPath") &&
            source.Contains("RightPageNumberLabelPath") &&
            source.Contains("page.PageNumberLabel.Text = $\"{logicalPageIndex + 1} / {TotalPages}\";"));
        AssertTrue("IngredientBookPanel builds clickable contents pages with hidden unknown names",
            source.Contains("RebuildContentsEntries") &&
            source.Contains("UnknownContentsLabel = \"???????\"") &&
            source.Contains("button.Pressed += () => OpenPage(targetPageIndex)"));
        AssertTrue("IngredientBookPanel does not handle whole-panel drag input",
            !source.Contains("HandleWholePanelDragInput"));
        AssertTrue("IngredientBookPanel keeps the anchored book position instead of drag state",
            !source.Contains("_dragOffset") &&
            !source.Contains("Position = mouseMotion.GlobalPosition"));
        AssertTrue("IngredientBookPanel scene defines side-by-side book pages and unknown icon placeholders",
            scene.Contains("[node name=\"LeftPage\" type=\"VBoxContainer\" parent=\"BookRow/BookPanel/Margin/VBox/Pages\"]") &&
            scene.Contains("[node name=\"CenterFold\" type=\"ColorRect\" parent=\"BookRow/BookPanel/Margin/VBox/Pages\"]") &&
            scene.Contains("[node name=\"RightPage\" type=\"VBoxContainer\" parent=\"BookRow/BookPanel/Margin/VBox/Pages\"]") &&
            scene.Contains("[node name=\"UnknownIcon\" type=\"Label\" parent=\"BookRow/BookPanel/Margin/VBox/Pages/LeftPage/IngredientContent/IconFrame\"]") &&
            scene.Contains("[node name=\"UnknownIcon\" type=\"Label\" parent=\"BookRow/BookPanel/Margin/VBox/Pages/RightPage/IngredientContent/IconFrame\"]"));
        AssertTrue("IngredientBookPanel scene defines page number labels on both pages",
            scene.Contains("LeftPageNumberLabelPath = NodePath(\"BookRow/BookPanel/Margin/VBox/Pages/LeftPage/PageNumber\")") &&
            scene.Contains("RightPageNumberLabelPath = NodePath(\"BookRow/BookPanel/Margin/VBox/Pages/RightPage/PageNumber\")") &&
            scene.Contains("[node name=\"PageNumber\" type=\"Label\" parent=\"BookRow/BookPanel/Margin/VBox/Pages/LeftPage\"]") &&
            scene.Contains("[node name=\"PageNumber\" type=\"Label\" parent=\"BookRow/BookPanel/Margin/VBox/Pages/RightPage\"]"));
        AssertTrue("IngredientBookPanel scene defines contents containers on both pages",
            scene.Contains("LeftContentsPath = NodePath(\"BookRow/BookPanel/Margin/VBox/Pages/LeftPage/Contents\")") &&
            scene.Contains("RightContentsPath = NodePath(\"BookRow/BookPanel/Margin/VBox/Pages/RightPage/Contents\")") &&
            scene.Contains("[node name=\"Contents\" type=\"VBoxContainer\" parent=\"BookRow/BookPanel/Margin/VBox/Pages/LeftPage\"]") &&
            scene.Contains("[node name=\"Contents\" type=\"VBoxContainer\" parent=\"BookRow/BookPanel/Margin/VBox/Pages/RightPage\"]"));
        AssertTrue("GameUi instances the ingredient book panel and dynamic book switch",
            gameUiScene.Contains("path=\"res://Scenes/UI/IngredientBookPanel.tscn\"") &&
            gameUiScene.Contains("[node name=\"IngredientBookPanel\" parent=\".\" instance=ExtResource(\"41_ingredient_book\")]") &&
            gameUiScene.Contains("[node name=\"BookSwitch\" type=\"Button\" parent=\"PotionBrewingStationView/Book\"]") &&
            !gameUiScene.Contains("[node name=\"IngredientBookTab\"") &&
            !gameUiScene.Contains("[node name=\"PotionBookTab\""));
        AssertTrue("GameUi uses one brewing-station book object and one clickable book hotspot",
            gameUiScene.Contains("[node name=\"Book\" type=\"TextureRect\" parent=\"PotionBrewingStationView\"]") &&
            gameUiScene.Contains("[node name=\"BookHotspot\" type=\"Button\" parent=\"PotionBrewingStationView/Book\"]") &&
            !gameUiScene.Contains("ShopFloor") &&
            !gameUiScene.Contains("PotionBookCloseupView"));
        AssertTrue("GameUi does not override book page number paths to null",
            !gameUiScene.Contains("PageNumberLabelPath = null"));
        AssertTrue("StationBookController wires the station book hotspot to both book panels",
            stationBookController.Contains("BookButtonPath = new(\"Book/BookHotspot\")") &&
            stationBookController.Contains("PotionBookPanelPath = new(\"../PotionBookPanel\")") &&
            stationBookController.Contains("IngredientBookPanelPath = new(\"../IngredientBookPanel\")") &&
            stationBookController.Contains("ShowBookPanel(_activeBookPanelKind)") &&
            stationBookController.Contains("bookPanelKind == BookPanelKind.Potion") &&
            stationBookController.Contains("_potionBookPanel.ShowPanel()") &&
            stationBookController.Contains("_ingredientBookPanel.ShowPanel()"));
        AssertTrue("StationBookController uses one book switch button that targets the opposite book",
            stationBookController.Contains("BookSwitchButtonPath = new(\"Book/BookSwitch\")") &&
            stationBookController.Contains("OnBookSwitchPressed") &&
            stationBookController.Contains("ShowBookPanel(GetOppositeBookPanelKind(_activeBookPanelKind))") &&
            stationBookController.Contains("_bookSwitchButton.Text = GetBookSwitchButtonText(targetBookPanelKind)") &&
            stationBookController.Contains("return targetBookPanelKind == BookPanelKind.Potion ? \"Potions\" : \"Ingredients\";"));
        AssertTrue("StationBookController remembers the active book between openings",
            stationBookController.Contains("private BookPanelKind _activeBookPanelKind = BookPanelKind.Potion;") &&
            stationBookController.Contains("_activeBookPanelKind = bookPanelKind;"));
    }
}
