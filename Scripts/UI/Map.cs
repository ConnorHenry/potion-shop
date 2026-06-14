using System;
using System.Collections.Generic;
using Godot;
using OccultShop.Autoload;
using OccultShop.Infrastructure;

namespace OccultShop.UI;

public partial class Map : Control
{
	private const int ColumnCount = 30;
	private const char FirstRowLetter = 'A';
	private const char LastRowLetter = 'Q';
	private const string EmptyCoordinateMessage = "Nothing of interest here";
	private const string DefaultPointOfInterestCoordinate = "F12";
	private const string JuniperPointOfInterestCoordinate = "K17";
	private const float HoveredCoordinateOffsetX = 18.0f;
	private const float HoveredCoordinateOffsetY = 22.0f;
	private const float HoveredCoordinateScreenPadding = 8.0f;
	private const float CompactModalHalfWidth = 190.0f;
	private const float CompactModalHalfHeight = 94.0f;
	private const float CompactModalMessageMinimumHeight = 58.0f;
	private const float PreviewModalMessageMinimumHeight = 200.0f;
	private const int GridColumnSlotCount = ColumnCount + 1;
	private static int RowCount => LastRowLetter - FirstRowLetter + 1;
	private static int GridRowSlotCount => RowCount + 1;

	[Export] public NodePath BackButtonPath = default!;
	[Export] public NodePath HoveredCoordinateLabelPath = default!;
	[Export] public NodePath MapArtworkPath = default!;
	[Export] public NodePath MapGridPath = default!;
	[Export] public NodePath ModalLayerPath = default!;
	[Export] public NodePath ModalDialogPath = default!;
	[Export] public NodePath ModalTilePreviewPath = default!;
	[Export] public NodePath ModalTitlePath = default!;
	[Export] public NodePath ModalMessagePath = default!;
	[Export] public NodePath ModalTravelButtonPath = default!;
	[Export] public NodePath ModalCloseButtonPath = default!;
	[Export] public NodePath SaveGameManagerPath = new(AutoloadNodePaths.SaveGameManager);
	[Export] public string MapTexturePath = "res://Assets/Maps/kerry_parchment_map_hires.png";
	[Export] public string F12ScenePath = ScenePaths.ForestGathering;
	[Export] public string F12PointOfInterestMessage = "A damp forest edge is marked here. The undergrowth may hold useful mint.";
	[Export] public string F12PreviewTexturePath = "res://Assets/Maps/CellPreviews/map_popup_f12_parchment_sketch.png";
	[Export] public string K17ScenePath = ScenePaths.JuniperGathering;
	[Export] public string K17PointOfInterestMessage = "Juniper bushes grow thick here. Ripe berries can be gathered with care.";
	[Export] public string K17PreviewTexturePath = "res://Assets/Maps/CellPreviews/map_popup_k17_parchment_sketch.png";

	private readonly Dictionary<string, MapPointOfInterest> _pointsOfInterest = new(StringComparer.OrdinalIgnoreCase);

	private Button _backButton = default!;
	private Label _hoveredCoordinateLabel = default!;
	private Control _mapCanvas = default!;
	private TextureRect _mapArtwork = default!;
	private GridContainer _mapGrid = default!;
	private MapGridLineOverlay? _gridLineOverlay;
	private Control _modalLayer = default!;
	private Control _modalDialog = default!;
	private TextureRect _modalTilePreview = default!;
	private Label _modalTitle = default!;
	private Label _modalMessage = default!;
	private Button _modalTravelButton = default!;
	private Button _modalCloseButton = default!;
	private SaveGameManager? _saveGameManager;
	private MapCoordinate? _hoveredCoordinate;
	private string? _pendingTravelScenePath;

	public override void _Ready()
	{
		if (!ResolveNodes())
			return;

		_backButton.Pressed += OnBackPressed;
		_modalTravelButton.Pressed += OnTravelPressed;
		_modalCloseButton.Pressed += HideModal;
		_modalLayer.Visible = false;

		_saveGameManager = GetNodeOrNull<SaveGameManager>(SaveGameManagerPath);
		if (_saveGameManager is null)
			GD.PushError($"Map: SaveGameManager was not found at '{SaveGameManagerPath}'.");

		LoadMapTexture();
		BuildPointsOfInterest();
		BuildMapGrid();
		AddDottedGridLineOverlay();
		_mapCanvas.Resized += LayoutMapLayersToCanvas;
		LayoutMapLayersToCanvas();
		SetHoveredCoordinate(null);
		TryAutoSave("entering the map");
	}

