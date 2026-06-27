using System.Collections.Generic;
using Godot;
using OccultShop.Infrastructure;

namespace OccultShop.UI;

public partial class TutorialOverlay : Control
{
	private static readonly Vector2 HighlightPadding = new(8.0f, 8.0f);
	private static readonly Color ArrowColor = new(0.94f, 0.79f, 0.31f, 0.96f);
	private const float ArrowLineWidth = 4.0f;
	private const float ArrowHeadLength = 18.0f;
	private const float ArrowHeadHalfWidth = 9.0f;

	[Signal]
	public delegate void NextPressedEventHandler();

	[Signal]
	public delegate void SkipPressedEventHandler();

	[Export] public NodePath DimPath = default!;
	[Export] public NodePath HighlightPath = default!;
	[Export] public NodePath PanelPath = default!;
	[Export] public NodePath TitleLabelPath = default!;
	[Export] public NodePath BodyLabelPath = default!;
	[Export] public NodePath NextButtonPath = default!;
	[Export] public NodePath SkipButtonPath = default!;

	private ColorRect _dim = default!;
	private Control _highlight = default!;
	private Control? _secondaryHighlight;
	private Control _panel = default!;
	private Label _title = default!;
	private RichTextLabel _body = default!;
	private Button _nextButton = default!;
	private Button _skipButton = default!;
	private readonly List<ColorRect> _dynamicDimRects = new();
	private readonly List<Control> _highlightControls = new();
	private readonly List<Node> _arrowNodes = new();

	public override void _Ready()
	{
		if (!NodeLookup.TryGetRequiredNode(this, DimPath, nameof(TutorialOverlay), nameof(DimPath), out _dim))
			return;
		if (!NodeLookup.TryGetRequiredNode(this, HighlightPath, nameof(TutorialOverlay), nameof(HighlightPath), out _highlight))
			return;
		if (!NodeLookup.TryGetRequiredNode(this, PanelPath, nameof(TutorialOverlay), nameof(PanelPath), out _panel))
			return;
		if (!NodeLookup.TryGetRequiredNode(this, TitleLabelPath, nameof(TutorialOverlay), nameof(TitleLabelPath), out _title))
			return;
		if (!NodeLookup.TryGetRequiredNode(this, BodyLabelPath, nameof(TutorialOverlay), nameof(BodyLabelPath), out _body))
			return;
		if (!NodeLookup.TryGetRequiredNode(this, NextButtonPath, nameof(TutorialOverlay), nameof(NextButtonPath), out _nextButton))
			return;
		if (!NodeLookup.TryGetRequiredNode(this, SkipButtonPath, nameof(TutorialOverlay), nameof(SkipButtonPath), out _skipButton))
			return;

		MouseFilter = MouseFilterEnum.Ignore;
		_dim.MouseFilter = MouseFilterEnum.Ignore;
		_secondaryHighlight = GetNodeOrNull<Control>("SecondaryHighlight");
		_highlight.MouseFilter = MouseFilterEnum.Ignore;
		if (_secondaryHighlight is not null)
			_secondaryHighlight.MouseFilter = MouseFilterEnum.Ignore;
		_panel.MouseFilter = MouseFilterEnum.Stop;
		_body.BbcodeEnabled = true;
		_dim.ZIndex = 0;
		_highlight.ZIndex = 1;
		if (_secondaryHighlight is not null)
			_secondaryHighlight.ZIndex = 1;
		_panel.ZIndex = 2;
		_highlightControls.Add(_highlight);
		if (_secondaryHighlight is not null)
			_highlightControls.Add(_secondaryHighlight);

		_nextButton.Pressed += OnNextButtonPressed;
		_skipButton.Pressed += OnSkipButtonPressed;

		SetNextButtonText("Next");
		SetNextButtonVisible(true);
		SetSkipButtonVisible(true);
		HideOverlay();
	}

