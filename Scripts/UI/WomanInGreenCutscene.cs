using Godot;
using OccultShop.Autoload;
using OccultShop.Infrastructure;

namespace OccultShop.UI;

public partial class WomanInGreenCutscene : Control
{
	private static readonly Vector2 FallbackStageSize = new(1920.0f, 1080.0f);

	[Export] public NodePath ConversationPath = new("Root/Margin/Conversation");
	[Export] public NodePath PanelsRootPath = new("PanelsRoot");
	[Export] public NodePath GameStatePath = new(AutoloadNodePaths.GameState);
	[Export] public NodePath SaveGameManagerPath = new(AutoloadNodePaths.SaveGameManager);
	[Export] public NodePath SceneTransitionPath = new(AutoloadNodePaths.SceneTransition);
	[Export] public Texture2D? ComicAtlasTexture { get; set; }
	[Export] public int DialogueTypewriterCharactersPerSecond = 45;
	[Export(PropertyHint.Range, "0.1,10.0,0.1")] public double SlideDuration { get; set; } = 1.0;
	[Export(PropertyHint.Range, "0.0,10.0,0.1")] public double HoldDuration { get; set; } = 3.0;
	[Export(PropertyHint.Range, "0.2,0.45,0.01")] public float BottomStripHeightRatio { get; set; } = 0.3f;
	[Export] public Rect2 Panel1Region { get; set; } = new(7.0f, 7.0f, 757.0f, 377.0f);
	[Export] public Rect2 Panel2Region { get; set; } = new(772.0f, 7.0f, 757.0f, 377.0f);
	[Export] public Rect2 Panel3Region { get; set; } = new(7.0f, 392.0f, 757.0f, 299.0f);
	[Export] public Rect2 Panel4Region { get; set; } = new(772.0f, 392.0f, 757.0f, 299.0f);
	[Export] public Rect2 Panel5Region { get; set; } = new(7.0f, 698.0f, 1522.0f, 319.0f);

	private static readonly NarrativeTextLine[] StoryLines =
	{
		new(null, "After picking the juniper berries, you both walk home.", allowMarkup: false),
		new(null, "By the river, you saw the woman in green.", allowMarkup: false),
		new(null, "She stood half-hidden beneath the alder trees, sleeves rolled to the elbow, washing a pale coat against the stones. The river ran brown around her hands. Though the rain had stopped, her hair and dress were soaked through.", allowMarkup: false),
		new(null, "You opened your mouth to ask who she was.", allowMarkup: false),
		new(null, "Your mother's fingers closed around your wrist.", allowMarkup: false),
		new(null, "\"Do not speak to her.\"", allowMarkup: false),
		new(null, "You had never heard fear in your mother's voice before.", allowMarkup: false),
		new(null, "The woman by the river lifted the coat from the water. For a moment, you thought it looked familiar. Then your mother pulled you away, and neither of you spoke until the shop lamps were lit again.", allowMarkup: false)
	};

	private GameState _gameState = default!;
	private SaveGameManager _saveGameManager = default!;
	private SceneTransition _sceneTransition = default!;
	private RichTextLabel _conversation = default!;
	private Control _panelsRoot = default!;
	private PanelView _panel1 = default!;
	private PanelView _panel2 = default!;
	private PanelView _panel3 = default!;
	private PanelView _panel4 = default!;
	private PanelView _panel5 = default!;
	private Tween? _animationTween;
	private bool _transitionStarted;
	private bool _animationPlaying;

	public override void _Ready()
	{
		if (!ResolveNodes())
			return;

		_gameState.RecordWomanInGreenCutsceneStarted();
		TryAutoSave("starting the woman in green cutscene");

		_conversation.Visible = false;
		_panelsRoot.ClipContents = true;

		if (ComicAtlasTexture is null)
		{
			GD.PushError("WomanInGreenCutscene: ComicAtlasTexture is not assigned.");
			TransitionToMainScene();
			return;
		}

		BuildPanels();
		StartPanelAnimation();
	}

	public override void _ExitTree()
	{
		_animationTween?.Kill();
		_animationTween = null;
	}

	private bool ResolveNodes()
	{
		if (!NodeLookup.TryGetRequiredNode<GameState>(this, GameStatePath, nameof(WomanInGreenCutscene), nameof(GameStatePath), out _gameState))
			return false;
		if (!NodeLookup.TryGetRequiredNode<SaveGameManager>(this, SaveGameManagerPath, nameof(WomanInGreenCutscene), nameof(SaveGameManagerPath), out _saveGameManager))
			return false;
		if (!NodeLookup.TryGetRequiredNode<SceneTransition>(this, SceneTransitionPath, nameof(WomanInGreenCutscene), nameof(SceneTransitionPath), out _sceneTransition))
			return false;
		if (!NodeLookup.TryGetRequiredNode<RichTextLabel>(this, ConversationPath, nameof(WomanInGreenCutscene), nameof(ConversationPath), out _conversation))
			return false;
		if (!NodeLookup.TryGetRequiredNode<Control>(this, PanelsRootPath, nameof(WomanInGreenCutscene), nameof(PanelsRootPath), out _panelsRoot))
			return false;

		return true;
	}

