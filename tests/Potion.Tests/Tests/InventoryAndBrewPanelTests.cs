using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using OccultShop.Models;
using OccultShop.Systems;
using OccultShop.UI;
using static ProjectFileTestHelper;
using static TestAssert;
using static UiReflectionTestHelper;

internal static class InventoryAndBrewPanelTests
{
    public static void Register(TestRunner runner)
    {
        runner.Run("Draggable panel whole-panel drag respects child action buttons", TestDraggablePanelWholePanelDragRespectsChildButtons);
        runner.Run("Inventory item slot clears highlight on outside click", TestInventoryItemSlotClearsHighlightOnOutsideClick);
        runner.Run("Inventory item detail summarizes unprepared ingredient preparation stats", TestUnpreparedIngredientDetailPreparationStats);
        runner.Run("BrewPanel ingredient tag detection is case-insensitive", TestBrewPanelIsIngredient);
        runner.Run("BrewPanel previews potion names before brewing", TestBrewPanelPreviewNameIsWired);
        runner.Run("BrewPanel previews live partial brew results", TestBrewPanelPreviewsLivePartialResults);
        runner.Run("BrewPanel request checklist compares active request to queued brew", TestBrewPanelRequestChecklistWiring);
        runner.Run("Potion inventory is capped at four unique potions with ten per stack", TestPotionInventoryCap);
        runner.Run("Consumable inventory and treatment tray are wired", TestConsumableInventoryAndTreatmentTrayWiring);
        runner.Run("Ingredient scales are wired on the brewing station", TestIngredientScalesWiring);
        runner.Run("Ingredient preparation tray previews prep outputs", TestIngredientPreparationTrayPreviewWiring);
        runner.Run("Prepared ingredients queue directly and brew panel discards clears", TestPreparedIngredientsQueueDirectlyAndBrewPanelDiscards);
        runner.Run("Measured ingredients are stored for exact-gram requests", TestMeasuredIngredientPersistenceWiring);
        runner.Run("Treatment service creates expected treatment outputs", TestTreatmentServiceCreatesExpectedOutputs);
        runner.Run("Brew and inventory price wiring stays intact", TestBrewAndInventoryPriceWiring);
        runner.Run("BrewPanel splits risk variants for known potion combinations", TestBrewPanelSplitsRiskVariantsForKnownCombinations);
        runner.Run("BrewPanel risk variant ids are deterministic", TestBrewPanelRiskVariantIdsAreDeterministic);
        runner.Run("Repeat brew failures show cursor toast instead of console error", TestRepeatBrewFailuresShowCursorToast);
    }

    private static void TestDraggablePanelWholePanelDragRespectsChildButtons()
    {
        var draggablePanel = ReadProjectFile("Scripts/UI/DraggablePanel.cs");

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
    }

    private static void TestInventoryItemSlotClearsHighlightOnOutsideClick()
    {
        var source = ReadProjectFile("Scripts/UI/InventoryItemSlot.cs");

        AssertTrue("InventoryItemSlot receives global input for outside clicks",
            source.Contains("public override void _Input(InputEvent @event)") &&
            source.Contains("@event is not InputEventMouseButton mouseButton"));
        AssertTrue("InventoryItemSlot only clears after left or right mouse presses outside its bounds",
            source.Contains("mouseButton.ButtonIndex != MouseButton.Left && mouseButton.ButtonIndex != MouseButton.Right") &&
            source.Contains("GetGlobalRect().HasPoint(mouseButton.GlobalPosition)"));
        AssertTrue("InventoryItemSlot clears hover outline and button state",
            source.Contains("_isHovered = false;") &&
            source.Contains("ButtonPressed = false;") &&
            source.Contains("ReleaseFocus();") &&
            source.Contains("UpdateHoverOutline();"));
        AssertTrue("InventoryItemSlot only releases focus while inside the tree",
            source.Contains("ReleaseFocusIfInsideTree()") &&
            source.Contains("if (!IsInsideTree())"));
    }

    private static void TestUnpreparedIngredientDetailPreparationStats()
    {
        var preparations = new Dictionary<string, IngredientPreparationDef>(StringComparer.OrdinalIgnoreCase)
        {
            ["raw"] = new()
            {
                Traits = new Dictionary<string, int> { ["calm"] = 4 },
                Risks = new Dictionary<string, int>()
            },
            ["steeped"] = new()
            {
                Traits = new Dictionary<string, int> { ["calm"] = 6 },
                Risks = new Dictionary<string, int> { ["drowsiness"] = 1 }
            },
            ["crushed"] = new()
            {
                Traits = new Dictionary<string, int> { ["clarity"] = 3 },
                Risks = new Dictionary<string, int> { ["instability"] = 2 }
            }
        };

        var traits = InventoryItemTextFormatter.FormatPreparationTraitNames(preparations, 3);
        var risks = InventoryItemTextFormatter.FormatPreparationRiskNames(preparations, 3);

        AssertEqual("Preparation trait names", "Calm\nClarity\n", traits);
        AssertEqual("Preparation risk names", "Drowsiness\nInstability\n", risks);
        AssertTrue("Preparation trait names omit values", !traits.Contains("+") && !traits.Contains("4") && !traits.Contains("6"));
        AssertTrue("Preparation risk names omit values", !risks.Contains("+") && !risks.Contains("1") && !risks.Contains("2"));
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
        var stationShelf = ReadProjectFile("Scripts/UI/StationShelfInventory.cs");
        AssertTrue("Station shelf right-click routes raw ingredients to prep and prepared ingredients to brew",
            stationShelf.Contains("_ingredientPreparationTray.TrySelectIngredientFromInventory(itemId)") &&
            stationShelf.Contains("_brewPanel.TryQueueIngredient(itemId);"));
        AssertTrue("Brew drop box still emits dragged item ids",
            ReadProjectFile("Scripts/UI/BrewDropBox.cs").Contains("EmitSignal(SignalName.ItemDropped, data.AsString());"));
    }

