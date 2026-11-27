using System;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game.Tests;

/// <summary>
/// Sheet v3 (docs/plans/n4/shop-sheet-v3.md, "Tests"): the right side's own rules, read from the live shop. S1 keys
/// ascend down the list; S2 every tall row has a second line, one name size, no ellipsis, the catalogue's names fit;
/// S3 no heading without a row, headings carry nothing, one foot line; S4 nothing of a pill outside it, no dashed frame,
/// four states; S5 compact rows look like tall ones; S6 no key cap in a navigation button, icons centred; S7 a row's
/// tooltip is a card (title, sentence, facts), never "Key:".
/// </summary>
public static partial class LayoutRules
{
    /// <summary>Where a key stands in 1 2 3 4 5 F G H 6 7 8 9 0 (-1: no key).</summary>
    public static int KeyOrder(string action) => action switch
    {
        "tt_buy_1" => 1, "tt_buy_2" => 2, "tt_buy_3" => 3, "tt_buy_4" => 4, "tt_buy_5" => 5,
        "tt_offer_1" => 6, "tt_offer_2" => 7, "tt_offer_3" => 8,
        "tt_buy_6" => 9, "tt_buy_7" => 10, "tt_buy_8" => 11, "tt_buy_9" => 12, "tt_buy_0" => 13,
        _ => -1,
    };

    public static void CheckShop(TownHud hud, string id, string canvas, FailFn fail, ref int checks)
    {
        var root = hud.GetNode<Control>("Root");
        var shop = root.GetNodeOrNull<Control>("Shop");
        if (shop == null || !Shows(shop)) return;
        int n = hud.ShownItems.Count, last = 0, nameSize = hud.RowNameSize;
        Font? nameFont = null;
        for (int i = 0; i < n; i++)
        {
            var r = hud.RowNode(i);
            if (r.Leaving || r.Alpha < 0.99f) continue;
            // S1: the keys ascend down the list.
            int k = KeyOrder(hud.RowAction(i));
            checks++;
            if (k >= 0 && k <= last) fail("S1-keys", id, canvas, $"row {i} '{r.Name.Text}' key order {k} after {last}");
            if (k >= 0) last = k;
            // S2: one name font and size; never cut; the second line of a tall row is there.
            checks++;
            var f = r.Name.GetThemeFont("font");
            nameFont ??= f;
            if (f != nameFont || r.Name.GetThemeFontSize("font_size") != nameSize)
                fail("S2-name", id, canvas, $"row '{r.Name.Text}' name font {r.Name.GetThemeFontSize("font_size")} px, the shop's is {nameSize}");
            checks++;
            if (TownHud.TextWidth(r.Name) > r.Name.Size.X + 0.5f || r.Name.Text.EndsWith("…") || r.Name.TextOverrunBehavior != TextServer.OverrunBehavior.NoTrimming)
                fail("S2-cut", id, canvas, $"row '{r.Name.Text}' name does not fit its box {r.Name.Size.X:0}");
            checks++;
            bool known = false;
            foreach (var c in TownHud.RowNameCatalogue) if (hud.ShownName(c) == r.Name.Text) known = true;
            if (!known) fail("S2-catalogue", id, canvas, $"row name '{r.Name.Text}' is not in TownHud.RowNameCatalogue");
            if (r.Tall)
            {
                checks++;
                if (!r.Line2.Visible || r.Line2.Text.Trim().Length == 0) fail("S2-line2", id, canvas, $"row '{r.Name.Text}' has no second line");
                checks++;
                if (r.Line2.Text.Contains(" short")) fail("S2-line2", id, canvas, $"row '{r.Name.Text}' line 2 says '{r.Line2.Text}' (the shortfall is the tooltip's)");
            }
            else
            {
                // S5: a compact row is the same row in one line: its icon and the same name.
                checks++;
                if (!r.Icon.Visible || r.Icon.Texture == null) fail("S5-compact", id, canvas, $"compact row '{r.Name.Text}' shows no icon");
                checks++;
                if (r.Key.Visible || r.Line2.Visible) fail("S5-compact", id, canvas, $"compact row '{r.Name.Text}' shows a key cap or a second line");
            }
            // The key cap sits under the icon (tall rows only).
            if (r.Key.Visible)
            {
                checks++;
                var ic = r.Icon.GetGlobalRect();
                var kc = r.Key.GetGlobalRect();
                if (kc.Position.Y < ic.End.Y || kc.Intersects(ic) || Math.Abs(kc.GetCenter().X - ic.GetCenter().X) > 1.01f)
                    fail("S1-keycap", id, canvas, $"row '{r.Name.Text}' key cap {kc} is not under its icon {ic}");
            }
            CheckPill(r, hud.UiScale, id, canvas, fail, ref checks);
            // S7: the tooltip's card.
            var tip = hud.RowTipParts(i);
            checks++;
            if (tip.title.Length == 0 || tip.sentence.Length == 0 || tip.facts.Length == 0 || tip.text.Contains("Key:"))
                fail("S7-tip", id, canvas, $"row '{r.Name.Text}' tooltip: title '{tip.title}', sentence '{tip.sentence}', {tip.facts.Length} facts; '{tip.text}'");
        }
        // S3: headings only over rows, nothing on them but the name and a hairline; at most one foot line.
        for (int g = 0; g < 5; g++)
        {
            if (!hud.GroupHeadShown(g)) continue;
            checks++;
            if (!hud.GroupHasRows(g)) fail("S3-heading", id, canvas, $"heading {g} shows with no row under it");
            foreach (var ch in hud.GroupHead(g).GetChildren())
            {
                checks++;
                if (ch is Control c && c.Name != "Name" && c.Name != "Rule") fail("S3-heading", id, canvas, $"heading {g} carries {c.Name}");
            }
        }
        checks++;
        if (hud.FootText.Contains('\n')) fail("S3-foot", id, canvas, $"the foot is more than one line: '{hud.FootText}'");
        // S4: no dashed frame anywhere in the interface.
        checks++;
        if (FindDash(root)) fail("S4-dash", id, canvas, "a dashed frame exists");
        CheckNav(root, id, canvas, fail, ref checks);
    }

