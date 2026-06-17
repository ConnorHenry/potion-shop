@tool
extends ScrollContainer

const DEFAULT_SETTINGS_PATH := "res://Assets/UI/InventorySlotLayoutSettings.tres"
const SAMPLE_INGREDIENT_ICON_PATH := "res://Assets/Items/mint.png"
const SAMPLE_CONSUMABLE_ICON_PATH := "res://Assets/Items/risk_salve.svg"
const SAMPLE_POTION_ID := "layout_preview_potion"
const JAR_OVERLAY_PATH := "res://Assets/Art/BrewingStationBright/ingredient_jar_overlay_bright.png"
const JAR_LABEL_OVERLAY_PATH := "res://Assets/Art/BrewingStationBright/ingredient_label_overlay_bright.png"
const POTION_OVERLAY_PATH := "res://Assets/Art/BrewingStationBright/potion_card_overlay_bright.png"
const PREVIEW_PADDING := Vector2(20.0, 20.0)
const PREVIEW_MINIMUM_SIZE := Vector2(260.0, 230.0)

const PROFILE_DEFS := [
	{
		"key": "IngredientShelf",
		"title": "Ingredient Shelf",
		"property": "IngredientShelfSlot",
		"fallback_size": Vector2(116.0, 160.0),
		"is_potion": false,
		"icon_path": SAMPLE_INGREDIENT_ICON_PATH,
		"default": {
			"SlotSize": Vector2(116.0, 160.0),
			"ArtOffset": Vector2(0.0, -4.0),
			"ArtSize": Vector2.ZERO,
			"IconSizeRatio": 0.62,
			"IconCenterYRatio": 0.43,
			"NameFontSize": 13,
			"MinimumNameFontSize": 9,
			"QuantityFontSize": 13,
			"NameColor": Color(0.055, 0.026, 0.012, 1.0),
			"QuantityColor": Color(0.13, 0.075, 0.032, 1.0),
			"PreserveParentheticalSuffix": true,
			"SingleLineCharacterLimit": 18,
			"HideQuantityWhenOne": false,
			"UseReadableNamePlaque": true,
			"UseGeneratedLabelTexture": true,
			"GeneratedLabelRectRatio": Rect2(Vector2(0.093945, 0.647182), Vector2(0.812109, 0.300065)),
			"GeneratedNameRectRatio": Rect2(Vector2(0.18, 0.653), Vector2(0.64, 0.16)),
			"GeneratedQuantityRectRatio": Rect2(Vector2.ZERO, Vector2.ZERO)
		}
	},
	{
		"key": "ConsumableShelf",
		"title": "Consumable Shelf",
		"property": "ConsumableShelfSlot",
		"fallback_size": Vector2(104.0, 160.0),
		"is_potion": false,
		"icon_path": SAMPLE_CONSUMABLE_ICON_PATH,
		"default": {
			"SlotSize": Vector2(104.0, 160.0),
			"ArtOffset": Vector2.ZERO,
			"ArtSize": Vector2.ZERO,
			"IconSizeRatio": 0.58,
			"IconCenterYRatio": 0.43,
			"NameFontSize": 14,
			"MinimumNameFontSize": 12,
			"QuantityFontSize": 14,
			"NameColor": Color(0.055, 0.026, 0.012, 1.0),
			"QuantityColor": Color(0.13, 0.075, 0.032, 1.0),
			"PreserveParentheticalSuffix": false,
			"SingleLineCharacterLimit": 12,
			"HideQuantityWhenOne": false,
			"UseReadableNamePlaque": false,
			"UseGeneratedLabelTexture": true,
			"GeneratedLabelRectRatio": Rect2(Vector2.ZERO, Vector2.ZERO),
			"GeneratedNameRectRatio": Rect2(Vector2.ZERO, Vector2.ZERO),
			"GeneratedQuantityRectRatio": Rect2(Vector2.ZERO, Vector2.ZERO)
		}
	},
	{
		"key": "PotionInventory",
		"title": "Potion Inventory",
		"property": "PotionInventorySlot",
		"fallback_size": Vector2(112.0, 168.0),
		"is_potion": true,
		"icon_path": SAMPLE_INGREDIENT_ICON_PATH,
		"default": {
			"SlotSize": Vector2(112.0, 168.0),
			"ArtOffset": Vector2.ZERO,
			"ArtSize": Vector2.ZERO,
			"IconSizeRatio": 0.58,
			"IconCenterYRatio": 0.43,
			"NameFontSize": 12,
			"MinimumNameFontSize": 9,
			"QuantityFontSize": 16,
			"NameColor": Color(0.13, 0.075, 0.032, 1.0),
			"QuantityColor": Color(0.13, 0.075, 0.032, 1.0),
			"PreserveParentheticalSuffix": false,
			"SingleLineCharacterLimit": 10,
			"HideQuantityWhenOne": false,
			"UseReadableNamePlaque": true,
			"UseGeneratedLabelTexture": true,
			"GeneratedLabelRectRatio": Rect2(Vector2(0.03, 0.634), Vector2(0.94, 0.34)),
			"GeneratedNameRectRatio": Rect2(Vector2(0.08, 0.657), Vector2(0.84, 0.20)),
			"GeneratedQuantityRectRatio": Rect2(Vector2(0.36, 0.858), Vector2(0.28, 0.17))
		}
	},
	{
		"key": "CustomerPotion",
		"title": "Customer Potion",
		"property": "CustomerPotionSlot",
		"fallback_size": Vector2(94.0, 132.0),
		"is_potion": true,
		"icon_path": SAMPLE_INGREDIENT_ICON_PATH,
		"default": {
			"SlotSize": Vector2(94.0, 132.0),
			"ArtOffset": Vector2.ZERO,
			"ArtSize": Vector2.ZERO,
			"IconSizeRatio": 0.54,
			"IconCenterYRatio": 0.43,
			"NameFontSize": 8,
			"MinimumNameFontSize": 9,
			"QuantityFontSize": 10,
			"NameColor": Color(0.055, 0.026, 0.012, 1.0),
			"QuantityColor": Color(0.13, 0.075, 0.032, 1.0),
			"PreserveParentheticalSuffix": false,
			"SingleLineCharacterLimit": 12,
			"HideQuantityWhenOne": false,
			"UseReadableNamePlaque": false,
			"UseGeneratedLabelTexture": false,
			"GeneratedLabelRectRatio": Rect2(Vector2.ZERO, Vector2.ZERO),
			"GeneratedNameRectRatio": Rect2(Vector2.ZERO, Vector2.ZERO),
			"GeneratedQuantityRectRatio": Rect2(Vector2.ZERO, Vector2.ZERO)
		}
	}
]

