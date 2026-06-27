@tool
extends Control

const AUTHORED_DATA_PATH := "res://Data/authored_data.tres"
const MAX_DIALOGUE_OPTIONS_PER_NODE := 8
const VALID_ID_PATTERN := "^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$"
const QUEST_STATUSES := ["NotStarted", "InProgress", "Complete", "Failed"]
const GRAPH_READABLE_ZOOM := 1.5
const GRAPH_MIN_VISIBLE_HEIGHT := 420.0

var _authored_resource: Resource
var _customer_resource: Resource
var _customer_path := ""
var _entries: Array = []
var _selected_index := -1
var _selected_node_id := ""
var _selected_option_index := -1
var _dirty := false
var _id_regex := RegEx.new()

var _root: VBoxContainer
var _interaction_list: ItemList
var _graph: GraphEdit
var _selected_summary_label: Label
var _inspector: VBoxContainer
var _status_label: Label
var _validation_label: Label

var _graph_name_by_node_id := {}
var _node_id_by_graph_name := {}
var _option_index_by_slot := {}

var _preview_reputation := 50
var _preview_flags_text := ""
var _preview_relationships_text := ""
var _preview_quests_text := ""
var _preview_seen_options_text := ""
var _preview_flags := {}
var _preview_relationships := {}
var _preview_quests := {}
var _preview_seen_options := {}


func _ready() -> void:
	name = "Dialogue Trees"
	custom_minimum_size = Vector2(360.0, 280.0)
	_id_regex.compile(VALID_ID_PATTERN)
	_build_ui()
	_load_resources()


func _build_ui() -> void:
	_clear_children(self)

	_root = VBoxContainer.new()
	_root.anchor_right = 1.0
	_root.anchor_bottom = 1.0
	_root.offset_left = 0.0
	_root.offset_top = 0.0
	_root.offset_right = 0.0
	_root.offset_bottom = 0.0
	_root.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_root.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_root.add_theme_constant_override("separation", 6)
	add_child(_root)

	var header := HBoxContainer.new()
	header.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	header.add_theme_constant_override("separation", 6)
	_root.add_child(header)

	var title := Label.new()
	title.text = "Dialogue Trees"
	title.add_theme_font_size_override("font_size", 16)
	title.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	header.add_child(title)

	header.add_child(_make_button("Reload", _load_resources))
	header.add_child(_make_button("Validate", func(): _validate_and_show()))
	header.add_child(_make_button("Save", _save_resource))

	var tabs := TabContainer.new()
	tabs.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	tabs.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_root.add_child(tabs)

	var interactions_panel := _build_left_panel()
	interactions_panel.name = "Interactions"
	tabs.add_child(interactions_panel)

	var graph_panel := _build_center_panel()
	graph_panel.name = "Graph"
	tabs.add_child(graph_panel)

	var inspector_panel := _build_right_panel()
	inspector_panel.name = "Inspector"
	tabs.add_child(inspector_panel)

	_status_label = Label.new()
	_status_label.text = "Load an authored customer resource to begin."
	_status_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_root.add_child(_status_label)


func _build_left_panel() -> Control:
	var panel := VBoxContainer.new()
	panel.custom_minimum_size = Vector2(0.0, 0.0)
	panel.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	panel.size_flags_vertical = Control.SIZE_EXPAND_FILL
	panel.add_theme_constant_override("separation", 6)

	var path_label := Label.new()
	path_label.text = AUTHORED_DATA_PATH
	path_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	panel.add_child(path_label)

	_interaction_list = ItemList.new()
	_interaction_list.custom_minimum_size = Vector2(0.0, 120.0)
	_interaction_list.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_interaction_list.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_interaction_list.item_selected.connect(_on_interaction_selected)
	panel.add_child(_interaction_list)

	var actions := HBoxContainer.new()
	actions.add_theme_constant_override("separation", 4)
	actions.add_child(_make_button("New", _add_interaction))
	actions.add_child(_make_button("Delete", _delete_selected_interaction))
	panel.add_child(actions)
	return panel


func _build_center_panel() -> Control:
	var panel := VBoxContainer.new()
	panel.custom_minimum_size = Vector2(0.0, 0.0)
	panel.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	panel.size_flags_vertical = Control.SIZE_EXPAND_FILL
	panel.add_theme_constant_override("separation", 4)

	_selected_summary_label = Label.new()
	_selected_summary_label.text = "No interaction selected."
	_selected_summary_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_selected_summary_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	panel.add_child(_selected_summary_label)

	var graph_actions := HBoxContainer.new()
	graph_actions.add_theme_constant_override("separation", 4)
	graph_actions.add_child(_make_button("Add Node", _add_node))
	graph_actions.add_child(_make_button("Add Option", _add_option_to_selected_node))
	panel.add_child(graph_actions)

	_graph = GraphEdit.new()
	_graph.custom_minimum_size = Vector2(0.0, GRAPH_MIN_VISIBLE_HEIGHT)
	_graph.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_graph.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_graph.zoom_min = 0.5
	_graph.zoom_max = 2.0
	_graph.show_zoom_label = true
	_graph.show_zoom_buttons = true
	call_deferred("_apply_graph_readability")
	_graph.connection_request.connect(_on_connection_request)
	_graph.disconnection_request.connect(_on_disconnection_request)
	panel.add_child(_graph)
	return panel


func _build_right_panel() -> Control:
	var scroll := ScrollContainer.new()
	scroll.custom_minimum_size = Vector2(0.0, 0.0)
	scroll.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL

	_inspector = VBoxContainer.new()
	_inspector.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_inspector.add_theme_constant_override("separation", 8)
	scroll.add_child(_inspector)
	return scroll


