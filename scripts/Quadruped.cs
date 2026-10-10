using System;
using Godot;

namespace IceAgeWorld;

/// <summary>
/// Shared skeleton and movement for the big four-legged animals built from it (reindeer, moose and polar bear): a
/// frame carrying the body, four two-segment legs with feet, a neck and head, and a tail. It walks with diagonal
/// pairs of legs, lengthening into a bounding gallop at speed; lowers its head and bends its forelegs to drink or
/// graze; sits and lies down; and goes limp on its side in death. Each animal supplies its own proportions and builds
/// its own body, head, feet and tail; deer bed down with their forelegs folded under them, while a bear sits up on
/// its haunches like a dog and lies sphinx-like. The model faces -Z.
/// </summary>
public abstract partial class Quadruped : Animal
{
    // Leg lengths, from hip to knee and knee to ankle, and the height of the foot (hoof or paw) below a straight leg.
    protected abstract float FrontUpperLength { get; }
    protected abstract float FrontLowerLength { get; }
    protected abstract float BackUpperLength { get; }
    protected abstract float BackLowerLength { get; }
    protected abstract float FootHeight { get; }

    /// <summary>How far ahead of and behind the body's middle the shoulders and hips are, and how far apart the legs.</summary>
    protected abstract float ShoulderOffset { get; }
    protected abstract float HipOffset { get; }
    protected abstract float LegSpread { get; }

    /// <summary>How far the body sinks to lie down, with the belly on the ground.</summary>
    protected abstract float LieDrop { get; }

    /// <summary>Dead, how high the body's middle lies off the ground on its side, and how far it rolls over sideways.</summary>
    protected abstract float DeadFlank { get; }
    protected abstract float DeadRoll { get; }

    /// <summary>How quickly the legs turn over for a given speed: long-legged animals stride slowly.</summary>
    protected virtual float Cadence => 3f;

    /// <summary>How far the body sinks and tips forward, and the neck bends down, to drink or graze.</summary>
    protected virtual float CrouchDrop => 0.15f;
    protected virtual float CrouchPitch => 0.25f;
    protected virtual float HeadDown => -1.5f;

    /// <summary>
    /// True for an animal that sits up on its haunches with its forelegs straight, as bears and dogs do. Deer can't:
    /// asked to sit, they bed down with their legs folded under them and their head up.
    /// </summary>
    protected virtual bool SitsUp => false;
    protected virtual float SitPitch => 0.65f;

    /// <summary>How the neck and head are held lying down: deer lay their head forward along the ground to sleep.</summary>
    protected virtual float LieNeck => 0.1f;
    protected virtual float LieHead => -0.1f;

    protected Node3D Frame = null!;
    protected Node3D Neck = null!;
    protected Node3D Head = null!;
    protected Node3D[] UpperLegs = [];
    protected Node3D[] LowerLegs = [];
    protected Node3D[] Feet = [];
    protected ShaderMaterial Hair = null!;
    protected float WalkCycle;
    protected float Clock;

    protected float FrontHipHeight => FrontUpperLength + FrontLowerLength + FootHeight;
    protected float BackHipHeight => BackUpperLength + BackLowerLength + FootHeight;

    // Walking moves diagonal pairs of legs together; galloping bounds with the front pair, then the back pair.
    private static readonly float[] WalkPhase = [0f, Mathf.Pi, Mathf.Pi, 0f];
    private static readonly float[] GallopPhase = [0f, 0.3f, Mathf.Pi, Mathf.Pi + 0.3f];

    public override void _Ready()
    {
        Hair = HairMaterial();
        Frame = Pivot(this, "Frame", Vector3.Zero);
        Build();
    }

    /// <summary>Builds the body, neck and head, legs (with <see cref="BuildLegs"/>) and tail onto <see cref="Frame"/>.</summary>
    protected abstract void Build();

    /// <summary>Moves the parts only this animal has, such as ears and tail, after the body, legs and neck are posed.</summary>
    protected abstract void AnimateExtras(float stride, float run, float dt);

    /// <summary>Lets the animal's own parts go limp once it is dead.</summary>
    protected virtual void GoLimpExtras() { }

