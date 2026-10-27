using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace IceAgeWorld;

/// <summary>
/// How good an animal is at each thing, out of 10: the design table every animal is balanced from. The speeds,
/// jumps, turning and stamina in <see cref="AnimalStats"/> are all worked out from these, so tuning an animal means
/// changing a score here rather than a dozen numbers.
/// </summary>
public sealed record AnimalScores
{
    public required int JumpHeight { get; init; }
    public required int JumpLength { get; init; }
    public required int LandSpeed { get; init; }
    public required int WaterSpeed { get; init; }

    /// <summary>How sharply the animal turns and how quickly it gets up to speed.</summary>
    public required int Agility { get; init; }

    /// <summary>How long the animal can keep running, swimming, flying or climbing, and how fast it gets its breath back.</summary>
    public required int Stamina { get; init; }

    /// <summary>Agility in the air, for animals that fly; they can be clumsy on the ground but nimble on the wing.</summary>
    public int? FlightAgility { get; init; }
}

/// <summary>Things only some animals can do.</summary>
[Flags]
public enum Ability
{
    None = 0,
    ClimbTrees = 1,
    Fly = 2,

    /// <summary>Travels with a pack that runs in file behind its leader.</summary>
    Pack = 4,

    /// <summary>Travels with a herd that ambles along in a loose crowd and grazes whenever it stops.</summary>
    Herd = 8,

    /// <summary>Stalks, attacks and kills wild animals, and eats from their carcasses.</summary>
    Hunt = 16,

    /// <summary>Leaps onto prey and clamps its jaws on, holding on and doing far more damage than a bite.</summary>
    Grip = 32,

    /// <summary>Dives under the water and swims about down there, holding its breath.</summary>
    Dive = 64,
}

/// <summary>Whether an animal is a male or a female.</summary>
public enum Sex { Male, Female }

/// <summary>What an animal is feeding on, which changes how it goes about it.</summary>
public enum Food { Grass, Water, Meat }

/// <summary>How an animal is resting, if at all.</summary>
public enum Posture { Standing, Sitting, Lying }

/// <summary>How an animal moves, feeds and fills the player's body and camera, so <see cref="Player"/> can drive any of them.</summary>
public sealed record AnimalStats
{
    public required AnimalScores Scores { get; init; }

    public Ability Abilities { get; init; }

    /// <summary>
    /// How big the player's animal grows compared with others of its kind. A pack's leading wolf is the biggest and
    /// strongest in it, so the player's wolf, at the head of its own pack, outgrows the rest.
    /// </summary>
    public float LeaderSize { get; init; } = 1f;

    /// <summary>
    /// How hard a hunter bites, compared with a snow leopard: a bear's jaws and the weight behind them do far more harm.
    /// </summary>
    public float Strength { get; init; } = 1f;

    /// <summary>
    /// How high the top of its back is, as a share of <see cref="BodyHeight"/>: where a snow leopard riding it lies.
    /// Long-legged deer carry their backs low in the capsule, but a mammoth's hump and a bear's shoulders come up to the top.
    /// </summary>
    public float BackHeight { get; init; } = 0.72f;

    /// <summary>How many computer-controlled animals of the same kind travel with the player's pack or herd.</summary>
    public int Companions { get; init; }

    /// <summary>Cruising airspeed, for animals that fly. Diving goes faster, and drifting without steering slower.</summary>
    public float FlySpeed { get; init; }

    // Top speed: a 9 (the snow leopard) sprints at about 15 m/s and a 6 (the mammoth) at about 10 m/s, close to
    // the real animals' bursts; a walk is a comfortable fraction of that.
    public float SprintSpeed => Scores.LandSpeed * 1.7f * Pace;
    public float WalkSpeed => SprintSpeed * 0.4f;
    public float SwimSpeed => (0.5f + Scores.WaterSpeed * 0.8f) * Pace;

    /// <summary>How high a standing jump clears, in metres: about 2.5 m for a 10, and barely a hop for a 1.</summary>
    public float JumpHeight => (0.2f + Scores.JumpHeight * 0.23f) * Pace;

    /// <summary>Extra forward speed a running jump launches with, so long jumpers carry further.</summary>
    public float JumpBoost => Scores.JumpLength * 0.45f * Pace;

    /// <summary>
    /// How fast and far a youngster runs, swims, flies and jumps compared with a grown animal: 1 once grown up. Short
    /// legs and growing muscles keep a calf or cub well behind its mother, though it is just as nimble.
    /// </summary>
    public float Pace { get; init; } = 1f;

    /// <summary>
    /// The same animal at <paramref name="size"/> times its usual grown size: a youngster under 1, or an outsized
    /// leader over it. Its body, reach and camera scale with it. A youngster also moves at a slower
    /// <see cref="Pace"/>, halfway to a grown animal's at birth; a big leader is no faster for its size.
    /// </summary>
    /// <param name="grown">
    /// How far it has grown by age alone, which is all that sets its pace: a grown female, smaller than the males, is no
    /// slower for it. The same as <paramref name="size"/> unless given.
    /// </param>
    public AnimalStats GrownTo(float size, float? grown = null)
    {
        if (size == 1f)
            return this;
        float youth = grown ?? size;
        float pace = youth < 1f ? 0.5f + 0.5f * youth : 1f;
        return this with
        {
            Pace = pace,
            FlySpeed = FlySpeed * pace,
            FloatDepth = FloatDepth * size,
            WadeDepth = WadeDepth * size,
            MouthDistance = MouthDistance * size,
            EatReach = EatReach * size,
            BodyRadius = BodyRadius * size,
            BodyHeight = BodyHeight * size,
            CameraHeight = CameraHeight * size,
            CameraDistance = CameraDistance * size,
        };
    }