func _load_resources() -> void:
	_dirty = false
	_selected_index = -1
	_selected_node_id = ""
	_selected_option_index = -1
	_entries = []

	_authored_resource = ResourceLoader.load(AUTHORED_DATA_PATH, "", ResourceLoader.CACHE_MODE_IGNORE)
	if _authored_resource == null:
		_set_status("Could not load %s." % AUTHORED_DATA_PATH)
		_refresh_interaction_list()
		_refresh_graph()
		_refresh_inspector()
		return

	_customer_path = str(_authored_resource.get("CustomerInteractionsPath"))
	if _customer_path.strip_edges().is_empty():
		_set_status("Authored data has no CustomerInteractionsPath.")
		_refresh_interaction_list()
		_refresh_graph()
		_refresh_inspector()
		return

	_customer_resource = ResourceLoader.load(_customer_path, "", ResourceLoader.CACHE_MODE_IGNORE)
	if _customer_resource == null:
		_set_status("Could not load customer interactions at %s." % _customer_path)
		_refresh_interaction_list()
		_refresh_graph()
		_refresh_inspector()
		return

	var loaded_entries = _customer_resource.get("Entries")
	if loaded_entries is Array:
		_entries = loaded_entries
	else:
		_entries = []
		_customer_resource.set("Entries", _entries)

	_refresh_interaction_list()
	if _entries.size() > 0:
		_select_interaction(0)
	else:
		_refresh_graph()
		_refresh_inspector()
	_set_status("Loaded %d interactions from %s." % [_entries.size(), _customer_path])


func _refresh_interaction_list() -> void:
	if _interaction_list == null:
		return

	_interaction_list.clear()
	for index in range(_entries.size()):
		var entry := _entry_at(index)
		var label := "%s  -  %s" % [_string_value(entry, "id", "<missing id>"), _string_value(entry, "title", "Untitled")]
		_interaction_list.add_item(label)

	if _selected_index >= 0 and _selected_index < _interaction_list.item_count:
		_interaction_list.select(_selected_index)


func _on_interaction_selected(index: int) -> void:
	_select_interaction(index)


func _select_interaction(index: int) -> void:
	if index < 0 or index >= _entries.size():
		return

	_selected_index = index
	var entry := _current_entry()
	var start_node_id := _string_value(entry, "dialogueStartNodeId", "")
	var nodes := _dialogue_nodes(entry)
	if start_node_id.is_empty() and nodes.size() > 0:
		start_node_id = _string_value(nodes[0], "id", "")
	_selected_node_id = start_node_id
	_selected_option_index = -1
	_refresh_interaction_list()
	_refresh_graph()
	_refresh_inspector()
	_set_status("Selected %s." % _string_value(entry, "id", "<missing id>"))


func _refresh_graph() -> void:
	if _graph == null:
		return

	for child in _graph.get_children():
		if child is GraphNode:
			_graph.remove_child(child)
			child.queue_free()
	_graph.clear_connections()
	_graph_name_by_node_id.clear()
	_node_id_by_graph_name.clear()
	_option_index_by_slot.clear()
	_refresh_selected_summary()

	var entry := _current_entry()
	if entry.is_empty():
		_add_graph_placeholder("No interaction selected", "Select an interaction from the list to edit its dialogue tree.", false)
		call_deferred("_apply_graph_readability")
		return

	var nodes := _dialogue_nodes(entry)
	if nodes.is_empty():
		_add_graph_placeholder(
			"No dialogue tree",
			"This interaction has no dialogue nodes yet. Use Add Node in the inspector or Add first node here to create its first branch.",
			true)
		call_deferred("_apply_graph_readability")
		return

	for index in range(nodes.size()):
		var node: Dictionary = nodes[index]
		var node_id := _string_value(node, "id", "node_%d" % index)
		var graph_name := "node_%d" % index
		_graph_name_by_node_id[node_id] = graph_name
		_node_id_by_graph_name[graph_name] = node_id

		var graph_node := GraphNode.new()
		graph_node.name = graph_name
		graph_node.title = node_id
		graph_node.custom_minimum_size = Vector2(320.0, 0.0)
		graph_node.position_offset = Vector2(40.0 + float(index % 2) * 390.0, 40.0 + float(index / 2) * 260.0)
		_graph.add_child(graph_node)

		var node_actions := HBoxContainer.new()
		node_actions.add_theme_constant_override("separation", 4)
		var select_button := Button.new()
		select_button.text = "Select node"
		select_button.custom_minimum_size = Vector2(136.0, 34.0)
		select_button.pressed.connect(_select_node.bind(node_id))
		node_actions.add_child(select_button)
		var add_option_button := Button.new()
		add_option_button.text = "Add option"
		add_option_button.custom_minimum_size = Vector2(136.0, 34.0)
		add_option_button.pressed.connect(_add_option_to_node.bind(node_id))
		node_actions.add_child(add_option_button)
		graph_node.add_child(node_actions)
		graph_node.set_slot_enabled_left(0, true)
		graph_node.set_slot_type_left(0, 0)

		var lines := _dialogue_lines(node)
		var summary := Label.new()
		summary.text = "%d lines" % lines.size()
		summary.add_theme_font_size_override("font_size", 14)
		graph_node.add_child(summary)

		var options := _dialogue_options(node)
		for option_index in range(options.size()):
			var option: Dictionary = options[option_index]
			var row := HBoxContainer.new()
			row.add_theme_constant_override("separation", 4)
			var option_button := Button.new()
			option_button.text = _build_option_preview_label(option)
			option_button.custom_minimum_size = Vector2(280.0, 34.0)
			option_button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
			option_button.pressed.connect(_select_option.bind(node_id, option_index))
			row.add_child(option_button)
			graph_node.add_child(row)

			var slot_index := graph_node.get_child_count() - 1
			graph_node.set_slot_enabled_right(slot_index, true)
			graph_node.set_slot_type_right(slot_index, 0)
			_option_index_by_slot["%s::%d" % [graph_name, slot_index]] = option_index

	for index in range(nodes.size()):
		var node: Dictionary = nodes[index]
		var node_id := _string_value(node, "id", "")
		var from_graph_name := str(_graph_name_by_node_id.get(node_id, ""))
		if from_graph_name.is_empty():
			continue

		var options := _dialogue_options(node)
		for option_index in range(options.size()):
			var option: Dictionary = options[option_index]
			var next_node_id := _string_value(option, "nextNodeId", "")
			if next_node_id.is_empty() or not _graph_name_by_node_id.has(next_node_id):
				continue

			var from_slot := _find_option_slot(from_graph_name, option_index)
			if from_slot < 0:
				continue

			_graph.connect_node(from_graph_name, from_slot, _graph_name_by_node_id[next_node_id], 0)

	call_deferred("_apply_graph_readability")