    public override void Animate(float speed, float stride, float eat, float dt)
    {
        Clock += dt;

        // Long legs take long strides, so the cadence rises more slowly than speed.
        WalkCycle += Mathf.Sqrt(speed) * Cadence * dt;
        float run = Mathf.Clamp((speed - Stats.WalkSpeed) / Mathf.Max(0.1f, Stats.SprintSpeed - Stats.WalkSpeed), 0f, 1f);

        float drop = eat * CrouchDrop;
        float pitch = eat * CrouchPitch;
        float bob = Mathf.Abs(Mathf.Sin(WalkCycle)) * stride * Mathf.Lerp(0.015f, 0.06f, run);

        // Sitting up tips the body back about the shoulders, so the forelegs stay planted while the rump sinks to the
        // ground. Bedding down, the whole body sinks level onto its folded legs.
        var sitShift = TipAbout(new Vector3(0, FrontHipHeight, -ShoulderOffset), SitPitch);
        var bedded = new Vector3(0, -LieDrop, 0);
        Frame.Position = Pose(new Vector3(0, bob - drop, 0), SitsUp ? sitShift : bedded, bedded);
        Frame.Rotation = new Vector3(Pose(-pitch + Mathf.Sin(WalkCycle) * run * 0.05f, SitsUp ? SitPitch : 0f, 0f), 0, 0);

        float amplitude = Mathf.Lerp(0.35f, 0.75f, run) * stride;
        float frontFold = FoldAngle(drop + ShoulderOffset * Mathf.Sin(pitch), FrontUpperLength + FrontLowerLength);
        float backFold = FoldAngle(drop - HipOffset * Mathf.Sin(pitch), BackUpperLength + BackLowerLength);

        // Resting, the hind legs fold right up, far enough to bring the feet under the lowered hips.
        float sitHip = (new Vector3(0, BackHipHeight, HipOffset).Rotated(Vector3.Right, SitPitch) + sitShift).Y;
        float sitFold = FoldToReach(LowerLegs[2].Position, Feet[2].Position, sitHip - FootHeight);
        float lieFold = FoldToReach(LowerLegs[2].Position, Feet[2].Position, BackHipHeight - LieDrop - FootHeight);

        // Lying, the forelegs either reach out in front from the shoulder, elbows on the ground (a bear), or fold right
        // under the chest with the knee down and the shin tucked back along the ground (a deer).
        float lowShoulder = FrontHipHeight - LieDrop - FootHeight * 0.5f;
        float reach = Mathf.Acos(Mathf.Clamp(lowShoulder / FrontUpperLength, -1f, 1f));
        bool foldsUnder = !SitsUp;
        float lieUpper = reach;
        float lieLower = foldsUnder ? -Mathf.Pi / 2f - reach - 0.3f : Mathf.Pi / 2f - reach;
        float lieFoot = foldsUnder ? -0.4f : -Mathf.Pi / 2f;
        float sitUpper = SitsUp ? -SitPitch + 0.05f : lieUpper;
        float sitLower = SitsUp ? 0f : lieLower;
        float sitFoot = SitsUp ? 0f : lieFoot;

        for (int i = 0; i < 4; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f;
            float phase = WalkCycle + Mathf.Lerp(WalkPhase[i], GallopPhase[i], run);
            float swing = Mathf.Sin(phase) * amplitude;
            float lift = Mathf.Max(0f, Mathf.Cos(phase)) * stride * Mathf.Lerp(0.7f, 1.2f, run);

            // Forelegs fold with the elbow behind, hind legs with the hock behind and the knee in front.
            if (i < 2)
            {
                UpperLegs[i].Rotation = new Vector3(Pose(swing - frontFold + pitch, sitUpper, lieUpper), 0, 0);
                LowerLegs[i].Rotation = new Vector3(Pose(-lift + frontFold * 2f, sitLower, lieLower), 0, 0);
                Feet[i].Rotation = new Vector3(Pose(0f, sitFoot, lieFoot), 0, 0);
            }
            else
            {
                float restFold = SitsUp ? sitFold - SitPitch : lieFold;
                UpperLegs[i].Rotation = new Vector3(Pose(swing + backFold + pitch, restFold, lieFold), 0,
                    Pose(0f, side * 0.12f, side * 0.25f));
                LowerLegs[i].Rotation = new Vector3(Pose(lift * 0.7f - backFold * 2f, SitsUp ? -sitFold * 2f : -lieFold * 2f, -lieFold * 2f), 0, 0);
                Feet[i].Rotation = new Vector3(Pose(0f, SitsUp ? sitFold : lieFold, lieFold), 0, 0);
            }
        }

        // The head drops right down to drink or graze, and is carried a little lower at a run. Sitting, it is held up,
        // looking ahead, however the body is tipped.
        Neck.Rotation = new Vector3(Pose(eat * HeadDown - run * 0.2f + Mathf.Sin(WalkCycle * 2f) * 0.03f * stride,
            SitsUp ? -SitPitch * 0.6f : 0.15f, LieNeck), 0, 0);
        Head.Rotation = new Vector3(Pose(-eat * HeadDown * 0.4f + run * 0.15f, SitsUp ? -SitPitch * 0.4f : -0.1f, LieHead), 0, 0);

        AnimateExtras(stride, run, dt);
        GoLimp();
        Hair.SetShaderParameter("sway_amount", (0.006f + 0.015f * stride) * Alive);
    }

