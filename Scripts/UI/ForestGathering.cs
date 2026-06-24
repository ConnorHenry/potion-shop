using System;
using System.Collections.Generic;
using Godot;
using OccultShop.Autoload;
using OccultShop.Infrastructure;

namespace OccultShop.UI;

public partial class ForestGathering : Control
{
	private const int RewardQuantityPerCorrectSelection = 1;
	private const int PlantVisualDepthRange = 20;
	private const int CandidateDebugBorderZIndex = PlantVisualDepthRange + 1;
	private const int TargetDebugHighlightZIndex = CandidateDebugBorderZIndex + 1;
	private const int CandidateBoundsPaddingPixels = 3;
	private const float DefaultInspectionZoomScale = 2.8f;
	private const float MinInspectionZoomScale = 1.3f;
	private const float MaxInspectionZoomScale = 7.0f;
	private const float InspectionZoomWheelStep = 0.35f;
	private static readonly Vector2 CluePanelSize = new(340.0f, 318.0f);
	private static readonly Vector2 CluePanelTopRightOffset = new(-358.0f, 52.0f);
	private static readonly Vector2 ClueSketchSize = new(316.0f, 176.0f);
	private static readonly Vector2 MagnifyingGlassCursorHotspot = new(18.0f, 18.0f);

	[Export] public NodePath ForestBackgroundPath = default!;
	[Export] public NodePath ClueToggleButtonPath = default!;
	[Export] public NodePath CluePanelPath = default!;
	[Export] public NodePath TargetNameLabelPath = default!;
	[Export] public NodePath TargetDescriptionLabelPath = default!;
	[Export] public NodePath SketchTextureRectPath = default!;
	[Export] public NodePath SketchPreviewOverlayPath = default!;
	[Export] public NodePath SketchPreviewImagePath = default!;
	[Export] public NodePath ActionsRemainingLabelPath = default!;
	[Export] public NodePath FeedbackLabelPath = default!;
	[Export] public NodePath PlantHotspotsPath = default!;
	[Export] public NodePath InspectionPanelPath = default!;
	[Export] public NodePath InspectionImagePath = default!;
	[Export] public NodePath InspectionTitleLabelPath = default!;
	[Export] public NodePath InspectionHarvestButtonPath = default!;
	[Export] public NodePath InspectionRemoveButtonPath = default!;
	[Export] public NodePath InspectionKeepLookingButtonPath = default!;
	[Export] public NodePath ReturnPromptPath = default!;
	[Export] public NodePath ReturnPromptMessagePath = default!;
	[Export] public NodePath ReturnButtonPath = default!;
	[Export] public NodePath GameStatePath = new(AutoloadNodePaths.GameState);
	[Export] public NodePath ItemCatalogPath = new(AutoloadNodePaths.ItemCatalog);
	[Export] public NodePath SaveGameManagerPath = new(AutoloadNodePaths.SaveGameManager);
	[Export] public string TargetItemId = "mint";
	[Export] public int MaxActions = 3;
	[Export] public string TargetDescription = "Look for bright green oval leaves with lightly toothed edges, paired on crisp square stems. The plant should look fresh and cooling, not woody or flowering.";
	[Export] public string SketchTexturePath = "res://Assets/Gathering/Sketches/mint_high_quality_pencil_sketch.png";
	[Export] public string MagnifyingGlassCursorPath = "res://Assets/UI/magnifying_glass_cursor.png";
	[Export] public bool ShowCandidateDebugBorders = false;
	[Export] public bool ShowTargetDebugHighlights = false;

	private TextureRect _forestBackground = default!;
	private Button _clueToggleButton = default!;
	private Control _cluePanel = default!;
	private Label _targetNameLabel = default!;
	private Label _targetDescriptionLabel = default!;
	private TextureRect _sketchTextureRect = default!;
	private Control _sketchPreviewOverlay = default!;
	private TextureRect _sketchPreviewImage = default!;
	private Label _actionsRemainingLabel = default!;
	private Label _feedbackLabel = default!;
	private Control _plantHotspots = default!;
	private Control _inspectionPanel = default!;
	private TextureRect _inspectionImage = default!;
	private Label _inspectionTitleLabel = default!;
	private Button _inspectionHarvestButton = default!;
	private Button _inspectionRemoveButton = default!;
	private Button _inspectionKeepLookingButton = default!;
	private Control _returnPrompt = default!;
	private Label _returnPromptMessage = default!;
	private Button _returnButton = default!;
	private GameState _gameState = default!;
	private ItemCatalogService _itemCatalog = default!;
	private SaveGameManager _saveGameManager = default!;
	private Control.GuiInputEventHandler? _plantHotspotsGuiInputHandler;
	private Control.GuiInputEventHandler? _sketchGuiInputHandler;
	private Control.GuiInputEventHandler? _sketchPreviewOverlayGuiInputHandler;
	private Control.GuiInputEventHandler? _inspectionImageGuiInputHandler;
	private readonly ForestGatheringPlantLayout _plantLayout = new();
	private readonly List<ForestGatheringPlantEntry> _activePlantEntries = new();
	private readonly List<TextureRect> _plantVisuals = new();
	private readonly List<Panel> _candidateDebugBorders = new();
	private readonly List<Panel> _targetDebugHighlights = new();
	private readonly Dictionary<int, TextureRect> _plantVisualsByPlantIndex = new();
	private readonly Dictionary<int, Panel> _candidateDebugBordersByPlantIndex = new();
	private readonly Dictionary<int, Panel> _targetDebugHighlightsByPlantIndex = new();
	private readonly Dictionary<string, Texture2D> _plantTextureCache = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, Rect2> _plantContentBoundsCache = new(StringComparer.OrdinalIgnoreCase);
	private readonly HashSet<int> _harvestedPlantIndexes = new();
	private readonly HashSet<int> _removedPlantIndexes = new();
	private Texture2D? _magnifyingGlassCursorTexture;
	private Texture2D? _inspectionSourceTexture;
	private Image? _inspectionSourceImage;
	private Rect2I _activeInspectionZoomCrop;
	private int _inspectedPlantIndex = -1;
	private int _remainingActions;
	private int _correctSelections;
	private int _pendingTargetQuantity;
	private int _pendingSeedQuantity;
	private bool _finished;
	private bool _rewardsCommitted;
	private bool _hasActiveInspectionZoomCrop;
	private bool _inspectionZoomEnabled;
	private bool _inspectionCursorActive;
	private float _inspectionZoomScale = DefaultInspectionZoomScale;

