using System.Collections.Generic;
using Godot;

namespace IceAgeWorld;

/// <summary>
/// The player's coins and the animals they have bought, kept on this computer between games. As in Savannah Life,
/// a few common animals are free to play from the start and the rest have to be bought: the bigger and fiercer the
/// animal, the dearer. Coins are earned by doing chores (see <see cref="Player.Chores"/>), paid through <see cref="Earn"/>.
/// </summary>
public static class Wallet
{
    /// <summary>Coins a brand-new player starts with: enough to buy one of the cheaper animals straight away.</summary>
    public const int StartingCoins = 250;

    private const string SavePath = "user://wallet.cfg";

    /// <summary>The player's coins.</summary>
    public static int Coins
    {
        get
        {
            Load();
            return _coins;
        }
    }

    private static int _coins;
    private static readonly HashSet<string> Bought = [];
    private static bool _loaded;

    /// <summary>
    /// What each animal costs. The free ones are everyday animals of the steppe; the hunters and giants cost more, and
    /// the ice-age giants most of all.
    /// </summary>
    public static int PriceOf(Animal animal) => animal switch
    {
        Reindeer or SeaOtter or ArcticWolf => 0,
        Moose => 250,
        BaldEagle or SnowyOwl => 400,
        CaveHyena => 500,
        SnowLeopard => 600,
        CaveLion => 900,
        Mammoth or WoollyRhino => 1000,
        Smilodon => 1200,
        PolarBear => 1500,
        _ => 0,
    };

    /// <summary>Whether the player can play as <paramref name="animal"/>: it's free or they have bought it.</summary>
    public static bool Owns(Animal animal)
    {
        Load();
        return PriceOf(animal) == 0 || Bought.Contains(Key(animal));
    }

    /// <summary>Whether the player has the coins to buy <paramref name="animal"/>.</summary>
    public static bool CanAfford(Animal animal)
    {
        Load();
        return Coins >= PriceOf(animal);
    }

    /// <summary>Buys <paramref name="animal"/> if the player can afford it, and returns whether they now own it.</summary>
    public static bool Buy(Animal animal)
    {
        if (Owns(animal))
            return true;
        if (!CanAfford(animal))
            return false;
        _coins -= PriceOf(animal);
        Bought.Add(Key(animal));
        Save();
        return true;
    }

    /// <summary>Gives the player <paramref name="amount"/> coins, e.g. as a reward, and saves them.</summary>
    public static void Earn(int amount)
    {
        Load();
        _coins += amount;
        Save();
    }

    /// <summary>Animals are saved by kind, so the save still works if the switching order changes.</summary>
    private static string Key(Animal animal) => animal.GetType().Name;

    private static void Load()
    {
        if (_loaded)
            return;
        _loaded = true;

        var file = new ConfigFile();
        if (file.Load(SavePath) != Error.Ok)
        {
            _coins = StartingCoins;
            return;
        }
        _coins = (int)file.GetValue("wallet", "coins", StartingCoins);
        foreach (var kind in (string[])file.GetValue("wallet", "animals", new string[0]))
            Bought.Add(kind);
    }

    private static void Save()
    {
        var file = new ConfigFile();
        file.SetValue("wallet", "coins", _coins);
        file.SetValue("wallet", "animals", new List<string>(Bought).ToArray());
        file.Save(SavePath);
    }
}