var _settings: Resource
var _root: VBoxContainer
var _status_label: Label
var _sample_name_input: LineEdit
var _sample_icon_path_input: LineEdit
var _sample_potion_id_input: LineEdit
var _sample_quantity_input: SpinBox
var _preview_hosts := {}


func _ready() -> void:
	name = "Inventory Slots"
	custom_minimum_size = Vector2(390.0, 620.0)
	_settings = _load_settings()
	_build_dock()
	_refresh_all_previews()


func _build_dock() -> void:
	_clear_children(self)
	_preview_hosts.clear()

	_root = VBoxContainer.new()
	_root.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_root.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_root.add_theme_constant_override("separation", 8)
	add_child(_root)

	var title := Label.new()
	title.text = "Inventory Slot Layouts"
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	title.add_theme_font_size_override("font_size", 18)
	_root.add_child(title)

	_root.add_child(_create_resource_path_row())
	_root.add_child(_create_action_row())
	_root.add_child(_create_sample_section())

	var tabs := TabContainer.new()
	tabs.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	tabs.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_root.add_child(tabs)

	for def in PROFILE_DEFS:
		tabs.add_child(_create_profile_page(def))

	_status_label = Label.new()
	_status_label.text = "Layout edits auto-save. Press Refresh State in the debug panel to reload them in a running scene."
	_status_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_root.add_child(_status_label)


func _create_resource_path_row() -> Control:
	var row := HBoxContainer.new()
	row.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	row.add_theme_constant_override("separation", 6)
	row.add_child(_make_label("Resource"))

	var path := LineEdit.new()
	path.text = DEFAULT_SETTINGS_PATH
	path.editable = false
	path.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	row.add_child(path)
	return row


func _create_action_row() -> Control:
	var row := HBoxContainer.new()
	row.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	row.add_theme_constant_override("separation", 6)

	var save_button := Button.new()
	save_button.text = "Save Layout Resource"
	save_button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	save_button.pressed.connect(func(): _save_settings())
	row.add_child(save_button)

	var reload_button := Button.new()
	reload_button.text = "Reload"
	reload_button.pressed.connect(_reload_settings)
	row.add_child(reload_button)

	var reset_button := Button.new()
	reset_button.text = "Reset All"
	reset_button.pressed.connect(_reset_all_profiles)
	row.add_child(reset_button)
	return row


