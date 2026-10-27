using System;
using System.Collections.Generic;
using Godot;

namespace IceAgeWorld;

/// <summary>
/// The animal shop: a button for every animal, showing whether it's free, bought, or what it costs, with the player's
/// coins at the top. Choosing one the player owns plays as it; choosing one they can afford buys it first. It comes up
/// to pick an animal at the start, to be reborn after dying, and on B at any time.
/// </summary>
public partial class AnimalShop : ColorRect
{
    public Player? Player { get; set; }

    /// <summary>True while it was opened with B, so B or Esc can close it again without choosing.</summary>
    public bool CanClose { get; private set; }

    private Label _title = null!;
    private Label _subtitle = null!;
    private Label _coins = null!;
    private Button _close = null!;
    private readonly List<Button> _buttons = [];
    private readonly Dictionary<Sex, Button> _sexes = [];
    private Action<int>? _chosen;

    private static readonly Color Gold = new(1f, 0.85f, 0.3f);

    public override void _Ready()
    {
        Color = new Color(0f, 0f, 0f, 0.6f);
        Visible = false;
        SetAnchorsPreset(LayoutPreset.FullRect);

        var centre = new CenterContainer();
        centre.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(centre);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 18);
        centre.AddChild(column);

        _title = Hud.Text("", 40);
        column.AddChild(_title);
        _subtitle = Hud.Text("", 22);
        column.AddChild(_subtitle);
        _coins = Hud.Text("", 26);
        _coins.AddThemeColorOverride("font_color", Gold);
        column.AddChild(_coins);

        // Male or female, chosen before the animal: in most kinds the female is smaller, and only one of the other sex
        // will pair up with you. V switches during play too.
        var sexes = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        sexes.AddThemeConstantOverride("separation", 12);
        column.AddChild(sexes);
        var group = new ButtonGroup();
        foreach (var sex in new[] { Sex.Male, Sex.Female })
        {
            var button = new Button { Text = sex.ToString(), ToggleMode = true, ButtonGroup = group, CustomMinimumSize = new Vector2(140, 44) };
            button.AddThemeFontSizeOverride("font_size", 19);
            button.Pressed += () => Player!.BecomeSex(sex);
            sexes.AddChild(button);
            _sexes[sex] = button;
        }

        // A button for each animal, wrapping onto a second row when there are too many for one.
        var buttons = new HFlowContainer { Alignment = FlowContainer.AlignmentMode.Center, CustomMinimumSize = new Vector2(760, 0) };
        buttons.AddThemeConstantOverride("h_separation", 12);
        buttons.AddThemeConstantOverride("v_separation", 12);
        column.AddChild(buttons);
        for (int i = 0; i < Player!.Animals.Count; i++)
        {
            int index = i;
            var button = new Button { CustomMinimumSize = new Vector2(176, 72) };
            button.AddThemeFontSizeOverride("font_size", 19);
            button.Pressed += () => Choose(index);
            buttons.AddChild(button);
            _buttons.Add(button);
        }

        _close = new Button { Text = "Close (B)", CustomMinimumSize = new Vector2(160, 44), SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        _close.AddThemeFontSizeOverride("font_size", 18);
        _close.Pressed += Close;
        column.AddChild(_close);
    }

    /// <summary>Shows the shop, calling <paramref name="chosen"/> with the animal picked (bought first if need be).</summary>
    public void Open(string title, string subtitle, bool canClose, Action<int> chosen)
    {
        _title.Text = title;
        _subtitle.Text = subtitle;
        CanClose = canClose;
        _close.Visible = canClose;
        _chosen = chosen;
        Visible = true;
        Refresh();
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    public void Close()
    {
        Visible = false;
        _chosen = null;
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    /// <summary>B opens the shop while playing, to buy an animal or switch to one; B or Esc closes it again.</summary>
    public override void _UnhandledInput(InputEvent @event)
    {
        if (Visible)
        {
            if (CanClose && (@event.IsActionPressed(InputSetup.Shop) || @event.IsActionPressed("ui_cancel")))
            {
                Close();
                GetViewport().SetInputAsHandled();
            }
            return;
        }
        // Not before the game has started (the player is held still behind the start menu until then), nor once dead,
        // when the shop comes up by itself.
        if (@event.IsActionPressed(InputSetup.Shop) && Player!.CanProcess() && !Player.IsDead)
        {
            Open("Animal shop", "Buy an animal with your coins, or choose one you own to play as it:", canClose: true, Player.SwitchTo);
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        // Coins can come in while the shop is open.
        if (Visible)
            Refresh();
    }

    /// <summary>Labels every button with its animal and what it costs, greying out what the player can't yet afford.</summary>
    private void Refresh()
    {
        _coins.Text = $"Coins: {Wallet.Coins}";
        foreach (var (sex, button) in _sexes)
            button.SetPressedNoSignal(Player!.Sex == sex);
        for (int i = 0; i < _buttons.Count; i++)
        {
            var animal = Player!.Animals[i];
            int price = Wallet.PriceOf(animal);
            bool owned = Wallet.Owns(animal);
            string status = CanClose && i == Player.AnimalIndex && !Player.IsDead ? "Playing now"
                : owned ? (price == 0 ? "Free" : "Owned")
                : $"Buy for {price} coins";
            _buttons[i].Text = $"{animal.DisplayName}\n{status}";
            _buttons[i].Disabled = !owned && !Wallet.CanAfford(animal);
            _buttons[i].Modulate = owned ? Colors.White : Gold;
        }
    }

    private void Choose(int index)
    {
        if (!Wallet.Buy(Player!.Animals[index]))
            return;
        var chosen = _chosen;
        Close();
        chosen?.Invoke(index);
    }
}
