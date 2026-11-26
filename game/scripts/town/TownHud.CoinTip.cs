using System;
using Godot;

namespace TideToll.Game;

/// <summary>
/// Night 3, L1 fix: the pointer's price tip and the "Suggested" tag carry the coin sign (the shop's coin icon) before
/// the amount, never the word "coin" (layout sheet section 4). A label gets a small coin icon child placed in a gap of
/// spaces left in its text; the text is rebuilt only when the amount or the words change.
/// </summary>
public partial class TownHud
{
    private static readonly StringName CoinSignName = "CoinSign";

    /// <summary>Sets "<paramref name="before"/>[coin] 1,234<paramref name="after"/>" on a tag label.</summary>
    private void SetCoinText(Label l, string before, double amount, string after)
    {
        int sig = HashCode.Combine(before, (long)Math.Floor(amount), after, _s);
        if (l.HasMeta(CoinSignName) && (int)l.GetMeta(CoinSignName) == sig) return;
        l.SetMeta(CoinSignName, sig);
        var icon = l.GetNodeOrNull<TextureRect>(new NodePath(CoinSignName));
        if (icon == null)
        {
            icon = UiKit.IconRect(_s, IconOr("ui-coin", "ui-coin"), 14);
            icon.Name = CoinSignName;
            l.AddChild(icon);
        }
        var font = l.GetThemeFont(FontName);
        int fs = l.GetThemeFontSize(FontSizeName);
        float space = Math.Max(1f, font.GetStringSize(" ", HorizontalAlignment.Left, -1, fs).X);
        int n = (int)Math.Ceiling((icon.Size.X + UiKit.U(_s, 4)) / space) + 1;
        l.Text = before + new string(' ', n) + Money(amount) + after;
        l.ResetSize();
        var box = l.GetThemeStylebox(NormalName);
        float x = (box?.GetMargin(Side.Left) ?? 0) + font.GetStringSize(before, HorizontalAlignment.Left, -1, fs).X + space * 0.5f;
        icon.Position = new Vector2(x, (l.Size.Y - icon.Size.Y) / 2f);
    }

    private static readonly StringName NormalName = "normal";
}