func _create_sample_section() -> Control:
	var panel := PanelContainer.new()
	panel.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	panel.add_theme_stylebox_override("panel", _create_section_stylebox())

	var box := VBoxContainer.new()
	box.add_theme_constant_override("separation", 6)
	panel.add_child(box)
	box.add_child(_create_section_header("Preview Sample"))

	_sample_name_input = LineEdit.new()
	_sample_name_input.text = "Mint (Steeped)"
	_sample_name_input.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_sample_name_input.text_changed.connect(func(_text: String): _refresh_all_previews())
	box.add_child(_create_labeled_control("Text", _sample_name_input))

	_sample_icon_path_input = LineEdit.new()
	_sample_icon_path_input.text = SAMPLE_INGREDIENT_ICON_PATH
	_sample_icon_path_input.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_sample_icon_path_input.text_changed.connect(func(_text: String): _refresh_all_previews())
	box.add_child(_create_labeled_control("Icon Path", _sample_icon_path_input))

	_sample_potion_id_input = LineEdit.new()
	_sample_potion_id_input.text = SAMPLE_POTION_ID
	_sample_potion_id_input.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_sample_potion_id_input.text_changed.connect(func(_text: String): _refresh_all_previews())
	box.add_child(_create_labeled_control("Potion Id", _sample_potion_id_input))

	_sample_quantity_input = _create_spin_box(4.0, 0.0, 999.0, 1.0)
	_sample_quantity_input.value_changed.connect(func(_value: float): _refresh_all_previews())
	box.add_child(_create_labeled_control("Quantity", _sample_quantity_input))
	return panel


func _create_profile_page(def: Dictionary) -> Control:
	var scroll := ScrollContainer.new()
	scroll.name = def["title"]
	scroll.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL

	var box := VBoxContainer.new()
	box.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	box.add_theme_constant_override("separation", 8)
	scroll.add_child(box)

	var profile := _get_profile(def)
	box.add_child(_create_preview_section(def))
	box.add_child(_create_profile_actions(def))
	box.add_child(_create_section_header("Slot Frame"))
	_add_vector_editor(box, "Slot Size", _get_vector(profile, "SlotSize", def["fallback_size"]), 1.0, 512.0, 1.0, func(value: Vector2):
		_set_profile_value(def, "SlotSize", Vector2(maxf(1.0, value.x), maxf(1.0, value.y)))
	)
	_add_vector_editor(box, "Art Offset", _get_vector(profile, "ArtOffset", Vector2.ZERO), -256.0, 256.0, 1.0, func(value: Vector2):
		_set_profile_value(def, "ArtOffset", value)
	)
	_add_vector_editor(box, "Art Size", _get_vector(profile, "ArtSize", Vector2.ZERO), 0.0, 512.0, 1.0, func(value: Vector2):
		_set_profile_value(def, "ArtSize", Vector2(maxf(0.0, value.x), maxf(0.0, value.y)))
	)

	box.add_child(_create_section_header("Icon"))
	_add_float_editor(box, "Icon Size Ratio", _get_float(profile, "IconSizeRatio", 0.58), 0.1, 1.5, 0.01, func(value: float):
		_set_profile_value(def, "IconSizeRatio", value)
	)
	_add_float_editor(box, "Icon Center Y Ratio", _get_float(profile, "IconCenterYRatio", 0.43), 0.0, 1.0, 0.01, func(value: float):
		_set_profile_value(def, "IconCenterYRatio", value)
	)

	box.add_child(_create_section_header("Name Text"))
	_add_int_editor(box, "Name Font Size", _get_int(profile, "NameFontSize", 10), 1.0, 64.0, func(value: int):
		_set_profile_value(def, "NameFontSize", value)
	)
	_add_int_editor(box, "Minimum Name Font Size", _get_int(profile, "MinimumNameFontSize", 9), 1.0, 64.0, func(value: int):
		_set_profile_value(def, "MinimumNameFontSize", value)
	)
	_add_int_editor(box, "Single Line Character Limit", _get_int(profile, "SingleLineCharacterLimit", 12), 1.0, 64.0, func(value: int):
		_set_profile_value(def, "SingleLineCharacterLimit", value)
	)
	_add_color_editor(box, "Name Color", _get_color(profile, "NameColor", Color(0.055, 0.026, 0.012, 1.0)), func(value: Color):
		_set_profile_value(def, "NameColor", value)
	)
	_add_bool_editor(box, "Preserve Parenthetical Suffix", _get_bool(profile, "PreserveParentheticalSuffix", false), func(value: bool):
		_set_profile_value(def, "PreserveParentheticalSuffix", value)
	)
	_add_bool_editor(box, "Use Readable Name Plaque", _get_bool(profile, "UseReadableNamePlaque", false), func(value: bool):
		_set_profile_value(def, "UseReadableNamePlaque", value)
	)
	_add_bool_editor(box, "Use Generated Label Texture", _get_bool(profile, "UseGeneratedLabelTexture", false), func(value: bool):
		_set_profile_value(def, "UseGeneratedLabelTexture", value)
	)

	box.add_child(_create_section_header("Quantity Text"))
	_add_int_editor(box, "Quantity Font Size", _get_int(profile, "QuantityFontSize", 11), 1.0, 64.0, func(value: int):
		_set_profile_value(def, "QuantityFontSize", value)
	)
	_add_color_editor(box, "Quantity Color", _get_color(profile, "QuantityColor", Color(0.13, 0.075, 0.032, 1.0)), func(value: Color):
		_set_profile_value(def, "QuantityColor", value)
	)
	_add_bool_editor(box, "Hide Quantity When One", _get_bool(profile, "HideQuantityWhenOne", false), func(value: bool):
		_set_profile_value(def, "HideQuantityWhenOne", value)
	)

	box.add_child(_create_section_header("Generated Label Ratios"))
	_add_rect_editor(box, "Label Rect", _get_rect(profile, "GeneratedLabelRectRatio", Rect2(Vector2.ZERO, Vector2.ZERO)), func(value: Rect2):
		_set_profile_value(def, "GeneratedLabelRectRatio", value)
	)
	_add_rect_editor(box, "Name Rect", _get_rect(profile, "GeneratedNameRectRatio", Rect2(Vector2.ZERO, Vector2.ZERO)), func(value: Rect2):
		_set_profile_value(def, "GeneratedNameRectRatio", value)
	)
	_add_rect_editor(box, "Quantity Rect", _get_rect(profile, "GeneratedQuantityRectRatio", Rect2(Vector2.ZERO, Vector2.ZERO)), func(value: Rect2):
		_set_profile_value(def, "GeneratedQuantityRectRatio", value)
	)
	return scroll


