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

internal static class InventoryAndBrewPanelTests
{
    public static void Register(TestRunner runner)
    {
        runner.Run("Draggable panel whole-panel drag respects child action buttons", TestDraggablePanelWholePanelDragRespectsChildButtons);
        runner.Run("InventoryPanel splits inventory labels predictably", TestInventoryPanelSplitInventoryName);
        runner.Run("InventoryPanel dictionary formatting is stable", TestInventoryPanelFormatDictionary);
        runner.Run("InventoryPanel top-traits formatting is stable", TestInventoryPanelFormatTopTraits);
        runner.Run("InventoryPanel potion filter uses only top traits", TestInventoryPanelPotionFilterUsesOnlyTopTraits);
        runner.Run("InventoryPanel closes detail after successful right-click queue of same ingredient", TestInventoryPanelRightClickQueueClosesMatchingDetail);
        runner.Run("InventoryPanel risk filter is wired", TestInventoryPanelRiskFilterIsWired);
        runner.Run("InventoryPanel colors risky potion names red", TestInventoryPanelRiskyPotionNamesAreRed);
        runner.Run("InventoryPanel clear buttons reserve layout space until filters are active", TestInventoryPanelClearButtonsReserveLayoutSpaceUntilFiltersAreActive);
        runner.Run("InventoryPanel ingredient type filter is populated and fixed", TestInventoryPanelTypeFilterIsPopulatedAndFixed);
        runner.Run("BrewPanel ingredient tag detection is case-insensitive", TestBrewPanelIsIngredient);
        runner.Run("BrewPanel previews potion names before brewing", TestBrewPanelPreviewNameIsWired);
        runner.Run("Potion inventory is capped at four unique potions with ten per stack", TestPotionInventoryCap);
        runner.Run("Consumable inventory and treatment tray are wired", TestConsumableInventoryAndTreatmentTrayWiring);
        runner.Run("Treatment service creates expected treatment outputs", TestTreatmentServiceCreatesExpectedOutputs);
        runner.Run("Brew and inventory price wiring stays intact", TestBrewAndInventoryPriceWiring);
        runner.Run("BrewPanel splits risk variants for known potion combinations", TestBrewPanelSplitsRiskVariantsForKnownCombinations);
        runner.Run("BrewPanel risk variant ids are deterministic", TestBrewPanelRiskVariantIdsAreDeterministic);
        runner.Run("Potion detail manually adds clean potions to the potion book", TestPotionDetailManuallyAddsCleanPotionsToBook);
        runner.Run("Repeat brew failures show cursor toast instead of console error", TestRepeatBrewFailuresShowCursorToast);
    }

    private static void TestDraggablePanelWholePanelDragRespectsChildButtons()
    {
        var draggablePanel = ReadProjectFile("Scripts/UI/DraggablePanel.cs");
        var inventoryPanel = ReadProjectFile("Scripts/UI/InventoryPanel.cs");

        AssertTrue("DraggablePanel inspects hovered GUI control before drag",
            draggablePanel.Contains("GuiGetHoveredControl()"));
        AssertTrue("DraggablePanel prevents whole-panel drag when a child button is hovered",
            draggablePanel.Contains("hoveredControl is BaseButton"));
        AssertTrue("DraggablePanel only applies button guard to its own children",
            draggablePanel.Contains("IsAncestorOf(hoveredControl)"));
        AssertTrue("DraggablePanel moves nested panels in global coordinates",
            draggablePanel.Contains("mouseButton.GlobalPosition - GlobalPosition") &&
            draggablePanel.Contains("GlobalPosition = mouseMotion.GlobalPosition - _dragOffset") &&
            draggablePanel.Contains("GetGlobalMousePosition() - GlobalPosition") &&
            draggablePanel.Contains("GlobalPosition = GetGlobalMousePosition() - _dragOffset"));
        AssertTrue("InventoryPanel close button remains wired to hide detail",
            inventoryPanel.Contains("_itemDetailCloseButton.Pressed += HideItemDetail;"));
        AssertTrue("InventoryPanel add-to-brew button remains wired",
            inventoryPanel.Contains("_itemDetailBrewButton.Pressed += TryUseSelectedItem;"));
        AssertTrue("InventoryPanel discard button remains wired",
            inventoryPanel.Contains("_itemDetailDiscardButton.Pressed += TryDiscardSelectedPotion;"));
        AssertTrue("InventoryPanel potion detail brew button uses short copy",
            inventoryPanel.Contains("_itemDetailBrewButton.Text = \"Brew\";"));
        AssertTrue("InventoryPanel potion detail discard consumes one potion",
            inventoryPanel.Contains("_gameState.ConsumeItem(itemId, 1)"));
    }

    private static void TestInventoryPanelSplitInventoryName()
    {
        var type = GetTypeFromUiAssembly("OccultShop.UI.InventoryPanel");
        var method = type.GetMethod("SplitInventoryName", BindingFlags.NonPublic | BindingFlags.Static);
        AssertTrue("SplitInventoryName method exists", method is not null);
        if (method is null)
            return;

        var splitArgs = new object?[] { "Moon Dust", null, null };
        method.Invoke(null, splitArgs);
        AssertEqual("Split first line", "Moon", splitArgs[1] as string ?? string.Empty);
        AssertEqual("Split second line", "Dust", splitArgs[2] as string ?? string.Empty);

        var singleWordArgs = new object?[] { "Elixir", null, null };
        method.Invoke(null, singleWordArgs);
        AssertEqual("Single word first line", "Elixir", singleWordArgs[1] as string ?? string.Empty);
        AssertEqual("Single word second line", string.Empty, singleWordArgs[2] as string ?? string.Empty);

        var emptyArgs = new object?[] { string.Empty, null, null };
        method.Invoke(null, emptyArgs);
        AssertEqual("Empty first line", string.Empty, emptyArgs[1] as string ?? string.Empty);
        AssertEqual("Empty second line", string.Empty, emptyArgs[2] as string ?? string.Empty);
    }

