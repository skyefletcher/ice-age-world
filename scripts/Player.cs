using System;
using System.Collections.Generic;
using Godot;

namespace IceAgeWorld;

/// <summary>
/// The player's animal: camera-relative movement, sprinting, jumping, swimming, eating and drinking,
/// hunger, thirst and stamina, and a third-person orbit camera. The player can switch between animals; each
/// <see cref="Animal"/> supplies its own speeds, size, diet and abilities, and animates itself from the speed,
/// stride and head-dip it is given each frame. Flying, tree climbing, hunting, growing up, and being hunted and dying live in their own files.
/// </summary>
public partial class Player : CharacterBody3D
{
    [Export] public float MouseSensitivity { get; set; } = 0.003f;

    /// <summary>Seconds one mouthful of grass takes, from lowering the head to raising it again.</summary>
    [Export] public float EatDuration { get; set; } = 1.4f;

    /// <summary>Seconds one drink takes.</summary>
    [Export] public float DrinkDuration { get; set; } = 2.5f;

    /// <summary>Seconds for a full hunger bar to empty while walking about.</summary>
    [Export] public float HungerDrainSeconds { get; set; } = 960f;

    /// <summary>Seconds for a full thirst bar to empty while walking about.</summary>
    [Export] public float ThirstDrainSeconds { get; set; } = 600f;

    /// <summary>How much faster hunger and thirst drain while running, swimming, flapping or climbing.</summary>
    [Export] public float ExertionDrainMultiplier { get; set; } = 2f;

    /// <summary>Hunger restored by one mouthful of grass.</summary>
    [Export] public float FoodPerMouthful { get; set; } = 12f;

    /// <summary>Thirst restored by one full drink.</summary>
    [Export] public float WaterPerDrink { get; set; } = 40f;

    /// <summary>Seconds to sit or lie down, or to get up again.</summary>
    [Export] public float PostureChangeSeconds { get; set; } = 1f;

    /// <summary>Once stamina runs out, it must refill this far before the animal can exert itself again.</summary>
    [Export] public float RecoveredStamina { get; set; } = 25f;

    /// <summary>Fullness from 0 (starving) to 100 (full).</summary>
    public float Hunger { get; private set; } = 75f;

    /// <summary>Hydration from 0 (parched) to 100 (fully watered).</summary>
    public float Thirst { get; private set; } = 60f;

    /// <summary>Breath from 0 (spent) to 100 (fresh). Hard work uses it up and rest brings it back.</summary>
    public float Stamina { get; private set; } = 100f;

    /// <summary>True when hunger or thirst has run out; the animal is too weak to run and walks slowly.</summary>
    public bool IsWeak => Hunger <= 0f || Thirst <= 0f;

    /// <summary>True from when stamina runs out until it has partly refilled; the animal can only walk, glide or cling on.</summary>
    public bool IsExhausted { get; private set; }

    public Terrain? Terrain { get; set; }
    public Grassland? Grassland { get; set; }
    public Water? Water { get; set; }
    public Wildlife? Wildlife { get; set; }

    /// <summary>What the player can do right now (e.g. "Press E to drink"), or null if nothing.</summary>
    public string? ActionPrompt { get; private set; }

    /// <summary>A short piece of news to show for a few seconds, e.g. a new wolf joining the pack, or null.</summary>
    public string? Message => _messageTime > 0f ? _message : null;

    private string? _message;
    private float _messageTime;

    /// <summary>
    /// A wild animal the player killed is reborn into the player's own pack or herd of that kind, if it has one, e.g. a
    /// wolf beaten in a fight joining the pack that beat it.
    /// </summary>
    public void Recruit(Animal fallen)
    {
        foreach (var (animal, herd) in _herds)
        {
            if (animal.GetType() != fallen.GetType())
                continue;
            herd.Recruit();
            _message = $"The {fallen.DisplayName.ToLower()} you beat is reborn: a pup in your pack, and one in its own!";
            _messageTime = 6f;
            return;
        }
    }

    public bool IsFeeding => _feeding != Feeding.None;
    public bool IsSwimming { get; private set; }

    /// <summary>Whether the player has asked the animal to sit or lie down; its companions follow suit.</summary>
    public Posture Posture { get; private set; }

    /// <summary>The animal the player is currently playing as.</summary>
    public Animal Animal => _animals[_animalIndex];