func _create_preview_section(def: Dictionary) -> Control:
	var panel := PanelContainer.new()
	panel.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	panel.add_theme_stylebox_override("panel", _create_preview_stylebox())

	var box := VBoxContainer.new()
	box.add_theme_constant_override("separation", 6)
	panel.add_child(box)
	box.add_child(_create_section_header("%s Preview" % def["title"]))

	var preview_host := Control.new()
	preview_host.custom_minimum_size = PREVIEW_MINIMUM_SIZE
	preview_host.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	preview_host.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_preview_hosts[def["key"]] = preview_host
	box.add_child(preview_host)
	return panel


func _create_profile_actions(def: Dictionary) -> Control:
	var row := HBoxContainer.new()
	row.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	row.add_theme_constant_override("separation", 6)

	var reset_button := Button.new()
	reset_button.text = "Reset Profile To Defaults"
	reset_button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	reset_button.pressed.connect(func():
		_reset_profile(def)
		_build_dock()
		_refresh_all_previews()
		_save_settings("Profile reset and saved.")
	)
	row.add_child(reset_button)
	return row


func _add_vector_editor(parent: VBoxContainer, label: String, value: Vector2, minimum: float, maximum: float, step: float, set_value: Callable) -> void:
	var row := _create_editor_row(label)
	var x_spin := _create_spin_box(value.x, minimum, maximum, step)
	var y_spin := _create_spin_box(value.y, minimum, maximum, step)
	var apply := func(_value: float):
		set_value.call(Vector2(x_spin.value, y_spin.value))
	x_spin.value_changed.connect(apply)
	y_spin.value_changed.connect(apply)
	row.add_child(_make_label("X"))
	row.add_child(x_spin)
	row.add_child(_make_label("Y"))
	row.add_child(y_spin)
	parent.add_child(row)


func _add_rect_editor(parent: VBoxContainer, label: String, value: Rect2, set_value: Callable) -> void:
	var row := _create_editor_row(label)
	var x_spin := _create_spin_box(value.position.x, -2.0, 2.0, 0.001)
	var y_spin := _create_spin_box(value.position.y, -2.0, 2.0, 0.001)
	var width_spin := _create_spin_box(value.size.x, -2.0, 2.0, 0.001)
	var height_spin := _create_spin_box(value.size.y, -2.0, 2.0, 0.001)
	var apply := func(_value: float):
		set_value.call(Rect2(Vector2(x_spin.value, y_spin.value), Vector2(width_spin.value, height_spin.value)))
	x_spin.value_changed.connect(apply)
	y_spin.value_changed.connect(apply)
	width_spin.value_changed.connect(apply)
	height_spin.value_changed.connect(apply)
	row.add_child(_make_label("X"))
	row.add_child(x_spin)
	row.add_child(_make_label("Y"))
	row.add_child(y_spin)
	row.add_child(_make_label("W"))
	row.add_child(width_spin)
	row.add_child(_make_label("H"))
	row.add_child(height_spin)
	parent.add_child(row)


