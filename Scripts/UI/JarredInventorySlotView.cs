using Godot;

namespace OccultShop.UI;

public sealed class JarredInventorySlotLayout
{
	public Vector2 ArtOffset { get; init; } = Vector2.Zero;
	public Vector2 ArtSize { get; init; } = Vector2.Zero;
	public float IconSizeRatio { get; init; } = 0.58f;
	public float IconCenterYRatio { get; init; } = 0.43f;
	public int NameFontSize { get; init; } = 10;
	public int MinimumNameFontSize { get; init; } = 9;
	public int QuantityFontSize { get; init; } = 11;
	public Color NameColor { get; init; } = new(0.055f, 0.026f, 0.012f, 1.0f);
	public Color QuantityColor { get; init; } = new(0.13f, 0.075f, 0.032f, 1.0f);
	public bool PreserveParentheticalSuffix { get; init; }
	public int SingleLineCharacterLimit { get; init; } = 12;
	public bool HideQuantityWhenOne { get; init; }
}

public static class JarredInventorySlotView
{
	private const string JarOverlayPath = "res://Assets/UI/ingredient_jar_overlay.png";
	private const string PotionBottleOverlayPath = "res://Assets/UI/potion_bottle_overlay.png";
	private const int PlaqueSingleLineCharacterLimit = 12;
	private const float NameLeftRatio = 0.065f;
	private const float NameTopRatio = 0.672f;
	private const float NameWidthRatio = 0.87f;
	private const float NameHeightRatio = 0.17f;
	private const float NameLineSpacingRatio = 0.006f;
	private const float QuantityLeftRatio = 0.34f;
	private const float QuantityTopRatio = 0.852f;
	private const float QuantityWidthRatio = 0.32f;
	private const float QuantityHeightRatio = 0.105f;

	public static Control CreateContent(
		Vector2 slotSize,
		string itemName,
		string? iconPath,
		int quantity,
		JarredInventorySlotLayout? layout = null)
	{
		layout ??= new JarredInventorySlotLayout();
		var content = new Control
		{
			Name = "JarSlotContent",
			CustomMinimumSize = slotSize,
			Size = slotSize,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};

		var artSize = ResolveArtSize(slotSize, layout);
		var artPosition = ResolveArtPosition(slotSize, artSize, layout);
		content.AddChild(CreateIcon(iconPath, artPosition, artSize, layout));
		content.AddChild(CreateOverlay(artPosition, artSize));
		content.AddChild(CreateNameBlock(itemName, artPosition, artSize, layout));
		content.AddChild(CreateQuantityLabel(quantity, artPosition, artSize, layout));
		return content;
	}

	public static Control CreatePotionContent(
		Vector2 slotSize,
		string itemName,
		string potionItemId,
		int quantity,
		JarredInventorySlotLayout? layout = null)
	{
		layout ??= new JarredInventorySlotLayout();
		var content = new Control
		{
			Name = "PotionSlotContent",
			CustomMinimumSize = slotSize,
			Size = slotSize,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};

		var artSize = ResolveArtSize(slotSize, layout);
		var artPosition = ResolveArtPosition(slotSize, artSize, layout);
		content.AddChild(CreatePotionLiquid(potionItemId, artPosition, artSize));
		content.AddChild(CreatePotionOverlay(artPosition, artSize));
		content.AddChild(CreateNameBlock(itemName, artPosition, artSize, layout));
		content.AddChild(CreateQuantityLabel(quantity, artPosition, artSize, layout));
		return content;
	}

	private static Vector2 ResolveArtSize(Vector2 slotSize, JarredInventorySlotLayout layout)
	{
		return layout.ArtSize == Vector2.Zero ? slotSize : layout.ArtSize;
	}

	private static Vector2 ResolveArtPosition(Vector2 slotSize, Vector2 artSize, JarredInventorySlotLayout layout)
	{
		return new Vector2(
			((slotSize.X - artSize.X) * 0.5f) + layout.ArtOffset.X,
			layout.ArtOffset.Y);
	}

