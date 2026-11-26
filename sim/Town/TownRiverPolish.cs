using System;

namespace TideToll.Sim.Town;

/// <summary>
/// River polish (docs/plans/town-river-polish-progress.md): boats' cargo counted in loads for display. Boats ask for a share
/// of the town's coin rate in units (hundreds at the seal) while a villager carries a few units, so the game shows the hold in
/// loads: one load is what one villager carries now (Town.LoadUnits). The economy still counts units (Want, Loaded).
/// </summary>
public sealed partial class Town
{
    private readonly string[] _cargoText = new string[TownRulesRiver.MaxBoats];
    private readonly int[] _cargoKey = new int[TownRulesRiver.MaxBoats * 4];

    /// <summary>Loads of a number of units at the current load size, rounded up.</summary>
    public int LoadsOf(int units) => units <= 0 ? 0 : (units + LoadUnits - 1) / LoadUnits;

    /// <summary>"Flour 12 of 30 loads" for boat slot i ("" for an unused slot). Loaded is rounded down, wanted up, so a boat
    /// reads full only when it is. The string is kept and rebuilt only when a number in it changes.</summary>
    public string BoatCargoText(int i)
    {
        var r = R;
        if (r == null || i < 0 || i >= r.Boats.Length || i >= _cargoText.Length) return "";
        var b = r.Boats[i];
        if (b.Phase == BoatPhase.Gone || b.Good == Good.None) return "";
        int want = LoadsOf(b.Want), loaded = Math.Min(want, b.Loaded / LoadUnits);
        if (b.Loaded >= b.Want) loaded = want;
        int k = i * 4;
        if (_cargoText[i] != null && _cargoKey[k] == (int)b.Good && _cargoKey[k + 1] == loaded && _cargoKey[k + 2] == want && _cargoKey[k + 3] == b.Id) return _cargoText[i];
        _cargoKey[k] = (int)b.Good; _cargoKey[k + 1] = loaded; _cargoKey[k + 2] = want; _cargoKey[k + 3] = b.Id;
        _cargoText[i] = b.Good + " " + loaded + " of " + want + (want == 1 ? " load" : " loads");
        return _cargoText[i];
    }
}
