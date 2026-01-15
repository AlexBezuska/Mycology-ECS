## Unity Onboarding (Quick Start)

1. Copy the `game_specific_ecs` folder into your Unity project's `Assets`.
2. Add the `Myco_Unity_Entities` component to an empty GameObject at the root of your scene.
3. In the inspector, set the folder paths for entities/components if needed (default: `Mycology_ECS/entities`, `Mycology_ECS/components`).
4. On play, entities with `create_on_start: true` will auto-spawn.
5. Customize entities/components by editing the JSON files.

That's it! No code changes required in the submodule.
# MycologyECS

MycologyECS by **fufroom**.

MycologyECS is a small JSON-driven ECS-style data layer with **engine translation layers** that turn entities/components into engine objects.

- Unity translation layer: in development now
- Godot translation layer: coming soon

## Concept (current Unity workflow)

- `components/*.json` defines reusable component data
- `entities/<SceneName>.json` defines entities for a Unity scene
- The Unity translation layer loads the scene JSON, spawns generic GameObjects, then applies engine components based on the ECS component types

Notes:

- `RenderLayer: ui` → parent under one shared `MycoCanvas`
- Optional pooling exists for repeated spawns
- A `MycoEntities` debug object can list spawned ids → GameObjects in the Inspector