    private static void TestBrewPanelPreviewNameIsWired()
    {
        var source = ReadProjectFile("Scripts/UI/BrewPanel.cs");
        var textFormatter = ReadProjectFile("Scripts/UI/BrewPanelTextFormatter.cs");
        var scene = ReadProjectFile("Scenes/UI/GameUi.tscn");

        AssertTrue("BrewPanel keeps preview naming internal after removing the current brew field",
            !source.Contains("PotionNamePreviewLabelPath") &&
            !scene.Contains("PotionNamePreviewLabelPath"));
        AssertTrue("BrewPanel caches the current preview combination",
            source.Contains("_previewPotionCombinationKey"));
        AssertTrue("BrewPanel caches the current preview name",
            source.Contains("_previewPotionName"));
        AssertTrue("BrewPanel resolves the preview name before brewing",
            source.Contains("var potionDisplayName = GetPreviewPotionName(combinationKey);"));
        AssertTrue("BrewPanel regenerates preview names from the combination key",
            source.Contains("GetPreviewPotionName(string combinationKey)"));
        AssertTrue("BrewPanel scene uses a board and parchment treatment instead of the old potion preview board texture",
            !scene.Contains("path=\"res://art/Potion-Preview-Board.png\"") &&
            scene.Contains("[node name=\"Board\" type=\"PanelContainer\" parent=\"PotionBrewingStationView/BrewPanel/Panel\"]") &&
            scene.Contains("theme_override_styles/panel = SubResource(\"StyleBoxFlat_brew_info_panel\")") &&
            scene.Contains("[node name=\"Paper\" type=\"PanelContainer\" parent=\"PotionBrewingStationView/BrewPanel/Panel\"]") &&
            scene.Contains("theme_override_styles/panel = SubResource(\"StyleBoxFlat_brew_paper_panel\")") &&
            scene.Contains("[node name=\"Marker1\" type=\"PanelContainer\" parent=\"PotionBrewingStationView/BrewPanel/Panel/FormulaSlots\"]"));
        AssertTrue("BrewPanel scene labels the brew button like the mockup",
            scene.Contains("text = \"Brew\""));
        AssertTrue("BrewPanel scene labels the clear button like the mockup",
            scene.Contains("text = \"Clear\""));
        AssertTrue("BrewPanel uses toast feedback after removing the instability result label",
            !scene.Contains("[node name=\"Result\" type=\"RichTextLabel\" parent=\"PotionBrewingStationView/BrewPanel/Panel/Instability\"]") &&
            source.Contains("ShowBrewFeedback(") &&
            source.Contains("CursorToast.Show(this, message);"));
        AssertTrue("BrewPanel shows transferred potion risks after brewing",
            source.Contains("BrewPanelTextFormatter.BuildBrewResultToastText") &&
            textFormatter.Contains("BuildBrewResultToastText") &&
            textFormatter.Contains("has been tainted with -"));
    }

    private static void TestBrewPanelPreviewsLivePartialResults()
    {
        var source = ReadProjectFile("Scripts/UI/BrewPanel.cs");
        var scene = ReadProjectFile("Scenes/UI/GameUi.tscn");
        AssertTrue("BrewPanel clears request checklist preview for an empty queue",
            source.Contains("if (ingredientCount == 0)") &&
            source.Contains("RefreshRequestChecklist(null);"));
        AssertTrue("BrewPanel calculates a live preview for request checklist matching",
            source.Contains("var previewResult = _brewingService.PreviewPotion(") &&
            source.Contains("knownStatsOnly: true") &&
            source.Contains("ingredient.Traits.Clear();") &&
            source.Contains("ingredient.Risks.Clear();") &&
            source.Contains("ingredient.IngredientEffects.Clear();") &&
            source.Contains("RefreshRequestChecklist(previewResult);"));
        AssertTrue("BrewPanel scene removes the requested current brew information fields",
            !scene.Contains("[node name=\"CurrentBrew\" type=\"Control\" parent=\"PotionBrewingStationView/BrewPanel/Panel\"]") &&
            !scene.Contains("[node name=\"KnownProperties\" type=\"Control\" parent=\"PotionBrewingStationView/BrewPanel/Panel\"]") &&
            !scene.Contains("[node name=\"KnownDangers\" type=\"Control\" parent=\"PotionBrewingStationView/BrewPanel/Panel\"]") &&
            !scene.Contains("[node name=\"Instability\" type=\"Control\" parent=\"PotionBrewingStationView/BrewPanel/Panel\"]") &&
            !scene.Contains("[node name=\"EstimatedValue\" type=\"Control\" parent=\"PotionBrewingStationView/BrewPanel/Panel\"]"));
        AssertTrue("BrewPanel no longer renders removed live preview fields",
            !source.Contains("BuildStatListText(previewResult.Traits, 3)") &&
            !source.Contains("BuildRiskChanceListText(previewResult.PossibleRisks, 2)") &&
            !source.Contains("BuildPreviewEffectText(previewResult)"));
    }