    private static void TestInventoryPanelFormatDictionary()
    {
        var values = new Dictionary<string, int>
        {
            ["zeta"] = 2,
            ["beta"] = 4,
            ["alpha"] = 4
        };

        var formatted = InvokePrivateStatic<string>("OccultShop.UI.InventoryPanel", "FormatTopStats", values, 3, "None");
        AssertEqual("Inventory dictionary order", "Alpha +4\nBeta +4\nZeta +2", formatted);

        var empty = InvokePrivateStatic<string>("OccultShop.UI.InventoryPanel", "FormatTopStats", new Dictionary<string, int>(), 3, "None");
        AssertEqual("Inventory dictionary empty", "None\n\n", empty);

        var nullValue = InvokePrivateStatic<string>("OccultShop.UI.InventoryPanel", "FormatTopStats", (object?)null, 3, "None");
        AssertEqual("Inventory dictionary null", "None\n\n", nullValue);
    }

    private static void TestInventoryPanelFormatTopTraits()
    {
        var values = new Dictionary<string, int>
        {
            ["chaos"] = 1,
            ["sleep"] = 5,
            ["focus"] = 5,
            ["calm"] = 2
        };

        var formatted = InvokePrivateStatic<string>("OccultShop.UI.InventoryPanel", "FormatTopStats", values, 2, "None");
        AssertEqual("Inventory top traits order", "Focus +5\nSleep +5", formatted);

        var empty = InvokePrivateStatic<string>("OccultShop.UI.InventoryPanel", "FormatTopStats", new Dictionary<string, int>(), 3, "None");
        AssertEqual("Inventory top traits empty", "None\n\n", empty);
    }

    private static void TestInventoryPanelPotionFilterUsesOnlyTopTraits()
    {
        var inventoryPanel = ReadProjectFile("Scripts/UI/InventoryPanel.cs");

        AssertTrue("InventoryPanel builds potion trait names from the top three traits only",
            inventoryPanel.Contains("ItemFilterUtilities.BuildTopTraitNames(potionStacks.Select(x => x.Key), 3, _itemCatalog)"));
        AssertTrue("InventoryPanel keeps ingredient trait names unchanged",
            inventoryPanel.Contains("ItemFilterUtilities.BuildTraitNames(ingredientStacks.Select(x => x.Key), _itemCatalog)"));
        AssertTrue("InventoryPanel top-trait helper limits the selected traits",
            ReadProjectFile("Scripts/UI/ItemFilterUtilities.cs").Contains(".Take(maxCount)"));
    }

    private static void TestInventoryPanelRightClickQueueClosesMatchingDetail()
    {
        var source = ReadProjectFile("Scripts/UI/InventoryPanel.cs");
        var brewPanel = ReadProjectFile("Scripts/UI/BrewPanel.cs");

        AssertTrue("InventoryPanel records quantity before right-click queue attempt",
            source.Contains("var quantityBeforeQueue = _gameState.Inventory.GetValueOrDefault(itemId);"));
        AssertTrue("InventoryPanel records quantity after right-click queue attempt",
            source.Contains("var quantityAfterQueue = _gameState.Inventory.GetValueOrDefault(itemId);"));
        AssertTrue("InventoryPanel only treats queue as success when inventory decreases",
            source.Contains("var queuedSuccessfully = quantityAfterQueue < quantityBeforeQueue;"));
        AssertTrue("InventoryPanel opens the brew panel before queueing a right-click ingredient",
            source.Contains("_brewPanel.ShowPanel();"));
        AssertTrue("BrewPanel exposes an explicit show method for ingredient adds",
            brewPanel.Contains("public void ShowPanel()"));
        AssertTrue("InventoryPanel only closes detail when same item is currently selected",
            source.Contains("string.Equals(_currentItemId, itemId, System.StringComparison.OrdinalIgnoreCase)"));
        AssertTrue("InventoryPanel hides detail after successful matching queue",
            source.Contains("HideItemDetail();"));
    }

