using Godot;

namespace IceAgeWorld;

/// <summary>Root of the game scene: wires up input, lighting and spawns the player on the terrain.</summary>
public partial class Main : Node3D
{
    public override void _Ready()
    {
        InputSetup.Register();

        // Low winter sun, angled so the terrain gets long shadows.
        GetNode<DirectionalLight3D>("Sun").RotationDegrees = new Vector3(-40, -35, 0);

        // Child nodes are ready before their parent, so the terrain has already been generated here.
        var terrain = GetNode<Terrain>("Terrain");
        var grassland = GetNode<Grassland>("Grassland");
        grassland.Populate(terrain);
        var water = GetNode<Water>("Water");
        water.Build(terrain);

        var player = GetNode<Player>("Player");
        player.Terrain = terrain;
        player.Grassland = grassland;
        player.Water = water;
        player.Respawn();

        GetNode<Hud>("Hud").Player = player;

        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel"))
        {
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
        else if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }
                 && Input.MouseMode == Input.MouseModeEnum.Visible)
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
        else if (@event is InputEventKey { Pressed: true, PhysicalKeycode: Key.F11 })
        {
            var mode = DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Fullscreen
                ? DisplayServer.WindowMode.Windowed
                : DisplayServer.WindowMode.Fullscreen;
            DisplayServer.WindowSetMode(mode);
        }
    }
}
