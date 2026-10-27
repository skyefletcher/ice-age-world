using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace IceAgeWorld;

/// <summary>
/// A dead body is a dead weight, not a statue. Its neck, head, jaw, ears, legs and tail hang under gravity, each joint
/// turning down as far as the joint allows, and come to rest on the ground (or the water) wherever that is, so no two
/// carcasses lie quite alike. Picked up, the whole body is held by the middle of its back: draped level across a
/// hunter's jaws, as a lion carries a gazelle, or beneath an eagle's talons, its head and legs dangling straight down.
/// A dead body doesn't twitch, so it eases into each pose and stays there rather than swinging.
/// </summary>
public abstract partial class Animal
{
    /// <summary>Joints that go floppy in death, found by name: the limbs, neck, head, jaw, ears, trunk and tail.</summary>
    private static readonly string[] FloppyNames =
        ["Leg", "Lower", "Foot", "Shank", "Hand", "Foreleg", "Hind", "Tail", "Neck", "Head", "Ear", "Tilt", "Trunk", "Jaw", "Wing"];

    /// <summary>How quickly a dead weight sags into its new pose, per second: soon, and without bouncing.</summary>
    private const float FlopRate = 6f;

    /// <summary>
    /// One joint of a dead body: the middle of everything hanging off it (where its weight is), in the joint's own space;
    /// how far it may turn from where the limp pose puts it; and which way, in the world, it hangs now. Before and After
    /// record the joint's pose either side of being turned, so a joint the animal's own posing leaves alone can be put
    /// back first, not turned further.
    /// </summary>
    private sealed class Floppy
    {
        public required Node3D Joint;
        public required Vector3 Weight;
        public required float Slack;
        public Vector3 Hangs;
        public Basis Before, After;
        public bool Swung;
    }

    private List<Floppy>? _flops;
    private ulong _flopFrame;

    /// <summary>Where a carried body is held, and a point straight back along its spine from there, both in the frame's space.</summary>
    private (Vector3 Grip, Vector3 Rump)? _jawHold, _backHold;
    private Vector3 _carriedAlong;
    private ulong _hangFrame;
    private Vector3? _biteAt;

    /// <summary>Where the jaws close, in the middle of the lower jaw: where a hunter holds whatever it carries in its mouth.</summary>
    public Vector3 Bite
    {
        get
        {
            if (_jaws.Count == 0)
                return GlobalPosition;
            _biteAt ??= Middle(_jaws[0], _ => false);
            return _jaws[0].GlobalTransform * _biteAt.Value;
        }
    }

