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
        var wildlife = GetNode<Wildlife>("Wildlife");
        wildlife.Populate(terrain, water);

        var player = GetNode<Player>("Player");
        player.Terrain = terrain;
        player.Grassland = grassland;
        player.Water = water;
        player.Wildlife = wildlife;
        wildlife.Player = player;
        player.Respawn();

        GetNode<Hud>("Hud").Player = player;

        Input.MouseMode = Input.MouseModeEnum.Captured;
        _terrain = terrain;
        _environment = GetNode<WorldEnvironment>("WorldEnvironment").Environment;
        _daylight = _environment.AmbientLightEnergy;
        _fogLight = _environment.FogLightEnergy;
    }

    private Terrain? _terrain;
    private Environment? _environment;
    private float _daylight;
    private float _fogLight;

    /// <summary>How much of the sky's light still reaches into a cave: the rock overhead shuts out all but a little.</summary>
    private const float CaveGloom = 0.2f;

    /// <summary>
    /// Inside a cave the light from the sky fades away, slowly, as eyes take time to get used to the dark, so the cave is
    /// gloomy within and its mouth glows bright from inside. The sun is already kept out by the shadow of the roof, and the
    /// haze in the air, lit by the sky outside, goes dim in there too.
    /// </summary>
    public override void _Process(double delta)
    {
        if (_terrain is null || _environment is null || GetViewport().GetCamera3D() is not { } camera)
            return;
        float light = _terrain.IsUnderRoof(camera.GlobalPosition) ? CaveGloom : 1f;
        float step = (float)delta;
        _environment.AmbientLightEnergy = Mathf.MoveToward(_environment.AmbientLightEnergy, _daylight * light, _daylight * step);
        _environment.FogLightEnergy = Mathf.MoveToward(_environment.FogLightEnergy, _fogLight * light, _fogLight * step);
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