	public override void _Ready()
	{
		if (!ResolveNodes())
			return;

		_returnButton.Pressed += OnReturnPressed;
		_clueToggleButton.Pressed += OnClueTogglePressed;
		_inspectionHarvestButton.Pressed += OnHarvestPressed;
		_inspectionRemoveButton.Pressed += OnRemovePressed;
		_inspectionKeepLookingButton.Pressed += HideInspectionPanel;
		_inspectionPanel.MouseEntered += OnInspectionPanelMouseEntered;
		_inspectionPanel.MouseExited += OnInspectionPanelMouseExited;
		_inspectionHarvestButton.MouseEntered += OnInspectionActionButtonMouseEntered;
		_inspectionHarvestButton.MouseExited += OnInspectionActionButtonMouseExited;
		_inspectionRemoveButton.MouseEntered += OnInspectionActionButtonMouseEntered;
		_inspectionRemoveButton.MouseExited += OnInspectionActionButtonMouseExited;
		_inspectionKeepLookingButton.MouseEntered += OnInspectionActionButtonMouseEntered;
		_inspectionKeepLookingButton.MouseExited += OnInspectionActionButtonMouseExited;
		MaxActions = Math.Max(1, MaxActions);
		_remainingActions = MaxActions;
		_returnPrompt.Visible = false;
		_inspectionPanel.Visible = false;
		_inspectionPanel.MouseFilter = MouseFilterEnum.Stop;
		_inspectionImage.MouseFilter = MouseFilterEnum.Stop;
		_inspectionImageGuiInputHandler = OnInspectionImageGuiInput;
		_inspectionImage.GuiInput += _inspectionImageGuiInputHandler;
		_sketchPreviewOverlay.Visible = false;
		_sketchPreviewOverlay.MouseFilter = MouseFilterEnum.Stop;
		_sketchPreviewOverlayGuiInputHandler = OnSketchPreviewOverlayGuiInput;
		_sketchPreviewOverlay.GuiInput += _sketchPreviewOverlayGuiInputHandler;
		_sketchTextureRect.MouseFilter = MouseFilterEnum.Stop;
		_sketchTextureRect.CustomMinimumSize = ClueSketchSize;
		_sketchTextureRect.Size = ClueSketchSize;
		_sketchGuiInputHandler = OnSketchGuiInput;
		_sketchTextureRect.GuiInput += _sketchGuiInputHandler;
		_sketchPreviewImage.MouseFilter = MouseFilterEnum.Stop;
		_cluePanel.Visible = false;
		ConfigureCluePanelLayout();
		_plantHotspots.MouseFilter = MouseFilterEnum.Stop;
		_plantHotspotsGuiInputHandler = OnPlantHotspotsGuiInput;
		_plantHotspots.GuiInput += _plantHotspotsGuiInputHandler;

		LoadMagnifyingGlassCursor();
		LoadSketchTexture();
		RefreshTargetText();
		RefreshActionsRemaining();
		ValidatePlantEntries();
		RandomizePlantLayout();
		CreatePlantVisuals();
		SetFeedback($"Inspect candidates, then harvest {_remainingActions} plants.");
		TryAutoSave("entering the forest gathering scene");
	}

	public override void _ExitTree()
	{
		if (_returnButton is not null)
			_returnButton.Pressed -= OnReturnPressed;
		if (_clueToggleButton is not null)
			_clueToggleButton.Pressed -= OnClueTogglePressed;
		if (_inspectionHarvestButton is not null)
			_inspectionHarvestButton.Pressed -= OnHarvestPressed;
		if (_inspectionRemoveButton is not null)
			_inspectionRemoveButton.Pressed -= OnRemovePressed;
		if (_inspectionKeepLookingButton is not null)
			_inspectionKeepLookingButton.Pressed -= HideInspectionPanel;
		if (_inspectionPanel is not null)
		{
			_inspectionPanel.MouseEntered -= OnInspectionPanelMouseEntered;
			_inspectionPanel.MouseExited -= OnInspectionPanelMouseExited;
		}
		if (_inspectionHarvestButton is not null)
		{
			_inspectionHarvestButton.MouseEntered -= OnInspectionActionButtonMouseEntered;
			_inspectionHarvestButton.MouseExited -= OnInspectionActionButtonMouseExited;
		}
		if (_inspectionRemoveButton is not null)
		{
			_inspectionRemoveButton.MouseEntered -= OnInspectionActionButtonMouseEntered;
			_inspectionRemoveButton.MouseExited -= OnInspectionActionButtonMouseExited;
		}
		if (_inspectionKeepLookingButton is not null)
		{
			_inspectionKeepLookingButton.MouseEntered -= OnInspectionActionButtonMouseEntered;
			_inspectionKeepLookingButton.MouseExited -= OnInspectionActionButtonMouseExited;
		}
		if (_inspectionImage is not null && _inspectionImageGuiInputHandler is not null)
			_inspectionImage.GuiInput -= _inspectionImageGuiInputHandler;
		if (_plantHotspots is not null && _plantHotspotsGuiInputHandler is not null)
			_plantHotspots.GuiInput -= _plantHotspotsGuiInputHandler;
		if (_sketchTextureRect is not null && _sketchGuiInputHandler is not null)
			_sketchTextureRect.GuiInput -= _sketchGuiInputHandler;
		if (_sketchPreviewOverlay is not null && _sketchPreviewOverlayGuiInputHandler is not null)
			_sketchPreviewOverlay.GuiInput -= _sketchPreviewOverlayGuiInputHandler;
		SetInspectionCursorActive(false);
	}

	public override void _Input(InputEvent @event)
	{
		if (_inspectionPanel is null || !_inspectionPanel.Visible)
			return;

		if (@event is InputEventMouseMotion)
		{
			RefreshInspectionMagnifierForMousePosition();
			return;
		}

		if (@event is not InputEventMouseButton mouseButton || !mouseButton.Pressed)
			return;

		if (!IsInspectionZoomWheel(mouseButton.ButtonIndex))
			return;

		if (!_inspectionZoomEnabled || !ShouldUseInspectionMagnifier(mouseButton.GlobalPosition))
			return;

		AdjustInspectionZoom(mouseButton.ButtonIndex, mouseButton.GlobalPosition);
		AcceptEvent();
	}

