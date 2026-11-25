using System;

namespace TideToll.Sim;

// Rain and mud (the toy's world.ts "rain and mud", round 4): the weather, the mud layer, its cost and speed. Runs only
// with SimParams.RainOn and never with the river rules (which have their own flood mud).
public sealed partial class World
{
    /// <summary>Rain is on for this run (SimParams.RainOn, and the river rules off). Fixed for the run.</summary>
    private readonly bool _rainOn;
    private readonly RainWeather _weather;
    /// <summary>1 on cells that are mud now: worn earth (wear 25 or more) rained on, until it dries.</summary>
    private readonly byte[] _mud;
    private int _mudCells;
    private readonly double[] _mudCostMul = new double[5];

    /// <summary>Whether this run has rain and mud (SimParams.RainOn with the river rules off).</summary>
    public bool RainOn => _rainOn;
    /// <summary>Rain and ground wetness (idle when rain is off).</summary>
    public RainWeather Weather => _weather;
    /// <summary>1 on cells that are mud now. Never gravel, a bridge, the ford or a fence (a gate may be mud).</summary>
    public ReadOnlySpan<byte> Mud => _mud;
    /// <summary>Cells that are mud now.</summary>
    public int MudCells => _mudCells;

    /// <summary>
    /// Mud follows the rain: while it rains every unpaved worn cell (wear 25 or more, land, not gravel, not a bridge, not
    /// a fence unless a gate) is mud; the mud stays on cells still worn to trail until the ground is dry. Runs once a sim
    /// second, when a shower starts, and before the costs are rebuilt after every edit.
    /// </summary>
    private void UpdateMud()
    {
        var wx = _weather;
        byte[] mud = _mud;
        if (wx.Wetness <= 0 && !wx.Raining)
        {
            if (_mudCells != 0) { Array.Clear(mud); _mudCells = 0; }
            return;
        }
        double thr = _p.ThresholdTrail;
        float[] wear = _wear;
        bool raining = wx.Raining;
        int n = 0;
        for (int c = 0; c < _nc; c++)
        {
            bool worn = wear[c] >= thr && _land[c] == 1 && _gravel[c] == 0 && _bridgeAt[c] < 0 && (_fence[c] == 0 || _gate[c] == 1);
            mud[c] = (byte)(worn && (raining || mud[c] == 1) ? 1 : 0);
            n += mud[c];
        }
        _mudCells = n;
    }

    /// <summary>
    /// Mud on cell c at wetness <paramref name="wet"/>, applied to _gc0 and _gc1: the ordinary walkers' muddy cost is
    /// MudCostMul (x1.6 trail, x1.7 track, x1.8 hollow way) on their own worn-ground cost, blended with the dry cost by
    /// wetness; frugal walkers see FrugalDisc of that, 1 + (muddy - 1) x FrugalDisc.
    /// </summary>
    private void MudCosts(int c, double wet)
    {
        int s = (int)_p.GroundOf(_wear[c], _hollow[c] == 1);
        double dry = _gc0;
        double muddy = (_p.MudReplace * _surfaceCost[s] + (1 - _p.MudReplace) * dry) * _mudCostMul[s];
        _gc0 = dry + (muddy - dry) * wet;
        _gc1 = 1 + (_gc0 - 1) * _p.FrugalDisc;
    }

    /// <summary>Speed factor of mud on cell c at the current wetness (1 off mud).</summary>
    private double MudSpeed(int c)
    {
        if (_mud[c] == 0) return 1;
        double wet = _weather.Wetness;
        return wet > 0 ? 1 + (_p.MudSpeedMul - 1) * wet : 1;
    }

    /// <summary>Rain now for <paramref name="simSec"/> seconds (default a day; debug, tutorials, tests); mud lasts as after a rain day of this season. Does nothing with rain off.</summary>
    public void StartRain(double? simSec = null, double strength = 1)
    {
        if (!_rainOn) return;
        _weather.StartRain(_time, simSec ?? _p.DaySimSec, strength);
        UpdateMud();
    }

    /// <summary>Stops the rain now; with <paramref name="dry"/> the ground dries at once too. Does nothing with rain off.</summary>
    public void StopRain(bool dry = false)
    {
        if (!_rainOn) return;
        _weather.StopRain(_time, dry);
        UpdateMud();
    }
}
