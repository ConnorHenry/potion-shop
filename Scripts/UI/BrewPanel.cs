using Godot;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using OccultShop.Autoload;
using OccultShop.Models;
using OccultShop.Systems;

namespace OccultShop.UI;

public partial class BrewPanel : Control
{
	[Signal]
	public delegate void IngredientQueuedEventHandler(string itemId, int queuedCount);

	[Signal]
	public delegate void PotionBrewedEventHandler(string potionItemId);

	private const string DefaultPotionIconPath = "res://Assets/Items/sight_tonic.svg";
	private const string PotionIconsDirectoryPath = "res://Assets/Potions";
	private const int BrewedPotionOutputQuantity = 1;
	private const string HerbTypeTag = ItemTags.Herb;
	private const string LiquidTypeTag = ItemTags.Liquid;
	private const string CatalystTypeTag = ItemTags.Catalyst;
	private const int DropAnimationLayerIndex = 2048;
	private const float IngredientDropIconSize = 70.0f;
	private const float IngredientDropDurationSeconds = 0.32f;
	private const float IngredientDropTargetBrewBoxHeightRatio = 0.68f;
	private const float RightClickDropStartOffset = 120.0f;
	private const float MinimumCursorDropDistance = 140.0f;
	private const int PreviewTraitLineCount = 4;
	private const int PreviewRiskLineCount = 4;

	[Export] public NodePath BrewBoxPath = default!;
	[Export] public NodePath IngredientSlotOnePath = default!;
	[Export] public NodePath IngredientSlotTwoPath = default!;
	[Export] public NodePath IngredientSlotThreePath = default!;
	[Export] public NodePath IngredientSlotOneContainerPath = default!;
	[Export] public NodePath IngredientSlotTwoContainerPath = default!;
	[Export] public NodePath IngredientSlotThreeContainerPath = default!;
	[Export] public NodePath IngredientSlotOneLabelPath = default!;
	[Export] public NodePath IngredientSlotTwoLabelPath = default!;
	[Export] public NodePath IngredientSlotThreeLabelPath = default!;
	[Export] public NodePath RequestChecklistLabelPath = default!;
	[Export] public NodePath PreviewTraitsLabelPath = default!;
	[Export] public NodePath PreviewRisksLabelPath = default!;
	[Export] public NodePath BrewButtonPath = default!;
	[Export] public NodePath ClearButtonPath = default!;
	[Export] public NodePath RuntimeContentDbPath = new(AutoloadNodePaths.RuntimeContentDb);
	[Export] public NodePath DataDbPath = new(AutoloadNodePaths.DataDb);
	[Export] public NodePath GameStatePath = new(AutoloadNodePaths.GameState);
	[Export] public NodePath ItemCatalogPath = new(AutoloadNodePaths.ItemCatalog);
	[Export] public NodePath CauldronSmokeEffectPath = new("../CauldronSmoke");
	[Export] public NodePath FireGlowEffectPath = new("../FireGlow");

	private Button _closeButton = default!;
	private BrewDropBox _brewBox = default!;
	private CanvasLayer _dropAnimationLayer = default!;
	private TextureRect _ingredientSlotOne = default!;
	private TextureRect _ingredientSlotTwo = default!;
	private TextureRect _ingredientSlotThree = default!;
	private PanelContainer _ingredientSlotOneContainer = default!;
	private PanelContainer _ingredientSlotTwoContainer = default!;
	private PanelContainer _ingredientSlotThreeContainer = default!;
	private Label _ingredientSlotOneLabel = default!;
	private Label _ingredientSlotTwoLabel = default!;
	private Label _ingredientSlotThreeLabel = default!;
	private RichTextLabel _requestChecklistLabel = default!;
	private Label _previewTraitsLabel = default!;
	private Label _previewRisksLabel = default!;
	private Button _brewButton = default!;
	private Button _clearButton = default!;
	private RuntimeContentDb _runtimeContentDb = default!;
	private DataDb _dataDb = default!;
	private GameState _gameState = default!;
	private ItemCatalogService _itemCatalog = default!;
	private CauldronSmokeEffect? _cauldronSmokeEffect;
	private BrewingStationFireGlow? _fireGlowEffect;
	private readonly List<IngredientPortionDef> _queuedIngredients = new();
	private readonly PotionRecipeLookup _predefinedPotionRecipes = new();
	private readonly PotionBrewingService _brewingService = new();
	private PotionInventoryBrewService _inventoryBrewService = default!;
	private string _previewPotionCombinationKey = string.Empty;
	private string _previewPotionName = string.Empty;
	private Control.GuiInputEventHandler? _slotOneGuiInputHandler;
	private Control.GuiInputEventHandler? _slotTwoGuiInputHandler;
	private Control.GuiInputEventHandler? _slotThreeGuiInputHandler;
	private int _draggingSlotIndex = -1;
	private Vector2 _dragStartGlobalPosition = Vector2.Zero;
	private bool _slotDragThresholdReached;
	private readonly List<IngredientDropAnimation> _dropAnimations = new();