	private bool ResolveNodes()
	{
		if (!NodeLookup.TryGetRequiredNode(this, ForestBackgroundPath, nameof(ForestGathering), nameof(ForestBackgroundPath), out _forestBackground))
			return false;
		if (!NodeLookup.TryGetRequiredNode(this, ClueToggleButtonPath, nameof(ForestGathering), nameof(ClueToggleButtonPath), out _clueToggleButton))
			return false;
		if (!NodeLookup.TryGetRequiredNode(this, CluePanelPath, nameof(ForestGathering), nameof(CluePanelPath), out _cluePanel))
			return false;
		if (!NodeLookup.TryGetRequiredNode(this, TargetNameLabelPath, nameof(ForestGathering), nameof(TargetNameLabelPath), out _targetNameLabel))
			return false;
		if (!NodeLookup.TryGetRequiredNode(this, TargetDescriptionLabelPath, nameof(ForestGathering), nameof(TargetDescriptionLabelPath), out _targetDescriptionLabel))
			return false;
		if (!NodeLookup.TryGetRequiredNode(this, SketchTextureRectPath, nameof(ForestGathering), nameof(SketchTextureRectPath), out _sketchTextureRect))
			return false;
		if (!NodeLookup.TryGetRequiredNode(this, SketchPreviewOverlayPath, nameof(ForestGathering), nameof(SketchPreviewOverlayPath), out _sketchPreviewOverlay))
			return false;
		if (!NodeLookup.TryGetRequiredNode(this, SketchPreviewImagePath, nameof(ForestGathering), nameof(SketchPreviewImagePath), out _sketchPreviewImage))
			return false;
		if (!NodeLookup.TryGetRequiredNode(this, ActionsRemainingLabelPath, nameof(ForestGathering), nameof(ActionsRemainingLabelPath), out _actionsRemainingLabel))
			return false;
		if (!NodeLookup.TryGetRequiredNode(this, FeedbackLabelPath, nameof(ForestGathering), nameof(FeedbackLabelPath), out _feedbackLabel))
			return false;
		if (!NodeLookup.TryGetRequiredNode(this, PlantHotspotsPath, nameof(ForestGathering), nameof(PlantHotspotsPath), out _plantHotspots))
			return false;
		if (!NodeLookup.TryGetRequiredNode(this, InspectionPanelPath, nameof(ForestGathering), nameof(InspectionPanelPath), out _inspectionPanel))
			return false;
		if (!NodeLookup.TryGetRequiredNode(this, InspectionImagePath, nameof(ForestGathering), nameof(InspectionImagePath), out _inspectionImage))
			return false;
		if (!NodeLookup.TryGetRequiredNode(this, InspectionTitleLabelPath, nameof(ForestGathering), nameof(InspectionTitleLabelPath), out _inspectionTitleLabel))
			return false;
		if (!NodeLookup.TryGetRequiredNode(this, InspectionHarvestButtonPath, nameof(ForestGathering), nameof(InspectionHarvestButtonPath), out _inspectionHarvestButton))
			return false;
		if (!NodeLookup.TryGetRequiredNode(this, InspectionRemoveButtonPath, nameof(ForestGathering), nameof(InspectionRemoveButtonPath), out _inspectionRemoveButton))
			return false;
		if (!NodeLookup.TryGetRequiredNode(this, InspectionKeepLookingButtonPath, nameof(ForestGathering), nameof(InspectionKeepLookingButtonPath), out _inspectionKeepLookingButton))
			return false;
		if (!NodeLookup.TryGetRequiredNode(this, ReturnPromptPath, nameof(ForestGathering), nameof(ReturnPromptPath), out _returnPrompt))
			return false;
		if (!NodeLookup.TryGetRequiredNode(this, ReturnPromptMessagePath, nameof(ForestGathering), nameof(ReturnPromptMessagePath), out _returnPromptMessage))
			return false;
		if (!NodeLookup.TryGetRequiredNode(this, ReturnButtonPath, nameof(ForestGathering), nameof(ReturnButtonPath), out _returnButton))
			return false;

		var gameState = GetNodeOrNull<GameState>(GameStatePath);
		if (gameState is null)
		{
			GD.PushError($"ForestGathering: GameState was not found at '{GameStatePath}'.");
			return false;
		}

		var itemCatalog = GetNodeOrNull<ItemCatalogService>(ItemCatalogPath);
		if (itemCatalog is null)
		{
			GD.PushError($"ForestGathering: ItemCatalog was not found at '{ItemCatalogPath}'.");
			return false;
		}

		var saveGameManager = GetNodeOrNull<SaveGameManager>(SaveGameManagerPath);
		if (saveGameManager is null)
		{
			GD.PushError($"ForestGathering: SaveGameManager was not found at '{SaveGameManagerPath}'.");
			return false;
		}

		_gameState = gameState;
		_itemCatalog = itemCatalog;
		_saveGameManager = saveGameManager;
		return true;
	}

	private void LoadSketchTexture()
	{
		if (string.IsNullOrWhiteSpace(SketchTexturePath))
			return;

		var texture = ResourceLoader.Load<Texture2D>(SketchTexturePath);
		if (texture is null)
		{
			GD.PushError($"ForestGathering: Failed to load sketch texture from '{SketchTexturePath}'.");
			return;
		}

		_sketchTextureRect.Texture = texture;
		_sketchPreviewImage.Texture = texture;
	}

	private void ConfigureCluePanelLayout()
	{
		_cluePanel.CustomMinimumSize = CluePanelSize;
		_cluePanel.AnchorLeft = 1.0f;
		_cluePanel.AnchorTop = 0.0f;
		_cluePanel.AnchorRight = 1.0f;
		_cluePanel.AnchorBottom = 0.0f;
		_cluePanel.OffsetLeft = CluePanelTopRightOffset.X;
		_cluePanel.OffsetTop = CluePanelTopRightOffset.Y;
		_cluePanel.OffsetRight = CluePanelTopRightOffset.X + CluePanelSize.X;
		_cluePanel.OffsetBottom = CluePanelTopRightOffset.Y + CluePanelSize.Y;
		_cluePanel.Size = CluePanelSize;
	}

