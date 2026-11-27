using System;
using System.Collections.Generic;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game.Tests;

/// <summary>The rules of the layout sheet's section 8, read from the live interface by stable node names.</summary>
public static partial class LayoutRules
{
    public delegate void FailFn(string rule, string id, string canvas, string what);

    /// <summary>Zones by node name under Hud/Root; pages, the tooltip, map tags and coin numbers are not zones.</summary>
    private static readonly string[] ZoneNames = { "Ledger", "Slim", "Shop", "Bell", "Tide", "Achievements", "AgePage", "HomeMark", "PauseMark", "Banner", "Choice", "Line", "Person" };
    private static readonly HashSet<string> NotChecked = new() { "Floaters", "HandMarks", "HoverTip", "PersonTag", "Suggest", "Tooltip", "AchievementsPage", "FirstAgePage", "PageTurn", "Scrim" };

    private static bool Shows(Control c)
    {
        if (!c.IsVisibleInTree()) return false;
        for (Node? n = c; n is CanvasItem ci; n = n.GetParent()) if (ci.Modulate.A < 0.05f) return false;
        return true;
    }

    public static Rect2 Expected(HudZones z, string name, Control c) => name switch
    {
        "Ledger" => z.Ledger,
        "Slim" => new Rect2(z.Purse.Position, z.Purse.Size.X, z.P(40)),
        "Shop" => new Rect2(z.ShopMax.Position, z.ShopMax.Size.X, Math.Min(c.Size.Y, z.ShopMaxHeight)),
        "Bell" => z.Bell,
        "Tide" => z.Tide,
        "Achievements" => z.NavButton(0), "AgePage" => z.NavButton(1), "HomeMark" => z.NavButton(2), "PauseMark" => z.NavButton(3),
        "Banner" => z.Banner,
        "Choice" => z.Message(HudZones.ChoiceMaxH),
        "Line" => z.Line,
        "Person" => new Rect2(z.Person.Position, z.Person.Size.X, Math.Min(c.Size.Y, z.Person.Size.Y)), // n7: the card hugs its lines inside its zone (as the shop)
        _ => new Rect2(),
    };

    public static void Check(Main main, TownHud hud, HudZones z, string id, string canvas, FailFn fail, ref int checks, out int loud, out int quiet, out string counted)
    {
        var root = hud.GetNode<Control>("Root");
        var shown = new List<(string name, Control c, Rect2 r)>();
        foreach (var name in ZoneNames)
        {
            var c = root.GetNodeOrNull<Control>(name);
            if (c == null || !Shows(c)) continue;
            var r = c.GetGlobalRect();
            shown.Add((name, c, r));
            // Rule 1: the zone's rectangle is the sheet's.
            var want = Expected(z, name, c);
            checks++;
            if (!Near(r, want)) fail("1-zone", id, canvas, $"{name} at {r}, the sheet says {want}");
        }
        var win = new Rect2(Vector2.Zero, z.Canvas);
        var middle = z.Middle;
        for (int i = 0; i < shown.Count; i++)
        {
            var (name, c, r) = shown[i];
            // Rule 2: no two zones overlap.
            for (int j = i + 1; j < shown.Count; j++)
            {
                checks++;
                if (r.Grow(-1).Intersects(shown[j].r.Grow(-1))) fail("2-overlap", id, canvas, $"{name} {r} and {shown[j].name} {shown[j].r}");
            }
            // Rule 3 (window): every zone inside the window.
            checks++;
            if (!win.Grow(1).Encloses(r)) fail("3-cut", id, canvas, $"{name} {r} leaves the window {z.Canvas}");
            // Rule 4: the middle stays free (the choice may enter down to y 216).
            checks++;
            bool allowed = name == "Choice" && r.End.Y <= z.P(216) + 1;
            if (!allowed && r.Grow(-1).Intersects(middle)) fail("4-middle", id, canvas, $"{name} {r} enters the middle {middle}");
        }
        CheckFit(hud, z, id, canvas, fail, ref checks);
        CheckRows(hud, id, canvas, fail, ref checks);
        CheckShop(hud, id, canvas, fail, ref checks);
        CheckTips(hud, z, id, canvas, fail, ref checks);
        CheckMessages(hud, z, id, canvas, fail, ref checks);
        // Rule 5: the attention budget (night 3, L2: it fails the step; at most 3 loud and 4 quiet things at once).
        loud = 0; quiet = 0;
        var parts = new List<string>();
        int afford = 0, on = 0;
        for (int i = 0; i < hud.ShownItems.Count; i++)
        {
            var p = hud.RowPill(i);
            if (p == ShopRow.PillState.Affordable) afford++;
            if (p == ShopRow.PillState.On) on++;
        }
        if (afford > 0) { loud++; parts.Add($"wheat pills ({afford})"); }
        if (on > 0) { loud++; parts.Add("On pill"); }
        if (hud.BannerText.Length > 0) { loud++; parts.Add("banner"); }
        if (hud.ChoiceCardVisible) { loud++; parts.Add("choice"); }
        if (hud.NoteShowing.Length > 0) { quiet++; parts.Add("note"); }
        if (hud.LineText.Length > 0) { quiet++; parts.Add("line"); }
        if (hud.NextAgeButton.IsVisibleInTree() || hud.DriveCardVisible) { quiet++; parts.Add("strip"); }
        if (hud.RequestCardVisible && hud.RequestTimeBar < 0.25f) { quiet++; parts.Add("goal under a quarter"); }
        counted = $"loud {loud} [{string.Join(", ", parts)}] quiet {quiet}";
        checks++;
        if (loud > LoudLimit || quiet > QuietLimit) fail("5-budget", id, canvas, $"the attention budget: {counted} (limits loud {LoudLimit}, quiet {QuietLimit})");
    }