	public override void _Ready()
	{
		var runtimeContentDb = GetNodeOrNull<RuntimeContentDb>(RuntimeContentDbPath);
		if (runtimeContentDb is null)
		{
			GD.PushError($"BrewPanel: RuntimeContentDb was not found at '{RuntimeContentDbPath}'.");
			return;
		}

		var dataDb = GetNodeOrNull<DataDb>(DataDbPath);
		if (dataDb is null)
		{
			GD.PushError($"BrewPanel: DataDb was not found at '{DataDbPath}'.");
			return;
		}

		var gameState = GetNodeOrNull<GameState>(GameStatePath);
		if (gameState is null)
		{
			GD.PushError($"BrewPanel: GameState was not found at '{GameStatePath}'.");
			return;
		}

		var itemCatalog = GetNodeOrNull<ItemCatalogService>(ItemCatalogPath);
		if (itemCatalog is null)
		{
			GD.PushError($"BrewPanel: ItemCatalog was not found at '{ItemCatalogPath}'.");
			return;
		}

		_runtimeContentDb = runtimeContentDb;
		_dataDb = dataDb;
		_gameState = gameState;
		_itemCatalog = itemCatalog;
		_inventoryBrewService = new PotionInventoryBrewService(_gameState, _itemCatalog);
		RebuildPredefinedPotionRecipeLookup();

		_brewBox = GetNode<BrewDropBox>(BrewBoxPath);
		_dropAnimationLayer = CreateDropAnimationLayer();
		GetTree().Root.AddChild(_dropAnimationLayer);
		_ingredientSlotOne = GetNode<TextureRect>(IngredientSlotOnePath);
		_ingredientSlotTwo = GetNode<TextureRect>(IngredientSlotTwoPath);
		_ingredientSlotThree = GetNode<TextureRect>(IngredientSlotThreePath);
		_ingredientSlotOneContainer = GetNode<PanelContainer>(IngredientSlotOneContainerPath);
		_ingredientSlotTwoContainer = GetNode<PanelContainer>(IngredientSlotTwoContainerPath);
		_ingredientSlotThreeContainer = GetNode<PanelContainer>(IngredientSlotThreeContainerPath);
		_ingredientSlotOneLabel = GetNode<Label>(IngredientSlotOneLabelPath);
		_ingredientSlotTwoLabel = GetNode<Label>(IngredientSlotTwoLabelPath);
		_ingredientSlotThreeLabel = GetNode<Label>(IngredientSlotThreeLabelPath);
		_requestChecklistLabel = GetNode<RichTextLabel>(RequestChecklistLabelPath);
		_previewTraitsLabel = GetNode<Label>(PreviewTraitsLabelPath);
		_previewRisksLabel = GetNode<Label>(PreviewRisksLabelPath);
		_brewButton = GetNode<Button>(BrewButtonPath);
		_clearButton = GetNode<Button>(ClearButtonPath);
		_cauldronSmokeEffect = GetNodeOrNull<CauldronSmokeEffect>(CauldronSmokeEffectPath);
		if (_cauldronSmokeEffect is null)
			GD.PushError($"BrewPanel: CauldronSmokeEffect was not found at '{CauldronSmokeEffectPath}'.");
		_fireGlowEffect = GetNodeOrNull<BrewingStationFireGlow>(FireGlowEffectPath);
		if (_fireGlowEffect is null)
			GD.PushError($"BrewPanel: BrewingStationFireGlow was not found at '{FireGlowEffectPath}'.");
		_requestChecklistLabel.BbcodeEnabled = true;

		SetInteractiveCursor(_ingredientSlotOneContainer);
		SetInteractiveCursor(_ingredientSlotTwoContainer);
		SetInteractiveCursor(_ingredientSlotThreeContainer);

		MouseFilter = MouseFilterEnum.Ignore;
		_brewBox.ItemDroppedAt += TryQueueDroppedIngredient;
		_brewButton.Pressed += TryBrew;
		_clearButton.Pressed += ClearQueue;
		_gameState.Changed += OnGameStateChanged;
		_slotOneGuiInputHandler = @event => HandleIngredientSlotGuiInput(0, @event);
		_slotTwoGuiInputHandler = @event => HandleIngredientSlotGuiInput(1, @event);
		_slotThreeGuiInputHandler = @event => HandleIngredientSlotGuiInput(2, @event);
		_ingredientSlotOneContainer.GuiInput += _slotOneGuiInputHandler;
		_ingredientSlotTwoContainer.GuiInput += _slotTwoGuiInputHandler;
		_ingredientSlotThreeContainer.GuiInput += _slotThreeGuiInputHandler;
		Visible = false;
		RefreshIngredientIcons();
		SetProcess(false);
	}

	public override void _ExitTree()
	{
		if (_brewBox is not null)
			_brewBox.ItemDroppedAt -= TryQueueDroppedIngredient;
		if (_brewButton is not null)
			_brewButton.Pressed -= TryBrew;
		if (_clearButton is not null)
			_clearButton.Pressed -= ClearQueue;
		if (_gameState is not null)
			_gameState.Changed -= OnGameStateChanged;
		if (_slotOneGuiInputHandler is not null)
			_ingredientSlotOneContainer.GuiInput -= _slotOneGuiInputHandler;
		if (_slotTwoGuiInputHandler is not null)
			_ingredientSlotTwoContainer.GuiInput -= _slotTwoGuiInputHandler;
		if (_slotThreeGuiInputHandler is not null)
			_ingredientSlotThreeContainer.GuiInput -= _slotThreeGuiInputHandler;

		ClearDropAnimations();
		if (_dropAnimationLayer is not null && GodotObject.IsInstanceValid(_dropAnimationLayer))
			_dropAnimationLayer.QueueFree();
	}