	private void LoadMagnifyingGlassCursor()
	{
		if (string.IsNullOrWhiteSpace(MagnifyingGlassCursorPath))
			return;

		var texture = ResourceLoader.Load<Texture2D>(MagnifyingGlassCursorPath);
		if (texture is null)
		{
			GD.PushError($"ForestGathering: Failed to load magnifying glass cursor from '{MagnifyingGlassCursorPath}'.");
			return;
		}

		_magnifyingGlassCursorTexture = texture;
	}

	private void RefreshTargetText()
	{
		var targetName = GetItemName(TargetItemId);
		_targetNameLabel.Text = $"Looking for: {targetName}";
		_targetDescriptionLabel.Text = TargetDescription;
	}

	private void ValidatePlantEntries()
	{
		if (!_itemCatalog.TryGetItem(TargetItemId, out _))
			GD.PushError($"ForestGathering: Target item '{TargetItemId}' is not in the item catalog.");

		foreach (var entry in ForestGatheringPlantCatalog.Definitions)
		{
			if (!_itemCatalog.TryGetItem(entry.ItemId, out _))
			{
				GD.PushError($"ForestGathering: Plant entry references unknown item '{entry.ItemId}'.");
			}
		}
	}

	private void OnPlantHotspotsGuiInput(InputEvent @event)
	{
		if (@event is not InputEventMouseButton mouseButton)
			return;

		if (mouseButton.ButtonIndex != MouseButton.Left || !mouseButton.Pressed)
			return;

		if (_finished || _inspectionPanel.Visible || _remainingActions <= 0)
			return;

		HandleGatheringClick(mouseButton.GlobalPosition);
		AcceptEvent();
	}

	private void HandleGatheringClick(Vector2 globalPosition)
	{
		if (!TryGetPlantEntryAtGlobalPosition(globalPosition, out var plantIndex, out var entry))
		{
			SetFeedback("No clear candidate there. Choose a plant shape to inspect.");
		}
		else if (_harvestedPlantIndexes.Contains(plantIndex))
		{
			SetFeedback("That plant has already been harvested.");
		}
		else if (_removedPlantIndexes.Contains(plantIndex))
		{
			SetFeedback("That plant has already been removed.");
		}
		else
		{
			ShowInspection(plantIndex, entry);
		}
	}

	private void ShowInspection(int plantIndex, ForestGatheringPlantEntry entry)
	{
		_inspectedPlantIndex = plantIndex;
		_inspectionTitleLabel.Text = "Inspection";
		_inspectionHarvestButton.Disabled = _remainingActions <= 0;
		_inspectionRemoveButton.Disabled = false;
		RefreshInspectionImage(entry);
		_inspectionPanel.Visible = true;
		_inspectionPanel.MoveToFront();
		_inspectionHarvestButton.GrabFocus();
		RefreshInspectionMagnifierForMousePosition();
		SetFeedback("Use the close-up to decide whether to harvest, remove, or keep looking.");
	}

	private void OnHarvestPressed()
	{
		if (_finished || _remainingActions <= 0 || _inspectedPlantIndex < 0 || _inspectedPlantIndex >= _activePlantEntries.Count)
			return;

		var plantIndex = _inspectedPlantIndex;
		HideInspectionPanel();

		if (!_harvestedPlantIndexes.Add(plantIndex))
		{
			SetFeedback("That plant has already been harvested.");
			return;
		}

		RemovePlantFromArea(plantIndex);
		_remainingActions -= 1;

		if (IsTargetPlant(plantIndex))
			CollectTargetPlant();
		else
			SetFeedback(BuildWrongPlantFeedback(_activePlantEntries[plantIndex]));

		RefreshActionsRemaining();

		if (_remainingActions <= 0 || !HasSelectablePlants())
			FinishGathering();
	}

	private void OnRemovePressed()
	{
		if (_finished || _inspectedPlantIndex < 0 || _inspectedPlantIndex >= _activePlantEntries.Count)
			return;

		var plantIndex = _inspectedPlantIndex;
		HideInspectionPanel();

		if (_harvestedPlantIndexes.Contains(plantIndex))
		{
			SetFeedback("That plant has already been harvested.");
			return;
		}

		if (!_removedPlantIndexes.Add(plantIndex))
		{
			SetFeedback("That plant has already been removed.");
			return;
		}

		RemovePlantFromArea(plantIndex);
		SetFeedback("Removed this plant from the area.");

		if (!HasSelectablePlants())
			FinishGathering();
	}

	private void OnClueTogglePressed()
	{
		var shouldShow = !_cluePanel.Visible;
		_cluePanel.Visible = shouldShow;
		if (shouldShow)
		{
			ConfigureCluePanelLayout();
			_cluePanel.MoveToFront();
		}
		else
		{
			HideSketchPreview();
		}
	}

	private void OnSketchGuiInput(InputEvent @event)
	{
		if (@event is not InputEventMouseButton mouseButton)
			return;

		if (mouseButton.ButtonIndex != MouseButton.Left || !mouseButton.Pressed)
			return;

		ShowSketchPreview();
		_sketchTextureRect.AcceptEvent();
	}

	private void OnSketchPreviewOverlayGuiInput(InputEvent @event)
	{
		if (@event is not InputEventMouseButton mouseButton)
			return;

		if (mouseButton.ButtonIndex != MouseButton.Left || !mouseButton.Pressed)
			return;

		if (!_sketchPreviewImage.GetGlobalRect().HasPoint(mouseButton.GlobalPosition))
			HideSketchPreview();

		_sketchPreviewOverlay.AcceptEvent();
	}

	private void ShowSketchPreview()
	{
		if (_sketchPreviewOverlay is null)
			return;

		_sketchPreviewOverlay.Visible = true;
		_sketchPreviewOverlay.MoveToFront();
	}

	private void HideSketchPreview()
	{
		if (_sketchPreviewOverlay is not null)
			_sketchPreviewOverlay.Visible = false;
	}