func _add_float_editor(parent: VBoxContainer, label: String, value: float, minimum: float, maximum: float, step: float, set_value: Callable) -> void:
	var spin := _create_spin_box(value, minimum, maximum, step)
	spin.value_changed.connect(func(new_value: float): set_value.call(new_value))
	parent.add_child(_create_labeled_control(label, spin))


func _add_int_editor(parent: VBoxContainer, label: String, value: int, minimum: float, maximum: float, set_value: Callable) -> void:
	var spin := _create_spin_box(value, minimum, maximum, 1.0)
	spin.value_changed.connect(func(new_value: float): set_value.call(maxi(1, int(round(new_value)))))
	parent.add_child(_create_labeled_control(label, spin))


func _add_bool_editor(parent: VBoxContainer, label: String, value: bool, set_value: Callable) -> void:
	var checkbox := CheckBox.new()
	checkbox.text = label
	checkbox.button_pressed = value
	checkbox.toggled.connect(func(toggled: bool): set_value.call(toggled))
	parent.add_child(checkbox)


func _add_color_editor(parent: VBoxContainer, label: String, value: Color, set_value: Callable) -> void:
	var picker := ColorPickerButton.new()
	picker.color = value
	picker.custom_minimum_size = Vector2(120.0, 0.0)
	picker.color_changed.connect(func(color: Color): set_value.call(color))
	parent.add_child(_create_labeled_control(label, picker))


func _set_profile_value(def: Dictionary, property_name: String, value: Variant) -> void:
	var profile := _get_profile(def)
	if profile == null:
		_set_status("Cannot edit %s because its profile resource is missing." % def["title"])
		return

	profile.set(property_name, value)
	profile.emit_changed()
	if _settings != null:
		_settings.emit_changed()
	_refresh_preview(def)
	_save_settings()


func _refresh_all_previews() -> void:
	for def in PROFILE_DEFS:
		_refresh_preview(def)


func _refresh_preview(def: Dictionary) -> void:
	var host: Control = _preview_hosts.get(def["key"])
	if host == null:
		return

	_clear_children(host)
	var profile := _get_profile(def)
	if profile == null:
		host.add_child(_make_label("Profile resource missing. Reload or recreate %s." % DEFAULT_SETTINGS_PATH))
		return

	var slot_size := _resolve_slot_size(profile, def["fallback_size"])
	host.custom_minimum_size = Vector2(
		maxf(PREVIEW_MINIMUM_SIZE.x, slot_size.x + (PREVIEW_PADDING.x * 2.0)),
		maxf(PREVIEW_MINIMUM_SIZE.y, slot_size.y + (PREVIEW_PADDING.y * 2.0))
	)

	var slot := PanelContainer.new()
	slot.position = PREVIEW_PADDING
	slot.custom_minimum_size = slot_size
	slot.size = slot_size
	slot.mouse_filter = Control.MOUSE_FILTER_IGNORE
	slot.add_theme_stylebox_override("panel", _create_slot_stylebox(Color(0.08, 0.055, 0.035, 0.08), Color(0.36, 0.24, 0.13, 0.16), 6))
	host.add_child(slot)

	var content := Control.new()
	content.custom_minimum_size = slot_size
	content.size = slot_size
	content.mouse_filter = Control.MOUSE_FILTER_IGNORE
	slot.add_child(content)

	_add_preview_content(content, def, profile, slot_size)


