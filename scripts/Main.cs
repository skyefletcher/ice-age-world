using Godot;

namespace IceAgeWorld;

/// <summary>Root of the game scene: wires up input, lighting and spawns the player on the terrain.</summary>
public partial class Main : Node3D
{
    public override void _Ready()
    {
        InputSetup.Register();

        // Low winter sun, angled so the terrain gets long shadows.
        _sun = GetNode<DirectionalLight3D>("Sun");
        _sun.RotationDegrees = new Vector3(-40, -35, 0);
        _sunlight = _sun.LightEnergy;

        // Child nodes are ready before their parent, so the terrain has already been generated here.
        var terrain = GetNode<Terrain>("Terrain");
        var grassland = GetNode<Grassland>("Grassland");
        grassland.Populate(terrain);
        var water = GetNode<Water>("Water");
        water.Build(terrain);
        var wildlife = GetNode<Wildlife>("Wildlife");
        wildlife.Populate(terrain, water);
        var underwater = new Underwater { Name = "Underwater" };
        AddChild(underwater);
        underwater.Build(terrain);

        var player = GetNode<Player>("Player");
        player.Terrain = terrain;
        player.Grassland = grassland;
        player.Water = water;
        player.Wildlife = wildlife;
        player.Underwater = underwater;
        underwater.Diver = player;
        wildlife.Player = player;
        player.Respawn();

        var hud = GetNode<Hud>("Hud");
        hud.Player = player;

        // Playing with friends: the network sends this player's animal to the others and shows theirs here.
        var network = new Network { Name = "Network", Player = player };
        AddChild(network);
        player.Network = network;
        network.Notice += message => hud.Notify(message);
        player.ChoreDone += chore => hud.Notify($"Chore done: {chore.Task}!  +{chore.Reward} coins");

        var shop = new AnimalShop { Name = "AnimalShop", Player = player };
        hud.AddChild(shop);
        hud.Shop = shop;

        // The game opens on the start menu, with the world going on behind it and the player held still until it has
        // chosen how to play and then which animal to be.
        player.ProcessMode = ProcessModeEnum.Disabled;
        var menu = new StartMenu { Name = "StartMenu", Network = network };
        hud.AddChild(menu);
        menu.Started += () =>
        {
            if (menu.Notice is { } notice)
                hud.Notify(notice, 15f);
            shop.Open("Choose your animal", "Free animals are ready to play. Do the chores in the top right corner to earn coins for the others.", canClose: false, index =>
            {
                player.SwitchTo(index);
                player.ProcessMode = ProcessModeEnum.Inherit;
            });
        };
        menu.StartFrom(OS.GetCmdlineUserArgs());

        Input.MouseMode = Input.MouseModeEnum.Visible;
        _terrain = terrain;
        _water = water;
        _environment = GetNode<WorldEnvironment>("WorldEnvironment").Environment;
        _daylight = _environment.AmbientLightEnergy;
        _fogLight = _environment.FogLightEnergy;
        _fogColour = _environment.FogLightColor;
        _fogDensity = _environment.FogDensity;
    }

    private Terrain? _terrain;
    private Environment? _environment;
    private float _daylight;
    private float _fogLight;
    private Water? _water;
    private DirectionalLight3D _sun = null!;
    private float _sunlight;
    private Color _fogColour;
    private float _fogDensity;

    /// <summary>
    /// Under the water the view closes in to a few tens of metres of blue-green murk, as in a real cold lake, and grows
    /// darker the deeper the camera goes, as the water above soaks up the daylight: on the ocean floor about
    /// a third of it is left.
    /// </summary>
    private static readonly Color UnderwaterColour = new(0.1f, 0.32f, 0.36f);
    private const float UnderwaterDensity = 0.05f;
    private const float DarkestDepth = 40f;

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

        // Plunging under the surface changes the view at once, and the light dims with depth.
        float depth = _water?.SurfaceAt(camera.GlobalPosition) is { } surface ? surface - camera.GlobalPosition.Y : 0f;
        bool underwater = depth > 0f;
        _environment.FogLightColor = underwater ? UnderwaterColour : _fogColour;
        _environment.FogDensity = underwater ? UnderwaterDensity : _fogDensity;
        if (underwater)
            light *= Mathf.Lerp(1f, 0.35f, Mathf.Clamp(depth / DarkestDepth, 0f, 1f));
        // The water soaks up the sunshine too, so the bed isn't lit like dry land: half is gone just under the surface.
        _sun.LightEnergy = _sunlight * (underwater ? Mathf.Lerp(0.5f, 0.15f, Mathf.Clamp(depth / DarkestDepth, 0f, 1f)) : 1f);

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