    /// <summary>Dead, the animal flops over onto its flank, legs stretched loosely out from the body and neck laid out.</summary>
    private void GoLimp()
    {
        if (Dead <= 0f)
            return;

        Frame.Position = Limp(Frame.Position, new Vector3(DeadRoll, DeadFlank, 0f));
        Frame.Rotation = Limp(Frame.Rotation, new Vector3(0f, 0f, Mathf.Pi / 2f));
        for (int i = 0; i < 4; i++)
        {
            bool front = i < 2;
            UpperLegs[i].Rotation = Limp(UpperLegs[i].Rotation, new Vector3(front ? 0.45f : -0.4f, 0f, 0f));
            LowerLegs[i].Rotation = Limp(LowerLegs[i].Rotation, new Vector3(front ? 0.2f : -0.2f, 0f, 0f));
            Feet[i].Rotation = Limp(Feet[i].Rotation, new Vector3(0.2f, 0f, 0f));
        }
        Neck.Rotation = Limp(Neck.Rotation, new Vector3(-0.6f, 0f, 0f));
        Head.Rotation = Limp(Head.Rotation, new Vector3(0.3f, 0f, 0f));
        GoLimpExtras();
    }

    /// <summary>
    /// Builds the four legs: a thick upper leg with a mound of muscle where it joins the body, a slimmer lower leg (the
    /// hind one the long hock, angled back), and a foot pivot at the ankle, which <paramref name="foot"/> fills with a
    /// hoof or paw. The legs wear <paramref name="coat"/>, and the muscle, which blends into the body, the body's
    /// <paramref name="muscleCoat"/>. Leg order: front left, front right, back left, back right.
    /// </summary>
    protected void BuildLegs(RandomNumberGenerator rng, Material coat, Func<Vector3, Vector3, Color> colouring,
        Material muscleCoat, Func<Vector3, Vector3, Color> muscleColouring,
        (float Upper, float Knee, float Ankle) front, (float Upper, float Knee, float Ankle) back, Vector3 muscle,
        int furPerLeg, (float Min, float Max) furLength, Color furRoot, Color furTip, Action<Node3D, int> foot)
    {
        UpperLegs = new Node3D[4];
        LowerLegs = new Node3D[4];
        Feet = new Node3D[4];
        string[] names = ["LegFrontLeft", "LegFrontRight", "LegBackLeft", "LegBackRight"];
        float width = (furLength.Min + furLength.Max) * 0.3f;
        for (int i = 0; i < 4; i++)
        {
            bool isFront = i < 2;
            float side = i % 2 == 0 ? -1f : 1f;
            var radii = isFront ? front : back;
            float upperLength = isFront ? FrontUpperLength : BackUpperLength;
            float lowerLength = isFront ? FrontLowerLength : BackLowerLength;
            float hipHeight = upperLength + lowerLength + FootHeight;
            var knee = isFront ? new Vector3(0, -upperLength, 0.02f * upperLength) : new Vector3(0, -upperLength, -0.15f * upperLength);
            var ankle = new Vector3(0, -lowerLength, isFront ? -0.02f * lowerLength : 0.18f * lowerLength);

            UpperLegs[i] = Pivot(Frame, names[i], new Vector3(side * LegSpread, hipHeight, isFront ? -ShoulderOffset : HipOffset));
            // The leg starts a little below the joint, so its own colour doesn't show through the body's coat; the muscle
            // mound hides the join.
            var top = new Vector3(0, -radii.Upper * 0.4f, 0);
            Attach(UpperLegs[i], "Upper", Tube([top, knee], [radii.Upper, radii.Knee], 12, coat, capEnd: false));
            Attach(UpperLegs[i], "UpperFur", Strands(rng, OnSegment(rng, furPerLeg, top, knee, radii.Upper, radii.Knee),
                new Vector3(0, -0.6f, 0.3f), furLength.Min, furLength.Max, furRoot, furTip, Hair, width: width, colouring: colouring));

            // Muscle where the leg meets the body: the shoulder in front, the heavier haunch behind.
            var bulk = isFront ? muscle : muscle * new Vector3(1.1f, 1.1f, 1.25f);
            var bulkAt = new Vector3(0, -bulk.Y * 0.2f, isFront ? 0f : bulk.Z * 0.15f);
            Attach(UpperLegs[i], "Muscle", Ellipsoid(bulk, 14, 10, muscleCoat, muscleColouring), bulkAt);
            Attach(UpperLegs[i], "MuscleFur", Strands(rng, OnShape(rng, u => u * bulk + bulkAt, furPerLeg, u => u.X * side > -0.2f),
                new Vector3(0, -0.2f, 0.8f), furLength.Min, furLength.Max, furRoot, furTip, Hair, width: width, colouring: muscleColouring));

            LowerLegs[i] = Pivot(UpperLegs[i], "Lower", knee);
            Attach(LowerLegs[i], "Joint", Ellipsoid(Vector3.One * radii.Knee, 10, 8, coat));
            Attach(LowerLegs[i], "Lower", Tube([Vector3.Zero, ankle], [radii.Knee * 0.9f, radii.Ankle], 10, coat, capEnd: false));
            Attach(LowerLegs[i], "LowerFur", Strands(rng, OnSegment(rng, furPerLeg / 2, Vector3.Zero, ankle, radii.Knee * 0.9f, radii.Ankle),
                new Vector3(0, -0.6f, 0.3f), furLength.Min * 0.6f, furLength.Max * 0.6f, furRoot, furTip, Hair, width: width * 0.8f,
                colouring: colouring));

            Feet[i] = Pivot(LowerLegs[i], "Foot", ankle);
            foot(Feet[i], i);
        }
    }

