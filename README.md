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
| E | Eat grass (when "Press E to eat grass" shows) |
| Mouse | Look around |
| Mouse wheel | Zoom camera |
| Esc / left click | Release / recapture mouse |
| F11 | Toggle fullscreen |

## Layout

- `scenes/Main.tscn` — the world: sky, sun, terrain, player and HUD
- `scenes/Player.tscn` — the player animal (a placeholder mammoth built from primitive shapes) and its camera
- `scripts/Terrain.cs` — generates the landscape, its collision and trees from noise
- `scripts/Grassland.cs` — edible grass clumps that flatten when eaten and regrow
- `scripts/Player.cs` — movement, camera, walk and eating animations
- `scripts/InputSetup.cs` — key bindings
- `scripts/Main.cs` — scene startup and mouse/window handling
