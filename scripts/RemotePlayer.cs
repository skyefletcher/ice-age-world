using Godot;

namespace IceAgeWorld;

/// <summary>
/// Another player's animal, as seen on this computer. It does nothing by itself: twenty times a second that player's
/// game sends where their animal is, which way it faces, how grown it is and what it's doing (see
/// <see cref="Network"/>), and this copies it, gliding smoothly between updates, with the player's name and a health
/// bar over its head.
/// </summary>
public partial class RemotePlayer : Node3D
{
    /// <summary>The other player's connection, which bites are sent to.</summary>
    public long PeerId { get; init; }

    public string PlayerName
    {
        get => _label.Text;
        set => _label.Text = value;
    }

    /// <summary>The animal the other player is playing as, or null before their first update arrives.</summary>
    public Animal? Animal { get; private set; }

    /// <summary>How much health the other player's animal has, out of 100.</summary>
    public float Health { get; private set; } = 100f;

    /// <summary>How far round the body reaches from its middle, so a bite can tell whether it's close enough.</summary>
    public float BodyRadius => Animal is null ? 0f : Animal.Stats.BodyRadius * Animal.Growth;

    /// <summary>The order the pose values come in over the network; see <see cref="Pack"/>.</summary>
    private enum Slot { Growth, Speed, Stride, Eat, Flap, Dive, Sitting, Lying, Dead, Landing, Impact, Spread, Health, Flags, Count }

    [System.Flags]
    private enum Flag { Swimming = 1, Flying = 2, Climbing = 4, Clutching = 8, Diving = 16 }

    private readonly Label3D _label = new()
    {
        Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
        FontSize = 64,
        OutlineSize = 12,
        PixelSize = 0.006f,
        NoDepthTest = true,
        FixedSize = false,
        Modulate = new Color(0.75f, 0.9f, 1f),
    };

    private readonly HealthBar _bar = new();
    private int _kind = -1;
    private float[] _pose = new float[(int)Slot.Count];
    private Vector3 _target;
    private Vector3 _offset;
    private Vector3 _turn;
    private bool _placed;

    public override void _Ready()
    {
        AddChild(_label);
        AddChild(_bar);
    }

    /// <summary>
    /// The values another player's game sends about its animal: everything the model needs to take the same pose,
    /// besides where it is.
    /// </summary>
    public static float[] Pack(Player player)
    {
        var animal = player.Animal;
        var pose = new float[(int)Slot.Count];
        pose[(int)Slot.Growth] = animal.Growth;
        (pose[(int)Slot.Speed], pose[(int)Slot.Stride], pose[(int)Slot.Eat]) = player.Motion;
        pose[(int)Slot.Flap] = animal.Flap;
        pose[(int)Slot.Dive] = animal.Dive;
        pose[(int)Slot.Sitting] = animal.Sitting;
        pose[(int)Slot.Lying] = animal.Lying;
        pose[(int)Slot.Dead] = animal.Dead;
        pose[(int)Slot.Landing] = animal.Landing;
        pose[(int)Slot.Impact] = animal.Impact;
        pose[(int)Slot.Spread] = animal.Spread;
        pose[(int)Slot.Health] = player.Health;
        var flags = (animal.IsSwimming ? Flag.Swimming : 0) | (animal.IsFlying ? Flag.Flying : 0)
            | (animal.IsClimbing ? Flag.Climbing : 0) | (animal is BaldEagle { Clutching: true } ? Flag.Clutching : 0)
            | (animal.IsDiving ? Flag.Diving : 0);
        pose[(int)Slot.Flags] = (int)flags;
        return pose;
    }

    /// <summary>
    /// Takes an update from the other player: which animal (by its place in <see cref="Player.Kinds"/>), where the
    /// body is, where the model sits on it and how it's turned, and its pose.
    /// </summary>
    public void Receive(int kind, Vector3 position, Vector3 offset, Vector3 rotation, float[] pose)
    {
        if (pose.Length < (int)Slot.Count || kind < 0 || kind >= Player.Kinds.Length)
            return;
        if (kind != _kind)
        {
            // They've switched animal: build the new one in place of the old.
            Animal?.QueueFree();
            Animal = Player.Kinds[kind]();
            AddChild(Animal);
            _kind = kind;
        }
        _pose = pose;
        _target = position;
        _offset = offset;
        _turn = rotation;

        // The first update puts it straight there, rather than gliding in from the middle of the world.
        if (!_placed)
        {
            GlobalPosition = position;
            _placed = true;
        }
    }

    public override void _Process(double delta)
    {
        if (Animal is null)
            return;
        float dt = (float)delta;

        // Updates come a twentieth of a second apart, so glide towards the latest rather than jumping to it. One far
        // off, e.g. after a rebirth, is jumped to.
        float follow = 1f - Mathf.Exp(-15f * dt);
        GlobalPosition = GlobalPosition.DistanceTo(_target) > 20f ? _target : GlobalPosition.Lerp(_target, follow);
        Animal.Position = Animal.Position.Lerp(_offset, follow);
        Animal.Rotation = new Vector3(
            Mathf.LerpAngle(Animal.Rotation.X, _turn.X, follow),
            Mathf.LerpAngle(Animal.Rotation.Y, _turn.Y, follow),
            Mathf.LerpAngle(Animal.Rotation.Z, _turn.Z, follow));

        float Get(Slot slot) => _pose[(int)slot];
        var flags = (Flag)(int)Get(Slot.Flags);
        if (!Mathf.IsEqualApprox(Animal.Growth, Get(Slot.Growth)))
            Animal.Growth = Get(Slot.Growth);
        Animal.IsSwimming = flags.HasFlag(Flag.Swimming);
        Animal.IsFlying = flags.HasFlag(Flag.Flying);
        Animal.IsClimbing = flags.HasFlag(Flag.Climbing);
        Animal.IsDiving = flags.HasFlag(Flag.Diving);
        if (Animal is BaldEagle eagle)
            eagle.Clutching = flags.HasFlag(Flag.Clutching);
        Animal.Flap = Get(Slot.Flap);
        Animal.Dive = Get(Slot.Dive);
        Animal.Sitting = Get(Slot.Sitting);
        Animal.Lying = Get(Slot.Lying);
        Animal.Dead = Get(Slot.Dead);
        Animal.Landing = Get(Slot.Landing);
        Animal.Impact = Get(Slot.Impact);
        Animal.Spread = Get(Slot.Spread);
        Health = Get(Slot.Health);
        Animal.Animate(Get(Slot.Speed), Get(Slot.Stride), Get(Slot.Eat), dt);

        // The name floats over the health bar, both clear of the animal's head however big it has grown.
        float top = Animal.Stats.BodyHeight * Animal.Growth;
        _bar.Position = new Vector3(0f, top + 1f, 0f);
        _label.Position = new Vector3(0f, top + 1.6f, 0f);
        _bar.Show(Health, GetViewport().GetCamera3D()?.GlobalPosition);
    }
}
