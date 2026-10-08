using Godot;

namespace IceAgeWorld;

/// <summary>
/// The player's animal: camera-relative movement, sprinting, jumping, swimming, eating and drinking,
/// hunger and thirst, and a third-person orbit camera. The player can switch between animals; each
/// <see cref="Animal"/> supplies its own speeds, size and diet, and animates itself from the speed, stride
/// and head-dip it is given each frame.
/// </summary>
public partial class Player : CharacterBody3D
{
    [Export] public float Acceleration { get; set; } = 10f;
    [Export] public float TurnSpeed { get; set; } = 6f;
    [Export] public float MouseSensitivity { get; set; } = 0.003f;

    /// <summary>Seconds one mouthful of grass takes, from lowering the head to raising it again.</summary>
    [Export] public float EatDuration { get; set; } = 1.4f;

    /// <summary>Seconds one drink takes.</summary>
    [Export] public float DrinkDuration { get; set; } = 2.5f;

    /// <summary>Seconds for a full hunger bar to empty while walking about.</summary>
    [Export] public float HungerDrainSeconds { get; set; } = 960f;

    /// <summary>Seconds for a full thirst bar to empty while walking about.</summary>
    [Export] public float ThirstDrainSeconds { get; set; } = 600f;

    /// <summary>How much faster hunger and thirst drain while running or swimming.</summary>
    [Export] public float ExertionDrainMultiplier { get; set; } = 2f;

    /// <summary>Hunger restored by one mouthful of grass.</summary>
    [Export] public float FoodPerMouthful { get; set; } = 12f;

    /// <summary>Thirst restored by one full drink.</summary>
    [Export] public float WaterPerDrink { get; set; } = 40f;

    /// <summary>Fullness from 0 (starving) to 100 (full).</summary>
    public float Hunger { get; private set; } = 75f;

    /// <summary>Hydration from 0 (parched) to 100 (fully watered).</summary>
    public float Thirst { get; private set; } = 60f;

    /// <summary>True when hunger or thirst has run out; the animal is too weak to run and walks slowly.</summary>
    public bool IsWeak => Hunger <= 0f || Thirst <= 0f;

    public Terrain? Terrain { get; set; }
    public Grassland? Grassland { get; set; }
    public Water? Water { get; set; }

    /// <summary>What pressing E will do right now (e.g. "Press E to drink"), or null if nothing.</summary>
    public string? ActionPrompt { get; private set; }

    public bool IsFeeding => _feeding != Feeding.None;
    public bool IsSwimming { get; private set; }

    /// <summary>The animal the player is currently playing as.</summary>
    public Animal Animal => _animals[_animalIndex];

    private AnimalStats Stats => Animal.Stats;

    private enum Feeding { None, Eating, Drinking }

    private readonly float _gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();

    private Animal[] _animals = [];
    private int _animalIndex;
    private CollisionShape3D _collision = null!;
    private Node3D _cameraPivot = null!;
    private SpringArm3D _springArm = null!;
    private Feeding _feeding;
    private float _feedTimer;
    private float _feedDuration;
    private int _grassTarget = -1;

    /// <summary>0..1 how far the head is dipped to eat or drink, passed to the model each frame.</summary>
    private float _headDip;

    public override void _Ready()
    {
        _collision = GetNode<CollisionShape3D>("CollisionShape3D");
        _cameraPivot = GetNode<Node3D>("CameraPivot");
        _springArm = GetNode<SpringArm3D>("CameraPivot/SpringArm3D");

        // Stop the camera arm colliding with our own body.
        _springArm.AddExcludedObject(GetRid());
        _springArm.Rotation = new Vector3(-0.35f, 0, 0);

        // Every animal is built up front, so switching is instant; only the current one is shown.
        _animals = [new Mammoth { Name = "Mammoth" }, new SnowLeopard { Name = "SnowLeopard" }];
        foreach (var animal in _animals)
        {
            animal.Visible = false;
            AddChild(animal);
        }
        BecomeAnimal(0);
    }

