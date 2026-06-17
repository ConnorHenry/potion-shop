using Godot;

namespace OccultShop.UI;

[GlobalClass]
public partial class InventorySlotLayoutProfile : Resource
{
	private Vector2 _slotSize = new(112.0f, 168.0f);
	private Vector2 _artOffset = Vector2.Zero;
	private Vector2 _artSize = Vector2.Zero;
	private float _iconSizeRatio = 0.58f;
	private float _iconCenterYRatio = 0.43f;
	private int _nameFontSize = 10;
	private int _minimumNameFontSize = 9;
	private int _quantityFontSize = 11;
	private Color _nameColor = new(0.055f, 0.026f, 0.012f, 1.0f);
	private Color _quantityColor = new(0.13f, 0.075f, 0.032f, 1.0f);
	private bool _preserveParentheticalSuffix;
	private int _singleLineCharacterLimit = 12;
	private bool _hideQuantityWhenOne;
	private bool _useReadableNamePlaque;
	private bool _useGeneratedLabelTexture;
	private Rect2 _generatedLabelRectRatio = new(Vector2.Zero, Vector2.Zero);
	private Rect2 _generatedNameRectRatio = new(Vector2.Zero, Vector2.Zero);
	private Rect2 _generatedQuantityRectRatio = new(Vector2.Zero, Vector2.Zero);

	[Export]
	public Vector2 SlotSize
	{
		get => _slotSize;
		set
		{
			_slotSize = value;
			EmitChanged();
		}
	}

	[Export]
	public Vector2 ArtOffset
	{
		get => _artOffset;
		set
		{
			_artOffset = value;
			EmitChanged();
		}
	}

	[Export]
	public Vector2 ArtSize
	{
		get => _artSize;
		set
		{
			_artSize = value;
			EmitChanged();
		}
	}

	[Export(PropertyHint.Range, "0.1,1.5,0.01")]
	public float IconSizeRatio
	{
		get => _iconSizeRatio;
		set
		{
			_iconSizeRatio = value;
			EmitChanged();
		}
	}

	[Export(PropertyHint.Range, "0,1,0.01")]
	public float IconCenterYRatio
	{
		get => _iconCenterYRatio;
		set
		{
			_iconCenterYRatio = value;
			EmitChanged();
		}
	}

	[Export(PropertyHint.Range, "1,64,1")]
	public int NameFontSize
	{
		get => _nameFontSize;
		set
		{
			_nameFontSize = value;
			EmitChanged();
		}
	}

	[Export(PropertyHint.Range, "1,64,1")]
	public int MinimumNameFontSize
	{
		get => _minimumNameFontSize;
		set
		{
			_minimumNameFontSize = value;
			EmitChanged();
		}
	}

	[Export(PropertyHint.Range, "1,64,1")]
	public int QuantityFontSize
	{
		get => _quantityFontSize;
		set
		{
			_quantityFontSize = value;
			EmitChanged();
		}
	}

	[Export]
	public Color NameColor
	{
		get => _nameColor;
		set
		{
			_nameColor = value;
			EmitChanged();
		}
	}

	[Export]
	public Color QuantityColor
	{
		get => _quantityColor;
		set
		{
			_quantityColor = value;
			EmitChanged();
		}
	}

	[Export]
	public bool PreserveParentheticalSuffix
	{
		get => _preserveParentheticalSuffix;
		set
		{
			_preserveParentheticalSuffix = value;
			EmitChanged();
		}
	}

	[Export(PropertyHint.Range, "1,64,1")]
	public int SingleLineCharacterLimit
	{
		get => _singleLineCharacterLimit;
		set
		{
			_singleLineCharacterLimit = value;
			EmitChanged();
		}
	}

	[Export]
	public bool HideQuantityWhenOne
	{
		get => _hideQuantityWhenOne;
		set
		{
			_hideQuantityWhenOne = value;
			EmitChanged();
		}
	}

	[Export]
	public bool UseReadableNamePlaque
	{
		get => _useReadableNamePlaque;
		set
		{
			_useReadableNamePlaque = value;
			EmitChanged();
		}
	}

	[Export]
	public bool UseGeneratedLabelTexture
	{
		get => _useGeneratedLabelTexture;
		set
		{
			_useGeneratedLabelTexture = value;
			EmitChanged();
		}
	}

	[Export]
	public Rect2 GeneratedLabelRectRatio
	{
		get => _generatedLabelRectRatio;
		set
		{
			_generatedLabelRectRatio = value;
			EmitChanged();
		}
	}

	[Export]
	public Rect2 GeneratedNameRectRatio
	{
		get => _generatedNameRectRatio;
		set
		{
			_generatedNameRectRatio = value;
			EmitChanged();
		}
	}

	[Export]
	public Rect2 GeneratedQuantityRectRatio
	{
		get => _generatedQuantityRectRatio;
		set
		{
			_generatedQuantityRectRatio = value;
			EmitChanged();
		}
	}

