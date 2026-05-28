using System.Collections.Generic;
using Godot;

namespace OccultShop.UI;

public partial class TutorialOverlay : Control
{
	private static readonly Vector2 HighlightPadding = new(8.0f, 8.0f);

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
	private ColorRect? _dimTop;
	private ColorRect? _dimBottom;
	private ColorRect? _dimLeft;
	private ColorRect? _dimRight;
	private Control _highlight = default!;
	private Control? _secondaryHighlight;
	private Control _panel = default!;
	private Label _title = default!;
	private RichTextLabel _body = default!;
	private Button _nextButton = default!;
	private Button _skipButton = default!;
	private readonly List<ColorRect> _dynamicDimRects = new();
	private readonly List<Control> _highlightControls = new();

	public override void _Ready()
	{
		if (!TryGetRequiredNode(DimPath, nameof(DimPath), out _dim))
			return;
		if (!TryGetRequiredNode(HighlightPath, nameof(HighlightPath), out _highlight))
			return;
		if (!TryGetRequiredNode(PanelPath, nameof(PanelPath), out _panel))
			return;
		if (!TryGetRequiredNode(TitleLabelPath, nameof(TitleLabelPath), out _title))
			return;
		if (!TryGetRequiredNode(BodyLabelPath, nameof(BodyLabelPath), out _body))
			return;
		if (!TryGetRequiredNode(NextButtonPath, nameof(NextButtonPath), out _nextButton))
			return;
		if (!TryGetRequiredNode(SkipButtonPath, nameof(SkipButtonPath), out _skipButton))
			return;

		MouseFilter = MouseFilterEnum.Ignore;
		_dim.MouseFilter = MouseFilterEnum.Ignore;
		_dimTop = GetOptionalDimRect("DimTop");
		_dimBottom = GetOptionalDimRect("DimBottom");
		_dimLeft = GetOptionalDimRect("DimLeft");
		_dimRight = GetOptionalDimRect("DimRight");
		_secondaryHighlight = GetNodeOrNull<Control>("SecondaryHighlight");
		_highlight.MouseFilter = MouseFilterEnum.Ignore;
		if (_secondaryHighlight is not null)
			_secondaryHighlight.MouseFilter = MouseFilterEnum.Ignore;
		_panel.MouseFilter = MouseFilterEnum.Stop;
		_body.BbcodeEnabled = true;
		_dim.ZIndex = 0;
		if (_dimTop is not null)
			_dimTop.ZIndex = 0;
		if (_dimBottom is not null)
			_dimBottom.ZIndex = 0;
		if (_dimLeft is not null)
			_dimLeft.ZIndex = 0;
		if (_dimRight is not null)
			_dimRight.ZIndex = 0;
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
		_dim.Visible = false;
		HideDimCutout();

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

		_dim.Visible = true;
		HideDimCutout();
		HideDynamicDimRects();
		foreach (var highlightControl in _highlightControls)
			HideHighlightControl(highlightControl);
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

	private void UpdateDimCutout(Rect2 highlightRect)
	{
		if (_dimTop is null || _dimBottom is null || _dimLeft is null || _dimRight is null)
			return;

		var overlaySize = Size;
		if (overlaySize.X <= 0.0f || overlaySize.Y <= 0.0f)
			overlaySize = GetViewportRect().Size;

		var left = Mathf.Clamp(highlightRect.Position.X, 0.0f, overlaySize.X);
		var top = Mathf.Clamp(highlightRect.Position.Y, 0.0f, overlaySize.Y);
		var right = Mathf.Clamp(highlightRect.Position.X + highlightRect.Size.X, 0.0f, overlaySize.X);
		var bottom = Mathf.Clamp(highlightRect.Position.Y + highlightRect.Size.Y, 0.0f, overlaySize.Y);

		SetDimRect(_dimTop, new Rect2(Vector2.Zero, new Vector2(overlaySize.X, top)));
		SetDimRect(_dimBottom, new Rect2(new Vector2(0.0f, bottom), new Vector2(overlaySize.X, overlaySize.Y - bottom)));
		SetDimRect(_dimLeft, new Rect2(new Vector2(0.0f, top), new Vector2(left, bottom - top)));
		SetDimRect(_dimRight, new Rect2(new Vector2(right, top), new Vector2(overlaySize.X - right, bottom - top)));
	}

	private void HideDimCutout()
	{
		SetDimRect(_dimTop, new Rect2());
		SetDimRect(_dimBottom, new Rect2());
		SetDimRect(_dimLeft, new Rect2());
		SetDimRect(_dimRight, new Rect2());
	}

	private static void SetDimRect(ColorRect? dimRect, Rect2 rect)
	{
		if (dimRect is null)
			return;

		dimRect.Position = rect.Position;
		dimRect.Size = rect.Size;
		dimRect.Visible = rect.Size.X > 0.0f && rect.Size.Y > 0.0f;
	}

	private ColorRect? GetOptionalDimRect(NodePath path)
	{
		var dimRect = GetNodeOrNull<ColorRect>(path);
		if (dimRect is not null)
			dimRect.MouseFilter = MouseFilterEnum.Ignore;

		return dimRect;
	}

	private void OnNextButtonPressed()
	{
		EmitSignal(SignalName.NextPressed);
	}

	private void OnSkipButtonPressed()
	{
		EmitSignal(SignalName.SkipPressed);
	}

	private bool TryGetRequiredNode<TNode>(NodePath path, string exportName, out TNode node) where TNode : Node
	{
		node = default!;

		if (path.IsEmpty)
		{
			GD.PushError($"TutorialOverlay: {exportName} is not assigned.");
			return false;
		}

		var resolvedNode = GetNodeOrNull<TNode>(path);
		if (resolvedNode is null)
		{
			GD.PushError($"TutorialOverlay: Node not found at '{path}'.");
			return false;
		}

		node = resolvedNode;
		return true;
	}
}
