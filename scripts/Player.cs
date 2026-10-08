using Godot;

namespace IceAgeWorld;

/// <summary>
/// The player's animal: camera-relative movement, sprinting, jumping, a third-person orbit camera
/// and a simple procedural walk animation for the placeholder model.
/// </summary>
public partial class Player : CharacterBody3D
{
    [Export] public float WalkSpeed { get; set; } = 6f;
    [Export] public float SprintSpeed { get; set; } = 13f;
    [Export] public float Acceleration { get; set; } = 10f;
    [Export] public float JumpVelocity { get; set; } = 7f;
    [Export] public float TurnSpeed { get; set; } = 6f;
    [Export] public float MouseSensitivity { get; set; } = 0.003f;

    /// <summary>Seconds one mouthful of grass takes, from lowering the head to raising it again.</summary>
    [Export] public float EatDuration { get; set; } = 1.4f;

    /// <summary>How close grass must be to the animal's mouth to be eaten.</summary>
    [Export] public float EatReach { get; set; } = 2f;

    public Terrain? Terrain { get; set; }
    public Grassland? Grassland { get; set; }

    /// <summary>True when there is grass in reach and the animal is free to eat it.</summary>
    public bool CanEat { get; private set; }

    public bool IsEating => _eatTimer > 0f;

    private const float MouthDistance = 2.6f;
    private const float HeadDownAngle = -0.6f;

    private readonly float _gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();

    private Node3D _model = null!;
    private Node3D _neck = null!;
    private Node3D _cameraPivot = null!;
    private SpringArm3D _springArm = null!;
    private Node3D[] _legs = [];
    private float _walkCycle;
    private float _eatTimer;
    private int _eatTarget = -1;

    public override void _Ready()
    {
        _model = GetNode<Node3D>("Model");
        _neck = GetNode<Node3D>("Model/Neck");
        _cameraPivot = GetNode<Node3D>("CameraPivot");
        _springArm = GetNode<SpringArm3D>("CameraPivot/SpringArm3D");
        _legs =
        [
            GetNode<Node3D>("Model/LegFrontLeft"),
            GetNode<Node3D>("Model/LegFrontRight"),
            GetNode<Node3D>("Model/LegBackLeft"),
            GetNode<Node3D>("Model/LegBackRight"),
        ];

        // Stop the camera arm colliding with our own body.
        _springArm.AddExcludedObject(GetRid());
        _springArm.Rotation = new Vector3(-0.35f, 0, 0);
    }

    /// <summary>Places the player just above the ground at the centre of the map.</summary>
    public void Respawn()
    {
        float ground = Terrain?.GetHeight(0, 0) ?? 0f;
        GlobalPosition = new Vector3(0, ground + 2f, 0);
        Velocity = Vector3.Zero;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Input.MouseMode != Input.MouseModeEnum.Captured)
            return;

        if (@event is InputEventMouseMotion motion)
        {
            _cameraPivot.RotateY(-motion.Relative.X * MouseSensitivity);
            var pitch = _springArm.Rotation.X - motion.Relative.Y * MouseSensitivity;
            _springArm.Rotation = new Vector3(Mathf.Clamp(pitch, -1.3f, 0.3f), 0, 0);
        }
        else if (@event is InputEventMouseButton { Pressed: true } button)
        {
            if (button.ButtonIndex == MouseButton.WheelUp)
                _springArm.SpringLength = Mathf.Max(4f, _springArm.SpringLength - 1f);
            else if (button.ButtonIndex == MouseButton.WheelDown)
                _springArm.SpringLength = Mathf.Min(25f, _springArm.SpringLength + 1f);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        var velocity = Velocity;

        if (!IsOnFloor())
            velocity.Y -= _gravity * dt;
        else if (!IsEating && Input.IsActionJustPressed(InputSetup.Jump))
            velocity.Y = JumpVelocity;

        // Movement is relative to where the camera is facing, flattened onto the ground plane.
        var input = Input.GetVector(InputSetup.MoveLeft, InputSetup.MoveRight, InputSetup.MoveForward, InputSetup.MoveBack);
        var camera = _cameraPivot.GlobalBasis;
        var direction = camera.X * input.X + camera.Z * input.Y;
        direction.Y = 0;
        direction = direction.Normalized();

        UpdateEating(dt);

        // The animal stands still while it eats.
        if (IsEating)
            direction = Vector3.Zero;

        float speed = Input.IsActionPressed(InputSetup.Sprint) ? SprintSpeed : WalkSpeed;
        var target = direction * speed;
        var horizontal = new Vector3(velocity.X, 0, velocity.Z).Lerp(target, Mathf.Min(1f, Acceleration * dt));
        velocity.X = horizontal.X;
        velocity.Z = horizontal.Z;

        // Turn the model (not the body, which also carries the camera) to face the way we're moving.
        if (direction != Vector3.Zero)
        {
            float yaw = Mathf.Atan2(-direction.X, -direction.Z);
            _model.Rotation = new Vector3(0, Mathf.LerpAngle(_model.Rotation.Y, yaw, TurnSpeed * dt), 0);
        }

        Velocity = velocity;
        MoveAndSlide();

        AnimateLegs(horizontal.Length(), dt);

        if (GlobalPosition.Y < -50f)
            Respawn();
    }

    /// <summary>
    /// Starts a mouthful when the eat key is pressed near grass, dips the head while eating,
    /// and flattens the grass halfway through, when the head is lowest.
    /// </summary>
    private void UpdateEating(float dt)
    {
        int reachable = -1;
        if (!IsEating && IsOnFloor() && Grassland is not null)
        {
            var mouth = GlobalPosition - _model.GlobalBasis.Z * MouthDistance;
            reachable = Grassland.FindEdible(mouth, EatReach);
        }
        CanEat = reachable >= 0;

        if (CanEat && Input.IsActionJustPressed(InputSetup.Eat))
        {
            _eatTarget = reachable;
            _eatTimer = EatDuration;
        }

        if (!IsEating)
            return;

        _eatTimer = Mathf.Max(0f, _eatTimer - dt);
        float progress = 1f - _eatTimer / EatDuration;
        _neck.Rotation = new Vector3(Mathf.Sin(progress * Mathf.Pi) * HeadDownAngle, 0, 0);

        if (progress >= 0.5f && _eatTarget >= 0)
        {
            Grassland!.Eat(_eatTarget);
            _eatTarget = -1;
        }
    }

    /// <summary>Swings diagonal pairs of legs together, like a real quadruped's walk.</summary>
    private void AnimateLegs(float speed, float dt)
    {
        _walkCycle += speed * dt * 0.9f;
        float amount = IsOnFloor() ? Mathf.Clamp(speed / WalkSpeed, 0f, 1f) * 0.5f : 0f;
        float swing = Mathf.Sin(_walkCycle) * amount;

        _legs[0].Rotation = new Vector3(swing, 0, 0);
        _legs[3].Rotation = new Vector3(swing, 0, 0);
        _legs[1].Rotation = new Vector3(-swing, 0, 0);
        _legs[2].Rotation = new Vector3(-swing, 0, 0);
    }
}