	private void BuildPanels()
	{
		_panel1 = CreatePanel("Panel1", Panel1Region);
		_panel2 = CreatePanel("Panel2", Panel2Region);
		_panel3 = CreatePanel("Panel3", Panel3Region);
		_panel4 = CreatePanel("Panel4", Panel4Region);
		_panel5 = CreatePanel("Panel5", Panel5Region);
	}

	private PanelView CreatePanel(string panelName, Rect2 sourceRegion)
	{
		var container = new Control
		{
			Name = panelName,
			ClipContents = true,
			MouseFilter = MouseFilterEnum.Ignore
		};
		SetAbsoluteLayout(container);

		var atlas = new AtlasTexture
		{
			Atlas = ComicAtlasTexture,
			Region = sourceRegion
		};

		var image = new TextureRect
		{
			Name = "Image",
			Texture = atlas,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			MouseFilter = MouseFilterEnum.Ignore
		};
		image.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		container.AddChild(image);
		_panelsRoot.AddChild(container);

		return new PanelView(container);
	}

	private void StartPanelAnimation()
	{
		_animationTween?.Kill();
		ApplyInitialLayout();
		_animationPlaying = true;

		_animationTween = CreateTween();
		_animationTween.TweenInterval(HoldDuration);
		_animationTween.TweenCallback(Callable.From(ShowSecondPanel));
	}

	private void ApplyInitialLayout()
	{
		var stageSize = GetStageSize();
		SetPanelRect(_panel1, new Rect2(Vector2.Zero, stageSize));
		ShowPanel(_panel1);

		HidePanel(_panel2);
		HidePanel(_panel3);
		HidePanel(_panel4);
		HidePanel(_panel5);
	}

	private void ShowSecondPanel()
	{
		if (!CanContinueAnimation())
			return;

		var stageSize = GetStageSize();
		var halfWidth = stageSize.X * 0.5f;
		var panelSize = new Vector2(halfWidth, stageSize.Y);
		var firstPanelRect = new Rect2(Vector2.Zero, panelSize);
		var secondPanelRect = new Rect2(new Vector2(halfWidth, 0.0f), panelSize);

		SetPanelRect(_panel2, new Rect2(new Vector2(stageSize.X, 0.0f), panelSize));
		ShowPanel(_panel2);

		var tween = CreateMotionTween();
		TweenPanel(tween, _panel1, firstPanelRect);
		TweenPanel(tween, _panel2, secondPanelRect);
		QueueNextStep(tween, ShowThirdPanel);
	}

	private void ShowThirdPanel()
	{
		if (!CanContinueAnimation())
			return;

		var stageSize = GetStageSize();
		var halfWidth = stageSize.X * 0.5f;
		var halfHeight = stageSize.Y * 0.5f;
		var topPanelSize = new Vector2(halfWidth, halfHeight);
		var bottomPanelSize = new Vector2(stageSize.X, halfHeight);

		var firstPanelRect = new Rect2(Vector2.Zero, topPanelSize);
		var secondPanelRect = new Rect2(new Vector2(halfWidth, 0.0f), topPanelSize);
		var thirdPanelRect = new Rect2(new Vector2(0.0f, halfHeight), bottomPanelSize);

		SetPanelRect(_panel3, new Rect2(new Vector2(0.0f, stageSize.Y), bottomPanelSize));
		ShowPanel(_panel3);

		var tween = CreateMotionTween();
		TweenPanel(tween, _panel1, firstPanelRect);
		TweenPanel(tween, _panel2, secondPanelRect);
		TweenPanel(tween, _panel3, thirdPanelRect);
		QueueNextStep(tween, ShowFourthPanel);
	}

	private void ShowFourthPanel()
	{
		if (!CanContinueAnimation())
			return;

		var stageSize = GetStageSize();
		var halfWidth = stageSize.X * 0.5f;
		var halfHeight = stageSize.Y * 0.5f;
		var quarterSize = new Vector2(halfWidth, halfHeight);

		var firstPanelRect = new Rect2(Vector2.Zero, quarterSize);
		var secondPanelRect = new Rect2(new Vector2(halfWidth, 0.0f), quarterSize);
		var thirdPanelRect = new Rect2(new Vector2(0.0f, halfHeight), quarterSize);
		var fourthPanelRect = new Rect2(new Vector2(halfWidth, halfHeight), quarterSize);

		SetPanelRect(_panel4, new Rect2(new Vector2(stageSize.X, halfHeight), quarterSize));
		ShowPanel(_panel4);

		var tween = CreateMotionTween();
		TweenPanel(tween, _panel1, firstPanelRect);
		TweenPanel(tween, _panel2, secondPanelRect);
		TweenPanel(tween, _panel3, thirdPanelRect);
		TweenPanel(tween, _panel4, fourthPanelRect);
		QueueNextStep(tween, ShowFifthPanel);
	}

