using System;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// Night 3, round L2 (message-system.md section 4): the first minute teaches itself. Four short hints in the line, each
/// shown once ever (remembered in the settings file, <c>hints_seen</c>, never in the save), each leaving as soon as the
/// player has done what it says (at least 4 s on screen, at most its own limit).
/// </summary>
public partial class TownHud
{
    public static readonly string[] FirstHints =
    {
        "Your villager fetches water and carries it home. Every trip earns coin.",
        "You can afford your first upgrade. Click it in the shop, or press its key.",
        "Each level gives the town something new. The purse shows what comes next.",
        "Scroll the wheel down to zoom out: the town is a page in a book.",
        // n7 trades: once, when the Trades card first shows.
        "Every villager has a trade. Change them on the card at the left, or leave it to the town.",
    };
    /// <summary>Each hint's longest stay, seconds (0: until done).</summary>
    public static readonly float[] FirstHintMax = { 12f, 0f, 6f, 8f, 10f };
    public static string TradesHint => FirstHints[4];
    public const float FirstHintLeast = 4f;
    /// <summary>The scroll hint's text (tests count it in the note log: at most once).</summary>
    public static string ScrollHint => FirstHints[3];

    private string _firstHint = "";
    private int _firstNow = -1, _firstShownMask;
    private float _firstAge;
    private bool _tradeTouched, _firstCoin, _firstBought, _firstLevel, _firstZoomed;
    private double _firstLevelAt = -1;
    /// <summary>Hint 3 is given up once the first level banner has been gone this long (game seconds).</summary>
    public const double FirstLevelHintWindow = 60;

    /// <summary>The first-minute hint in the line now (-1 none) and every hint shown in this run, one bit each (tests).</summary>
    public int FirstHintNow => _firstNow;
    public int FirstHintsShown => _firstShownMask;

    /// <summary>The first level banner has appeared (TownHud.Messages.cs: when it shows, not when the level came).</summary>
    private void FirstLevelSeen() { if (_firstLevel) return; _firstLevel = true; _firstLevelAt = _drv.Town.Time; }

    private bool FirstSeen(int i) => (_firstShownMask & (1 << i)) != 0 || (Settings.GameSettings.Current?.HintSeen(i) ?? false);

    private void FirstEvent(in TownEvent e)
    {
        if (e.Kind == TownEventKind.Delivery) _firstCoin = true;
        else if (e.Kind == TownEventKind.Purchase) _firstBought = true;
        else if (e.Kind == TownEventKind.TradeChanged && e.Level == 1) _tradeTouched = true;
    }

    /// <summary>One frame: ends the hint showing when it is done, else starts the next one whose moment has come.</summary>
    private void FirstMinuteFrame(float dt, float viewSize, float slimSize)
    {
        var t = _drv.Town;
        if (viewSize >= slimSize) _firstZoomed = true;
        if (_firstNow >= 0)
        {
            // Real time, and only while the line shows it: a refusal or a mode in front of it does not use it up.
            if (LineKindNow == LineKind.FirstHint) _firstAge += MessageQueue.Clock(dt);
            bool done = _firstNow switch
            {
                0 => _firstCoin,
                1 => _firstBought,
                3 => _firstZoomed,
                4 => _tradeTouched,
                _ => false,
            };
            float max = FirstHintMax[_firstNow];
            if ((done && _firstAge >= FirstHintLeast) || (max > 0f && _firstAge >= max)) { _firstNow = -1; _firstHint = ""; }
            return;
        }
        if (_fullA < 0.5f) return;
        // A hint waits while a decision or a page is open (L2 review 2).
        if (t.ChoiceOpen || _choiceF.Value > 0f || AchievementsOpen || AgePageOpen) return;
        for (int i = 0; i < FirstHints.Length; i++)
        {
            if (FirstSeen(i)) continue;
            bool due = i switch
            {
                0 => t.Time < 30 && !_firstCoin,
                1 => !_firstBought && AnyAffordable(),
                2 => _firstLevel && !_large.Showing && _large.Waiting == 0 && t.Time - _firstLevelAt <= FirstLevelHintWindow,
                3 => t.Time >= ScrollHintAt && !_firstZoomed,
                4 => _tradesF.On && _tradesF.Value >= 1f && !_large.Showing && _large.Waiting == 0, // not over a banner (as hint 3)
                _ => false,
            };
            if (i == 3 && _firstZoomed) { _firstShownMask |= 1 << 3; continue; } // found it alone: no hint needed
            if (!due) continue;
            _firstNow = i;
            _firstAge = 0f;
            _firstHint = FirstHints[i];
            _firstShownMask |= 1 << i;
            Log(NoteLog, _firstHint);
            Settings.GameSettings.Current?.SetHintSeen(i);
            if (i == 1) PulseCheapest(); // "Click it in the shop": the cheapest row it means pulses once
            return;
        }
    }

    private bool AnyAffordable()
    {
        foreach (var r in _rowOrder) if (r.Item.Affordable && r.Item.Kind is not (ShopItemKind.Pave or ShopItemKind.Extend)) return true;
        return false;
    }

    private void PulseCheapest()
    {
        int id = -1; double best = double.MaxValue;
        foreach (var r in _rowOrder)
            if (r.Item.Affordable && r.Item.Kind is not (ShopItemKind.Pave or ShopItemKind.Extend) && r.Item.Price < best) { best = r.Item.Price; id = r.Id; }
        if (id >= 0 && !_pulseQueue.Contains(id)) _pulseQueue.Enqueue(id);
    }

    /// <summary>The Pave row's pill changes when a road wears in: it pulses once (message-system section 2).</summary>
    private void PavePillPulse()
    {
        foreach (var r in _rowOrder) if (r.Item.Kind == ShopItemKind.Pave && !_pulseQueue.Contains(r.Id)) _pulseQueue.Enqueue(r.Id);
    }
}
