using System;
using System.Collections.Generic;
using Godot;

namespace IceAgeWorld;

/// <summary>
/// Playing together. One player hosts a game and the others join it by the host's address, as on a Roblox server. The
/// world is the same for everyone, since the terrain is made from the same seed on every computer, and each player's
/// game tells all the others, twenty times a second, where its animal is and what it's doing, so they see it there as
/// a <see cref="RemotePlayer"/>. Bites between players are sent to the one bitten. The wild animals and the grass
/// are each player's own, and aren't shared.
/// </summary>
public partial class Network : Node
{
    /// <summary>The port games are hosted on; a host on the internet needs it forwarded (UDP) to their computer.</summary>
    public const int Port = 7777;

    /// <summary>The most players one game can hold, besides the host.</summary>
    private const int MaxPlayers = 15;

    /// <summary>How many times a second the player's animal is sent to the others.</summary>
    private const float SendRate = 20f;

    public Player? Player { get; set; }

    /// <summary>The name shown over this player's animal on everyone else's screen.</summary>
    public string PlayerName { get; set; } = "Player";

    /// <summary>True while in a game with other players, whether hosting or joined.</summary>
    public bool IsOnline =>
        Multiplayer.MultiplayerPeer is ENetMultiplayerPeer peer
        && peer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected;

    public bool IsHost => IsOnline && Multiplayer.IsServer();

    /// <summary>Everyone else in the game.</summary>
    public IEnumerable<RemotePlayer> Others => _others.Values;

    /// <summary>Something worth telling the player, e.g. that someone joined.</summary>
    public event Action<string>? Notice;

    /// <summary>The game being joined answered, or couldn't be found.</summary>
    public event Action? Joined;
    public event Action? JoinFailed;

    private readonly Dictionary<long, RemotePlayer> _others = [];
    private float _sendTimer;

    public override void _Ready()
    {
        Multiplayer.PeerConnected += OnPeerConnected;
        Multiplayer.PeerDisconnected += OnPeerDisconnected;
        Multiplayer.ConnectedToServer += () => Joined?.Invoke();
        Multiplayer.ConnectionFailed += () =>
        {
            Multiplayer.MultiplayerPeer = null;
            JoinFailed?.Invoke();
        };
        Multiplayer.ServerDisconnected += () =>
        {
            Leave();
            Notice?.Invoke("The host left the game. You're playing alone now.");
        };
    }

    /// <summary>Starts a game for others to join; returns false if the port is already in use.</summary>
    public bool Host()
    {
        var peer = new ENetMultiplayerPeer();
        if (peer.CreateServer(Port, MaxPlayers) != Error.Ok)
            return false;
        Multiplayer.MultiplayerPeer = peer;
        return true;
    }

    /// <summary>Starts looking for the game hosted at <paramref name="address"/>; <see cref="Joined"/> or <see cref="JoinFailed"/> follows.</summary>
    public bool Join(string address)
    {
        var peer = new ENetMultiplayerPeer();
        if (peer.CreateClient(address, Port) != Error.Ok)
            return false;
        Multiplayer.MultiplayerPeer = peer;
        return true;
    }

    /// <summary>Leaves the game, and everyone else's animals with it.</summary>
    public void Leave()
    {
        Multiplayer.MultiplayerPeer = null;
        foreach (var other in _others.Values)
            other.QueueFree();
        _others.Clear();
    }

    /// <summary>
    /// Another player's animal (alive) within <paramref name="reach"/> of <paramref name="mouth"/>, the nearest if
    /// there are several, or null.
    /// </summary>
    public RemotePlayer? RivalNear(Vector3 mouth, float reach)
    {
        RemotePlayer? nearest = null;
        float best = float.MaxValue;
        foreach (var other in _others.Values)
        {
            if (other.Animal is null || other.Health <= 0f)
                continue;
            float gap = mouth.DistanceTo(other.GlobalPosition + Vector3.Up * other.BodyRadius) - other.BodyRadius;
            if (gap < reach && gap < best)
            {
                best = gap;
                nearest = other;
            }
        }
        return nearest;
    }

    /// <summary>Bites another player's animal, taking <paramref name="damage"/> before its size and toughness.</summary>
    public void Bite(RemotePlayer rival, float damage) => RpcId(rival.PeerId, MethodName.Bitten, damage);

    public override void _PhysicsProcess(double delta)
    {
        if (!IsOnline || Player is null || !Player.CanProcess())
            return;
        _sendTimer -= (float)delta;
        if (_sendTimer > 0f)
            return;
        _sendTimer = 1f / SendRate;
        var animal = Player.Animal;
        Rpc(MethodName.State, Player.AnimalIndex, Player.GlobalPosition, animal.Position, animal.Rotation, RemotePlayer.Pack(Player));
    }

    /// <summary>Someone new has joined: tell them who this is. Everyone in the game hears of a newcomer, through the host.</summary>
    private void OnPeerConnected(long id) => RpcId(id, MethodName.Introduce, PlayerName);

    private void OnPeerDisconnected(long id)
    {
        if (!_others.Remove(id, out var other))
            return;
        Notice?.Invoke($"{other.PlayerName} left the game");
        other.QueueFree();
    }

    /// <summary>A player says who they are.</summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Introduce(string name)
    {
        // Keep names short enough to fit over an animal's head.
        name = name.StripEdges();
        if (name.Length > 20)
            name = name[..20];
        var other = Other(Multiplayer.GetRemoteSenderId());
        other.PlayerName = name.Length > 0 ? name : "Player";
        Notice?.Invoke($"{other.PlayerName} joined the game");
    }

    /// <summary>A player's animal, as it is now.</summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    private void State(int kind, Vector3 position, Vector3 offset, Vector3 rotation, float[] pose) =>
        Other(Multiplayer.GetRemoteSenderId()).Receive(kind, position, offset, rotation, pose);

    /// <summary>Another player has bitten this one.</summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Bitten(float damage)
    {
        if (Player is null || !Player.CanProcess() || !_others.ContainsKey(Multiplayer.GetRemoteSenderId()))
            return;
        Player.Bitten(Mathf.Clamp(damage, 0f, 200f));
    }

    /// <summary>The animal of the player on connection <paramref name="id"/>, made the first time they're heard from.</summary>
    private RemotePlayer Other(long id)
    {
        if (_others.TryGetValue(id, out var other))
            return other;
        other = new RemotePlayer { PeerId = id, Name = $"Player{id}" };
        _others[id] = other;
        // In the world rather than under this node, so they aren't tangled up with the network's own node path.
        GetParent().AddChild(other);
        return other;
    }
}