	private void HideInspectionPanel()
	{
		_inspectedPlantIndex = -1;
		_inspectionZoomEnabled = false;
		_inspectionZoomScale = DefaultInspectionZoomScale;
		SetInspectionCursorActive(false);
		RestoreFullInspectionImage();
		if (_inspectionPanel is not null)
			_inspectionPanel.Visible = false;
	}

	private void RefreshInspectionImage(ForestGatheringPlantEntry entry)
	{
		var texture = LoadPlantTexture(entry.InspectionTexturePath) ?? LoadPlantTexture(entry.TexturePath);
		_inspectionSourceTexture = texture;
		_inspectionSourceImage = texture?.GetImage();
		_hasActiveInspectionZoomCrop = false;
		_inspectionZoomEnabled = false;
		_inspectionZoomScale = DefaultInspectionZoomScale;
		SetInspectionCursorActive(false);
		_inspectionImage.Texture = texture;
	}

	private void OnInspectionImageGuiInput(InputEvent @event)
	{
		if (@event is not InputEventMouseButton mouseButton)
			return;

		if (mouseButton.ButtonIndex != MouseButton.Left || !mouseButton.Pressed)
			return;

		ToggleInspectionZoom(mouseButton.GlobalPosition);
		_inspectionImage.AcceptEvent();
	}

	private void OnInspectionPanelMouseEntered()
	{
		RefreshInspectionMagnifierForMousePosition();
	}

	private void OnInspectionPanelMouseExited()
	{
		SetInspectionCursorActive(false);
		RestoreFullInspectionImage();
	}

	private void OnInspectionActionButtonMouseEntered()
	{
		SetInspectionCursorActive(false);
		RestoreFullInspectionImage();
	}

	private void OnInspectionActionButtonMouseExited()
	{
		RefreshInspectionMagnifierForMousePosition();
	}

	private void RefreshInspectionMagnifierForMousePosition()
	{
		if (_inspectionPanel is null || !_inspectionPanel.Visible || !_inspectionZoomEnabled)
		{
			SetInspectionCursorActive(false);
			RestoreFullInspectionImage();
			return;
		}

		var mousePosition = GetViewport().GetMousePosition();
		if (!ShouldUseInspectionMagnifier(mousePosition))
		{
			SetInspectionCursorActive(false);
			RestoreFullInspectionImage();
			return;
		}

		SetInspectionCursorActive(true);
		UpdateInspectionZoomFromGlobalPosition(mousePosition);
	}

	private void ToggleInspectionZoom(Vector2 globalPosition)
	{
		_inspectionZoomEnabled = !_inspectionZoomEnabled;
		if (!_inspectionZoomEnabled)
		{
			SetInspectionCursorActive(false);
			RestoreFullInspectionImage();
			return;
		}

		_inspectionZoomScale = DefaultInspectionZoomScale;
		if (ShouldUseInspectionMagnifier(globalPosition))
		{
			SetInspectionCursorActive(true);
			UpdateInspectionZoomFromGlobalPosition(globalPosition);
		}
	}

	private void AdjustInspectionZoom(MouseButton buttonIndex, Vector2 globalPosition)
	{
		var zoomDelta = buttonIndex == MouseButton.WheelUp
			? InspectionZoomWheelStep
			: -InspectionZoomWheelStep;
		_inspectionZoomScale = Math.Clamp(
			_inspectionZoomScale + zoomDelta,
			MinInspectionZoomScale,
			MaxInspectionZoomScale);
		UpdateInspectionZoomFromGlobalPosition(globalPosition);
	}

	private static bool IsInspectionZoomWheel(MouseButton buttonIndex)
	{
		return buttonIndex == MouseButton.WheelUp || buttonIndex == MouseButton.WheelDown;
	}

	private bool ShouldUseInspectionMagnifier(Vector2 globalPosition)
	{
		var overPanel = _inspectionPanel.GetGlobalRect().HasPoint(globalPosition);
		if (!overPanel)
			return false;

		var overHarvestButton = _inspectionHarvestButton is not null && _inspectionHarvestButton.GetGlobalRect().HasPoint(globalPosition);
		var overRemoveButton = _inspectionRemoveButton is not null && _inspectionRemoveButton.GetGlobalRect().HasPoint(globalPosition);
		var overKeepLookingButton = _inspectionKeepLookingButton is not null && _inspectionKeepLookingButton.GetGlobalRect().HasPoint(globalPosition);
		return !overHarvestButton && !overRemoveButton && !overKeepLookingButton;
	}

	private void SetInspectionCursorActive(bool active)
	{
		if (_inspectionCursorActive == active)
			return;

		_inspectionCursorActive = active;
		if (active && _magnifyingGlassCursorTexture is not null)
		{
			Input.SetCustomMouseCursor(_magnifyingGlassCursorTexture, Input.CursorShape.Arrow, MagnifyingGlassCursorHotspot);
			return;
		}

		Input.SetCustomMouseCursor(null, Input.CursorShape.Arrow);
	}

	private void UpdateInspectionZoomFromGlobalPosition(Vector2 globalPosition)
	{
		if (_inspectionSourceTexture is null || _inspectionSourceImage is null)
		{
			RestoreFullInspectionImage();
			return;
		}

		if (!TryMapInspectionGlobalPositionToSource(globalPosition, out var sourcePosition))
		{
			RestoreFullInspectionImage();
			return;
		}

		var pixelX = Math.Clamp(Mathf.RoundToInt(sourcePosition.X), 0, _inspectionSourceImage.GetWidth() - 1);
		var pixelY = Math.Clamp(Mathf.RoundToInt(sourcePosition.Y), 0, _inspectionSourceImage.GetHeight() - 1);
		var cropRect = BuildInspectionZoomCrop(pixelX, pixelY);
		if (_hasActiveInspectionZoomCrop && _activeInspectionZoomCrop.Equals(cropRect))
			return;

		_activeInspectionZoomCrop = cropRect;
		_hasActiveInspectionZoomCrop = true;
		var crop = _inspectionSourceImage.GetRegion(cropRect);
		_inspectionImage.Texture = ImageTexture.CreateFromImage(crop);
	}

