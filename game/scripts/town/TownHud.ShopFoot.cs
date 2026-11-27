using System;
using System.Collections.Generic;
using System.Text;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>An eased place for the shop's moving parts (a heading, the card's bottom edge): the rows' slide curve.</summary>
public sealed class ShopSlide
{
    public float Y = -1, From, To, T = -1, Alpha, Want;
    public bool Wanted;

    public void Reset() { Y = -1; T = -1; Alpha = 0; Wanted = false; }

    public void Slide(float dt, float length)
    {
        if (T < 0) return;
        T += dt;
        float u = Math.Clamp(T / length, 0, 1);
        Y = From + (To - From) * BookMotion.Smoother(u);
        if (u >= 1) T = -1;
    }

    /// <summary>The card's bottom edge: a new target starts a slide from where it is (held while the pointer holds the rows).</summary>
    public void Aim(float target, bool hold)
    {
        if (Y < 0) { Y = To = target; return; }
        if (hold || Math.Abs(target - To) <= 0.5f) return;
        From = Y; To = target; T = 0;
    }

    public void Step(float dt) => Slide(dt, TownHud.RowSlide);
}

/// <summary>
/// Sheet v3: the shop's foot line ("Next: Basket at level 3", only while something is locked; its tooltip lists every
/// locked purchase with its level and the finished ones) and the rows' hover and pressed band.
/// </summary>
public partial class TownHud
{
    private int _footSig = int.MinValue;
    private readonly List<ShopItem> _footOrder = new();
    private readonly StringBuilder _footSb = new();

    private bool LockedNow(in ShopItem it, Town t) => IsLocked(it) && !(it.Reason == "Unlocks at a later level" && t.Level >= t.MaxLevel);

    /// <summary>The level in "Unlocks at level 7" (int.MaxValue when the reason names none).</summary>
    private static int LockLevel(string reason)
    {
        const string at = "Unlocks at level ";
        return reason.StartsWith(at, StringComparison.Ordinal) && int.TryParse(reason.AsSpan(at.Length), out int n) ? n : int.MaxValue;
    }

    private void FootFrame(Town t)
    {
        int sig = 17;
        foreach (var it in _raw) if (!IsRowItem(it, t)) sig = HashCode.Combine(sig, (int)it.Kind, it.Index, it.Reason, it.Name);
        if (sig == _footSig) return;
        _footSig = sig;
        // In list order (the display's), the locked purchase with the lowest level; "+N more" for others at that level.
        Grouped(_raw, _footOrder, all: true);
        int best = -1, bestLevel = int.MaxValue, same = 0;
        for (int i = 0; i < _footOrder.Count; i++)
        {
            var it = _footOrder[i];
            if (!LockedNow(it, t)) continue;
            int lv = LockLevel(it.Reason);
            if (best < 0 || lv < bestLevel) { best = i; bestLevel = lv; same = 0; }
            else if (lv == bestLevel) same++;
        }
        string text = "";
        if (best >= 0)
        {
            var it = _footOrder[best];
            string first = $"Next: {RowName(it)} {Lower(FoldLock(it.Reason))}";
            text = same > 0 ? $"{first} · +{same} more" : first;
            _footText.Text = text;
            if (TextWidth(_footText) > _footText.Size.X) text = first;
            _footText.Text = text;
            if (TextWidth(_footText) > _footText.Size.X) text = $"Next: {RowName(it)}";
        }
        _footText.Text = text;
        _foot.Visible = text.Length > 0;
    }

    /// <summary>The foot's tooltip: every locked purchase with its level, then the finished ones.</summary>
    private string FootTip()
    {
        var t = _drv.Town;
        var sb = _footSb.Clear();
        bool any = false;
        foreach (var it in _footOrder)
        {
            if (!LockedNow(it, t)) continue;
            sb.Append(any ? "\n" : "Still to come:\n").Append(RowName(it)).Append(": ").Append(Lower(FoldLock(it.Reason)));
            any = true;
        }
        bool done = false;
        foreach (var it in _footOrder)
        {
            if (IsRowItem(it, t) || LockedNow(it, t)) continue;
            sb.Append(done ? ", " : any ? "\nDone: " : "Done: ").Append(RowName(it));
            done = true;
        }
        return sb.ToString();
    }

    /// <summary>Hover and pressed band of every row (no strings; the band changes only when its look does).</summary>
    private void BandFrame()
    {
        bool over = PointerOverShop() && !(_main.Menu != null && _main.Menu.IsPaused);
        var p = _shop.GetGlobalMousePosition();
        foreach (var r in _rows)
        {
            bool on = over && !r.Leaving && r.Button.Visible && r.Alpha > 0.5f && r.Button.GetGlobalRect().HasPoint(p);
            var mode = r.Button.GetDrawMode();
            r.SetBand(on, mode is BaseButton.DrawMode.Pressed or BaseButton.DrawMode.HoverPressed);
        }
    }
}