	public Vector2 ResolveSlotSize(Vector2 fallback)
	{
		return SlotSize.X > 0.0f && SlotSize.Y > 0.0f
			? SlotSize
			: fallback;
	}

	public JarredInventorySlotLayout CreateJarredLayout(Color? nameColorOverride = null)
	{
		return new JarredInventorySlotLayout
		{
			ArtOffset = ArtOffset,
			ArtSize = ArtSize,
			IconSizeRatio = IconSizeRatio,
			IconCenterYRatio = IconCenterYRatio,
			NameFontSize = NameFontSize,
			MinimumNameFontSize = MinimumNameFontSize,
			QuantityFontSize = QuantityFontSize,
			NameColor = nameColorOverride ?? NameColor,
			QuantityColor = QuantityColor,
			PreserveParentheticalSuffix = PreserveParentheticalSuffix,
			SingleLineCharacterLimit = SingleLineCharacterLimit,
			HideQuantityWhenOne = HideQuantityWhenOne,
			UseReadableNamePlaque = UseReadableNamePlaque,
			UseGeneratedLabelTexture = UseGeneratedLabelTexture,
			GeneratedLabelRectRatio = GeneratedLabelRectRatio,
			GeneratedNameRectRatio = GeneratedNameRectRatio,
			GeneratedQuantityRectRatio = GeneratedQuantityRectRatio
		};
	}

	public void CopyFrom(InventorySlotLayoutProfile source)
	{
		SlotSize = source.SlotSize;
		ArtOffset = source.ArtOffset;
		ArtSize = source.ArtSize;
		IconSizeRatio = source.IconSizeRatio;
		IconCenterYRatio = source.IconCenterYRatio;
		NameFontSize = source.NameFontSize;
		MinimumNameFontSize = source.MinimumNameFontSize;
		QuantityFontSize = source.QuantityFontSize;
		NameColor = source.NameColor;
		QuantityColor = source.QuantityColor;
		PreserveParentheticalSuffix = source.PreserveParentheticalSuffix;
		SingleLineCharacterLimit = source.SingleLineCharacterLimit;
		HideQuantityWhenOne = source.HideQuantityWhenOne;
		UseReadableNamePlaque = source.UseReadableNamePlaque;
		UseGeneratedLabelTexture = source.UseGeneratedLabelTexture;
		GeneratedLabelRectRatio = source.GeneratedLabelRectRatio;
		GeneratedNameRectRatio = source.GeneratedNameRectRatio;
		GeneratedQuantityRectRatio = source.GeneratedQuantityRectRatio;
	}

	public static InventorySlotLayoutProfile CreateIngredientShelfDefault()
	{
		return new InventorySlotLayoutProfile
		{
			SlotSize = new Vector2(116.0f, 160.0f),
			ArtOffset = new Vector2(0.0f, -4.0f),
			IconSizeRatio = 0.62f,
			NameFontSize = 12,
			MinimumNameFontSize = 9,
			QuantityFontSize = 13,
			PreserveParentheticalSuffix = true,
			SingleLineCharacterLimit = 18,
			UseReadableNamePlaque = true,
			UseGeneratedLabelTexture = true,
			GeneratedNameRectRatio = new Rect2(new Vector2(0.18f, 0.653f), new Vector2(0.64f, 0.16f))
		};
	}

	public static InventorySlotLayoutProfile CreateConsumableShelfDefault()
	{
		return new InventorySlotLayoutProfile
		{
			SlotSize = new Vector2(104.0f, 160.0f),
			NameFontSize = 14,
			MinimumNameFontSize = 12,
			QuantityFontSize = 14,
			SingleLineCharacterLimit = 12,
			UseGeneratedLabelTexture = true
		};
	}

	public static InventorySlotLayoutProfile CreatePotionInventoryDefault()
	{
		return new InventorySlotLayoutProfile
		{
			SlotSize = new Vector2(112.0f, 168.0f),
			NameColor = new Color(0.13f, 0.075f, 0.032f, 1.0f),
			NameFontSize = 12,
			MinimumNameFontSize = 9,
			SingleLineCharacterLimit = 10,
			QuantityFontSize = 16,
			UseReadableNamePlaque = true,
			UseGeneratedLabelTexture = true,
			GeneratedLabelRectRatio = new Rect2(new Vector2(0.03f, 0.634f), new Vector2(0.94f, 0.34f)),
			GeneratedNameRectRatio = new Rect2(new Vector2(0.08f, 0.657f), new Vector2(0.84f, 0.20f)),
			GeneratedQuantityRectRatio = new Rect2(new Vector2(0.36f, 0.858f), new Vector2(0.28f, 0.17f))
		};
	}

	public static InventorySlotLayoutProfile CreateCustomerPotionDefault()
	{
		return new InventorySlotLayoutProfile
		{
			SlotSize = new Vector2(94.0f, 132.0f),
			IconSizeRatio = 0.54f,
			NameFontSize = 8,
			MinimumNameFontSize = 9,
			QuantityFontSize = 10
		};
	}
}
