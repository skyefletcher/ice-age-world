# ice-age-world

An ice-age animal survival game, inspired by Savannah Life, built with Godot 4 (.NET) and C#.

## Requirements

- [Godot 4.7 .NET edition](https://godotengine.org/download): portable, so just unzip it anywhere (e.g. `~/code/tools/`)
- .NET SDK 10 (`winget install Microsoft.DotNet.SDK.10`)

## Running

Open the editor, import this folder (`project.godot`), and press **F5**, or from the command line:

```
dotnet build
godot --path .
```

## Controls

| Key | Action |
|---|---|
| WASD / arrows | Move |
| Shift | Run (eagle: dive) |
| Space | Jump; eagle: take off, hold to flap and climb; snow leopard: leap out of a tree |
| Walk into a tree | Snow leopard: climb it. W / S climb up or down (all the way to the top), A / D go round the trunk, E steps out onto a branch beside you, then W / S walk along it |
| E | Eat grass or drink water (when the prompt shows) |
| Tab | Switch animal (woolly mammoth, snow leopard, arctic wolf, sea otter or bald eagle) |
| Mouse | Look around |
| Mouse wheel | Zoom camera |
| Esc / left click | Release / recapture mouse |
| F11 | Toggle fullscreen |

## Layout

- `scenes/Main.tscn` — the world: sky, sun, terrain, player and HUD
- `scenes/Player.tscn` — the player animal and its camera
- `scripts/Terrain.cs` — generates the landscape, lake basins, collision and mixed forest patches from noise, and remembers where each tree trunk, branch and treetop is for climbers
- `scripts/TreeBuilder.cs` — builds randomised meshes for five kinds of tree (spruce, Scots pine, golden larch, white birch and narrow fir), so no two trees look alike, remembering which branches are sturdy enough to walk along
- `scripts/Water.cs` — lake water surfaces, and where the water is for drinking and swimming
- `scripts/Animal.cs` — base class for playable animals: their 1–10 scores (jump height and length, land and water speed, agility, stamina) and the speeds, jumps, turning and stamina worked out from them, their abilities (climbing, flying, pack, herd), size, diet and camera, and shared mesh-building helpers
- `scripts/Mammoth.cs` — builds the woolly mammoth in code (body, trunk, tusks, tail, hair) and animates it
- `scripts/SnowLeopard.cs` — builds the snow leopard in code to real proportions (fur patterned with rosettes and spots, ringed tail, cat face) and animates its walk, gallop, drinking crouch and forepaws-first landing when it leaps out of a tree
- `scripts/ArcticWolf.cs` — builds the arctic wolf in code (white winter coat, neck ruff, bushy tail, a big, cute head with large amber eyes) and animates its walk, gallop and drinking
- `scripts/SeaOtter.cs` — builds the sea otter in code (dense brown fur, grizzled head, whiskers, webbed hind flippers, flat tail) and animates its bounding walk, belly-down swimming and floating on its back
- `scripts/BaldEagle.cs` — builds the bald eagle in code (white head and tail, hooked yellow beak, talons, folding feathered wings) and animates its waddle, wingbeats, glide and dive
- `scripts/ProceduralTextures.cs` — generates the fur, rosette-spotted coat and ivory textures with normal maps at startup
- `scripts/MeshBuilder.cs` — small helper for building meshes in code
- `shaders/fur.gdshader` — sways the hair strands
- `icons/` — HUD icons (leaf for hunger, water drop for thirst, lightning bolt for stamina)
- `scripts/Grassland.cs` — the edible meadow covering the green steppe; eaten grass flattens and regrows
- `scripts/Player.cs` — movement, jumping, swimming, eating, drinking, hunger, thirst and stamina, switching animal, and the camera
- `scripts/Player.Flying.cs` — flight for animals that fly: take off, flap to climb, glide, dive, bank and land
- `scripts/Player.Climbing.cs` — tree climbing for animals that climb: up, down and round the trunk, out along branches, up onto the treetop, and leaping off
- `scripts/Herd.cs` — the computer-controlled wolf pack and mammoth herd that follow the player
- `scripts/Hud.cs` — current animal name, hunger, thirst and stamina bars and the on-screen action prompt
- `scripts/InputSetup.cs` — key bindings
- `scripts/Main.cs` — scene startup and mouse/window handling