    /// <summary>How quickly the body swings round to face a new heading.</summary>
    public float TurnSpeed => TurnSpeedFor(Scores.Agility);

    /// <summary>How quickly the animal reaches the speed it's asked for, or stops.</summary>
    public float Acceleration => 3f + Scores.Agility * 1.1f;

    /// <summary>How quickly a flying animal banks round to a new heading. Wide, sweeping turns even at best.</summary>
    public float FlightTurnSpeed => TurnSpeedFor(Scores.FlightAgility ?? Scores.Agility) * 0.35f;

    /// <summary>Seconds of flat-out effort a full stamina bar lasts.</summary>
    public float StaminaSeconds => Scores.Stamina * 4f;

    /// <summary>Seconds of rest to refill an empty stamina bar.</summary>
    public float RecoverySeconds => 30f - Scores.Stamina * 2f;

    /// <summary>
    /// How hard swimming is compared with sprinting: strong swimmers hardly tire in the water, while poor ones
    /// wear themselves out almost as fast as running.
    /// </summary>
    public float SwimEffort => 1f - Scores.WaterSpeed / 10f;

    public bool Can(Ability ability) => (Abilities & ability) != 0;

    /// <summary>The highest drop, in metres, the animal lands from unhurt: about twice its own jump.</summary>
    public float SafeDrop => 1f + JumpHeight * 2f;

    /// <summary>
    /// Health, from 100, a drop of <paramref name="height"/> metres takes: nothing up to <see cref="SafeDrop"/>, and all
    /// of it from four times that. A climber is never hurt: cats are built for falling, twisting upright, spreading out
    /// to slow themselves and landing on springy legs, and snow leopards leap down cliffs after their prey. Nor is a
    /// bird, which opens its wings before it hits the ground.
    /// </summary>
    public float FallDamage(float height) =>
        Can(Ability.ClimbTrees) || Can(Ability.Fly) ? 0f : 100f * Mathf.Clamp((height - SafeDrop) / (SafeDrop * 3f), 0f, 1f);

    private static float TurnSpeedFor(int agility) => 1.5f + agility * 0.85f;

    /// <summary>
    /// Seconds a diver (<see cref="Ability.Dive"/>) can stay under water on one breath. It goes down from the surface
    /// and must come back up before this runs out.
    /// </summary>
    public float BreathSeconds { get; init; }

    /// <summary>How far below the water surface the animal's feet hang while it floats.</summary>
    public required float FloatDepth { get; init; }

    /// <summary>Water deeper than this (measured at the feet) slows walking down to a wade.</summary>
    public required float WadeDepth { get; init; }

    /// <summary>How far in front of the body's centre the mouth reaches when the head is lowered.</summary>
    public required float MouthDistance { get; init; }

    /// <summary>How close grass must be to the mouth to be eaten.</summary>
    public required float EatReach { get; init; }

    /// <summary>Whether the animal eats grass. Meat eaters that can <see cref="Ability.Hunt"/> eat what they kill instead.</summary>
    public required bool CanGraze { get; init; }

    /// <summary>Radius and height of the upright capsule the animal collides with.</summary>
    public required float BodyRadius { get; init; }
    public required float BodyHeight { get; init; }

    /// <summary>Height the camera orbits around, and its starting distance from there.</summary>
    public required float CameraHeight { get; init; }
    public required float CameraDistance { get; init; }
}

/// <summary>
/// A playable animal model, built entirely in code and facing -Z, that animates itself from the speed, stride
/// and head-dip <see cref="Player"/> gives it each frame. Also holds the mesh-building helpers the animals share.
/// </summary>
public abstract partial class Animal : Node3D
{
    /// <summary>Name shown on the HUD.</summary>
    public abstract string DisplayName { get; }

    public abstract AnimalStats Stats { get; }

    /// <summary>
    /// Advances the animation. <paramref name="speed"/> is ground speed, <paramref name="stride"/> is 0..1 how
    /// hard the animal is walking, and <paramref name="eat"/> is 0..1 how far the head is dipped to eat or drink.
    /// </summary>
    public abstract void Animate(float speed, float stride, float eat, float dt);

    /// <summary>Name shown on the HUD while the player's animal is still growing up, e.g. "Snow leopard cub".</summary>
    public abstract string YoungName { get; }

    /// <summary>
    /// How grown the player's animal is, as a fraction of its full size: the whole model scales with it, on top of any
    /// scale the model gives itself when it is built.
    /// </summary>
    public float Growth
    {
        get => _growth;
        set
        {
            _growth = value;
            Rescale();
        }
    }

    private float _growth = 1f;
    private Vector3? _grownScale;

    /// <summary>
    /// Whether the animal is a male or a female. A female grows to <see cref="FemaleSize"/> of a male's size, and only
    /// males grow the parts marked with <see cref="ForMales{T}"/>, or grow them bigger. Set once the model is built.
    /// </summary>
    public Sex Sex
    {
        get => _sex;
        set
        {
            _sex = value;
            Rescale();
        }
    }

    private Sex _sex;

    /// <summary>
    /// Parts a male grows bigger than a female, with the share of his size she grows them to (0 if not at all), and the
    /// <see cref="Growth"/> he grows them at.
    /// </summary>
    private readonly List<MalePart> _maleParts = [];

    private readonly record struct MalePart(Node3D Part, Vector3 MaleScale, float FemaleSize, float GrownAt);