    /// <summary>
    /// A deer's foot: a short pastern sloping down and forward from the ankle to a pair of cloven hooves, each half a
    /// dark rounded wedge resting on the ground <see cref="FootHeight"/> below the ankle, with the dewclaws higher up
    /// behind. <paramref name="size"/> is the length of one hoof half.
    /// </summary>
    protected void Hooves(Node3D foot, float size, Material skin, Material horn)
    {
        var hoofAt = new Vector3(0, -FootHeight + size * 0.3f, -size * 0.45f);
        Attach(foot, "Pastern", Tube([Vector3.Zero, hoofAt + Vector3.Up * size * 0.2f], [size * 0.42f, size * 0.38f], 8, skin, capEnd: false));
        foreach (float side in new[] { -1f, 1f })
        {
            var half = new Vector3(size * 0.42f, size * 0.5f, size * 0.85f);
            Attach(foot, "Hoof", Ellipsoid(u => new Vector3(u.X * half.X, u.Y * half.Y * (u.Y < 0 ? 0.6f : 1f), u.Z * half.Z), 10, 8, horn),
                hoofAt + new Vector3(side * size * 0.38f, 0, -size * 0.2f));
            Attach(foot, "Dewclaw", Ellipsoid(Vector3.One * size * 0.2f, 6, 4, horn), new Vector3(side * size * 0.35f, -FootHeight * 0.35f, size * 0.45f));
        }
    }
}
