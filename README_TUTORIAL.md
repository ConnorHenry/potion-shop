# Tutorial Maintenance Guide

This guide explains how to:

- add a new tutorial step
- modify existing tutorial flow
- change tutorial overlay visuals/behavior

The project targets Godot 4.4 + C#.

## Files To Know

- `Scripts/Controllers/TutorialController.cs`
  - Scene orchestration and target selection for each step.
- `Scripts/Tutorial/TutorialStepId.cs`
  - Stable numeric IDs for tutorial steps.
- `Scripts/Tutorial/TutorialStateMachine.cs`
  - Pure transition rules (what event advances to what step).
- `Scripts/Tutorial/TutorialContentResource.cs`
  - Tutorial copy and IDs (item/customer IDs, per-step title/body/button config).
- `Scripts/Tutorial/TutorialStepContentResource.cs`
  - Per-step content model, including optional button-lock behavior.
- `Scripts/Tutorial/Presentation/TutorialOverlayPresenter.cs`
  - How content is sent to the overlay.
- `Scripts/Tutorial/Presentation/TutorialInteractionGate.cs`
  - Step-specific button-lock behavior.
- `Scripts/UI/TutorialOverlay.cs`
  - Overlay drawing/highlight logic.
- `Scenes/UI/TutorialOverlay.tscn`
  - Overlay node tree and styling.
- `tests/PotionBrewingService.Tests/TutorialTests.cs`
  - Source-contract tests for tutorial wiring/architecture.

## Add A New Tutorial Step

1. Add a step enum value in `Scripts/Tutorial/TutorialStepId.cs`.
2. Add default copy in `BuildDefaultSteps()` in `Scripts/Tutorial/TutorialContentResource.cs`.
3. Add or update transition logic in `Scripts/Tutorial/TutorialStateMachine.cs`.
   - Reuse existing evaluators when possible (`EvaluateNextPressed`, `EvaluateIngredientQueued`, etc.).
   - Add a new evaluator only when the event type is genuinely new.
4. Update `ShowStep(...)` in `Scripts/Controllers/TutorialController.cs`.
   - Choose where to point the overlay (`ShowForTarget`, `ShowForTargets`, or message-only).
   - Apply any special behavior (forced customer, inventory focus, etc.).
5. If needed, update timer/input lock behavior:
   - `UpdateShopTimerPause(...)`
   - `UpdateTutorialButtonLock(...)`
6. Update the relevant grouped test file in `tests/PotionBrewingService.Tests/` to cover the new wiring/contract.
7. Validate:
   - `dotnet build`
   - `dotnet run --project .\tests\PotionBrewingService.Tests\PotionBrewingService.Tests.csproj`

## Important Step-ID Rule

Do not reorder existing enum IDs in `TutorialStepId`.

- Appending new steps is safe.
- Reordering/renumbering can break in-progress saves that persist step index.

## Change Tutorial Text Or IDs

Edit `Scripts/Tutorial/TutorialContentResource.cs`.

- Step copy: `BuildDefaultSteps()`.
- Runtime IDs used by flow logic:
  - `GraveMintId`
  - `ObsidianResinId`
  - `IronLullabyRootId`
  - `BlackIchorId`
  - `TutorialPotionId`
  - `TutorialCustomerId`
  - `AmbiguousTutorialCustomerId`
- Post-sale feedback copy:
  - `BuildSaleResultBody(bool saleSucceeded)`

If you change IDs, make sure related authored data still uses those IDs.

## Change Overlay Behavior

### Layout/Theme/Node Structure

Edit `Scenes/UI/TutorialOverlay.tscn`.

- Panel sizing and anchors
- Highlight style
- Button arrangement
- Theme references

Keep exported node paths in sync with scene nodes:

- `DimPath`
- `HighlightPath`
- `PanelPath`
- `TitleLabelPath`
- `BodyLabelPath`
- `NextButtonPath`
- `SkipButtonPath`

### Rendering/Highlight Logic

Edit `Scripts/UI/TutorialOverlay.cs`.

- Use `ShowForTarget(...)` for one highlight target.
- Use `ShowForTargets(...)` for multiple targets.
- Use `ShowMessageWithoutDim(...)` for instruction-only steps.

The overlay now uses dynamic cutouts only. Do not reintroduce legacy `DimTop/Bottom/Left/Right` nodes unless you intentionally revert the dimming strategy in both code and scene.

### Presentation Policy

Edit `Scripts/Tutorial/Presentation/TutorialOverlayPresenter.cs` if you need to change how step content maps to overlay behavior (button visibility, panel position, dim/no-dim policy).

### Button Locking

To disable all other buttons during a step, set `LockOtherButtons = true` on the step content and have `TutorialController` pass through the allowed button or buttons for that step.

The controller uses `TutorialInteractionGate` to disable buttons under the configured scene roots while leaving the passed-through buttons enabled.

## Add A New "Manual Next" Step Example

If you want a new manual step after `Status`:

1. Add enum value, for example `StatusDetails`.
2. Add content entry (`ShowNextButton = true`).
3. In `EvaluateNextPressed`, change:
   - `Status -> StatusDetails`
   - `StatusDetails -> OpenBrewPanel`
4. Add `case TutorialStepId.StatusDetails` in `ShowStep(...)`.
5. Run build/tests.

## Troubleshooting

- Tutorial never starts:
  - Check `GameState.TutorialRequested` initialization path (`SaveGameManager.StartNewGame(bool)`).
- Overlay shows but does not advance:
  - Verify transition exists in `TutorialStateMachine`.
  - Verify the expected event is wired in `TutorialController`.
- Wrong highlight target:
  - Verify target node lookup and `ShowStep(...)` case wiring.
- Failing source-contract tests:
  - Update relevant assertions in the grouped test files under `tests/PotionBrewingService.Tests/` when intended architecture changes.
