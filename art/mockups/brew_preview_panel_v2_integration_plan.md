# Brew Preview Panel V2 Integration Plan

## Generated Files

- Final game asset: `res://Assets/UI/brew_preview_panel_board_v2.png`
- Chroma-key source: `res://Assets/UI/brew_preview_panel_board_v2_chromakey.png`
- Button overlay, normal: `res://Assets/UI/brew_preview_button_overlay_normal.png`
- Button overlay, hover: `res://Assets/UI/brew_preview_button_overlay_hover.png`
- Button overlay, pressed: `res://Assets/UI/brew_preview_button_overlay_pressed.png`
- Button overlay, disabled: `res://Assets/UI/brew_preview_button_overlay_disabled.png`
- Live-text mockup: `res://art/mockups/brew_preview_panel_v2_live_text_mockup.png`

## Design Intent

- Use the PNG as the painted board/background only.
- Keep title, subtitle, request text, ingredient icons, ingredient names, slot numbers, and button labels as live Godot controls.
- Do not bake dynamic text into the asset.
- Update live text colors toward ink/oxblood on parchment for legibility.

## Scene Integration

Target scene: `res://Scenes/UI/GameUi.tscn`

Current root path:

- `PotionBrewingStationView/BrewPanel/Panel`

Implemented changes:

1. Added `BoardArt` as the first visual child under `PotionBrewingStationView/BrewPanel/Panel`.
2. Set its texture to `res://Assets/UI/brew_preview_panel_board_v2.png`.
3. Set its size to the panel base size: `Vector2(1085, 1450)`.
4. Hid the previous stylebox-only visual pieces: `Board`, `Paper`, `Clip`, `ClipLoop`, and corner pins.
5. Kept the existing live controls and script export paths intact.
6. Made visual styleboxes for slot markers, slot panels, and checklist frame transparent while preserving their layout role.
7. Replaced button stylebox states with painted `StyleBoxTexture` overlays:
   - normal
   - hover
   - pressed
   - disabled
8. Repositioned live controls to sit over the painted areas in the new asset:
   - Header labels over the upper parchment.
   - Slot number labels centered on the three brass medallions.
   - Ingredient icon/name controls inside the three painted slot plates.
   - Request checklist text inside the lower parchment frame.
   - `Brew` and `Clear` buttons over the bottom painted button plates.
9. Added import metadata for the generated PNGs.

## Verification

- Confirm the panel still opens from the brewing station.
- Confirm ingredient drag/drop still hits all three ingredient slots.
- Confirm `BrewPanel` export paths still resolve.
- Confirm the request checklist updates for no request, failed request, and satisfied request states.
- Confirm `Brew` and `Clear` button hover, pressed, disabled, and tooltip behavior still works.
- Run `dotnet build`.