    private static void TestInventoryPanelRiskFilterIsWired()
    {
        var inventoryPanel = ReadProjectFile("Scripts/UI/InventoryPanel.cs");
        var scene = ReadProjectFile("Scenes/UI/InventoryPanel.tscn");

        AssertTrue("InventoryPanel exports a potion risk filter path",
            inventoryPanel.Contains("PotionsRiskFilterPath"));
        AssertTrue("InventoryPanel exports an ingredient type filter path",
            inventoryPanel.Contains("IngredientsTypeFilterPath"));
        AssertTrue("InventoryPanel exports an ingredient risk filter path",
            inventoryPanel.Contains("IngredientsRiskFilterPath"));
        AssertTrue("InventoryPanel keeps fixed ingredient type options",
            inventoryPanel.Contains("IngredientTypeFilterOptions"));
        AssertTrue("InventoryPanel filters ingredients by selected type",
            inventoryPanel.Contains("ItemHasIngredientType(stack.Key, _activeIngredientTypeFilter)"));
        AssertTrue("InventoryPanel builds risk names",
            inventoryPanel.Contains("ItemFilterUtilities.BuildRiskNames(potionStacks.Select(x => x.Key), _itemCatalog)"));
        AssertTrue("InventoryPanel checks potion risks",
            inventoryPanel.Contains("ItemFilterUtilities.ItemHasRisk(stack.Key, _activePotionRiskFilter, _itemCatalog)"));
        AssertTrue("InventoryPanel checks ingredient risks",
            inventoryPanel.Contains("ItemFilterUtilities.ItemHasRisk(stack.Key, _activeIngredientRiskFilter, _itemCatalog)"));
        AssertTrue("InventoryPanel defines potion risk filter in the scene",
            scene.Contains("PotionsRiskFilterPath = NodePath(\"Panel/Margin/VBox/PotionsHeaderRow/RiskFilter\")"));
        AssertTrue("InventoryPanel type filter wiring is provided by scene path or fallback lookup",
            scene.Contains("IngredientsTypeFilterPath = NodePath(\"Panel/Margin/VBox/IngredientsHeaderRow/TypeFilter\")")
            || inventoryPanel.Contains("IngredientsTypeFilterPath.IsEmpty"));
        AssertTrue("InventoryPanel defines ingredient risk filter in the scene",
            scene.Contains("IngredientsRiskFilterPath = NodePath(\"Panel/Margin/VBox/IngredientsHeaderRow/RiskFilter\")"));
        AssertTrue("InventoryPanel scene places potion risk filter to the right of trait filter",
            scene.Contains("[node name=\"RiskFilter\" type=\"OptionButton\" parent=\"Panel/Margin/VBox/PotionsHeaderRow\"]"));
        AssertTrue("InventoryPanel scene places ingredient risk filter to the right of trait filter",
            scene.Contains("[node name=\"RiskFilter\" type=\"OptionButton\" parent=\"Panel/Margin/VBox/IngredientsHeaderRow\"]"));
        AssertTrue("InventoryPanel scene includes ingredient type filter",
            scene.Contains("[node name=\"TypeFilter\" type=\"OptionButton\" parent=\"Panel/Margin/VBox/IngredientsHeaderRow\"]"));
    }

    private static void TestInventoryPanelRiskyPotionNamesAreRed()
    {
        var inventoryPanel = ReadProjectFile("Scripts/UI/InventoryPanel.cs");

        AssertTrue("InventoryPanel checks potion risk state before coloring slot names",
            inventoryPanel.Contains("var shouldShowRiskNameColor = IsPotion(itemId) && HasActiveRisk(item);"));
        AssertTrue("InventoryPanel only treats positive risks as active",
            inventoryPanel.Contains("private static bool HasActiveRisk(ItemDef? item)") &&
            inventoryPanel.Contains("risk.Value > 0"));
        AssertTrue("InventoryPanel colors the first potion name line red",
            inventoryPanel.Contains("name.AddThemeColorOverride(\"font_color\", new Color(0.9f, 0.25f, 0.25f, 1f));"));
        AssertTrue("InventoryPanel colors the second potion name line red",
            inventoryPanel.Contains("secondName.AddThemeColorOverride(\"font_color\", new Color(0.9f, 0.25f, 0.25f, 1f));"));
    }

    private static void TestInventoryPanelClearButtonsReserveLayoutSpaceUntilFiltersAreActive()
    {
        var source = ReadProjectFile("Scripts/UI/InventoryPanel.cs");
        var scene = ReadProjectFile("Scenes/UI/InventoryPanel.tscn");
        var potionClearButtonReservesSpace =
            scene.Contains($"[node name=\"Clear\" type=\"Button\" parent=\"Panel/Margin/VBox/PotionsHeaderRow\"]{Environment.NewLine}visible = false{Environment.NewLine}custom_minimum_size = Vector2(64, 0)") ||
            scene.Contains("[node name=\"Clear\" type=\"Button\" parent=\"Panel/Margin/VBox/PotionsHeaderRow\"]\nvisible = false\ncustom_minimum_size = Vector2(64, 0)");
        var ingredientClearButtonReservesSpace =
            scene.Contains($"[node name=\"Clear\" type=\"Button\" parent=\"Panel/Margin/VBox/IngredientsHeaderRow\"]{Environment.NewLine}visible = false{Environment.NewLine}custom_minimum_size = Vector2(64, 0)") ||
            scene.Contains("[node name=\"Clear\" type=\"Button\" parent=\"Panel/Margin/VBox/IngredientsHeaderRow\"]\nvisible = false\ncustom_minimum_size = Vector2(64, 0)");

        AssertTrue("InventoryPanel keeps potion clear button layout stable from filter state",
            source.Contains("UpdateClearFilterButtonVisibility();") &&
            source.Contains("ApplyClearFilterButtonState(_potionsClearFilterButton, hasActivePotionFilter)") &&
            source.Contains("_activePotionTraitFilter") &&
            source.Contains("_activePotionRiskFilter"));
        AssertTrue("InventoryPanel keeps ingredient clear button layout stable from filter state",
            source.Contains("ApplyClearFilterButtonState(_ingredientsClearFilterButton, hasActiveIngredientFilter)") &&
            source.Contains("_activeIngredientTypeFilter") &&
            source.Contains("_activeIngredientTraitFilter") &&
            source.Contains("_activeIngredientRiskFilter"));
        AssertTrue("InventoryPanel reserves width for the potion clear button", potionClearButtonReservesSpace);
        AssertTrue("InventoryPanel reserves width for the ingredient clear button", ingredientClearButtonReservesSpace);
        AssertTrue("InventoryPanel inactive clear buttons stay in layout but non-interactive",
            source.Contains("button.Visible = true") &&
            source.Contains("button.Disabled = !isActive") &&
            source.Contains("button.MouseFilter = isActive ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore") &&
            source.Contains("button.Modulate = isActive ? Colors.White : new Color(1f, 1f, 1f, 0f)"));
    }

