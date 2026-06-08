using Godot;

namespace OccultShop.UI;

public partial class ScaleWeightButton : Button
{
	[Signal]
	public delegate void WeightActivatedEventHandler(int grams);

	public const string DragDataPrefix = "scale_weight:";

	[Export] public int Grams { get; set; } = 1;
	[Export] public NodePath SpritePath { get; set; } = new("Sprite");

	private Color _defaultModulate = Colors.White;
	private TextureRect? _sprite;
	private bool _dragActive;
	private bool _suppressNextPress;

	public override void _Ready()
	{
		_defaultModulate = Modulate;
		_sprite = GetNodeOrNull<TextureRect>(SpritePath);
		MouseDefaultCursorShape = CursorShape.Drag;
		FocusMode = FocusModeEnum.None;
		Text = _sprite is null ? $"{Mathf.Max(1, Grams)}g" : string.Empty;
		Pressed += OnPressed;
	}

	public override void _ExitTree()
	{
		Pressed -= OnPressed;
	}

	public override Variant _GetDragData(Vector2 atPosition)
	{
		_dragActive = true;
		_suppressNextPress = true;
		Modulate = new Color(_defaultModulate.R, _defaultModulate.G, _defaultModulate.B, 0.0f);
		SetDragPreview(CreateDragPreview());
		ReleaseFocus();
		return Variant.CreateFrom(BuildDragData(Grams));
	}

	public override void _Notification(int what)
	{
		base._Notification(what);

		if (what != NotificationDragEnd || !_dragActive)
			return;

		_dragActive = false;
		Modulate = _defaultModulate;
		CallDeferred(nameof(ClearPressSuppression));
	}

	public static string BuildDragData(int grams)
	{
		return $"{DragDataPrefix}{Mathf.Max(1, grams)}";
	}

	public static bool TryParseDragData(string value, out int grams)
	{
		grams = 0;
		if (string.IsNullOrWhiteSpace(value))
			return false;
		if (!value.StartsWith(DragDataPrefix, System.StringComparison.OrdinalIgnoreCase))
			return false;

		var amountText = value[DragDataPrefix.Length..];
		if (!int.TryParse(amountText, out var parsedGrams) || parsedGrams <= 0)
			return false;

		grams = parsedGrams;
		return true;
	}

	private void OnPressed()
	{
		if (_suppressNextPress)
			return;

		EmitSignal(SignalName.WeightActivated, Mathf.Max(1, Grams));
	}

	private Control CreateDragPreview()
	{
		if (_sprite?.Texture is not null)
		{
			var previewSize = _sprite.Size;
			if (previewSize == Vector2.Zero)
				previewSize = new Vector2(52.0f, 64.0f);

			return new TextureRect
			{
				Texture = _sprite.Texture,
				CustomMinimumSize = previewSize,
				MouseFilter = MouseFilterEnum.Ignore,
				ExpandMode = _sprite.ExpandMode,
				StretchMode = _sprite.StretchMode
			};
		}

		return new Label
		{
			Text = $"{Mathf.Max(1, Grams)}g",
			MouseFilter = MouseFilterEnum.Ignore
		};
	}

	private void ClearPressSuppression()
	{
		_suppressNextPress = false;
	}
}
