# Persistence Boundary

This project now has a runtime save/load system (`SaveGameManager`) that writes separate save files under `user://saves/`.

If save/load is added later, keep the serialized domains separate:

- Authored data: always reload from `res://Data/*.json`
- Runtime-generated item catalog: persist separately from authored data so generated potion definitions survive reloads
- Player state: save independently from authored data and from the runtime catalog

Do not mix generated item definitions into authored JSON files.
