using Godot;

namespace IceAgeWorld;

/// <summary>
/// Smilodon, the sabre-toothed cat, built to the great South American Smilodon populator: about 1.2 m at the shoulder
/// and heavier than any lion, with a short, bear-like body, massive forelegs for wrestling prey to the ground, a back
/// that slopes down to shorter hind legs, and a stubby bobtail. Its upper canines, the sabres, hang some 18 cm below
/// the lip, guarded by a flange on the chin, and it could gape its jaws twice as wide as a lion to drive them in. No
/// one knows its coat; it is drawn tawny with faint spots, like many forest-edge ambushers. It hunts alone, by
/// ambush: rather than run prey down, it leaps on, holds it with its forelegs and bites. The model faces -Z.
/// </summary>
public partial class Smilodon : BigCat
{
    public override string DisplayName => "Smilodon";
    public override string YoungName => "Smilodon cub";

    // Smilodon fossils show little difference between the sexes, unlike today's big cats.
    protected override float FemaleSize => 0.97f;

    public override AnimalStats Stats { get; } = new()
    {
        // Short-legged and heavy, built to burst out of cover rather than chase.
        Scores = new() { JumpHeight = 5, JumpLength = 6, LandSpeed = 6, WaterSpeed = 3, Agility = 6, Stamina = 4 },
        // It seized prey in its huge forelegs and held it down while it bit, so it can leap on and hold on.
        Abilities = Ability.Hunt | Ability.Grip,
        Strength = 4f,
        FloatDepth = 0.75f * BodyScale,
        WadeDepth = 0.45f * BodyScale,
        MouthDistance = 1.05f * BodyScale,
        EatReach = 0.55f * BodyScale,
        CanGraze = false,
        BodyRadius = 0.42f * BodyScale,
        BodyHeight = 1.35f * BodyScale,
        BackHeight = 0.9f,
        CameraHeight = 1.2f * BodyScale,
        CameraDistance = 4.5f * BodyScale,
    };

    protected override int Seed => 89;
    protected override float BodyCentre => 0.95f;
    protected override Vector3 BodySize => new(0.27f, 0.32f, 0.68f);
    protected override float BackSlope => 0.08f;

    protected override Color Coat => new(0.74f, 0.56f, 0.36f);
    protected override Color Belly => new(0.92f, 0.85f, 0.72f);
    protected override Color Dark => new(0.16f, 0.11f, 0.08f);
    protected override Color? Spots => new(0.55f, 0.4f, 0.25f);
    // Drawn a little cuter than the fossil skull suggests: a rounder head on a shorter muzzle, with big eyes, wide
    // pupils and bigger, rounder ears, the proportions that read as young and friendly.
    protected override float HeadScale => 1.3f;
    protected override float MuzzleScale => 0.75f;
    protected override float EyeScale => 1.4f;
    protected override float EarScale => 1.25f;
    protected override float Pupil => 0.58f;

    // The sabres need a huge gape to clear the prey: Smilodon could open its jaws to some 110 degrees.
    protected override float JawGape => 1f;

    // Its sabres get in the way of tearing, so a smilodon gapes enormously and shears carefully from the side, slowly.
    protected override FeedingStyle FeedingStyleFor(Food food) => food == Food.Meat
        ? base.FeedingStyleFor(food) with { BiteMin = 1.5f, BiteMax = 2.5f, BiteSeconds = 0.55f, Tilt = 0.35f, Tug = 0.2f }
        : base.FeedingStyleFor(food);

    // The upper legs run right up into the body to the shoulder blade and hip, so the shoulders and thighs swell out
    // of the flanks instead of the legs hanging beneath the body like posts.
    protected override float FrontUpperLength => 0.5f;
    protected override float FrontLowerLength => 0.34f;
    protected override float BackUpperLength => 0.46f;
    protected override float BackLowerLength => 0.29f;
    protected override float ShoulderOffset => 0.42f;
    protected override float HipOffset => 0.4f;
    protected override float LegSpread => 0.15f;
    protected override float LieDrop => 0.6f;
    protected override float DeadFlank => 0.3f;
    protected override float DeadRoll => BodyCentre;

    // Forelegs far heavier than the hind ones, for grappling.
    protected override (float Upper, float Knee, float Ankle) FrontLeg => (0.135f, 0.095f, 0.072f);
    protected override (float Upper, float Knee, float Ankle) BackLeg => (0.12f, 0.075f, 0.058f);
    protected override Vector3 LegMuscle => new(0.085f, 0.26f, 0.15f);

    // A short bobtail, like a lynx's, drooping from the rump with its stub of a tip curled up.
    protected override Vector3[] TailPath =>
        [Vector3.Zero, new(0, -0.04f, 0.08f), new(0, -0.11f, 0.14f), new(0, -0.16f, 0.2f), new(0, -0.15f, 0.25f)];
    protected override float[] TailRadii => [0.05f, 0.045f, 0.04f, 0.034f, 0.026f];

    /// <summary>
    /// The sabres: long, blade-like upper canines, flattened side to side and curving gently back, hanging well below
    /// the chin, and the bony flange on each side of the chin that sheathed their tips with the mouth shut.
    /// </summary>
    protected override void BuildMouthExtras(Node3D snout, Node3D jaw, StandardMaterial3D face)
    {
        var tooth = new StandardMaterial3D { AlbedoColor = new Color(0.93f, 0.9f, 0.8f), Roughness = 0.35f };
        foreach (float side in new[] { -1f, 1f })
        {
            var sabre = Pivot(snout, "Sabre", new Vector3(side * 0.034f, -0.06f, -0.112f));
            sabre.Scale = new Vector3(0.5f, 1f, 1f);
            Attach(sabre, "Tooth", Tube([Vector3.Zero, new(0, -0.07f, 0.004f), new(0, -0.14f, 0.022f), new(0, -0.19f, 0.055f)],
                [0.018f, 0.017f, 0.012f, 0.002f], 10, tooth, capEnd: true));

            Attach(jaw, "Flange", Ellipsoid(new Vector3(0.016f, 0.04f, 0.03f), 10, 8, face, (_, _) => Belly),
                new Vector3(side * 0.036f, -0.045f, -0.16f));
        }
    }
}
