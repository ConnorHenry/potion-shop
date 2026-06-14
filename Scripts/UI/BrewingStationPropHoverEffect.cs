using Godot;

namespace OccultShop.UI;

public partial class BrewingStationPropHoverEffect : Node
{
	[Export] public NodePath TargetPath = new("..");
	[Export] public NodePath HoverSourcePath = new("..");
	[Export] public float HoverScale = 1.035f;
	[Export] public Vector2 HoverOffset = new(0.0f, -6.0f);
	[Export] public float DurationSeconds = 0.12f;

	private Node? _target;
	private Control? _hoverSource;
	private Tween? _tween;
	private Vector2 _restScale = Vector2.One;
	private Vector2 _restPosition = Vector2.Zero;

	public override void _Ready()
	{
		_target = GetNodeOrNull<Node>(TargetPath);
		_hoverSource = GetNodeOrNull<Control>(HoverSourcePath);
		if (_target is null)
		{
			GD.PushError($"BrewingStationPropHoverEffect: Target was not found at '{TargetPath}'.");
			return;
		}

		if (_hoverSource is null)
		{
			GD.PushError($"BrewingStationPropHoverEffect: Hover source was not found at '{HoverSourcePath}'.");
			return;
		}

		CacheTargetTransform();
		_hoverSource.MouseEntered += OnMouseEntered;
		_hoverSource.MouseExited += OnMouseExited;
	}

	public override void _ExitTree()
	{
		if (_hoverSource is not null)
		{
			_hoverSource.MouseEntered -= OnMouseEntered;
			_hoverSource.MouseExited -= OnMouseExited;
		}

		_tween?.Kill();
	}

	private void CacheTargetTransform()
	{
		if (_target is Control control)
		{
			_restScale = control.Scale;
			_restPosition = control.Position;
			Callable.From(() =>
			{
				if (GodotObject.IsInstanceValid(control))
					control.PivotOffset = control.Size * 0.5f;
			}).CallDeferred();
			return;
		}

		if (_target is Node2D node2D)
		{
			_restScale = node2D.Scale;
			_restPosition = node2D.Position;
		}
	}

	private void OnMouseEntered()
	{
		Animate(active: true);
	}

	private void OnMouseExited()
	{
		Animate(active: false);
	}

	private void Animate(bool active)
	{
		if (_target is null)
			return;

		_tween?.Kill();
		var targetScale = _restScale * (active ? HoverScale : 1.0f);
		var targetPosition = _restPosition + (active ? HoverOffset : Vector2.Zero);

		_tween = CreateTween();
		_tween.SetParallel();
		_tween.SetTrans(Tween.TransitionType.Sine);
		_tween.SetEase(Tween.EaseType.Out);
		_tween.TweenProperty(_target, "scale", targetScale, DurationSeconds);
		_tween.TweenProperty(_target, "position", targetPosition, DurationSeconds);
	}
}