	public override void _ExitTree()
	{
		if (_nextButton is not null)
			_nextButton.Pressed -= OnNextButtonPressed;
		if (_skipButton is not null)
			_skipButton.Pressed -= OnSkipButtonPressed;
	}

	public void ShowMessage(string title, string body)
	{
		ClearHighlight();
		ShowPanel(title, body);
	}

	public void ShowMessageWithoutDim(string title, string body)
	{
		ClearHighlight();
		_dim.Visible = false;
		ShowPanel(title, body);
	}

	public void ShowForTarget(string title, string body, Control? targetControl)
	{
		if (targetControl is null)
		{
			ShowMessage(title, body);
			return;
		}

		ShowWithHighlight(title, body, targetControl.GetGlobalRect());
	}

	public void ShowForTargets(string title, string body, params Control?[] targetControls)
	{
		var globalRects = new List<Rect2>();
		foreach (var targetControl in targetControls)
		{
			if (targetControl is null)
				continue;

			globalRects.Add(targetControl.GetGlobalRect());
		}

		if (globalRects.Count == 0)
		{
			ShowMessage(title, body);
			return;
		}

		ShowWithHighlights(title, body, globalRects);
	}

	public void ShowForTargetsWithArrow(string title, string body, Control? fromControl, Control? toControl, params Control?[] targetControls)
	{
		if (fromControl is null || toControl is null)
		{
			ShowForTargets(title, body, targetControls);
			return;
		}

		var globalRects = new List<Rect2>
		{
			fromControl.GetGlobalRect(),
			toControl.GetGlobalRect()
		};
		foreach (var targetControl in targetControls)
		{
			if (targetControl is null)
				continue;

			globalRects.Add(targetControl.GetGlobalRect());
		}

		SetHighlightRects(globalRects);
		SetArrow(fromControl.GetGlobalRect(), toControl.GetGlobalRect());
		ShowPanel(title, body);
	}

	public void ShowWithHighlight(string title, string body, Rect2 globalRect)
	{
		SetHighlightRects(new List<Rect2> { globalRect });
		ShowPanel(title, body);
	}

	public void ShowWithHighlights(string title, string body, IReadOnlyList<Rect2> globalRects)
	{
		SetHighlightRects(globalRects);
		ShowPanel(title, body);
	}

	public void HideOverlay()
	{
		Visible = false;
		ClearHighlight();
	}

	public void SetNextButtonText(string text)
	{
		_nextButton.Text = string.IsNullOrWhiteSpace(text) ? "Next" : text;
	}

	public void SetNextButtonVisible(bool visible)
	{
		_nextButton.Visible = visible;
	}

	public void SetNextButtonEnabled(bool enabled)
	{
		_nextButton.Disabled = !enabled;
	}

	public void SetSkipButtonVisible(bool visible)
	{
		_skipButton.Visible = visible;
	}

	public void PlacePanelAtTop()
	{
		SetPanelVerticalPlacement(0.0f, 32.0f, 196.0f);
	}

	public void PlacePanelAtBottom()
	{
		SetPanelVerticalPlacement(1.0f, -196.0f, -32.0f);
	}

	private void ShowPanel(string title, string body)
	{
		_title.Text = title;
		_body.Text = body;
		Visible = true;
		MoveToFront();

		if (_nextButton.Visible && !_nextButton.Disabled)
		{
			_nextButton.GrabFocus();
			return;
		}

		if (_skipButton.Visible)
			_skipButton.GrabFocus();
	}

	private void SetPanelVerticalPlacement(float anchorY, float offsetTop, float offsetBottom)
	{
		_panel.AnchorTop = anchorY;
		_panel.AnchorBottom = anchorY;
		_panel.OffsetTop = offsetTop;
		_panel.OffsetBottom = offsetBottom;
	}