	private bool TryMapInspectionGlobalPositionToSource(Vector2 globalPosition, out Vector2 sourcePosition)
	{
		sourcePosition = Vector2.Zero;
		var imageRect = _inspectionImage.GetGlobalRect();
		if (imageRect.Size.X <= 0.0f || imageRect.Size.Y <= 0.0f)
			return false;

		var localPosition = new Vector2(
			Math.Clamp(globalPosition.X - imageRect.Position.X, 0.0f, imageRect.Size.X),
			Math.Clamp(globalPosition.Y - imageRect.Position.Y, 0.0f, imageRect.Size.Y));
		return TryMapInspectionImagePositionToSource(localPosition, true, out sourcePosition);
	}

	private bool TryMapInspectionImagePositionToSource(Vector2 localPosition, bool clampToDrawnTexture, out Vector2 sourcePosition)
	{
		sourcePosition = Vector2.Zero;
		if (_inspectionSourceTexture is null)
			return false;

		var sourceSize = _inspectionSourceTexture.GetSize();
		var controlSize = _inspectionImage.Size;
		if (sourceSize.X <= 0.0f || sourceSize.Y <= 0.0f || controlSize.X <= 0.0f || controlSize.Y <= 0.0f)
			return false;

		var scale = MathF.Min(controlSize.X / sourceSize.X, controlSize.Y / sourceSize.Y);
		if (scale <= 0.0f)
			return false;

		var drawnSize = sourceSize * scale;
		var topLeft = (controlSize - drawnSize) * 0.5f;
		var bottomRight = topLeft + drawnSize;
		if (clampToDrawnTexture)
		{
			localPosition = new Vector2(
				Math.Clamp(localPosition.X, topLeft.X, bottomRight.X),
				Math.Clamp(localPosition.Y, topLeft.Y, bottomRight.Y));
		}
		else if (localPosition.X < topLeft.X || localPosition.Y < topLeft.Y || localPosition.X > bottomRight.X || localPosition.Y > bottomRight.Y)
		{
			return false;
		}

		var normalizedX = (localPosition.X - topLeft.X) / drawnSize.X;
		var normalizedY = (localPosition.Y - topLeft.Y) / drawnSize.Y;
		sourcePosition = new Vector2(normalizedX * sourceSize.X, normalizedY * sourceSize.Y);
		return true;
	}

	private Rect2I BuildInspectionZoomCrop(int centerX, int centerY)
	{
		if (_inspectionSourceImage is null)
			return new Rect2I();

		var sourceWidth = _inspectionSourceImage.GetWidth();
		var sourceHeight = _inspectionSourceImage.GetHeight();
		if (sourceWidth <= 0 || sourceHeight <= 0)
			return new Rect2I();

		var controlSize = _inspectionImage.Size;
		var controlAspect = controlSize.Y > 0.0f ? controlSize.X / controlSize.Y : 1.0f;
		var cropWidth = Math.Clamp(Mathf.RoundToInt(sourceWidth / _inspectionZoomScale), 1, sourceWidth);
		var cropHeight = Math.Clamp(Mathf.RoundToInt(cropWidth / controlAspect), 1, sourceHeight);
		cropWidth = Math.Clamp(Mathf.RoundToInt(cropHeight * controlAspect), 1, sourceWidth);

		var cropLeft = Math.Clamp(centerX - (cropWidth / 2), 0, sourceWidth - cropWidth);
		var cropTop = Math.Clamp(centerY - (cropHeight / 2), 0, sourceHeight - cropHeight);
		return new Rect2I(cropLeft, cropTop, cropWidth, cropHeight);
	}

	private void RestoreFullInspectionImage()
	{
		_hasActiveInspectionZoomCrop = false;
		if (_inspectionImage is not null)
			_inspectionImage.Texture = _inspectionSourceTexture;
	}

	private bool TryGetPlantEntryAtGlobalPosition(Vector2 globalPosition, out int plantIndex, out ForestGatheringPlantEntry entry)
	{
		plantIndex = -1;
		entry = default;

		var hotspotsRect = _plantHotspots.GetGlobalRect();
		if (hotspotsRect.Size.X <= 0.0f || hotspotsRect.Size.Y <= 0.0f || !hotspotsRect.HasPoint(globalPosition))
			return false;

		var normalizedPosition = new Vector2(
			(globalPosition.X - hotspotsRect.Position.X) / hotspotsRect.Size.X,
			(globalPosition.Y - hotspotsRect.Position.Y) / hotspotsRect.Size.Y);

		return TryGetPlantEntryAtNormalizedPosition(normalizedPosition, hotspotsRect.Size, out plantIndex, out entry);
	}

	private bool TryGetPlantEntryAtNormalizedPosition(Vector2 normalizedPosition, Vector2 surfaceSize, out int plantIndex, out ForestGatheringPlantEntry entry)
	{
		for (var index = _activePlantEntries.Count - 1; index >= 0; index--)
		{
			if (IsPlantUnavailable(index))
				continue;

			var candidate = _activePlantEntries[index];
			var candidateBounds = CalculateCandidateBounds(candidate, surfaceSize);
			if (!candidateBounds.HasPoint(normalizedPosition))
				continue;

			plantIndex = index;
			entry = candidate;
			return true;
		}

		plantIndex = -1;
		entry = default;
		return false;
	}

	private void CollectTargetPlant()
	{
		_correctSelections += 1;
		_pendingTargetQuantity += RewardQuantityPerCorrectSelection;
		SetFeedback($"Correct. Marked {GetItemName(TargetItemId)} for collection.");
	}

	private bool IsTargetPlant(int plantIndex)
	{
		if (plantIndex < 0 || plantIndex >= _activePlantEntries.Count)
			return false;

		return IsTargetEntry(_activePlantEntries[plantIndex]);
	}

	private bool IsTargetEntry(ForestGatheringPlantEntry entry)
	{
		return string.Equals(entry.ItemId, TargetItemId, StringComparison.OrdinalIgnoreCase);
	}

	private string BuildWrongPlantFeedback(ForestGatheringPlantEntry entry)
	{
		var targetName = GetItemName(TargetItemId);
		var plantName = GetItemName(entry.ItemId);
		return ForestGatheringFeedbackFormatter.BuildWrongPlantFeedback(entry, targetName, plantName);
	}

	private bool IsPlantUnavailable(int plantIndex)
	{
		return _harvestedPlantIndexes.Contains(plantIndex) || _removedPlantIndexes.Contains(plantIndex);
	}

