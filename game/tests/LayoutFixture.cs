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
        ("Line/Box/Text", "Extend: click a lit cottage. Hold Shift to extend several. E or Esc to stop."),
        ("Bell/State", "rests 99 s"),
        ("Achievements/Count", "40 of 40"),
    };

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
            l.Text = text;
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
            if (price != null)
            {
                bool from = b.Name == "Row3000"; // Pave: "from" and the cheapest road's price
                // As the row does (ShopRow.FitPrice): body when it fits, else caption.
                price.Text = from ? "from 9,999" : "9.99M";
                if (from) { price.GetParent().GetNode<Control>("Coin").Visible = false; price.Position = new Vector2(UiKit.U(hud.UiScale, 8), 0); price.Size = new Vector2(price.GetParent<Control>().Size.X - UiKit.U(hud.UiScale, 12), price.Size.Y); }
                price.AddThemeFontSizeOverride("font_size", UiKit.Type(hud.UiScale, UiKit.Body));
                if (TownHud.TextWidth(price) > price.Size.X) price.AddThemeFontSizeOverride("font_size", UiKit.Type(hud.UiScale, UiKit.Caption));
            }
            var line2 = b.GetNodeOrNull<Label>("Line2");
            if (line2 != null && line2.Visible)
            {
                // As the row does (TownHud.RowContent): the worth is dropped whole when the line does not fit.
                line2.Text = "+120 a minute · 49,999 short";
                if (TownHud.TextWidth(line2) > line2.Size.X) line2.Text = "49,999 short";
            }
        }
    }
}