    /// <summary>
    /// Lets a dead body's joints hang under gravity, after <see cref="Animate"/> has posed it limp; call every frame it
    /// is seen. <paramref name="floor"/> gives the height below a point that nothing can sink through: the ground, or
    /// the water's surface for a body afloat.
    /// </summary>
    public void Flop(float dt, Func<Vector3, float> floor)
    {
        _flops ??= FindFlops();
        ulong frame = Engine.GetProcessFrames();
        bool fresh = frame - _flopFrame > 2;
        _flopFrame = frame;
        float ease = 1f - Mathf.Exp(-FlopRate * dt);

        // Parents come before their children in the list, so each joint turns from where its parent already hangs.
        foreach (var flop in _flops)
        {
            var joint = flop.Joint;
            // A joint the animal's own posing doesn't set would otherwise be turned again on top of last frame's turn.
            if (flop.Swung && joint.Basis.IsEqualApprox(flop.After))
                joint.Basis = flop.Before;
            flop.Before = joint.Basis;

            var origin = joint.GlobalPosition;
            var posed = joint.GlobalTransform * flop.Weight;
            float reach = (posed - origin).Length();
            if (reach < 0.0001f)
                continue;
            var limp = (posed - origin) / reach;

            // Its weight hangs straight down, as near as the joint lets it: a leg can't swing up through the body.
            float angle = limp.AngleTo(Vector3.Down);
            var target = angle > flop.Slack ? Swing(limp, Vector3.Down, flop.Slack / angle) : Vector3.Down;
            if (fresh)
                flop.Hangs = limp;
            var direction = Swing(flop.Hangs, target, ease);
            flop.Hangs = direction;

            // It rests on the ground rather than sinking into it, lying out along it instead.
            var at = origin + direction * reach;
            float lowest = floor(at) + reach * 0.3f;
            if (at.Y < lowest)
            {
                var flat = direction with { Y = 0f };
                if (flat.LengthSquared() < 0.0001f)
                    flat = limp with { Y = 0f };
                if (flat.LengthSquared() > 0.0001f)
                {
                    float drop = Mathf.Clamp((origin.Y - lowest) / reach, 0f, 1f);
                    direction = (flat.Normalized() * Mathf.Sqrt(1f - drop * drop) + Vector3.Down * drop).Normalized();
                }
            }

            // Turn the joint, in its parent's space, from the limp pose to where its weight hangs.
            var parent = joint.GetParent<Node3D>().GlobalBasis.Inverse();
            var from = (parent * limp).Normalized();
            var to = (parent * direction).Normalized();
            if (from.Dot(to) < 0.9999f)
                joint.Basis = new Basis(new Quaternion(from, to)) * joint.Basis;
            flop.After = joint.Basis;
            flop.Swung = true;
        }
    }

    /// <summary>Puts back any joint left turned that the animal's own posing doesn't set, as it comes back to life.</summary>
    private void Unflop()
    {
        if (_flops is null)
            return;
        foreach (var flop in _flops.Where(f => f.Swung))
        {
            if (flop.Joint.Basis.IsEqualApprox(flop.After))
                flop.Joint.Basis = flop.Before;
            flop.Swung = false;
        }
        _flopFrame = 0;
    }

    /// <summary>
    /// Holds a dead body at <paramref name="grip"/> by the middle of its back; call every frame it's carried, after
    /// <see cref="Animate"/>. <paramref name="inJaws"/>, it's draped crosswise over the hunter's jaws, head to one side
    /// and rump to the other, as a lion carries a gazelle; in talons it's held level beneath, head the way the bird
    /// flies. Either way its back is up, so <see cref="Flop"/> lets its head and legs dangle. <paramref name="facing"/>
    /// is the way the carrier faces. Turns the animal to be held so, and returns how far its parent must move so the
    /// grip lands on <paramref name="grip"/>.
    /// </summary>
    public Vector3 Hang(Vector3 grip, Vector3 facing, bool inJaws, float dt)
    {
        var frame = GetNode<Node3D>("Frame");
        var (gripAt, rumpAt) = inJaws ? _jawHold ??= JawHold(frame) : _backHold ??= BackHold(frame);

        // Crosswise in the jaws, rump to the hunter's right; in talons, rump trailing behind. It follows the hunter's
        // turns a moment late, as a dead weight would, but settles rather than swinging back and forth.
        var target = inJaws ? facing.Cross(Vector3.Up).Normalized() : -facing;
        ulong now = Engine.GetProcessFrames();
        if (now - _hangFrame > 2 || _carriedAlong.LengthSquared() < 0.5f)
            _carriedAlong = target;
        _hangFrame = now;
        _carriedAlong = Swing(_carriedAlong, target, 1f - Mathf.Exp(-FlopRate * dt));

        var turn = Axes(_carriedAlong, Vector3.Up)
            * Axes(frame.Basis * (rumpAt - gripAt), frame.Basis * Vector3.Up).Transposed();
        Basis = GetParent<Node3D>().GlobalBasis.Orthonormalized().Inverse() * turn;
        return grip - frame.GlobalTransform * gripAt;
    }