    /// <summary>
    /// How big a female grows compared with a male, along each dimension. In most of these animals the males are the
    /// bigger, but a female eagle outgrows her mate, and a female hyena hers.
    /// </summary>
    protected virtual float FemaleSize => 1f;

    /// <summary>How big the animal is for its sex, compared with a male of its kind.</summary>
    public float SexSize => Sex == Sex.Female ? FemaleSize : 1f;

    private void Rescale()
    {
        _grownScale ??= Scale;
        Scale = _grownScale.Value * _growth * SexSize;
        foreach (var part in _maleParts)
            ShowFor(part);
    }

    /// <summary>
    /// Marks a part that a male grows bigger than a female: she grows it to <paramref name="femaleSize"/> of his size,
    /// or not at all at 0, e.g. a moose's antlers. It shrinks about its own origin, so pivot it at its root. A young
    /// male only grows it once he is <paramref name="grownAt"/> of his full size, as a lion's mane comes in late.
    /// </summary>
    protected T ForMales<T>(T part, float femaleSize = 0f, float grownAt = 0f) where T : Node3D
    {
        _maleParts.Add(new MalePart(part, part.Scale, femaleSize, grownAt));
        ShowFor(_maleParts[^1]);
        return part;
    }

    private void ShowFor(MalePart male)
    {
        bool female = Sex == Sex.Female;
        male.Part.Visible = female ? male.FemaleSize > 0f : _growth >= male.GrownAt;
        male.Part.Scale = female && male.FemaleSize > 0f ? male.MaleScale * male.FemaleSize : male.MaleScale;
    }

    // What the animal is doing besides walking, set before each Animate so the model can take the right pose.
    public bool IsSwimming { get; set; }
    public bool IsFlying { get; set; }
    public bool IsClimbing { get; set; }

    /// <summary>Swimming about under the water rather than on top of it.</summary>
    public bool IsDiving { get; set; }

    /// <summary>0..1 how hard a flying animal is beating its wings; 0 glides on outstretched wings.</summary>
    public float Flap { get; set; }

    /// <summary>0..1 how far a dropping animal has tipped forward to land forepaws first, easing back to 0 after touchdown.</summary>
    public float Landing { get; set; }

    /// <summary>0..1 how deep the animal has crouched to soak up a hard landing, easing back up as it recovers.</summary>
    public float Impact { get; set; }

    /// <summary>0..1 how far a long-falling animal has spread its legs out wide, as a falling cat does to slow itself.</summary>
    public float Spread { get; set; }

    /// <summary>
    /// How hard gravity pulls, in m/s². Heavier than the real 9.8 so big animals don't drift through the air like
    /// balloons: jumps keep their height but rise and fall quicker.
    /// </summary>
    public const float Gravity = 15f;

    /// <summary>How much harder gravity pulls on the way down than up, so a jump hangs at the top and then drops fast.</summary>
    public const float FallingGravity = 1.4f;

    /// <summary>Gravity on something moving up or down at <paramref name="verticalSpeed"/>.</summary>
    public static float GravityOn(float verticalSpeed) => verticalSpeed < 0f ? Gravity * FallingGravity : Gravity;

    /// <summary>How high a drop something has fallen to hit the ground at <paramref name="speed"/>.</summary>
    public static float DropHeight(float speed) => speed * speed / (2f * Gravity * FallingGravity);

    /// <summary>0..1 how far a flying animal has tucked its wings in to dive.</summary>
    public float Dive { get; set; }

    /// <summary>0..1 how far the animal has sat down, eased in as it settles and out as it gets up.</summary>
    public float Sitting { get; set; }

    /// <summary>0..1 how far the animal has lain down to rest, eased in as it settles and out as it gets up.</summary>
    public float Lying { get; set; }

    /// <summary>True while the animal is sitting or lying, or still getting up.</summary>
    public bool IsResting => Sitting > 0f || Lying > 0f;

    /// <summary>
    /// 0..1 how far the animal has gone limp in death, eased in as it falls. Its eyes close, it stops stirring, and
    /// <see cref="Animate"/> lets its joints flop into a dead weight, see <see cref="Limp(float, float)"/>, which
    /// <see cref="Flop"/> then swings and settles under gravity.
    /// </summary>
    public float Dead
    {
        get => _dead;
        set
        {
            if (value == _dead)
                return;
            _dead = value;
            if (value <= 0f)
                Unflop();
            _eyes ??= FindChildren("*", "", true, false).OfType<Node3D>().Where(n => n.IsInGroup(EyeGroup)).ToList();
            foreach (var eye in _eyes)
                eye.Scale = new Vector3(1f, Mathf.Lerp(1f, 0.12f, value), 1f);
        }
    }

    private float _dead;
    private List<Node3D>? _eyes;

    /// <summary>Group the eyes are in, so they can be closed when the animal dies.</summary>
    private const string EyeGroup = "eyes";

    /// <summary>1 while alive, easing to 0 in death: scales idle motion such as twitching ears and a swaying trunk.</summary>
    protected float Alive => 1f - Dead;

    /// <summary>Blends a joint angle from its living pose to its limp one as the animal dies, easing in like <see cref="Pose(float, float, float)"/>.</summary>
    protected float Limp(float alive, float dead) => Mathf.Lerp(alive, dead, Mathf.SmoothStep(0f, 1f, Dead));

    protected Vector3 Limp(Vector3 alive, Vector3 dead) => alive.Lerp(dead, Mathf.SmoothStep(0f, 1f, Dead));