    private static bool FindDash(Node n)
    {
        foreach (var ch in n.GetChildren())
        {
            if (ch.Name.ToString().Contains("Dash") || ch.GetType().Name.Contains("Dash")) return true;
            if (FindDash(ch)) return true;
        }
        return false;
    }

    /// <summary>S4: every part of a pill lies inside the pill, which clips; the wash inside its outline; the state's look.</summary>
    private static void CheckPill(ShopRow r, float s, string id, string canvas, FailFn fail, ref int checks)
    {
        var pill = r.Pill;
        var pr = pill.GetGlobalRect();
        checks++;
        if (!pill.ClipContents) fail("S4-pill", id, canvas, $"row '{r.Name.Text}' pill does not clip");
        foreach (var ch in pill.GetChildren())
        {
            if (ch is not Control c || !c.Visible) continue;
            checks++;
            if (!pr.Grow(0.5f).Encloses(c.GetGlobalRect())) fail("S4-pill", id, canvas, $"row '{r.Name.Text}' pill part {c.Name} {c.GetGlobalRect()} leaves the pill {pr}");
        }
        // The wash: every point inside the capsule (radius half the height).
        float h = pill.Size.Y, w = pill.Size.X, rad = h / 2f;
        foreach (var p in r.Wash.Points)
        {
            float cx = Math.Clamp(p.X, rad, w - rad);
            checks++;
            if (new Vector2(p.X - cx, p.Y - rad).Length() > rad + 0.5f) { fail("S4-wash", id, canvas, $"row '{r.Name.Text}' wash point {p} outside the outline"); break; }
        }
        var box = pill.GetThemeStylebox("panel") as StyleBoxFlat;
        var frame = r.Frame.Visible ? r.Frame.GetThemeStylebox("panel") as StyleBoxFlat : null;
        bool ok = r.State switch
        {
            ShopRow.PillState.Affordable => box != null && box.BgColor == UiKit.Wheat && box.BorderWidthTop == 0 && frame == null && !r.Wash.Visible,
            ShopRow.PillState.NotYet => box != null && box.BgColor == UiKit.Paper && frame != null && frame.BorderWidthTop == 1 && r.Wash.Visible,
            ShopRow.PillState.Blocked => box != null && box.BgColor == UiKit.Paper && frame != null && frame.BorderWidthTop == 1 && !r.Wash.Visible,
            ShopRow.PillState.On => box != null && box.BgColor == UiKit.Ink && frame == null && r.Price.Text == "On",
            _ => false,
        };
        checks++;
        if (!ok) fail("S4-state", id, canvas, $"row '{r.Name.Text}' pill does not look like its state {r.State}");
        // Prices: never set smaller in one row than in another (body), right-aligned beside the coin.
        checks++;
        if (r.Price.GetThemeFontSize("font_size") != UiKit.Type(s, UiKit.Body))
            fail("S4-price", id, canvas, $"row '{r.Name.Text}' price at {r.Price.GetThemeFontSize("font_size")} px, not body");
    }

