using System;
using Godot;

namespace IceAgeWorld;

/// <summary>
/// The first thing on screen: a name to go by, and whether to play alone, host a game for friends, or join one by the
/// host's address. The world carries on behind it. Once a game is under way, <see cref="Started"/> fires and the
/// animal shop comes up to pick an animal.
/// </summary>
public partial class StartMenu : ColorRect
{
    public Network? Network { get; set; }

    /// <summary>The player has started playing, alone or with others.</summary>
    public event Action? Started;

    private const string SavePath = "user://player.cfg";

    private LineEdit _name = null!;
    private LineEdit _address = null!;
    private Label _status = null!;
    private Button[] _buttons = [];

    public override void _Ready()
    {
        Color = new Color(0f, 0f, 0f, 0.55f);
        SetAnchorsPreset(LayoutPreset.FullRect);

        var centre = new CenterContainer();
        centre.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(centre);

        var column = new VBoxContainer { CustomMinimumSize = new Vector2(460, 0) };
        column.AddThemeConstantOverride("separation", 14);
        centre.AddChild(column);

        column.AddChild(Hud.Text("Ice Age World", 52));

        // The name and address are remembered for next time, so a regular group can rejoin in one click.
        var saved = new ConfigFile();
        saved.Load(SavePath);
        column.AddChild(Hud.Text("Your name", 20));
        _name = Field((string)saved.GetValue("player", "name", ""), "Type your name");
        _name.MaxLength = 20;
        column.AddChild(_name);

        var alone = MenuButton("Play alone");
        alone.Pressed += () => Begin();
        column.AddChild(alone);

        var host = MenuButton("Host a game for friends");
        host.Pressed += Host;
        column.AddChild(host);

        column.AddChild(Hud.Text("Join a friend's game: type their address", 20));
        _address = Field((string)saved.GetValue("player", "address", "127.0.0.1"), "e.g. 192.168.1.20");
        column.AddChild(_address);
        var join = MenuButton("Join game");
        join.Pressed += Join;
        column.AddChild(join);

        _status = Hud.Text("", 20);
        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        column.AddChild(_status);
        _buttons = [alone, host, join];

        Network!.Joined += () => Begin($"Joined the game at {_address.Text}");
        Network.JoinFailed += () => Waiting(false, $"Couldn't find a game at {_address.Text}. Check the address, and that your friend is hosting.");
    }

    /// <summary>Starts straight away without the menu, from the command line: <c>-- --host</c> or <c>-- --join ADDRESS</c>, and <c>--name NAME</c>.</summary>
    public void StartFrom(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            string? next = i + 1 < args.Length ? args[i + 1] : null;
            if (args[i] == "--name" && next is not null)
                _name.Text = next;
            else if (args[i] == "--join" && next is not null)
                _address.Text = next;
        }
        if (Array.IndexOf(args, "--host") >= 0)
            Host();
        else if (Array.IndexOf(args, "--join") >= 0)
            Join();
    }

    private void Host()
    {
        Network!.PlayerName = PlayerName;
        if (!Network.Host())
        {
            _status.Text = $"Couldn't host: is a game already running on this computer? (port {Network.Port})";
            return;
        }
        // Friends on the same network join by this computer's address; from elsewhere they need its internet address
        // and the port forwarded to it.
        string addresses = string.Join("  or  ", Array.FindAll(IP.GetLocalAddresses(), a => a.StartsWith("192.168.") || a.StartsWith("10.")));
        Begin(addresses.Length > 0 ? $"Hosting! Friends join with:  {addresses}" : "Hosting!");
    }

    private void Join()
    {
        Network!.PlayerName = PlayerName;
        string address = _address.Text.StripEdges();
        if (address.Length == 0 || !Network.Join(address))
        {
            _status.Text = "That address doesn't look right.";
            return;
        }
        Waiting(true, $"Looking for a game at {address}…");
    }

    /// <summary>Greys out the buttons while looking for a game to join, or brings them back with what went wrong.</summary>
    private void Waiting(bool waiting, string status)
    {
        foreach (var button in _buttons)
            button.Disabled = waiting;
        _status.Text = status;
    }

    private void Begin(string? notice = null)
    {
        var saved = new ConfigFile();
        saved.SetValue("player", "name", _name.Text.StripEdges());
        saved.SetValue("player", "address", _address.Text.StripEdges());
        saved.Save(SavePath);

        Visible = false;
        Notice = notice;
        Started?.Invoke();
    }

    /// <summary>What to tell the player once the game has started, e.g. the address friends can join on.</summary>
    public string? Notice { get; private set; }

    private string PlayerName => _name.Text.StripEdges() is { Length: > 0 } name ? name : "Player";

    private static Button MenuButton(string text)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(0, 52) };
        button.AddThemeFontSizeOverride("font_size", 22);
        return button;
    }

    private static LineEdit Field(string text, string placeholder)
    {
        var field = new LineEdit { Text = text, PlaceholderText = placeholder, CustomMinimumSize = new Vector2(0, 44) };
        field.AddThemeFontSizeOverride("font_size", 20);
        return field;
    }
}
