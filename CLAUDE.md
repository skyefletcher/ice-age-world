# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

An ice-age animal survival game (inspired by Savannah Life) in Godot 4.7 .NET with C# (`net10.0`, nullable enabled, namespace `IceAgeWorld`). The player switches (Tab) between a woolly mammoth and a snow leopard, and roams procedurally generated terrain, eating grass and drinking from lakes to keep hunger and thirst up.

## Commands

```
dotnet build          # compile the C# assembly; Godot runs the last build
godot --path .        # run the game (main scene: scenes/Main.tscn)
```

On this machine neither tool is on PATH: use `"C:\Program Files\dotnet\dotnet.exe"` and `~/code/tools/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe`. There are no tests or linters; `dotnet build` with 0 warnings is the check.

### Seeing a model change

Everything visual is built in code, so check appearance by rendering it. Add a throwaway scene and script (e.g. `zz_shot/FaceShot.tscn` + `.cs`), `dotnet build`, then run `godot --path . --resolution 900x700 res://zz_shot/FaceShot.tscn -- <outdir>`. The script adds a `WorldEnvironment`, a `DirectionalLight3D`, the animal (`new SnowLeopard()`), and a `Camera3D`. For each shot it waits a few `RenderingServer.FramePostDraw` frames, then saves `GetViewport().GetTexture().GetImage().SavePng(...)` using the output dir from `OS.GetCmdlineUserArgs()`, and quits. Find body parts by node path (e.g. `Frame/Neck/Head`). Delete the scene and script afterwards. They must live inside the project so they compile into the assembly.

## Architecture

**Scene wiring.** `Main.tscn` holds Terrain, Grassland, Water, Player and Hud nodes. `Main._Ready` registers key bindings in code (`InputSetup.Register()`; there are none in `project.godot`). It then wires the systems together explicitly: Terrain generates in its own `_Ready`, then `Grassland.Populate(terrain)`, `Water.Build(terrain)`, and finally Player gets references to all three and `Respawn()`s. Terrain is the shared source of truth: heights, `Lakes`, steepness, grass amount and distance from water.

**Player vs. Animal.** `Player` (a `CharacterBody3D`) owns all gameplay: movement, swimming, eating/drinking, hunger/thirst, and the orbit camera. It builds every `Animal` up front as hidden children and shows only the current one. On a switch it resizes the collision capsule from the animal's `AnimalStats`. Each frame it calls `Animal.Animate(speed, stride, eat, dt)` and the animal poses itself. To add an animal: subclass `Animal`, supply `DisplayName` and `Stats`, build the model in `_Ready`, implement `Animate`, and add it to the `_animals` array in `Player._Ready`.

**Animals are built entirely in code, facing -Z.** There are no imported meshes. Models are trees of `Node3D` pivots (via `Pivot`), so joints can rotate in `Animate`. Meshes hang off the pivots via `Attach`. The shared helpers live in `Animal.cs`:
- `Ellipsoid`: a sphere pushed through a shape function, with optional per-vertex `colouring` for materials with `VertexColorUseAsAlbedo`.
- `Tube`: a swept tube with varying radii.
- `Strands`: hair cards, using the `fur.gdshader` material from `HairMaterial()`.
- `OnShape`, `OnSegment` and `OnTube`: scatter hair roots over a surface.

Hair takes the colour of the hide beneath it: `ProceduralTextures.Colouring(material)` samples a triplanar material's albedo the way its shader would, so fur carries the coat pattern. `ProceduralTextures` generates all textures (fur, rosette coat, ivory) at startup.

**Snow leopard conventions.** Proportions are tuned through scale constants at the top of `SnowLeopard.cs`: `HeadScale`, `MuzzleScale`, `EyeScale` and `EarScale` scale whole sub-trees. Change them rather than every dimension. Leg lengths (`FrontUpperLength` etc.) drive both hip height and the fold maths in `Animate` (`FoldAngle`), which keeps paws planted while crouching to drink. Changing leg lengths therefore affects ground contact and animation, not just looks.

**Conventions.** Code comments explain the real-animal or design reason behind shapes and numbers. Keep that style and keep the README's Layout section current when adding files. Recent commit messages describe the visible change in plain words ("Give the snow leopard …").