    private static void TestInventoryPanelTypeFilterIsPopulatedAndFixed()
    {
        var source = ReadProjectFile("Scripts/UI/InventoryPanel.cs");

        AssertTrue("InventoryPanel keeps a fixed type options list", source.Contains("IngredientTypeFilterOptions"));
        AssertTrue("InventoryPanel includes Herb option", source.Contains("\"Herb\""));
        AssertTrue("InventoryPanel includes Liquid option", source.Contains("\"Liquid\""));
        AssertTrue("InventoryPanel includes Catalyst option", source.Contains("\"Catalyst\""));
        AssertTrue("InventoryPanel refreshes ingredient type options explicitly",
            source.Contains("RefreshIngredientTypeFilterOptions();"));
        AssertTrue("InventoryPanel uses TypeFilter fallback lookup when exported path is empty",
            source.Contains("IngredientsTypeFilterPath.IsEmpty"));
        AssertTrue("InventoryPanel fallback path targets the ingredients type filter node",
            source.Contains("Panel/Margin/VBox/IngredientsHeaderRow/TypeFilter"));
        AssertTrue("InventoryPanel applies the selected type filter to ingredient stacks",
            source.Contains("ItemHasIngredientType(stack.Key, _activeIngredientTypeFilter)"));
    }

    private static void TestBrewPanelIsIngredient()
    {
        var itemDefType = GetTypeFromUiAssembly("OccultShop.Models.ItemDef");
        var ingredientItem = Activator.CreateInstance(itemDefType)
            ?? throw new InvalidOperationException("Failed to create ItemDef instance.");
        var nonIngredientItem = Activator.CreateInstance(itemDefType)
            ?? throw new InvalidOperationException("Failed to create ItemDef instance.");

        SetProperty(ingredientItem, "Tags", new List<string> { "ingredient", "rare" });
        SetProperty(nonIngredientItem, "Tags", new List<string> { "potion" });

        var ingredientResult = InvokePrivateStatic<bool>("OccultShop.UI.BrewPanel", "IsIngredient", ingredientItem);
        var nonIngredientResult = InvokePrivateStatic<bool>("OccultShop.UI.BrewPanel", "IsIngredient", nonIngredientItem);

        AssertTrue("Ingredient tag recognized", ingredientResult);
        AssertTrue("Non-ingredient rejected", !nonIngredientResult);
    }

    // TODO: Re-enable as a registered test if duplicate ingredient-type validation becomes a BrewPanel contract again.
    private static void StaleBrewPanelRejectsDuplicateQueuedIngredients()
    {
        var source = ReadProjectFile("Scripts/UI/BrewPanel.cs");

        AssertTrue("BrewPanel still prevents duplicate queue entries",
            source.Contains("Each ingredient can only be used once per potion."));
        AssertTrue("BrewPanel blocks duplicate ingredient types with a specific message",
            source.Contains("Cannot add duplicate type: {newIngredientType} (need one herb, one liquid, one catalyst)"));
        AssertTrue("BrewPanel requires one of each ingredient type before brewing",
            source.Contains("Brewing requires one herb, one liquid, and one catalyst."));
        AssertTrue("BrewPanel resolves item types from tags",
            source.Contains("TryGetIngredientType(ItemDef item, out string ingredientType)"));
        AssertTrue("BrewPanel queue remains list-based without stack counting",
            source.Contains("private readonly List<string> _queuedIngredients = new();"));
        AssertTrue("Inventory drag/drop still routes through TryQueueIngredient",
            ReadProjectFile("Scripts/UI/InventoryPanel.cs").Contains("_brewPanel.TryQueueIngredient(itemId);"));
        AssertTrue("Brew drop box still emits dragged item ids",
            ReadProjectFile("Scripts/UI/BrewDropBox.cs").Contains("EmitSignal(SignalName.ItemDropped, data.AsString());"));
    }

    private static void TestBrewPanelPreviewNameIsWired()
    {
        var source = ReadProjectFile("Scripts/UI/BrewPanel.cs");
        var scene = ReadProjectFile("Scenes/UI/GameUi.tscn");

        AssertTrue("BrewPanel exports a preview name label path",
            source.Contains("PotionNamePreviewLabelPath"));
        AssertTrue("BrewPanel caches the current preview combination",
            source.Contains("_previewPotionCombinationKey"));
        AssertTrue("BrewPanel caches the current preview name",
            source.Contains("_previewPotionName"));
        AssertTrue("BrewPanel resolves the preview name before brewing",
            source.Contains("var potionDisplayName = GetPreviewPotionName(combinationKey);"));
        AssertTrue("BrewPanel regenerates preview names from the combination key",
            source.Contains("GetPreviewPotionName(string combinationKey)"));
        AssertTrue("BrewPanel scene wires the live preview name label",
            scene.Contains("PotionNamePreviewLabelPath = NodePath(\"Panel/CurrentBrew/Name\")"));
        AssertTrue("BrewPanel scene uses the text-free potion preview board",
            scene.Contains("path=\"res://art/Potion-Preview-Board.png\"") &&
            scene.Contains("[node name=\"Board\" type=\"TextureRect\" parent=\"PotionBrewingStationView/BrewPanel/Panel\"]"));
        AssertTrue("BrewPanel scene labels the brew button like the mockup",
            scene.Contains("text = \"Brew Potion\""));
        AssertTrue("BrewPanel scene labels the clear button like the mockup",
            scene.Contains("text = \"Clear Ingredients\""));
        AssertTrue("BrewPanel result label supports colored taint text",
            scene.Contains("[node name=\"Result\" type=\"RichTextLabel\" parent=\"PotionBrewingStationView/BrewPanel/Panel/Instability\"]") &&
            source.Contains("_resultLabel.BbcodeEnabled = true;"));
        AssertTrue("BrewPanel shows transferred potion risks after brewing",
            source.Contains("has been tainted with -") &&
            source.Contains("[color=#E7C84E]") &&
            source.Contains("[color=#E64040]"));
    }