    private static void TestBrewPanelRequestChecklistWiring()
    {
        var source = ReadProjectFile("Scripts/UI/BrewPanel.cs");
        var formatter = ReadProjectFile("Scripts/UI/CustomerDialogueTextFormatter.cs");
        var scene = ReadProjectFile("Scenes/UI/GameUi.tscn");

        AssertTrue("BrewPanel exports and resolves the request checklist label",
            source.Contains("RequestChecklistLabelPath") &&
            source.Contains("_requestChecklistLabel = GetNode<RichTextLabel>(RequestChecklistLabelPath);") &&
            source.Contains("_requestChecklistLabel.BbcodeEnabled = true;"));
        AssertTrue("BrewPanel refreshes the checklist from the active request and queued ingredients",
            source.Contains("RefreshRequestChecklist(previewResult);") &&
            source.Contains("RefreshRequestChecklist(null);") &&
            source.Contains("_gameState.ActiveCustomerRequest") &&
            source.Contains("previewResult?.PossibleRisks") &&
            source.Contains("_queuedIngredients"));
        AssertTrue("BrewPanel scene places the request fit checklist on the preview board",
            scene.Contains("RequestChecklistLabelPath = NodePath(\"Panel/RequestChecklist/Lines\")") &&
            scene.Contains("[node name=\"RequestChecklist\" type=\"Control\" parent=\"PotionBrewingStationView/BrewPanel/Panel\"]") &&
            scene.Contains("[node name=\"Lines\" type=\"RichTextLabel\" parent=\"PotionBrewingStationView/BrewPanel/Panel/RequestChecklist\"]") &&
            scene.Contains("text = \"Requested Fit\""));
        AssertTrue("Customer request formatter exposes brewing checklist output",
            formatter.Contains("BuildBrewingRequestChecklistText") &&
            formatter.Contains("AddDesiredTraitChecklistLines") &&
            formatter.Contains("AddBadTraitChecklistLines") &&
            formatter.Contains("AddIngredientRequirementChecklistLines"));

        var request = new CustomerRequestDef
        {
            DesiredTraits = new Dictionary<string, CustomerTraitRangeDef>
            {
                ["sleep"] = new() { Min = 3 }
            },
            BadTraits = new Dictionary<string, CustomerTraitRangeDef>
            {
                ["insomnia"] = new() { Max = 0 }
            },
            RequiredMinTraits = new Dictionary<string, int>
            {
                ["calm"] = 2
            },
            RequiredMaxTraits = new Dictionary<string, int>
            {
                ["vigor"] = 1
            },
            RequiredIngredientAmounts = new List<IngredientPortionDef>
            {
                new()
                {
                    IngredientId = "mint",
                    PreparationId = "raw",
                    Grams = 5
                }
            }
        };
        var producedTraits = new Dictionary<string, int>
        {
            ["sleep"] = 2,
            ["calm"] = 2,
            ["vigor"] = 3
        };
        var possibleRisks = new Dictionary<string, int>
        {
            ["insomnia"] = 1
        };
        var queuedIngredients = new List<IngredientPortionDef>
        {
            new()
            {
                IngredientId = "mint",
                PreparationId = "raw",
                Grams = 5
            }
        };

        var checklist = CustomerDialogueTextFormatter.BuildBrewingRequestChecklistText(
            request,
            producedTraits,
            possibleRisks,
            queuedIngredients);

        AssertTrue("Checklist marks desired traits as partial until the target is reached",
            checklist.Contains("[color=#E7C84E]~[/color] sleep 2 / >= 3"));
        AssertTrue("Checklist marks required minimum traits as satisfied",
            checklist.Contains("[color=#59D959]OK[/color] calm 2 / >= 2"));
        AssertTrue("Checklist marks bad possible risks as conflicts",
            checklist.Contains("[color=#E64040]![/color] insomnia 1 / <= 0"));
        AssertTrue("Checklist marks required maximum traits as conflicts",
            checklist.Contains("[color=#E64040]![/color] vigor 3 / <= 1"));
        AssertTrue("Checklist marks exact prepared ingredient requirements as satisfied",
            checklist.Contains("[color=#59D959]OK[/color] mint: Raw prep, 5g"));
    }

