using System.Linq;
using Godot;

namespace IceAgeWorld;

/// <summary>
/// Registers the game's input actions in code, so key bindings live in one readable place
/// instead of the serialized input map in project.godot.
/// </summary>
public static class InputSetup
{
    public const string MoveForward = "move_forward";
    public const string MoveBack = "move_back";
    public const string MoveLeft = "move_left";
    public const string MoveRight = "move_right";
    public const string Sprint = "sprint";
    public const string Jump = "jump";
    public const string Eat = "eat";
    public const string Attack = "attack";
    public const string Grip = "grip";
    public const string Sit = "sit";
    public const string LieDown = "lie_down";
    public const string SwitchAnimal = "switch_animal";
    public const string Map = "map";

    public static void Register()
    {
        // Holding the right mouse button walks forward, so the animal can be steered with the mouse alone.
        Add(MoveForward, new InputEventKey { PhysicalKeycode = Key.W }, new InputEventKey { PhysicalKeycode = Key.Up },
            new InputEventMouseButton { ButtonIndex = MouseButton.Right });
        Add(MoveBack, Key.S, Key.Down);
        Add(MoveLeft, Key.A, Key.Left);
        Add(MoveRight, Key.D, Key.Right);
        Add(Sprint, Key.Shift);
        Add(Jump, Key.Space);
        // Eating goes on E, or a left click: the other half of playing with the mouse alone.
        Add(Eat, new InputEventKey { PhysicalKeycode = Key.E }, new InputEventMouseButton { ButtonIndex = MouseButton.Left });
        Add(Attack, Key.F);
        // The snow leopard's leap-and-hold.
        Add(Grip, Key.G);
        Add(Sit, Key.C);
        Add(LieDown, Key.X);
        Add(SwitchAnimal, Key.Tab);
        Add(Map, Key.M);
    }

    private static void Add(string action, params Key[] keys) =>
        Add(action, keys.Select(key => (InputEvent)new InputEventKey { PhysicalKeycode = key }).ToArray());

    private static void Add(string action, params InputEvent[] events)
    {
        if (InputMap.HasAction(action))
            return;

        InputMap.AddAction(action);
        foreach (var @event in events)
            InputMap.ActionAddEvent(action, @event);
    }
}
