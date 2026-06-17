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
	public bool UseReadableNamePlaque { get; init; }
	public bool UseGeneratedLabelTexture { get; init; }
	public Rect2 GeneratedLabelRectRatio { get; init; } = new(Vector2.Zero, Vector2.Zero);
	public Rect2 GeneratedNameRectRatio { get; init; } = new(Vector2.Zero, Vector2.Zero);
	public Rect2 GeneratedQuantityRectRatio { get; init; } = new(Vector2.Zero, Vector2.Zero);
}

public static class JarredInventorySlotView
{
	private const string JarOverlayPath = "res://Assets/Art/BrewingStationBright/ingredient_jar_overlay_bright.png";
	private const string JarLabelOverlayPath = "res://Assets/Art/BrewingStationBright/ingredient_label_overlay_bright.png";
	private const string PotionBottleOverlayPath = "res://Assets/Art/BrewingStationBright/potion_card_overlay_bright.png";
	private const int PlaqueSingleLineCharacterLimit = 12;
	private const float LabelOverlayLeftRatio = 0.130859375f;
	private const float LabelOverlayTopRatio = 0.66861979f;
	private const float LabelOverlayWidthRatio = 0.73828125f;
	private const float LabelOverlayHeightRatio = 0.27278647f;
	private const float NameLeftRatio = 0.065f;
	private const float NameTopRatio = 0.652f;
	private const float NameWidthRatio = 0.87f;
	private const float NameHeightRatio = 0.20f;
	private const float ReadableNameLeftRatio = 0.052f;
	private const float ReadableNameTopRatio = 0.655f;
	private const float ReadableNameWidthRatio = 0.896f;
	private const float ReadableNameHeightRatio = 0.215f;
	private const float GeneratedNameLeftRatio = 0.18f;
	private const float GeneratedNameTopRatio = 0.681f;
	private const float GeneratedNameWidthRatio = 0.64f;
	private const float GeneratedNameHeightRatio = 0.16f;
	private const float NameLineSpacingRatio = 0.006f;
	private const float QuantityLeftRatio = 0.34f;
	private const float QuantityTopRatio = 0.852f;
	private const float QuantityWidthRatio = 0.32f;
	private const float QuantityHeightRatio = 0.105f;
	private const float ReadableQuantityCenterXRatio = 0.5f;
	private const float ReadableQuantityCenterYRatio = 0.884f;
	private const float ReadableQuantityWidthRatio = 0.50f;
	private const float ReadableQuantityHeightRatio = 0.12f;
	private const float GeneratedQuantityCenterXRatio = 0.5f;
	private const float GeneratedQuantityCenterYRatio = 0.883f;
	private const float GeneratedQuantityWidthRatio = 0.38f;
	private const float GeneratedQuantityHeightRatio = 0.16f;
	private const float ReadableNameTextInset = 4.0f;
	private const float GeneratedNameTextInset = 3.0f;
	private const float NameFitSafetyPadding = 2.0f;
	private const float EstimatedAverageGlyphWidthRatio = 0.55f;

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
		if (layout.UseGeneratedLabelTexture)
			content.AddChild(CreateLabelOverlay(artPosition, artSize, layout));
		content.AddChild(CreateNameBlock(itemName, artPosition, artSize, layout));
		content.AddChild(CreateQuantityBlock(quantity, artPosition, artSize, layout));
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
		if (layout.UseGeneratedLabelTexture)
			content.AddChild(CreateLabelOverlay(artPosition, artSize, layout));
		content.AddChild(CreateNameBlock(itemName, artPosition, artSize, layout));
		content.AddChild(CreateQuantityBlock(quantity, artPosition, artSize, layout));
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