func _add_preview_content(parent: Control, def: Dictionary, profile: Resource, slot_size: Vector2) -> void:
	var art_size := _get_vector(profile, "ArtSize", Vector2.ZERO)
	if art_size == Vector2.ZERO:
		art_size = slot_size
	var art_offset := _get_vector(profile, "ArtOffset", Vector2.ZERO)
	var art_position := Vector2(((slot_size.x - art_size.x) * 0.5) + art_offset.x, art_offset.y)
	var sample_name := _sample_name_input.text.strip_edges()
	if sample_name.is_empty():
		sample_name = "Preview Item"
	var quantity := maxi(0, int(round(_sample_quantity_input.value)))

	if def["is_potion"]:
		parent.add_child(_create_liquid_preview(art_position, art_size))
		parent.add_child(_create_texture_rect("PotionBottleOverlay", POTION_OVERLAY_PATH, art_position, art_size, 0))
	else:
		var icon_path := _sample_icon_path_input.text.strip_edges()
		if icon_path.is_empty():
			icon_path = def["icon_path"]
		parent.add_child(_create_icon_preview(icon_path, art_position, art_size, profile))
		parent.add_child(_create_texture_rect("JarOverlay", JAR_OVERLAY_PATH, art_position, art_size, 0))

	if _get_bool(profile, "UseGeneratedLabelTexture", false):
		var label_rect := _resolve_generated_label_rect(art_position, art_size, profile)
		parent.add_child(_create_texture_rect("JarLabelOverlay", JAR_LABEL_OVERLAY_PATH, label_rect.position, label_rect.size, 0))

	parent.add_child(_create_name_block(sample_name, art_position, art_size, profile))
	if quantity != 1 or not _get_bool(profile, "HideQuantityWhenOne", false):
		parent.add_child(_create_centered_label("x%d" % quantity, _resolve_quantity_rect(art_position, art_size, profile), _get_int(profile, "QuantityFontSize", 11), _get_color(profile, "QuantityColor", Color(0.13, 0.075, 0.032, 1.0))))


func _create_icon_preview(icon_path: String, art_position: Vector2, art_size: Vector2, profile: Resource) -> Control:
	var icon_size := clampf(art_size.x * _get_float(profile, "IconSizeRatio", 0.58), 32.0, art_size.x * 0.72)
	var icon_top := art_position.y + (art_size.y * _get_float(profile, "IconCenterYRatio", 0.43)) - (icon_size * 0.5)
	return _create_texture_rect("Icon", icon_path, Vector2(art_position.x + ((art_size.x - icon_size) * 0.5), icon_top), Vector2(icon_size, icon_size), 5)


func _create_liquid_preview(position: Vector2, size: Vector2) -> Control:
	var liquid := ColorRect.new()
	liquid.name = "PotionLiquidPreview"
	liquid.position = position + Vector2(size.x * 0.30, size.y * 0.38)
	liquid.custom_minimum_size = Vector2(size.x * 0.40, size.y * 0.34)
	liquid.size = liquid.custom_minimum_size
	liquid.color = Color(0.58, 0.11, 0.64, 0.72)
	liquid.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return liquid


func _create_texture_rect(node_name: String, path: String, position: Vector2, size: Vector2, stretch_mode: int) -> TextureRect:
	var texture_rect := TextureRect.new()
	texture_rect.name = node_name
	texture_rect.position = position
	texture_rect.custom_minimum_size = size
	texture_rect.size = size
	texture_rect.texture = load(path)
	texture_rect.expand_mode = 1
	texture_rect.stretch_mode = stretch_mode
	texture_rect.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return texture_rect


func _create_name_block(item_name: String, art_position: Vector2, art_size: Vector2, profile: Resource) -> Control:
	var name_rect := _resolve_name_rect(art_position, art_size, profile)
	var block := Control.new()
	block.name = "NameBlock"
	block.position = name_rect.position
	block.custom_minimum_size = name_rect.size
	block.size = name_rect.size
	block.mouse_filter = Control.MOUSE_FILTER_IGNORE

	var lines := _build_name_lines(item_name, profile)
	var line_spacing := art_size.y * 0.006
	var font_size := _get_int(profile, "NameFontSize", 10)
	var line_height := minf(font_size * 1.12, name_rect.size.y / maxf(1.0, float(lines.size())))
	var total_height := (line_height * lines.size()) + (line_spacing * maxf(0.0, float(lines.size() - 1)))
	var top_offset := maxf(0.0, (name_rect.size.y - total_height) * 0.5)
	for index in range(lines.size()):
		var line_rect := Rect2(Vector2(0.0, top_offset + (index * (line_height + line_spacing))), Vector2(name_rect.size.x, line_height))
		block.add_child(_create_centered_label(lines[index], line_rect, font_size, _get_color(profile, "NameColor", Color(0.055, 0.026, 0.012, 1.0))))
	return block


func _create_centered_label(text: String, rect: Rect2, font_size: int, color: Color) -> Label:
	var label := Label.new()
	label.text = text
	label.position = rect.position
	label.custom_minimum_size = rect.size
	label.size = rect.size
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	label.clip_text = false
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	label.add_theme_font_size_override("font_size", font_size)
	label.add_theme_color_override("font_color", color)
	return label


func _build_name_lines(item_name: String, profile: Resource) -> Array:
	var line_limit := maxi(1, _get_int(profile, "SingleLineCharacterLimit", 12))
	if item_name.length() <= line_limit:
		return [item_name]

	var split_index := item_name.rfind(" ", line_limit)
	if split_index <= 0:
		split_index = line_limit
	return [
		item_name.substr(0, split_index).strip_edges(),
		item_name.substr(split_index).strip_edges()
	]