func _apply_graph_readability() -> void:
	if _graph == null:
		return

	_graph.zoom = GRAPH_READABLE_ZOOM


func _refresh_selected_summary() -> void:
	if _selected_summary_label == null:
		return

	var entry := _current_entry()
	if entry.is_empty():
		_selected_summary_label.text = "No interaction selected."
		return

	var nodes := _dialogue_nodes(entry)
	var option_count := 0
	for node in nodes:
		if node is Dictionary:
			var node_dictionary: Dictionary = node
			option_count += _dialogue_options(node_dictionary).size()

	var interaction_id := _string_value(entry, "id", "<missing id>")
	var title := _string_value(entry, "title", "Untitled")
	var selected_node := _selected_node_id if not _selected_node_id.is_empty() else "<none>"
	_selected_summary_label.text = "%s - %s | %d node(s), %d option(s) | selected node: %s" % [
		interaction_id,
		title,
		nodes.size(),
		option_count,
		selected_node
	]


func _add_graph_placeholder(title: String, message: String, show_add_node_button: bool) -> void:
	var graph_node := GraphNode.new()
	graph_node.name = "empty_state"
	graph_node.title = title
	graph_node.custom_minimum_size = Vector2(320.0, 0.0)
	graph_node.position_offset = Vector2(40.0, 40.0)
	_graph.add_child(graph_node)

	var label := Label.new()
	label.text = message
	label.custom_minimum_size = Vector2(300.0, 0.0)
	label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	graph_node.add_child(label)

	if show_add_node_button:
		var button := Button.new()
		button.text = "Add first node"
		button.custom_minimum_size = Vector2(280.0, 34.0)
		button.pressed.connect(_add_node)
		graph_node.add_child(button)


func _select_node(node_id: String) -> void:
	_selected_node_id = node_id
	_selected_option_index = -1
	_refresh_graph()
	_refresh_inspector()


func _select_option(node_id: String, option_index: int) -> void:
	_selected_node_id = node_id
	_selected_option_index = option_index
	_refresh_graph()
	_refresh_inspector()


func _on_connection_request(from_node, from_port, to_node, to_port) -> void:
	var from_graph_name := str(from_node)
	var to_graph_name := str(to_node)
	var source_node_id := str(_node_id_by_graph_name.get(from_graph_name, ""))
	var target_node_id := str(_node_id_by_graph_name.get(to_graph_name, ""))
	var option_index := int(_option_index_by_slot.get("%s::%d" % [from_graph_name, from_port], -1))
	if source_node_id.is_empty() or target_node_id.is_empty() or option_index < 0:
		return

	var option := _option_at(source_node_id, option_index)
	if option.is_empty():
		return

	option["nextNodeId"] = target_node_id
	option["endsInteraction"] = false
	_mark_dirty("Connected option to %s." % target_node_id)
	_refresh_graph()
	_refresh_inspector()


func _on_disconnection_request(from_node, from_port, to_node, to_port) -> void:
	var from_graph_name := str(from_node)
	var source_node_id := str(_node_id_by_graph_name.get(from_graph_name, ""))
	var target_node_id := str(_node_id_by_graph_name.get(str(to_node), ""))
	var option_index := int(_option_index_by_slot.get("%s::%d" % [from_graph_name, from_port], -1))
	if source_node_id.is_empty() or option_index < 0:
		return

	var option := _option_at(source_node_id, option_index)
	if option.is_empty():
		return
	if _string_value(option, "nextNodeId", "") == target_node_id:
		option.erase("nextNodeId")
		_mark_dirty("Disconnected option.")
		_refresh_graph()
		_refresh_inspector()