    private enum Feeding { None, Eating, Drinking }

    private enum Mode { Ground, Flying, Climbing, Treetop, Branch }

    private readonly float _gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();

    private Animal[] _animals = [];
    private readonly Dictionary<Animal, Herd> _herds = [];
    private int _animalIndex;
    private Mode _mode;
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
        _animals =
        [
            new Mammoth { Name = "Mammoth" },
            new SnowLeopard { Name = "SnowLeopard" },
            new ArcticWolf { Name = "ArcticWolf" },
            new SeaOtter { Name = "SeaOtter" },
            new BaldEagle { Name = "BaldEagle" },
            new Reindeer { Name = "Reindeer" },
            new Moose { Name = "Moose" },
            new PolarBear { Name = "PolarBear" },
        ];
        foreach (var animal in _animals)
        {
            animal.Visible = false;
            AddChild(animal);

            // Pack and herd animals come with companions of their own kind, who only show while the player is one of them.
            if (animal.Stats.Companions > 0)
            {
                var kind = animal.GetType();
                var herd = new Herd
                {
                    Leader = this,
                    Breed = () => (Animal)Activator.CreateInstance(kind)!,
                    Count = animal.Stats.Companions,
                    Name = animal.Name + "Herd",
                    Visible = false,
                };
                _herds[animal] = herd;
                AddChild(herd);
            }
        }
        BecomeAnimal(0);
    }

    /// <summary>Swaps to the next animal, keeping the way the old one was facing.</summary>
    public void SwitchAnimal()
    {
        float yaw = Animal.Rotation.Y;
        LeaveTree();
        Animal.Landing = 0f;
        Animal.Visible = false;
        if (_herds.TryGetValue(Animal, out var oldHerd))
            oldHerd.Visible = false;

        BecomeAnimal((_animalIndex + 1) % _animals.Length);
        Animal.Rotation = new Vector3(0, yaw, 0);
    }

    /// <summary>Shows the given animal and fits the collision body and camera to its size and age.</summary>
    private void BecomeAnimal(int index)
    {
        _animalIndex = index;
        _mode = Mode.Ground;
        Animal.Visible = true;
        Animal.IsFlying = Animal.IsClimbing = Animal.IsSwimming = false;
        _fromTree = false;
        Posture = Posture.Standing;
        Animal.Sitting = Animal.Lying = 0f;

        // Interrupt any meal in progress; the new animal may not even eat grass.
        _feeding = Feeding.None;
        _grassTarget = -1;
        _headDip = 0f;
        StopHunting();

        _fittedSize = 0f;
        FitToSize();

        if (_herds.TryGetValue(Animal, out var herd))
        {
            herd.Visible = true;
            herd.Gather();
        }
    }

    /// <summary>Places the player just above the ground at the centre of the map.</summary>
    public void Respawn()
    {
        _mode = Mode.Ground;
        Animal.IsFlying = Animal.IsClimbing = false;
        _fromTree = false;
        Animal.Landing = 0f;
        Posture = Posture.Standing;
        Animal.Sitting = Animal.Lying = 0f;
        Animal.Rotation = new Vector3(0, Animal.Rotation.Y, 0);
        StopHunting();
        float ground = Terrain?.GetHeight(0, 0) ?? 0f;
        GlobalPosition = new Vector3(0, ground + 2f, 0);
        Velocity = Vector3.Zero;
        if (_herds.TryGetValue(Animal, out var herd))
            herd.Gather();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // The dead choose what to be reborn as instead (see Hud).
        if (@event.IsActionPressed(InputSetup.SwitchAnimal) && !IsDead)
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
        ActionPrompt = null;
        _messageTime -= dt;
        if (IsDead)
        {
            LieDead(dt);
            return;
        }
        Grow(dt);
        Heal(dt);

        // Movement is relative to where the camera is facing, flattened onto the ground plane.
        var input = Input.GetVector(InputSetup.MoveLeft, InputSetup.MoveRight, InputSetup.MoveForward, InputSetup.MoveBack);
        var camera = _cameraPivot.GlobalBasis;
        var direction = camera.X * input.X + camera.Z * input.Y;
        direction.Y = 0;
        direction = direction.Normalized();

        switch (_mode)
        {
            case Mode.Ground when IsGripping:
                HoldOn(dt);
                break;
            case Mode.Flying:
                Fly(dt, direction);
                break;
            case Mode.Climbing:
            case Mode.Treetop:
            case Mode.Branch:
                Climb(dt, input);
                break;
            default:
                Walk(dt, direction);
                break;
        }

        if (GlobalPosition.Y < -50f)
            Respawn();
        HoldKill();
    }

    /// <summary>Moving on foot or swimming: everything every animal can do.</summary>
    private void Walk(float dt, Vector3 direction)
    {
        var velocity = Velocity;

        // How deep the water is at our feet; 0 on dry land.
        float? surface = Water?.SurfaceAt(GlobalPosition);
        float waterDepth = surface.HasValue ? Mathf.Max(0f, surface.Value - GlobalPosition.Y) : 0f;

        // Start swimming a little before the float depth, and only stop once well out of it, so bobbing on the surface
        // never flickers the animal in and out of swimming.
        IsSwimming = waterDepth > Stats.FloatDepth * (IsSwimming ? 0.5f : 0.87f);

        // Sit or lie down, or get up again. Moving or jumping gets the animal up, but it can't go anywhere until it's
        // back on its feet.
        bool resting = UpdatePosture(dt, IsOnFloor() && !IsSwimming && !IsFeeding, direction != Vector3.Zero);
        UpdateFeeding(dt, lookForFood: IsOnFloor() && !IsSwimming);
        if (Stats.Can(Ability.Hunt))
            UpdateHunting(dt, resting, upTree: false);

        // The animal stands still while it eats or drinks.
        if (IsFeeding || resting)
            direction = Vector3.Zero;

        // A climber that walks into a tree trunk starts up it.
        if (direction != Vector3.Zero && Stats.Can(Ability.ClimbTrees) && IsOnFloor() && !IsSwimming && TryStartClimb(direction))
            return;

        bool jumping = !IsFeeding && !resting && Input.IsActionJustPressed(InputSetup.Jump);
        if (jumping && Stats.Can(Ability.Fly) && (IsOnFloor() || IsSwimming))
        {
            TakeOff(direction);
            return;
        }

        bool airborne = false;
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
            airborne = true;
        }
        else if (jumping)
        {
            // Launch fast enough to clear the animal's jump height, and a running jump carries it further forward.
            velocity.Y = Mathf.Sqrt(2f * _gravity * Stats.JumpHeight);
            velocity += direction * Stats.JumpBoost;
            airborne = true;
        }

        bool sprinting = Input.IsActionPressed(InputSetup.Sprint) && direction != Vector3.Zero && !IsSwimming && !IsWeak && !IsExhausted;
        UpdateNeeds(dt, exerting: sprinting || IsSwimming);
        UpdateStamina(dt, effort: sprinting ? 1f : IsSwimming ? Stats.SwimEffort : 0f);

        float speed = (sprinting ? Stats.SprintSpeed : Stats.WalkSpeed) * (IsCarrying ? CarrySpeed : 1f);
        if (IsSwimming)
            speed = Stats.SwimSpeed * (IsExhausted ? 0.6f : 1f);
        else if (waterDepth > Stats.WadeDepth)
            speed *= 0.6f;
        if (IsWeak)
            speed *= 0.6f;

        var target = direction * speed;
        var horizontal = new Vector3(velocity.X, 0, velocity.Z);
        if (airborne)
        {
            // In the air the animal can only steer a little, and keeps the speed it leapt with.
            if (direction != Vector3.Zero)
                target = direction * Mathf.Max(speed, horizontal.Length());
            horizontal = horizontal.MoveToward(target, Stats.Acceleration * 0.5f * dt);
        }
        else
        {
            horizontal = horizontal.Lerp(target, Mathf.Min(1f, Stats.Acceleration * dt));
        }

        // A pounce springs the hunter forward on top of whatever it was doing, dying away as it lands.
        horizontal += _lunge;
        _lunge = _lunge.MoveToward(Vector3.Zero, _lunge.Length() * 6f * dt + 0.5f * dt);
        velocity.X = horizontal.X;
        velocity.Z = horizontal.Z;

        // Turn the model (not the body, which also carries the camera) to face the way we're moving.
        if (direction != Vector3.Zero)
            Animal.Rotation = new Vector3(0, Mathf.LerpAngle(Animal.Rotation.Y, Yaw(direction), Stats.TurnSpeed * dt), 0);

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
        Animal.IsSwimming = IsSwimming;
        UpdateLanding(dt);
        Animal.Animate(groundSpeed, stride, _headDip, dt);
    }

    /// <summary>Heading that faces a level direction, as a rotation about Y (the models face -Z).</summary>
    private static float Yaw(Vector3 direction) => Mathf.Atan2(-direction.X, -direction.Z);

    /// <summary>
    /// Works out whether the animal can drink or eat, starts doing so when E is pressed, and tracks how far
    /// the head is dipped while it happens. Grass is flattened halfway through a mouthful, when the head is
    /// lowest. Water takes priority over grass, since grass doesn't grow at the water's edge. Only looks for food
    /// when it can reach the ground (<paramref name="lookForFood"/>), but finishes a mouthful anywhere, e.g. of a kill
    /// up a tree (see <see cref="UpdateHunting"/>).
    /// </summary>
    private void UpdateFeeding(float dt, bool lookForFood)
    {
        if (!IsFeeding && lookForFood && !Animal.IsResting)
        {
            int grass = Stats.CanGraze ? Grassland?.FindEdible(Mouth, Stats.EatReach) ?? -1 : -1;

            if (Water?.SurfaceAt(Mouth) is not null)
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

        // Meat is torn off at the same point in the mouthful.
        if (_feeding == Feeding.Eating && progress >= 0.5f && _carcass is not null)
        {
            if (Wildlife!.EatFrom(_carcass, MeatShare))
                Hunger = Mathf.Min(100f, Hunger + MeatPerMouthful);
            _carcass = null;
        }

        // Thirst fills steadily for as long as the animal drinks.
        if (_feeding == Feeding.Drinking)
            Thirst = Mathf.Min(100f, Thirst + WaterPerDrink * dt / _feedDuration);

        if (_feedTimer <= 0f)
            _feeding = Feeding.None;
    }

    /// <summary>
    /// Sits or lies down when its key is pressed somewhere it <paramref name="canRest"/>: dry ground, or up a tree on a
    /// branch or the treetop. Pressing it again, moving or jumping gets the animal up. Eases the model into and out of
    /// the pose, and returns true while the animal is down or still getting up.
    /// </summary>
    private bool UpdatePosture(float dt, bool canRest, bool moving)
    {
        if (canRest && Input.IsActionJustPressed(InputSetup.Sit))
            Posture = Posture == Posture.Sitting ? Posture.Standing : Posture.Sitting;
        else if (canRest && Input.IsActionJustPressed(InputSetup.LieDown))
            Posture = Posture == Posture.Lying ? Posture.Standing : Posture.Lying;
        else if (!canRest || moving || Input.IsActionJustPressed(InputSetup.Jump))
            Posture = Posture.Standing;

        Animal.Settle(Posture, PostureChangeSeconds, dt);
        return Animal.IsResting;
    }

    /// <summary>Hunger and thirst drain all the time, and faster when the animal is working hard.</summary>
    private void UpdateNeeds(float dt, bool exerting)
    {
        float rate = exerting ? ExertionDrainMultiplier : 1f;
        Hunger = Mathf.Max(0f, Hunger - 100f / HungerDrainSeconds * rate * dt);
        Thirst = Mathf.Max(0f, Thirst - 100f / ThirstDrainSeconds * rate * dt);
    }

    /// <summary>
    /// Stamina drains while the animal works, by <paramref name="effort"/> (1 is flat out), and refills while it rests.
    /// Running dry leaves it exhausted until it has partly got its breath back. Sitting down brings it back half as
    /// fast again, and lying down twice as fast.
    /// </summary>
    private void UpdateStamina(float dt, float effort)
    {
        float rest = 1f + Animal.Sitting * 0.5f + Animal.Lying;
        if (effort > 0f)
            Stamina = Mathf.Max(0f, Stamina - 100f / Stats.StaminaSeconds * effort * dt);
        else
            Stamina = Mathf.Min(100f, Stamina + 100f / Stats.RecoverySeconds * rest * dt);

        if (Stamina <= 0f)
            IsExhausted = true;
        else if (Stamina >= RecoveredStamina)
            IsExhausted = false;
    }

    private void StartFeeding(Feeding feeding, float duration)
    {
        _feeding = feeding;
        _feedDuration = duration;
        _feedTimer = duration;
    }
}