    /// <summary>Marks a node as an eye, which flattens shut when the animal dies.</summary>
    protected static T AsEye<T>(T node) where T : Node3D
    {
        node.AddToGroup(EyeGroup);
        return node;
    }

    /// <summary>
    /// How far, in radians, the animal can turn its head to either side, neck and all. A long-necked or supple animal
    /// can look back along its flank; a mammoth or rhino, its huge head on a short, thick neck, hardly at all; an owl,
    /// with twice as many neck bones as a mammal, right round over its shoulder.
    /// </summary>
    protected virtual float HeadTurn => 1f;

    /// <summary>How much of a turn the base of the neck takes; the head turns the rest.</summary>
    protected virtual float NeckShare => 0.6f;

    /// <summary>How fast the head swings round to a new look: the time it takes to get most of the way there, in seconds.</summary>
    private const float LookSeconds = 0.15f;

    // Joints inserted above the neck and the head, which turn them about the vertical whatever pose Animate gives them.
    private Node3D? _neckSwivel;
    private Node3D? _headSwivel;
    private float _lookYaw;

    /// <summary>
    /// Turns the head, as far as the animal can, to look along <paramref name="direction"/>: the way the camera looks, so
    /// the player sees the back of its head from wherever they view it. It faces ahead again as it feeds, by
    /// <paramref name="feeding"/>, climbs or dies. Call after <see cref="Animate"/>.
    /// </summary>
    public void LookToward(Vector3 direction, float feeding, float dt)
    {
        if (_neckSwivel is null)
        {
            if (FindChild("Neck", true, false) is not Node3D neck || neck.FindChild("Head", true, false) is not Node3D head)
                return;
            _neckSwivel = Swivel(neck);
            _headSwivel = Swivel(head);
        }

        var forward = -GlobalBasis.Z with { Y = 0f };
        direction.Y = 0f;
        float target = 0f;
        if (forward.Length() > 0.1f && direction.Length() > 0.1f && !IsClimbing)
        {
            target = forward.SignedAngleTo(direction, Vector3.Up);
            // Out of reach, past the front, the head stays turned to whichever side it already is, rather than whipping
            // across as the view swings past the face.
            if (Mathf.Abs(target) > HeadTurn)
                target = (Mathf.Abs(_lookYaw) > 0.05f ? Mathf.Sign(_lookYaw) : Mathf.Sign(target)) * HeadTurn;
        }
        target *= (1f - Mathf.Clamp(feeding, 0f, 1f)) * Alive;
        _lookYaw = Mathf.Lerp(_lookYaw, target, 1f - Mathf.Exp(-dt / LookSeconds));

        Turn(_neckSwivel, _lookYaw * NeckShare);
        Turn(_headSwivel!, _lookYaw * (1f - NeckShare));
    }

    /// <summary>A joint put in above <paramref name="joint"/>, where it was, so it can turn it without upsetting its pose.</summary>
    private static Node3D Swivel(Node3D joint, string suffix = "Swivel")
    {
        var parent = joint.GetParent<Node3D>();
        var swivel = new Node3D { Name = joint.Name + suffix, Position = joint.Position };
        parent.AddChild(swivel);
        joint.Reparent(swivel, false);
        joint.Position = Vector3.Zero;
        return swivel;
    }

    /// <summary>Turns a swivel by <paramref name="yaw"/> about the vertical, however its parent is tipped.</summary>
    private static void Turn(Node3D swivel, float yaw)
    {
        var up = swivel.GetParent<Node3D>().GlobalBasis.Orthonormalized().Inverse() * Vector3.Up;
        swivel.Basis = new Basis(up.Normalized(), yaw);
    }

    /// <summary>How far the lower jaw drops, in radians, when the mouth is held open to feed.</summary>
    protected virtual float JawGape => 0.35f;

    /// <summary>
    /// What the animal is feeding on whenever it lowers its head to eat, so it can feed the way its kind does. Left
    /// unset, as for wild animals, a grazer is taken to be grazing and anything else to be eating meat.
    /// </summary>
    public Food? Meal { get; set; }

    /// <summary>
    /// How an animal feeds on one kind of food: how often and how hard it bites, and how its head and jaw move
    /// between bites. Times are in seconds and angles in radians; every motion is 0, still, unless given.
    /// </summary>
    protected readonly record struct FeedingStyle()
    {
        /// <summary>Shortest and longest wait from one bite to the next.</summary>
        public float BiteMin { get; init; } = 0.9f;
        public float BiteMax { get; init; } = 2.4f;

        /// <summary>How long a bite takes, from opening wide through snapping shut to settling again.</summary>
        public float BiteSeconds { get; init; } = 0.35f;

        /// <summary>How wide the bite opens, as a fraction of <see cref="JawGape"/>.</summary>
        public float Gape { get; init; } = 1f;

        /// <summary>How far open, as a fraction of the bite, the mouth is held between bites: 0 keeps it shut to chew.</summary>
        public float Held { get; init; } = 1f;

        /// <summary>
        /// Laps a second, for an animal that drinks with its tongue: the jaw flicks open and shut this fast instead of
        /// biting, and the head bobs by <see cref="Tug"/> with each lap. 0 bites instead.
        /// </summary>
        public float Laps { get; init; }

        /// <summary>How far the head pulls back and up just after each bite, to tear off a mouthful.</summary>
        public float Tug { get; init; }

        /// <summary>How far the head shakes from side to side as it tugs, to rip meat loose, and how fast.</summary>
        public float Shake { get; init; }
        public float ShakeRate { get; init; } = 5f;

        /// <summary>
        /// How far the head rolls onto its side to bite with the cheek teeth, and the chance at each bite of turning
        /// it over to chew on the other side.
        /// </summary>
        public float Tilt { get; init; }
        public float SwapSide { get; init; } = 0.3f;

        /// <summary>How far the head tips back after each bite, to swallow, as birds do, which can't suck down water.</summary>
        public float Gulp { get; init; }

        /// <summary>How far the lower jaw swings from side to side between bites, chewing, and chews a second.</summary>
        public float Grind { get; init; }
        public float GrindRate { get; init; } = 1.5f;

        /// <summary>How far the head sweeps from side to side, cropping a swathe as it goes, and seconds for each sweep.</summary>
        public float Sweep { get; init; }
        public float SweepSeconds { get; init; } = 4f;
    }