	public void Toggle()
	{
		if (Visible)
		{
			HidePanel();
			return;
		}

		ShowPanel();
	}

	public void ShowPanel()
	{
		Visible = true;
		MoveToFront();
		RefreshIngredientIcons();
	}

	public Button? GetBrewButton()
	{
		return _brewButton;
	}

	public void HidePanel()
	{
		ResetSlotDragState();
		ClearDropAnimations();
		Visible = false;
		RefreshIngredientIcons();
	}

	public override void _Input(InputEvent @event)
	{
		if (!Visible)
			return;

		if (_draggingSlotIndex < 0)
			return;

		if (@event is InputEventMouseMotion mouseMotion)
		{
			if (_slotDragThresholdReached)
				return;

			const float dragThresholdPixels = 8.0f;
			if (mouseMotion.GlobalPosition.DistanceTo(_dragStartGlobalPosition) >= dragThresholdPixels)
				_slotDragThresholdReached = true;

			return;
		}

		if (@event is not InputEventMouseButton mouseButton)
			return;

		if (mouseButton.ButtonIndex != MouseButton.Left || mouseButton.Pressed)
			return;

		if (_slotDragThresholdReached && !IsPointInsideAnyIngredientSlot(mouseButton.GlobalPosition))
			RemoveQueuedIngredientAt(_draggingSlotIndex);

		ResetSlotDragState();
	}

	public override void _Process(double delta)
	{
		if (_dropAnimations.Count == 0)
		{
			SetProcess(false);
			return;
		}

		for (var i = _dropAnimations.Count - 1; i >= 0; i--)
		{
			var animation = _dropAnimations[i];
			animation.ElapsedSeconds += delta;

			var progress = Mathf.Clamp((float)(animation.ElapsedSeconds / IngredientDropDurationSeconds), 0.0f, 1.0f);
			var easedProgress = progress * progress;
			animation.Icon.Position = animation.StartTopLeft.Lerp(animation.EndTopLeft, easedProgress);

			if (progress < 1.0f)
				continue;

			animation.Icon.QueueFree();
			_dropAnimations.RemoveAt(i);
			PlayCauldronSmokeBurst();
		}

		if (_dropAnimations.Count == 0)
			SetProcess(false);
	}

	public void TryQueueIngredient(string itemId)
	{
		if (TryQueueIngredientPortion(itemId, 0))
			PlayQueuedIngredientDrop(itemId, GetRightClickDropStartPosition());
	}

	private void TryQueueDroppedIngredient(string itemId, Vector2 dropGlobalPosition)
	{
		if (TryQueueIngredientPortion(itemId, 0))
			PlayQueuedIngredientDrop(itemId, dropGlobalPosition);
	}

	public bool TryQueueMeasuredIngredient(string itemId, int grams)
	{
		if (grams <= 0)
		{
			ShowBrewFeedback("Measured ingredients need at least 1g.");
			return false;
		}

		return TryQueueIngredientPortion(itemId, grams);
	}

	public bool TryQueueReservedIngredient(string itemId)
	{
		if (!TryQueueIngredientPortion(itemId, 0, consumeInventory: false))
			return false;

		PlayQueuedIngredientDrop(itemId, GetRightClickDropStartPosition());
		return true;
	}

	public bool TryQueueReservedMeasuredIngredient(string itemId, int grams)
	{
		if (grams <= 0)
		{
			ShowBrewFeedback("Measured ingredients need at least 1g.");
			return false;
		}

		return TryQueueIngredientPortion(itemId, grams, consumeInventory: false);
	}

	private void PlayQueuedIngredientDrop(string itemId, Vector2 startCenterGlobalPosition)
	{
		if (!_itemCatalog.TryGetItem(itemId, out var item))
			return;

		if (string.IsNullOrWhiteSpace(item.IconPath))
			return;

		var endCenterGlobalPosition = GetDropEndPosition(startCenterGlobalPosition);
		if (InventoryDragPreview.TryPlayBrewDropAnimation(
			item.IconPath,
			startCenterGlobalPosition,
			endCenterGlobalPosition,
			PlayCauldronSmokeBurst))
			return;

		var texture = UiIconLoader.LoadIcon(item.IconPath);
		if (texture is null)
			return;

		var iconSize = new Vector2(IngredientDropIconSize, IngredientDropIconSize);
		var icon = new TextureRect
		{
			Name = "BrewIngredientDrop",
			Texture = texture,
			CustomMinimumSize = iconSize,
			Size = iconSize,
			MouseFilter = MouseFilterEnum.Ignore,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			ZIndex = 4096
		};

		_dropAnimationLayer.AddChild(icon);

		var halfSize = iconSize * 0.5f;
		var animation = new IngredientDropAnimation
		{
			Icon = icon,
			StartTopLeft = startCenterGlobalPosition - halfSize,
			EndTopLeft = endCenterGlobalPosition - halfSize
		};

		icon.Position = animation.StartTopLeft;
		_dropAnimations.Add(animation);
		SetProcess(true);
	}

