using System;
using System.Collections.Generic;
using Godot;
using OccultShop.Autoload;
using OccultShop.Infrastructure;

namespace OccultShop.UI;

public partial class Map : Control
{
	private const int ColumnCount = 15;
	private const int RowCount = 15;
	private const char FirstRowLetter = 'A';
	private const string EmptyCoordinateMessage = "Nothing of interest here";
	private const string DefaultPointOfInterestCoordinate = "F12";

	[Export] public NodePath BackButtonPath = default!;
	[Export] public NodePath HoveredCoordinateLabelPath = default!;
	[Export] public NodePath MapTextureRectPath = default!;
	[Export] public NodePath MapGridPath = default!;
	[Export] public NodePath ModalLayerPath = default!;
	[Export] public NodePath ModalTitlePath = default!;
	[Export] public NodePath ModalMessagePath = default!;
	[Export] public NodePath ModalTravelButtonPath = default!;
	[Export] public NodePath ModalCloseButtonPath = default!;
	[Export] public NodePath SaveGameManagerPath = new(AutoloadNodePaths.SaveGameManager);
	[Export] public string MapTexturePath = "res://Assets/Maps/kerry_samuel_lewis_1844_lowres.jpg";
	[Export] public string F12ScenePath = "";
	[Export] public string F12PointOfInterestMessage = "There is something of interest at F12.";

	private readonly Dictionary<string, MapPointOfInterest> _pointsOfInterest = new(StringComparer.OrdinalIgnoreCase);

	private Button _backButton = default!;
	private Label _hoveredCoordinateLabel = default!;
	private TextureRect _mapTextureRect = default!;
	private GridContainer _mapGrid = default!;
	private Control _modalLayer = default!;
	private Label _modalTitle = default!;
	private Label _modalMessage = default!;
	private Button _modalTravelButton = default!;
	private Button _modalCloseButton = default!;
	private SaveGameManager? _saveGameManager;
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
	}

	private bool ResolveNodes()
	{
		if (!NodeLookup.TryGetRequiredNode(this, BackButtonPath, nameof(Map), nameof(BackButtonPath), out _backButton))
			return false;
		if (!NodeLookup.TryGetRequiredNode(this, HoveredCoordinateLabelPath, nameof(Map), nameof(HoveredCoordinateLabelPath), out _hoveredCoordinateLabel))
			return false;
		if (!NodeLookup.TryGetRequiredNode(this, MapTextureRectPath, nameof(Map), nameof(MapTextureRectPath), out _mapTextureRect))
			return false;
		if (!NodeLookup.TryGetRequiredNode(this, MapGridPath, nameof(Map), nameof(MapGridPath), out _mapGrid))
			return false;
		if (!NodeLookup.TryGetRequiredNode(this, ModalLayerPath, nameof(Map), nameof(ModalLayerPath), out _modalLayer))
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

		_mapTextureRect.Texture = texture;
	}

	private void BuildPointsOfInterest()
	{
		_pointsOfInterest.Clear();

		// TODO: Assign F12ScenePath once the destination scene for this map clue exists.
		_pointsOfInterest[DefaultPointOfInterestCoordinate] = new MapPointOfInterest(
			DefaultPointOfInterestCoordinate,
			F12PointOfInterestMessage,
			F12ScenePath);
	}

	private void BuildMapGrid()
	{
		ClearChildren(_mapGrid);
		_mapGrid.Columns = ColumnCount + 1;

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
			TooltipText = coordinate.ToString(),
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
		button.Pressed += () => ShowCoordinateResult(coordinate);
		return button;
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
			Columns = ColumnCount + 1,
			Rows = RowCount + 1,
			ZIndex = _mapGrid.ZIndex + 1,
			MouseFilter = MouseFilterEnum.Ignore
		};
		FillParent(overlay);
		mapCanvas.AddChild(overlay);
	}

	private void SetHoveredCoordinate(MapCoordinate? coordinate)
	{
		_hoveredCoordinateLabel.Text = coordinate is null
			? "Cell: --"
			: $"Cell: {coordinate.Value}";
	}

	private void ClearHoveredCoordinate(MapCoordinate coordinate)
	{
		if (_hoveredCoordinateLabel.Text == $"Cell: {coordinate}")
			SetHoveredCoordinate(null);
	}

	private static void FillParent(Control control)
	{
		control.AnchorLeft = 0.0f;
		control.AnchorTop = 0.0f;
		control.AnchorRight = 1.0f;
		control.AnchorBottom = 1.0f;
		control.OffsetLeft = 0.0f;
		control.OffsetTop = 0.0f;
		control.OffsetRight = 0.0f;
		control.OffsetBottom = 0.0f;
	}

	private void ShowCoordinateResult(MapCoordinate coordinate)
	{
		var coordinateText = coordinate.ToString();
		if (_pointsOfInterest.TryGetValue(coordinateText, out var pointOfInterest))
		{
			ShowPointOfInterest(pointOfInterest);
			return;
		}

		_pendingTravelScenePath = null;
		_modalTitle.Text = coordinateText;
		_modalMessage.Text = EmptyCoordinateMessage;
		_modalTravelButton.Visible = false;
		ShowModal();
	}

	private void ShowPointOfInterest(MapPointOfInterest pointOfInterest)
	{
		var scenePath = pointOfInterest.ScenePath.Trim();
		var hasTravelTarget = !string.IsNullOrWhiteSpace(scenePath);

		_pendingTravelScenePath = hasTravelTarget ? scenePath : null;
		_modalTitle.Text = pointOfInterest.Coordinate;
		_modalMessage.Text = hasTravelTarget
			? pointOfInterest.Message
			: $"{pointOfInterest.Message}\n\nDestination scene not assigned yet.";
		_modalTravelButton.Visible = true;
		_modalTravelButton.Disabled = !hasTravelTarget;
		_modalTravelButton.TooltipText = hasTravelTarget
			? "Travel to this location."
			: "Assign F12ScenePath on the Map scene to enable travel.";
		ShowModal();
	}

	private void ShowModal()
	{
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

	private readonly record struct MapPointOfInterest(string Coordinate, string Message, string ScenePath);
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
