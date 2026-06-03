using Godot;

namespace OccultShop.UI;

public partial class CursorToast : CanvasLayer
{
	private const double DisplaySeconds = 3.0;
	private const float CursorOffsetY = 32.0f;
	private const float ScreenPadding = 12.0f;
	private const int FontSize = 18;
	private const int MaxWidth = 420;

	private string _message = string.Empty;
	private Vector2 _cursorPosition;
	private PanelContainer _panel = default!;

	public static void Show(Control owner, string message)
	{
		if (owner is null || string.IsNullOrWhiteSpace(message))
			return;

		var viewport = owner.GetViewport();
		if (viewport is null)
			return;

		var toast = new CursorToast
		{
			_message = message,
			_cursorPosition = viewport.GetMousePosition()
		};

		owner.GetTree().Root.AddChild(toast);
	}

	public override void _Ready()
	{
		Layer = 100;

		_panel = new PanelContainer
		{
			MouseFilter = Control.MouseFilterEnum.Ignore,
			CustomMinimumSize = new Vector2(0, 0)
		};
		_panel.AddThemeStyleboxOverride("panel", CreatePanelStyleBox());

		var margin = new MarginContainer
		{
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		margin.AddThemeConstantOverride("margin_left", 12);
		margin.AddThemeConstantOverride("margin_top", 8);
		margin.AddThemeConstantOverride("margin_right", 12);
		margin.AddThemeConstantOverride("margin_bottom", 8);

		var label = new Label
		{
			Text = _message,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(MaxWidth, 0),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		label.AddThemeColorOverride("font_color", new Color("f4f7ef"));
		label.AddThemeFontSizeOverride("font_size", FontSize);

		margin.AddChild(label);
		_panel.AddChild(margin);
		AddChild(_panel);

		CallDeferred(MethodName.PositionToast);

		var timer = new Godot.Timer
		{
			OneShot = true,
			WaitTime = DisplaySeconds
		};
		timer.Timeout += QueueFree;
		AddChild(timer);
		timer.Start();
	}

	private void PositionToast()
	{
		var viewport = GetViewport();
		if (viewport is null)
			return;

		var viewportSize = viewport.GetVisibleRect().Size;
		var toastSize = _panel.Size;
		var position = new Vector2(
			_cursorPosition.X - toastSize.X * 0.5f,
			_cursorPosition.Y - toastSize.Y - CursorOffsetY);

		position.X = Mathf.Clamp(position.X, ScreenPadding, Mathf.Max(ScreenPadding, viewportSize.X - toastSize.X - ScreenPadding));
		position.Y = Mathf.Clamp(position.Y, ScreenPadding, Mathf.Max(ScreenPadding, viewportSize.Y - toastSize.Y - ScreenPadding));
		_panel.Position = position;
	}

	private static StyleBoxFlat CreatePanelStyleBox()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.08f, 0.09f, 0.1f, 0.94f),
			BorderColor = new Color(0.58f, 0.68f, 0.55f, 0.95f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomRight = 6,
			CornerRadiusBottomLeft = 6
		};
	}
}
