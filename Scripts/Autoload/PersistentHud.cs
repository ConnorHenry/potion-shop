using Godot;
using OccultShop.UI;

namespace OccultShop.Autoload;

public partial class PersistentHud : CanvasLayer
{
	private const string DefaultHudScenePath = "res://Scenes/UI/Hud.tscn";
	private const int PersistentHudLayer = 2048;

	[Export] public string HudScenePath { get; set; } = DefaultHudScenePath;
	[Export] public bool ShowHudWhenNoOverride { get; set; } = true;

	private Hud? _hud;
	private Node? _currentScene;
	private bool _refreshQueued;

	public Hud? Hud => _hud;

	public override void _Ready()
	{
		Layer = PersistentHudLayer;
		EnsureHud();
		GetTree().NodeAdded += OnNodeAdded;
		GetTree().NodeRemoved += OnNodeRemoved;
		QueueSceneRefresh();
	}

	public override void _ExitTree()
	{
		var tree = GetTree();
		if (tree is not null)
		{
			tree.NodeAdded -= OnNodeAdded;
			tree.NodeRemoved -= OnNodeRemoved;
		}
	}

	private void EnsureHud()
	{
		if (_hud is not null)
			return;

		var hudScene = ResourceLoader.Load<PackedScene>(HudScenePath);
		if (hudScene is null)
		{
			GD.PushError($"PersistentHud: HUD scene could not be loaded from '{HudScenePath}'.");
			return;
		}

		var hud = hudScene.InstantiateOrNull<Hud>();
		if (hud is null)
		{
			GD.PushError($"PersistentHud: HUD scene root must be {nameof(Hud)}.");
			return;
		}

		_hud = hud;
		_hud.Visible = false;
		AddChild(_hud);
	}

	private void OnNodeAdded(Node node)
	{
		var tree = GetTree();
		if (node == tree.CurrentScene || node.GetParent() == tree.Root)
			QueueSceneRefresh();
	}

	private void OnNodeRemoved(Node node)
	{
		if (node != _currentScene)
			return;

		_currentScene = null;
		if (_hud is not null)
		{
			_hud.Visible = false;
			_hud.SetAmbientPlaybackAllowed(false);
			_hud.HideSettingsPanel();
			_hud.RefreshSceneBindings();
		}

		QueueSceneRefresh();
	}

	private void QueueSceneRefresh()
	{
		if (_refreshQueued)
			return;

		_refreshQueued = true;
		Callable.From(RefreshForCurrentScene).CallDeferred();
	}

	private void RefreshForCurrentScene()
	{
		_refreshQueued = false;
		EnsureHud();
		if (_hud is null)
			return;

		var currentScene = GetTree().CurrentScene;
		_currentScene = currentScene;
		_hud.RefreshSceneBindings();
		var shouldShowHud = ShouldShowHud(currentScene);
		_hud.Visible = shouldShowHud;
		_hud.SetAmbientPlaybackAllowed(shouldShowHud);
		if (!shouldShowHud)
			_hud.HideSettingsPanel();
	}

	private bool ShouldShowHud(Node? currentScene)
	{
		if (currentScene is null)
			return false;

		var visibilityOverride = FindVisibilityOverride(currentScene);
		return visibilityOverride?.HudVisible ?? ShowHudWhenNoOverride;
	}

	private static PersistentHudVisibility? FindVisibilityOverride(Node root)
	{
		if (root is PersistentHudVisibility visibility)
			return visibility;

		foreach (var child in root.GetChildren())
		{
			var result = FindVisibilityOverride(child);
			if (result is not null)
				return result;
		}

		return null;
	}
}
