# Occult Shop (menu-driven) starter scaffold — Godot 4 + C#

This is a minimal, data-driven skeleton for a **2D menu-driven occult shop management** game with **nightly horror event cards + escalating rules**.

## Assumptions
- Godot **4.x** with **.NET/C#** enabled.
- You’ll copy these files into your Godot project (recommended) rather than treating this as a complete Godot project.

## Copy into your Godot project
1) Copy the folder contents into your Godot project root:
   - `Data/`
   - `Scripts/`
   - `Main.tscn`

2) In Godot: **Project → Project Settings → Autoload**
   Add these singletons (Path → Name):
   - `res://Scripts/Autoload/DataDb.cs` → `DataDb`
   - `res://Scripts/Autoload/GameState.cs` → `GameState`

3) Open `res://Main.tscn` and press Play.

What you should see: a tiny HUD with Gold/Dread/Day, and an **End Day** button that triggers a **Night Event** modal.

## Where to add content
- Items: `res://Data/items.json`
- Events: `res://Data/events.json`
- Rules: `res://Data/rules.json`

## Next MVP steps (suggested)
- Prep phase UI: move inventory → shelf slots (still menu-driven)
- Open phase: customer queue UI (requests by tag, price sensitivity)
- Rule hooks: `onSale`, `endOfDay`, `startDay`