    private static void TestPotionInventoryCap()
    {
        var brewService = ReadProjectFile("Scripts/Systems/PotionInventoryBrewService.cs");
        var brewPanel = ReadProjectFile("Scripts/UI/BrewPanel.cs");
        var gameState = ReadProjectFile("Scripts/Autoload/GameState.cs");
        var inventoryScene = ReadProjectFile("Scenes/UI/InventoryPanel.tscn");

        AssertTrue("Potion inventory cap is four unique potions",
            gameState.Contains("public const int MaxUniquePotionInventoryQuantity = 4") &&
            brewService.Contains("public const int MaxUniquePotionInventoryQuantity = GameState.MaxUniquePotionInventoryQuantity"));
        AssertTrue("Potion stack cap is ten per potion",
            gameState.Contains("public const int MaxPotionStackQuantity = 10") &&
            brewService.Contains("public const int MaxPotionStackQuantity = GameState.MaxPotionStackQuantity"));
        AssertTrue("GameState enforces potion stack caps for all item adds",
            gameState.Contains("ResolveInventoryAddQuantity(itemId, qty)") &&
            gameState.Contains("CountInventoryPotionStacks() >= MaxUniquePotionInventoryQuantity") &&
            gameState.Contains("MaxPotionStackQuantity - currentQuantity"));
        AssertTrue("Inventory brew service blocks brewing when potion inventory is full",
            brewService.Contains("CountOwnedUniquePotions() < MaxUniquePotionInventoryQuantity") &&
            brewService.Contains("currentQuantity + quantity > MaxPotionStackQuantity") &&
            brewService.Contains("PotionInventoryFullMessage"));
        AssertTrue("BrewPanel shows the full-inventory warning in the brew result area",
            brewPanel.Contains("_resultLabel.Text = PotionInventoryBrewService.PotionInventoryFullMessage;"));
        AssertTrue("BrewPanel checks the cap before adding the brewed potion",
            brewPanel.Contains("_inventoryBrewService.CanAddPotion(potionItemId, BrewedPotionOutputQuantity)") &&
            brewPanel.Contains("_gameState.AddItem(potionItemId, BrewedPotionOutputQuantity);"));
        AssertTrue("InventoryPanel potion grid stays four slots wide",
            inventoryScene.Contains("[node name=\"Potions\" type=\"GridContainer\" parent=\"Panel/Margin/VBox/PotionsScroll\"]") &&
            inventoryScene.Contains("columns = 4"));
        AssertTrue("InventoryPanel potion scroll is one slot row tall",
            inventoryScene.Contains("[node name=\"PotionsScroll\" type=\"ScrollContainer\" parent=\"Panel/Margin/VBox\"]") &&
            inventoryScene.Contains("custom_minimum_size = Vector2(356, 168)") &&
            inventoryScene.Contains("size_flags_vertical = 0"));
    }