	private static TextureRect CreateLabelOverlay(Vector2 artPosition, Vector2 artSize, JarredInventorySlotLayout layout)
	{
		var labelRect = ResolveGeneratedLabelRect(artPosition, artSize, layout);
		return new TextureRect
		{
			Name = "JarLabelOverlay",
			Position = labelRect.Position,
			CustomMinimumSize = labelRect.Size,
			Size = labelRect.Size,
			Texture = UiIconLoader.LoadIcon(JarLabelOverlayPath),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
	}

	private static Rect2 ResolveGeneratedLabelRect(Vector2 artPosition, Vector2 artSize, JarredInventorySlotLayout layout)
	{
		var defaultRatioRect = new Rect2(
			new Vector2(LabelOverlayLeftRatio, LabelOverlayTopRatio),
			new Vector2(LabelOverlayWidthRatio, LabelOverlayHeightRatio));
		return ScaleRatioRect(
			artPosition,
			artSize,
			ResolveCustomRatioRect(layout.GeneratedLabelRectRatio, defaultRatioRect));
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
		var nameRect = ResolveNameRect(artPosition, artSize, layout);
		var nameSize = nameRect.Size;
		var block = new Control
		{
			Name = "NameBlock",
			Position = nameRect.Position,
			CustomMinimumSize = nameSize,
			Size = nameSize,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		if (layout.UseReadableNamePlaque && !layout.UseGeneratedLabelTexture)
			block.AddChild(CreateReadableNamePlaque(nameSize));

		var lines = BuildPlaqueNameLines(itemName, layout);
		var lineSpacing = artSize.Y * NameLineSpacingRatio;
		var lineHeight = ResolveNameLineHeight(lines.Length, nameSize.Y, lineSpacing, layout.NameFontSize);
		var totalTextHeight = (lineHeight * lines.Length) + (lineSpacing * Mathf.Max(0, lines.Length - 1));
		var topOffset = Mathf.Max(0.0f, (nameSize.Y - totalTextHeight) * 0.5f);
		var horizontalInset = ResolveNameHorizontalInset(layout);
		var lineWidth = Mathf.Max(1.0f, nameSize.X - (horizontalInset * 2.0f));
		for (var index = 0; index < lines.Length; index += 1)
		{
			block.AddChild(CreateNameLine(
				lines[index],
				horizontalInset,
				topOffset + (index * (lineHeight + lineSpacing)),
				lineHeight,
				lineWidth,
				layout.NameFontSize,
				layout));
		}

		return block;
	}

	private static float ResolveNameHorizontalInset(JarredInventorySlotLayout layout)
	{
		if (layout.UseGeneratedLabelTexture)
			return GeneratedNameTextInset;
		return layout.UseReadableNamePlaque ? ReadableNameTextInset : 0.0f;
	}

	private static Rect2 ResolveNameRect(Vector2 artPosition, Vector2 artSize, JarredInventorySlotLayout layout)
	{
		var leftRatio = NameLeftRatio;
		var topRatio = NameTopRatio;
		var widthRatio = NameWidthRatio;
		var heightRatio = NameHeightRatio;
		if (layout.UseGeneratedLabelTexture)
		{
			var defaultRatioRect = new Rect2(
				new Vector2(GeneratedNameLeftRatio, GeneratedNameTopRatio),
				new Vector2(GeneratedNameWidthRatio, GeneratedNameHeightRatio));
			return ScaleRatioRect(
				artPosition,
				artSize,
				ResolveCustomRatioRect(layout.GeneratedNameRectRatio, defaultRatioRect));
		}
		else if (layout.UseReadableNamePlaque)
		{
			leftRatio = ReadableNameLeftRatio;
			topRatio = ReadableNameTopRatio;
			widthRatio = ReadableNameWidthRatio;
			heightRatio = ReadableNameHeightRatio;
		}

		return new Rect2(
			new Vector2(artPosition.X + (artSize.X * leftRatio), artPosition.Y + (artSize.Y * topRatio)),
			new Vector2(artSize.X * widthRatio, artSize.Y * heightRatio));
	}

	private static PanelContainer CreateReadableNamePlaque(Vector2 size)
	{
		var plaque = new PanelContainer
		{
			Name = "ReadableNamePlaque",
			Position = Vector2.Zero,
			CustomMinimumSize = size,
			Size = size,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		plaque.AddThemeStyleboxOverride("panel", CreateReadableNamePlaqueStyleBox());
		return plaque;
	}

	private static StyleBoxFlat CreateReadableNamePlaqueStyleBox()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.62f, 0.42f, 0.24f, 0.97f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			BorderColor = new Color(0.13f, 0.065f, 0.025f, 0.98f),
			CornerRadiusTopLeft = 2,
			CornerRadiusTopRight = 2,
			CornerRadiusBottomRight = 2,
			CornerRadiusBottomLeft = 2
		};
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
		float leftOffset,
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
			Position = new Vector2(leftOffset, topOffset),
			CustomMinimumSize = new Vector2(width, lineHeight),
			Size = new Vector2(width, lineHeight),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.Off,
			ClipText = true,
			TextOverrunBehavior = TextServer.OverrunBehavior.NoTrimming,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		label.AddThemeColorOverride("font_color", layout.NameColor);
		label.AddThemeColorOverride("font_outline_color", new Color(0.78f, 0.56f, 0.32f, 0.48f));
		label.AddThemeFontSizeOverride("font_size", ResolveNameFontSize(label, text, width, lineHeight, fontSize, layout));
		label.AddThemeConstantOverride("outline_size", 1);
		return label;
	}

	private static int ResolveNameFontSize(
		Label label,
		string text,
		float width,
		float lineHeight,
		int maximumFontSize,
		JarredInventorySlotLayout layout)
	{
		maximumFontSize = ResolveVerticalFontSize(maximumFontSize, lineHeight, layout.MinimumNameFontSize);
		if (string.IsNullOrWhiteSpace(text) || maximumFontSize <= layout.MinimumNameFontSize)
			return maximumFontSize;

		var availableWidth = Mathf.Max(1.0f, width - NameFitSafetyPadding);
		for (var fontSize = maximumFontSize; fontSize >= layout.MinimumNameFontSize; fontSize -= 1)
		{
			if (FitsNameLine(label, text, availableWidth, fontSize))
				return fontSize;
		}

		return layout.MinimumNameFontSize;
	}

	private static int ResolveVerticalFontSize(int maximumFontSize, float lineHeight, int minimumFontSize)
	{
		var effectiveMaximum = System.Math.Max(1, maximumFontSize);
		var effectiveMinimum = System.Math.Clamp(minimumFontSize, 1, effectiveMaximum);
		var verticalMaximum = System.Math.Max(1, (int)System.Math.Floor(lineHeight - 2.0f));
		return System.Math.Clamp(System.Math.Min(effectiveMaximum, verticalMaximum), effectiveMinimum, effectiveMaximum);
	}

	private static bool FitsNameLine(Label label, string text, float availableWidth, int fontSize)
	{
		var font = label.GetThemeFont("font");
		if (font is not null)
			return font.GetStringSize(text, HorizontalAlignment.Left, -1.0f, fontSize).X <= availableWidth;

		return EstimateNameLineWidth(text, fontSize) <= availableWidth;
	}

	private static float EstimateNameLineWidth(string text, int fontSize)
	{
		return text.Length * fontSize * EstimatedAverageGlyphWidthRatio;
	}

	private static Control CreateQuantityBlock(int quantity, Vector2 artPosition, Vector2 artSize, JarredInventorySlotLayout layout)
	{
		var quantityRect = ResolveQuantityRect(artPosition, artSize, layout);
		if (!layout.UseReadableNamePlaque)
			return CreateQuantityLabel(quantity, quantityRect, layout);

		var block = new Control
		{
			Name = "QuantityBlock",
			Position = quantityRect.Position,
			CustomMinimumSize = quantityRect.Size,
			Size = quantityRect.Size,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		if (!layout.UseGeneratedLabelTexture)
			block.AddChild(CreateReadableQuantityBadge(quantityRect.Size));
		block.AddChild(CreateQuantityLabel(
			quantity,
			new Rect2(Vector2.Zero, quantityRect.Size),
			layout));
		return block;
	}

	private static PanelContainer CreateReadableQuantityBadge(Vector2 size)
	{
		var badge = new PanelContainer
		{
			Name = "ReadableQuantityBadge",
			Position = Vector2.Zero,
			CustomMinimumSize = size,
			Size = size,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		badge.AddThemeStyleboxOverride("panel", CreateReadableQuantityBadgeStyleBox());
		return badge;
	}

	private static StyleBoxFlat CreateReadableQuantityBadgeStyleBox()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.56f, 0.36f, 0.19f, 0.98f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			BorderColor = new Color(0.10f, 0.045f, 0.018f, 1.0f),
			CornerRadiusTopLeft = 14,
			CornerRadiusTopRight = 14,
			CornerRadiusBottomRight = 14,
			CornerRadiusBottomLeft = 14
		};
	}

	private static Label CreateQuantityLabel(int quantity, Rect2 quantityRect, JarredInventorySlotLayout layout)
	{
		var label = new Label
		{
			Name = "Quantity",
			Text = layout.HideQuantityWhenOne && quantity <= 1 ? string.Empty : quantity.ToString(),
			Visible = !layout.HideQuantityWhenOne || quantity > 1,
			Position = quantityRect.Position,
			CustomMinimumSize = quantityRect.Size,
			Size = quantityRect.Size,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			ClipText = true,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		label.AddThemeColorOverride("font_color", layout.QuantityColor);
		label.AddThemeColorOverride("font_outline_color", new Color(0.78f, 0.56f, 0.32f, 0.48f));
		label.AddThemeFontSizeOverride("font_size", layout.QuantityFontSize);
		label.AddThemeConstantOverride("outline_size", 1);
		return label;
	}

	private static Rect2 ResolveQuantityRect(Vector2 artPosition, Vector2 artSize, JarredInventorySlotLayout layout)
	{
		if (layout.UseGeneratedLabelTexture && HasCustomRatioRect(layout.GeneratedQuantityRectRatio))
		{
			var defaultRatioRect = layout.UseReadableNamePlaque
				? BuildReadableQuantityRatioRect(generatedLabelTexture: true)
				: new Rect2(
					new Vector2(QuantityLeftRatio, QuantityTopRatio),
					new Vector2(QuantityWidthRatio, QuantityHeightRatio));
			return ScaleRatioRect(
				artPosition,
				artSize,
				ResolveCustomRatioRect(layout.GeneratedQuantityRectRatio, defaultRatioRect));
		}

		if (!layout.UseReadableNamePlaque)
		{
			return new Rect2(
				new Vector2(artPosition.X + (artSize.X * QuantityLeftRatio), artPosition.Y + (artSize.Y * QuantityTopRatio)),
				new Vector2(artSize.X * QuantityWidthRatio, artSize.Y * QuantityHeightRatio));
		}

		var centerXRatio = layout.UseGeneratedLabelTexture ? GeneratedQuantityCenterXRatio : ReadableQuantityCenterXRatio;
		var centerYRatio = layout.UseGeneratedLabelTexture ? GeneratedQuantityCenterYRatio : ReadableQuantityCenterYRatio;
		var widthRatio = layout.UseGeneratedLabelTexture ? GeneratedQuantityWidthRatio : ReadableQuantityWidthRatio;
		var heightRatio = layout.UseGeneratedLabelTexture ? GeneratedQuantityHeightRatio : ReadableQuantityHeightRatio;

		var quantitySize = new Vector2(artSize.X * widthRatio, artSize.Y * heightRatio);
		var quantityCenter = new Vector2(
			artPosition.X + (artSize.X * centerXRatio),
			artPosition.Y + (artSize.Y * centerYRatio));
		return new Rect2(quantityCenter - (quantitySize * 0.5f), quantitySize);
	}

	private static Rect2 BuildReadableQuantityRatioRect(bool generatedLabelTexture)
	{
		var centerXRatio = generatedLabelTexture ? GeneratedQuantityCenterXRatio : ReadableQuantityCenterXRatio;
		var centerYRatio = generatedLabelTexture ? GeneratedQuantityCenterYRatio : ReadableQuantityCenterYRatio;
		var widthRatio = generatedLabelTexture ? GeneratedQuantityWidthRatio : ReadableQuantityWidthRatio;
		var heightRatio = generatedLabelTexture ? GeneratedQuantityHeightRatio : ReadableQuantityHeightRatio;
		return new Rect2(
			new Vector2(centerXRatio - (widthRatio * 0.5f), centerYRatio - (heightRatio * 0.5f)),
			new Vector2(widthRatio, heightRatio));
	}

	private static bool HasCustomRatioRect(Rect2 rect)
	{
		return rect.Position != Vector2.Zero || rect.Size != Vector2.Zero;
	}

	private static Rect2 ResolveCustomRatioRect(Rect2 customRatioRect, Rect2 defaultRatioRect)
	{
		if (!HasCustomRatioRect(customRatioRect))
			return defaultRatioRect;

		var customSize = customRatioRect.Size;
		var resolvedSize = new Vector2(
			customSize.X > 0.0f ? customSize.X : defaultRatioRect.Size.X,
			customSize.Y > 0.0f ? customSize.Y : defaultRatioRect.Size.Y);
		return new Rect2(customRatioRect.Position, resolvedSize);
	}

	private static Rect2 ScaleRatioRect(Vector2 artPosition, Vector2 artSize, Rect2 ratioRect)
	{
		return new Rect2(
			new Vector2(
				artPosition.X + (artSize.X * ratioRect.Position.X),
				artPosition.Y + (artSize.Y * ratioRect.Position.Y)),
			new Vector2(
				artSize.X * ratioRect.Size.X,
				artSize.Y * ratioRect.Size.Y));
	}

	private static string[] BuildPlaqueNameLines(string itemName, JarredInventorySlotLayout layout)
	{
		if (string.IsNullOrWhiteSpace(itemName))
			return new[] { itemName };

		var trimmedName = layout.PreserveParentheticalSuffix
			? itemName.Trim()
			: StripParentheticalSuffix(itemName.Trim());
		if (layout.PreserveParentheticalSuffix &&
			TrySplitPreparedPlaqueName(trimmedName, out var baseName, out var preparationName))
		{
			return new[] { baseName, $"({preparationName})" };
		}

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

	private static bool TrySplitPreparedPlaqueName(string itemName, out string baseName, out string preparationName)
	{
		return TrySplitPreparedPlaqueName(itemName, " (", ')', out baseName, out preparationName) ||
			TrySplitPreparedPlaqueName(itemName, " [", ']', out baseName, out preparationName);
	}

	private static bool TrySplitPreparedPlaqueName(
		string itemName,
		string suffixMarker,
		char closingCharacter,
		out string baseName,
		out string preparationName)
	{
		baseName = string.Empty;
		preparationName = string.Empty;
		if (string.IsNullOrWhiteSpace(itemName) || !itemName.EndsWith(closingCharacter))
			return false;

		var suffixStart = itemName.LastIndexOf(suffixMarker, System.StringComparison.Ordinal);
		if (suffixStart <= 0)
			return false;

		var suffixContentStart = suffixStart + suffixMarker.Length;
		var suffixContentLength = itemName.Length - suffixContentStart - 1;
		if (suffixContentLength <= 0)
			return false;

		baseName = itemName[..suffixStart].Trim();
		preparationName = itemName.Substring(suffixContentStart, suffixContentLength).Trim();
		return !string.IsNullOrWhiteSpace(baseName) && !string.IsNullOrWhiteSpace(preparationName);
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

[Tool]
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
