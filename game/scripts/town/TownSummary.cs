using System;
using System.Text;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// The left page of the book as the town's summary, printed in the book's fonts (Uncial Antiqua for the name, Alegreya
/// for the rest): the town's name (made from the seed), the age and level, then villagers, cottages, roads and how many
/// are paved, coin earned in all, trips, time in the town, the next gift and achievements. Label3Ds lying on the page,
/// so it is part of the book at every zoom. Rewritten twice a second while the page can be seen, and only when a value
/// changed (no allocation otherwise).
/// </summary>
public partial class TownSummary : Node3D
{
    private TownDriver _drv = null!;
    private Label3D _achHead = null!;
    private readonly Label3D[] _achNames = new Label3D[2], _achVals = new Label3D[2];
    private string _lastAch = "";
    private Label3D _name = null!, _sub = null!, _keys = null!, _vals = null!, _next = null!, _boons = null!;
    private readonly System.Collections.Generic.List<Boon> _held = new();
    private int _boonSig = -1;
    private bool _keysRiver;
    private double _clock = 1e9;
    private readonly StringBuilder _sb = new(256);
    private string _lastVals = "";
    /// <summary>Set by Main: whether the left page can be seen (skips the work when it cannot).</summary>
    public bool Watched = true;
    public int Rewrites { get; private set; }

    public static readonly string[] Rows = { "Villagers", "Cottages", "Roads", "Paved roads", "Coin earned", "Trips", "Time in the town", "Achievements" };
    /// <summary>Round 5: the second age adds these rows (its achievements count in the Achievements row then).</summary>
    public static readonly string[] RiverRows = { "River cottages", "Boats filled", "Tolls taken" };

    public void Init(Node3D leftPage, TownDriver drv)
    {
        _drv = drv;
        Name = "Summary";
        leftPage.AddChild(this);
        // Round 3: the summary in the upper half, the achievements with their progress in the lower half.
        _name = BookBuilder.AddLabel(this, "TownName", "", new Vector3(0, 0.1f, -136), 1150, Palette.Ink, UiKit.Display);
        _sub = BookBuilder.AddLabel(this, "Age", "", new Vector3(0, 0.1f, -119), 620, Palette.FadedInk, UiKit.Serif);
        _keys = BookBuilder.AddLabel(this, "Keys", string.Join("\n", Rows), new Vector3(-80, 0.1f, -64), 600, Palette.Ink, UiKit.Serif);
        _keys.HorizontalAlignment = HorizontalAlignment.Left;
        _keys.LineSpacing = 4f;
        _vals = BookBuilder.AddLabel(this, "Values", "", new Vector3(112, 0.1f, -64), 600, Palette.Ink, UiKit.Serif);
        _vals.HorizontalAlignment = HorizontalAlignment.Right;
        _vals.LineSpacing = 4f;
        _next = BookBuilder.AddLabel(this, "Next", "", new Vector3(0, 0.1f, -21), 560, Palette.FadedInk, UiKit.Serif);
        // Round 5: the boons the town holds, under the next gift.
        _boons = BookBuilder.AddLabel(this, "Boons", "", new Vector3(0, 0.1f, -8), 540, Palette.Ink, UiKit.Serif);
        _boons.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _boons.Width = 3400; // round 6: one line for four boons (it wrapped into the achievements' heading)
        _next.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _next.Width = 2600;
        _achHead = BookBuilder.AddLabel(this, "AchievementsHead", "Achievements", new Vector3(0, 0.1f, 2), 760, Palette.Ink, UiKit.Serif);
        for (int c = 0; c < 2; c++)
        {
            // Round 6: the left column starts at -84 (was -114, -112 for the rows): at the page stage the screen's left edge
            // cut the row names, so the page's words keep to its inner part.
            float x0 = c == 0 ? -84 : 8, x1 = c == 0 ? -4 : 114;
            _achNames[c] = BookBuilder.AddLabel(this, $"AchNames{c}", "", new Vector3(x0, 0.1f, 52), 520, Palette.Ink, UiKit.Serif);
            _achNames[c].HorizontalAlignment = HorizontalAlignment.Left;
            _achNames[c].LineSpacing = 1f;
            _achVals[c] = BookBuilder.AddLabel(this, $"AchValues{c}", "", new Vector3(x1, 0.1f, 52), 520, Palette.FadedInk, UiKit.Serif);
            _achVals[c].HorizontalAlignment = HorizontalAlignment.Right;
            _achVals[c].LineSpacing = 1f;
        }
        drv.Replaced += Force;
        Force();
    }

    public override void _ExitTree() { if (_drv != null) _drv.Replaced -= Force; }

    private void Force() { _clock = 1e9; _lastVals = ""; _lastAch = ""; _boonSig = -1; if (_name != null) _name.Text = TownName.Of(_drv.Town.Seed); }

    public string NameText => _name.Text;
    public string AgeText => _sub.Text;
    public string ValuesText => _vals.Text;
    public string NextText => _next.Text;
    public string BoonsText => _boons.Text;
    public string KeysText => _keys.Text;

