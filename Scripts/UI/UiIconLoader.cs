using Godot;

namespace OccultShop.UI;

public static class UiIconLoader
{
	public static Texture2D? LoadIcon(string? iconPath)
	{
		if (string.IsNullOrWhiteSpace(iconPath))
			return null;

		return ResourceLoader.Load<Texture2D>(iconPath);
	}
}