	public override void _ExitTree()
	{
		if (_backButton is not null)
			_backButton.Pressed -= OnBackPressed;
		if (_modalTravelButton is not null)
			_modalTravelButton.Pressed -= OnTravelPressed;
		if (_modalCloseButton is not null)
			_modalCloseButton.Pressed -= HideModal;
		if (_mapCanvas is not null)
			_mapCanvas.Resized -= LayoutMapLayersToCanvas;
	}

	private bool ResolveNodes()
	{
		if (!NodeLookup.TryGetRequiredNode(this, BackButtonPath, nameof(Map), nameof(BackButtonPath), out _backButton))
			return false;
		if (!NodeLookup.TryGetRequiredNode(this, HoveredCoordinateLabelPath, nameof(Map), nameof(HoveredCoordinateLabelPath), out _hoveredCoordinateLabel))
			return false;
		if (!NodeLookup.TryGetRequiredNode(this, MapArtworkPath, nameof(Map), nameof(MapArtworkPath), out _mapArtwork))
			return false;
		if (!NodeLookup.TryGetRequiredNode(this, MapGridPath, nameof(Map), nameof(MapGridPath), out _mapGrid))
			return false;
		if (_mapGrid.GetParent() is not Control mapCanvas)
		{
			GD.PushError("Map: MapGrid parent must be a Control so the grid can be laid out.");
			return false;
		}

		_mapCanvas = mapCanvas;
		if (!NodeLookup.TryGetRequiredNode(this, ModalLayerPath, nameof(Map), nameof(ModalLayerPath), out _modalLayer))
			return false;
		if (!NodeLookup.TryGetRequiredNode(this, ModalDialogPath, nameof(Map), nameof(ModalDialogPath), out _modalDialog))
			return false;
		if (!NodeLookup.TryGetRequiredNode(this, ModalTilePreviewPath, nameof(Map), nameof(ModalTilePreviewPath), out _modalTilePreview))
			return false;
		if (!NodeLookup.TryGetRequiredNode(this, ModalTitlePath, nameof(Map), nameof(ModalTitlePath), out _modalTitle))
			return false;
		if (!NodeLookup.TryGetRequiredNode(this, ModalMessagePath, nameof(Map), nameof(ModalMessagePath), out _modalMessage))
			return false;
		if (!NodeLookup.TryGetRequiredNode(this, ModalTravelButtonPath, nameof(Map), nameof(ModalTravelButtonPath), out _modalTravelButton))
			return false;
		if (!NodeLookup.TryGetRequiredNode(this, ModalCloseButtonPath, nameof(Map), nameof(ModalCloseButtonPath), out _modalCloseButton))
			return false;

		return true;
	}

	private void LoadMapTexture()
	{
		if (string.IsNullOrWhiteSpace(MapTexturePath))
		{
			GD.PushError("Map: MapTexturePath is not assigned.");
			return;
		}

		var texture = ResourceLoader.Load<Texture2D>(MapTexturePath);
		if (texture is null)
		{
			GD.PushError($"Map: Failed to load map texture from '{MapTexturePath}'.");
			return;
		}

		_mapArtwork.Texture = texture;
	}

	private void BuildPointsOfInterest()
	{
		_pointsOfInterest.Clear();

		_pointsOfInterest[DefaultPointOfInterestCoordinate] = new MapPointOfInterest(
			DefaultPointOfInterestCoordinate,
			F12PointOfInterestMessage,
			F12ScenePath,
			F12PreviewTexturePath);

		_pointsOfInterest[JuniperPointOfInterestCoordinate] = new MapPointOfInterest(
			JuniperPointOfInterestCoordinate,
			K17PointOfInterestMessage,
			K17ScenePath,
			K17PreviewTexturePath);
	}

