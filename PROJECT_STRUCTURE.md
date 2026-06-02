# Project Structure

This project targets **Godot 4.4 + C# (.NET 8)**.

## Top-Level Layout

- `project.godot`: Godot project configuration and autoload registration.
- `MainMenu.tscn`: Main entry scene (`run/main_scene`).
- `Main.tscn`: Primary gameplay scene.
- `Scenes/`: Scene files grouped by feature (currently `Scenes/UI`).
- `Scripts/`: Runtime C# code split by responsibilities.
- `Data/`: Authored gameplay content resources (`.tres`).
- `Assets/`: Source art assets (SVG + Godot import metadata).
- `tests/`: Isolated .NET test harnesses.

## Scripts Layout

- `Scripts/Autoload`: Global singletons loaded by Godot (`DataDb`, `RuntimeContentDb`, `GameState`, `SaveGameManager`).
- `Scripts/Controllers`: Orchestration/flow logic.
- `Scripts/Systems`: Core domain systems (brewing, effects, requirements).
- `Scripts/Models`: Data model types for runtime content.
- `Scripts/Persistence`: Save/load data contracts.
- `Scripts/Tutorial`: Tutorial domain/presentation flow (`TutorialStateMachine`, `TutorialContentResource`, step/status enums).
- `Scripts/UI`: UI behavior scripts.

## Tutorial Architecture

- `Scripts/Controllers/TutorialController.cs`: Runtime orchestration only (signal wiring + scene integration).
- `Scripts/Tutorial/TutorialStateMachine.cs`: Pure C# transition rules for tutorial progression.
- `Scripts/Tutorial/TutorialContentResource.cs`: Tutorial copy/IDs as resource-backed configuration.
- `Scripts/Tutorial/Presentation/*`: Overlay rendering and interaction gate helpers.

## Source Of Truth vs Generated Artifacts

Keep in version control:

- `*.cs`, `*.tscn`, `*.tres`, `*.svg`
- `*.import` (import settings)
- `*.uid` (Godot UID sidecar files)
- `.sln`, `.csproj`, docs

Do not keep in version control:

- `.godot/`
- `.mono/`
- `bin/`
- `obj/`
- `.vs/`
- temporary caches/logs

## Maintenance

- Use `tools/cleanup-stale-artifacts.ps1` to remove local generated artifacts.
- When moving/renaming resources outside Godot, keep paired `.uid` files in sync.
