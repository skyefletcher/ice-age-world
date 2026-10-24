# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

An ice-age animal survival game (inspired by Savannah Life) in Godot 4.7 .NET with C# (`net10.0`, nullable enabled, namespace `IceAgeWorld`). The player plays one of thirteen animals (woolly mammoth, snow leopard, arctic wolf, sea otter, bald eagle, reindeer, moose, polar bear, snowy owl, cave lion, smilodon, woolly rhino and cave hyena), born young and growing up, male or female, and roams procedurally generated terrain with wild herds, packs and predators, an ocean and lakes. It eats (grass, kills or shellfish, by animal) and drinks to keep hunger and thirst up. Some animals are free and the rest are bought in the animal shop (B) with coins earned from chores. Tab switches between the animals the player owns. Friends can join over the network from the start menu.

## Commands

```
dotnet build          # compile the C# assembly; Godot runs the last build
godot --path .        # run the game (main scene: scenes/Main.tscn)
```

On this machine neither tool is on PATH: use `"C:\Program Files\dotnet\dotnet.exe"` and `~/code/tools/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe`. There are no tests or linters; `dotnet build` with 0 warnings is the check. To catch runtime errors too, run the game headless for a few seconds: `godot --headless --path . --quit-after 900` and look for exceptions. New `.svg` icons need `godot --headless --path . --import` once before they load. Git has no identity configured here; commit with `git -c user.name="Skye Fletcher" -c user.email="skye.fletcher@skyefletcher.com" commit ...`, as the existing history does.

### Seeing a model change

Everything visual is built in code, so check appearance by rendering it. Add a throwaway scene and script (e.g. `zz_shot/FaceShot.tscn` + `.cs`), `dotnet build`, then run `godot --path . --resolution 900x700 res://zz_shot/FaceShot.tscn -- <outdir>`. The script adds a `WorldEnvironment`, a `DirectionalLight3D`, the animal (`new SnowLeopard()`), and a `Camera3D`. For each shot it waits a few `RenderingServer.FramePostDraw` frames, then saves `GetViewport().GetTexture().GetImage().SavePng(...)` using the output dir from `OS.GetCmdlineUserArgs()`, and quits. Find body parts by node path (e.g. `Frame/Neck/Head`). Delete the scene and script afterwards. They must live inside the project so they compile into the assembly.

To see the HUD or the world, instantiate `res://scenes/Main.tscn` in the throwaway script instead, wait ~30 frames, hide `Hud/StartMenu`, and drive the `Player` node (e.g. `SwitchTo(index)`). Generating the world takes a couple of minutes, so run such a shot in the background.

## Architecture

**Scene wiring.** `Main.tscn` holds Terrain, Grassland, Water, Wildlife, Player and Hud nodes. `Main._Ready` registers key bindings in code (`InputSetup.Register()`; there are none in `project.godot`). It then wires the systems together explicitly. Terrain generates in its own `_Ready`. Then come `Grassland.Populate(terrain)`, `Water.Build(terrain)`, `Wildlife.Populate(terrain, water)`, and the code-created `Seabed` (under the ocean: reefs, kelp, cod) and `Underwater` (under the small lakes). Player gets references to all of them and `Respawn()`s. Last, Main creates the `Network`, `AnimalShop` and `StartMenu`. The game opens on the start menu with the Player disabled until an animal is chosen. Terrain is the shared source of truth: heights, `Lakes` (index 1 is the ocean, centred on the world's north-east corner, so use `Lake.Middle`/`Reach` for where its open water is), steepness, grass amount and distance from water.

**Player vs. Animal.** `Player` (a `CharacterBody3D`) owns all gameplay: movement, swimming, eating/drinking, hunger/thirst, and the orbit camera. It is split into partial files by behaviour (`Player.Diving.cs`, `Player.Hunting.cs`, `Player.Chores.cs` …). It builds every `Animal` up front as hidden children and shows only the current one. On a switch it resizes the collision capsule from the animal's `AnimalStats`. Each frame it calls `Animal.Animate(speed, stride, eat, dt)` and the animal poses itself. To add an animal: subclass `Animal`, supply `DisplayName`, `YoungName` and `Stats`, build the model in `_Ready`, and implement `Animate`. Then append it to `Player.Kinds` (always at the end: an animal's place in that list is its id over the network) and give it a price in `Wallet.PriceOf`. The HUD's food icon follows from its stats (`CanGraze` → leaf, `Ability.Dive` without `Ability.Hunt` → clam, otherwise meat), and a diver needs `Ability.Dive` and a `BreathSeconds`. A big four-legged animal can subclass `Quadruped` instead, which supplies the legs, gaits, drinking, sitting, lying and death pose: give it proportions, build the body, head, feet and tail in `Build`, and move its ears and tail in `AnimateExtras`. A bird of prey can subclass `Bird`, which supplies the flying skeleton and its animation (wingbeats, glide, dive, clutching prey): give it proportions and build the pivots in `Build`.

**Animals are built entirely in code, facing -Z.** There are no imported meshes. Models are trees of `Node3D` pivots (via `Pivot`), so joints can rotate in `Animate`. Meshes hang off the pivots via `Attach`. The shared helpers live in `Animal.cs`:
- `Ellipsoid`: a sphere pushed through a shape function, with optional per-vertex `colouring` for materials with `VertexColorUseAsAlbedo`.
- `Tube`: a swept tube with varying radii.
- `Strands`: hair cards, using the `fur.gdshader` material from `HairMaterial()`.
- `OnShape`, `OnSegment` and `OnTube`: scatter hair roots over a surface.

Hair takes the colour of the hide beneath it: `ProceduralTextures.Colouring(material)` samples a triplanar material's albedo the way its shader would, so fur carries the coat pattern. `ProceduralTextures` generates all textures (fur, rosette coat, ivory) at startup.

**Snow leopard conventions.** Proportions are tuned through scale constants at the top of `SnowLeopard.cs`: `HeadScale`, `MuzzleScale`, `EyeScale` and `EarScale` scale whole sub-trees. Change them rather than every dimension. Leg lengths (`FrontUpperLength` etc.) drive both hip height and the fold maths in `Animate` (`FoldAngle`), which keeps paws planted while crouching to drink. Changing leg lengths therefore affects ground contact and animation, not just looks.

**Conventions.** Code comments explain the real-animal or design reason behind shapes and numbers. Keep that style and keep the README's Layout section current when adding files. Recent commit messages describe the visible change in plain words ("Give the snow leopard …").
