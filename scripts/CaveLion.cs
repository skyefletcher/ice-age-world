using Godot;

namespace IceAgeWorld;

/// <summary>
/// A cave lion, the biggest cat of the ice-age steppe, some tenth bigger than an African lion: about 1.2 m at the
/// shoulder and over 2 m long before the tail, built long and leggy. The cave paintings at Chauvet show it maneless,
/// but it is drawn here as the lion everyone knows: a plain, tawny gold coat, cream beneath, a long tail ending in a
/// dark tuft, dark-backed ears, and for a grown male a great shaggy mane, golden round the face and darkening to brown
/// over the neck and chest. The lioness has none. It lives in a pride that hunts together. The model faces -Z.
/// </summary>
public partial class CaveLion : BigCat
{
    public override string DisplayName => "Cave lion";

    // A lion heaves at its meat harder than a smaller cat, bracing to tear off big strips with each shearing bite.
    protected override FeedingStyle FeedingStyleFor(Food food) =>
        food == Food.Meat ? base.FeedingStyleFor(food) with { Tug = 0.32f, Shake = 0.08f } : base.FeedingStyleFor(food);
    public override string YoungName => "Cave lion cub";

    // A lioness weighs about a third less than a lion.
    protected override float FemaleSize => 0.88f;

    public override AnimalStats Stats { get; } = new()
    {
        // A lion sprints at nearly 80 km/h, but only for a short dash before it tires; it swims only if it has to.
        Scores = new() { JumpHeight = 6, JumpLength = 7, LandSpeed = 8, WaterSpeed = 3, Agility = 7, Stamina = 4 },
        // Lions are the only cats that live and hunt together, and a pride of lionesses brings down prey far bigger
        // than any one of them.
        Abilities = Ability.Pack | Ability.Hunt,
        LeaderSize = 1.15f,
        Companions = 3,
        Strength = 3f,
        FloatDepth = 0.75f * BodyScale,
        WadeDepth = 0.45f * BodyScale,
        MouthDistance = 1.2f * BodyScale,
        EatReach = 0.55f * BodyScale,
        CanGraze = false,
        BodyRadius = 0.42f * BodyScale,
        BodyHeight = 1.35f * BodyScale,
        BackHeight = 0.9f,
        CameraHeight = 1.2f * BodyScale,
        CameraDistance = 4.5f * BodyScale,
    };

    protected override int Seed => 83;
    protected override float BodyCentre => 0.96f;
    protected override Vector3 BodySize => new(0.23f, 0.3f, 0.78f);

    // A warm, sandy gold coat, paling to cream on the belly, throat and muzzle, with dark brown on the tail tuft, ear
    // backs and lip line.
    protected override Color Coat => new(0.86f, 0.66f, 0.4f);
    protected override Color Belly => new(0.97f, 0.92f, 0.82f);
    protected override Color Dark => new(0.3f, 0.2f, 0.13f);

    // The mane is golden where it frames the face and darkens to brown towards its back and underneath, over the chest.
    private static readonly Color ManeGold = new(0.95f, 0.55f, 0.16f);
    private static readonly Color ManeBrown = new(0.5f, 0.27f, 0.1f);
    protected override float MuzzleScale => 0.98f;
    // A lion has a big, broad head for its body.
    protected override float HeadScale => 1.3f;

    // The upper legs run right up into the body to the shoulder blade and hip, as on a real cat, so the shoulders and
    // thighs swell out of the flanks instead of the legs hanging beneath the body like posts.
    protected override float FrontUpperLength => 0.5f;
    protected override float FrontLowerLength => 0.35f;
    protected override float BackUpperLength => 0.52f;
    protected override float BackLowerLength => 0.35f;
    protected override float ShoulderOffset => 0.5f;
    protected override float HipOffset => 0.52f;
    protected override float LegSpread => 0.13f;
    protected override float LieDrop => 0.62f;
    protected override float DeadFlank => 0.25f;
    protected override float DeadRoll => BodyCentre;