func _refresh_inspector() -> void:
	if _inspector == null:
		return

	_clear_children(_inspector)
	var entry := _current_entry()
	if entry.is_empty():
		_inspector.add_child(_make_label("No interaction selected."))
		return

	_inspector.add_child(_section_header("Interaction"))
	_inspector.add_child(_line_edit("Id", _string_value(entry, "id", ""), func(value): _set_entry_value("id", value)))
	_inspector.add_child(_line_edit("Title", _string_value(entry, "title", ""), func(value): _set_entry_value("title", value)))
	_inspector.add_child(_text_edit("Text", _string_value(entry, "text", ""), 70.0, func(value): _set_entry_value("text", value)))
	_inspector.add_child(_line_edit("Story Character", _string_value(entry, "storyCharacterId", ""), func(value): _set_optional_entry_value("storyCharacterId", value)))
	_inspector.add_child(_line_edit("Visit Id", _string_value(entry, "visitId", ""), func(value): _set_optional_entry_value("visitId", value)))
	_inspector.add_child(_line_edit("Start Node", _string_value(entry, "dialogueStartNodeId", ""), func(value): _set_entry_value("dialogueStartNodeId", value)))

	_inspector.add_child(_json_editor("Requires", entry.get("requires", {}), true, func(value): _set_optional_entry_value("requires", value)))
	_inspector.add_child(_build_preview_section())

	_inspector.add_child(_section_header("Graph Actions"))
	var action_row := HBoxContainer.new()
	action_row.add_theme_constant_override("separation", 4)
	action_row.add_child(_make_button("Add Node", _add_node))
	action_row.add_child(_make_button("Add Option", _add_option_to_selected_node))
	_inspector.add_child(action_row)

	var node := _selected_node()
	if not node.is_empty():
		_inspector.add_child(_section_header("Node"))
		_inspector.add_child(_rename_node_editor(node))
		_inspector.add_child(_text_edit("Fallback Text", _string_value(node, "text", ""), 55.0, func(value): _set_node_value("text", value)))
		_inspector.add_child(_json_editor("Lines", _dialogue_lines(node), false, func(value): _set_node_value("lines", value)))
		_inspector.add_child(_build_options_list(node))

	var option := _selected_option()
	if not option.is_empty():
		_inspector.add_child(_section_header("Option"))
		_inspector.add_child(_line_edit("Id", _string_value(option, "id", ""), func(value): _set_option_value("id", value)))
		_inspector.add_child(_line_edit("Label", _string_value(option, "label", ""), func(value): _set_option_value("label", value)))
		_inspector.add_child(_text_edit("Response Text", _string_value(option, "responseText", ""), 55.0, func(value): _set_optional_option_value("responseText", value)))
		_inspector.add_child(_line_edit("Next Node", _string_value(option, "nextNodeId", ""), func(value): _set_optional_option_value("nextNodeId", value)))
		_inspector.add_child(_line_edit("Return Node", _string_value(option, "returnNodeId", ""), func(value): _set_optional_option_value("returnNodeId", value)))
		_inspector.add_child(_check_box("Reveals Request", bool(option.get("revealsRequest", false)), func(value): _set_optional_option_value("revealsRequest", value)))
		_inspector.add_child(_check_box("Returns To Dialogue", bool(option.get("returnsToDialogue", false)), func(value): _set_optional_option_value("returnsToDialogue", value)))
		_inspector.add_child(_check_box("Ends Interaction", bool(option.get("endsInteraction", false)), func(value): _set_optional_option_value("endsInteraction", value)))
		_inspector.add_child(_json_editor("Response Lines", option.get("responseLines", []), false, func(value): _set_optional_option_value("responseLines", value)))
		_inspector.add_child(_json_editor("Requires", option.get("requires", {}), true, func(value): _set_optional_option_value("requires", value)))
		_inspector.add_child(_json_editor("Effects", option.get("effects", []), false, func(value): _set_optional_option_value("effects", value)))

	_validation_label = Label.new()
	_validation_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_inspector.add_child(_validation_label)
	_validate_and_show(false)


func _build_preview_section() -> Control:
	var box := VBoxContainer.new()
	box.add_theme_constant_override("separation", 4)
	box.add_child(_section_header("Preview State"))

	var reputation := SpinBox.new()
	reputation.min_value = 0
	reputation.max_value = 100
	reputation.step = 1
	reputation.value = _preview_reputation
	reputation.value_changed.connect(func(value): _preview_reputation = int(value))
	box.add_child(_labeled_control("Reputation", reputation))
	box.add_child(_text_edit("Flags", _preview_flags_text, 48.0, func(value): _preview_flags_text = value))
	box.add_child(_text_edit("Relationships", _preview_relationships_text, 48.0, func(value): _preview_relationships_text = value))
	box.add_child(_text_edit("Quests", _preview_quests_text, 48.0, func(value): _preview_quests_text = value))
	box.add_child(_text_edit("Seen Options", _preview_seen_options_text, 48.0, func(value): _preview_seen_options_text = value))
	box.add_child(_make_button("Apply Preview", _apply_preview_state))
	return box


func _apply_preview_state() -> void:
	_preview_flags = _parse_set_lines(_preview_flags_text)
	_preview_relationships = _parse_int_map_lines(_preview_relationships_text, 50)
	_preview_quests = _parse_string_map_lines(_preview_quests_text)
	_preview_seen_options = _parse_set_lines(_preview_seen_options_text)
	_refresh_graph()
	_set_status("Preview state applied.")


func _build_options_list(node: Dictionary) -> Control:
	var box := VBoxContainer.new()
	box.add_theme_constant_override("separation", 4)
	box.add_child(_section_header("Options"))

	var options := _dialogue_options(node)
	for index in range(options.size()):
		var option: Dictionary = options[index]
		var button := Button.new()
		button.text = "%d. %s" % [index + 1, _string_value(option, "label", "<empty label>")]
		button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		button.pressed.connect(_select_option.bind(_selected_node_id, index))
		box.add_child(button)

	return box


func _rename_node_editor(node: Dictionary) -> Control:
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 4)
	var edit := LineEdit.new()
	edit.text = _string_value(node, "id", "")
	edit.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	row.add_child(_make_label("Id"))
	row.add_child(edit)
	row.add_child(_make_button("Apply", func(): _rename_node(_selected_node_id, edit.text)))
	return row