func _resolve_generated_label_rect(art_position: Vector2, art_size: Vector2, profile: Resource) -> Rect2:
	var default_rect := Rect2(Vector2(0.130859375, 0.66861979), Vector2(0.73828125, 0.27278647))
	return _scale_ratio_rect(art_position, art_size, _resolve_custom_ratio_rect(_get_rect(profile, "GeneratedLabelRectRatio", Rect2(Vector2.ZERO, Vector2.ZERO)), default_rect))


func _resolve_name_rect(art_position: Vector2, art_size: Vector2, profile: Resource) -> Rect2:
	if _get_bool(profile, "UseGeneratedLabelTexture", false):
		var generated_default := Rect2(Vector2(0.18, 0.681), Vector2(0.64, 0.16))
		return _scale_ratio_rect(art_position, art_size, _resolve_custom_ratio_rect(_get_rect(profile, "GeneratedNameRectRatio", Rect2(Vector2.ZERO, Vector2.ZERO)), generated_default))
	if _get_bool(profile, "UseReadableNamePlaque", false):
		return _scale_ratio_rect(art_position, art_size, Rect2(Vector2(0.052, 0.655), Vector2(0.896, 0.215)))
	return _scale_ratio_rect(art_position, art_size, Rect2(Vector2(0.065, 0.652), Vector2(0.87, 0.20)))


func _resolve_quantity_rect(art_position: Vector2, art_size: Vector2, profile: Resource) -> Rect2:
	if _get_bool(profile, "UseGeneratedLabelTexture", false):
		var generated_default := Rect2(Vector2(0.31, 0.803), Vector2(0.38, 0.16))
		return _scale_ratio_rect(art_position, art_size, _resolve_custom_ratio_rect(_get_rect(profile, "GeneratedQuantityRectRatio", Rect2(Vector2.ZERO, Vector2.ZERO)), generated_default))
	if _get_bool(profile, "UseReadableNamePlaque", false):
		return _scale_ratio_rect(art_position, art_size, Rect2(Vector2(0.25, 0.824), Vector2(0.50, 0.12)))
	return _scale_ratio_rect(art_position, art_size, Rect2(Vector2(0.34, 0.852), Vector2(0.32, 0.105)))


func _resolve_custom_ratio_rect(custom_rect: Rect2, default_rect: Rect2) -> Rect2:
	if custom_rect.position == Vector2.ZERO and custom_rect.size == Vector2.ZERO:
		return default_rect

	return Rect2(
		custom_rect.position,
		Vector2(
			custom_rect.size.x if custom_rect.size.x > 0.0 else default_rect.size.x,
			custom_rect.size.y if custom_rect.size.y > 0.0 else default_rect.size.y
		)
	)


func _scale_ratio_rect(origin: Vector2, size: Vector2, ratio_rect: Rect2) -> Rect2:
	return Rect2(
		origin + Vector2(size.x * ratio_rect.position.x, size.y * ratio_rect.position.y),
		Vector2(size.x * ratio_rect.size.x, size.y * ratio_rect.size.y)
	)


func _load_settings() -> Resource:
	if not ResourceLoader.exists(DEFAULT_SETTINGS_PATH):
		_set_deferred_status("Missing %s." % DEFAULT_SETTINGS_PATH)
		return null

	var loaded := ResourceLoader.load(DEFAULT_SETTINGS_PATH, "", ResourceLoader.CACHE_MODE_IGNORE)
	if loaded == null:
		_set_deferred_status("Could not load %s." % DEFAULT_SETTINGS_PATH)
		return null

	return loaded


func _reload_settings() -> void:
	_settings = _load_settings()
	_build_dock()
	_refresh_all_previews()
	_set_status("Reloaded %s." % DEFAULT_SETTINGS_PATH)


func _save_settings(success_message := "") -> bool:
	if _settings == null:
		_set_status("Cannot save because the layout settings resource is missing.")
		return false

	var error := ResourceSaver.save(_settings, DEFAULT_SETTINGS_PATH)
	if error == OK:
		_set_status(success_message if not success_message.is_empty() else "Saved %s." % DEFAULT_SETTINGS_PATH)
		return true

	_set_status("Save failed with error %s." % error)
	return false


func _reset_all_profiles() -> void:
	for def in PROFILE_DEFS:
		_reset_profile(def)
	_build_dock()
	_refresh_all_previews()
	_save_settings("All profiles reset and saved.")


