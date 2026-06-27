using Godot;

namespace OccultShop.Autoload;

public partial class SceneTransition : CanvasLayer
{
	private const int TransitionLayer = 8192;
	private const float HiddenAlpha = 0.0f;
	private const float VisibleAlpha = 1.0f;

	[Export] public double DefaultFadeSeconds { get; set; } = 0.45;

	private ColorRect _fadeOverlay = default!;
	private Tween? _fadeTween;
	private string _pendingScenePath = string.Empty;
	private bool _transitioning;

	public bool IsTransitioning => _transitioning;

	public override void _Ready()
	{
		Layer = TransitionLayer;
		ProcessMode = ProcessModeEnum.Always;
		BuildFadeOverlay();
	}

	public override void _ExitTree()
	{
		_fadeTween?.Kill();
		_fadeTween = null;
	}

	public void ChangeSceneWithFade(string scenePath)
	{
		if (_transitioning)
			return;
		if (string.IsNullOrWhiteSpace(scenePath))
		{
			GD.PushError("SceneTransition: Cannot transition to an empty scene path.");
			return;
		}

		_transitioning = true;
		_pendingScenePath = scenePath.Trim();
		_fadeOverlay.Visible = true;
		SetOverlayAlpha(HiddenAlpha);
		StartFade(VisibleAlpha, OnFadeOutFinished);
	}

	private void BuildFadeOverlay()
	{
		_fadeOverlay = new ColorRect
		{
			Name = "FadeOverlay",
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f,
			GrowHorizontal = Control.GrowDirection.Both,
			GrowVertical = Control.GrowDirection.Both,
			MouseFilter = Control.MouseFilterEnum.Stop,
			Color = Colors.Black,
			Visible = false
		};
		AddChild(_fadeOverlay);
		SetOverlayAlpha(HiddenAlpha);
	}

	private void OnFadeOutFinished()
	{
		Error error = GetTree().ChangeSceneToFile(_pendingScenePath);
		if (error != Error.Ok)
		{
			GD.PushError($"SceneTransition: Failed to load scene '{_pendingScenePath}'. Error: {error}");
			StartFade(HiddenAlpha, FinishTransition);
			return;
		}

		StartFade(HiddenAlpha, FinishTransition);
	}

	private void FinishTransition()
	{
		SetOverlayAlpha(HiddenAlpha);
		_fadeOverlay.Visible = false;
		_pendingScenePath = string.Empty;
		_transitioning = false;
	}

	private void StartFade(float targetAlpha, System.Action callback)
	{
		_fadeTween?.Kill();
		_fadeTween = CreateTween();
		_fadeTween.SetTrans(Tween.TransitionType.Sine);
		_fadeTween.SetEase(targetAlpha >= VisibleAlpha ? Tween.EaseType.In : Tween.EaseType.Out);
		_fadeTween.TweenProperty(_fadeOverlay, "modulate:a", targetAlpha, DefaultFadeSeconds);
		_fadeTween.Finished += callback;
	}

	private void SetOverlayAlpha(float alpha)
	{
		var color = _fadeOverlay.Modulate;
		color.A = alpha;
		_fadeOverlay.Modulate = color;
	}
}