	private void PlayCauldronReactionBurst()
	{
		PlayCauldronSmokeBurst();

		if (_fireGlowEffect is not null && GodotObject.IsInstanceValid(_fireGlowEffect))
			_fireGlowEffect.PlayIgnitionBurst();
	}

	private void PlayCauldronSmokeBurst()
	{
		if (_cauldronSmokeEffect is not null && GodotObject.IsInstanceValid(_cauldronSmokeEffect))
			_cauldronSmokeEffect.PlayRandomBurst();
	}

	private static CanvasLayer CreateDropAnimationLayer()
	{
		var layer = new CanvasLayer
		{
			Name = "BrewDropAnimationLayer",
			Layer = DropAnimationLayerIndex
		};
		return layer;
	}

	private Vector2 GetRightClickDropStartPosition()
	{
		var brewBoxRect = _brewBox.GetGlobalRect();
		return new Vector2(
			brewBoxRect.Position.X + (brewBoxRect.Size.X * 0.5f),
			brewBoxRect.Position.Y - RightClickDropStartOffset);
	}

	private Vector2 GetDropEndPosition(Vector2 startCenterGlobalPosition)
	{
		var brewBoxRect = _brewBox.GetGlobalRect();
		var brewBoxTargetY = brewBoxRect.Position.Y + (brewBoxRect.Size.Y * IngredientDropTargetBrewBoxHeightRatio);
		var targetY = Mathf.Max(startCenterGlobalPosition.Y + MinimumCursorDropDistance, brewBoxTargetY);
		return new Vector2(startCenterGlobalPosition.X, targetY);
	}

	private bool TryQueueIngredientPortion(string itemId, int grams)
	{
		return TryQueueIngredientPortion(itemId, grams, consumeInventory: true);
	}

	private bool TryQueueIngredientPortion(string itemId, int grams, bool consumeInventory)
	{
		if (!_itemCatalog.TryGetItem(itemId, out var item))
		{
			ShowBrewFeedback("That item is not recognized.");
			return false;
		}

		if (!IsIngredient(item))
		{
			ShowBrewFeedback(IsPotion(itemId)
				? "Brewing only accepts ingredients, not potions."
				: "Brewing only accepts ingredients.");
			return false;
		}

		if (!TryBuildQueuedIngredientPortion(itemId, item, grams, out var queuedIngredient, out var queueError))
		{
			ShowBrewFeedback(queueError);
			return false;
		}

		if (_queuedIngredients.Any(x => string.Equals(x.IngredientId, queuedIngredient.IngredientId, System.StringComparison.OrdinalIgnoreCase)))
		{
			ShowBrewFeedback("Each ingredient can only be used once per potion.");
			return false;
		}

		var queuedWithCandidate = CloneQueuedIngredients(_queuedIngredients);
		queuedWithCandidate.Add(queuedIngredient.Clone());

		var followsPredefinedRecipe = _predefinedPotionRecipes.MatchesAnyRecipePrefix(queuedWithCandidate);
		if (!followsPredefinedRecipe)
		{
			if (!TryGetIngredientType(item, out _))
			{
				ShowBrewFeedback("Ingredient type is missing.");
				return false;
			}
		}

		if (_queuedIngredients.Count >= 3)
		{
			ShowBrewFeedback("Brewing requires exactly 3 ingredients.");
			return false;
		}

		if (consumeInventory && !_gameState.HasItem(itemId, 1))
		{
			ShowBrewFeedback("Not enough stock for that ingredient.");
			return false;
		}

		if (consumeInventory && !_gameState.ConsumeItem(itemId, 1))
		{
			ShowBrewFeedback("Could not take that ingredient.");
			return false;
		}

		_queuedIngredients.Add(queuedIngredient);
		RefreshIngredientIcons();
		EmitSignal(SignalName.IngredientQueued, itemId, _queuedIngredients.Count);
		return true;
	}

	private bool TryBuildQueuedIngredientPortion(
		string itemId,
		ItemDef item,
		int grams,
		out IngredientPortionDef ingredientPortion,
		out string error)
	{
		ingredientPortion = default!;
		error = string.Empty;
		if (!IngredientPreparationCatalog.TryGetPreparedIngredientInfo(item, out var baseIngredientId, out var preparationId))
		{
			error = "Prepare ingredients before brewing. Use Raw for recipes that call for raw ingredients.";
			return false;
		}

		ingredientPortion = new IngredientPortionDef
		{
			IngredientId = baseIngredientId,
			ItemId = itemId,
			PreparationId = preparationId,
			Grams = Math.Max(0, grams)
		};
		return true;
	}

	private void ClearQueue()
	{
		ResetSlotDragState();
		ClearDropAnimations();
		_queuedIngredients.Clear();
		RefreshIngredientIcons();
	}

	private void OnGameStateChanged()
	{
		RefreshBrewPreview();
	}