	private void ShowFifthPanel()
	{
		if (!CanContinueAnimation())
			return;

		var stageSize = GetStageSize();
		var stripHeight = stageSize.Y * Mathf.Clamp(BottomStripHeightRatio, 0.2f, 0.45f);
		var topAreaHeight = stageSize.Y - stripHeight;
		var halfWidth = stageSize.X * 0.5f;
		var rowHeight = topAreaHeight * 0.5f;
		var topPanelSize = new Vector2(halfWidth, rowHeight);
		var stripSize = new Vector2(stageSize.X, stripHeight);

		var firstPanelRect = new Rect2(Vector2.Zero, topPanelSize);
		var secondPanelRect = new Rect2(new Vector2(halfWidth, 0.0f), topPanelSize);
		var thirdPanelRect = new Rect2(new Vector2(0.0f, rowHeight), topPanelSize);
		var fourthPanelRect = new Rect2(new Vector2(halfWidth, rowHeight), topPanelSize);
		var fifthPanelRect = new Rect2(new Vector2(0.0f, topAreaHeight), stripSize);

		SetPanelRect(_panel5, new Rect2(new Vector2(0.0f, stageSize.Y), stripSize));
		ShowPanel(_panel5);

		var tween = CreateMotionTween();
		TweenPanel(tween, _panel1, firstPanelRect);
		TweenPanel(tween, _panel2, secondPanelRect);
		TweenPanel(tween, _panel3, thirdPanelRect);
		TweenPanel(tween, _panel4, fourthPanelRect);
		TweenPanel(tween, _panel5, fifthPanelRect);
		QueueNextStep(tween, TransitionToMainScene);
	}

	private Tween CreateMotionTween()
	{
		var tween = CreateTween();
		_animationTween = tween;
		tween.SetParallel();
		tween.SetTrans(Tween.TransitionType.Sine);
		tween.SetEase(Tween.EaseType.Out);
		return tween;
	}

	private void QueueNextStep(Tween tween, System.Action nextStep)
	{
		tween.SetParallel(false);
		tween.TweenInterval(HoldDuration);
		tween.TweenCallback(Callable.From(nextStep));
	}

	private void TweenPanel(Tween tween, PanelView panel, Rect2 targetRect)
	{
		tween.TweenProperty(panel.Container, "position", targetRect.Position, SlideDuration);
		tween.TweenProperty(panel.Container, "size", targetRect.Size, SlideDuration);
	}

	private bool CanContinueAnimation()
	{
		return _animationPlaying && !_transitionStarted && IsInsideTree();
	}

	private Vector2 GetStageSize()
	{
		var stageSize = _panelsRoot.Size;
		if (stageSize.X <= 0.0f || stageSize.Y <= 0.0f)
			stageSize = GetViewportRect().Size;
		if (stageSize.X <= 0.0f || stageSize.Y <= 0.0f)
			stageSize = FallbackStageSize;

		return stageSize;
	}

	private static void SetPanelRect(PanelView panel, Rect2 rect)
	{
		panel.Container.Position = rect.Position;
		panel.Container.Size = rect.Size;
	}

	private static void ShowPanel(PanelView panel)
	{
		panel.Container.Visible = true;
	}

	private static void HidePanel(PanelView panel)
	{
		panel.Container.Visible = false;
		panel.Container.Position = Vector2.Zero;
		panel.Container.Size = Vector2.Zero;
	}

	private static void SetAbsoluteLayout(Control control)
	{
		control.AnchorLeft = 0.0f;
		control.AnchorTop = 0.0f;
		control.AnchorRight = 0.0f;
		control.AnchorBottom = 0.0f;
		control.OffsetLeft = 0.0f;
		control.OffsetTop = 0.0f;
		control.OffsetRight = 0.0f;
		control.OffsetBottom = 0.0f;
	}

	private void TransitionToMainScene()
	{
		if (_transitionStarted)
			return;

		_transitionStarted = true;
		_animationPlaying = false;
		_gameState.RecordWomanInGreenCutsceneCompleted();
		TryAutoSave("completing the woman in green cutscene");
		_sceneTransition.ChangeSceneWithFade(ScenePaths.Main);
	}

	private bool TryAutoSave(string context)
	{
		var saveSucceeded = _saveGameManager.SaveGame();
		if (!saveSucceeded)
			GD.PushError($"WomanInGreenCutscene: Auto-save failed while {context}.");

		return saveSucceeded;
	}

	private sealed class PanelView
	{
		public PanelView(Control container)
		{
			Container = container;
		}

		public Control Container { get; }
	}
}
