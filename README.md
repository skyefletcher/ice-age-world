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
| Shift | Run |
| Space | Jump |
| E | Eat grass or drink water (when the prompt shows) |
| Mouse | Look around |
| Mouse wheel | Zoom camera |
| Esc / left click | Release / recapture mouse |
| F11 | Toggle fullscreen |

## Layout

- `scenes/Main.tscn` — the world: sky, sun, terrain, player and HUD
- `scenes/Player.tscn` — the player animal (a placeholder mammoth built from primitive shapes) and its camera
- `scripts/Terrain.cs` — generates the landscape, lake basins, collision and trees from noise
- `scripts/Water.cs` — lake water surfaces, and where the water is for drinking and swimming
- `scripts/Grassland.cs` — the edible meadow covering the green steppe; eaten grass flattens and regrows
- `scripts/Player.cs` — movement, swimming, eating, drinking, hunger and thirst, camera and animations
- `scripts/Hud.cs` — hunger and thirst bars and the on-screen action prompt
- `scripts/InputSetup.cs` — key bindings
- `scripts/Main.cs` — scene startup and mouse/window handling