    /// <summary>
    /// A cat or a dog drinks by flicking its tongue into the water and snapping up the column of water it lifts, a few
    /// laps a second, its head bobbing with each.
    /// </summary>
    protected static readonly FeedingStyle Lapping = new() { Laps = 3.5f, Gape = 0.35f, Tug = 0.03f };

    /// <summary>
    /// A grazer drinks by sucking water up through its barely parted lips, its throat gulping it down every second or so.
    /// </summary>
    protected static readonly FeedingStyle Sipping = new()
    {
        BiteMin = 0.9f, BiteMax = 1.3f, BiteSeconds = 0.5f, Gape = 0.15f, Held = 0.3f, Gulp = 0.04f,
    };

    /// <summary>How this kind of animal feeds on <paramref name="food"/>; by default it laps water and bites anything else.</summary>
    protected virtual FeedingStyle FeedingStyleFor(Food food) => food == Food.Water ? Lapping : new FeedingStyle();

    private readonly List<Node3D> _jaws = [];
    private float _sinceBite;
    private float _nextBite = 1f;
    private float _feedClock;
    private float _biteSide = 1f;
    private float _tilt;

    // Joints put in above the neck and the head, which move them as the animal feeds whatever pose Animate gives them.
    private Node3D? _neckFeed;
    private Node3D? _headFeed;
    private bool _feedJointsAdded;

    /// <summary>How far through the wait from one bite to the next the animal is, 0 at a bite and nearing 1 by the next.</summary>
    protected float FeedCycle => Mathf.Clamp(_sinceBite / _nextBite, 0f, 1f);

    /// <summary>
    /// A lower jaw hinged at <paramref name="hinge"/>, at the back of the mouth, which <see cref="Chew"/> drops open to
    /// feed. Hang the chin, lower lip or lower beak off it, built where it was before, less the hinge.
    /// </summary>
    protected Node3D Jaw(Node3D parent, Vector3 hinge)
    {
        var jaw = Pivot(parent, "Jaw", hinge);
        _jaws.Add(jaw);
        return jaw;
    }

    /// <summary>
    /// The inside of the mouth: a dark lining with a pink tongue resting on the lower jaw, hidden in the closed mouth
    /// and showing when it opens. <paramref name="radii"/> is the tongue's size and <paramref name="position"/> where
    /// it lies on the jaw.
    /// </summary>
    protected static void MouthInside(Node3D jaw, Vector3 radii, Vector3 position)
    {
        var lining = new StandardMaterial3D { AlbedoColor = new Color(0.22f, 0.07f, 0.07f), Roughness = 0.6f };
        var tongue = new StandardMaterial3D { AlbedoColor = new Color(0.72f, 0.36f, 0.38f), Roughness = 0.4f };
        Attach(jaw, "Mouth", Ellipsoid(radii * new Vector3(1.2f, 1.5f, 1.1f), 12, 8, lining), position + new Vector3(0, -radii.Y * 0.6f, 0));
        Attach(jaw, "Tongue", Ellipsoid(radii, 12, 8, tongue), position);
    }