    /// <summary>S6: a navigation button holds no key cap and its icon is centred (Achievements: its icon and count).</summary>
    private static void CheckNav(Control root, string id, string canvas, FailFn fail, ref int checks)
    {
        foreach (var name in new[] { "Achievements", "AgePage", "HomeMark", "PauseMark" })
        {
            var b = root.GetNodeOrNull<Control>(name);
            if (b == null || !Shows(b)) continue;
            foreach (var ch in b.GetChildren())
            {
                checks++;
                if (ch is Label l && (l.Name == "Key" || l.Name.ToString().StartsWith("Key"))) fail("S6-nav", id, canvas, $"{name} holds a key cap '{l.Text}'");
            }
            var icon = b.GetNodeOrNull<Control>("Icon");
            checks++;
            if (icon == null) { fail("S6-nav", id, canvas, $"{name} has no icon"); continue; }
            var br = b.GetGlobalRect();
            var ir = icon.GetGlobalRect();
            bool centred = Math.Abs(ir.GetCenter().Y - br.GetCenter().Y) <= 1.01f && (name == "Achievements" || Math.Abs(ir.GetCenter().X - br.GetCenter().X) <= 1.01f);
            if (!centred) fail("S6-nav", id, canvas, $"{name} icon {ir} is not centred in {br}");
        }
    }

    /// <summary>S2 with the fixture: every name of the catalogue fits its box at the shop's one size (body on the 320
    /// rail), and every line-2 text fits at caption.</summary>
    public static void CheckCatalogue(TownHud hud, HudZones z, string canvas, FailFn fail, ref int checks)
    {
        float box = ShopRow.NameBox(hud.RowWidth, hud.PillWidthUnits, z.S, true);
        int size = hud.RowNameSize;
        checks++;
        if (z.RailUnits >= 320 && size != UiKit.Type(z.S, UiKit.Body)) fail("S2-catalogue", "fixture", canvas, $"names are {size} px on the 320 rail, not body");
        foreach (var n in TownHud.RowNameCatalogue)
        {
            checks++;
            string shown = hud.ShownName(n);
            float w = UiKit.UiFont.GetStringSize(shown, HorizontalAlignment.Left, -1, size).X;
            if (w > box) fail("S2-catalogue", "fixture", canvas, $"'{shown}' is {w:0} wide at {size} px in a box of {box:0}");
        }
        float l2 = hud.RowWidth - UiKit.U(z.S, ShopRow.NameX) - UiKit.U(z.S, 8);
        int cap = UiKit.Type(z.S, UiKit.Caption);
        var lines = new System.Collections.Generic.List<string>();
        foreach (ShopItemKind k in Enum.GetValues(typeof(ShopItemKind))) lines.Add(TownHud.DoesLine(k));
        foreach (var t in new[] { "No room: wait for a cottage", "No worn-in road to pave", "Click a lit road", "Click a lit cottage", TownHud.WorthLine(999_990) })
            lines.Add(TownHud.BlockedLine(t));
        foreach (var t in lines)
        {
            checks++;
            float w = UiKit.UiFont.GetStringSize(t, HorizontalAlignment.Left, -1, cap).X;
            if (w > l2) fail("S2-line2", "fixture", canvas, $"line 2 '{t}' is {w:0} wide in {l2:0}");
        }
    }
}