	private void SetHighlightRects(IReadOnlyList<Rect2> globalRects)
	{
		ClearArrows();
		_dim.Visible = false;

		var highlightRects = new List<Rect2>();
		foreach (var globalRect in globalRects)
		{
			var localPosition = GetGlobalTransformWithCanvas().AffineInverse() * globalRect.Position;
			var paddedSize = new Vector2(
				Mathf.Max(0.0f, globalRect.Size.X + HighlightPadding.X * 2.0f),
				Mathf.Max(0.0f, globalRect.Size.Y + HighlightPadding.Y * 2.0f));

			highlightRects.Add(new Rect2(localPosition - HighlightPadding, paddedSize));
		}

		UpdateDimCutouts(highlightRects);

		for (var i = 0; i < _highlightControls.Count; i++)
		{
			var highlightControl = _highlightControls[i];
			if (i >= highlightRects.Count)
			{
				HideHighlightControl(highlightControl);
				continue;
			}

			SetHighlightControlRect(highlightControl, highlightRects[i]);
		}
	}

	private static void SetHighlightControlRect(Control highlightControl, Rect2 highlightRect)
	{
		highlightControl.Position = highlightRect.Position;
		highlightControl.CustomMinimumSize = Vector2.Zero;
		highlightControl.Size = highlightRect.Size;
		highlightControl.CustomMinimumSize = highlightRect.Size;
		highlightControl.Visible = true;
	}

	private void ClearHighlight()
	{
		if (_highlight is null)
			return;

		ClearArrows();
		_dim.Visible = true;
		HideDynamicDimRects();
		foreach (var highlightControl in _highlightControls)
			HideHighlightControl(highlightControl);
	}

	private void SetArrow(Rect2 fromGlobalRect, Rect2 toGlobalRect)
	{
		ClearArrows();

		var start = ToOverlayLocal(GetRectCenter(fromGlobalRect));
		var end = ToOverlayLocal(GetRectCenter(toGlobalRect));
		var vector = end - start;
		if (vector.Length() < 1.0f)
			return;

		var direction = vector.Normalized();
		var lineEnd = end - direction * (ArrowHeadLength * 0.55f);
		var line = new Line2D
		{
			Width = ArrowLineWidth,
			DefaultColor = ArrowColor,
			Points = new[] { start, lineEnd },
			ZIndex = 1
		};

		var arrowHead = new Polygon2D
		{
			Color = ArrowColor,
			Position = end,
			Rotation = direction.Angle(),
			Polygon = new[]
			{
				Vector2.Zero,
				new Vector2(-ArrowHeadLength, -ArrowHeadHalfWidth),
				new Vector2(-ArrowHeadLength, ArrowHeadHalfWidth)
			},
			ZIndex = 1
		};

		AddChild(line);
		AddChild(arrowHead);
		_arrowNodes.Add(line);
		_arrowNodes.Add(arrowHead);
	}

	private void ClearArrows()
	{
		foreach (var arrowNode in _arrowNodes)
		{
			if (GodotObject.IsInstanceValid(arrowNode))
				arrowNode.QueueFree();
		}

		_arrowNodes.Clear();
	}

	private Vector2 ToOverlayLocal(Vector2 globalPoint)
	{
		return GetGlobalTransformWithCanvas().AffineInverse() * globalPoint;
	}

	private static Vector2 GetRectCenter(Rect2 rect)
	{
		return rect.Position + rect.Size * 0.5f;
	}

	private static void HideHighlightControl(Control highlightControl)
	{
		highlightControl.Visible = false;
		highlightControl.Size = Vector2.Zero;
		highlightControl.CustomMinimumSize = Vector2.Zero;
	}