    public const int LoudLimit = 3, QuietLimit = 4;

    /// <summary>
    /// Night 3, L2 (message-system section 5): every message lies inside its zone (a large message may stand up to its
    /// 12 of arrival above it; a note up to 8 left of its place, inside the note section; the goal's content 8 below).
    /// </summary>
    public static void CheckMessages(TownHud hud, HudZones z, string id, string canvas, FailFn fail, ref int checks)
    {
        float m = UiKit.U(z.S, 12) + 1, n = UiKit.U(z.S, 8) + 1;
        int c = 0;
        void In(string what, Rect2 r, Rect2 zone)
        {
            if (r.Size.X <= 0) return;
            c++;
            if (!zone.Grow(1).Encloses(r)) fail("msg-zone", id, canvas, $"{what} {r} lies outside its zone {zone}");
        }
        In("the banner", hud.BannerLiveRect, z.Banner.GrowIndividual(0, m, 0, 0));
        In("the choice", hud.ChoiceRect, z.Message(HudZones.ChoiceMaxH).GrowIndividual(0, m, 0, 0));
        In("the note", hud.NoteRect, new Rect2(z.Note.Position, z.Note.Size));
        In("the line", hud.LineRect, z.Line);
        In("the goal's content", hud.RequestContentRect, z.Goal.GrowIndividual(0, 0, 0, n));
        checks += c;
    }

    private static bool Near(Rect2 a, Rect2 b) =>
        Math.Abs(a.Position.X - b.Position.X) <= 1.01f && Math.Abs(a.Position.Y - b.Position.Y) <= 1.01f &&
        Math.Abs(a.Size.X - b.Size.X) <= 1.01f && Math.Abs(a.Size.Y - b.Size.Y) <= 1.01f;

    /// <summary>Rule 3 (nothing cut, inside its zone, text fits its box) and rule 6 (only the sheet's sizes and cards).</summary>
    public static void CheckFit(TownHud hud, HudZones z, string id, string canvas, FailFn fail, ref int checks)
    {
        var root = hud.GetNode<Control>("Root");
        var sizes = new HashSet<int>();
        foreach (var t in UiKit.PlaySizes) sizes.Add(UiKit.Type(z.S, t));
        foreach (var zone in root.GetChildren())
        {
            if (zone is not Control zc || NotChecked.Contains(zc.Name) || !Shows(zc)) continue;
            var zr = zc.GetGlobalRect();
            bool scrolls = zc.Name == "Shop" && hud.ShopScrolls;
            Walk(zc, zc, zr, scrolls, sizes, hud, id, canvas, fail, ref checks);
        }
    }

    private static void Walk(Control zone, Control c, Rect2 zr, bool scrolls, HashSet<int> sizes, TownHud hud, string id, string canvas, FailFn fail, ref int checks)
    {
        foreach (var ch in c.GetChildren())
        {
            if (ch is not Control k || !Shows(k)) continue;
            var r = k.GetGlobalRect();
            string nm = UiAuditDump.StableName(k);
            bool clipped = scrolls && !zr.Encloses(r);
            if (!clipped && k.Size.X > 0 && k.Size.Y > 0 && k is not Label { Text: "" })
            {
                checks++;
                if (!zr.Grow(1).Encloses(r) && k.Name != "Pill") fail("3-zone", id, canvas, $"{nm} {r} leaves its zone {zone.Name} {zr}");
            }
            if (k is Label l && l.Text.Length > 0 && !clipped)
            {
                int px = l.GetThemeFontSize("font_size");
                checks++;
                if (!sizes.Contains(px)) fail("6-type", id, canvas, $"{nm} font {px} px is none of the four sizes");
                checks++;
                string? why = TextCut(l);
                if (why != null) fail("3-text", id, canvas, $"{nm} '{Short(l.Text)}': {why}");
            }
            if (k is PanelContainer or Panel)
            {
                var sb = k.GetThemeStylebox("panel");
                checks++;
                if (sb is not StyleBoxEmpty && !(sb.HasMeta("ui_card"))) fail("6-card", id, canvas, $"{nm} has a card style that is not the sheet's");
            }
            Walk(zone, k, zr, scrolls, sizes, hud, id, canvas, fail, ref checks);
        }
    }