    /// <summary>
    /// Turns one direction <paramref name="weight"/> of the way to another. Godot's <see cref="Vector3.Slerp"/> throws
    /// when the two all but line up, as they do for a joint already hanging straight down, so then it just blends.
    /// </summary>
    private static Vector3 Swing(Vector3 from, Vector3 to, float weight)
    {
        if (from.Cross(to).LengthSquared() > 0.000001f)
            return from.Slerp(to, weight).Normalized();
        var blend = from.Lerp(to, weight);
        return blend.LengthSquared() > 0.000001f ? blend.Normalized() : to.Normalized();
    }

    /// <summary>An upright frame of axes: the first along <paramref name="along"/>, the second as near <paramref name="toward"/> as it can be.</summary>
    private static Basis Axes(Vector3 along, Vector3 toward)
    {
        var a = along.Normalized();
        var b = toward - a * a.Dot(toward);
        b = b.LengthSquared() > 0.000001f ? b.Normalized() : a.Cross(Vector3.Right).Normalized();
        return new Basis(a, b, a.Cross(b));
    }

    /// <summary>
    /// In the jaws, the body is held on top of its back a little in front of the middle, so it balances, the head and
    /// shoulders just outweighing the rump; the second point lies straight back along the spine at the same height.
    /// </summary>
    private (Vector3, Vector3) JawHold(Node3D frame)
    {
        var body = Torso(frame);
        var middle = body.GetCenter();
        var grip = middle with { Z = Mathf.Lerp(middle.Z, body.Position.Z, 0.15f), Y = body.End.Y };
        return (grip, grip with { Z = body.End.Z });
    }

    /// <summary>In talons it's held by the middle of the back.</summary>
    private (Vector3, Vector3) BackHold(Node3D frame)
    {
        var body = Torso(frame);
        var grip = body.GetCenter() with { Y = body.End.Y };
        return (grip, grip with { Z = body.End.Z });
    }

    /// <summary>The box round the body itself, everything on the frame but the floppy parts, in the frame's space.</summary>
    private Aabb Torso(Node3D frame)
    {
        var floppy = (_flops ??= FindFlops()).Select(f => f.Joint).ToHashSet();
        return Bounds(frame, n => floppy.Contains(n));
    }

    /// <summary>The middle of everything hanging off <paramref name="joint"/>, in its own space, leaving out what <paramref name="skip"/> says.</summary>
    private static Vector3 Middle(Node3D joint, Func<Node3D, bool> skip) => Bounds(joint, skip).GetCenter();

    private static Aabb Bounds(Node3D root, Func<Node3D, bool> skip)
    {
        var into = root.GlobalTransform.AffineInverse();
        Aabb? box = null;
        void Visit(Node3D node)
        {
            foreach (var child in node.GetChildren().OfType<Node3D>())
            {
                if (skip(child))
                    continue;
                if (child is MeshInstance3D mesh)
                {
                    var bounds = into * mesh.GlobalTransform * mesh.GetAabb();
                    box = box?.Merge(bounds) ?? bounds;
                }
                Visit(child);
            }
        }
        Visit(root);
        return box ?? new Aabb(Vector3.Zero, Vector3.Zero);
    }

    private List<Floppy> FindFlops()
    {
        var flops = new List<Floppy>();
        foreach (var joint in FindChildren("*", "Node3D", true, false).OfType<Node3D>())
        {
            if (joint.GetType() != typeof(Node3D) || joint.IsInGroup(EyeGroup)
                || !FloppyNames.Any(n => joint.Name.ToString().StartsWith(n, StringComparison.Ordinal)))
                continue;
            var weight = Middle(joint, _ => false);
            if (weight.LengthSquared() < 0.000001f)
                continue;
            // A neck, head and legs can swing right round to hang straight down; ears flop, and a jaw only drops open.
            string name = joint.Name.ToString();
            float slack = name.StartsWith("Jaw") ? 0.5f : name.StartsWith("Ear") || name.StartsWith("Tilt") ? 1f : 1.8f;
            flops.Add(new Floppy { Joint = joint, Weight = weight, Slack = slack });
        }
        return flops;
    }
}