	private void UpdateDimCutouts(IReadOnlyList<Rect2> highlightRects)
	{
		HideDynamicDimRects();

		var overlaySize = GetOverlaySize();
		var cutouts = new List<Rect2>();
		foreach (var highlightRect in highlightRects)
		{
			var left = Mathf.Clamp(highlightRect.Position.X, 0.0f, overlaySize.X);
			var top = Mathf.Clamp(highlightRect.Position.Y, 0.0f, overlaySize.Y);
			var right = Mathf.Clamp(highlightRect.Position.X + highlightRect.Size.X, 0.0f, overlaySize.X);
			var bottom = Mathf.Clamp(highlightRect.Position.Y + highlightRect.Size.Y, 0.0f, overlaySize.Y);
			if (right <= left || bottom <= top)
				continue;

			cutouts.Add(new Rect2(new Vector2(left, top), new Vector2(right - left, bottom - top)));
		}

		if (cutouts.Count == 0)
		{
			_dim.Visible = true;
			return;
		}

		_dim.Visible = false;

		var xBoundaries = new List<float> { 0.0f, overlaySize.X };
		var yBoundaries = new List<float> { 0.0f, overlaySize.Y };
		foreach (var cutout in cutouts)
		{
			AddBoundary(xBoundaries, cutout.Position.X);
			AddBoundary(xBoundaries, cutout.Position.X + cutout.Size.X);
			AddBoundary(yBoundaries, cutout.Position.Y);
			AddBoundary(yBoundaries, cutout.Position.Y + cutout.Size.Y);
		}

		xBoundaries.Sort();
		yBoundaries.Sort();

		var dimIndex = 0;
		for (var yIndex = 0; yIndex < yBoundaries.Count - 1; yIndex++)
		{
			var y = yBoundaries[yIndex];
			var height = yBoundaries[yIndex + 1] - y;
			if (height <= 0.0f)
				continue;

			for (var xIndex = 0; xIndex < xBoundaries.Count - 1; xIndex++)
			{
				var x = xBoundaries[xIndex];
				var width = xBoundaries[xIndex + 1] - x;
				if (width <= 0.0f)
					continue;

				var center = new Vector2(x + width * 0.5f, y + height * 0.5f);
				if (IsInsideAnyCutout(center, cutouts))
					continue;

				SetDimRect(GetOrCreateDynamicDimRect(dimIndex), new Rect2(new Vector2(x, y), new Vector2(width, height)));
				dimIndex++;
			}
		}

		for (var i = dimIndex; i < _dynamicDimRects.Count; i++)
			SetDimRect(_dynamicDimRects[i], new Rect2());
	}

	private Vector2 GetOverlaySize()
	{
		var overlaySize = Size;
		if (overlaySize.X <= 0.0f || overlaySize.Y <= 0.0f)
			overlaySize = GetViewportRect().Size;

		return overlaySize;
	}

	private ColorRect GetOrCreateDynamicDimRect(int index)
	{
		while (_dynamicDimRects.Count <= index)
		{
			var dimRect = new ColorRect
			{
				Color = _dim.Color,
				MouseFilter = MouseFilterEnum.Ignore,
				ZIndex = 0
			};
			AddChild(dimRect);
			_dynamicDimRects.Add(dimRect);
		}

		return _dynamicDimRects[index];
	}

	private void HideDynamicDimRects()
	{
		foreach (var dimRect in _dynamicDimRects)
			SetDimRect(dimRect, new Rect2());
	}

	private static void AddBoundary(List<float> boundaries, float value)
	{
		foreach (var boundary in boundaries)
		{
			if (Mathf.Abs(boundary - value) < 0.5f)
				return;
		}

		boundaries.Add(value);
	}

	private static bool IsInsideAnyCutout(Vector2 point, IReadOnlyList<Rect2> cutouts)
	{
		foreach (var cutout in cutouts)
		{
			if (cutout.HasPoint(point))
				return true;
		}

		return false;
	}

	private static void SetDimRect(ColorRect? dimRect, Rect2 rect)
	{
		if (dimRect is null)
			return;

		dimRect.Position = rect.Position;
		dimRect.Size = rect.Size;
		dimRect.Visible = rect.Size.X > 0.0f && rect.Size.Y > 0.0f;
	}

	private void OnNextButtonPressed()
	{
		EmitSignal(SignalName.NextPressed);
	}

	private void OnSkipButtonPressed()
	{
		EmitSignal(SignalName.SkipPressed);
	}

}
