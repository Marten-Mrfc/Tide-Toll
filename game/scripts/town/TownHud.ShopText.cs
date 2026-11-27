using System;
using System.Collections.Generic;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>The shop's words (sheet v3): row names and their catalogue, line 2 (worth, what it does, why it is
/// blocked), the pill's money, the tooltips' sentences.</summary>
public partial class TownHud
{
    /// <summary>Sheet v3: a pill's price, the same way in every row: whole below <see cref="_pillShortFrom"/> (100,000
    /// when "99,999" fits the pill at body size, else 10,000), then "123k", then "1.2M", "12M".</summary>
    public string PillPrice(double c)
    {
        c = Math.Floor(c);
        if (c < _pillShortFrom) return c.ToString("N0", Inv);
        if (c < 1_000_000) return Math.Floor(c / 1000).ToString(Inv) + "k";
        // Never rounded up (9,999,999 reads 9.9M, not 10.00M).
        if (c < 10_000_000) return (Math.Floor(c / 100_000) / 10).ToString("0.0", Inv) + "M";
        return Math.Floor(c / 1_000_000).ToString(Inv) + "M";
    }

    /// <summary>Pave's pill: "from 12" centred without the coin sign when it fits the pill at body size, else the price
    /// beside the coin as in every other row (sheet v3: a price is never set smaller).</summary>
    public (string text, bool centred) PaveFace(double price)
    {
        string p = PillPrice(price), t = "from " + p;
        float box = UiKit.U(_s, PillUnits) - UiKit.U(_s, 12);
        return UiKit.TabBold.GetStringSize(t, HorizontalAlignment.Left, -1, UiKit.Type(_s, UiKit.Body)).X <= box ? (t, true) : (p, false);
    }

    private double _pillShortFrom = 100_000;

    /// <summary>Decides the pill's width and where its prices start to shorten for this layout (once, when the shop is
    /// built): "99,999" whole at body size if a pill of 96 (or, on the 320 rail, 104) holds it, else from 10,000 "12k".</summary>
    private void DecidePillShort()
    {
        bool wide = Zones.RailUnits >= 320;
        float w = UiKit.TabBold.GetStringSize("99,999", HorizontalAlignment.Left, -1, UiKit.Type(_s, UiKit.Body)).X;
        float Box(float units) => UiKit.U(_s, units) - UiKit.U(_s, 29) - UiKit.U(_s, 11);
        _pillUnits = wide ? 96 : 88;
        if (wide && w > Box(96)) _pillUnits = 104;
        _pillShortFrom = w <= Box(_pillUnits) ? 100_000 : 10_000;
    }

    /// <summary>"+1,299 a min": the worth's short form for a narrow line 2.</summary>
    public static string WorthShort(int tenths) => tenths < 100 && tenths % 10 != 0 ? $"+{tenths / 10}.{tenths % 10} a min" : $"+{Money(tenths / 10)} a min";

    /// <summary>Line 2 of a blocked row: why, in a few words.</summary>
    public static string BlockedLine(string reason) => reason switch
    {
        "No room: wait for a cottage" => "Cottages are full",
        "No worn-in road to pave" => "No worn road",
        "" => "Not yet",
        _ => reason,
    };

    /// <summary>Line 2 of a row whose purchase has no rate: what it does, in a few words.</summary>
    public static string DoesLine(ShopItemKind k) => k switch
    {
        ShopItemKind.Villager => "More villagers",
        ShopItemKind.Pace => "Faster walking",
        ShopItemKind.Load => "Bigger loads",
        ShopItemKind.Pave => "A faster road",
        ShopItemKind.Extend => "One more room",
        ShopItemKind.Tools => "Faster work",
        ShopItemKind.Value or ShopItemKind.RiverValue => "A better price",
        ShopItemKind.BellTrack => "A longer ring",
        ShopItemKind.Regulars => "Pays more",
        ShopItemKind.Training => "Better at their trades",
        ShopItemKind.Hand => "Another worker",
        ShopItemKind.Ferry => "Any tide",
        ShopItemKind.Berth => "Two berths",
        ShopItemKind.Crane => "Bigger cargoes",
        ShopItemKind.BoatPrices => "Boats pay more",
        ShopItemKind.Raft => "Bigger raft",
        ShopItemKind.Rope => "Crosses quicker",
        ShopItemKind.Toll => "More toll a boat",
        ShopItemKind.Winch => "Lifts quicker",
        _ => "A new building",
    };

    /// <summary>The name a row shows (sheet v3: one size for all, so the extra hands read "Mill hand"; their whole
    /// name is the tooltip's title).</summary>
    public static string RowName(in ShopItem it) => it.Kind switch
    {
        ShopItemKind.Pave => "Pave a road",
        ShopItemKind.Extend => "Extend a cottage",
        ShopItemKind.Hand => HandRowName(it.Name),
        _ => it.Name,
    };

    /// <summary>"A third hand at the mill" reads "Mill hand" on its row.</summary>
    public static string HandRowName(string name)
    {
        int at = name.LastIndexOf(' ');
        if (at < 0 || at + 1 >= name.Length) return name;
        if (!_handNames.TryGetValue(name, out var n)) _handNames[name] = n = char.ToUpperInvariant(name[at + 1]) + name.Substring(at + 2) + " hand";
        return n;
    }

