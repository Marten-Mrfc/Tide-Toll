using Godot;

namespace TideToll.Game.Tests;

/// <summary>
/// game-layout's longest-text fixture (layout sheet section 8, rule 3): the longest real text of every kind the
/// interface shows (from message-inventory.md and a finished second age: largest numbers, longest banner, note,
/// request and hint), written into the live labels by their stable names; then every label must still fit its box.
/// </summary>
public static class LayoutFixture
{
    public static readonly (string path, string text)[] Texts =
    {
        ("Ledger/Purse/Coin", "9,999,999"),
        ("Ledger/Purse/Rate", "+99,999 a minute"),
        ("Ledger/Purse/Level", "Level 28, the last"),
        ("Ledger/Purse/Next", "Next: a cottage and its lane, and a second bucket for the well"),
        ("Ledger/Goal/Request/Who", "Request from the skipper Isolde"),
        ("Ledger/Goal/Request/Ask", "180 Cloth at the landing"),
        ("Ledger/Goal/Request/Done", "Loaded 179 of 180, leaves in 170 s"),
        ("Ledger/Goal/Request/Reward", "Pays 1,240 a unit, more when filled"),
        ("Ledger/NoteSection/Note", "Achievement: A town of many trades. Every workplace has an extra hand and a full queue."),
        ("Banner/Box/Title", "The borough seal"),
        ("Banner/Box/Text", "The town is granted its charter. Ring in the next age when you are ready, and read its page."),
        // Fix round: the longer of the longest mode help and the longest first-minute hint (the trades' hint, 87 characters);
        // the card grows upward for two lines (TownHud.FitLine, run by Apply below).
        ("Line/Box/Text", Longer("Extend: click a lit cottage. Hold Shift to extend several. E or Esc to stop.", LongestFirstHint)),
        ("Bell/State", "rests 99 s"),
        ("Achievements/Count", "40 of 40"),
        // n7: the villager's card (the longest name with a by-name, home, leaning, now and counts).
        ("Person/Name", "Yolande of the brook 2"),
        // n7 trades: line 2 is the like and the home; the home is dropped whole by the card when it does not fit, so the
        // longest text the card can show alone is the longest like (or the longest home with no like). The leaning line
        // is gone (its label stays hidden).
        ("Person/Home", "Likes carrying water"),
        ("Person/Trade", " · Carrier"),
        ("Person/TradeLabel", "Trade"),
        // The Trades card: the longest names and a two-figure count in every line.
        ("Trades/Heading", "Trades"),
        ("Trades/Switch/Caption", "Town fills trades"),
        ("Trades/LineCarriers/Name", "Carriers"), ("Trades/LineCarriers/Count", "99"),
        ("Trades/LineReapers/Name", "Reapers"), ("Trades/LineReapers/Count", "99"),
        ("Trades/LineMillers/Name", "Millers"), ("Trades/LineMillers/Count", "99"),
        ("Trades/LineHelpers/Name", "Helpers"), ("Trades/LineHelpers/Count", "99"),
        ("Person/Now", "Carrying cloth to the landing"),
        ("Person/Counts", "999 trips · earned     9,999,999"),
    };

    private static string Longer(string a, string b) => b.Length > a.Length ? b : a;

    public static string LongestFirstHint
    {
        get { string l = ""; foreach (var h in TownHud.FirstHints) if (h.Length > l.Length) l = h; return l; }
    }

    public static void Apply(TownHud hud)
    {
        var root = hud.GetNode<Control>("Root");
        foreach (var (path, text) in Texts)
        {
            var l = root.GetNodeOrNull<Label>(path);
            if (l == null)
            {
                var names = new System.Text.StringBuilder();
                foreach (var ch in root.GetChildren()) names.Append(ch.Name).Append(' ');
                GD.Print($"LAYOUT fixture: no label at {path} (root has {names})");
                continue;
            }
            // L2: a note is two lines at most (message-system 1): the longest note goes in as the note section sets it,
            // cut at a word with an ellipsis when it would take a third line (was: written raw into a 3-line label).
            l.Text = path == "Ledger/NoteSection/Note" ? hud.NoteFit(text) : text;
            if (path == "Line/Box/Text") hud.FitLineFor(text);
            l.GetParent<Control>().Visible = true;
            l.Visible = true;
        }
        // Rows: the largest prices and the longest second lines.
        var body = root.GetNodeOrNull<Control>("Shop/Body");
        if (body == null) return;
        foreach (var n in body.GetChildren())
        {
            if (n is not Button b || !b.Name.ToString().StartsWith("Row")) continue;
            var price = b.GetNodeOrNull<Label>("Pill/Price");
            ShopRow? row = null;
            for (int i = 0; i < hud.ShownItems.Count; i++) if (hud.RowNode(i).Button == b) row = hud.RowNode(i);
            if (price != null && row != null)
            {
                // Sheet v3: the largest price a pill shows, at body size in every row, laid out as the row lays it out
                // (Pave: "from" centred, or beside the coin when that does not fit). Was: set in caption when it did not fit.
                int bodyPx = UiKit.Type(hud.UiScale, UiKit.Body);
                double widest = 0; float best = -1;
                foreach (double v in new[] { 99_999d, 999_999d, 9_999_999d, 99_999_999d })
                {
                    float w = UiKit.TabBold.GetStringSize(hud.PillPrice(v), HorizontalAlignment.Left, -1, bodyPx).X;
                    if (w > best) { best = w; widest = v; }
                }
                var (text, centred) = b.Name == "Row3000" ? hud.PaveFace(widest) : (hud.PillPrice(widest), false);
                row.SetPrice(text, centred);
                price.AddThemeFontOverride("font", UiKit.TabBold);
                price.AddThemeFontSizeOverride("font_size", bodyPx);
            }
            var line2 = b.GetNodeOrNull<Label>("Line2");
            if (line2 != null && line2.Visible) line2.Text = "Cottages are full";
        }
    }
}
