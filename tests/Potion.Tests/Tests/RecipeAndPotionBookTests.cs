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
        var potionBookTheme = ReadProjectFile("Assets/UI/PotionBookTheme.tres");
        var gameUiScene = ReadProjectFile("Scenes/UI/GameUi.tscn");
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
        AssertTrue("PotionBookPanel is instanced on the book overlay layer above active brewing station UI",
            potionBookScene.Contains("[node name=\"PotionBookPanel\" type=\"Control\"]") &&
            gameUiScene.Contains("[node name=\"BookOverlayLayer\" type=\"CanvasLayer\" parent=\".\"]") &&
            gameUiScene.Contains("layer = 4096") &&
            gameUiScene.Contains("[node name=\"PotionBookPanel\" parent=\"BookOverlayLayer\" instance=ExtResource(\"18_potion_book\")]"));
        AssertTrue("Potion book theme defines generated open-book StyleBoxFlat resources",
            potionBookTheme.Contains("OpenBookCover/base_type = &\"PanelContainer\"") &&
            potionBookTheme.Contains("OpenBookCover/styles/panel = SubResource(\"StyleBoxFlat_book_cover\")") &&
            potionBookTheme.Contains("OpenBookLeftPage/base_type = &\"PanelContainer\"") &&
            potionBookTheme.Contains("OpenBookLeftPage/styles/panel = SubResource(\"StyleBoxFlat_book_left_page\")") &&
            potionBookTheme.Contains("OpenBookRightPage/base_type = &\"PanelContainer\"") &&
            potionBookTheme.Contains("OpenBookRightPage/styles/panel = SubResource(\"StyleBoxFlat_book_right_page\")") &&
            potionBookTheme.Contains("bg_color = Color(0.86, 0.77, 0.51, 0.98)") &&
            potionBookTheme.Contains("bg_color = Color(0.88, 0.79, 0.53, 0.98)"));
        AssertTrue("PotionBookPanel scene draws the generated open-book background behind page writing",
            potionBookScene.Contains("[node name=\"BookSurface\" type=\"Control\" parent=\"BookRow/BookPanel\"]") &&
            potionBookScene.Contains("[node name=\"LeftPageBackground\" type=\"PanelContainer\" parent=\"BookRow/BookPanel/BookSurface\"]") &&
            potionBookScene.Contains("[node name=\"RightPageBackground\" type=\"PanelContainer\" parent=\"BookRow/BookPanel/BookSurface\"]") &&
            potionBookScene.Contains("theme_type_variation = &\"OpenBookLeftPage\"") &&
            potionBookScene.Contains("theme_type_variation = &\"OpenBookRightPage\"") &&
            potionBookScene.IndexOf("[node name=\"BookSurface\"") < potionBookScene.IndexOf("[node name=\"Margin\" type=\"MarginContainer\" parent=\"BookRow/BookPanel\"]"));
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
        var potionBookScene = ReadProjectFile("Scenes/UI/PotionBookPanel.tscn");
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
        AssertTrue("IngredientBookPanel is instanced on the book overlay layer above active brewing station UI",
            scene.Contains("[node name=\"IngredientBookPanel\" type=\"Control\"]") &&
            gameUiScene.Contains("[node name=\"BookOverlayLayer\" type=\"CanvasLayer\" parent=\".\"]") &&
            gameUiScene.Contains("layer = 4096") &&
            gameUiScene.Contains("[node name=\"IngredientBookPanel\" parent=\"BookOverlayLayer\" instance=ExtResource(\"41_ingredient_book\")]"));
        AssertTrue("IngredientBookPanel scene draws the generated open-book background behind page writing",
            scene.Contains("[node name=\"BookSurface\" type=\"Control\" parent=\"BookRow/BookPanel\"]") &&
            scene.Contains("[node name=\"LeftPageBackground\" type=\"PanelContainer\" parent=\"BookRow/BookPanel/BookSurface\"]") &&
            scene.Contains("[node name=\"RightPageBackground\" type=\"PanelContainer\" parent=\"BookRow/BookPanel/BookSurface\"]") &&
            scene.Contains("theme_type_variation = &\"OpenBookLeftPage\"") &&
            scene.Contains("theme_type_variation = &\"OpenBookRightPage\"") &&
            scene.IndexOf("[node name=\"BookSurface\"") < scene.IndexOf("[node name=\"Margin\" type=\"MarginContainer\" parent=\"BookRow/BookPanel\"]"));
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
        AssertTrue("Book panel scenes own the dynamic book switch buttons",
            gameUiScene.Contains("path=\"res://Scenes/UI/IngredientBookPanel.tscn\"") &&
            gameUiScene.Contains("[node name=\"BookOverlayLayer\" type=\"CanvasLayer\" parent=\".\"]") &&
            gameUiScene.Contains("layer = 4096") &&
            gameUiScene.Contains("[node name=\"BookDismissOverlay\" type=\"Control\" parent=\"BookOverlayLayer\"]") &&
            gameUiScene.Contains("[node name=\"PotionBookPanel\" parent=\"BookOverlayLayer\" instance=ExtResource(\"18_potion_book\")]") &&
            gameUiScene.Contains("[node name=\"IngredientBookPanel\" parent=\"BookOverlayLayer\" instance=ExtResource(\"41_ingredient_book\")]") &&
            !gameUiScene.Contains("[node name=\"PotionBookPanel\" parent=\".\" instance=ExtResource(\"18_potion_book\")]\ntop_level = true") &&
            !gameUiScene.Contains("[node name=\"PotionBookPanel\" parent=\".\" instance=ExtResource(\"18_potion_book\")]\nz_index = 0") &&
            potionBookScene.Contains("[node name=\"BookSwitch\" type=\"Button\" parent=\"BookRow/BookPanel\"]") &&
            potionBookScene.Contains("text = \"Ingredients\"") &&
            scene.Contains("[node name=\"BookSwitch\" type=\"Button\" parent=\"BookRow/BookPanel\"]") &&
            scene.Contains("text = \"Potions\"") &&
            !gameUiScene.Contains("[node name=\"BookSwitch\" type=\"Button\" parent=\"PotionBrewingStationView/Book\"]") &&
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
            stationBookController.Contains("PotionBookPanelPath = new(\"../BookOverlayLayer/PotionBookPanel\")") &&
            stationBookController.Contains("IngredientBookPanelPath = new(\"../BookOverlayLayer/IngredientBookPanel\")") &&
            stationBookController.Contains("BookDismissOverlayPath = new(\"../BookOverlayLayer/BookDismissOverlay\")") &&
            stationBookController.Contains("ShowBookPanel(_activeBookPanelKind)") &&
            stationBookController.Contains("bookPanelKind == BookPanelKind.Potion") &&
            stationBookController.Contains("_potionBookPanel.ShowPanel()") &&
            stationBookController.Contains("_ingredientBookPanel.ShowPanel()"));
        AssertTrue("StationBookController closes active book panels from outside clicks",
            stationBookController.Contains("_bookDismissOverlay.GuiInput += _bookDismissOverlayGuiInputHandler") &&
            stationBookController.Contains("OnBookDismissOverlayGuiInput") &&
            stationBookController.Contains("HideBookPanels();") &&
            stationBookController.Contains("_bookDismissOverlay?.AcceptEvent();") &&
            stationBookController.Contains("SetBookDismissOverlayVisible(false)") &&
            stationBookController.Contains("MouseFilterEnum.Stop") &&
            stationBookController.Contains("MouseFilterEnum.Ignore"));
        AssertTrue("StationBookController wires switch buttons owned by the open book panels",
            stationBookController.Contains("PotionBookSwitchButtonPath = new(\"../BookOverlayLayer/PotionBookPanel/BookRow/BookPanel/BookSwitch\")") &&
            stationBookController.Contains("IngredientBookSwitchButtonPath = new(\"../BookOverlayLayer/IngredientBookPanel/BookRow/BookPanel/BookSwitch\")") &&
            stationBookController.Contains("OnPotionBookSwitchPressed") &&
            stationBookController.Contains("ShowBookPanel(BookPanelKind.Ingredient)") &&
            stationBookController.Contains("OnIngredientBookSwitchPressed") &&
            stationBookController.Contains("ShowBookPanel(BookPanelKind.Potion)") &&
            !stationBookController.Contains("BookSwitchButtonPath = new(\"Book/BookSwitch\")"));
        AssertTrue("StationBookController remembers the active book between openings",
            stationBookController.Contains("private BookPanelKind _activeBookPanelKind = BookPanelKind.Potion;") &&
            stationBookController.Contains("_activeBookPanelKind = bookPanelKind;"));
    }
}
