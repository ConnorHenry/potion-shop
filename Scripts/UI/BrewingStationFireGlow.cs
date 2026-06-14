using System;
using Godot;

namespace OccultShop.UI;

public partial class BrewingStationFireGlow : Control
{
	private const string DefaultSpriteSheetPath = "res://Assets/Art/BrewingStationBright/fire_under_cauldron_sheet.png";

	[Export] public string SpriteSheetPath = DefaultSpriteSheetPath;
	[Export] public int SheetColumns = 4;
	[Export] public int SheetRows = 2;
	[Export] public int FrameCount = 8;
	[Export] public float FrameRate = 8.0f;
	[Export] public float FrameBlendPortion = 0.55f;
	[Export] public float SpriteWidthScale = 0.94f;
	[Export] public float SpriteHeightScale = 0.64f;
	[Export] public float SpriteCenterY = 0.71f;
	[Export] public float IdleSpriteAlpha = 0.86f;
	[Export] public float PulseDurationSeconds = 1.45f;
	[Export] public float MaxAlpha = 0.08f;
	[Export] public float BurstDurationSeconds = 0.42f;
	[Export] public float BurstAlphaBonus = 0.12f;
	[Export] public float BurstHeightScaleBonus = 0.12f;
	[Export] public Color CoreColor = new(0.713725f, 0.258824f, 0.227451f, 1.0f);
	[Export] public Color OuterColor = new(0.427451f, 0.121569f, 0.168627f, 1.0f);