    private static void TestPotionInventoryCap()
    {
        var brewService = ReadProjectFile("Scripts/Systems/PotionInventoryBrewService.cs");
        var brewPanel = ReadProjectFile("Scripts/UI/BrewPanel.cs");
        var gameState = ReadProjectFile("Scripts/Autoload/GameState.cs");
        var inventoryState = ReadProjectFile("Scripts/Systems/InventoryState.cs");
        var potionRow = ReadProjectFile("Scripts/UI/PotionInventoryRow.cs");
        var gameUiScene = ReadProjectFile("Scenes/UI/GameUi.tscn");

        AssertTrue("Potion inventory cap is four unique potions",
            gameState.Contains("public const int MaxUniquePotionInventoryQuantity = 4") &&
            brewService.Contains("public const int MaxUniquePotionInventoryQuantity = GameState.MaxUniquePotionInventoryQuantity"));
        AssertTrue("Potion stack cap is ten per potion",
            gameState.Contains("public const int MaxPotionStackQuantity = 10") &&
            brewService.Contains("public const int MaxPotionStackQuantity = GameState.MaxPotionStackQuantity"));
        AssertTrue("GameState enforces potion stack caps for all item adds",
            inventoryState.Contains("ResolveInventoryAddQuantity(itemId, quantity, out var changed)") &&
            inventoryState.Contains("CountPotionStacks() >= _maxUniquePotionInventoryQuantity") &&
            inventoryState.Contains("_maxPotionStackQuantity - currentQuantity"));
        AssertTrue("Inventory brew service blocks brewing when potion inventory is full",
            brewService.Contains("CountOwnedUniquePotions() < MaxUniquePotionInventoryQuantity") &&
            brewService.Contains("currentQuantity + quantity > MaxPotionStackQuantity") &&
            brewService.Contains("PotionInventoryFullMessage"));
        AssertTrue("BrewPanel shows the full-inventory warning through brew feedback",
            brewPanel.Contains("ShowBrewFeedback(PotionInventoryBrewService.PotionInventoryFullMessage);"));
        AssertTrue("BrewPanel checks the cap before adding the brewed potion",
            brewPanel.Contains("_inventoryBrewService.CanAddPotion(potionItemId, BrewedPotionOutputQuantity)") &&
            brewPanel.Contains("_gameState.AddItem(potionItemId, BrewedPotionOutputQuantity);"));
        AssertTrue("Potion inventory row stays capped to the unique potion limit",
            potionRow.Contains("VisiblePotionSlots = GameState.MaxUniquePotionInventoryQuantity") &&
            potionRow.Contains("if (stacks.Count >= VisiblePotionSlots)") &&
            gameUiScene.Contains("[node name=\"PotionSlots\" type=\"GridContainer\" parent=\"PotionBrewingStationView/PotionInventoryRow\"]") &&
            gameUiScene.Contains("columns = 4"));
	}