	private void BuildMapGrid()
	{
		ClearChildren(_mapGrid);
		_mapGrid.Columns = GridColumnSlotCount;

		var labelStyle = CreateLabelStyle();
		var normalCellStyle = CreateCellStyle(new Color(0.93f, 0.82f, 0.57f, 0.0f));
		var hoverCellStyle = CreateCellStyle(new Color(0.98f, 0.82f, 0.34f, 0.34f));
		var pressedCellStyle = CreateCellStyle(new Color(0.70f, 0.48f, 0.24f, 0.38f));

		_mapGrid.AddChild(CreateHeaderCell("", labelStyle));
		for (var column = 1; column <= ColumnCount; column++)
			_mapGrid.AddChild(CreateHeaderCell(column.ToString(), labelStyle));

		for (var row = 0; row < RowCount; row++)
		{
			var rowLetter = (char)(FirstRowLetter + row);
			_mapGrid.AddChild(CreateHeaderCell(rowLetter.ToString(), labelStyle));

			for (var column = 1; column <= ColumnCount; column++)
			{
				var coordinate = new MapCoordinate(rowLetter, column);
				_mapGrid.AddChild(CreateMapCellButton(coordinate, normalCellStyle, hoverCellStyle, pressedCellStyle));
			}
		}
	}

	private Button CreateMapCellButton(
		MapCoordinate coordinate,
		StyleBoxFlat normalStyle,
		StyleBoxFlat hoverStyle,
		StyleBoxFlat pressedStyle)
	{
		var button = new Button
		{
			Text = "",
			Flat = false,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			FocusMode = FocusModeEnum.None,
			MouseDefaultCursorShape = CursorShape.PointingHand
		};

		button.AddThemeStyleboxOverride("normal", normalStyle);
		button.AddThemeStyleboxOverride("hover", hoverStyle);
		button.AddThemeStyleboxOverride("pressed", pressedStyle);
		button.AddThemeStyleboxOverride("focus", normalStyle);
		button.MouseEntered += () => SetHoveredCoordinate(coordinate);
		button.MouseExited += () => ClearHoveredCoordinate(coordinate);
		button.GuiInput += @event => OnMapCellGuiInput(coordinate, @event);
		button.Pressed += () => ShowCoordinateResult(coordinate);
		return button;
	}

	private void OnMapCellGuiInput(MapCoordinate coordinate, InputEvent @event)
	{
		if (@event is InputEventMouseMotion mouseMotion)
			SetHoveredCoordinate(coordinate, mouseMotion.GlobalPosition);
	}

	private Label CreateHeaderCell(string text, StyleBoxFlat labelStyle)
	{
		var label = new Label
		{
			Text = text,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};

		label.AddThemeColorOverride("font_color", new Color(0.10f, 0.07f, 0.04f, 0.95f));
		label.AddThemeFontSizeOverride("font_size", 19);
		label.AddThemeStyleboxOverride("normal", labelStyle);
		return label;
	}