	private void TryBrew()
	{
		if (_queuedIngredients.Count != 3)
		{
			ShowBrewFeedback("Brewing requires exactly 3 ingredients.");
			return;
		}

		var combinationKey = PotionRecipeLookup.BuildCombinationKey(_queuedIngredients);
		var hasPredefinedRecipe = _predefinedPotionRecipes.TryGetRecipe(_queuedIngredients, out var predefinedRecipe);

		if (!TryBuildIngredientDefs(_queuedIngredients, out var ingredientDefs, out var ingredientError))
		{
			ShowBrewFeedback(ingredientError);
			return;
		}

		var brewResult = _brewingService.BrewPotion(
			ingredientDefs,
			null);

		var totalIngredientPrice = CalculateIngredientTotalPrice(_queuedIngredients);
		var potionBasePrice = Math.Max(0, totalIngredientPrice - brewResult.RiskIngredientPricePenalty);
		var brewCost = BrewPricing.CalculateBrewCost(totalIngredientPrice, brewResult);
		if (_gameState.Gold < brewCost)
		{
			ShowBrewFeedback($"Need {brewCost} gold to brew this potion.");
			return;
		}

		var potionDisplayName = GetPreviewPotionName(combinationKey);
		if (hasPredefinedRecipe)
			potionDisplayName = predefinedRecipe.Name;
		var isNewCombination = !_gameState.TryGetPotionForCombination(combinationKey, out var potionItemId);
		var iconPath = string.Empty;
		var potionTraits = BuildPotionTraitsForRegistration(brewResult, hasPredefinedRecipe ? predefinedRecipe : null);
		if (isNewCombination)
		{
			potionItemId = hasPredefinedRecipe
				? PotionVariantIdBuilder.BuildPredefinedPotionItemId(predefinedRecipe.Id)
				: $"brew_{_gameState.PotionDisplayNames.Count + 1}";
			iconPath = ResolvePotionIconPath();

			_runtimeContentDb.RegisterRuntimePotionItem(
				potionItemId,
				potionDisplayName,
				iconPath,
				potionBasePrice,
				brewResult.IngredientQualityScore,
				potionTraits,
				new Dictionary<string, int>(brewResult.Risks));

			_gameState.SetPotionForCombination(combinationKey, potionItemId);
			_gameState.SetPotionDisplayName(potionItemId, potionDisplayName);
		}
		else
		{
			if (!_itemCatalog.TryGetItem(potionItemId, out var basePotionItem))
			{
				ShowBrewFeedback("Known potion recipe is missing from the item catalog.");
				GD.PushError($"BrewPanel: Known potion item '{potionItemId}' is missing from the item catalog.");
				return;
			}

			iconPath = string.IsNullOrWhiteSpace(basePotionItem.IconPath)
				? ResolvePotionIconPath()
				: basePotionItem.IconPath;

			if (!PotionVariantIdBuilder.RisksMatch(basePotionItem.Risks, brewResult.Risks))
			{
				var variantPotionItemId = PotionVariantIdBuilder.BuildRiskVariantItemId(potionItemId, brewResult.Risks);
				if (!_itemCatalog.TryGetItem(variantPotionItemId, out _))
				{
					_runtimeContentDb.RegisterRuntimePotionItem(
						variantPotionItemId,
						potionDisplayName,
						iconPath,
						potionBasePrice,
						brewResult.IngredientQualityScore,
						potionTraits,
						new Dictionary<string, int>(brewResult.Risks));
				}

				potionItemId = variantPotionItemId;
			}

			_gameState.SetPotionDisplayName(potionItemId, potionDisplayName);
		}

		if (!_inventoryBrewService.CanAddPotion(potionItemId, BrewedPotionOutputQuantity))
		{
			ShowBrewFeedback(PotionInventoryBrewService.PotionInventoryFullMessage);
			return;
		}

		_gameState.AddGold(-brewCost);

		_gameState.RegisterPotionBasePrice(potionItemId, potionBasePrice);
		_runtimeContentDb.TrySetRuntimeItemBasePrice(potionItemId, potionBasePrice);

		_gameState.RecordPotionRecipe(potionItemId, BuildIngredientIdList(_queuedIngredients));
		_gameState.RecordIngredientPreparationKnowledge(_queuedIngredients);
		_gameState.AddItem(potionItemId, BrewedPotionOutputQuantity);
		_gameState.RecordPotionBatch(potionItemId, _queuedIngredients);
		EmitSignal(SignalName.PotionBrewed, potionItemId);
		PlayCauldronReactionBurst();
		_queuedIngredients.Clear();
		ResetSlotDragState();
		RefreshIngredientIcons();
		RefreshBrewTraitRiskPreview(brewResult, showCarriedRisks: true);
		ShowBrewFeedback(BrewPanelTextFormatter.BuildBrewResultToastText(
			PotionDisplayName(potionItemId, DefaultItemName(potionItemId)),
			brewResult));
	}

	private void HandleIngredientSlotGuiInput(int slotIndex, InputEvent @event)
	{
		if (slotIndex < 0 || slotIndex >= _queuedIngredients.Count)
			return;

		if (@event is InputEventMouseButton rightMouseButton &&
			rightMouseButton.ButtonIndex == MouseButton.Right &&
			rightMouseButton.Pressed)
		{
			RemoveQueuedIngredientAt(slotIndex);
			AcceptEvent();
			return;
		}

		if (@event is InputEventMouseButton leftMouseButton &&
			leftMouseButton.ButtonIndex == MouseButton.Left)
		{
			if (leftMouseButton.Pressed)
			{
				_draggingSlotIndex = slotIndex;
				_dragStartGlobalPosition = leftMouseButton.GlobalPosition;
				_slotDragThresholdReached = false;
			}
			else
			{
				ResetSlotDragState();
			}

			AcceptEvent();
		}
	}

