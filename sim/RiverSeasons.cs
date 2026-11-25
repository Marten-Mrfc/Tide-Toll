using System;

namespace TideToll.Sim;

// Spring high water and winter ice (doc, Gameplay: "Spring high water", "Winter ice", "Seasons and weather"). Both run
// only with the river rules on, inside RiverStep, and draw from the river's own weather stream (separate from the boat
// schedule, so a flood or a freeze never shifts the boats' arrivals; the walkers' stream is never touched).
//   Flood: on the first day of spring, with FloodChance, the flood band (land within FloodBandCells of the water) is under
//   water for 1 or 2 days: the ford is closed, walkers wade the band slowly, bridges and the ferry still work. Then the
//   band is mud for the rest of spring (worn paths there cost more, so walkers braid round them).
//   Ice: from the second day of winter, with IceChance, until the thaw on the first day of spring: boats stop where they
//   are (patience and mooring clocks frozen, no new arrivals), boats buy nothing, and walkers cross the river anywhere.
//   Footsteps on the ice wear a separate ice-wear layer (double gain, no decay) that never feeds the wear grid and is
//   wiped at the thaw.
public sealed partial class World
{
    private bool _frozen, _flooded, _muddy;
    private byte[] _band = Array.Empty<byte>();
    private float[] _iceWear = Array.Empty<float>();
    private Mulberry32 _weatherRng;
    private long _floodEndTick = -1;

    internal bool FrozenValue => _frozen;
    internal bool FloodedValue => _flooded;
    internal bool MuddyValue => _muddy;
    internal ReadOnlySpan<byte> BandSpan => _band;
    internal ReadOnlySpan<float> IceWearSpan => _iceWear;
    internal double FloodEndsAt => _flooded ? _floodEndTick * _dt : -1;

    /// <summary>Set-up with the rest of the river: the flood band and the ice-wear layer.</summary>
    private void InitSeasons(int seed)
    {
        var r = _river!;
        _band = new byte[_nc];
        _iceWear = new float[_nc];
        _weatherRng = new Mulberry32(unchecked((uint)(seed * 7919 + 101)));
        var m = _map;
        for (int y = 0; y < _n; y++)
            for (int x = 0; x < _n; x++)
            {
                int c = y * _n + x;
                if (m.Water[c] == 0 && Math.Abs(x + 0.5 - m.RiverX[y]) <= m.RiverHalfW + r.FloodBandCells) _band[c] = 1;
            }
        SeasonBoundary(0, dayStart: true); // the first day of the first spring
    }

    /// <summary>
    /// The season's river events at the start of day <paramref name="day"/> (0 = the first day of the first spring):
    /// called when the world is made and at the end of the tick that closes each day, so the state read between ticks
    /// always belongs to the day the clock shows. Also ends a flood whose time is up.
    /// </summary>
    private void SeasonBoundary(long day, bool dayStart)
    {
        var r = _river!;
        if (_flooded && _ticks >= _floodEndTick) EndFlood();
        if (!dayStart) return;
        int daysPerSeason = (int)Math.Round(_p.SeasonSimSec / _p.DaySimSec);
        int season = (int)(day / daysPerSeason % 4), dayInSeason = (int)(day % daysPerSeason);
        if (season == 0 && dayInSeason == 0)
        {
            if (_frozen) Thaw();
            // Two draws every spring, used or not, so one spring's weather never shifts the next.
            double roll = _weatherRng.NextDouble(), len = _weatherRng.NextDouble();
            if (r.SpringFlood && roll < r.FloodChance)
                StartFlood(r.FloodMinDays + Math.Floor(len * (r.FloodMaxDays - r.FloodMinDays + 1)));
        }
        else if (season == 1 && dayInSeason == 0 && _muddy)
        {
            _muddy = false; // summer dries the band
            CostsChanged();
        }
        else if (season == 3 && dayInSeason == 1)
        {
            double roll = _weatherRng.NextDouble();
            if (r.WinterIce && roll < r.IceChance && !_frozen) Freeze();
        }
    }

    /// <summary>
    /// Costs, fields and walkers after the river changed under them, unless the world is still being made. Not an edit:
    /// World.Version (the player's edits) does not move; a flow preview's base is at most a second old anyway.
    /// </summary>
    private void CostsChanged()
    {
        if (_urgent != null) GroundChanged();
    }

    /// <summary>
    /// Puts the flood band under water now for <paramref name="days"/> days (the spring rule calls this; also for tests,
    /// tutorials and events). Closes the ford, charges repairs for buildings in the band. Draws nothing.
    /// </summary>
    public RiverResult StartFlood(double days)
    {
        if (_river == null) return Refuse("The river rules are off");
        var r = _river;
        bool was = _flooded;
        _flooded = true;
        _muddy = false;
        _floodEndTick = Math.Max(_floodEndTick, _ticks + (long)Math.Round(days * _p.TicksPerDay));
        if (!was)
        {
            Emit(RiverEventKind.FloodRising, -1, -1, days);
            Emit(RiverEventKind.FordClosed, -1, 0, 0);
            int hit = 0;
            foreach (var b in _map.Buildings)
                if (b.Kind != BuildingKind.Landing && InBand(b)) hit++;
            if (hit > 0)
            {
                double cost = hit * r.FloodRepairCost;
                _money -= cost;
                _rs!.Upkeep += cost;
                Emit(RiverEventKind.FloodRepairs, -1, -1, cost);
            }
            CostsChanged();
        }
        _lastRefusal = "";
        return RiverResult.Success(0);
    }