	private static StyleBoxFlat CreateLabelStyle()
	{
		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.86f, 0.67f, 0.38f, 0.18f),
			BorderColor = new Color(0.16f, 0.10f, 0.04f, 0.0f)
		};
		style.SetBorderWidthAll(0);
		style.SetCornerRadiusAll(0);
		return style;
	}

	private static StyleBoxFlat CreateCellStyle(Color backgroundColor)
	{
		var style = new StyleBoxFlat
		{
			BgColor = backgroundColor,
			BorderColor = new Color(0.0f, 0.0f, 0.0f, 0.0f)
		};
		style.SetBorderWidthAll(0);
		style.SetCornerRadiusAll(0);
		return style;
	}

	private void AddDottedGridLineOverlay()
	{
		var mapCanvas = _mapGrid.GetParent();
		if (mapCanvas is null)
		{
			GD.PushError("Map: Cannot draw dotted grid lines because MapGrid has no parent.");
			return;
		}

		var overlay = new MapGridLineOverlay
		{
			Name = "DottedGridLines",
			Columns = GridColumnSlotCount,
			Rows = GridRowSlotCount,
			ZIndex = _mapGrid.ZIndex + 1,
			MouseFilter = MouseFilterEnum.Ignore
		};
		_gridLineOverlay = overlay;
		mapCanvas.AddChild(overlay);
	}

	private void LayoutMapLayersToCanvas()
	{
		if (_mapCanvas is null || _mapArtwork is null || _mapGrid is null)
			return;
		if (_mapCanvas.Size.X <= 0.0f || _mapCanvas.Size.Y <= 0.0f)
			return;
		if (!TryCalculateMapLayoutRects(out var artworkRect, out var gridRect))
			return;

		SetControlRect(_mapArtwork, artworkRect);
		SetControlRect(_mapGrid, gridRect);
		if (_gridLineOverlay is not null)
			SetControlRect(_gridLineOverlay, gridRect);

		var sepiaTint = _mapCanvas.GetNodeOrNull<Control>("SepiaTint");
		if (sepiaTint is not null)
			SetControlRect(sepiaTint, artworkRect);
	}

	private bool TryCalculateMapLayoutRects(out Rect2 artworkRect, out Rect2 gridRect)
	{
		artworkRect = default;
		gridRect = default;

		if (_mapArtwork.Texture is null)
			return false;

		var textureSize = _mapArtwork.Texture.GetSize();
		if (textureSize.X <= 0.0f || textureSize.Y <= 0.0f)
			return false;

		var artworkAspect = textureSize.X / textureSize.Y;
		var gridWidthScale = (float)GridColumnSlotCount / ColumnCount;
		var gridHeightScale = (float)GridRowSlotCount / RowCount;
		var artworkHeight = Mathf.Min(
			_mapCanvas.Size.X / (artworkAspect * gridWidthScale),
			_mapCanvas.Size.Y / gridHeightScale);

		if (artworkHeight <= 0.0f)
			return false;

		var artworkSize = new Vector2(artworkHeight * artworkAspect, artworkHeight);
		var headerSize = new Vector2(artworkSize.X / ColumnCount, artworkSize.Y / RowCount);
		var gridSize = artworkSize + headerSize;
		var gridPosition = new Vector2(
			Mathf.Round((_mapCanvas.Size.X - gridSize.X) * 0.5f),
			Mathf.Round((_mapCanvas.Size.Y - gridSize.Y) * 0.5f));

		gridRect = new Rect2(gridPosition, gridSize);
		artworkRect = new Rect2(gridPosition + headerSize, artworkSize);
		return true;
	}

	private void SetHoveredCoordinate(MapCoordinate? coordinate)
	{
		SetHoveredCoordinate(coordinate, GetGlobalMousePosition());
	}

	private void SetHoveredCoordinate(MapCoordinate? coordinate, Vector2 cursorGlobalPosition)
	{
		_hoveredCoordinate = coordinate;

		if (coordinate is null)
		{
			_hoveredCoordinateLabel.Visible = false;
			_hoveredCoordinateLabel.Text = "";
			return;
		}

		_hoveredCoordinateLabel.Text = coordinate.Value.ToString();
		_hoveredCoordinateLabel.Visible = true;
		PositionHoveredCoordinateLabel(cursorGlobalPosition);
	}

	private void ClearHoveredCoordinate(MapCoordinate coordinate)
	{
		if (_hoveredCoordinate.HasValue && _hoveredCoordinate.Value.Equals(coordinate))
			SetHoveredCoordinate(null);
	}

	private void PositionHoveredCoordinateLabel(Vector2 cursorGlobalPosition)
	{
		var labelSize = _hoveredCoordinateLabel.GetCombinedMinimumSize();
		if (labelSize.X <= 0.0f || labelSize.Y <= 0.0f)
			labelSize = _hoveredCoordinateLabel.Size;
		_hoveredCoordinateLabel.Size = labelSize;

		var viewportSize = GetViewport().GetVisibleRect().Size;
		var position = cursorGlobalPosition + new Vector2(HoveredCoordinateOffsetX, HoveredCoordinateOffsetY);
		position.X = Mathf.Clamp(
			position.X,
			HoveredCoordinateScreenPadding,
			Mathf.Max(HoveredCoordinateScreenPadding, viewportSize.X - labelSize.X - HoveredCoordinateScreenPadding));
		position.Y = Mathf.Clamp(
			position.Y,
			HoveredCoordinateScreenPadding,
			Mathf.Max(HoveredCoordinateScreenPadding, viewportSize.Y - labelSize.Y - HoveredCoordinateScreenPadding));

		_hoveredCoordinateLabel.GlobalPosition = position;
	}

	private static void SetControlRect(Control control, Rect2 rect)
	{
		control.AnchorLeft = 0.0f;
		control.AnchorTop = 0.0f;
		control.AnchorRight = 0.0f;
		control.AnchorBottom = 0.0f;
		control.OffsetLeft = rect.Position.X;
		control.OffsetTop = rect.Position.Y;
		control.OffsetRight = rect.Position.X + rect.Size.X;
		control.OffsetBottom = rect.Position.Y + rect.Size.Y;
	}

	private void ShowCoordinateResult(MapCoordinate coordinate)
	{
		var coordinateText = coordinate.ToString();
		if (_pointsOfInterest.TryGetValue(coordinateText, out var pointOfInterest))
		{
			ShowPointOfInterest(coordinate, pointOfInterest);
			return;
		}

		_pendingTravelScenePath = null;
		HideModalTilePreview();
		UseCompactModalLayout();
		_modalTitle.Text = coordinateText;
		_modalMessage.Text = EmptyCoordinateMessage;
		_modalTravelButton.Visible = false;
		ShowModal();
	}

	private void ShowPointOfInterest(MapCoordinate coordinate, MapPointOfInterest pointOfInterest)
	{
		var scenePath = pointOfInterest.ScenePath.Trim();
		var hasTravelTarget = !string.IsNullOrWhiteSpace(scenePath);

		_pendingTravelScenePath = hasTravelTarget ? scenePath : null;
		SetModalTilePreview(coordinate, pointOfInterest);
		UsePreviewModalLayout();
		_modalTitle.Text = pointOfInterest.Coordinate;
		_modalMessage.Text = hasTravelTarget
			? pointOfInterest.Message
			: $"{pointOfInterest.Message}\n\nDestination scene not assigned yet.";
		_modalTravelButton.Visible = true;
		_modalTravelButton.Disabled = !hasTravelTarget;
		_modalTravelButton.TooltipText = hasTravelTarget
			? "Travel to this location."
			: "Assign a scene path on the Map scene to enable travel.";
		ShowModal();
	}

	private void SetModalTilePreview(MapCoordinate coordinate, MapPointOfInterest pointOfInterest)
	{
		var previewTexturePath = pointOfInterest.PreviewTexturePath.Trim();
		if (!string.IsNullOrWhiteSpace(previewTexturePath))
		{
			var previewTexture = ResourceLoader.Load<Texture2D>(previewTexturePath);
			if (previewTexture is not null)
			{
				_modalTilePreview.Texture = previewTexture;
				_modalTilePreview.Visible = true;
				return;
			}

			GD.PushError($"Map: Failed to load point-of-interest preview texture from '{previewTexturePath}'.");
		}

		SetModalTilePreviewFromMapCrop(coordinate);
	}

	private void SetModalTilePreviewFromMapCrop(MapCoordinate coordinate)
	{
		if (_mapArtwork.Texture is null)
		{
			_modalTilePreview.Texture = null;
			_modalTilePreview.Visible = false;
			GD.PushError("Map: Cannot show tile preview because the map texture is missing.");
			return;
		}

		var tileRegion = CalculateTileSourceRegion(coordinate, _mapArtwork.Texture.GetSize());
		_modalTilePreview.Texture = new AtlasTexture
		{
			Atlas = _mapArtwork.Texture,
			Region = tileRegion
		};
		_modalTilePreview.Visible = true;
	}

	private void HideModalTilePreview()
	{
		_modalTilePreview.Texture = null;
		_modalTilePreview.Visible = false;
	}

	private void UseCompactModalLayout()
	{
		SetModalMessageMinimumHeight(CompactModalMessageMinimumHeight);
		_modalDialog.AnchorLeft = 0.5f;
		_modalDialog.AnchorTop = 0.5f;
		_modalDialog.AnchorRight = 0.5f;
		_modalDialog.AnchorBottom = 0.5f;
		_modalDialog.OffsetLeft = -CompactModalHalfWidth;
		_modalDialog.OffsetTop = -CompactModalHalfHeight;
		_modalDialog.OffsetRight = CompactModalHalfWidth;
		_modalDialog.OffsetBottom = CompactModalHalfHeight;
	}

	private void UsePreviewModalLayout()
	{
		SetModalMessageMinimumHeight(PreviewModalMessageMinimumHeight);
		_modalDialog.AnchorLeft = 0.25f;
		_modalDialog.AnchorTop = 0.25f;
		_modalDialog.AnchorRight = 0.75f;
		_modalDialog.AnchorBottom = 0.75f;
		_modalDialog.OffsetLeft = 0.0f;
		_modalDialog.OffsetTop = 0.0f;
		_modalDialog.OffsetRight = 0.0f;
		_modalDialog.OffsetBottom = 0.0f;
	}

	private void SetModalMessageMinimumHeight(float height)
	{
		_modalMessage.CustomMinimumSize = new Vector2(_modalMessage.CustomMinimumSize.X, height);
	}

	private static Rect2 CalculateTileSourceRegion(MapCoordinate coordinate, Vector2 textureSize)
	{
		var columnIndex = coordinate.Column - 1;
		var rowIndex = coordinate.Row - FirstRowLetter;
		var tileSize = new Vector2(textureSize.X / ColumnCount, textureSize.Y / RowCount);
		return new Rect2(columnIndex * tileSize.X, rowIndex * tileSize.Y, tileSize);
	}

	private void ShowModal()
	{
		SetHoveredCoordinate(null);
		_modalLayer.Visible = true;
		_modalLayer.MoveToFront();
		_modalCloseButton.GrabFocus();
	}

	private void HideModal()
	{
		_pendingTravelScenePath = null;
		_modalLayer.Visible = false;
	}

	private void OnTravelPressed()
	{
		if (string.IsNullOrWhiteSpace(_pendingTravelScenePath))
		{
			GD.PushError("Map: Cannot travel because no destination scene path is assigned.");
			return;
		}

		TryAutoSave("travelling from the map");
		Error error = GetTree().ChangeSceneToFile(_pendingTravelScenePath);
		if (error != Error.Ok)
			GD.PushError($"Map: Failed to load map destination '{_pendingTravelScenePath}'. Error: {error}");
	}

	private void OnBackPressed()
	{
		TryAutoSave("leaving the map");
		Error error = GetTree().ChangeSceneToFile(ScenePaths.Main);
		if (error != Error.Ok)
		{
			GD.PushError($"Map: Failed to load main scene. Error: {error}");
		}
	}

	private bool TryAutoSave(string context)
	{
		if (_saveGameManager is null)
		{
			GD.PushError($"Map: Cannot auto-save while {context} because SaveGameManager is missing.");
			return false;
		}

		var saveSucceeded = _saveGameManager.SaveGame();
		if (!saveSucceeded)
			GD.PushError($"Map: Auto-save failed while {context}.");

		return saveSucceeded;
	}

	private static void ClearChildren(Node parent)
	{
		foreach (var child in parent.GetChildren())
		{
			parent.RemoveChild(child);
			child.QueueFree();
		}
	}

	private readonly record struct MapCoordinate(char Row, int Column)
	{
		public override string ToString()
		{
			return $"{Row}{Column}";
		}
	}

	private readonly record struct MapPointOfInterest(string Coordinate, string Message, string ScenePath, string PreviewTexturePath);
}

