using Godot;

namespace OccultShop.UI;

public static class InventorySlotVisuals
{
	public static void ClearChildren(Node container)
	{
		foreach (var child in container.GetChildren())
		{
			container.RemoveChild(child);
			child.QueueFree();
		}
	}

	public static StyleBoxFlat CreateSlotStyleBox(
		Color fillColor,
		Color borderColor,
		int cornerRadius,
		int borderWidth = 1)
	{
		return new StyleBoxFlat
		{
			BgColor = fillColor,
			BorderWidthLeft = borderWidth,
			BorderWidthTop = borderWidth,
			BorderWidthRight = borderWidth,
			BorderWidthBottom = borderWidth,
			BorderColor = borderColor,
			CornerRadiusTopLeft = cornerRadius,
			CornerRadiusTopRight = cornerRadius,
			CornerRadiusBottomRight = cornerRadius,
			CornerRadiusBottomLeft = cornerRadius
		};
	}

	public static PanelContainer CreateHoverOutline(
		Color fillColor,
		Color borderColor,
		int cornerRadius,
		int borderWidth)
	{
		var hoverOutline = new PanelContainer
		{
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Visible = false
		};
		hoverOutline.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		hoverOutline.AddThemeStyleboxOverride("panel", CreateSlotStyleBox(
			fillColor,
			borderColor,
			cornerRadius,
			borderWidth));
		return hoverOutline;
	}
}