    private static void TestConsumableInventoryAndTreatmentTrayWiring()
    {
        var gameState = ReadProjectFile("Scripts/Autoload/GameState.cs");
        var inventoryPanel = ReadProjectFile("Scripts/UI/InventoryPanel.cs");
        var inventoryScene = ReadProjectFile("Scenes/UI/InventoryPanel.tscn");
        var hud = ReadProjectFile("Scripts/UI/Hud.cs");
        var hudScene = ReadProjectFile("Scenes/UI/Hud.tscn");
        var gameUiScene = ReadProjectFile("Scenes/UI/GameUi.tscn");
        var treatmentTray = ReadProjectFile("Scripts/UI/TreatmentTray.cs");
        var authoredItems = ReadProjectFile("Data/items_data.tres");

        AssertTrue("GameState caps consumables at four unique stacks",
            gameState.Contains("public const int MaxUniqueConsumableInventoryQuantity = 4") &&
            gameState.Contains("public const int MaxConsumableStackQuantity = 10") &&
            gameState.Contains("CountInventoryConsumableStacks() >= MaxUniqueConsumableInventoryQuantity"));
        AssertTrue("GameState records pending consumable grants when the section is full",
            gameState.Contains("PendingConsumableItemId") &&
            gameState.Contains("TryAcceptPendingConsumableByDiscarding") &&
            gameState.Contains("DeclinePendingConsumableGrant"));
        AssertTrue("InventoryPanel separates consumables from ingredients",
            inventoryPanel.Contains("ConsumablesContainerPath") &&
            inventoryPanel.Contains("var consumableStacks = _gameState.Inventory.Where(x => IsConsumable(x.Key)).ToList();") &&
            inventoryPanel.Contains("!IsPotion(x.Key) && !IsConsumable(x.Key)"));
        AssertTrue("InventoryPanel exposes consumable effect and gate details",
            inventoryPanel.Contains("BuildConsumableEffectText") &&
            inventoryPanel.Contains("BuildConsumableGateText") &&
            inventoryPanel.Contains("_itemDetailDiscardButton.Text = \"Sell\""));
        AssertTrue("InventoryPanel shows a discard prompt for full consumable grants",
            inventoryPanel.Contains("CreatePendingConsumableDialog") &&
            inventoryPanel.Contains("Discard and Accept") &&
            inventoryPanel.Contains("TryAcceptPendingConsumableByDiscarding"));
        AssertTrue("InventoryPanel scene defines a four-slot consumables section",
            inventoryScene.Contains("ConsumablesContainerPath = NodePath(\"Panel/Margin/VBox/ConsumablesScroll/Consumables\")") &&
            inventoryScene.Contains("[node name=\"ConsumablesSectionHeader\" type=\"Button\" parent=\"Panel/Margin/VBox\"]") &&
            inventoryScene.Contains("[node name=\"Consumables\" type=\"GridContainer\" parent=\"Panel/Margin/VBox/ConsumablesScroll\"]") &&
            inventoryScene.Contains("columns = 4"));
        AssertTrue("HUD exposes a Treatment Tray button",
            hud.Contains("TreatmentTrayPanelPath") &&
            hud.Contains("OnTreatmentTrayPressed") &&
            hudScene.Contains("[node name=\"TreatmentTray\" type=\"Button\" parent=\".\"]") &&
            hudScene.Contains("text = \"Treatment Tray\""));
        AssertTrue("Game UI defines the Treatment Tray panel and drop boxes",
            gameUiScene.Contains("[node name=\"TreatmentTray\" type=\"Control\" parent=\".\"]") &&
            gameUiScene.Contains("custom_minimum_size = Vector2(430, 286)") &&
            gameUiScene.Contains("[node name=\"Panel\" type=\"PanelContainer\" parent=\"TreatmentTray\"]") &&
            gameUiScene.Contains("script = ExtResource(\"20_treatment_tray\")") &&
            gameUiScene.Contains("ConsumableDropBoxPath") &&
            gameUiScene.Contains("TargetDropBoxPath") &&
            gameUiScene.Contains("text = \"Apply Treatment\""));
        AssertTrue("Treatment Tray panel is draggable from the panel body",
            gameUiScene.Contains("[node name=\"Panel\" type=\"PanelContainer\" parent=\"TreatmentTray\"]") &&
            gameUiScene.Contains("script = ExtResource(\"12_6x1m8\")") &&
            gameUiScene.Contains("DragHandlePath = NodePath(\"\")"));
        AssertTrue("TreatmentTray normalizes its runtime geometry before showing",
            treatmentTray.Contains("public Vector2 TraySize") &&
            treatmentTray.Contains("ApplyTrayGeometry();") &&
            treatmentTray.Contains("panel.AnchorRight = 0.0f") &&
            treatmentTray.Contains("panel.Size = normalizedTraySize"));
        AssertTrue("TreatmentTray reserves dropped items until treatment or clear",
            treatmentTray.Contains("ReserveSlotItem") &&
            treatmentTray.Contains("_gameState.ConsumeItem(itemId, 1)") &&
            treatmentTray.Contains("ReturnSelectedItems") &&
            treatmentTray.Contains("_gameState.AddItem(_selectedConsumableId, 1)") &&
            treatmentTray.Contains("_gameState.AddItem(_selectedTargetId, 1)") &&
            treatmentTray.Contains("TryApplyReservedTreatment"));
        AssertTrue("Authored consumables include remove-risk definitions",
            authoredItems.Contains("\"id\": \"risk_salve\"") &&
            authoredItems.Contains("\"id\": \"cleansing_salt\"") &&
            authoredItems.Contains("\"kind\": \"remove_risk\"") &&
            authoredItems.Contains("\"allowedTargetTags\": [\"ingredient\", \"potion\"]"));
    }

