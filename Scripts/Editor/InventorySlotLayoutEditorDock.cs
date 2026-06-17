using System;
using System.Collections.Generic;
using Godot;
using OccultShop.UI;

namespace OccultShop.Editor;

[Tool]
public partial class InventorySlotLayoutEditorDock : ScrollContainer
{
	private const string SampleIngredientIconPath = "res://Assets/Items/mint.png";
	private const string SampleConsumableIconPath = "res://Assets/Items/risk_salve.svg";
	private const string SamplePotionId = "layout_preview_potion";
	private static readonly Vector2 PreviewPadding = new(20.0f, 20.0f);
	private static readonly Vector2 PreviewMinimumSize = new(260.0f, 230.0f);

	private readonly Dictionary<InventorySlotLayoutKind, Control> _previewHosts = new();
	private InventorySlotLayoutSettings _settings = default!;
	private VBoxContainer _root = default!;
	private LineEdit _sampleNameInput = default!;
	private LineEdit _sampleIconPathInput = default!;
	private LineEdit _samplePotionIdInput = default!;
	private SpinBox _sampleQuantityInput = default!;
	private Label _statusLabel = default!;

	public override void _Ready()
	{
		if (!Engine.IsEditorHint())
			return;

		Name = "Inventory Slots";
		CustomMinimumSize = new Vector2(390.0f, 620.0f);
		_settings = InventorySlotLayoutSettings.LoadDefault(forceReload: true);
		BuildDock();
		RefreshAllPreviews();
	}