    public override void _Process(double delta)
    {
        _clock += delta;
        if (!Watched || _clock < 0.5) return;
        UpdateNow();
    }

    /// <summary>
    /// Writes the page now (round 7: PauseMenu, in the frame before a transition's first, so the left page turning with
    /// the board shows the town as it is and its text never changes while it moves).
    /// </summary>
    public void UpdateNow()
    {
        _clock = 0;
        var t = _drv.Town;
        _sb.Clear();
        _sb.Append(t.InRiverAge ? "The second age, level " : "The first age, level ").Append(t.Level).Append(" of ").Append(t.MaxLevel);
        if (!Same(_sb, _sub.Text)) _sub.Text = _sb.ToString();
        _sb.Clear();
        var v = Values(t);
        if (t.InRiverAge != _keysRiver)
        {
            _keysRiver = t.InRiverAge;
            _keys.Text = _keysRiver ? string.Join("\n", Rows) + "\n" + string.Join("\n", RiverRows) : string.Join("\n", Rows);
            // Round 6: eleven rows in the room of eight (the last row ran into the next line).
            foreach (var l in new[] { _keys, _vals }) { l.PixelSize = (_keysRiver ? 450 : 600) / 10000f; l.LineSpacing = _keysRiver ? 2f : 4f; }
        }
        int rows = t.InRiverAge ? v.Length : Rows.Length;
        for (int i = 0; i < rows; i++) { if (i > 0) _sb.Append('\n'); _sb.Append(v[i]); }
        if (!Same(_sb, _lastVals)) { _lastVals = _sb.ToString(); _vals.Text = _lastVals; Rewrites++; }
        // Round 6: in the second age the line is the river's next gift, or the seal (it said the river comes next).
        if (t.Level != _nextLevel || t.NextAgeRung != _nextRung || t.InRiverAge != _nextRiver || t.HasSeal != _nextSeal)
        {
            _nextLevel = t.Level; _nextRung = t.NextAgeRung; _nextRiver = t.InRiverAge; _nextSeal = t.HasSeal;
            _next.Text = t.HasSeal ? "The borough seal is the town's. Both ages are complete."
                : t.InRiverAge ? t.NextGift
                : t.NextAgeRung ? "The first age is complete. The river comes next." : t.NextGift;
        }
        Achievements(t);
        Boons(t);
    }

    /// <summary>"Boons: Wider lanes, A deeper well" (rewritten only when one is taken).</summary>
    private void Boons(Town t)
    {
        int sig = 0;
        for (int c = 0; c < TownRules.ChoiceLevels.Length; c++) sig = sig * 9 + t.ChoicePicked(c) + 1;
        if (sig == _boonSig) return;
        _boonSig = sig;
        t.BoonsHeld(_held);
        if (_held.Count == 0) { _boons.Text = ""; return; }
        var b = new StringBuilder(_held.Count == 1 ? "Boon: " : "Boons: ");
        for (int i = 0; i < _held.Count; i++) { if (i > 0) b.Append(", "); b.Append(Town.BoonName(_held[i])); }
        _boons.Text = b.ToString();
    }

    /// <summary>The achievements in two columns, name and state ("Done", "37 of 100"), rewritten only when a state changes.</summary>
    private void Achievements(Town t)
    {
        _sb.Clear();
        bool river = t.InRiverAge;
        int n1 = t.AchievementCount, n2 = river ? t.RiverAchievementCount : 0, n = n1 + n2;
        for (int i = 0; i < n; i++) { var a = i < n1 ? t.Achievement(i) : t.RiverAchievement(i - n1); _sb.Append(a.Unlocked ? 'D' : 'n').Append((long)Math.Floor(a.Value * (a.Target <= 1 ? 100 : 1))).Append(','); }
        if (Same(_sb, _lastAch)) return;
        _lastAch = _sb.ToString();
        // Round 6: after the river is rung in the page lists both ages (it listed only the river's): the first age's, then
        // the river's under a line of their own, in two columns, set smaller when the rows would run down the page.
        int rows = n + (river ? 1 : 0);
        int half = (rows + 1) / 2;
        float size = Math.Clamp(520f * 12f / Math.Max(12, half), 400f, 520f);
        float block = half * 1.385f * size / 100f;
        var nm = new StringBuilder(); var vl = new StringBuilder();
        for (int c = 0; c < 2; c++)
        {
            nm.Clear(); vl.Clear();
            for (int r = c * half; r < Math.Min(rows, (c + 1) * half); r++)
            {
                if (nm.Length > 0 || r > c * half) { nm.Append('\n'); vl.Append('\n'); }
                if (river && r == n1) { nm.Append("On the river"); vl.Append($"{RiverAchieved(t)} of {n2}"); continue; }
                int i = river && r > n1 ? r - 1 : r;
                var a = i < n1 ? t.Achievement(i) : t.RiverAchievement(i - n1);
                nm.Append(a.Name);
                vl.Append(TownHud.AchievementState(a));
            }
            _achNames[c].Text = nm.ToString();
            _achVals[c].Text = vl.ToString();
            foreach (var l in new[] { _achNames[c], _achVals[c] })
            {
                l.PixelSize = size / 10000f;
                l.Position = new Vector3(l.Position.X, l.Position.Y, 9f + block / 2f);
            }
        }
        _achHead.Text = river ? $"Achievements of both ages, {Achieved(t) + RiverAchieved(t)} of {n}" : $"Achievements, {Achieved(t)} of {n}";
    }