    private static void TestTreatmentServiceCreatesExpectedOutputs()
    {
        var treatmentService = ReadProjectFile("Scripts/Systems/TreatmentService.cs");
        var itemCatalog = ReadProjectFile("Scripts/Autoload/ItemCatalogService.cs");
        var runtimeDb = ReadProjectFile("Scripts/Autoload/RuntimeContentDb.cs");
        var inventoryPanel = ReadProjectFile("Scripts/UI/InventoryPanel.cs");
        var potionBookPanel = ReadProjectFile("Scripts/UI/PotionBookPanel.cs");
        var recipeBookPanel = ReadProjectFile("Scripts/UI/RecipeBookPanel.cs");

        AssertTrue("TreatmentService validates remove-risk consumables",
            treatmentService.Contains("ConsumableEffectDef.RemoveRiskKind") &&
            treatmentService.Contains("TrySelectRiskToRemove") &&
            treatmentService.Contains("Selected item has no risks to remove."));
        AssertTrue("TreatmentService blocks already treated targets",
            treatmentService.Contains("target.Treatment is not null") &&
            treatmentService.Contains("That item has already been treated."));
        AssertTrue("TreatmentService creates a runtime item for treated ingredients instead of mutating the base definition",
            treatmentService.Contains("BuildTreatmentCandidate") &&
            treatmentService.Contains("new Dictionary<string, int>(target.Risks, StringComparer.OrdinalIgnoreCase)") &&
            treatmentService.Contains("risks.Remove(removedRisk)") &&
            treatmentService.Contains("_runtimeContentDb.UpsertRuntimeItem(candidate.RuntimeItem)"));
        AssertTrue("TreatmentService cleans treated potions back into matching potion stacks",
            treatmentService.Contains("TryBuildPotionTreatmentCandidate") &&
            treatmentService.Contains("GetBasePotionItemId") &&
            treatmentService.Contains("PotionVariantIdBuilder.BuildRiskVariantItemId") &&
            treatmentService.Contains("outputPotionItemId = basePotionItemId") &&
            treatmentService.Contains("new TreatmentCandidate(outputPotionItemId, null, removedRisk)"));
        AssertTrue("TreatmentService consumes exactly one consumable and one target item",
            treatmentService.Contains("_gameState.ConsumeItem(consumableItemId, 1)") &&
            treatmentService.Contains("_gameState.ConsumeItem(targetItemId, 1)") &&
            treatmentService.Contains("_gameState.AddItem(candidate.OutputItemId, 1)"));
        AssertTrue("TreatmentService supports applying already reserved tray items",
            treatmentService.Contains("CanApplyReservedTreatment") &&
            treatmentService.Contains("TryApplyReservedTreatment") &&
            treatmentService.Contains("TryBuildTreatmentCandidate(consumableItemId, targetItemId, false, true") &&
            treatmentService.Contains("if (requireInventory && !_gameState.HasItem"));
        AssertTrue("TreatmentService protects potion inventory caps for treated potion variants",
            treatmentService.Contains("CanFitTreatedPotion") &&
            treatmentService.Contains("GameState.MaxUniquePotionInventoryQuantity") &&
            treatmentService.Contains("Potion inventory is full. Sell a potion before treating another unique potion."));
        AssertTrue("Catalog and runtime clones preserve treatment metadata",
            itemCatalog.Contains("IsConsumable") &&
            itemCatalog.Contains("IsTreatedItem") &&
            runtimeDb.Contains("ConsumableEffect = item.ConsumableEffect is null") &&
            runtimeDb.Contains("Treatment = item.Treatment is null"));
        AssertTrue("Treated potions cannot be added to or brewed from the potion books",
			inventoryPanel.Contains("item.Treatment is not null") &&
			potionBookPanel.Contains("potion.Treatment is not null") &&
			recipeBookPanel.Contains("item.Treatment is not null"));
	}

    private static void TestBrewAndInventoryPriceWiring()
    {
        var brewPanel = ReadProjectFile("Scripts/UI/BrewPanel.cs");
        AssertTrue("BrewPanel calculates potion price from ingredient totals",
            brewPanel.Contains("CalculateIngredientTotalPrice(_queuedIngredients)"));
        AssertTrue("BrewPanel renders the mockup price label",
            brewPanel.Contains("\\u00A3{totalIngredientPrice}"));
        AssertTrue("BrewPanel stores the potion base price in state",
            brewPanel.Contains("RegisterPotionBasePrice(potionItemId, potionBasePrice)"));
        AssertTrue("BrewPanel sums ingredient BasePrice values",
            brewPanel.Contains("totalPrice += Math.Max(0, item.BasePrice);"));

        var inventoryPanel = ReadProjectFile("Scripts/UI/InventoryPanel.cs");
        AssertTrue("InventoryPanel resolves stored potion prices",
            inventoryPanel.Contains("TryGetPotionBasePrice(itemId, out _)"));
        AssertTrue("InventoryPanel shows potion price in the detail panel",
            inventoryPanel.Contains("GetItemPrice(_currentItemId, item)"));
        AssertTrue("InventoryPanel shows item prices on the slot icon",
            inventoryPanel.Contains("GetItemPrice(itemId, item)"));
    }

    private static void TestBrewPanelSplitsRiskVariantsForKnownCombinations()
    {
        var brewPanel = ReadProjectFile("Scripts/UI/BrewPanel.cs");

        AssertTrue("BrewPanel compares the known potion risks against the new brew result",
            brewPanel.Contains("PotionVariantIdBuilder.RisksMatch(basePotionItem.Risks, brewResult.Risks)"));
        AssertTrue("BrewPanel builds a distinct item id for changed carried risks",
            brewPanel.Contains("PotionVariantIdBuilder.BuildRiskVariantItemId(potionItemId, brewResult.Risks)"));
        AssertTrue("BrewPanel registers the variant with the newly rolled risks",
            brewPanel.Contains("variantPotionItemId") &&
            brewPanel.Contains("new Dictionary<string, int>(brewResult.Risks)"));
        AssertTrue("BrewPanel adds the variant item to inventory after risk splitting",
            brewPanel.Contains("potionItemId = variantPotionItemId;") &&
            brewPanel.Contains("_gameState.AddItem(potionItemId, BrewedPotionOutputQuantity);"));
    }

    private static void TestBrewPanelRiskVariantIdsAreDeterministic()
    {
        var risks = new Dictionary<string, int>
        {
            ["Wasting Fever"] = 1,
            ["corruption"] = 1,
            ["ignored"] = 0
        };

        var variantId = PotionVariantIdBuilder.BuildRiskVariantItemId("brew_1", risks);
        AssertEqual("Risk variant id", "brew_1__risk_corruption_wasting_fever", variantId);

        var cleanVariantId = PotionVariantIdBuilder.BuildRiskVariantItemId("brew_1", new Dictionary<string, int>());
        AssertEqual("Clean variant id", "brew_1__risk_clean", cleanVariantId);

        var existingRisks = new Dictionary<string, int>
        {
            ["Corruption"] = 1,
            ["wasting fever"] = 1
        };
        var risksMatch = PotionVariantIdBuilder.RisksMatch(existingRisks, risks);
        AssertTrue("Risk comparison ignores casing and zero values", risksMatch);
    }