	private void RemoveQueuedIngredientAt(int slotIndex)
	{
		if (slotIndex < 0 || slotIndex >= _queuedIngredients.Count)
			return;

		_queuedIngredients.RemoveAt(slotIndex);
		RefreshIngredientIcons();
	}

	private bool IsPointInsideAnyIngredientSlot(Vector2 globalPosition)
	{
		return IsPointInsideSlot(_ingredientSlotOneContainer, globalPosition)
			|| IsPointInsideSlot(_ingredientSlotTwoContainer, globalPosition)
			|| IsPointInsideSlot(_ingredientSlotThreeContainer, globalPosition);
	}

	private static bool IsPointInsideSlot(Control slot, Vector2 globalPosition)
	{
		return new Rect2(slot.GlobalPosition, slot.Size).HasPoint(globalPosition);
	}

	private void ResetSlotDragState()
	{
		_draggingSlotIndex = -1;
		_dragStartGlobalPosition = Vector2.Zero;
		_slotDragThresholdReached = false;
	}

	private void RefreshIngredientIcons()
	{
		var slots = new[] { _ingredientSlotOne, _ingredientSlotTwo, _ingredientSlotThree };
		var labels = new[] { _ingredientSlotOneLabel, _ingredientSlotTwoLabel, _ingredientSlotThreeLabel };

		for (var i = 0; i < slots.Length; i++)
		{
			if (i >= _queuedIngredients.Count)
			{
				slots[i].Texture = null;
				labels[i].Text = string.Empty;
				continue;
			}

			var ingredient = _queuedIngredients[i];
			var ingredientId = ingredient.InventoryItemId;
			if (!_itemCatalog.TryGetItem(ingredientId, out var item))
			{
				slots[i].Texture = null;
				labels[i].Text = string.Empty;
				continue;
			}

			slots[i].Texture = UiIconLoader.LoadIcon(item.IconPath);
			labels[i].Text = FormatIngredientPortionLabel(ingredient);
		}

		RefreshBrewPreview();
	}

	private void RefreshBrewPreview()
	{
		var ingredientCount = _queuedIngredients.Count;

		if (ingredientCount == 0)
		{
			RefreshRequestChecklist(null);
			RefreshBrewTraitRiskPreview(null, showCarriedRisks: false);
			return;
		}

		if (!TryBuildIngredientDefs(_queuedIngredients, out var ingredientDefs, out _, knownStatsOnly: true))
		{
			RefreshRequestChecklist(null);
			RefreshBrewTraitRiskPreview(null, showCarriedRisks: false);
			return;
		}

		var previewResult = _brewingService.PreviewPotion(
			ingredientDefs,
			null);
		RefreshRequestChecklist(previewResult);
		RefreshBrewTraitRiskPreview(previewResult, showCarriedRisks: false);
	}

	private void RefreshRequestChecklist(PotionResult? previewResult)
	{
		_requestChecklistLabel.Text = CustomerDialogueTextFormatter.BuildBrewingRequestChecklistText(
			_gameState.ActiveCustomerRequest,
			previewResult?.Traits,
			previewResult?.PossibleRisks,
			_queuedIngredients);
	}

	private void RefreshBrewTraitRiskPreview(PotionResult? previewResult, bool showCarriedRisks)
	{
		_previewTraitsLabel.Text = previewResult is null
			? BrewPanelTextFormatter.BuildStatListText(new Dictionary<string, int>(), PreviewTraitLineCount)
			: BrewPanelTextFormatter.BuildStatListText(previewResult.Traits, PreviewTraitLineCount);

		_previewRisksLabel.Text = previewResult is null || !showCarriedRisks
			? BrewPanelTextFormatter.BuildCarriedRiskListText(new Dictionary<string, int>(), PreviewRiskLineCount)
			: BrewPanelTextFormatter.BuildCarriedRiskListText(previewResult.Risks, PreviewRiskLineCount);
	}

	private string GetPreviewPotionName(string combinationKey)
	{
		if (string.IsNullOrWhiteSpace(combinationKey))
			return string.Empty;

		if (_previewPotionCombinationKey == combinationKey && !string.IsNullOrWhiteSpace(_previewPotionName))
			return _previewPotionName;

		if (_gameState.TryGetPotionForCombination(combinationKey, out var potionItemId))
		{
			_previewPotionCombinationKey = combinationKey;
			_previewPotionName = PotionDisplayName(potionItemId, DefaultItemName(potionItemId));
			return _previewPotionName;
		}

		if (_predefinedPotionRecipes.TryGetRecipe(combinationKey, out var predefinedRecipe))
		{
			_previewPotionCombinationKey = combinationKey;
			_previewPotionName = predefinedRecipe.Name;
			return _previewPotionName;
		}

		_previewPotionCombinationKey = combinationKey;
		_previewPotionName = GeneratePotionName();
		return _previewPotionName;
	}