    private static void TestIngredientScalesWiring()
    {
        var scene = ReadProjectFile("Scenes/UI/GameUi.tscn");
        var scalesPanel = ReadProjectFile("Scripts/UI/IngredientScalesPanel.cs");
        var scalesDropBox = ReadProjectFile("Scripts/UI/ScalesDropBox.cs");
        var scaleWeightButton = ReadProjectFile("Scripts/UI/ScaleWeightButton.cs");
        var brewPanel = ReadProjectFile("Scripts/UI/BrewPanel.cs");

        AssertTrue("Game UI references the brewing station scales sprite",
            scene.Contains("path=\"res://Assets/Art/BrewingStationBright/scales_bright.png\"") &&
            scene.Contains("[node name=\"IngredientScales\" type=\"Control\" parent=\"PotionBrewingStationView\"]") &&
            scene.Contains("script = ExtResource(\"34_scales_panel\")"));
        AssertTrue("Scales panel exposes only the ingredient drop box",
            scene.Contains("[node name=\"IngredientPan\" type=\"PanelContainer\" parent=\"PotionBrewingStationView/IngredientScales\"]") &&
            scene.Contains("IngredientDropBoxPath = NodePath(\"IngredientPan\")") &&
            !scene.Contains("WeightDropBoxPath") &&
            !scene.Contains("[node name=\"WeightPan\"") &&
            !scene.Contains("AcceptWeights"));
        AssertTrue("Scales scene provides draggable 1g, 2g, 5g, and 10g weights",
            scene.Contains("[node name=\"OneGram\" type=\"Button\" parent=\"PotionBrewingStationView/IngredientScales/Weights\"]") &&
            scene.Contains("Grams = 2") &&
            scene.Contains("Grams = 5") &&
            scene.Contains("Grams = 10") &&
            scene.Contains("script = ExtResource(\"36_scale_weight\")"));
        AssertTrue("Scales scene uses SVG sprites for each gameplay weight",
            scene.Contains("path=\"res://Assets/UI/scale_weight_1g.svg\"") &&
            scene.Contains("path=\"res://Assets/UI/scale_weight_2g.svg\"") &&
            scene.Contains("path=\"res://Assets/UI/scale_weight_5g.svg\"") &&
            scene.Contains("path=\"res://Assets/UI/scale_weight_10g.svg\"") &&
            scene.Contains("[node name=\"Sprite\" type=\"TextureRect\" parent=\"PotionBrewingStationView/IngredientScales/Weights/OneGram\"]") &&
            scene.Contains("[node name=\"Sprite\" type=\"TextureRect\" parent=\"PotionBrewingStationView/IngredientScales/Weights/TwoGram\"]") &&
            scene.Contains("[node name=\"Sprite\" type=\"TextureRect\" parent=\"PotionBrewingStationView/IngredientScales/Weights/FiveGram\"]") &&
            scene.Contains("[node name=\"Sprite\" type=\"TextureRect\" parent=\"PotionBrewingStationView/IngredientScales/Weights/TenGram\"]") &&
            !scene.Contains("text = \"1g\"") &&
            !scene.Contains("text = \"2g\"") &&
            !scene.Contains("text = \"5g\"") &&
            !scene.Contains("text = \"10g\""));
        AssertTrue("Scales scene removed the old weight pan preview nodes",
            !scene.Contains("PlacedWeightsContainerPath") &&
            !scene.Contains("WeightPanSummaryLabelPath") &&
            !scene.Contains("[node name=\"PlacedWeights\"") &&
            !scene.Contains("[node name=\"WeightContent\"") &&
            !scene.Contains("[node name=\"WeightHint\""));
        AssertTrue("IngredientScalesPanel reserves dropped ingredients until clear or confirm",
            scalesPanel.Contains("GetNodeOrNull<GameState>(GameStatePath)") &&
            scalesPanel.Contains("ReserveSelectedIngredient(itemId)") &&
            scalesPanel.Contains("_gameState.ConsumeItem(itemId, 1)") &&
            scalesPanel.Contains("ReturnSelectedIngredient()") &&
            scalesPanel.Contains("_gameState.AddItem(_selectedIngredientId, 1)") &&
            scalesPanel.Contains("ResetScaleToDefault(returnIngredient: false)") &&
            scalesPanel.Contains("TryQueueReservedMeasuredIngredient(_selectedIngredientId, totalGrams)"));
        AssertTrue("IngredientScalesPanel confirms measured ingredients through BrewPanel",
            scalesPanel.Contains("TryQueueReservedMeasuredIngredient(_selectedIngredientId, totalGrams)") &&
            scalesPanel.Contains("public override bool _CanDropData") &&
            scalesPanel.Contains("public override void _DropData") &&
            scalesPanel.Contains("MouseFilter = MouseFilterEnum.Stop") &&
            scalesPanel.Contains("ScaleWeightButton.TryParseDragData(value, out var grams)") &&
            scalesPanel.Contains("ConnectWeightButtons") &&
            scalesPanel.Contains("ResetScaleToDefault") &&
            scalesPanel.Contains("_statusLabel.Text = DefaultStatusText") &&
            !scalesPanel.Contains("WeightDropBoxPath") &&
            !scalesPanel.Contains("RenderPlacedWeights") &&
            scalesPanel.Contains("Drop an ingredient and at least one weight.") &&
            scalesPanel.Contains("The scales only accept ingredients.") &&
            !scalesPanel.Contains("_statusLabel.Text = $\"Added"));
        AssertTrue("ScalesDropBox rejects weight drags and emits ingredient drops",
            scalesDropBox.Contains("ScaleWeightButton.TryParseDragData") &&
            scalesDropBox.Contains("return !ScaleWeightButton.TryParseDragData(value, out _)") &&
            scalesDropBox.Contains("EmitSignal(SignalName.ItemDropped, value)") &&
            !scalesDropBox.Contains("WeightDropped") &&
            !scalesDropBox.Contains("AcceptWeights") &&
            !scalesDropBox.Contains("AcceptItems"));
        AssertTrue("ScaleWeightButton uses an explicit drag data prefix",
            scaleWeightButton.Contains("DragDataPrefix = \"scale_weight:\"") &&
            scaleWeightButton.Contains("BuildDragData(Grams)") &&
            scaleWeightButton.Contains("TryParseDragData") &&
            scaleWeightButton.Contains("GetNodeOrNull<TextureRect>(SpritePath)") &&
            scaleWeightButton.Contains("SetDragPreview(CreateDragPreview())") &&
            scaleWeightButton.Contains("Modulate = new Color(_defaultModulate.R, _defaultModulate.G, _defaultModulate.B, 0.0f)") &&
            scaleWeightButton.Contains("WeightActivated") &&
            scaleWeightButton.Contains("_suppressNextPress") &&
            scaleWeightButton.Contains("NotificationDragEnd"));
        AssertTrue("BrewPanel keeps direct and measured queue entry points",
            brewPanel.Contains("public void TryQueueIngredient(string itemId)") &&
            brewPanel.Contains("public bool TryQueueMeasuredIngredient(string itemId, int grams)") &&
            brewPanel.Contains("public bool TryQueueReservedIngredient(string itemId)") &&
            brewPanel.Contains("public bool TryQueueReservedMeasuredIngredient(string itemId, int grams)") &&
            brewPanel.Contains("TryQueueIngredientPortion(itemId, 0)") &&
            brewPanel.Contains("TryQueueIngredientPortion(itemId, grams)") &&
            brewPanel.Contains("TryQueueIngredientPortion(itemId, 0, consumeInventory: false)") &&
            brewPanel.Contains("TryQueueIngredientPortion(itemId, grams, consumeInventory: false)") &&
            brewPanel.Contains("if (consumeInventory && !_gameState.HasItem(itemId, 1))") &&
            brewPanel.Contains("if (consumeInventory && !_gameState.ConsumeItem(itemId, 1))"));
    }