    private static void TestPotionDetailManuallyAddsCleanPotionsToBook()
    {
        var inventoryPanel = ReadProjectFile("Scripts/UI/InventoryPanel.cs");
        var inventoryScene = ReadProjectFile("Scenes/UI/InventoryPanel.tscn");
        var gameUiScene = ReadProjectFile("Scenes/UI/GameUi.tscn");
        var gameState = ReadProjectFile("Scripts/Autoload/GameState.cs");

        AssertTrue("GameState records potion recipes without automatically learning them",
            gameState.Contains("public void RecordPotionRecipe(string potionItemId, IReadOnlyList<string> ingredientIds)") &&
            !gameState.Contains("LearnPotion(potionItemId);"));
        AssertTrue("InventoryPanel exports an add-to-book detail button path",
            inventoryPanel.Contains("ItemDetailAddToPotionBookButtonPath"));
        AssertTrue("InventoryPanel resolves the add-to-book detail button",
            inventoryPanel.Contains("GetNodeOrNull<Button>(ItemDetailAddToPotionBookButtonPath)") &&
            inventoryPanel.Contains("GetNodeOrNull<Button>(new NodePath(\"../InventoryItemDetail/Panel/Margin/VBox/Actions/AddToBook\"))"));
        AssertTrue("InventoryPanel wires the add-to-book action",
            inventoryPanel.Contains("if (_itemDetailAddToPotionBookButton is not null)") &&
            inventoryPanel.Contains("_itemDetailAddToPotionBookButton.Pressed += TryAddSelectedPotionToBook"));
        AssertTrue("InventoryPanel manually learns the selected clean potion",
            inventoryPanel.Contains("_gameState.LearnPotion(_currentItemId);"));
        AssertTrue("InventoryPanel hides add-to-book for tainted potions",
            inventoryPanel.Contains("if (HasActiveRisk(item))") &&
            inventoryPanel.Contains("_itemDetailAddToPotionBookButton.Visible = false"));
        AssertTrue("InventoryPanel hides add-to-book for already learned potions",
            inventoryPanel.Contains("if (_gameState.KnowsPotion(itemId))"));
        AssertTrue("InventoryPanel repeat brew requires the potion to be in the book",
            inventoryPanel.Contains("var isInPotionBook = _gameState.KnowsPotion(_currentItemId);") &&
            inventoryPanel.Contains("_itemDetailBrewButton.Disabled = !isInPotionBook || !hasIngredients;"));
        AssertTrue("InventoryPanel scene wires the add-to-book path",
            inventoryScene.Contains("ItemDetailAddToPotionBookButtonPath = NodePath(\"../InventoryItemDetail/Panel/Margin/VBox/Actions/AddToBook\")"));
        AssertTrue("Game UI defines the add-to-book button beside potion detail actions",
            gameUiScene.Contains("[node name=\"AddToBook\" type=\"Button\" parent=\"InventoryItemDetail/Panel/Margin/VBox/Actions\"]") &&
            gameUiScene.Contains("text = \"Add to Book\""));
        AssertTrue("Game UI instance does not null out the add-to-book path",
            inventoryScene.Contains("ItemDetailAddToPotionBookButtonPath = NodePath(\"../InventoryItemDetail/Panel/Margin/VBox/Actions/AddToBook\")") &&
            !gameUiScene.Contains("ItemDetailAddToPotionBookButtonPath = null") &&
            !gameUiScene.Contains("ItemDetailAddToPotionBookButtonPath = NodePath(\"\")"));
        AssertTrue("Game UI instance wires the discard path",
            inventoryScene.Contains("ItemDetailDiscardButtonPath = NodePath(\"../InventoryItemDetail/Panel/Margin/VBox/Actions/Discard\")") &&
            !gameUiScene.Contains("ItemDetailDiscardButtonPath = null") &&
            !gameUiScene.Contains("ItemDetailDiscardButtonPath = NodePath(\"\")"));
    }

    private static void TestRepeatBrewFailuresShowCursorToast()
    {
        var cursorToast = ReadProjectFile("Scripts/UI/CursorToast.cs");
        var inventoryPanel = ReadProjectFile("Scripts/UI/InventoryPanel.cs");
        var potionBookPanel = ReadProjectFile("Scripts/UI/PotionBookPanel.cs");
        var recipeBookPanel = ReadProjectFile("Scripts/UI/RecipeBookPanel.cs");

        AssertTrue("CursorToast renders above the captured cursor position",
            cursorToast.Contains("viewport.GetMousePosition()") &&
            cursorToast.Contains("_cursorPosition.Y - toastSize.Y - CursorOffsetY"));
        AssertTrue("CursorToast lasts three seconds",
            cursorToast.Contains("DisplaySeconds = 3.0") &&
            cursorToast.Contains("WaitTime = DisplaySeconds"));
        AssertTrue("InventoryPanel brew failure uses cursor toast",
            inventoryPanel.Contains("CursorToast.Show(this, error);") &&
            !inventoryPanel.Contains("GD.PushError(error);"));
        AssertTrue("PotionBookPanel brew failure uses cursor toast",
            potionBookPanel.Contains("CursorToast.Show(this, error);") &&
            !potionBookPanel.Contains("GD.PushError(error);"));
        AssertTrue("RecipeBookPanel brew failure uses cursor toast",
            recipeBookPanel.Contains("CursorToast.Show(this, error);") &&
            !recipeBookPanel.Contains("GD.PushError(error);"));
    }
}