    /// <summary>
    /// Feeding with its head down, the animal feeds the way its kind does on its <see cref="Meal"/> (see
    /// <see cref="FeedingStyleFor"/>): biting off mouthfuls, tearing them loose and chewing them, or lapping or sipping
    /// water. Any other time the mouth stays closed and the head still. <paramref name="eat"/> is the head-dip passed
    /// to <see cref="Animate"/>; call after posing the head.
    /// </summary>
    protected void Chew(float eat, float dt)
    {
        if (!_feedJointsAdded)
        {
            _feedJointsAdded = true;
            if (FindChild("Neck", true, false) is Node3D neck && neck.FindChild("Head", true, false) is Node3D head)
            {
                _neckFeed = Swivel(neck, "Feed");
                _headFeed = Swivel(head, "Feed");
            }
        }

        var style = FeedingStyleFor(Meal ?? (Stats.CanGraze ? Food.Grass : Food.Meat));
        float feeding = Mathf.SmoothStep(0.4f, 0.9f, eat) * Alive;
        _feedClock += dt;
        _sinceBite += dt;
        if (_sinceBite >= _nextBite)
        {
            _sinceBite = 0f;
            _nextBite = (float)GD.RandRange(style.BiteMin, style.BiteMax);
            if (GD.Randf() < style.SwapSide)
                _biteSide = -_biteSide;
        }

        float open, tug, gulp = 0f, grind = 0f;
        if (style.Laps > 0f)
        {
            // Each lap flicks the jaw open and shut and bobs the head up with the water it lifts.
            float lap = 0.5f - 0.5f * Mathf.Cos(_feedClock * style.Laps * Mathf.Tau);
            open = lap;
            tug = lap;
        }
        else
        {
            // A bite opens wide, snaps shut and settles back to however the mouth is held. Just after it the head tugs
            // the mouthful loose, then tips back to swallow, and the jaw chews until the next bite.
            float t = _sinceBite / style.BiteSeconds;
            open = t < 0.4f ? Mathf.Lerp(style.Held, 1f, Mathf.SmoothStep(0f, 1f, t / 0.4f))
                : t < 0.6f ? 1f - Mathf.SmoothStep(0f, 1f, (t - 0.4f) / 0.2f)
                : Mathf.Lerp(0f, style.Held, Mathf.SmoothStep(0f, 1f, (t - 0.6f) / 0.4f));
            tug = Bump(t, 0.5f, 1.5f);
            gulp = Bump(t, 0.8f, 2.6f);
            grind = Mathf.SmoothStep(1f, 1.5f, t) * Mathf.Sin(_feedClock * style.GrindRate * Mathf.Tau);
        }

        foreach (var jaw in _jaws)
            jaw.Rotation = new Vector3(-JawGape * style.Gape * open * feeding, style.Grind * grind * feeding, 0);

        _tilt = Mathf.MoveToward(_tilt, _biteSide * style.Tilt, dt * 1.5f);
        if (_neckFeed is null || _headFeed is null)
            return;
        float sweep = style.Sweep * Mathf.Sin(_feedClock * Mathf.Tau / style.SweepSeconds);
        float shake = style.Shake * Mathf.Sin(_feedClock * style.ShakeRate * Mathf.Tau) * tug;
        _neckFeed.Rotation = new Vector3(style.Tug * tug, sweep + shake, 0) * feeding;
        _headFeed.Rotation = new Vector3(style.Gulp * gulp, 0, _tilt) * feeding;
    }

    /// <summary>A smooth hump from 0 up to 1 and back down to 0 as <paramref name="t"/> runs from <paramref name="from"/> to <paramref name="to"/>.</summary>
    private static float Bump(float t, float from, float to) =>
        t <= from || t >= to ? 0f : Mathf.Sin((t - from) / (to - from) * Mathf.Pi);

    /// <summary>Eases the sitting and lying poses towards <paramref name="posture"/>, taking <paramref name="seconds"/> to settle or get up.</summary>
    public void Settle(Posture posture, float seconds, float dt)
    {
        Sitting = Mathf.MoveToward(Sitting, posture == Posture.Sitting ? 1f : 0f, dt / seconds);
        Lying = Mathf.MoveToward(Lying, posture == Posture.Lying ? 1f : 0f, dt / seconds);
    }

    /// <summary>Blends a joint angle or offset from its standing value towards its sitting and lying ones, easing in and out.</summary>
    protected float Pose(float standing, float sitting, float lying) =>
        Mathf.Lerp(Mathf.Lerp(standing, sitting, Mathf.SmoothStep(0f, 1f, Sitting)), lying, Mathf.SmoothStep(0f, 1f, Lying));

    protected Vector3 Pose(Vector3 standing, Vector3 sitting, Vector3 lying) =>
        standing.Lerp(sitting, Mathf.SmoothStep(0f, 1f, Sitting)).Lerp(lying, Mathf.SmoothStep(0f, 1f, Lying));

    /// <summary>
    /// Where a frame must move so that, tipped nose-up by <paramref name="pitch"/>, the point <paramref name="anchor"/>
    /// in it stays where it was: e.g. the shoulders, so the forelegs stay planted while the hindquarters sink to sit.
    /// </summary>
    protected static Vector3 TipAbout(Vector3 anchor, float pitch) => anchor - anchor.Rotated(Vector3.Right, pitch);

    /// <summary>
    /// How far each joint of a two-segment leg of the given length must bend for the leg to reach
    /// <paramref name="shorten"/> less far, with the foot staying under the hip.
    /// </summary>
    protected static float FoldAngle(float shorten, float length) =>
        Mathf.Acos(Mathf.Clamp(1f - Mathf.Max(0f, shorten) / length, -1f, 1f));

    /// <summary>
    /// How far to fold a two-segment leg (the upper segment forward by the angle, the lower back by twice it) for the
    /// ankle to end up <paramref name="drop"/> below the hip. Unlike <see cref="FoldAngle"/> it takes the knee and the
    /// ankle (each relative to the joint above) as built, kinks and all, so it stays true even when folded right up.
    /// </summary>
    protected static float FoldToReach(Vector3 knee, Vector3 ankle, float drop)
    {
        float straight = 0f, folded = 2.5f;
        for (int i = 0; i < 20; i++)
        {
            float fold = (straight + folded) / 2f;
            float reach = -(knee.Rotated(Vector3.Right, fold) + ankle.Rotated(Vector3.Right, -fold)).Y;
            if (reach > drop)
                straight = fold;
            else
                folded = fold;
        }
        return (straight + folded) / 2f;
    }

    protected static ShaderMaterial HairMaterial() => new() { Shader = GD.Load<Shader>("res://shaders/fur.gdshader") };

    protected static Node3D Pivot(Node3D parent, string name, Vector3 position)
    {
        var pivot = new Node3D { Name = name, Position = position };
        parent.AddChild(pivot);
        return pivot;
    }

    protected static MeshInstance3D Attach(Node3D parent, string name, Mesh mesh, Vector3 position = default)
    {
        var instance = new MeshInstance3D { Name = name, Mesh = mesh, Position = position };
        parent.AddChild(instance);
        return instance;
    }

