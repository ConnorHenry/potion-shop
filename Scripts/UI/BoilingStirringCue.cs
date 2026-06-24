using System;
using Godot;

namespace OccultShop.UI;

public partial class BoilingStirringCue : Control
{
	private const float DefaultStickAngleRadians = 0.68f;
	private const float SlowArrowRadiansPerSecond = 1.15f;
	private const float FastArrowRadiansPerSecond = 2.85f;
	private const float ArrowSweepRadians = Mathf.Pi * 1.46f;
	private const int ArrowSegmentCount = 28;

	[Export] public bool IsDirectionArrow { get; set; }
	[Export] public Color StickColor { get; set; } = new(0.49f, 0.31f, 0.16f, 1.0f);
	[Export] public Color StickHighlightColor { get; set; } = new(0.78f, 0.56f, 0.32f, 1.0f);
	[Export] public Color StickShadowColor { get; set; } = new(0.08f, 0.035f, 0.018f, 0.58f);
	[Export] public Color ArrowColor { get; set; } = new(0.92f, 0.72f, 0.23f, 0.82f);
	[Export] public Color ArrowShadowColor { get; set; } = new(0.04f, 0.02f, 0.05f, 0.48f);

	private bool _active;
	private bool _clockwise = true;
	private bool _fast;
	private float _stickAngleRadians = DefaultStickAngleRadians;
	private float _arrowAngleRadians = -Mathf.Pi * 0.92f;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		Visible = false;
		Resized += QueueRedraw;
	}

	public override void _ExitTree()
	{
		Resized -= QueueRedraw;
	}

	public override void _Process(double delta)
	{
		if (!_active || !IsDirectionArrow)
			return;

		var direction = _clockwise ? 1.0f : -1.0f;
		var speed = _fast ? FastArrowRadiansPerSecond : SlowArrowRadiansPerSecond;
		_arrowAngleRadians = WrapRadians(_arrowAngleRadians + (direction * speed * (float)delta));
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (!_active || Size.X <= 0.0f || Size.Y <= 0.0f)
			return;

		if (IsDirectionArrow)
			DrawDirectionArrow();
		else
			DrawStirringStick();
	}

	public void SetCueState(bool active, bool clockwise, bool fast, bool resetStickAngle)
	{
		_active = active;
		_clockwise = clockwise;
		_fast = fast;
		Visible = active;
		SetProcess(active && IsDirectionArrow);

		if (resetStickAngle)
			_stickAngleRadians = DefaultStickAngleRadians;

		if (active && IsDirectionArrow)
			_arrowAngleRadians = clockwise ? -Mathf.Pi * 0.92f : Mathf.Pi * 0.42f;

		QueueRedraw();
	}

	public void AddStickAngle(float angleDeltaRadians)
	{
		if (!_active || IsDirectionArrow)
			return;

		_stickAngleRadians = WrapRadians(_stickAngleRadians + angleDeltaRadians);
		QueueRedraw();
	}

	private void DrawStirringStick()
	{
		var baseSize = Math.Min(Size.X, Size.Y);
		var pivot = new Vector2(Size.X * 0.50f, Size.Y * 0.54f);
		var direction = new Vector2(Mathf.Sin(_stickAngleRadians), -Mathf.Cos(_stickAngleRadians)).Normalized();
		var perpendicular = new Vector2(-direction.Y, direction.X);
		var innerEnd = pivot - (direction * baseSize * 0.28f);
		var outerEnd = pivot + (direction * baseSize * 0.56f);
		var stickWidth = Math.Max(6.0f, baseSize * 0.035f);

		DrawLine(innerEnd, outerEnd, StickShadowColor, stickWidth + 4.0f);
		DrawLine(innerEnd, outerEnd, StickColor, stickWidth);
		DrawLine(
			innerEnd + (perpendicular * stickWidth * 0.22f),
			outerEnd + (perpendicular * stickWidth * 0.22f),
			StickHighlightColor,
			Math.Max(1.0f, stickWidth * 0.22f));
		DrawCircle(innerEnd, stickWidth * 0.48f, StickShadowColor);
	}

	private void DrawDirectionArrow()
	{
		var baseSize = Math.Min(Size.X, Size.Y);
		var center = Size * 0.5f;
		var radius = baseSize * 0.34f;
		var lineWidth = Math.Max(4.0f, baseSize * 0.022f);
		var direction = _clockwise ? 1.0f : -1.0f;
		var endAngle = _arrowAngleRadians + (direction * ArrowSweepRadians);
		var endPoint = center + (VectorFromAngle(endAngle) * radius);
		var tangent = new Vector2(-Mathf.Sin(endAngle), Mathf.Cos(endAngle)) * direction;

		DrawArcLines(center, radius, direction, ArrowShadowColor, lineWidth + 4.0f);
		DrawArrowHead(endPoint, tangent.Normalized(), ArrowShadowColor, lineWidth + 4.0f);
		DrawArcLines(center, radius, direction, ArrowColor, lineWidth);
		DrawArrowHead(endPoint, tangent.Normalized(), ArrowColor, lineWidth);
	}

	private void DrawArcLines(Vector2 center, float radius, float direction, Color color, float width)
	{
		var previousPoint = center + (VectorFromAngle(_arrowAngleRadians) * radius);
		for (var segment = 1; segment <= ArrowSegmentCount; segment += 1)
		{
			var progress = segment / (float)ArrowSegmentCount;
			var angle = _arrowAngleRadians + (direction * ArrowSweepRadians * progress);
			var currentPoint = center + (VectorFromAngle(angle) * radius);
			DrawLine(previousPoint, currentPoint, color, width);
			previousPoint = currentPoint;
		}
	}

	private void DrawArrowHead(Vector2 tip, Vector2 tangent, Color color, float width)
	{
		var baseSize = Math.Min(Size.X, Size.Y);
		var headLength = baseSize * 0.085f;
		var headWidth = baseSize * 0.055f;
		var normal = new Vector2(-tangent.Y, tangent.X);
		var basePoint = tip - (tangent * headLength);

		DrawLine(tip, basePoint + (normal * headWidth), color, width);
		DrawLine(tip, basePoint - (normal * headWidth), color, width);
	}

	private static Vector2 VectorFromAngle(float angle)
	{
		return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
	}

	private static float WrapRadians(float value)
	{
		var fullTurn = Mathf.Pi * 2.0f;
		while (value > fullTurn)
			value -= fullTurn;
		while (value < -fullTurn)
			value += fullTurn;
		return value;
	}
}
