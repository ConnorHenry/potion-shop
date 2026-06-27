using Godot;
using OccultShop.Infrastructure;

namespace OccultShop.UI;

public static class HudNavigationService
{
	public static bool IsNavigationBlocked(SceneTree tree)
	{
		return tree.CurrentScene is ForestGathering or JuniperGathering;
	}

	public static bool TryOpenGarden(Node owner)
	{
		return TryChangeScene(owner, ScenePaths.Garden, "garden scene");
	}

	public static bool TryOpenMap(Node owner)
	{
		return TryChangeScene(owner, ScenePaths.Map, "map scene");
	}

	public static bool TryOpenMainMenu(Node owner)
	{
		return TryChangeScene(owner, ScenePaths.MainMenu, "main menu scene");
	}

	private static bool TryChangeScene(Node owner, string scenePath, string description)
	{
		if (owner is null)
			return false;

		var error = owner.GetTree().ChangeSceneToFile(scenePath);
		if (error == Error.Ok)
			return true;

		GD.PushError($"Hud: Failed to load {description}. Error: {error}");
		return false;
	}
}