	private Texture2D? _spriteSheet;
	private double _elapsedSeconds;
	private float _burstTimeRemaining;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		LoadSpriteSheet();
		SetProcess(true);
	}

	public override void _Process(double delta)
	{
		_elapsedSeconds += delta;

		if (_burstTimeRemaining > 0.0f)
			_burstTimeRemaining = Math.Max(0.0f, _burstTimeRemaining - (float)delta);

		QueueRedraw();
	}

	public override void _Draw()
	{
		if (Size.X <= 0.0f || Size.Y <= 0.0f)
			return;

		var pulse = BuildPulse();
		var burst = BuildBurstAlpha();

		DrawAmbientGlow(pulse, burst);
		DrawSpriteFrame(pulse, burst);
	}

	public void PlayIgnitionBurst()
	{
		_burstTimeRemaining = Math.Max(_burstTimeRemaining, Math.Max(0.01f, BurstDurationSeconds));
	}

	private void LoadSpriteSheet()
	{
		if (string.IsNullOrWhiteSpace(SpriteSheetPath))
		{
			GD.PushError("BrewingStationFireGlow: SpriteSheetPath is empty.");
			return;
		}

		_spriteSheet = LoadSpriteSheetFromSourceFile(SpriteSheetPath) ?? ResourceLoader.Load<Texture2D>(SpriteSheetPath);
		if (_spriteSheet is null)
			GD.PushError($"BrewingStationFireGlow: Sprite sheet could not be loaded from '{SpriteSheetPath}'.");
	}

	private static Texture2D? LoadSpriteSheetFromSourceFile(string resourcePath)
	{
		var absolutePath = ProjectSettings.GlobalizePath(resourcePath);
		if (string.IsNullOrWhiteSpace(absolutePath) || !System.IO.File.Exists(absolutePath))
			return null;

		var image = Image.LoadFromFile(absolutePath);
		if (image is null || image.IsEmpty())
			return null;

		return ImageTexture.CreateFromImage(image);
	}

	private void DrawAmbientGlow(float pulse, float burst)
	{
		var alpha = MaxAlpha * (0.58f + (pulse * 0.24f) + (burst * 0.28f));
		var center = new Vector2(Size.X * 0.5f, Size.Y * 0.78f);
		var radius = Math.Min(Size.X, Size.Y) * 0.34f;

		DrawCircle(center, radius * 1.62f, WithAlpha(OuterColor, alpha * 0.30f));
		DrawCircle(center + new Vector2(0.0f, radius * 0.08f), radius * 1.06f, WithAlpha(OuterColor, alpha * 0.38f));
		DrawCircle(center + new Vector2(0.0f, radius * 0.13f), radius * 0.58f, WithAlpha(CoreColor, alpha));
	}

	private void DrawSpriteFrame(float pulse, float burst)
	{
		if (_spriteSheet is null)
			return;

		var columns = Math.Max(1, SheetColumns);
		var rows = Math.Max(1, SheetRows);
		var totalFrames = Math.Clamp(FrameCount, 1, columns * rows);
		var frameRate = Math.Max(0.01f, FrameRate);
		var framePosition = _elapsedSeconds * frameRate;
		var wholeFramePosition = Math.Floor(framePosition);
		var frameIndex = (int)wholeFramePosition % totalFrames;
		var nextFrameIndex = (frameIndex + 1) % totalFrames;
		var frameProgress = (float)(framePosition - wholeFramePosition);
		var frameBlendPortion = Mathf.Clamp(FrameBlendPortion, 0.0f, 1.0f);
		var frameBlend = 0.0f;
		if (frameBlendPortion > 0.0f)
		{
			var blendStart = 1.0f - frameBlendPortion;
			if (frameProgress > blendStart)
				frameBlend = SmoothStep01((frameProgress - blendStart) / frameBlendPortion);
		}

		var frameWidth = _spriteSheet.GetWidth() / (float)columns;
		var frameHeight = _spriteSheet.GetHeight() / (float)rows;
		var sourceRect = BuildFrameSourceRect(frameIndex, columns, frameWidth, frameHeight);
		var nextSourceRect = BuildFrameSourceRect(nextFrameIndex, columns, frameWidth, frameHeight);

		var flicker = 0.5f + (0.5f * Mathf.Sin((float)_elapsedSeconds * Mathf.Pi * 2.0f / Math.Max(0.01f, PulseDurationSeconds)));
		var baseDrawSize = new Vector2(
			Size.X * SpriteWidthScale,
			Size.Y * SpriteHeightScale);
		var burstHeightScale = 1.0f + (burst * BurstHeightScaleBonus);
		var drawSize = new Vector2(baseDrawSize.X, baseDrawSize.Y * burstHeightScale);
		var baseBottomY = (Size.Y * SpriteCenterY) + (baseDrawSize.Y * 0.5f);
		var targetRect = new Rect2(
			new Vector2((Size.X - drawSize.X) * 0.5f, baseBottomY - drawSize.Y),
			drawSize);
		var alpha = Mathf.Clamp(IdleSpriteAlpha + (flicker * 0.04f) + (burst * BurstAlphaBonus), 0.0f, 1.0f);

		var currentFrameWeight = (float)Math.Sqrt(1.0f - frameBlend);
		var nextFrameWeight = (float)Math.Sqrt(frameBlend);
		DrawTextureRectRegion(_spriteSheet, targetRect, sourceRect, new Color(1.0f, 1.0f, 1.0f, alpha * currentFrameWeight));
		if (nextFrameWeight > 0.0f)
			DrawTextureRectRegion(_spriteSheet, targetRect, nextSourceRect, new Color(1.0f, 1.0f, 1.0f, alpha * nextFrameWeight));
	}

	private static Rect2 BuildFrameSourceRect(int frameIndex, int columns, float frameWidth, float frameHeight)
	{
		var column = frameIndex % columns;
		var row = frameIndex / columns;
		return new Rect2(
			column * frameWidth,
			row * frameHeight,
			frameWidth,
			frameHeight);
	}

	private float BuildPulse()
	{
		var duration = Math.Max(0.01f, PulseDurationSeconds);
		return 0.5f + (0.5f * Mathf.Sin((float)(_elapsedSeconds / duration) * Mathf.Pi * 2.0f));
	}

	private float BuildBurstAlpha()
	{
		var duration = Math.Max(0.01f, BurstDurationSeconds);
		var progress = 1.0f - Mathf.Clamp(_burstTimeRemaining / duration, 0.0f, 1.0f);
		return 1.0f - SmoothStep01(progress);
	}

	private static Color WithAlpha(Color color, float alpha)
	{
		return new Color(color.R, color.G, color.B, alpha);
	}

	private static float SmoothStep01(float value)
	{
		var clamped = Mathf.Clamp(value, 0.0f, 1.0f);
		return clamped * clamped * (3.0f - (2.0f * clamped));
	}
}
