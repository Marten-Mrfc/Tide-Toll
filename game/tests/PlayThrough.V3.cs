using System;
using System.Threading.Tasks;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game.Tests;

/// <summary>
/// Round 5, rules v3 in the whole-age play: each choice of two is picked like a person would (the boon worth more now),
/// by a click on the card or by its key in turn; the charter drive's card is seen and the drive met; extra hands are
/// bought from their bookmarks like any purchase; touches arrive and are drawn; milestones are announced.
/// </summary>
public partial class PlayThrough
{
    private int _boonClicks, _boonKeys;
    private bool _driveSeen;

    private async Task Choose()
    {
        await Frames(1);
        var c = T.Choice;
        if (!Main.Hud.ChoiceCardVisible) { P("MISS the open choice has no card"); _misses++; return; }
        int side = c.BWorth > c.AWorth ? 1 : 0;
        bool byKey = (_boonClicks + _boonKeys) % 2 == 1;
        if (byKey) { await Key(BoundKey(side == 0 ? "tt_choose_a" : "tt_choose_b")); _keysUsed++; }
        else { await Click(TestInput.Centre(Main.Hud.BoonButton(side))); _clicks++; }
        var want = side == 0 ? c.A : c.B;
        if (T.ChoicePicked(c.Choice) != (int)want) { P($"MISS the {(byKey ? "key" : "click")} for {Town.BoonName(want)} took nothing"); _misses++; return; }
        if (byKey) _boonKeys++; else _boonClicks++;
        _decMin[Min]++;
        P($"BOON {Town.BoonName(want)} by {(byKey ? "key" : "click")} (worth {(side == 0 ? c.AWorth : c.BWorth):0.0} against {(side == 0 ? c.BWorth : c.AWorth):0.0})");
    }

    private void CheckRulesV3()
    {
        int hands = 0;
        for (int w = 0; w < T.WorkplaceCount; w++) hands += T.HandsBought(w);
        Check(_boonClicks >= 1 && _boonKeys >= 1, $"boons picked by click ({_boonClicks}) and by key ({_boonKeys})");
        Check(_driveSeen && T.Drive.Done, $"the charter drive's card seen ({_driveSeen}) and the drive met ({T.Drive.Done})");
        Check(hands >= 1, "an extra hand bought from its bookmark");
        Check(T.TouchCount > 0 && Main.Touches.Shown == T.TouchCount, $"touches arrived and are drawn ({Main.Touches.Shown} of {T.TouchCount})");
        Check(Main.Hud.Milestones >= 1, "milestones announced");
        GD.Print($"PLAY v3: boons click {_boonClicks} key {_boonKeys}, hands {hands}, touches {T.TouchCount}, milestones {Main.Hud.Milestones}, drive met {T.Drive.Done}");
    }
}