func _add_interaction() -> void:
	if _customer_resource == null:
		return

	var id := _next_unique_interaction_id()
	var entry := {
		"id": id,
		"title": "New Dialogue",
		"text": "",
		"pool": "plot",
		"difficulty": 1,
		"dialogueStartNodeId": "intro",
		"desiredTraits": {},
		"badTraits": {},
		"dialogueNodes": [
			{
				"id": "intro",
				"lines": [],
				"options": []
			}
		]
	}
	_entries.append(entry)
	_customer_resource.set("Entries", _entries)
	_mark_dirty("Created %s." % id)
	_refresh_interaction_list()
	_select_interaction(_entries.size() - 1)


func _delete_selected_interaction() -> void:
	if _selected_index < 0 or _selected_index >= _entries.size():
		return

	_entries.remove_at(_selected_index)
	_customer_resource.set("Entries", _entries)
	_mark_dirty("Deleted interaction.")
	var next_index = min(_selected_index, _entries.size() - 1)
	_selected_index = -1
	_refresh_interaction_list()
	if next_index >= 0:
		_select_interaction(next_index)
	else:
		_refresh_graph()
		_refresh_inspector()


func _add_node() -> void:
	var entry := _current_entry()
	if entry.is_empty():
		return

	var nodes := _dialogue_nodes(entry)
	var was_empty := nodes.is_empty()
	var node_id := "intro" if was_empty else _next_unique_node_id("node")
	nodes.append({
		"id": node_id,
		"lines": [],
		"options": []
	})
	if was_empty and _string_value(entry, "dialogueStartNodeId", "").is_empty():
		entry["dialogueStartNodeId"] = node_id
	_selected_node_id = node_id
	_selected_option_index = -1
	_mark_dirty("Added node %s." % node_id)
	_refresh_graph()
	_refresh_inspector()


func _add_option_to_selected_node() -> void:
	if _selected_node().is_empty():
		_set_status("Select a node before adding an option.")
		return

	_add_option_to_node(_selected_node_id)


func _add_option_to_node(node_id: String) -> void:
	_selected_node_id = node_id
	_selected_option_index = -1
	var node := _selected_node()
	if node.is_empty():
		_set_status("Select a node before adding an option.")
		return

	var options := _dialogue_options(node)
	var option_id := _next_unique_option_id(node, "option")
	options.append({
		"id": option_id,
		"label": "New option",
		"responseLines": [],
		"endsInteraction": true
	})
	_selected_option_index = options.size() - 1
	_mark_dirty("Added option %s." % option_id)
	_refresh_graph()
	_refresh_inspector()


func _rename_node(old_id: String, new_id: String) -> void:
	var normalized_new_id := new_id.strip_edges()
	if normalized_new_id.is_empty() or old_id == normalized_new_id:
		return

	var entry := _current_entry()
	for node in _dialogue_nodes(entry):
		if _string_value(node, "id", "") == normalized_new_id:
			_set_status("Node id '%s' already exists." % normalized_new_id)
			return

	var node := _selected_node()
	if node.is_empty():
		return

	node["id"] = normalized_new_id
	if _string_value(entry, "dialogueStartNodeId", "") == old_id:
		entry["dialogueStartNodeId"] = normalized_new_id

	for other_node in _dialogue_nodes(entry):
		for option in _dialogue_options(other_node):
			if _string_value(option, "nextNodeId", "") == old_id:
				option["nextNodeId"] = normalized_new_id
			if _string_value(option, "returnNodeId", "") == old_id:
				option["returnNodeId"] = normalized_new_id

	_selected_node_id = normalized_new_id
	_mark_dirty("Renamed node.")
	_refresh_graph()
	_refresh_inspector()


func _set_entry_value(key: String, value) -> void:
	var entry := _current_entry()
	if entry.is_empty():
		return

	entry[key] = value
	_mark_dirty("Edited interaction.")
	_refresh_interaction_list()


func _set_optional_entry_value(key: String, value) -> void:
	var entry := _current_entry()
	if entry.is_empty():
		return

	_set_optional_dictionary_value(entry, key, value)
	_mark_dirty("Edited interaction.")
	_refresh_graph()


func _set_node_value(key: String, value) -> void:
	var node := _selected_node()
	if node.is_empty():
		return

	node[key] = value
	_mark_dirty("Edited node.")
	_refresh_graph()


func _set_option_value(key: String, value) -> void:
	var option := _selected_option()
	if option.is_empty():
		return

	option[key] = value
	_mark_dirty("Edited option.")
	_refresh_graph()


func _set_optional_option_value(key: String, value) -> void:
	var option := _selected_option()
	if option.is_empty():
		return

	_set_optional_dictionary_value(option, key, value)
	_mark_dirty("Edited option.")
	_refresh_graph()


func _set_optional_dictionary_value(target: Dictionary, key: String, value) -> void:
	if value == null:
		target.erase(key)
		return
	if value is String and value.strip_edges().is_empty():
		target.erase(key)
		return
	if value is Array and value.is_empty():
		target.erase(key)
		return
	if value is Dictionary and value.is_empty():
		target.erase(key)
		return
	if value is bool and value == false:
		target.erase(key)
		return

	target[key] = value


func _save_resource() -> void:
	if _customer_resource == null:
		_set_status("Nothing to save.")
		return

	_customer_resource.set("Entries", _entries)
	var error := ResourceSaver.save(_customer_resource, _customer_path)
	if error == OK:
		_dirty = false
		_set_status("Saved %s." % _customer_path)
	else:
		_set_status("Save failed with error %s." % error)