	private void RebuildPredefinedPotionRecipeLookup()
	{
		_predefinedPotionRecipes.Rebuild(
			_dataDb.PotionRecipes,
			IsKnownIngredient,
			error => GD.PushError($"BrewPanel: {error}"));
	}

	private bool IsKnownIngredient(string ingredientId)
	{
		return _itemCatalog.TryGetItem(ingredientId, out var ingredientItem) && IsIngredient(ingredientItem);
	}

	private static Dictionary<string, int> BuildPotionTraitsForRegistration(PotionResult brewResult, PotionRecipeDef? predefinedRecipe)
	{
		if (predefinedRecipe is null || predefinedRecipe.Traits is null || predefinedRecipe.Traits.Count == 0)
			return new Dictionary<string, int>(brewResult.Traits);

		var traits = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
		foreach (var trait in predefinedRecipe.Traits)
		{
			if (string.IsNullOrWhiteSpace(trait))
				continue;
			if (!brewResult.Traits.TryGetValue(trait, out var strength))
				continue;

			traits[trait] = strength;
		}

		return traits.Count > 0
			? traits
			: new Dictionary<string, int>(brewResult.Traits);
	}

	private static void SetInteractiveCursor(Control control)
	{
		control.MouseDefaultCursorShape = CursorShape.PointingHand;
	}

	private void ShowBrewFeedback(string message)
	{
		CursorToast.Show(this, message);
	}

	private string ItemName(string itemId)
	{
		return PotionDisplayName(itemId, DefaultItemName(itemId));
	}

	private string FormatIngredientPortionLabel(IngredientPortionDef ingredient)
	{
		var itemName = ItemName(ingredient.InventoryItemId);
		return ingredient.Grams > 0
			? $"{itemName} ({ingredient.Grams}g)"
			: itemName;
	}

	private string PotionDisplayName(string itemId, string fallbackName)
	{
		if (IsPotion(itemId))
		{
			var customName = _gameState.GetPotionDisplayName(itemId);
			if (!string.IsNullOrWhiteSpace(customName))
				return customName;
		}

		return fallbackName;
	}

	private string DefaultItemName(string itemId)
	{
		return _itemCatalog.GetItemName(itemId);
	}

	private bool IsPotion(string itemId)
	{
		return _itemCatalog.IsPotion(itemId);
	}

	private static bool IsIngredient(ItemDef item)
	{
		return ItemCatalogService.HasTag(item, ItemTags.Ingredient);
	}

	private bool HasQueuedIngredientType(string ingredientType)
	{
		foreach (var queuedIngredient in _queuedIngredients)
		{
			var queuedItemId = queuedIngredient.InventoryItemId;
			if (!_itemCatalog.TryGetItem(queuedItemId, out var queuedItem))
				continue;

			if (!TryGetIngredientType(queuedItem, out var queuedType))
				continue;

			if (string.Equals(queuedType, ingredientType, System.StringComparison.OrdinalIgnoreCase))
				return true;
		}

		return false;
	}

	private bool HasRequiredIngredientTypes(out string error)
	{
		var requiredTypes = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase)
		{
			[HerbTypeTag] = 0,
			[LiquidTypeTag] = 0,
			[CatalystTypeTag] = 0
		};

		foreach (var queuedIngredient in _queuedIngredients)
		{
			var queuedItemId = queuedIngredient.InventoryItemId;
			if (!_itemCatalog.TryGetItem(queuedItemId, out var queuedItem))
			{
				error = $"Unknown ingredient: {queuedItemId}";
				return false;
			}

			if (!TryGetIngredientType(queuedItem, out var queuedType))
			{
				error = "Ingredient type is missing. Need one herb, one liquid, and one catalyst.";
				return false;
			}

			requiredTypes[queuedType] += 1;
		}

		if (requiredTypes[HerbTypeTag] != 1 || requiredTypes[LiquidTypeTag] != 1 || requiredTypes[CatalystTypeTag] != 1)
		{
			error = "Brewing requires one herb, one liquid, and one catalyst.";
			return false;
		}

