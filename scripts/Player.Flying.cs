using Godot;

namespace IceAgeWorld;

/// <summary>
/// Flight, for animals that can fly. A bird can't hover: it is always flying forward along its heading and banks
/// round towards the way the player steers. Holding Space beats the wings to climb, which costs stamina; letting go
/// glides gently down for free; and Shift tucks the wings in to dive fast. Touching the ground or water lands it.
/// </summary>
public partial class Player
{
    /// <summary>How fast flapping climbs, and how fast a glide and a dive sink, in metres a second.</summary>
    private const float FlapClimbRate = 4.5f;
    private const float GlideSinkRate = 1.2f;
    private const float DiveSinkRate = 12f;

    /// <summary>Above this height over the ground the air is too thin to climb any further.</summary>
    private const float MaxFlightHeight = 70f;

    /// <summary>Seconds after taking off before touching the ground counts as landing, so the bird can get clear first.</summary>
    private const float TakeOffGrace = 0.5f;

    private float _flightTime;

    /// <summary>Leaps into the air with a first big wingbeat, heading the way the player is steering.</summary>
    private void TakeOff(Vector3 direction)
    {
        _mode = Mode.Flying;
        _flightTime = 0f;
        IsSwimming = false;
        if (direction != Vector3.Zero)
            Animal.Rotation = new Vector3(0, Yaw(direction), 0);
        Velocity = Forward(Animal.Rotation.Y) * Stats.FlySpeed * 0.4f + Vector3.Up * FlapClimbRate * 1.5f;
        Animal.IsFlying = true;
    }

    private void Fly(float dt, Vector3 direction)
    {
        _flightTime += dt;
        var velocity = Velocity;

        // Bank round towards the way the player is steering.
        float yaw = Animal.Rotation.Y;
        float turn = 0f;
        if (direction != Vector3.Zero)
        {
            float wanted = Mathf.LerpAngle(yaw, Yaw(direction), Mathf.Min(1f, Stats.FlightTurnSpeed * dt));
            turn = Mathf.AngleDifference(yaw, wanted) / dt;
            yaw = wanted;
        }

        bool flapping = Input.IsActionPressed(InputSetup.Jump) && !IsExhausted && !IsWeak;
        bool diving = Input.IsActionPressed(InputSetup.Sprint) && !flapping;

        // Wings bite harder the faster the bird flies, so it picks up speed when diving and loses it when climbing.
        float cruise = direction != Vector3.Zero ? Stats.FlySpeed : Stats.FlySpeed * 0.6f;
        float targetSpeed = diving ? Stats.FlySpeed * 1.7f : cruise;
        float climb = flapping ? FlapClimbRate : diving ? -DiveSinkRate : -GlideSinkRate;

        float ground = Terrain?.GetHeight(GlobalPosition.X, GlobalPosition.Z) ?? 0f;
        if (GlobalPosition.Y - ground > MaxFlightHeight)
            climb = Mathf.Min(climb, 0f);

        float speed = Mathf.MoveToward(new Vector2(velocity.X, velocity.Z).Length(), targetSpeed, Stats.Acceleration * 0.5f * dt);
        var horizontal = Forward(yaw) * speed;
        velocity = new Vector3(horizontal.X, Mathf.MoveToward(velocity.Y, climb, 10f * dt), horizontal.Z);

        UpdateNeeds(dt, exerting: flapping);
        UpdateStamina(dt, effort: flapping ? 1f : 0f);

        // Lean into turns and tip the nose with the climb or dive, smoothly so the bird doesn't twitch.
        float bank = Mathf.Clamp(turn * 0.35f, -0.9f, 0.9f);
        float pitch = Mathf.Clamp(velocity.Y / Mathf.Max(speed, 1f), -0.9f, 0.5f);
        var rotation = Animal.Rotation;
        float smooth = Mathf.Min(1f, 6f * dt);
        Animal.Rotation = new Vector3(Mathf.Lerp(rotation.X, pitch, smooth), yaw, Mathf.Lerp(rotation.Z, bank, smooth));

        Velocity = velocity;
        MoveAndSlide();

        // Splash down on water, or land when the feet touch the ground.
        float? surface = Water?.SurfaceAt(GlobalPosition);
        bool onWater = surface.HasValue && GlobalPosition.Y < surface.Value - Stats.FloatDepth * 0.5f;
        if (_flightTime > TakeOffGrace && (onWater || (IsOnFloor() && !flapping)))
        {
            Land();
            return;
        }

        Animal.Flap = Mathf.MoveToward(Animal.Flap, flapping ? 1f : 0f, 4f * dt);
        Animal.Dive = Mathf.MoveToward(Animal.Dive, diving ? 1f : 0f, 3f * dt);
        Animal.Animate(speed, 0f, 0f, dt);
    }

    private void Land()
    {
        _mode = Mode.Ground;
        Animal.IsFlying = false;
        Animal.Flap = Animal.Dive = 0f;
        Animal.Rotation = new Vector3(0, Animal.Rotation.Y, 0);

        // Back-pedal with the wings to pull up on landing.
        Velocity = Velocity with { X = Velocity.X * 0.2f, Z = Velocity.Z * 0.2f };
    }

    /// <summary>Level unit direction a model with the given heading faces.</summary>
    private static Vector3 Forward(float yaw) => new(-Mathf.Sin(yaw), 0f, -Mathf.Cos(yaw));
}
