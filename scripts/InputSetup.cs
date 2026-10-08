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
    public const string Sit = "sit";
    public const string LieDown = "lie_down";
    public const string SwitchAnimal = "switch_animal";

    public static void Register()
    {
        Add(MoveForward, Key.W, Key.Up);
        Add(MoveBack, Key.S, Key.Down);
        Add(MoveLeft, Key.A, Key.Left);
        Add(MoveRight, Key.D, Key.Right);
        Add(Sprint, Key.Shift);
        Add(Jump, Key.Space);
        Add(Eat, Key.E);
        // A pounce goes on F, or a click, since the mouse is already in hand to look about.
        Add(Attack, new InputEventKey { PhysicalKeycode = Key.F }, new InputEventMouseButton { ButtonIndex = MouseButton.Left });
        Add(Sit, Key.C);
        Add(LieDown, Key.X);
        Add(SwitchAnimal, Key.Tab);
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