    private static readonly Dictionary<string, string> _handNames = new();

    /// <summary>Shorter forms, used for the whole layout when a name of the catalogue would not fit even at caption (the
    /// 272 rail): the tooltip's title keeps the whole name.</summary>
    public static readonly Dictionary<string, string> ShortRowNames = new() { ["Better boat prices"] = "Boat prices" };
    private bool _shortNames;

    /// <summary>The name a row shows in this layout.</summary>
    private string RowLabel(in ShopItem it)
    {
        string n = RowName(it);
        return _shortNames && ShortRowNames.TryGetValue(n, out var sh) ? sh : n;
    }

    /// <summary>A catalogue name as rows of this layout show it (tests).</summary>
    public string ShownName(string catalogueName) => _shortNames && ShortRowNames.TryGetValue(catalogueName, out var sh) ? sh : catalogueName;

    /// <summary>Every name a row can show (sheet v3: the name size is decided once from all of them; game-layout proves
    /// every shown row's name is here and that they all fit).</summary>
    public static readonly string[] RowNameCatalogue =
    {
        "Villager", "Pace", "Basket", "Yoke", "Handcart", "Pave a road", "Extend a cottage", "Second bucket", "Market stall",
        "Second field", "Mill", "Tools", "Water price", "Grain price", "Flour price", "Louder bell", "Regulars",
        "Well hand", "Field hand", "Mill hand", "The ferry", "A second berth", "Crane", "Better boat prices", "Wool price",
        "Cloth price", "A bigger raft", "A faster rope", "A higher toll", "A faster winch", "Training",
    };

    /// <summary>Round 5: the simulation's worth (coin a minute) as shown: tenths below 10, whole numbers above.</summary>
    public static int WorthTenths(double w) => w <= 0.05 ? 0 : w < 10 ? (int)Math.Round(w * 10) : (int)Math.Round(w) * 10;
    public static string WorthText(int tenths) => tenths < 100 && tenths % 10 != 0 ? $"+{tenths / 10}.{tenths % 10} coin a minute" : $"+{Money(tenths / 10)} coin a minute";

    /// <summary>Round 6: "Unlocks at level 25" reads "level 25" on a folded line.</summary>
    private static string ShortLock(string reason) => reason.StartsWith("Unlocks at ") ? reason.Substring("Unlocks at ".Length) : reason;

    /// <summary>"Unlocks at level 7" reads "at level 7" on a group's heading line.</summary>
    private static string FoldLock(string reason) => reason.StartsWith("Unlocks ") ? reason.Substring("Unlocks ".Length) : reason;

    private static string Lower(string s) => s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s.Substring(1);

    private static string Tooltip(in ShopItem it) => it.Kind switch
    {
        ShopItemKind.Villager => "One more villager. Each cottage houses two, more when extended.",
        ShopItemKind.Pace => "Everyone walks a little faster.",
        ShopItemKind.Load => "Everyone carries one more unit per trip.",
        ShopItemKind.Tools => "Work at the well, field and mill goes faster.",
        ShopItemKind.Value => "This good pays more for every unit delivered.",
        ShopItemKind.BellTrack => "The bell hurries everyone a little longer and rests a little less.",
        ShopItemKind.Extend => "One more room in a cottage: room for one more villager. Click a lit cottage.",
        ShopItemKind.Pave => "Pave one worn-in road: faster, and it carries more people. Click a lit road.",
        ShopItemKind.Ferry => "A ferry over the river: it crosses at any tide, a few people at a time.",
        ShopItemKind.Berth => "A second berth at the landing: two boats moor at once.",
        ShopItemKind.Crane => "A crane at the landing: boats ask for bigger cargoes.",
        ShopItemKind.BoatPrices => "Boats pay more for every unit.",
        ShopItemKind.RiverValue => "This good pays more for every unit delivered.",
        ShopItemKind.Raft => "A bigger raft: the ferry carries one more person.",
        ShopItemKind.Rope => "A faster rope: the ferry crosses quicker.",
        ShopItemKind.Toll => "Every boat through the drawbridge pays more toll.",
        ShopItemKind.Winch => "A faster winch: the drawbridge lifts quicker, so walkers wait less.",
        ShopItemKind.Regulars => RegularsHelp,
        ShopItemKind.Training => TrainingHelp,
        ShopItemKind.Hand => "One more place to work where people queue. Its workplace shows a marker.",
        _ => "A building for the town, placed on its plot by the town.",
    };

    /// <summary>The Training row's tooltip: what one step does, its numbers read from the simulation.</summary>
    public static string TrainingHelp
    {
        get
        {
            double p = Math.Round(TownRules.TradeStep * 100);
            return p > 0 ? $"Every villager works {p}% faster at its own trade and its goods fetch {p}% more."
                : "Every villager works faster at its own trade and its goods fetch more.";
        }
    }

    public const string RegularsHelp = "A regular customer: every delivery pays a little more.";
}
