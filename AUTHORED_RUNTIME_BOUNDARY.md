# Authored vs Runtime Boundary

This project follows a strict data boundary:

- `DataDb` owns only authored, file-backed content loaded from `Data/*.tres`.
- Runtime-generated content must live outside `DataDb`.
- Player progress and runtime state stay in `GameState` or in a dedicated runtime-state object, not in authored data.

## Rule of Use

- Authored data is read from disk (root `authored_data.tres` + split section resources) and treated as immutable at runtime.
- Runtime-generated items, recipes, and other discovered content must be stored separately from authored resource content.
- UI and gameplay systems should resolve runtime content explicitly, then fall back to authored data when needed.

## Purpose

- Prevent authored content from being mutated by gameplay.
- Keep runtime-generated state disposable and easy to reset.
- Make save/load boundaries easier to reason about later.
