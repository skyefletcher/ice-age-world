using Godot;

namespace IceAgeWorld;

/// <summary>
/// A little health bar that floats over a wild animal's head and turns to face the camera: green while it is healthy,
/// yellow once it has been hurt and red when it is close to falling. It grows with distance so it stays readable
/// across the steppe, and hides once the animal is dead.
/// </summary>
public partial class HealthBar : Node3D
{
    /// <summary>At or above this much health the bar is green; below <see cref="Low"/> it is red, and yellow between.</summary>
    public const float High = 60f;
    public const float Low = 30f;

    public static readonly Color Healthy = new(0.25f, 0.85f, 0.3f);
    public static readonly Color Hurt = new(0.95f, 0.85f, 0.2f);
    public static readonly Color Dying = new(0.9f, 0.2f, 0.15f);

    private const float Width = 1.4f;
    private const float Thickness = 0.2f;

    /// <summary>How much bigger the bar grows for every metre it is from the camera, so far-off animals' bars stay readable.</summary>
    private const float SizePerMetre = 0.04f;

    private MeshInstance3D _fill = null!;
    private StandardMaterial3D _fillMaterial = null!;

    /// <summary>The colour a bar shows for <paramref name="health"/> out of 100.</summary>
    public static Color ColourFor(float health) => health >= High ? Healthy : health >= Low ? Hurt : Dying;

    public override void _Ready()
    {
        // A dark backing, so the empty part of the bar still shows how much has been lost.
        var back = new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = new Vector2(Width + 0.06f, Thickness + 0.06f) },
            MaterialOverride = Flat(new Color(0.05f, 0.05f, 0.05f, 0.8f), 0),
        };
        AddChild(back);

        _fillMaterial = Flat(Healthy, 1);
        _fill = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(Width, Thickness) }, MaterialOverride = _fillMaterial };
        AddChild(_fill);
    }

    /// <summary>Shows <paramref name="health"/> out of 100, turned to face a camera at <paramref name="eye"/>.</summary>
    public void Show(float health, Vector3? eye)
    {
        Visible = health > 0f;
        if (!Visible)
            return;

        float fraction = Mathf.Clamp(health / 100f, 0f, 1f);
        // The fill shrinks towards the left end, like the HUD's bars.
        _fill.Scale = new Vector3(Mathf.Max(fraction, 0.001f), 1f, 1f);
        _fill.Position = new Vector3(-Width * (1f - fraction) * 0.5f, 0f, 0.001f);
        _fillMaterial.AlbedoColor = ColourFor(health);

        if (eye is not { } at)
            return;
        // The quads face +Z, so turn that towards the camera, upright, at a size that suits how far away it is.
        var offset = at - GlobalPosition;
        float distance = offset.Length();
        if (distance < 0.01f)
            return;
        var basis = Basis.LookingAt(-offset / distance, Vector3.Up);
        GlobalBasis = basis.Scaled(Vector3.One * Mathf.Max(1f, distance * SizePerMetre));
    }

    private static StandardMaterial3D Flat(Color colour, int priority) => new()
    {
        AlbedoColor = colour,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        DisableFog = true,
        RenderPriority = priority,
    };
}