	private bool HasSelectablePlants()
	{
		for (var index = 0; index < _activePlantEntries.Count; index++)
		{
			if (!IsPlantUnavailable(index))
				return true;
		}

		return false;
	}

	private void FinishGathering()
	{
		_finished = true;
		HideInspectionPanel();

		var perfectGathering = _correctSelections == MaxActions;
		if (perfectGathering)
			StagePerfectGatheringSeedReward();

		_returnPromptMessage.Text = BuildReturnSummary();
		_returnPrompt.Visible = true;
		_returnPrompt.MoveToFront();
		_returnButton.GrabFocus();
	}

	private void StagePerfectGatheringSeedReward()
	{
		if (!_gameState.TryGetGardenCropByIngredientId(TargetItemId, out _))
		{
			GD.PushError($"ForestGathering: Cannot stage seed for unknown garden crop '{TargetItemId}'.");
			return;
		}

		_pendingSeedQuantity = 1;
		SetFeedback($"Perfect gathering. A {GetItemName(TargetItemId)} seed is ready to bring home.");
	}

	private void OnReturnPressed()
	{
		CommitGatheredRewards();
		TryAutoSave("returning from the forest gathering scene");
		Error error = GetTree().ChangeSceneToFile(ScenePaths.Main);
		if (error != Error.Ok)
			GD.PushError($"ForestGathering: Failed to load main scene. Error: {error}");
	}

	private string BuildReturnSummary()
	{
		var targetName = GetItemName(TargetItemId);
		if (_pendingTargetQuantity <= 0 && _pendingSeedQuantity <= 0)
			return $"You harvested {_correctSelections}/{MaxActions} {targetName} plants. Return to the house.";

		var seedSummary = _pendingSeedQuantity > 0
			? $"\n{targetName} seed x{_pendingSeedQuantity}"
			: string.Empty;
		return $"You harvested {_correctSelections}/{MaxActions} {targetName} plants.\n\nReturn to the house to add:\n{targetName} x{_pendingTargetQuantity}{seedSummary}";
	}

	private void CommitGatheredRewards()
	{
		if (_rewardsCommitted)
			return;

		if (_pendingTargetQuantity > 0)
			_gameState.AddItem(TargetItemId, _pendingTargetQuantity);

		if (_pendingSeedQuantity > 0)
			_gameState.AddSeed(GameState.BuildSeedId(TargetItemId), _pendingSeedQuantity);

		_rewardsCommitted = true;
	}

	private void RefreshActionsRemaining()
	{
		_actionsRemainingLabel.Text = $"Harvests remaining: {_remainingActions}";
	}

	private void SetFeedback(string message)
	{
		_feedbackLabel.Text = message;
	}

	private string GetItemName(string itemId)
	{
		return _itemCatalog.TryGetItem(itemId, out var item) && !string.IsNullOrWhiteSpace(item.Name)
			? item.Name
			: itemId;
	}

	private bool TryAutoSave(string context)
	{
		var saveSucceeded = _saveGameManager.SaveGame();
		if (!saveSucceeded)
			GD.PushError($"ForestGathering: Auto-save failed while {context}.");

		return saveSucceeded;
	}

	private void RandomizePlantLayout()
	{
		_activePlantEntries.Clear();
		_harvestedPlantIndexes.Clear();
		_removedPlantIndexes.Clear();

		var random = new RandomNumberGenerator();
		random.Randomize();

		_activePlantEntries.AddRange(_plantLayout.CreateRandomizedEntries(
			ForestGatheringPlantCatalog.Definitions,
			random));
	}

	private void CreatePlantVisuals()
	{
		foreach (var visual in _plantVisuals)
			visual.QueueFree();
		_plantVisuals.Clear();
		_plantVisualsByPlantIndex.Clear();
		foreach (var border in _candidateDebugBorders)
			border.QueueFree();
		_candidateDebugBorders.Clear();
		_candidateDebugBordersByPlantIndex.Clear();
		foreach (var highlight in _targetDebugHighlights)
			highlight.QueueFree();
		_targetDebugHighlights.Clear();
		_targetDebugHighlightsByPlantIndex.Clear();
		var hotspotSize = _plantHotspots.GetGlobalRect().Size;

		for (var index = 0; index < _activePlantEntries.Count; index++)
		{
			var entry = _activePlantEntries[index];
			var texture = LoadPlantTexture(entry.TexturePath);
			if (texture is null)
				continue;

			var visual = new TextureRect
			{
				Name = $"Plant{index}",
				Texture = texture,
				MouseFilter = MouseFilterEnum.Ignore,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				Modulate = new Color(1.0f, 1.0f, 1.0f, 0.88f),
				ZIndex = Mathf.RoundToInt(entry.Center.Y * PlantVisualDepthRange)
			};
			SetNormalizedRect(visual, entry.Center, entry.Size);
			_plantHotspots.AddChild(visual);
			_plantVisuals.Add(visual);
			_plantVisualsByPlantIndex[index] = visual;

			if (!ShowCandidateDebugBorders && (!ShowTargetDebugHighlights || !IsTargetEntry(entry)))
				continue;

			var candidateBounds = CalculateCandidateBounds(entry, hotspotSize);
			if (ShowCandidateDebugBorders)
			{
				var border = new Panel
				{
					Name = $"CandidateDebugBorder{index}",
					MouseFilter = MouseFilterEnum.Ignore,
					ZIndex = CandidateDebugBorderZIndex
				};
				border.AddThemeStyleboxOverride("panel", CreateCandidateDebugBorderStyleBox());
				SetNormalizedRect(border, candidateBounds.Position + (candidateBounds.Size * 0.5f), candidateBounds.Size);
				_plantHotspots.AddChild(border);
				_candidateDebugBorders.Add(border);
				_candidateDebugBordersByPlantIndex[index] = border;
			}

			if (!ShowTargetDebugHighlights || !IsTargetEntry(entry))
				continue;

			var highlight = new Panel
			{
				Name = $"TargetDebugHighlight{index}",
				MouseFilter = MouseFilterEnum.Ignore,
				ZIndex = TargetDebugHighlightZIndex
			};
			highlight.AddThemeStyleboxOverride("panel", CreateTargetDebugHighlightStyleBox());
			SetNormalizedRect(highlight, candidateBounds.Position + (candidateBounds.Size * 0.5f), candidateBounds.Size);
			_plantHotspots.AddChild(highlight);
			_targetDebugHighlights.Add(highlight);
			_targetDebugHighlightsByPlantIndex[index] = highlight;
		}
	}