func _validate_and_show(update_status := true) -> void:
	var warnings := _validate_all()
	if _validation_label != null:
		_validation_label.text = "No validation warnings." if warnings.is_empty() else "\n".join(warnings)
	if update_status:
		_set_status("Validation passed." if warnings.is_empty() else "Validation found %d warning(s)." % warnings.size())


func _validate_all() -> Array:
	var warnings := []
	var interaction_ids := {}
	for index in range(_entries.size()):
		var entry := _entry_at(index)
		var context := "Interaction #%d" % (index + 1)
		var interaction_id := _string_value(entry, "id", "")
		_validate_id(interaction_id, "%s id" % context, warnings)
		if not interaction_id.is_empty():
			if interaction_ids.has(interaction_id):
				warnings.append("%s duplicates interaction id '%s'." % [context, interaction_id])
			interaction_ids[interaction_id] = true
		_validate_id(_string_value(entry, "storyCharacterId", ""), "%s storyCharacterId" % context, warnings)
		_validate_requirements(entry.get("requires", {}), "%s requirements" % context, warnings)
		_validate_interaction_nodes(entry, context, warnings)
	return warnings


func _validate_interaction_nodes(entry: Dictionary, context: String, warnings: Array) -> void:
	var nodes := _dialogue_nodes(entry)
	var node_ids := {}
	for node in nodes:
		var node_id := _string_value(node, "id", "")
		_validate_id(node_id, "%s node id" % context, warnings)
		if node_ids.has(node_id):
			warnings.append("%s has duplicate node id '%s'." % [context, node_id])
		node_ids[node_id] = true

	var start_node_id := _string_value(entry, "dialogueStartNodeId", "")
	if not start_node_id.is_empty() and not node_ids.has(start_node_id):
		warnings.append("%s starts at missing node '%s'." % [context, start_node_id])

	for node in nodes:
		var node_id := _string_value(node, "id", "")
		var options := _dialogue_options(node)
		if options.size() > MAX_DIALOGUE_OPTIONS_PER_NODE:
			warnings.append("%s node '%s' has %d options; only %d are shown at runtime." % [context, node_id, options.size(), MAX_DIALOGUE_OPTIONS_PER_NODE])
		for option in options:
			var option_context := "%s node '%s' option '%s'" % [context, node_id, _string_value(option, "id", "")]
			var next_node_id := _string_value(option, "nextNodeId", "")
			var return_node_id := _string_value(option, "returnNodeId", "")
			if not next_node_id.is_empty() and not node_ids.has(next_node_id):
				warnings.append("%s points to missing node '%s'." % [option_context, next_node_id])
			if not return_node_id.is_empty() and not node_ids.has(return_node_id):
				warnings.append("%s returns to missing node '%s'." % [option_context, return_node_id])
			if bool(option.get("revealsRequest", false)) and bool(option.get("endsInteraction", false)):
				warnings.append("%s both reveals a request and ends the interaction." % option_context)
			if bool(option.get("returnsToDialogue", false)) and bool(option.get("endsInteraction", false)):
				warnings.append("%s both returns to dialogue and ends the interaction." % option_context)
			_validate_requirements(option.get("requires", {}), "%s requirements" % option_context, warnings)
			_validate_effects(option.get("effects", []), "%s effects" % option_context, warnings)


func _validate_requirements(requirements, context: String, warnings: Array) -> void:
	if not (requirements is Dictionary):
		return

	_validate_score(requirements.get("reputationMin", null), "%s reputationMin" % context, warnings)
	_validate_score(requirements.get("reputationMax", null), "%s reputationMax" % context, warnings)
	if requirements.has("reputationMin") and requirements.has("reputationMax") and int(requirements["reputationMin"]) > int(requirements["reputationMax"]):
		warnings.append("%s has reputationMin greater than reputationMax." % context)
	_validate_id(str(requirements.get("hasStoryFlag", "")), "%s hasStoryFlag" % context, warnings)
	_validate_id(str(requirements.get("missingStoryFlag", "")), "%s missingStoryFlag" % context, warnings)

	var quest_id := str(requirements.get("questId", "")).strip_edges()
	var quest_status := str(requirements.get("questStatus", "")).strip_edges()
	if not quest_id.is_empty() or not quest_status.is_empty():
		_validate_id(quest_id, "%s questId" % context, warnings)
		if quest_id.is_empty():
			warnings.append("%s defines questStatus without questId." % context)
		if quest_status.is_empty():
			warnings.append("%s defines questId without questStatus." % context)
		elif not QUEST_STATUSES.has(quest_status):
			warnings.append("%s references unknown questStatus '%s'." % [context, quest_status])

	var relationship_id := str(requirements.get("relationshipCharacterId", "")).strip_edges()
	if requirements.has("relationshipMin") or requirements.has("relationshipMax") or not relationship_id.is_empty():
		_validate_id(relationship_id, "%s relationshipCharacterId" % context, warnings)
		_validate_score(requirements.get("relationshipMin", null), "%s relationshipMin" % context, warnings)
		_validate_score(requirements.get("relationshipMax", null), "%s relationshipMax" % context, warnings)
		if relationship_id.is_empty() and (requirements.has("relationshipMin") or requirements.has("relationshipMax")):
			warnings.append("%s defines relationship range without relationshipCharacterId." % context)


