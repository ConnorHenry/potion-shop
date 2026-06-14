# Juniper Catching Redesign Implementation Plan

## Goal

Replace the placeholder block-shape presentation in `Scenes/Main/JuniperGathering.tscn` with a painted forest playfield while preserving the current mini-game mechanics:

- shake the juniper bush
- spawn falling berries
- catch only ripe dark blue berries
- freeze the basket on wrong berry catches
- keep the current reward and return flow

## Generated Concept Files

- `juniper_catching_screen_mockup.png` - active-play screen mockup.
- `juniper_catching_background_concept.png` - clean background concept without UI or active objects.
- `juniper_catching_asset_sheet_concept.png` - magenta chroma-key source sheet.
- `juniper_catching_asset_sheet_concept_alpha.png` - alpha-converted concept sheet for slicing.

## Proposed Asset Outputs

Create final sliced PNG assets under `Assets/Gathering/Juniper/`:

- `juniper_catching_background.png`
- `juniper_bush.png`
- `juniper_basket.png`
- `juniper_berry_ripe.png`
- `juniper_berry_wrong_red.png`
- `juniper_berry_wrong_amber.png`
- `juniper_berry_wrong_green.png`
- `juniper_berry_wrong_pale_blue.png`
- `juniper_leaf_particle_*.png` if leaf drift is implemented
- `juniper_shake_marks.png` if explicit shake marks are implemented

Let Godot create the matching `.import` files.

## Scene Changes

Update `Scenes/Main/JuniperGathering.tscn` conservatively:

- Replace `Root/Background` from `ColorRect` to a `TextureRect`, or add a new `TextureRect` background below `PlayArea`.
- Keep the existing exported paths where possible:
  - `Root/PlayArea/Bush`
  - `Root/PlayArea/Basket`
  - `Root/PlayArea/CatchLine`
- Change `Bush` from a styled `Panel` to a `TextureRect` using `juniper_bush.png`.
- Change `Basket` from a styled `Panel` to a `TextureRect` using `juniper_basket.png`.
- Keep `Bush` and `Basket` as `Control` descendants so the current `GuiInput` handlers still work.
- Keep the current top HUD, right feedback/instruction panel, bottom feedback panel, and result dialog as Godot UI nodes.
- Retheme panels with the existing dark translucent forest style rather than baking UI text into the background.

## Script Changes

Update `Scripts/UI/JuniperGathering.cs` in small steps:

- Add exported texture paths for the ripe berry and wrong berry sprites.
- Load textures once in `_Ready()` with `ResourceLoader.Load<Texture2D>()`.
- Replace dynamic `Panel` berry visuals with `TextureRect` berry visuals.
- Keep `BerryState.Visual` typed as `Control` or `TextureRect` instead of `Panel`.
- Preserve current collision math by using the existing `BerryDiameter`, basket rect, and positions.
- Keep the current shake, countdown, freeze, reward, save, and scene-return logic unchanged.
- Optionally add lightweight leaf particles later, only after the sprite replacement compiles.

## Suggested Implementation Order

1. Slice final sprites from `juniper_catching_asset_sheet_concept_alpha.png`.
2. Add final PNGs to `Assets/Gathering/Juniper/`.
3. Update the scene node types and textures while preserving node names and exported paths.
4. Update berry spawning to instantiate `TextureRect` sprites instead of stylebox `Panel` circles.
5. Build with `dotnet build`.
6. Run the scene and verify:
   - bush drag/shake still releases berries
   - basket drag still clamps below the catch line
   - ripe catches increment count
   - wrong catches freeze the basket
   - result prompt and reward commit still work

## Notes

The generated files are concept assets. The asset sheet is suitable for slicing and iteration, but the final in-game sprites should be reviewed at game scale after import because the bush and basket may need tighter crops for collision readability.