	private void BuildDock()
	{
		ClearChildren(this);
		_previewHosts.Clear();

		_root = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		_root.AddThemeConstantOverride("separation", 8);
		AddChild(_root);

		var title = new Label
		{
			Text = "Inventory Slot Layouts",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		title.AddThemeFontSizeOverride("font_size", 18);
		_root.AddChild(title);

		_root.AddChild(CreateResourcePathRow());
		_root.AddChild(CreateActionRow());
		_root.AddChild(CreateSampleSection());

		var tabs = new TabContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		_root.AddChild(tabs);

		tabs.AddChild(CreateProfilePage(InventorySlotLayoutKind.IngredientShelf, "Ingredient Shelf", true));
		tabs.AddChild(CreateProfilePage(InventorySlotLayoutKind.ConsumableShelf, "Consumable Shelf", false));
		tabs.AddChild(CreateProfilePage(InventorySlotLayoutKind.PotionInventory, "Potion Inventory", true));
		tabs.AddChild(CreateProfilePage(InventorySlotLayoutKind.CustomerPotion, "Customer Potion", false));

		_statusLabel = new Label
		{
			Text = "Layout edits auto-save. Press Refresh State in the debug panel to reload them in a running scene.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		_root.AddChild(_statusLabel);
	}

	private Control CreateResourcePathRow()
	{
		var row = new HBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		row.AddThemeConstantOverride("separation", 6);
		row.AddChild(new Label { Text = "Resource" });
		row.AddChild(new LineEdit
		{
			Text = InventorySlotLayoutSettings.DefaultResourcePath,
			Editable = false,
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		});
		return row;
	}

	private Control CreateActionRow()
	{
		var row = new HBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		row.AddThemeConstantOverride("separation", 6);

		var saveButton = new Button
		{
			Text = "Save Layout Resource",
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		saveButton.Pressed += () => SaveSettings();
		row.AddChild(saveButton);

		var reloadButton = new Button
		{
			Text = "Reload"
		};
		reloadButton.Pressed += ReloadSettings;
		row.AddChild(reloadButton);

		var resetButton = new Button
		{
			Text = "Reset All"
		};
		resetButton.Pressed += ResetAllProfiles;
		row.AddChild(resetButton);

		return row;
	}

	private Control CreateSampleSection()
	{
		var panel = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		panel.AddThemeStyleboxOverride("panel", CreateSectionStyleBox());

		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 6);
		panel.AddChild(box);
		box.AddChild(CreateSectionHeader("Preview Sample"));

		_sampleNameInput = new LineEdit
		{
			Text = "Mint (Steeped)",
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		_sampleNameInput.TextChanged += _ => RefreshAllPreviews();
		box.AddChild(CreateLabeledControl("Text", _sampleNameInput));

		_sampleIconPathInput = new LineEdit
		{
			Text = SampleIngredientIconPath,
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		_sampleIconPathInput.TextChanged += _ => RefreshAllPreviews();
		box.AddChild(CreateLabeledControl("Icon Path", _sampleIconPathInput));

		_samplePotionIdInput = new LineEdit
		{
			Text = SamplePotionId,
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		_samplePotionIdInput.TextChanged += _ => RefreshAllPreviews();
		box.AddChild(CreateLabeledControl("Potion Id", _samplePotionIdInput));

		_sampleQuantityInput = CreateSpinBox(4.0, 0.0, 999.0, 1.0);
		_sampleQuantityInput.ValueChanged += _ => RefreshAllPreviews();
		box.AddChild(CreateLabeledControl("Quantity", _sampleQuantityInput));

		return panel;
	}

	private Control CreateProfilePage(InventorySlotLayoutKind kind, string title, bool openByDefault)
	{
		var scroll = new ScrollContainer
		{
			Name = title,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};

		var box = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		box.AddThemeConstantOverride("separation", 8);
		scroll.AddChild(box);

		var profile = _settings.GetProfile(kind);
		box.AddChild(CreatePreviewSection(kind, title));
		box.AddChild(CreateProfileActions(kind));
		box.AddChild(CreateSectionHeader("Slot Frame"));
		AddVectorEditor(box, "Slot Size", profile.SlotSize, value =>
		{
			profile.SlotSize = ClampMinimum(value, 1.0f);
			CommitProfileChange(kind);
		}, 1.0, 512.0, 1.0);
		AddVectorEditor(box, "Art Offset", profile.ArtOffset, value =>
		{
			profile.ArtOffset = value;
			CommitProfileChange(kind);
		}, -256.0, 256.0, 1.0);
		AddVectorEditor(box, "Art Size", profile.ArtSize, value =>
		{
			profile.ArtSize = ClampMinimum(value, 0.0f);
			CommitProfileChange(kind);
		}, 0.0, 512.0, 1.0);

		box.AddChild(CreateSectionHeader("Icon"));
		AddFloatEditor(box, "Icon Size Ratio", profile.IconSizeRatio, 0.1, 1.5, 0.01, value =>
		{
			profile.IconSizeRatio = (float)value;
			CommitProfileChange(kind);
		});
		AddFloatEditor(box, "Icon Center Y Ratio", profile.IconCenterYRatio, 0.0, 1.0, 0.01, value =>
		{
			profile.IconCenterYRatio = (float)value;
			CommitProfileChange(kind);
		});

		box.AddChild(CreateSectionHeader("Name Text"));
		AddIntEditor(box, "Name Font Size", profile.NameFontSize, 1.0, 64.0, value =>
		{
			profile.NameFontSize = value;
			CommitProfileChange(kind);
		});
		AddIntEditor(box, "Minimum Name Font Size", profile.MinimumNameFontSize, 1.0, 64.0, value =>
		{
			profile.MinimumNameFontSize = value;
			CommitProfileChange(kind);
		});
		AddIntEditor(box, "Single Line Character Limit", profile.SingleLineCharacterLimit, 1.0, 64.0, value =>
		{
			profile.SingleLineCharacterLimit = value;
			CommitProfileChange(kind);
		});
		AddColorEditor(box, "Name Color", profile.NameColor, value =>
		{
			profile.NameColor = value;
			CommitProfileChange(kind);
		});
		AddBoolEditor(box, "Preserve Parenthetical Suffix", profile.PreserveParentheticalSuffix, value =>
		{
			profile.PreserveParentheticalSuffix = value;
			CommitProfileChange(kind);
		});
		AddBoolEditor(box, "Use Readable Name Plaque", profile.UseReadableNamePlaque, value =>
		{
			profile.UseReadableNamePlaque = value;
			CommitProfileChange(kind);
		});
		AddBoolEditor(box, "Use Generated Label Texture", profile.UseGeneratedLabelTexture, value =>
		{
			profile.UseGeneratedLabelTexture = value;
			CommitProfileChange(kind);
		});

		box.AddChild(CreateSectionHeader("Quantity Text"));
		AddIntEditor(box, "Quantity Font Size", profile.QuantityFontSize, 1.0, 64.0, value =>
		{
			profile.QuantityFontSize = value;
			CommitProfileChange(kind);
		});
		AddColorEditor(box, "Quantity Color", profile.QuantityColor, value =>
		{
			profile.QuantityColor = value;
			CommitProfileChange(kind);
		});
		AddBoolEditor(box, "Hide Quantity When One", profile.HideQuantityWhenOne, value =>
		{
			profile.HideQuantityWhenOne = value;
			CommitProfileChange(kind);
		});

		box.AddChild(CreateSectionHeader("Generated Label Ratios"));
		AddRectEditor(box, "Label Rect", profile.GeneratedLabelRectRatio, value =>
		{
			profile.GeneratedLabelRectRatio = value;
			CommitProfileChange(kind);
		});
		AddRectEditor(box, "Name Rect", profile.GeneratedNameRectRatio, value =>
		{
			profile.GeneratedNameRectRatio = value;
			CommitProfileChange(kind);
		});
		AddRectEditor(box, "Quantity Rect", profile.GeneratedQuantityRectRatio, value =>
		{
			profile.GeneratedQuantityRectRatio = value;
			CommitProfileChange(kind);
		});

		if (openByDefault)
			scroll.ScrollVertical = 0;

		return scroll;
	}

	private Control CreatePreviewSection(InventorySlotLayoutKind kind, string title)
	{
		var panel = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		panel.AddThemeStyleboxOverride("panel", CreatePreviewStyleBox());

		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 6);
		panel.AddChild(box);
		box.AddChild(CreateSectionHeader($"{title} Preview"));

		var previewHost = new Control
		{
			CustomMinimumSize = PreviewMinimumSize,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore
		};
		_previewHosts[kind] = previewHost;
		box.AddChild(previewHost);
		return panel;
	}

	private Control CreateProfileActions(InventorySlotLayoutKind kind)
	{
		var row = new HBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		row.AddThemeConstantOverride("separation", 6);

		var resetButton = new Button
		{
			Text = "Reset Profile To Defaults",
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		resetButton.Pressed += () =>
		{
			_settings.ResetProfileToDefault(kind);
			BuildDock();
			RefreshAllPreviews();
			SaveSettings("Profile reset and saved.");
		};
		row.AddChild(resetButton);
		return row;
	}

	private void AddVectorEditor(
		VBoxContainer parent,
		string label,
		Vector2 value,
		Action<Vector2> setValue,
		double minimum,
		double maximum,
		double step)
	{
		var row = CreateEditorRow(label);
		var xSpin = CreateSpinBox(value.X, minimum, maximum, step);
		var ySpin = CreateSpinBox(value.Y, minimum, maximum, step);
		xSpin.ValueChanged += _ => setValue(new Vector2((float)xSpin.Value, (float)ySpin.Value));
		ySpin.ValueChanged += _ => setValue(new Vector2((float)xSpin.Value, (float)ySpin.Value));
		row.AddChild(new Label { Text = "X" });
		row.AddChild(xSpin);
		row.AddChild(new Label { Text = "Y" });
		row.AddChild(ySpin);
		parent.AddChild(row);
	}

	private void AddRectEditor(VBoxContainer parent, string label, Rect2 value, Action<Rect2> setValue)
	{
		var row = CreateEditorRow(label);
		var xSpin = CreateSpinBox(value.Position.X, -2.0, 2.0, 0.001);
		var ySpin = CreateSpinBox(value.Position.Y, -2.0, 2.0, 0.001);
		var widthSpin = CreateSpinBox(value.Size.X, -2.0, 2.0, 0.001);
		var heightSpin = CreateSpinBox(value.Size.Y, -2.0, 2.0, 0.001);
		void Apply()
		{
			setValue(new Rect2(
				new Vector2((float)xSpin.Value, (float)ySpin.Value),
				new Vector2((float)widthSpin.Value, (float)heightSpin.Value)));
		}

		xSpin.ValueChanged += _ => Apply();
		ySpin.ValueChanged += _ => Apply();
		widthSpin.ValueChanged += _ => Apply();
		heightSpin.ValueChanged += _ => Apply();
		row.AddChild(new Label { Text = "X" });
		row.AddChild(xSpin);
		row.AddChild(new Label { Text = "Y" });
		row.AddChild(ySpin);
		row.AddChild(new Label { Text = "W" });
		row.AddChild(widthSpin);
		row.AddChild(new Label { Text = "H" });
		row.AddChild(heightSpin);
		parent.AddChild(row);
	}

	private void AddFloatEditor(
		VBoxContainer parent,
		string label,
		float value,
		double minimum,
		double maximum,
		double step,
		Action<double> setValue)
	{
		var spin = CreateSpinBox(value, minimum, maximum, step);
		spin.ValueChanged += newValue => setValue(newValue);
		parent.AddChild(CreateLabeledControl(label, spin));
	}

	private void AddIntEditor(VBoxContainer parent, string label, int value, double minimum, double maximum, Action<int> setValue)
	{
		var spin = CreateSpinBox(value, minimum, maximum, 1.0);
		spin.ValueChanged += newValue => setValue(Math.Max(1, (int)Math.Round(newValue)));
		parent.AddChild(CreateLabeledControl(label, spin));
	}

	private void AddBoolEditor(VBoxContainer parent, string label, bool value, Action<bool> setValue)
	{
		var checkbox = new CheckBox
		{
			Text = label,
			ButtonPressed = value
		};
		checkbox.Toggled += toggled => setValue(toggled);
		parent.AddChild(checkbox);
	}

	private void AddColorEditor(VBoxContainer parent, string label, Color value, Action<Color> setValue)
	{
		var picker = new ColorPickerButton
		{
			Color = value,
			CustomMinimumSize = new Vector2(120.0f, 0.0f)
		};
		picker.ColorChanged += color => setValue(color);
		parent.AddChild(CreateLabeledControl(label, picker));
	}

	private HBoxContainer CreateEditorRow(string label)
	{
		var row = new HBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		row.AddThemeConstantOverride("separation", 4);
		row.AddChild(new Label
		{
			Text = label,
			CustomMinimumSize = new Vector2(122.0f, 0.0f)
		});
		return row;
	}

	private static Control CreateLabeledControl(string label, Control control)
	{
		var row = new HBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		row.AddThemeConstantOverride("separation", 6);
		row.AddChild(new Label
		{
			Text = label,
			CustomMinimumSize = new Vector2(122.0f, 0.0f)
		});
		control.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		row.AddChild(control);
		return row;
	}

	private static Label CreateSectionHeader(string text)
	{
		var label = new Label
		{
			Text = text
		};
		label.AddThemeFontSizeOverride("font_size", 14);
		return label;
	}

	private static SpinBox CreateSpinBox(double value, double minimum, double maximum, double step)
	{
		return new SpinBox
		{
			MinValue = minimum,
			MaxValue = maximum,
			Step = step,
			Value = value,
			CustomMinimumSize = new Vector2(72.0f, 0.0f)
		};
	}

	private void RefreshAllPreviews()
	{
		foreach (var kind in _previewHosts.Keys)
			RefreshPreview(kind);
	}

	private void RefreshPreview(InventorySlotLayoutKind kind)
	{
		if (!_previewHosts.TryGetValue(kind, out var host))
			return;

		ClearChildren(host);

		var profile = _settings.GetProfile(kind);
		var slotSize = profile.ResolveSlotSize(GetFallbackSlotSize(kind));
		host.CustomMinimumSize = new Vector2(
			Mathf.Max(PreviewMinimumSize.X, slotSize.X + (PreviewPadding.X * 2.0f)),
			Mathf.Max(PreviewMinimumSize.Y, slotSize.Y + (PreviewPadding.Y * 2.0f)));

		var content = CreatePreviewContent(kind, slotSize, profile);
		content.Position = PreviewPadding;
		host.AddChild(content);
	}

	private void CommitProfileChange(InventorySlotLayoutKind kind)
	{
		_settings.GetProfile(kind).EmitChanged();
		_settings.EmitChanged();
		RefreshPreview(kind);
		SaveSettings();
	}

	private Control CreatePreviewContent(InventorySlotLayoutKind kind, Vector2 slotSize, InventorySlotLayoutProfile profile)
	{
		var sampleName = string.IsNullOrWhiteSpace(_sampleNameInput.Text)
			? "Preview Item"
			: _sampleNameInput.Text;
		var quantity = Math.Max(0, (int)Math.Round(_sampleQuantityInput.Value));

		return kind switch
		{
			InventorySlotLayoutKind.PotionInventory or InventorySlotLayoutKind.CustomerPotion =>
				JarredInventorySlotView.CreatePotionContent(
					slotSize,
					sampleName,
					string.IsNullOrWhiteSpace(_samplePotionIdInput.Text) ? SamplePotionId : _samplePotionIdInput.Text,
					quantity,
					profile.CreateJarredLayout()),
			InventorySlotLayoutKind.ConsumableShelf =>
				JarredInventorySlotView.CreateContent(
					slotSize,
					sampleName,
					string.IsNullOrWhiteSpace(_sampleIconPathInput.Text) ? SampleConsumableIconPath : _sampleIconPathInput.Text,
					quantity,
					profile.CreateJarredLayout()),
			_ =>
				JarredInventorySlotView.CreateContent(
					slotSize,
					sampleName,
					string.IsNullOrWhiteSpace(_sampleIconPathInput.Text) ? SampleIngredientIconPath : _sampleIconPathInput.Text,
					quantity,
					profile.CreateJarredLayout())
		};
	}

	private static Vector2 GetFallbackSlotSize(InventorySlotLayoutKind kind)
	{
		return kind switch
		{
			InventorySlotLayoutKind.IngredientShelf => new Vector2(116.0f, 160.0f),
			InventorySlotLayoutKind.ConsumableShelf => new Vector2(104.0f, 160.0f),
			InventorySlotLayoutKind.PotionInventory => new Vector2(112.0f, 168.0f),
			InventorySlotLayoutKind.CustomerPotion => new Vector2(94.0f, 132.0f),
			_ => new Vector2(112.0f, 168.0f)
		};
	}

	private bool SaveSettings(string? successMessage = null)
	{
		_settings.EnsureProfiles();
		var error = ResourceSaver.Save(_settings, InventorySlotLayoutSettings.DefaultResourcePath);
		SetStatus(error == Error.Ok
			? successMessage ?? $"Saved {InventorySlotLayoutSettings.DefaultResourcePath}."
			: $"Save failed with error {error}.");
		return error == Error.Ok;
	}

	private void ReloadSettings()
	{
		_settings = InventorySlotLayoutSettings.LoadDefault(forceReload: true);
		BuildDock();
		RefreshAllPreviews();
		SetStatus($"Reloaded {InventorySlotLayoutSettings.DefaultResourcePath}.");
	}

	private void ResetAllProfiles()
	{
		_settings.ResetToDefaults();
		BuildDock();
		RefreshAllPreviews();
		SaveSettings("All profiles reset and saved.");
	}

	private void SetStatus(string message)
	{
		if (_statusLabel is not null)
			_statusLabel.Text = message;
	}

	private static Vector2 ClampMinimum(Vector2 value, float minimum)
	{
		return new Vector2(Mathf.Max(minimum, value.X), Mathf.Max(minimum, value.Y));
	}

	private static StyleBoxFlat CreateSectionStyleBox()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.11f, 0.11f, 0.12f, 0.42f),
			ContentMarginLeft = 8.0f,
			ContentMarginTop = 8.0f,
			ContentMarginRight = 8.0f,
			ContentMarginBottom = 8.0f,
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomRight = 4,
			CornerRadiusBottomLeft = 4
		};
	}

	private static StyleBoxFlat CreatePreviewStyleBox()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.07f, 0.065f, 0.055f, 0.9f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			BorderColor = new Color(0.46f, 0.36f, 0.22f, 0.8f),
			ContentMarginLeft = 8.0f,
			ContentMarginTop = 8.0f,
			ContentMarginRight = 8.0f,
			ContentMarginBottom = 8.0f,
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomRight = 4,
			CornerRadiusBottomLeft = 4
		};
	}

	private static void ClearChildren(Node node)
	{
		foreach (var child in node.GetChildren())
		{
			node.RemoveChild(child);
			child.QueueFree();
		}
	}
}