    private static void TestMeasuredIngredientPersistenceWiring()
    {
        var brewPanel = ReadProjectFile("Scripts/UI/BrewPanel.cs");
        var gameState = ReadProjectFile("Scripts/Autoload/GameState.cs");
        var potionBatchStore = ReadProjectFile("Scripts/Systems/PotionBatchStore.cs");
        var saveData = ReadProjectFile("Scripts/Persistence/SaveData.cs");
        var dataDb = ReadProjectFile("Scripts/Autoload/DataDb.cs");
        var recipeDef = ReadProjectFile("Scripts/Models/PotionRecipeDef.cs");
        var customerDef = ReadProjectFile("Scripts/Models/CustomerInteractionDef.cs");
        var customerPanel = ReadProjectFile("Scripts/UI/CustomerPanel.cs");

        AssertTrue("BrewPanel stores queued ingredients as portions",
            brewPanel.Contains("private readonly List<IngredientPortionDef> _queuedIngredients = new();") &&
            brewPanel.Contains("FormatIngredientPortionLabel") &&
            brewPanel.Contains("RecordPotionRecipe(potionItemId, BuildIngredientIdList(_queuedIngredients))") &&
            brewPanel.Contains("RecordPotionBatch(potionItemId, _queuedIngredients)"));
        AssertTrue("GameState stores exact portion batches alongside legacy batches",
            potionBatchStore.Contains("_potionIngredientPortionBatches") &&
            gameState.Contains("RecordPotionBatch(string potionItemId, IReadOnlyList<IngredientPortionDef> ingredientPortions)") &&
            gameState.Contains("TryPeekPotionIngredientPortionBatch") &&
            potionBatchStore.Contains("RestoreUnmeasuredPortionBatchesFromLegacyBatches"));
        AssertTrue("Save data preserves exact portion batches without renaming legacy PotionBatches",
            saveData.Contains("Dictionary<string, List<List<string>>> PotionBatches") &&
            saveData.Contains("Dictionary<string, List<List<IngredientPortionDef>>> PotionIngredientPortionBatches"));
        AssertTrue("Authored data can parse exact recipe and customer gram requirements",
            recipeDef.Contains("IngredientAmounts") &&
            customerDef.Contains("RequiredIngredientAmounts") &&
            dataDb.Contains("ParseIngredientPortions(ReadArray(entry, \"ingredientAmounts\"))") &&
            dataDb.Contains("ParseIngredientPortions(ReadArray(entry, \"requiredIngredientAmounts\"))"));
        AssertTrue("CustomerPanel checks exact gram requirements against the potion batch",
            customerPanel.Contains("DoesPotionBatchSatisfyIngredientAmountRequirements") &&
            customerPanel.Contains("TryPeekPotionIngredientPortionBatch") &&
            customerPanel.Contains("portion.Grams == requiredIngredientAmount.Grams"));
    }

    private static void TestIngredientPreparationTrayPreviewWiring()
    {
        var tray = ReadProjectFile("Scripts/UI/IngredientPreparationTray.cs");
        var scene = ReadProjectFile("Scenes/UI/GameUi.tscn");

        AssertTrue("IngredientPreparationTray builds prep button columns with preview labels",
            tray.Contains("new VBoxContainer") &&
            tray.Contains("new RichTextLabel") &&
            tray.Contains("_preparationPreviewLabels") &&
            tray.Contains("column.AddChild(button);") &&
            tray.Contains("column.AddChild(preview);") &&
            tray.Contains("_preparationPreviewLabels[preparationId] = preview;"));
        AssertTrue("IngredientPreparationTray refreshes previews from selected ingredient preparation data",
            tray.Contains("ItemDef? selectedItem = null;") &&
            tray.Contains("RefreshPreparationPreviews(selectedItem);") &&
            tray.Contains("BuildPreparationPreviewText(item, option.Id)") &&
            tray.Contains("_gameState.KnowsIngredientPreparation(item.Id, preparationId)") &&
            tray.Contains("UnknownPreparationStatsLabel") &&
            tray.Contains("IngredientPreparationCatalog.TryGetPreparation(item, preparationId, out var preparation)"));
        AssertTrue("IngredientPreparationTray supports right-click inventory selection and selected-item return",
            tray.Contains("public bool TrySelectIngredientFromInventory(string itemId)") &&
            tray.Contains("_ingredientDropBox.GuiInput += _dropBoxGuiInputHandler") &&
            tray.Contains("OnIngredientDropBoxGuiInput") &&
            tray.Contains("rightMouseButton.ButtonIndex != MouseButton.Right") &&
            tray.Contains("ClearSelection();") &&
            tray.Contains("ReserveSelectedIngredient(itemId)") &&
            tray.Contains("ReturnSelectedIngredient()") &&
            tray.Contains("_gameState.AddItem(_selectedIngredientId, 1)"));
        AssertTrue("IngredientPreparationTray formats both trait and risk preview lines",
            tray.Contains("InventoryItemTextFormatter.DisplayStatName(trait.Key)") &&
            tray.Contains("InventoryItemTextFormatter.DisplayStatName(risk.Key)") &&
            tray.Contains("[color=#6ED775]{traitName} +{trait.Value}[/color]") &&
            tray.Contains("[color=#F0544F]{riskName} +{risk.Value}[/color]") &&
            !tray.Contains("Risk: {"));
        AssertTrue("Game UI reserves room for preparation preview labels under buttons",
            scene.Contains("custom_minimum_size = Vector2(430, 330)") &&
            scene.Contains("offset_bottom = 1117.0") &&
            scene.Contains("[node name=\"PreparationMethods\" type=\"HBoxContainer\" parent=\"PotionBrewingStationView/IngredientPreparationTray\"]"));
    }