	private static TextureRect CreateIcon(string? iconPath, Vector2 artPosition, Vector2 artSize, JarredInventorySlotLayout layout)
	{
		var iconSize = Mathf.Clamp(artSize.X * layout.IconSizeRatio, 32.0f, artSize.X * 0.72f);
		var iconTop = artPosition.Y + (artSize.Y * layout.IconCenterYRatio) - (iconSize * 0.5f);
		return new TextureRect
		{
			Name = "Icon",
			Position = new Vector2(artPosition.X + ((artSize.X - iconSize) * 0.5f), iconTop),
			CustomMinimumSize = new Vector2(iconSize, iconSize),
			Size = new Vector2(iconSize, iconSize),
			Texture = UiIconLoader.LoadIcon(iconPath),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
	}

	private static TextureRect CreateOverlay(Vector2 artPosition, Vector2 artSize)
	{
		return new TextureRect
		{
			Name = "JarOverlay",
			Position = artPosition,
			CustomMinimumSize = artSize,
			Size = artSize,
			Texture = UiIconLoader.LoadIcon(JarOverlayPath),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
	}

	private static Control CreatePotionLiquid(string potionItemId, Vector2 artPosition, Vector2 artSize)
	{
		return new PotionLiquidView
		{
			Name = "PotionLiquid",
			Position = artPosition,
			CustomMinimumSize = artSize,
			Size = artSize,
			PotionItemId = potionItemId,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
	}

	private static TextureRect CreatePotionOverlay(Vector2 artPosition, Vector2 artSize)
	{
		return new TextureRect
		{
			Name = "PotionBottleOverlay",
			Position = artPosition,
			CustomMinimumSize = artSize,
			Size = artSize,
			Texture = UiIconLoader.LoadIcon(PotionBottleOverlayPath),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
	}

	private static Control CreateNameBlock(string itemName, Vector2 artPosition, Vector2 artSize, JarredInventorySlotLayout layout)
	{
		var nameSize = new Vector2(artSize.X * NameWidthRatio, artSize.Y * NameHeightRatio);
		var block = new Control
		{
			Name = "NameBlock",
			Position = new Vector2(artPosition.X + (artSize.X * NameLeftRatio), artPosition.Y + (artSize.Y * NameTopRatio)),
			CustomMinimumSize = nameSize,
			Size = nameSize,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};

		var lines = BuildPlaqueNameLines(itemName, layout);
		var lineSpacing = artSize.Y * NameLineSpacingRatio;
		var lineHeight = ResolveNameLineHeight(lines.Length, nameSize.Y, lineSpacing, layout.NameFontSize);
		var totalTextHeight = (lineHeight * lines.Length) + (lineSpacing * Mathf.Max(0, lines.Length - 1));
		var topOffset = Mathf.Max(0.0f, (nameSize.Y - totalTextHeight) * 0.5f);
		for (var index = 0; index < lines.Length; index += 1)
		{
			block.AddChild(CreateNameLine(
				lines[index],
				topOffset + (index * (lineHeight + lineSpacing)),
				lineHeight,
				nameSize.X,
				ResolveNameFontSize(lines[index], nameSize.X, layout),
				layout));
		}

		return block;
	}

	private static float ResolveNameLineHeight(int lineCount, float nameBlockHeight, float lineSpacing, int fontSize)
	{
		if (lineCount <= 1)
			return Mathf.Max(fontSize + 2.0f, nameBlockHeight * 0.5f);

		var totalSpacing = lineSpacing * (lineCount - 1);
		var availableLineHeight = (nameBlockHeight - totalSpacing) / lineCount;
		return Mathf.Max(1.0f, availableLineHeight);
	}

	private static Label CreateNameLine(
		string text,
		float topOffset,
		float lineHeight,
		float width,
		int fontSize,
		JarredInventorySlotLayout layout)
	{
		var label = new Label
		{
			Name = "Name",
			Text = text,
			Position = new Vector2(0.0f, topOffset),
			CustomMinimumSize = new Vector2(width, lineHeight),
			Size = new Vector2(width, lineHeight),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.Off,
			ClipText = true,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		label.AddThemeColorOverride("font_color", layout.NameColor);
		label.AddThemeColorOverride("font_outline_color", new Color(0.78f, 0.56f, 0.32f, 0.48f));
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeConstantOverride("outline_size", 1);
		return label;
	}

	private static int ResolveNameFontSize(string text, float width, JarredInventorySlotLayout layout)
	{
		if (string.IsNullOrWhiteSpace(text) || layout.NameFontSize <= layout.MinimumNameFontSize)
			return layout.NameFontSize;

		const float EstimatedAverageGlyphWidthRatio = 0.5f;
		var estimatedWidth = text.Length * layout.NameFontSize * EstimatedAverageGlyphWidthRatio;
		if (estimatedWidth <= width)
			return layout.NameFontSize;

		var fitSize = (int)(width / (text.Length * EstimatedAverageGlyphWidthRatio));
		return System.Math.Clamp(fitSize, layout.MinimumNameFontSize, layout.NameFontSize);
	}

	private static Label CreateQuantityLabel(int quantity, Vector2 artPosition, Vector2 artSize, JarredInventorySlotLayout layout)
	{
		var quantitySize = new Vector2(artSize.X * QuantityWidthRatio, artSize.Y * QuantityHeightRatio);
		var label = new Label
		{
			Name = "Quantity",
			Text = layout.HideQuantityWhenOne && quantity <= 1 ? string.Empty : quantity.ToString(),
			Visible = !layout.HideQuantityWhenOne || quantity > 1,
			Position = new Vector2(artPosition.X + (artSize.X * QuantityLeftRatio), artPosition.Y + (artSize.Y * QuantityTopRatio)),
			CustomMinimumSize = quantitySize,
			Size = quantitySize,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			ClipText = true,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		label.AddThemeColorOverride("font_color", layout.QuantityColor);
		label.AddThemeFontSizeOverride("font_size", layout.QuantityFontSize);
		return label;
	}

	private static string[] BuildPlaqueNameLines(string itemName, JarredInventorySlotLayout layout)
	{
		if (string.IsNullOrWhiteSpace(itemName))
			return new[] { itemName };

		var trimmedName = layout.PreserveParentheticalSuffix
			? itemName.Trim()
			: StripParentheticalSuffix(itemName.Trim());
		var singleLineCharacterLimit = layout.SingleLineCharacterLimit > 0
			? layout.SingleLineCharacterLimit
			: PlaqueSingleLineCharacterLimit;
		if (trimmedName.Length <= singleLineCharacterLimit)
			return new[] { trimmedName };

		var words = trimmedName.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
		if (words.Length <= 1)
			return new[] { trimmedName };

		return SplitBalancedPlaqueName(words);
	}

	private static string[] SplitBalancedPlaqueName(string[] words)
	{
		var splitIndex = 1;
		var bestScore = int.MaxValue;
		for (var index = 1; index < words.Length; index += 1)
		{
			var firstLineLength = GetJoinedWordLength(words, 0, index);
			var secondLineLength = GetJoinedWordLength(words, index, words.Length - index);
			var score = (System.Math.Max(firstLineLength, secondLineLength) * 100) +
				System.Math.Abs(firstLineLength - secondLineLength);
			if (score >= bestScore)
				continue;

			bestScore = score;
			splitIndex = index;
		}

		var firstLine = string.Join(' ', words, 0, splitIndex);
		var secondLine = string.Join(' ', words, splitIndex, words.Length - splitIndex);
		return string.IsNullOrWhiteSpace(secondLine)
			? new[] { firstLine }
			: new[] { firstLine, secondLine };
	}

	private static int GetJoinedWordLength(string[] words, int startIndex, int count)
	{
		var length = 0;
		for (var offset = 0; offset < count; offset += 1)
		{
			if (offset > 0)
				length += 1;
			length += words[startIndex + offset].Length;
		}

		return length;
	}

	private static string StripParentheticalSuffix(string itemName)
	{
		var parentheticalStart = itemName.IndexOf(" (", System.StringComparison.Ordinal);
		if (parentheticalStart <= 0)
			return itemName;

		var visibleName = itemName[..parentheticalStart].Trim();
		return string.IsNullOrWhiteSpace(visibleName) ? itemName : visibleName;
	}
}

public partial class PotionLiquidView : Control
{
	private static readonly Color[] LiquidColors =
	{
		new(0.88f, 0.16f, 0.47f, 0.92f),
		new(0.24f, 0.65f, 0.94f, 0.92f),
		new(0.25f, 0.78f, 0.42f, 0.92f),
		new(0.94f, 0.66f, 0.18f, 0.92f),
		new(0.62f, 0.36f, 0.94f, 0.92f),
		new(0.10f, 0.74f, 0.72f, 0.92f),
		new(0.85f, 0.22f, 0.18f, 0.92f),
		new(0.93f, 0.85f, 0.24f, 0.92f)
	};

	public string PotionItemId { get; set; } = string.Empty;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		Resized += QueueRedraw;
		QueueRedraw();
	}

	public override void _ExitTree()
	{
		Resized -= QueueRedraw;
	}

	public override void _Draw()
	{
		if (Size.X <= 0.0f || Size.Y <= 0.0f)
			return;

		var hash = BuildStableHash(PotionItemId);
		var baseColor = LiquidColors[(int)(hash % (uint)LiquidColors.Length)];
		var darkColor = Mix(baseColor, Colors.Black, 0.22f);
		var lightColor = Mix(baseColor, Colors.White, 0.38f);
		var surfaceColor = Mix(baseColor, Colors.White, 0.52f);

		var left = Size.X * 0.255f;
		var right = Size.X * 0.745f;
		var bottom = Size.Y * 0.592f;
		var heightVariance = ((hash >> 8) & 0xFF) / 255.0f;
		var fillHeight = Size.Y * (0.185f + (heightVariance * 0.07f));
		var surfaceY = bottom - fillHeight;
		var liquidWidth = right - left;

		DrawRect(new Rect2(new Vector2(left, surfaceY), new Vector2(liquidWidth, bottom - surfaceY)), darkColor);
		DrawCircle(new Vector2(left + (liquidWidth * 0.18f), bottom), liquidWidth * 0.18f, darkColor);
		DrawCircle(new Vector2(right - (liquidWidth * 0.18f), bottom), liquidWidth * 0.18f, darkColor);
		DrawRect(new Rect2(new Vector2(left, surfaceY), new Vector2(liquidWidth, (bottom - surfaceY) * 0.46f)), baseColor);
		DrawLine(
			new Vector2(left + (liquidWidth * 0.07f), surfaceY),
			new Vector2(right - (liquidWidth * 0.07f), surfaceY + (Size.Y * 0.004f)),
			surfaceColor,
			Mathf.Max(1.0f, Size.Y * 0.014f));
		DrawLine(
			new Vector2(left + (liquidWidth * 0.12f), surfaceY + (Size.Y * 0.028f)),
			new Vector2(left + (liquidWidth * 0.32f), surfaceY + (Size.Y * 0.018f)),
			lightColor,
			Mathf.Max(1.0f, Size.Y * 0.008f));

		DrawBubbles(hash, left, right, surfaceY, bottom, lightColor);
	}

	private void DrawBubbles(uint hash, float left, float right, float surfaceY, float bottom, Color color)
	{
		var bubbleCount = 2 + (int)((hash >> 20) % 4);
		for (var index = 0; index < bubbleCount; index += 1)
		{
			var offsetHash = Rotate(hash, index * 7);
			var xRatio = ((offsetHash >> 4) & 0xFF) / 255.0f;
			var yRatio = ((offsetHash >> 12) & 0xFF) / 255.0f;
			var radiusRatio = ((offsetHash >> 22) & 0x3F) / 63.0f;
			var x = Mathf.Lerp(left + (Size.X * 0.045f), right - (Size.X * 0.045f), xRatio);
			var y = Mathf.Lerp(surfaceY + (Size.Y * 0.028f), bottom - (Size.Y * 0.032f), yRatio);
			var radius = Size.X * (0.010f + (radiusRatio * 0.012f));

			DrawCircle(new Vector2(x, y), radius, color);
		}
	}

	private static uint BuildStableHash(string text)
	{
		const uint offsetBasis = 2166136261u;
		const uint prime = 16777619u;
		var hash = offsetBasis;

		if (string.IsNullOrWhiteSpace(text))
			return hash;

		foreach (var character in text)
		{
			hash ^= char.ToUpperInvariant(character);
			hash *= prime;
		}

		return hash;
	}

	private static uint Rotate(uint value, int amount)
	{
		amount &= 31;
		if (amount == 0)
			return value;

		return (value << amount) | (value >> (32 - amount));
	}

	private static Color Mix(Color first, Color second, float amount)
	{
		return new Color(
			Mathf.Lerp(first.R, second.R, amount),
			Mathf.Lerp(first.G, second.G, amount),
			Mathf.Lerp(first.B, second.B, amount),
			first.A);
	}
}