    /// <summary>Swaps to the next animal, keeping the way the old one was facing.</summary>
    public void SwitchAnimal()
    {
        float yaw = Animal.Rotation.Y;
        Animal.Visible = false;
        BecomeAnimal((_animalIndex + 1) % _animals.Length);
        Animal.Rotation = new Vector3(0, yaw, 0);
    }

    /// <summary>Shows the given animal and fits the collision body and camera to its size.</summary>
    private void BecomeAnimal(int index)
    {
        _animalIndex = index;
        Animal.Visible = true;

        // Interrupt any meal in progress; the new animal may not even eat grass.
        _feeding = Feeding.None;
        _grassTarget = -1;
        _headDip = 0f;

        _collision.Shape = new CapsuleShape3D { Radius = Stats.BodyRadius, Height = Stats.BodyHeight };
        _collision.Position = new Vector3(0, Stats.BodyHeight / 2f, 0);
        _cameraPivot.Position = new Vector3(0, Stats.CameraHeight, 0);
        _springArm.SpringLength = Stats.CameraDistance;
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
        if (@event.IsActionPressed(InputSetup.SwitchAnimal))
        {
            SwitchAnimal();
            return;
        }

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
            // Zoom range and step scale with the animal, so a small one can be seen up close.
            float step = Stats.CameraDistance / 11f;
            if (button.ButtonIndex == MouseButton.WheelUp)
                _springArm.SpringLength = Mathf.Max(Stats.CameraDistance * 0.35f, _springArm.SpringLength - step);
            else if (button.ButtonIndex == MouseButton.WheelDown)
                _springArm.SpringLength = Mathf.Min(Stats.CameraDistance * 2.3f, _springArm.SpringLength + step);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        var velocity = Velocity;

        // How deep the water is at our feet; 0 on dry land.
        float? surface = Water?.SurfaceAt(GlobalPosition);
        float waterDepth = surface.HasValue ? Mathf.Max(0f, surface.Value - GlobalPosition.Y) : 0f;

        // Start swimming a little before the float depth, so we don't flicker in and out of it while floating.
        IsSwimming = waterDepth > Stats.FloatDepth * 0.87f;

        if (IsSwimming)
        {
            // Spring towards floating height, with a gentle bob.
            float bob = Mathf.Sin(Time.GetTicksMsec() / 1000f * 2f) * 0.06f;
            float floatY = surface!.Value - Stats.FloatDepth + bob;
            velocity.Y = (floatY - GlobalPosition.Y) * 3f;
        }
        else if (!IsOnFloor())
        {
            velocity.Y -= _gravity * dt;
        }
        else if (!IsFeeding && Input.IsActionJustPressed(InputSetup.Jump))
        {
            velocity.Y = Stats.JumpVelocity;
        }

        // Movement is relative to where the camera is facing, flattened onto the ground plane.
        var input = Input.GetVector(InputSetup.MoveLeft, InputSetup.MoveRight, InputSetup.MoveForward, InputSetup.MoveBack);
        var camera = _cameraPivot.GlobalBasis;
        var direction = camera.X * input.X + camera.Z * input.Y;
        direction.Y = 0;
        direction = direction.Normalized();

        UpdateFeeding(dt);

        // The animal stands still while it eats or drinks.
        if (IsFeeding)
            direction = Vector3.Zero;

        bool sprinting = Input.IsActionPressed(InputSetup.Sprint) && direction != Vector3.Zero && !IsSwimming && !IsWeak;
        UpdateNeeds(dt, exerting: sprinting || IsSwimming);

        float speed = sprinting ? Stats.SprintSpeed : Stats.WalkSpeed;
        if (IsSwimming)
            speed = Stats.SwimSpeed;
        else if (waterDepth > Stats.WadeDepth)
            speed *= 0.6f;
        if (IsWeak)
            speed *= 0.6f;

        var target = direction * speed;
        var horizontal = new Vector3(velocity.X, 0, velocity.Z).Lerp(target, Mathf.Min(1f, Acceleration * dt));
        velocity.X = horizontal.X;
        velocity.Z = horizontal.Z;

        // Turn the model (not the body, which also carries the camera) to face the way we're moving.
        if (direction != Vector3.Zero)
        {
            float yaw = Mathf.Atan2(-direction.X, -direction.Z);
            Animal.Rotation = new Vector3(0, Mathf.LerpAngle(Animal.Rotation.Y, yaw, TurnSpeed * dt), 0);
        }

        Velocity = velocity;
        MoveAndSlide();

        float groundSpeed = horizontal.Length();
        float stride;
        if (IsSwimming)
        {
            // Keep paddling even when not moving, to tread water.
            groundSpeed += 2f;
            stride = 1f;
        }
        else
        {
            stride = IsOnFloor() ? Mathf.Clamp(groundSpeed / Stats.WalkSpeed, 0f, 1f) : 0f;
        }
        Animal.Animate(groundSpeed, stride, _headDip, dt);

        if (GlobalPosition.Y < -50f)
            Respawn();
    }