    private static void TestPreparedIngredientsQueueDirectlyAndBrewPanelDiscards()
    {
        var tray = ReadProjectFile("Scripts/UI/IngredientPreparationTray.cs");
        var brewPanel = ReadProjectFile("Scripts/UI/BrewPanel.cs");

        AssertTrue("IngredientPreparationTray resolves BrewPanel for prepared output handoff",
            tray.Contains("BrewPanelPath = new(\"../BrewPanel\")") &&
            tray.Contains("GetNodeOrNull<BrewPanel>(BrewPanelPath)") &&
            tray.Contains("_brewPanel.TryQueueReservedIngredient(preparedIngredient.Id)") &&
            tray.Contains("SetStatus($\"{preparedIngredient.Name} added to brew.\")") &&
            !tray.Contains("_gameState.AddItem(preparedIngredient.Id, 1)"));
        AssertTrue("BrewPanel accepts already-reserved unmeasured ingredients",
            brewPanel.Contains("public bool TryQueueReservedIngredient(string itemId)") &&
            brewPanel.Contains("TryQueueIngredientPortion(itemId, 0, consumeInventory: false)") &&
            brewPanel.Contains("PlayQueuedIngredientDrop(itemId, GetRightClickDropStartPosition())"));
        AssertTrue("BrewPanel clear and slot removal discard queued ingredients instead of refunding inventory",
            !brewPanel.Contains("ReturnQueuedIngredients") &&
            !brewPanel.Contains("_gameState.AddItem(removedIngredientId, 1)") &&
            brewPanel.Contains("_queuedIngredients.Clear();") &&
            brewPanel.Contains("_queuedIngredients.RemoveAt(slotIndex);"));
        AssertTrue("Preparation trait knowledge is recorded only by successful brew paths",
            !tray.Contains("RecordIngredientPreparationKnowledge") &&
            brewPanel.Contains("_gameState.RecordIngredientPreparationKnowledge(_queuedIngredients);"));
    }

    private static void TestConsumableInventoryAndTreatmentTrayWiring()
    {
        var gameState = ReadProjectFile("Scripts/Autoload/GameState.cs");
        var inventoryState = ReadProjectFile("Scripts/Systems/InventoryState.cs");
        var stationShelf = ReadProjectFile("Scripts/UI/StationShelfInventory.cs");
        var hud = ReadProjectFile("Scripts/UI/Hud.cs");
        var hudScene = ReadProjectFile("Scenes/UI/Hud.tscn");
        var gameUiScene = ReadProjectFile("Scenes/UI/GameUi.tscn");
        var treatmentTray = ReadProjectFile("Scripts/UI/TreatmentTray.cs");
        var authoredItems = ReadProjectFile("Data/items_data.tres");

        AssertTrue("GameState caps consumables at four unique stacks",
            gameState.Contains("public const int MaxUniqueConsumableInventoryQuantity = 4") &&
            gameState.Contains("public const int MaxConsumableStackQuantity = 10") &&
            inventoryState.Contains("CountConsumableStacks() >= _maxUniqueConsumableInventoryQuantity"));
        AssertTrue("GameState records pending consumable grants when the section is full",
            gameState.Contains("PendingConsumableItemId") &&
            gameState.Contains("TryAcceptPendingConsumableByDiscarding") &&
            gameState.Contains("DeclinePendingConsumableGrant"));
        AssertTrue("Station shelf separates consumables from ingredients",
            stationShelf.Contains("BuildShelfStacks(includeIngredients: false)") &&
            stationShelf.Contains("_itemCatalog.IsConsumable(stack.Key)") &&
            stationShelf.Contains("_itemCatalog.IsIngredient(stack.Key)"));
        AssertTrue("Station shelf scene defines a four-slot consumables section",
            gameUiScene.Contains("ConsumableSlotsPath = NodePath(\"ConsumableSlots\")") &&
            gameUiScene.Contains("[node name=\"ConsumableSlots\" type=\"GridContainer\" parent=\"PotionBrewingStationView/StationShelfInventory\"]") &&
            gameUiScene.Contains("columns = 4"));
        AssertTrue("HUD omits the Treatment Tray button",
            !hud.Contains("TreatmentTrayPanelPath") &&
            !hud.Contains("OnTreatmentTrayPressed") &&
            !hudScene.Contains("[node name=\"TreatmentTray\" type=\"Button\" parent=\".\"]") &&
            !hudScene.Contains("text = \"Treatment Tray\""));
        AssertTrue("Game UI defines the Treatment Tray sprite drop target and inline actions",
            gameUiScene.Contains("[node name=\"TreatmentTray\" type=\"Control\" parent=\"PotionBrewingStationView\"]") &&
            !gameUiScene.Contains("[node name=\"TreatmentTray\" type=\"TextureRect\" parent=\"ShopFloor/Art\"]") &&
            !gameUiScene.Contains("[node name=\"TreatmentTray\" type=\"Button\" parent=\"ShopFloor/Hotspots\"]") &&
            gameUiScene.Contains("custom_minimum_size = Vector2(430, 286)") &&
            gameUiScene.Contains("TrayDropBoxPath = NodePath(\"TreatmentDropBox\")") &&
            gameUiScene.Contains("HelperLabelPath = NodePath(\"TreatmentHelper\")") &&
            gameUiScene.Contains("ApplyButtonPath = NodePath(\"Actions/Apply\")") &&
            gameUiScene.Contains("ClearButtonPath = NodePath(\"Actions/Clear\")") &&
            gameUiScene.Contains("[node name=\"Treatment\" type=\"Sprite2D\" parent=\"PotionBrewingStationView/TreatmentTray\"]") &&
            gameUiScene.Contains("[node name=\"TreatmentDropBox\" type=\"PanelContainer\" parent=\"PotionBrewingStationView/TreatmentTray\"]") &&
            gameUiScene.Contains("[node name=\"TreatmentHelper\" type=\"Label\" parent=\"PotionBrewingStationView/TreatmentTray\"]") &&
            gameUiScene.Contains("[node name=\"Actions\" type=\"HBoxContainer\" parent=\"PotionBrewingStationView/TreatmentTray\"]") &&
            gameUiScene.Contains("[node name=\"Apply\" type=\"Button\" parent=\"PotionBrewingStationView/TreatmentTray/Actions\"]") &&
            gameUiScene.Contains("[node name=\"Clear\" type=\"Button\" parent=\"PotionBrewingStationView/TreatmentTray/Actions\"]") &&
            gameUiScene.Contains("script = ExtResource(\"20_treatment_tray\")") &&
            !gameUiScene.Contains("PanelPath = NodePath(\"Panel\")") &&
            !gameUiScene.Contains("[node name=\"Panel\" type=\"PanelContainer\" parent=\"PotionBrewingStationView/TreatmentTray\"]") &&
            !gameUiScene.Contains("HotspotButtonPath") &&
            !gameUiScene.Contains("ConsumableDropBoxPath") &&
            !gameUiScene.Contains("TargetDropBoxPath") &&
            gameUiScene.Contains("text = \"Apply Treatment\""));
        AssertTrue("Treatment Tray removed panel item summaries",
            !gameUiScene.Contains("[node name=\"SelectionsRow\" type=\"HBoxContainer\" parent=\"PotionBrewingStationView/TreatmentTray/Panel/Margin/VBox\"]") &&
            !gameUiScene.Contains("[node name=\"ConsumableSelection\" type=\"PanelContainer\" parent=\"PotionBrewingStationView/TreatmentTray/Panel/Margin/VBox/SelectionsRow\"]") &&
            !gameUiScene.Contains("[node name=\"TargetSelection\" type=\"PanelContainer\" parent=\"PotionBrewingStationView/TreatmentTray/Panel/Margin/VBox/SelectionsRow\"]") &&
            !gameUiScene.Contains("ConsumableIconPath = NodePath(\"Panel/Margin/VBox/SelectionsRow/ConsumableSelection/Content/Icon\")") &&
            !gameUiScene.Contains("TargetIconPath = NodePath(\"Panel/Margin/VBox/SelectionsRow/TargetSelection/Content/Icon\")") &&
            !gameUiScene.Contains("StatusLabelPath = NodePath(\"Panel/Margin/VBox/Status\")"));
        AssertTrue("TreatmentTray accepts sprite drops without panel behavior",
            treatmentTray.Contains("TrayDropBoxPath") &&
            treatmentTray.Contains("_trayDropBox.ItemDropped += OnTrayItemDropped") &&
            treatmentTray.Contains("ClearStagedItems") &&
            treatmentTray.Contains("_applyButton.Visible = true") &&
            treatmentTray.Contains("_applyButton.Visible = false") &&
            treatmentTray.Contains("_clearButton.Visible = hasAnySelection") &&
            !treatmentTray.Contains("ShowPanel") &&
            !treatmentTray.Contains("HidePanel") &&
            !treatmentTray.Contains("PanelPath"));
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
        var potionBookPanel = ReadProjectFile("Scripts/UI/PotionBookPanel.cs");

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
        AssertTrue("Treated potions cannot be brewed from the potion book",
			potionBookPanel.Contains("potion.Treatment is not null"));
	}