    protected override (float Upper, float Knee, float Ankle) FrontLeg => (0.11f, 0.075f, 0.06f);
    protected override (float Upper, float Knee, float Ankle) BackLeg => (0.12f, 0.07f, 0.055f);
    // Long, flat slabs of muscle over the shoulder and thigh, lying along the flank rather than bulging from it.
    protected override Vector3 LegMuscle => new(0.075f, 0.25f, 0.14f);

    // A long tail, nearly a metre, hanging in a curve almost to the hocks, its end turned up and out in the J-shaped
    // hook a resting lion's tail keeps.
    protected override Vector3[] TailPath =>
    [
        Vector3.Zero, new(0, -0.05f, 0.16f), new(0, -0.22f, 0.29f), new(0, -0.44f, 0.35f), new(0, -0.62f, 0.37f),
        new(0, -0.73f, 0.42f), new(0, -0.75f, 0.5f), new(0, -0.68f, 0.57f),
    ];
    protected override float[] TailRadii => [0.045f, 0.037f, 0.032f, 0.028f, 0.025f, 0.023f, 0.022f, 0.021f];
    protected override bool TailTuft => true;

    /// <summary>
    /// A grown male's mane: a thick ruff of long hair framing the face from the brow round under the chin, and a shaggy
    /// mass over the whole neck, hanging down onto the chest and back over the shoulders. Each sits on a dense
    /// underlayer so it reads as one solid mass. Young males only grow it in their third year, so a cub has none.
    /// </summary>
    protected override void BuildExtras(RandomNumberGenerator rng)
    {
        var under = ProceduralTextures.Fur(Seed + 7, ManeBrown, ManeGold.Lerp(ManeBrown, 0.4f), 3f);

        // The ruff, behind the face on the head, so it nods with it. It leaves the face, ears and crown clear.
        var ruff = ForMales(Pivot(Head, "Mane", Vector3.Zero), grownAt: 0.8f);
        var ruffSize = new Vector3(0.2f, 0.19f, 0.1f);
        var ruffAt = new Vector3(0, -0.04f, 0.01f);
        Vector3 RuffShape(Vector3 u) => u * ruffSize + ruffAt;
        Attach(ruff, "Under", Ellipsoid(RuffShape, 20, 14, under));
        // The hair stands straight out from the ruff all round, so it frames the face like a sunburst.
        Attach(ruff, "Hair", Strands(rng, OnShape(rng, RuffShape, 2200, u => u.Z > -0.75f), new Vector3(0, 0.15f, 0.1f),
            0.09f, 0.15f, Colors.White, Colors.White * 1.1f, Hair, width: 0.026f,
            colouring: (p, n) => ManeGold.Lerp(ManeBrown, Mathf.Clamp(Mathf.SmoothStep(0.02f, 0.14f, p.Z) * 0.6f + Mathf.SmoothStep(0f, -0.9f, n.Y) * 0.5f, 0f, 1f))));

        // Over the neck, from the head back past the shoulders, longest and darkest beneath where it hangs over the chest.
        var mane = ForMales(Pivot(Neck, "Mane", Vector3.Zero), grownAt: 0.8f);
        var from = new Vector3(0, 0.0f, 0.22f);
        var to = Head.Position + new Vector3(0, -0.02f, 0.02f);
        Attach(mane, "Under", Tube([from, to], [0.22f, 0.19f], 16, under, capEnd: false));
        Attach(mane, "Hair", Strands(rng, OnSegment(rng, 2600, from, to, 0.22f, 0.19f), new Vector3(0, -0.25f, 0.35f),
            0.1f, 0.17f, Colors.White, Colors.White * 1.1f, Hair, width: 0.026f,
            colouring: (p, n) => ManeGold.Lerp(ManeBrown, Mathf.Clamp(Mathf.SmoothStep(0.3f, -0.8f, n.Y) * 0.8f + Mathf.SmoothStep(-0.2f, 0.2f, p.Z) * 0.3f, 0f, 1f))));
    }
}