func _validate_effects(effects, context: String, warnings: Array) -> void:
	if not (effects is Array):
		return

	for index in range(effects.size()):
		var effect = effects[index]
		if not (effect is Dictionary):
			continue
		var effect_context := "%s #%d" % [context, index + 1]
		_validate_score(effect.get("setReputation", null), "%s setReputation" % effect_context, warnings)
		_validate_id(str(effect.get("addStoryFlag", "")), "%s addStoryFlag" % effect_context, warnings)
		_validate_id(str(effect.get("removeStoryFlag", "")), "%s removeStoryFlag" % effect_context, warnings)
		var quest_id := str(effect.get("questId", "")).strip_edges()
		var set_quest_status := str(effect.get("setQuestStatus", "")).strip_edges()
		if not quest_id.is_empty() or not set_quest_status.is_empty():
			_validate_id(quest_id, "%s questId" % effect_context, warnings)
			if quest_id.is_empty():
				warnings.append("%s sets quest status without questId." % effect_context)
			if set_quest_status.is_empty():
				warnings.append("%s defines questId without setQuestStatus." % effect_context)
			elif not QUEST_STATUSES.has(set_quest_status):
				warnings.append("%s sets unknown quest status '%s'." % [effect_context, set_quest_status])
		var relationship_id := str(effect.get("relationshipCharacterId", "")).strip_edges()
		if effect.has("addRelationship") or effect.has("setRelationship") or not relationship_id.is_empty():
			_validate_id(relationship_id, "%s relationshipCharacterId" % effect_context, warnings)
			_validate_score(effect.get("setRelationship", null), "%s setRelationship" % effect_context, warnings)
			if relationship_id.is_empty() and (effect.has("addRelationship") or effect.has("setRelationship")):
				warnings.append("%s changes relationship without relationshipCharacterId." % effect_context)


func _validate_id(value: String, context: String, warnings: Array) -> void:
	var text := value.strip_edges()
	if text.is_empty():
		return
	if _id_regex.search(text) == null:
		warnings.append("%s '%s' should use lower_snake_case letters and numbers." % [context, text])


func _validate_score(value, context: String, warnings: Array) -> void:
	if value == null:
		return
	var score := int(value)
	if score < 0 or score > 100:
		warnings.append("%s should be between 0 and 100." % context)


func _build_option_preview_label(option: Dictionary) -> String:
	var labels := []
	if not _is_option_available(option):
		labels.append("locked")
	if _preview_seen_options.has(_string_value(option, "id", "")):
		labels.append("seen")

	var prefix := ""
	if not labels.is_empty():
		prefix = "[%s] " % ",".join(labels)
	return "%s%s" % [prefix, _string_value(option, "label", "<empty label>")]


func _is_option_available(option: Dictionary) -> bool:
	return _are_requirements_met(option.get("requires", {}))


func _are_requirements_met(requirements) -> bool:
	if not (requirements is Dictionary):
		return true

	if requirements.has("reputationMin") and _preview_reputation < int(requirements["reputationMin"]):
		return false
	if requirements.has("reputationMax") and _preview_reputation > int(requirements["reputationMax"]):
		return false

	var has_flag := str(requirements.get("hasStoryFlag", "")).strip_edges()
	if not has_flag.is_empty() and not _preview_flags.has(has_flag):
		return false
	var missing_flag := str(requirements.get("missingStoryFlag", "")).strip_edges()
	if not missing_flag.is_empty() and _preview_flags.has(missing_flag):
		return false

	var quest_id := str(requirements.get("questId", "")).strip_edges()
	var quest_status := str(requirements.get("questStatus", "")).strip_edges()
	if not quest_id.is_empty() and not quest_status.is_empty():
		if str(_preview_quests.get(quest_id, "NotStarted")) != quest_status:
			return false

	var relationship_id := str(requirements.get("relationshipCharacterId", "")).strip_edges()
	if requirements.has("relationshipMin") or requirements.has("relationshipMax"):
		if relationship_id.is_empty():
			return false
		var relationship_score := int(_preview_relationships.get(relationship_id, 50))
		if requirements.has("relationshipMin") and relationship_score < int(requirements["relationshipMin"]):
			return false
		if requirements.has("relationshipMax") and relationship_score > int(requirements["relationshipMax"]):
			return false

	return true


func _entry_at(index: int) -> Dictionary:
	if index < 0 or index >= _entries.size():
		return {}
	return _entries[index] if _entries[index] is Dictionary else {}


func _current_entry() -> Dictionary:
	return _entry_at(_selected_index)


func _dialogue_nodes(entry: Dictionary) -> Array:
	if not entry.has("dialogueNodes") or not (entry["dialogueNodes"] is Array):
		entry["dialogueNodes"] = []
	return entry["dialogueNodes"]


func _dialogue_lines(node: Dictionary) -> Array:
	if not node.has("lines") or not (node["lines"] is Array):
		node["lines"] = []
	return node["lines"]


func _dialogue_options(node: Dictionary) -> Array:
	if not node.has("options") or not (node["options"] is Array):
		node["options"] = []
	return node["options"]


func _selected_node() -> Dictionary:
	var entry := _current_entry()
	for node in _dialogue_nodes(entry):
		if _string_value(node, "id", "") == _selected_node_id:
			return node
	return {}


func _selected_option() -> Dictionary:
	return _option_at(_selected_node_id, _selected_option_index)


func _option_at(node_id: String, option_index: int) -> Dictionary:
	if option_index < 0:
		return {}
	var entry := _current_entry()
	for node in _dialogue_nodes(entry):
		if _string_value(node, "id", "") != node_id:
			continue
		var options := _dialogue_options(node)
		if option_index >= 0 and option_index < options.size() and options[option_index] is Dictionary:
			return options[option_index]
	return {}