    private static void TestBrewAndInventoryPriceWiring()
    {
        var brewPanel = ReadProjectFile("Scripts/UI/BrewPanel.cs");
        var gameUiScene = ReadProjectFile("Scenes/UI/GameUi.tscn");
        AssertTrue("BrewPanel calculates potion price from ingredient totals",
            brewPanel.Contains("CalculateIngredientTotalPrice(_queuedIngredients)"));
        AssertTrue("BrewPanel no longer renders the removed estimated value field",
            !brewPanel.Contains("\\u00A3{totalIngredientPrice}") &&
            !gameUiScene.Contains("[node name=\"EstimatedValue\" type=\"Control\" parent=\"PotionBrewingStationView/BrewPanel/Panel\"]"));
        AssertTrue("BrewPanel stores the potion base price in state",
            brewPanel.Contains("RegisterPotionBasePrice(potionItemId, potionBasePrice)"));
        AssertTrue("BrewPanel sums ingredient BasePrice values",
            brewPanel.Contains("totalPrice += Math.Max(0, item.BasePrice);"));

        var gameState = ReadProjectFile("Scripts/Autoload/GameState.cs");
        var potionInventoryBrewService = ReadProjectFile("Scripts/Systems/PotionInventoryBrewService.cs");
        AssertTrue("GameState exposes stored potion prices",
            gameState.Contains("RegisterPotionBasePrice") &&
            gameState.Contains("TryGetPotionBasePrice"));
        AssertTrue("Potion book repeat brews unlock the consumed preparation traits",
            potionInventoryBrewService.Contains("_gameState.RecordIngredientPreparationKnowledge(requiredIngredients.Keys);"));
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

    private static void TestRepeatBrewFailuresShowCursorToast()
    {
        var cursorToast = ReadProjectFile("Scripts/UI/CursorToast.cs");
        var potionBookPanel = ReadProjectFile("Scripts/UI/PotionBookPanel.cs");

        AssertTrue("CursorToast renders above the captured cursor position",
            cursorToast.Contains("viewport.GetMousePosition()") &&
            cursorToast.Contains("_cursorPosition.Y - toastSize.Y - CursorOffsetY"));
        AssertTrue("CursorToast lasts three seconds",
            cursorToast.Contains("DisplaySeconds = 3.0") &&
            cursorToast.Contains("WaitTime = DisplaySeconds"));
        AssertTrue("PotionBookPanel brew failure uses cursor toast",
            potionBookPanel.Contains("CursorToast.Show(this, error);") &&
            !potionBookPanel.Contains("GD.PushError(error);"));
    }
}