    public string AchievementsText => _achNames[0].Text + "\n" + _achNames[1].Text;

    private int _nextLevel = -1;
    private bool _nextRung, _nextRiver, _nextSeal;
    private readonly string[] _v = new string[11];
    private readonly long[] _num = new long[11];

    /// <summary>The values column, row by row (cached strings: rebuilt only for numbers that changed).</summary>
    private string[] Values(Town t)
    {
        long mins = (long)(t.Time / 60.0);
        Set(0, t.VillagerCount, null);
        Set(1, Cottages(t), null);
        Set(2, OpenRoads(t), null);
        Set(3, t.CountPaved(), null);
        Set(4, (long)Math.Floor(t.CoinEarned), "coin");
        Set(5, t.Trips, null);
        Set(6, mins, "time");
        Set(7, Achieved(t) + (t.InRiverAge ? RiverAchieved(t) : 0), "ach");
        Set(8, RiverCottages(t), null);
        Set(9, t.InRiverAge ? t.BoatsFilled : 0, null);
        Set(10, t.InRiverAge ? t.Tolls : 0, null);
        return _v;
    }

    private void Set(int i, long n, string? kind)
    {
        if (_v[i] != null && _num[i] == n) return;
        _num[i] = n;
        _v[i] = kind switch
        {
            "coin" => TownHud.Money(n),
            "time" => n >= 60 ? $"{n / 60} h {n % 60:00} min" : $"{n} min",
            "ach" => $"{n} of {TownAchievementsCount(_drv.Town)}",
            _ => n.ToString(TownHud.Inv),
        };
    }

    private static int TownAchievementsCount(Town t) => t.AchievementCount + (t.InRiverAge ? t.RiverAchievementCount : 0);

    public static int RiverAchieved(Town t)
    {
        int n = 0;
        for (int i = 0; i < t.RiverAchievementCount; i++) if (t.RiverAchievement(i).Unlocked) n++;
        return n;
    }

    public static int Cottages(Town t)
    {
        int n = 0;
        foreach (var p in t.Plan.Plots) if (p.Kind == PlotKind.Cottage && t.PlotGiven(p.Id)) n++;
        return n;
    }

    /// <summary>Round 6: the river cottages given (the book counted only the first age's).</summary>
    public static int RiverCottages(Town t)
    {
        var rp = t.RiverPlan;
        if (rp == null) return 0;
        int n = 0;
        for (int i = 0; i < rp.Plots.Length; i++) if (rp.Plots[i].Kind == RiverPlotKind.Cottage && t.RiverPlotGiven(i)) n++;
        return n;
    }

    public static int OpenRoads(Town t)
    {
        int n = 0;
        for (int r = 0; r < t.RoadCount; r++) if (t.RoadOpen(r)) n++;
        return n;
    }

    public static int Achieved(Town t)
    {
        int n = 0;
        for (int i = 0; i < t.AchievementCount; i++) if (t.Achievement(i).Unlocked) n++;
        return n;
    }

    private static bool Same(StringBuilder sb, string s)
    {
        if (sb.Length != s.Length) return false;
        for (int i = 0; i < s.Length; i++) if (sb[i] != s[i]) return false;
        return true;
    }
}

/// <summary>A plain village name made from the seed out of a small table of syllables (Ashford, Elmley, Thornby...).</summary>
public static class TownName
{
    private static readonly string[] Heads =
    {
        "Ash", "Brook", "Cold", "Elm", "Fen", "Hol", "Kings", "Lang", "Mar", "Nether", "Oak", "Red", "Stan", "Thorn", "Wick", "Wood",
        "Bar", "Chal", "Dun", "Hart",
    };
    private static readonly string[] Tails = { "ford", "ham", "ley", "ton", "by", "field", "wick", "bury", "stead", "combe", "worth", "well", "thorpe", "brook" };

    public static string Of(int seed)
    {
        uint h = (uint)seed * 2654435761u;
        h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
        string a = Heads[h % (uint)Heads.Length], b = Tails[(h / (uint)Heads.Length) % (uint)Tails.Length];
        // Avoid a doubled letter at the join reading oddly (Brookbrook, Thornthorpe).
        if (a.EndsWith(b.Substring(0, 1), StringComparison.OrdinalIgnoreCase) && a.Length > 3) b = Tails[(h / 7u) % (uint)Tails.Length];
        if (a.ToLowerInvariant() == b) b = "ton";
        return a + b;
    }
}