	private void RemovePlantFromArea(int plantIndex)
	{
		if (_plantVisualsByPlantIndex.TryGetValue(plantIndex, out var visual))
			visual.Visible = false;

		if (_candidateDebugBordersByPlantIndex.TryGetValue(plantIndex, out var border))
			border.Visible = false;

		if (_targetDebugHighlightsByPlantIndex.TryGetValue(plantIndex, out var highlight))
			highlight.Visible = false;
	}

	private static StyleBoxFlat CreateCandidateDebugBorderStyleBox()
	{
		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.0f, 0.0f, 0.0f, 0.0f),
			BorderColor = new Color(1.0f, 0.86f, 0.22f, 0.95f)
		};
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll(2);
		return style;
	}

	private static StyleBoxFlat CreateTargetDebugHighlightStyleBox()
	{
		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.14f, 0.95f, 0.30f, 0.18f),
			BorderColor = new Color(0.24f, 1.0f, 0.36f, 1.0f)
		};
		style.SetBorderWidthAll(4);
		style.SetCornerRadiusAll(4);
		return style;
	}

	private Texture2D? LoadPlantTexture(string texturePath)
	{
		if (_plantTextureCache.TryGetValue(texturePath, out var cachedTexture))
			return cachedTexture;

		var texture = ResourceLoader.Load<Texture2D>(texturePath);
		if (texture is null)
		{
			GD.PushError($"ForestGathering: Failed to load plant texture from '{texturePath}'.");
			return null;
		}

		_plantTextureCache[texturePath] = texture;
		return texture;
	}

	private Rect2 CalculateCandidateBounds(ForestGatheringPlantEntry entry, Vector2 surfaceSize)
	{
		var fallbackBounds = new Rect2(entry.Center - (entry.Size * 0.5f), entry.Size);
		if (surfaceSize.X <= 0.0f || surfaceSize.Y <= 0.0f)
			return fallbackBounds;

		var texture = LoadPlantTexture(entry.TexturePath);
		if (texture is null)
			return fallbackBounds;

		var textureSize = texture.GetSize();
		if (textureSize.X <= 0.0f || textureSize.Y <= 0.0f)
			return fallbackBounds;

		var visualSizePixels = new Vector2(entry.Size.X * surfaceSize.X, entry.Size.Y * surfaceSize.Y);
		if (visualSizePixels.X <= 0.0f || visualSizePixels.Y <= 0.0f)
			return fallbackBounds;

		var scale = MathF.Min(visualSizePixels.X / textureSize.X, visualSizePixels.Y / textureSize.Y);
		if (scale <= 0.0f)
			return fallbackBounds;

		var drawnTextureSize = textureSize * scale;
		var drawnTextureTopLeft = new Vector2(
			(entry.Center.X * surfaceSize.X) - (drawnTextureSize.X * 0.5f),
			(entry.Center.Y * surfaceSize.Y) - (drawnTextureSize.Y * 0.5f));
		var textureContentBounds = GetPlantContentTextureBounds(entry.TexturePath);
		var contentTopLeftPixels = drawnTextureTopLeft + new Vector2(
			textureContentBounds.Position.X * textureSize.X * scale,
			textureContentBounds.Position.Y * textureSize.Y * scale);
		var contentSizePixels = new Vector2(
			textureContentBounds.Size.X * textureSize.X * scale,
			textureContentBounds.Size.Y * textureSize.Y * scale);

		return new Rect2(
			new Vector2(contentTopLeftPixels.X / surfaceSize.X, contentTopLeftPixels.Y / surfaceSize.Y),
			new Vector2(contentSizePixels.X / surfaceSize.X, contentSizePixels.Y / surfaceSize.Y));
	}

	private Rect2 GetPlantContentTextureBounds(string texturePath)
	{
		if (_plantContentBoundsCache.TryGetValue(texturePath, out var cachedBounds))
			return cachedBounds;

		var texture = LoadPlantTexture(texturePath);
		if (texture is null)
			return new Rect2(Vector2.Zero, Vector2.One);

		var image = texture.GetImage();
		if (image is null)
			return new Rect2(Vector2.Zero, Vector2.One);

		var imageWidth = image.GetWidth();
		var imageHeight = image.GetHeight();
		if (imageWidth <= 0 || imageHeight <= 0)
			return new Rect2(Vector2.Zero, Vector2.One);

		var usedRect = image.GetUsedRect();
		if (usedRect.Size.X <= 0 || usedRect.Size.Y <= 0)
			return new Rect2(Vector2.Zero, Vector2.One);

		var left = Math.Clamp(usedRect.Position.X - CandidateBoundsPaddingPixels, 0, imageWidth - 1);
		var top = Math.Clamp(usedRect.Position.Y - CandidateBoundsPaddingPixels, 0, imageHeight - 1);
		var right = Math.Clamp(usedRect.Position.X + usedRect.Size.X + CandidateBoundsPaddingPixels, left + 1, imageWidth);
		var bottom = Math.Clamp(usedRect.Position.Y + usedRect.Size.Y + CandidateBoundsPaddingPixels, top + 1, imageHeight);
		var bounds = new Rect2(
			new Vector2((float)left / imageWidth, (float)top / imageHeight),
			new Vector2((float)(right - left) / imageWidth, (float)(bottom - top) / imageHeight));
		_plantContentBoundsCache[texturePath] = bounds;
		return bounds;
	}

	private static void SetNormalizedRect(Control control, Vector2 center, Vector2 size)
	{
		var halfSize = size * 0.5f;
		control.AnchorLeft = center.X - halfSize.X;
		control.AnchorTop = center.Y - halfSize.Y;
		control.AnchorRight = center.X + halfSize.X;
		control.AnchorBottom = center.Y + halfSize.Y;
		control.OffsetLeft = 0.0f;
		control.OffsetTop = 0.0f;
		control.OffsetRight = 0.0f;
		control.OffsetBottom = 0.0f;
	}

}