func _find_option_slot(graph_name: String, option_index: int) -> int:
	for key in _option_index_by_slot.keys():
		if int(_option_index_by_slot[key]) != option_index:
			continue
		if str(key).begins_with("%s::" % graph_name):
			return int(str(key).split("::")[1])
	return -1


func _next_unique_interaction_id() -> String:
	var index := 1
	while true:
		var candidate := "new_dialogue_%d" % index
		var exists := false
		for entry in _entries:
			if entry is Dictionary and _string_value(entry, "id", "") == candidate:
				exists = true
				break
		if not exists:
			return candidate
		index += 1
	return "new_dialogue_%d" % index


func _next_unique_node_id(base: String) -> String:
	var entry := _current_entry()
	var index := 1
	while true:
		var candidate := "%s_%d" % [base, index]
		var exists := false
		for node in _dialogue_nodes(entry):
			if _string_value(node, "id", "") == candidate:
				exists = true
				break
		if not exists:
			return candidate
		index += 1
	return "%s_%d" % [base, index]


func _next_unique_option_id(node: Dictionary, base: String) -> String:
	var index := 1
	while true:
		var candidate := "%s_%d" % [base, index]
		var exists := false
		for option in _dialogue_options(node):
			if _string_value(option, "id", "") == candidate:
				exists = true
				break
		if not exists:
			return candidate
		index += 1
	return "%s_%d" % [base, index]


func _string_value(source: Dictionary, key: String, fallback: String = "") -> String:
	if not source.has(key):
		return fallback
	var value = source[key]
	if value == null:
		return fallback
	var text := str(value)
	return fallback if text.strip_edges().is_empty() else text


func _parse_set_lines(text: String) -> Dictionary:
	var result := {}
	for line in text.split("\n", false):
		var value := line.strip_edges()
		if not value.is_empty():
			result[value] = true
	return result


func _parse_string_map_lines(text: String) -> Dictionary:
	var result := {}
	for line in text.split("\n", false):
		var parts := line.split("=", false, 1)
		if parts.size() != 2:
			continue
		var key := parts[0].strip_edges()
		var value := parts[1].strip_edges()
		if not key.is_empty() and not value.is_empty():
			result[key] = value
	return result


func _parse_int_map_lines(text: String, fallback: int) -> Dictionary:
	var result := {}
	for line in text.split("\n", false):
		var parts := line.split("=", false, 1)
		if parts.size() != 2:
			continue
		var key := parts[0].strip_edges()
		if key.is_empty():
			continue
		var value := int(parts[1].strip_edges()) if parts[1].strip_edges().is_valid_int() else fallback
		result[key] = clampi(value, 0, 100)
	return result


func _json_editor(label: String, value, expect_dictionary: bool, set_value: Callable) -> Control:
	var box := VBoxContainer.new()
	box.add_theme_constant_override("separation", 4)
	box.add_child(_make_label(label))
	var editor := TextEdit.new()
	editor.custom_minimum_size = Vector2(0.0, 92.0)
	editor.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	editor.text = "{}" if value == null else JSON.stringify(value, "\t")
	box.add_child(editor)
	box.add_child(_make_button("Apply %s" % label, func():
		var parsed = JSON.parse_string(editor.text)
		if parsed == null:
			_set_status("Could not parse %s JSON." % label)
			return
		if expect_dictionary and not (parsed is Dictionary):
			_set_status("%s must be a JSON object." % label)
			return
		if not expect_dictionary and not (parsed is Array):
			_set_status("%s must be a JSON array." % label)
			return
		set_value.call(parsed)
		_refresh_inspector()
	))
	return box


func _text_edit(label: String, value: String, height: float, set_value: Callable) -> Control:
	var box := VBoxContainer.new()
	box.add_theme_constant_override("separation", 4)
	box.add_child(_make_label(label))
	var editor := TextEdit.new()
	editor.custom_minimum_size = Vector2(0.0, height)
	editor.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	editor.text = value
	editor.text_changed.connect(func(): set_value.call(editor.text))
	box.add_child(editor)
	return box


func _line_edit(label: String, value: String, set_value: Callable) -> Control:
	var edit := LineEdit.new()
	edit.text = value
	edit.text_changed.connect(func(new_value): set_value.call(new_value))
	return _labeled_control(label, edit)


func _check_box(label: String, value: bool, set_value: Callable) -> CheckBox:
	var checkbox := CheckBox.new()
	checkbox.text = label
	checkbox.button_pressed = value
	checkbox.toggled.connect(func(toggled): set_value.call(toggled))
	return checkbox


func _labeled_control(label: String, control: Control) -> Control:
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 4)
	row.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	var label_control := Label.new()
	label_control.text = label
	label_control.custom_minimum_size = Vector2(88.0, 0.0)
	row.add_child(label_control)
	control.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	row.add_child(control)
	return row


func _section_header(text: String) -> Label:
	var label := _make_label(text)
	label.add_theme_font_size_override("font_size", 14)
	return label


func _make_label(text: String) -> Label:
	var label := Label.new()
	label.text = text
	label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	return label


func _make_button(text: String, action: Callable) -> Button:
	var button := Button.new()
	button.text = text
	button.pressed.connect(action)
	return button


func _mark_dirty(message: String) -> void:
	_dirty = true
	_set_status("%s Unsaved changes." % message)


func _set_status(message: String) -> void:
	if _status_label == null:
		return
	_status_label.text = message


func _clear_children(node: Node) -> void:
	for child in node.get_children():
		node.remove_child(child)
		child.queue_free()