    private static string Short(string s) => s.Length > 40 ? s[..40] + "..." : s;

    /// <summary>Why a label's text does not fit its box (null when it fits). Names may end in an ellipsis (the sheet).</summary>
    public static string? TextCut(Label l)
    {
        var f = l.GetThemeFont("font");
        int px = l.GetThemeFontSize("font_size");
        float lineH = f.GetHeight(px);
        if (l.AutowrapMode == TextServer.AutowrapMode.Off)
        {
            float w = 0;
            int nLines = 0;
            foreach (var line in l.Text.Split('\n')) { w = Math.Max(w, f.GetStringSize(line, HorizontalAlignment.Left, -1, px).X); nLines++; }
            lineH *= nLines;
            var sb = l.GetThemeStylebox("normal");
            float pad = sb?.GetMinimumSize().X ?? 0;
            bool ellipsisOk = l.Name == "Name" || l.Name == "Next";
            if (w + pad > l.Size.X + 1 && !ellipsisOk) return $"{w + pad:0} wide in a box of {l.Size.X:0}";
            if (lineH > l.Size.Y + 2) return $"{lineH:0} high in a box of {l.Size.Y:0}";
            return null;
        }
        float raw = f.GetMultilineStringSize(l.Text, HorizontalAlignment.Left, l.Size.X, px).Y;
        int lines = Mathf.RoundToInt(raw / Math.Max(1f, lineH));
        float h = lines * lineH + (lines - 1) * l.GetThemeConstant("line_spacing");
        if (l.MaxLinesVisible > 0 && lines > l.MaxLinesVisible) return $"{lines} lines where {l.MaxLinesVisible} show";
        if (h > l.Size.Y + 2) return $"{h:0} high in a box of {l.Size.Y:0}";
        return null;
    }

    /// <summary>Rule 7: locked and finished purchases are not rows; a key cap shows only a key bound to its action.</summary>
    private static void CheckRows(TownHud hud, string id, string canvas, FailFn fail, ref int checks)
    {
        int offer = 0;
        for (int i = 0; i < hud.ShownItems.Count; i++)
        {
            var it = hud.ShownItems[i];
            int ord = it.Kind == ShopItemKind.Building ? offer++ : 0;
            checks++;
            if (it.Locked || it.Finished) fail("7-rows", id, canvas, $"row {i} '{it.Name}' is locked or finished");
            string key = hud.RowKey(i);
            if (key.Length == 0) continue;
            string action = TownHud.ActionFor(it, ord);
            bool bound = false;
            if (action.Length > 0 && InputMap.HasAction(action))
                foreach (var ev in InputMap.ActionGetEvents(action))
                    if (ev is InputEventKey k && Settings.GameInput.KeyName(Settings.GameInput.KeyOf(k)) == key) bound = true;
            checks++;
            if (!bound) fail("7-keys", id, canvas, $"row '{it.Name}' shows [{key}], not bound to {action}");
        }
    }

    /// <summary>Rule 8: TipRect for every owner: inside the window, not over its owner.</summary>
    private static void CheckTips(TownHud hud, HudZones z, string id, string canvas, FailFn fail, ref int checks)
    {
        var root = hud.GetNode<Control>("Root");
        var size = new Vector2(z.P(HudZones.TipW), z.P(140));
        var win = new Rect2(Vector2.Zero, z.Canvas);
        int n0 = 0;
        void One(Control? c, HudZones.TipSide side)
        {
            if (c == null || !Shows(c)) return;
            var o = c.GetGlobalRect();
            var r = z.TipRect(o, size, side);
            n0++;
            if (!win.Encloses(r) || r.Intersects(o)) fail("8-tip", id, canvas, $"{UiAuditDump.StableName(c)} {o}: tip {r}");
        }
        foreach (var b in hud.Marks) One(b, HudZones.TipSide.Left);
        var body = root.GetNodeOrNull<Control>("Shop/Body");
        if (body != null) foreach (var n in body.GetChildren()) if (n is Control g && g.Name.ToString().StartsWith("Group")) One(g, HudZones.TipSide.Left);
        One(root.GetNodeOrNull<Control>("Bell"), HudZones.TipSide.Right);
        One(root.GetNodeOrNull<Control>("Tide"), HudZones.TipSide.Right);
        One(root.GetNodeOrNull<Control>("Ledger/Purse"), HudZones.TipSide.Right);
        foreach (var n in new[] { "Achievements", "AgePage", "HomeMark", "PauseMark" }) One(root.GetNodeOrNull<Control>(n), HudZones.TipSide.Above);
        checks += n0;
    }
}