public partial class MapGridLineOverlay : Control
{
	private const float DotSpacing = 8.0f;
	private const float DotRadius = 1.15f;
	private static readonly Color GridLineColor = new(0.10f, 0.07f, 0.04f, 0.72f);

	public int Columns { get; set; } = 1;
	public int Rows { get; set; } = 1;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		Resized += QueueRedraw;
		QueueRedraw();
	}

	public override void _ExitTree()
	{
		Resized -= QueueRedraw;
	}

	public override void _Draw()
	{
		if (Columns <= 0 || Rows <= 0 || Size.X <= 0.0f || Size.Y <= 0.0f)
			return;

		var columnWidth = Size.X / Columns;
		for (var column = 0; column <= Columns; column++)
		{
			var x = Mathf.Round(column * columnWidth);
			DrawDottedLine(new Vector2(x, 0.0f), new Vector2(x, Size.Y));
		}

		var rowHeight = Size.Y / Rows;
		for (var row = 0; row <= Rows; row++)
		{
			var y = Mathf.Round(row * rowHeight);
			DrawDottedLine(new Vector2(0.0f, y), new Vector2(Size.X, y));
		}
	}

	private void DrawDottedLine(Vector2 start, Vector2 end)
	{
		var length = start.DistanceTo(end);
		if (length <= 0.0f)
			return;

		var direction = (end - start).Normalized();
		for (var distance = 0.0f; distance <= length; distance += DotSpacing)
			DrawCircle(start + direction * distance, DotRadius, GridLineColor);
	}
}