func _reset_profile(def: Dictionary) -> void:
	var profile := _get_profile(def)
	if profile == null:
		return

	for property_name in def["default"].keys():
		profile.set(property_name, def["default"][property_name])
	profile.emit_changed()
	if _settings != null:
		_settings.emit_changed()


func _get_profile(def: Dictionary) -> Resource:
	if _settings == null:
		return null
	return _settings.get(def["property"])


func _resolve_slot_size(profile: Resource, fallback: Vector2) -> Vector2:
	var slot_size := _get_vector(profile, "SlotSize", fallback)
	if slot_size.x > 0.0 and slot_size.y > 0.0:
		return slot_size
	return fallback


func _get_vector(profile: Resource, property_name: String, fallback: Vector2) -> Vector2:
	var value = profile.get(property_name) if profile != null else null
	return value if value is Vector2 else fallback


func _get_rect(profile: Resource, property_name: String, fallback: Rect2) -> Rect2:
	var value = profile.get(property_name) if profile != null else null
	return value if value is Rect2 else fallback


func _get_color(profile: Resource, property_name: String, fallback: Color) -> Color:
	var value = profile.get(property_name) if profile != null else null
	return value if value is Color else fallback


func _get_float(profile: Resource, property_name: String, fallback: float) -> float:
	var value = profile.get(property_name) if profile != null else null
	return float(value) if value != null else fallback


func _get_int(profile: Resource, property_name: String, fallback: int) -> int:
	var value = profile.get(property_name) if profile != null else null
	return int(value) if value != null else fallback


func _get_bool(profile: Resource, property_name: String, fallback: bool) -> bool:
	var value = profile.get(property_name) if profile != null else null
	return bool(value) if value != null else fallback


func _create_editor_row(label: String) -> HBoxContainer:
	var row := HBoxContainer.new()
	row.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	row.add_theme_constant_override("separation", 4)
	var label_control := _make_label(label)
	label_control.custom_minimum_size = Vector2(122.0, 0.0)
	row.add_child(label_control)
	return row


func _create_labeled_control(label: String, control: Control) -> Control:
	var row := HBoxContainer.new()
	row.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	row.add_theme_constant_override("separation", 6)
	var label_control := _make_label(label)
	label_control.custom_minimum_size = Vector2(122.0, 0.0)
	row.add_child(label_control)
	control.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	row.add_child(control)
	return row


func _create_section_header(text: String) -> Label:
	var label := _make_label(text)
	label.add_theme_font_size_override("font_size", 14)
	return label


func _make_label(text: String) -> Label:
	var label := Label.new()
	label.text = text
	return label


func _create_spin_box(value: float, minimum: float, maximum: float, step: float) -> SpinBox:
	var spin := SpinBox.new()
	spin.min_value = minimum
	spin.max_value = maximum
	spin.step = step
	spin.value = value
	spin.custom_minimum_size = Vector2(72.0, 0.0)
	return spin


func _create_section_stylebox() -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.11, 0.11, 0.12, 0.42)
	style.content_margin_left = 8.0
	style.content_margin_top = 8.0
	style.content_margin_right = 8.0
	style.content_margin_bottom = 8.0
	style.corner_radius_top_left = 4
	style.corner_radius_top_right = 4
	style.corner_radius_bottom_right = 4
	style.corner_radius_bottom_left = 4
	return style


func _create_preview_stylebox() -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.07, 0.065, 0.055, 0.9)
	style.border_width_left = 1
	style.border_width_top = 1
	style.border_width_right = 1
	style.border_width_bottom = 1
	style.border_color = Color(0.46, 0.36, 0.22, 0.8)
	style.content_margin_left = 8.0
	style.content_margin_top = 8.0
	style.content_margin_right = 8.0
	style.content_margin_bottom = 8.0
	style.corner_radius_top_left = 4
	style.corner_radius_top_right = 4
	style.corner_radius_bottom_right = 4
	style.corner_radius_bottom_left = 4
	return style


func _create_slot_stylebox(fill_color: Color, border_color: Color, corner_radius: int) -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = fill_color
	style.border_width_left = 1
	style.border_width_top = 1
	style.border_width_right = 1
	style.border_width_bottom = 1
	style.border_color = border_color
	style.corner_radius_top_left = corner_radius
	style.corner_radius_top_right = corner_radius
	style.corner_radius_bottom_right = corner_radius
	style.corner_radius_bottom_left = corner_radius
	return style


func _clear_children(node: Node) -> void:
	for child in node.get_children():
		node.remove_child(child)
		child.queue_free()


func _set_deferred_status(message: String) -> void:
	call_deferred("_set_status", message)


func _set_status(message: String) -> void:
	if _status_label != null:
		_status_label.text = message