    private static Vector3 Direction(float theta, float phi) =>
        new(Mathf.Cos(phi) * Mathf.Cos(theta), Mathf.Sin(phi), Mathf.Cos(phi) * Mathf.Sin(theta));

    /// <summary>Outward normal of a deformed sphere, from finite differences of the shape function.</summary>
    private static Vector3 ShapeNormal(Func<Vector3, Vector3> shape, float theta, float phi)
    {
        const float e = 0.002f;
        var p = shape(Direction(theta, phi));
        var dTheta = shape(Direction(theta + e, phi)) - p;
        var dPhi = shape(Direction(theta, phi + e)) - p;
        var n = dPhi.Cross(dTheta);

        // Only at the poles, where dTheta vanishes, is the cross product too short to trust. The test is relative
        // to the shape's size so small shapes, whose differences are tiny anyway, still get proper normals.
        float reach = dPhi.LengthSquared();
        return n.LengthSquared() > reach * reach * 1e-6f ? n.Normalized() : (phi > 0 ? Vector3.Up : Vector3.Down);
    }

    /// <summary>
    /// A sphere pushed through <paramref name="shape"/>, which maps unit directions to surface points. When
    /// <paramref name="colouring"/> is given it paints each vertex from its position and normal, for materials that
    /// use vertex colour.
    /// </summary>
    public static ArrayMesh Ellipsoid(Func<Vector3, Vector3> shape, int segments, int rings, Material material,
        Func<Vector3, Vector3, Color>? colouring = null)
    {
        var mesh = new MeshBuilder();
        var index = new int[rings + 1, segments + 1];
        for (int r = 0; r <= rings; r++)
        {
            float phi = -Mathf.Pi / 2f + Mathf.Pi * r / rings;
            for (int s = 0; s <= segments; s++)
            {
                float theta = Mathf.Tau * s / segments;
                var point = shape(Direction(theta, phi));
                var normal = ShapeNormal(shape, theta, phi);
                index[r, s] = mesh.Add(point, normal, colouring?.Invoke(point, normal) ?? Colors.White);
            }
        }
        for (int r = 0; r < rings; r++)
        for (int s = 0; s < segments; s++)
            mesh.Quad(index[r, s], index[r, s + 1], index[r + 1, s + 1], index[r + 1, s]);
        return mesh.Commit(material);
    }

    /// <summary>A plain axis-aligned ellipsoid with the given half-extents.</summary>
    public static ArrayMesh Ellipsoid(Vector3 radii, int segments, int rings, Material material,
        Func<Vector3, Vector3, Color>? colouring = null) =>
        Ellipsoid(u => u * radii, segments, rings, material, colouring);

    /// <summary>A tube of varying radius swept along a path, with the cross-section frame carried along the curve.</summary>
    protected static ArrayMesh Tube(Vector3[] path, float[] radii, int sides, Material material, bool capEnd)
    {
        var mesh = new MeshBuilder();
        var rings = new int[path.Length, sides + 1];
        var normal = Vector3.Zero;
        var tangent = Vector3.Zero;

        for (int i = 0; i < path.Length; i++)
        {
            tangent = (path[Math.Min(i + 1, path.Length - 1)] - path[Math.Max(i - 1, 0)]).Normalized();
            var helper = i == 0 ? (Mathf.Abs(tangent.Y) < 0.9f ? Vector3.Up : Vector3.Right) : normal;
            normal = (helper - tangent * helper.Dot(tangent)).Normalized();
            var binormal = tangent.Cross(normal);

            for (int s = 0; s <= sides; s++)
            {
                float a = Mathf.Tau * s / sides;
                var outward = normal * Mathf.Cos(a) + binormal * Mathf.Sin(a);
                rings[i, s] = mesh.Add(path[i] + outward * radii[i], outward, Colors.White);
            }
        }

        for (int i = 0; i < path.Length - 1; i++)
        for (int s = 0; s < sides; s++)
            mesh.Quad(rings[i, s], rings[i + 1, s], rings[i + 1, s + 1], rings[i, s + 1]);

        if (capEnd)
        {
            int last = path.Length - 1;
            int centre = mesh.Add(path[last], tangent, Colors.White);
            for (int s = 0; s < sides; s++)
                mesh.Tri(centre, rings[last, s + 1], rings[last, s]);
        }

        return mesh.Commit(material);
    }

    /// <summary>Random points on a deformed sphere's surface that satisfy <paramref name="where"/> (tested on the unit direction).</summary>
    protected static List<(Vector3 Root, Vector3 Normal)> OnShape(RandomNumberGenerator rng, Func<Vector3, Vector3> shape, int count, Func<Vector3, bool> where)
    {
        var roots = new List<(Vector3, Vector3)>(count);
        while (roots.Count < count)
        {
            float phi = Mathf.Asin(rng.RandfRange(-1f, 1f));
            float theta = rng.Randf() * Mathf.Tau;
            var u = Direction(theta, phi);
            if (where(u))
                roots.Add((shape(u), ShapeNormal(shape, theta, phi)));
        }
        return roots;
    }

    /// <summary>Random points around a vertical cylinder between two heights.</summary>
    protected static List<(Vector3 Root, Vector3 Normal)> OnTube(RandomNumberGenerator rng, int count, float radius, float topY, float bottomY, float zOffset = 0f)
    {
        var roots = new List<(Vector3, Vector3)>(count);
        for (int i = 0; i < count; i++)
        {
            float a = rng.Randf() * Mathf.Tau;
            var outward = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            roots.Add((outward * radius + new Vector3(0, rng.RandfRange(bottomY, topY), zOffset), outward));
        }
        return roots;
    }