    private bool InBand(in Building b)
    {
        if (_band[b.Door] != 0) return true;
        for (int y = b.Y; y < b.Y + b.H; y++)
            for (int x = b.X; x < b.X + b.W; x++)
                if (_band[y * _n + x] != 0) return true;
        return false;
    }

    private void EndFlood()
    {
        _flooded = false;
        _floodEndTick = -1;
        _muddy = true; // for the rest of spring
        Emit(RiverEventKind.FloodReceded, -1, -1, 0);
        Emit(RiverEventKind.FordOpen, -1, 0, 0);
        CostsChanged();
    }

    /// <summary>Freezes the river now (the winter rule calls this; also for tests, tutorials and events). It thaws on the first day of spring, or with <see cref="Thaw"/>.</summary>
    public RiverResult Freeze()
    {
        if (_river == null) return Refuse("The river rules are off");
        if (_frozen) return Refuse("The river is already frozen");
        _frozen = true;
        // The ferry is frozen in: its riders step off on the far stage, its queues walk on the ice.
        foreach (var x in _crossings)
        {
            if (x.Type == CrossingType.Ferry) { ReleaseAll(x); x.Sailing = false; x.LoadT = -1; }
            // A drawbridge that is up or going up comes down; a boat already let through sails on after the thaw.
            if (x.Type == CrossingType.Drawbridge && (x.State == BridgeState.Raised || x.State == BridgeState.Raising)) StartLower(x);
        }
        Emit(RiverEventKind.RiverFrozen, -1, -1, 0);
        CostsChanged();
        _lastRefusal = "";
        return RiverResult.Success(0);
    }

    /// <summary>Ends the ice now: the ice-wear layer is wiped and walkers on the ice step off to the nearest bank.</summary>
    public RiverResult Thaw()
    {
        if (_river == null) return Refuse("The river rules are off");
        if (!_frozen) return Refuse("The river is not frozen");
        _frozen = false;
        Array.Clear(_iceWear);
        Emit(RiverEventKind.Thaw, -1, -1, 0);
        CostsChanged(); // evicts walkers standing on water that is no longer ice
        _lastRefusal = "";
        return RiverResult.Success(0);
    }

    // ---------------------------------------------------------------- costs, speed and wear (hooks of World)

    /// <summary>Whether a water cell is walkable ice now.</summary>
    private bool IceCell(int c) => _frozen && _map.Water[c] != 0 && _bridgeAt[c] < 0;

    /// <summary>Hook of IsBlocked with the river rules on: water that is neither ford, bridge nor ice; the ford in a flood; a raised span.</summary>
    private bool RiverBlocked(int c)
    {
        if (_map.Water[c] != 0 && _bridgeAt[c] < 0)
        {
            if (_frozen) return false;
            return _map.Ford[c] == 0 || _flooded;
        }
        return RiverSpanBlocked(c);
    }

    /// <summary>Routing cost of an ice cell before crowding: the ice-wear discount, like worn ground.</summary>
    private double IceCost(int c)
    {
        double f = _flowLut[Math.Min(_flowLut.Length - 1, (int)(_flow[c] * (double)FlowLutSteps))];
        double g = _p.TrailDiscount > 0
            ? _wearLut[Math.Min(_wearLut.Length - 1, (int)(_iceWear[c] * (double)WearLutSteps))]
            : _surfaceCost[(int)_p.GroundOf(_iceWear[c], false)];
        return Math.Max(_p.MinSurface, f * g) * (1 + _p.StaticNoise * _map.Noise[c]) * _river!.IceCostMul;
    }

    /// <summary>Multiplier on a land cell's cost from the flood band: wading while flooded, mud on worn paths after.</summary>
    private double BandCostMul(int c)
    {
        if (_band[c] == 0) return 1;
        var r = _river!;
        if (_flooded) return r.FloodWadeCostMul;
        if (!_muddy) return 1;
        return _p.GroundOf(_wear[c], _hollow[c] == 1) switch
        {
            Ground.Trail => r.MudTrailMul,
            Ground.Track => r.MudTrackMul,
            Ground.Hollow => r.MudHollowMul,
            _ => 1,
        };
    }

    /// <summary>Hook of Footstep while flooded or frozen: true when the step was handled here (no wear in flood water, ice wear on the ice).</summary>
    private bool RiverFootstep(int c, double x, double y, double dx, double dy, double d)
    {
        if (_flooded && _band[c] != 0) return true; // wading: water does not wear
        if (!IceCell(c)) return false;
        IceFootstep(c, x, y, dx, dy, d);
        return true;
    }

    /// <summary>Footsteps on the ice: the ice-wear layer only (double gain, side cells on the ice too), never the wear grid.</summary>
    private void IceFootstep(int c, double x, double y, double dx, double dy, double d)
    {
        double I = _p.WearPerStep * _river!.IceWearMul;
        _iceWear[c] = (float)(_iceWear[c] + I * (1 - _iceWear[c] / 100.0));
        if (d <= 1e-9) return;
        double px = (-dy / d) * 0.8, py = (dx / d) * 0.8;
        for (int s = 1; s >= -1; s -= 2)
        {
            int sx = (int)Math.Floor(x + px * s), sy = (int)Math.Floor(y + py * s);
            if (!_g.Inside(sx, sy)) continue;
            int si = sy * _n + sx;
            if (si == c || !IceCell(si)) continue;
            _iceWear[si] = (float)(_iceWear[si] + I * _p.SideShare * (1 - _iceWear[si] / 100.0));
        }
    }
}