    /// <summary>
    /// Works out whether the animal can drink or eat, starts doing so when E is pressed, and tracks how far
    /// the head is dipped while it happens. Grass is flattened halfway through a mouthful, when the head is
    /// lowest. Water takes priority over grass, since grass doesn't grow at the water's edge.
    /// </summary>
    private void UpdateFeeding(float dt)
    {
        ActionPrompt = null;

        if (!IsFeeding && IsOnFloor() && !IsSwimming)
        {
            var mouth = GlobalPosition - Animal.GlobalBasis.Z * Stats.MouthDistance;
            int grass = Stats.CanGraze ? Grassland?.FindEdible(mouth, Stats.EatReach) ?? -1 : -1;

            if (Water?.SurfaceAt(mouth) is not null)
            {
                ActionPrompt = "Press E to drink";
                if (Input.IsActionJustPressed(InputSetup.Eat))
                    StartFeeding(Feeding.Drinking, DrinkDuration);
            }
            else if (grass >= 0)
            {
                ActionPrompt = "Press E to eat grass";
                if (Input.IsActionJustPressed(InputSetup.Eat))
                {
                    StartFeeding(Feeding.Eating, EatDuration);
                    _grassTarget = grass;
                }
            }
        }

        if (!IsFeeding)
        {
            _headDip = 0f;
            return;
        }

        _feedTimer = Mathf.Max(0f, _feedTimer - dt);
        float progress = 1f - _feedTimer / _feedDuration;

        // Lower the head, hold it down, then raise it again.
        _headDip = Mathf.Min(1f, Mathf.Sin(progress * Mathf.Pi) * 1.6f);

        // Grass is flattened halfway through the mouthful, when the head is lowest.
        if (_feeding == Feeding.Eating && progress >= 0.5f && _grassTarget >= 0)
        {
            Grassland!.Eat(_grassTarget);
            _grassTarget = -1;
            Hunger = Mathf.Min(100f, Hunger + FoodPerMouthful);
        }

        // Thirst fills steadily for as long as the animal drinks.
        if (_feeding == Feeding.Drinking)
            Thirst = Mathf.Min(100f, Thirst + WaterPerDrink * dt / _feedDuration);

        if (_feedTimer <= 0f)
            _feeding = Feeding.None;
    }

    /// <summary>Hunger and thirst drain all the time, and faster when the animal is working hard.</summary>
    private void UpdateNeeds(float dt, bool exerting)
    {
        float rate = exerting ? ExertionDrainMultiplier : 1f;
        Hunger = Mathf.Max(0f, Hunger - 100f / HungerDrainSeconds * rate * dt);
        Thirst = Mathf.Max(0f, Thirst - 100f / ThirstDrainSeconds * rate * dt);
    }

    private void StartFeeding(Feeding feeding, float duration)
    {
        _feeding = feeding;
        _feedDuration = duration;
        _feedTimer = duration;
    }
}
