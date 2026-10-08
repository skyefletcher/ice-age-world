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
| E | Eat grass or drink water; snow leopard: eat from a kill, on the ground or up a tree (when the prompt shows) |
| F / left click | Snow leopard: pounce on a wild animal close in front and bite it; beside a kill, pick it up to carry (even up a tree); press again to put it down (when the prompt shows) |
| C | Sit down; press again (or move) to get up |
| X | Lie down; press again (or move) to get up. Resting gets your breath back faster |
| Tab | Switch animal (woolly mammoth, snow leopard, arctic wolf, sea otter or bald eagle) |
| Mouse | Look around |
| Mouse wheel | Zoom camera |
| Esc / left click | Release / recapture mouse |
| F11 | Toggle fullscreen |

## Layout

- `scenes/Main.tscn` — the world: sky, sun, terrain, wildlife, player and HUD
- `scenes/Player.tscn` — the player animal and its camera
- `scripts/Terrain.cs` — generates the landscape, lake basins, collision and mixed forest patches from noise, and remembers where each tree trunk, branch and treetop is for climbers
- `scripts/TreeBuilder.cs` — builds randomised meshes for five kinds of tree (spruce, Scots pine, golden larch, white birch and narrow fir), so no two trees look alike, remembering which branches are sturdy enough to walk along
- `scripts/Water.cs` — lake water surfaces, and where the water is for drinking and swimming
- `scripts/Animal.cs` — base class for playable animals: their 1–10 scores (jump height and length, land and water speed, agility, stamina) and the speeds, jumps, turning and stamina worked out from them, their abilities (climbing, flying, pack, herd), size, diet and camera, how far they are sitting, lying or gone limp in death, and shared mesh-building and posing helpers
- `scripts/Mammoth.cs` — builds the woolly mammoth in code (body, trunk, tusks, tail, hair) and animates it, sitting back on its rump or lying on its side as elephants sleep
- `scripts/SnowLeopard.cs` — builds the snow leopard in code to real proportions (fur patterned with rosettes and spots, ringed tail, cat face) and animates its walk, gallop, drinking crouch, sitting and sphinx-like lying, and forepaws-first landing when it leaps out of a tree
- `scripts/ArcticWolf.cs` — builds the arctic wolf in code (white winter coat, neck ruff, bushy tail, a big, cute head with large amber eyes) and animates its walk, gallop, drinking, sitting and lying
- `scripts/SeaOtter.cs` — builds the sea otter in code (dense brown fur, grizzled head, whiskers, webbed hind flippers, flat tail) and animates its bounding walk, belly-down swimming, floating on its back, sitting up on its haunches and lying on its back on land
- `scripts/BaldEagle.cs` — builds the bald eagle in code (white head and tail, hooked yellow beak, talons, folding feathered wings) and animates its waddle, wingbeats, glide, dive, settling down on its legs and resting on its breast
- `scripts/ProceduralTextures.cs` — generates the fur, rosette-spotted coat and ivory textures with normal maps at startup
- `scripts/MeshBuilder.cs` — small helper for building meshes in code
- `shaders/fur.gdshader` — sways the hair strands
- `icons/` — HUD icons (leaf for hunger, water drop for thirst, lightning bolt for stamina)
- `scripts/Grassland.cs` — the edible meadow covering the green steppe; eaten grass flattens and regrows
- `scripts/Player.cs` — movement, jumping, swimming, eating, drinking, sitting and lying down, hunger, thirst and stamina, switching animal, and the camera
- `scripts/Player.Flying.cs` — flight for animals that fly: take off, flap to climb, glide, dive, bank and land
- `scripts/Player.Climbing.cs` — tree climbing for animals that climb: up, down and round the trunk, out along branches, up onto the treetop, and leaping off
- `scripts/Player.Hunting.cs` — hunting for animals that hunt: pouncing on wild animals, eating from kills, and carrying a kill in the jaws, up a tree if need be, to put it down and eat it there
- `scripts/Herd.cs` — the computer-controlled wolf pack and mammoth herd that follow the player, and sit or lie down when it does
- `scripts/Wildlife.cs` — wild mammoth herds, wolf packs and sea otter rafts that live in the world on their own: the mammoths graze and stampede from hunters, the wolves hunt them down, feed and sleep it off, the otters float about the lakes and haul out on the shore to rest, every animal running and tiring by its own scores, and anything that falls (to the wolves or to a snow leopard player) goes limp and is reborn as a youngster
- `scripts/Hud.cs` — current animal name, hunger, thirst and stamina bars and the on-screen action prompt
- `scripts/InputSetup.cs` — key bindings
- `scripts/Main.cs` — scene startup and mouse/window handling