    /// <summary>Random points around a straight tube from <paramref name="from"/> to <paramref name="to"/>, whose radius tapers between the ends.</summary>
    protected static List<(Vector3 Root, Vector3 Normal)> OnSegment(RandomNumberGenerator rng, int count, Vector3 from, Vector3 to, float fromRadius, float toRadius)
    {
        var axis = (to - from).Normalized();
        var across = axis.Cross(Mathf.Abs(axis.X) < 0.9f ? Vector3.Right : Vector3.Up).Normalized();
        var roots = new List<(Vector3, Vector3)>(count);
        for (int i = 0; i < count; i++)
        {
            float t = rng.Randf();
            var outward = across.Rotated(axis, rng.Randf() * Mathf.Tau);
            roots.Add((from.Lerp(to, t) + outward * Mathf.Lerp(fromRadius, toRadius, t), outward));
        }
        return roots;
    }

    /// <summary>
    /// Hair: a thin, two-segment strand at each root, drifting in <paramref name="drift"/>'s direction and
    /// bending down under its own weight, shading from <paramref name="rootColour"/> to <paramref name="tipColour"/>.
    /// Strands take the normal of the surface beneath them so they are lit like that surface rather than as
    /// individual slivers. When <paramref name="colouring"/> is given, both colours are multiplied by its colour at
    /// each root, so the hair can carry the coat's pattern.
    /// </summary>
    protected static ArrayMesh Strands(RandomNumberGenerator rng, List<(Vector3 Root, Vector3 Normal)> roots, Vector3 drift,
        float minLength, float maxLength, Color rootColour, Color tipColour, Material hair, float width = 0.07f,
        Func<Vector3, Vector3, Color>? colouring = null)
    {
        var mesh = new MeshBuilder();
        foreach (var (root, normal) in roots)
        {
            var under = colouring?.Invoke(root, normal) ?? Colors.White;
            float length = rng.RandfRange(minLength, maxLength);
            var jitter = new Vector3(rng.RandfRange(-1f, 1f), rng.RandfRange(-1f, 1f), rng.RandfRange(-1f, 1f)) * 0.2f;
            var direction = (normal * 0.35f + drift + jitter).Normalized();
            var bent = (direction + Vector3.Down * 0.7f).Normalized();

            var mid = root + direction * length * 0.5f;
            var tip = mid + bent * length * 0.5f;
            var side = direction.Cross(normal).Normalized();
            if (side.LengthSquared() < 0.5f)
                side = direction.Cross(Vector3.Right).Normalized();

            float shade = rng.RandfRange(0.8f, 1.2f);
            var rootShade = Shade(rootColour * under, shade);
            var midShade = Shade(rootColour.Lerp(tipColour, 0.5f) * under, shade);
            var tipShade = Shade(tipColour * under, shade);

            int r0 = mesh.Add(root - side * width * 0.5f, normal, rootShade, new Vector2(0, 0));
            int r1 = mesh.Add(root + side * width * 0.5f, normal, rootShade, new Vector2(0, 0));
            int m0 = mesh.Add(mid - side * width * 0.36f, normal, midShade, new Vector2(0.5f, 0));
            int m1 = mesh.Add(mid + side * width * 0.36f, normal, midShade, new Vector2(0.5f, 0));
            int t = mesh.Add(tip, normal, tipShade, new Vector2(1, 0));
            mesh.Quad(r0, r1, m1, m0);
            mesh.Tri(m0, m1, t);
        }
        return mesh.Commit(hair);
    }

    /// <summary>
    /// A round eye set into the face at <paramref name="position"/>, looking along -Z after <paramref name="rotation"/>:
    /// a thin dark rim, a coloured iris under a glossy cornea, a round pupil filling <paramref name="pupil"/> of the
    /// iris, and a bright point of reflected light.
    /// </summary>
    protected static void Eye(Node3D parent, Vector3 position, Vector3 rotation, float radius, Color iris, float pupil)
    {
        var rim = new StandardMaterial3D { AlbedoColor = new Color(0.04f, 0.035f, 0.035f), Roughness = 0.5f };
        StandardMaterial3D Glossy(Color colour) => new()
        {
            AlbedoColor = colour, Roughness = 0.3f, ClearcoatEnabled = true, Clearcoat = 1f, ClearcoatRoughness = 0f,
        };
        var catchlight = new StandardMaterial3D { AlbedoColor = Colors.White, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };

        var socket = AsEye(Pivot(parent, "Eye", position));
        socket.Rotation = rotation;
        float depth = radius * 0.5f;
        Attach(socket, "Rim", Ellipsoid(new Vector3(radius * 1.15f, radius * 1.15f, depth), 16, 10, rim));
        Attach(socket, "Iris", Ellipsoid(new Vector3(radius, radius, depth), 16, 10, Glossy(iris)), new Vector3(0, 0, -radius * 0.12f));
        Attach(socket, "Pupil", Ellipsoid(new Vector3(radius * pupil, radius * pupil, radius * 0.1f), 12, 8, Glossy(new Color(0.01f, 0.01f, 0.01f))),
            new Vector3(0, 0, -depth - radius * 0.06f));
        Attach(socket, "Catchlight", Ellipsoid(new Vector3(radius * 0.14f, radius * 0.14f, radius * 0.04f), 8, 6, catchlight),
            new Vector3(radius * 0.25f, radius * 0.3f, -depth - radius * 0.12f));
    }

    private static Color Shade(Color colour, float amount) =>
        new(colour.R * amount, colour.G * amount, colour.B * amount);
}
