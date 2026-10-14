using Godot;

namespace IceAgeWorld;

/// <summary>
/// Fighting other players. In a game with friends, any animal can bite another player's animal standing close in front
/// with F, the way Savannah Life lets every animal fight: it turns on the other, springs at it and bites, much like a
/// hunter's pounce. The bite is sent over the network to the other player, whose animal takes it like any other.
/// </summary>
public partial class Player
{
    /// <summary>
    /// How much one bite takes from another player's animal, before its size and toughness soften it: about an eighth of
    /// a wolf's health from a grown wolf, far more from a polar bear, and far less from a cub.
    /// </summary>
    [Export] public float FightBite { get; set; } = 12f;

    /// <summary>The game with other players, if there is one; it knows where their animals are and carries bites to them.</summary>
    public Network? Network { get; set; }

    /// <summary>
    /// Bites another player's animal close in front when F is pressed. Hunters (see <see cref="UpdateHunting"/>) play
    /// out the pounce themselves; for every other animal it's eased out here.
    /// </summary>
    private void Fight(float dt, bool resting)
    {
        if (_pounce > 0f && !Stats.Can(Ability.Hunt))
        {
            _pounce = Mathf.Max(0f, _pounce - dt);
            _headDip = Mathf.Max(_headDip, Mathf.Sin((1f - _pounce / PounceSeconds) * Mathf.Pi) * 0.8f);
        }
        if (_pounce > 0f || resting || IsFeeding || IsSwimming || IsExhausted || IsCarrying || Network is null)
            return;
        if (!Input.IsActionJustPressed(InputSetup.Attack))
            return;
        if (Network.RivalNear(Mouth, PounceReach * Size) is not { } rival)
            return;

        var toward = (rival.GlobalPosition - GlobalPosition) with { Y = 0f };
        if (toward.LengthSquared() > 0.0001f)
        {
            toward = toward.Normalized();
            Animal.Rotation = new Vector3(0, Yaw(toward), 0);
        }
        _lunge = toward * Mathf.Min(Stats.JumpBoost, 2f);
        _pounce = PounceSeconds;
        Stamina = Mathf.Max(0f, Stamina - PounceStamina);
        if (Stamina <= 0f)
            IsExhausted = true;

        // Bites go with bulk, as a hunter's do, so a calf nips and a grown bear mauls.
        Network.Bite(rival, FightBite * Stats.Strength * Size * Size);
    }
}