		error = string.Empty;
		return true;
	}

	private static bool TryGetIngredientType(ItemDef item, out string ingredientType)
	{
		ingredientType = string.Empty;

		if (ItemCatalogService.HasTag(item, HerbTypeTag))
		{
			ingredientType = HerbTypeTag;
			return true;
		}

		if (ItemCatalogService.HasTag(item, LiquidTypeTag))
		{
			ingredientType = LiquidTypeTag;
			return true;
		}

		if (ItemCatalogService.HasTag(item, CatalystTypeTag))
		{
			ingredientType = CatalystTypeTag;
			return true;
		}

		return false;
	}

	private string GeneratePotionName()
	{
		var prefixes = new[]
		{
			"Moon", "Velvet", "Ashen", "Gilded", "Silent", "Waking", "Hollow", "Duskwind", "Ivory", "Sable",
			"Grave", "Blood", "Fever", "Widow", "Saint", "Witch", "Mournful", "Whispering", "Buried", "Forgotten",
			"Crimson", "Silver", "Pale", "Black", "Honeyed", "Thorn", "Lantern", "Raven", "Serpent", "Spider",
			"Cursed", "Hallowed", "Profane", "Restless", "Lucid", "Delirious", "Withered", "Blooming", "Frozen", "Burning",
			"Marrow", "Salt", "Iron", "Mercury", "Obsidian", "Amber", "Violet", "Opal", "Spectral", "Haunted"
		};

		var suffixes = new[]
		{
			"Draught", "Tonic", "Elixir", "Brew", "Vial", "Concoction", "Essence", "Infusion", "Philter",
			"Serum", "Mixture", "Distillate", "Extract", "Syrup", "Remedy", "Cordial", "Tincture", "Decoction",
			"Salve", "Balm", "Oil", "Poultice", "Powder", "Salt", "Ash", "Dust", "Resin", "Venom", "Ichor",
			"Phial", "Ampoule", "Flask", "Mist", "Vapour", "Smoke", "Fume", "Charm", "Hex", "Curse",
			"Blessing", "Rite", "Offering", "Relic", "Memory", "Dream", "Vision", "Whisper", "Lullaby",
			"Confession", "Mercy", "Fever", "Shiver", "Rot", "Binding", "Release", "Awakening"
		};

		for (var i = 0; i < 12; i++)
		{
			var prefix = prefixes[Random.Shared.Next(prefixes.Length)];
			var suffix = suffixes[Random.Shared.Next(suffixes.Length)];
			var candidate = $"{prefix} {suffix}";

			if (!_gameState.PotionDisplayNames.Values.Any(x => string.Equals(x, candidate, System.StringComparison.OrdinalIgnoreCase)))
				return candidate;
		}

		return $"{prefixes[Random.Shared.Next(prefixes.Length)]} {suffixes[Random.Shared.Next(suffixes.Length)]}";
	}

	private int CalculateIngredientTotalPrice(IReadOnlyList<IngredientPortionDef> ingredients)
	{
		var totalPrice = 0;

		foreach (var ingredientPortion in ingredients)
		{
			var itemId = ingredientPortion.InventoryItemId;
			if (!_itemCatalog.TryGetItem(itemId, out var item))
				continue;

			totalPrice += Math.Max(0, item.BasePrice);
		}

		return Math.Max(0, totalPrice);
	}

	private string ResolvePotionIconPath()
	{
		var iconPaths = GetPotionIconPaths();
		if (iconPaths.Count == 0)
			return DefaultPotionIconPath;

		return iconPaths[Random.Shared.Next(iconPaths.Count)];
	}

	private static List<string> GetPotionIconPaths()
	{
		var absoluteDirectoryPath = ProjectSettings.GlobalizePath(PotionIconsDirectoryPath);
		if (!Directory.Exists(absoluteDirectoryPath))
			return new List<string>();

		var files = Directory.GetFiles(absoluteDirectoryPath, "*.svg");
		var iconPaths = new List<string>(files.Length);

		foreach (var filePath in files)
		{
			var fileName = Path.GetFileName(filePath);
			if (string.IsNullOrWhiteSpace(fileName))
				continue;

			iconPaths.Add($"{PotionIconsDirectoryPath}/{fileName}");
		}

		return iconPaths;
	}

	private bool TryBuildIngredientDefs(
		IReadOnlyList<IngredientPortionDef> ingredientPortions,
		out List<IngredientDef> ingredients,
		out string error,
		bool knownStatsOnly = false)
	{
		ingredients = new List<IngredientDef>();

		foreach (var ingredientPortion in ingredientPortions)
		{
			var itemId = ingredientPortion.InventoryItemId;
			if (!_itemCatalog.TryGetItem(itemId, out var item))
			{
				error = $"Unknown ingredient: {itemId}";
				return false;
			}

			var ingredient = IngredientDefFactory.FromItemDef(item);
			if (knownStatsOnly &&
				IngredientPreparationCatalog.TryGetPreparedIngredientInfo(item, out var baseIngredientId, out var preparationId) &&
				!_gameState.KnowsIngredientPreparation(baseIngredientId, preparationId))
			{
				ingredient.Traits.Clear();
				ingredient.Risks.Clear();
				ingredient.IngredientEffects.Clear();
			}

			ingredients.Add(ingredient);
		}

		error = string.Empty;
		return true;
	}

	private static List<IngredientPortionDef> CloneQueuedIngredients(IReadOnlyList<IngredientPortionDef> ingredients)
	{
		var clones = new List<IngredientPortionDef>(ingredients.Count);
		foreach (var ingredient in ingredients)
			clones.Add(ingredient.Clone());

		return clones;
	}

	private static List<string> BuildIngredientIdList(IReadOnlyList<IngredientPortionDef> ingredients)
	{
		var ingredientIds = new List<string>(ingredients.Count);
		foreach (var ingredient in ingredients)
			ingredientIds.Add(ingredient.InventoryItemId);

		return ingredientIds;
	}

	private void ClearDropAnimations()
	{
		foreach (var animation in _dropAnimations)
		{
			if (GodotObject.IsInstanceValid(animation.Icon))
				animation.Icon.QueueFree();
		}

		_dropAnimations.Clear();
		SetProcess(false);
	}

	private sealed class IngredientDropAnimation
	{
		public TextureRect Icon = default!;
		public Vector2 StartTopLeft;
		public Vector2 EndTopLeft;
		public double ElapsedSeconds;
	}

}
