using System;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>The rewards themselves: which event starts which pieces, where (from the town's plan), and its sound.</summary>
public partial class TownFeel
{
    public enum Reward { Coin, Flourish, Extend, Pave, TrailDust, Ripple, FordRing, Bell, Gift, RequestMet, Count }

    private static readonly Color Dust = Palette.Limewash;                                    // a cottage extended
    private static readonly Color TrailPuff = Palette.TrailEarth.Lerp(Palette.Limewash, 0.2f);   // Trail earth, light
    private static readonly Color Spark = Palette.Thatch.Lerp(Palette.Paper, 0.15f);          // Thatch, light tone
    private static readonly Color WaterRing = Palette.Paper;
    private static readonly Color WaterRing2 = Palette.Limewash.Lerp(Palette.River, 0.25f);
    private static readonly Color GroundRing = Palette.Limewash;
    private static readonly Color CoinColour = Palette.Thatch.Lerp(Palette.Paper, 0.05f);      // Thatch gold, light
    private static readonly Color GritA = Palette.PackedEarth.Lerp(Palette.TrailEarth, 0.5f), GritB = Palette.TrailEarth;

    private double _now, _dustBeat, _fordBeat, _lastQuay, _lastCoinAt;
    private BoatPhase[] _phase = Array.Empty<BoatPhase>();
    private int[] _fordCell = Array.Empty<int>();
    private int _bellPlot = -2, _market = -1, _mill = -1;
    private float _pealLeft, _pealBeat;

    private void Spawn(float dt)
    {
        _now += dt;
        var t = _drv.Town;
        if (_bellPlot == -2) FindPlots(t);
        var events = _drv.Events;
        for (int i = 0; i < events.Count; i++) On(t, events[i]);
        // The peal of the new age: a ring on the green with each of the four long bells (0.7 s apart).
        if (_pealLeft > 0)
        {
            _pealLeft -= dt; _pealBeat -= dt;
            if (_pealBeat <= 0) { _pealBeat = 0.7f; BellRings(); }
        }
        TrailDust(t, dt);
        if (t.InRiverAge) { Boats(t); Ford(t, dt); }
    }

    /// <summary>One event's reward (the strips feed events shaped as the simulation raises them).</summary>
    public void On(Town t, in TownEvent e)
    {
        if (_bellPlot == -2) FindPlots(t);
        {
            switch (e.Kind)
            {
                case TownEventKind.Delivery:
                    // n5 fix round: the market's coin hops from its table (where the load was set down), not from the
                    // lane, and smaller (it read as a pale disc floating by the lane)
                    if (e.Plot >= 0 && e.Plot == _market && _main.Work != null && _main.Buildings.PlotDrawn(e.Plot))
                    {
                        var at = _main.Work.TableEdge(e.Plot);
                        CoinHop(at.X / TownGround.Cell, at.Z / TownGround.Cell, 1.05f, at.Y);
                    }
                    else CoinHop(e.X, e.Y);
                    break;
                case TownEventKind.VillagerArrived: Flourish(DoorOf(t, e.Plot, e.X, e.Y)); break;
                case TownEventKind.BuildingOpened: if (e.Plot >= 0 && e.Plot < t.Plan.Plots.Length) Flourish(DoorOf(t, e.Plot, 0, 0)); break;
                case TownEventKind.Purchase: Purchase(t, in e); break;
                case TownEventKind.CottageExtended: ExtendDust(t, e.Plot, DoorOf(t, e.Plot, e.X, e.Y)); break;
                case TownEventKind.RoadPaved: Grit(t, e.Index); break;
                case TownEventKind.BellRung: BellRings(); break;
                case TownEventKind.NextAge: _pealLeft = 2.8f; _pealBeat = 0; break;
                case TownEventKind.LevelReached: Gift(t, e.Level); break;
                case TownEventKind.RequestMet:
                {
                    // At the asker's own building (an open one: the plan's first plot of its kind may be unbuilt).
                    int asker = OpenPlot(t, e.Index == 1 ? PlotKind.Mill : PlotKind.Market);
                    if (asker < 0) asker = e.Index == 1 && _mill >= 0 ? _mill : _market;
                    if (asker >= 0) Flourish(DoorOf(t, asker, 0, 0), Reward.RequestMet);
                    break;
                }
                case TownEventKind.LandingDelivery:
                    if (_now - _lastQuay >= 0.3) { _lastQuay = _now; Ripple(Berth(t, e.X, e.Y), 2); }
                    break;
            }
        }
    }

    private void FindPlots(Town t)
    {
        _bellPlot = -1; _market = -1; _mill = -1;
        foreach (var p in t.Plan.Plots)
        {
            if (p.Kind == PlotKind.BellGreen) _bellPlot = p.Id;
            else if (p.Kind == PlotKind.Market && _market < 0) _market = p.Id;
            else if (p.Kind == PlotKind.Mill && _mill < 0) _mill = p.Id;
        }
    }

    /// <summary>A plot's door cell centre in map metres; the given cells when the plot is not a first-age plot.</summary>
    private static Vector3 DoorOf(Town t, int plot, double x, double y)
    {
        if (plot >= 0 && plot < t.Plan.Plots.Length)
        {
            var p = t.Plan.Plots[plot];
            return At(p.DoorX + 0.5, p.DoorY + 0.5);
        }
        return At(x, y);
    }

    /// <summary>The first plot of a kind whose building stands and works (-1 none).</summary>
    private static int OpenPlot(Town t, PlotKind k)
    {
        for (int w = 0; w < t.WorkplaceCount; w++) { var wp = t.Workplace(w); if (wp.Open && wp.Kind == k) return wp.Plot; }
        foreach (var p in t.Plan.Plots) if (p.Kind == k && t.PlotWorking(p.Id)) return p.Id;   // the market is no workplace
        return -1;
    }

    /// <summary>The water beside the landing: the berth nearest the given cell (map metres).</summary>
    private static Vector3 Berth(Town t, double x, double y)
    {
        var rp = t.RiverPlan;
        if (rp == null) return At(x, y);
        int best = 0;
        double bd = double.MaxValue;
        for (int k = 0; k < rp.BerthX.Length; k++)
        {
            double d = (rp.BerthX[k] - x) * (rp.BerthX[k] - x) + (rp.BerthY[k] - y) * (rp.BerthY[k] - y);
            if (d < bd) { bd = d; best = k; }
        }
        return At(rp.BerthX[best], rp.BerthY[best]);
    }

    private static Vector3 CentreOf(Plot p) => At(p.X + p.W / 2.0, p.Y + p.H / 2.0);

    /// <summary>A coin hops out at the building and falls back (its sound as it lands). At most one every 0.2 s.</summary>
    private void CoinHop(double x, double y, float size = 1.6f, float h = 0.5f)
    {
        if (_now - _lastCoinAt < 0.2 || !Room(1, true)) return;
        _lastCoinAt = _now;
        Started[(int)Reward.Coin]++;
        Add(Kind.Coin, At(x, y, h), new Vector3(R(-0.3f, 0.3f), R(5.4f, 5.8f), R(-0.3f, 0.3f)), 0.95f, size, CoinColour, gravity: 12f);
    }

    /// <summary>A small flourish: a thin ring on the ground and five sparks rising a little (a new villager, a hand,
    /// a building, the louder bell, a request met).</summary>
    private void Flourish(Vector3 at, Reward r = Reward.Flourish)
    {
        if (!Room(6, false)) return;
        Started[(int)r]++;
        Add(Kind.Band, at + new Vector3(0, 0.08f, 0), Vector3.Zero, 0.7f, 0.8f, Spark, grow: 1.2f);
        for (int i = 0; i < 5; i++)
        {
            float a = i * 1.2566f + R(-0.2f, 0.2f);
            Add(Kind.Puff, at + new Vector3(MathF.Cos(a) * 0.7f, 0.4f, MathF.Sin(a) * 0.7f), new Vector3(MathF.Cos(a) * 0.6f, R(1.2f, 1.8f), MathF.Sin(a) * 0.6f),
                0.6f, 0.9f, Spark, delay: i * 0.04f);
        }
        if (r == Reward.Flourish) Sound("gift-ring");
    }

    private void Purchase(Town t, in TownEvent e)
    {
        switch (e.Item)
        {
            case ShopItemKind.Hand:
                if (e.Index >= 0 && e.Index < t.WorkplaceCount) Flourish(DoorOf(t, t.Workplace(e.Index).Plot, 0, 0));
                break;
            case ShopItemKind.Tools:
                for (int w = 0; w < t.WorkplaceCount; w++) { var wp = t.Workplace(w); if (wp.Open) Flourish(DoorOf(t, wp.Plot, 0, 0)); }
                break;
            case ShopItemKind.BellTrack:
                if (_bellPlot >= 0) Flourish(CentreOf(t.Plan.Plots[_bellPlot]));
                break;
        }
    }

    /// <summary>A cottage extended: a puff of dust at the door (the cue itself has the saw and the two hammer taps).</summary>
    private void ExtendDust(Town t, int plot, Vector3 door)
    {
        if (!Room(8, false)) return;
        Started[(int)Reward.Extend]++;
        // The cloud sits against the cottage's front and spreads along it and away from the path, not over it.
        var toHouse = plot >= 0 && plot < t.Plan.Plots.Length ? CentreOf(t.Plan.Plots[plot]) - door : Vector3.Zero;
        toHouse.Y = 0;
        var inward = toHouse.LengthSquared() > 0.01f ? toHouse.Normalized() : Vector3.Zero;
        var at = door + inward * 0.9f;
        float face = MathF.Atan2(inward.Z, inward.X);
        for (int i = 0; i < 8; i++)
        {
            // A half fan facing the house (angles within 100 degrees of the way in).
            float a = (inward == Vector3.Zero ? i * 0.785f : face + (i - 3.5f) * 0.5f) + R(-0.15f, 0.15f);
            Add(Kind.Puff, at + new Vector3(MathF.Cos(a) * 0.4f, 0.3f, MathF.Sin(a) * 0.4f), new Vector3(MathF.Cos(a) * 1.3f, R(0.3f, 0.7f), MathF.Sin(a) * 1.3f),
                R(0.45f, 0.6f), R(1.0f, 1.3f), Dust, delay: i % 2 == 0 ? 0 : 0.15f);
        }
        Sound("dust-puff");
    }

    /// <summary>A road paved: grit scatters along the road from one end to the other as the surface changes.</summary>
    private void Grit(Town t, int road)
    {
        if (road < 0 || road >= t.Plan.Roads.Length) return;
        var rd = t.Plan.Roads[road];
        int pts = rd.Px.Length;
        if (pts < 2 || !Room(GritStones, true)) return;
        Started[(int)Reward.Pave]++;
        double len = rd.Length;
        for (int i = 0; i < GritStones; i++)
        {
            // A wave along the drawn curve (the road's own At; its points are drawn at +0.5) from one end to the
            // other: each stone lives 0.26 s and the next starts 0.065 s later, so at most four are up at once.
            rd.At(len * (i + 0.5) / GritStones, out double x, out double y, out _);
            var at = At(x + 0.5, y + 0.5, 0.15f) + new Vector3(R(-0.35f, 0.35f), 0, R(-0.35f, 0.35f));
            Add(Kind.Grit, at, new Vector3(R(-0.5f, 0.5f), R(1.2f, 1.6f), R(-0.5f, 0.5f)), 0.26f, 0.25f, i % 2 == 0 ? GritA : GritB,
                delay: i * 0.065f, gravity: 12f);
        }
        Sound("grit");
    }

    private const int GritStones = 12;

    /// <summary>Dust at the feet of villagers walking a trail that is not worn in, fewer as it wears.</summary>
    private void TrailDust(Town t, float dt)
    {
        _dustBeat += dt;
        if (_dustBeat < 0.2) return;
        _dustBeat = 0;
        if (_drv.Paused && StripStep < 0) return;
        bool any = false;
        for (int i = 0; i < t.VillagerCount; i++)
        {
            var v = t.Villager(i);
            if (v.Road < 0 || v.Road >= t.RoadCount) continue;
            var r = t.RoadInfo(v.Road);
            if (r.Surface != Surface.Track || r.WornIn) continue;
            if (_rng.NextDouble() > 0.4 * (1 - r.Wear) || !Room(1, true)) continue;
            any = true;
            Started[(int)Reward.TrailDust]++;
            Add(Kind.Puff, At(v.X, v.Y, 0.15f) + new Vector3(R(-0.2f, 0.2f), 0, R(-0.2f, 0.2f)), new Vector3(0, R(0.3f, 0.5f), 0),
                0.6f, R(0.8f, 1.1f), TrailPuff);
        }
        if (any) Sound("dust-puff-trail");
    }

    /// <summary>Rings on the green round the bell when it is rung (and for the peal of the new age); the bell itself
    /// swings in TownBuildings.</summary>
    private void BellRings()
    {
        if (_bellPlot < 0 || !Room(3, false)) return;
        Started[(int)Reward.Bell]++;
        var at = CentreOf(_drv.Town.Plan.Plots[_bellPlot]) + new Vector3(0, 0.06f, 0);
        for (int i = 0; i < 3; i++) Add(Kind.Ring, at, Vector3.Zero, 0.9f, 1.0f, GroundRing, delay: i * 0.22f, grow: 2.5f);
        Sound("bell-creak");
    }

    /// <summary>A level's gift arriving: a short ring on the ground at each new plot.</summary>
    private void Gift(Town t, int level)
    {
        if (level < 1 || level > t.Plan.MaxLevel) return;
        var plots = t.Plan.Level(level).Plots;
        bool any = false;
        for (int i = 0; i < plots.Length && i < 6; i++)
        {
            if (plots[i] < 0 || plots[i] >= t.Plan.Plots.Length || !Room(1, false)) continue;
            any = true;
            // At its widest the ring is 20 percent smaller than the plot, inside the plot's own frame.
            var pl = t.Plan.Plots[plots[i]];
            float rmax = 0.8f * Math.Min(pl.W, pl.H) * TownGround.Cell / 2f;
            Add(Kind.Band, CentreOf(pl) + new Vector3(0, 0.08f, 0), Vector3.Zero, 0.9f, rmax * 0.4f, Spark, delay: i * 0.1f, grow: rmax * 0.6f);
        }
        if (!any) return;
        Started[(int)Reward.Gift]++;
        Sound("gift-ring");
    }

    private void Ripple(Vector3 at, int rings)
    {
        if (!Room(rings, false)) return;
        Started[(int)Reward.Ripple]++;
        // Two rings on the water round the berth, wider than a moored boat (it would hide a smaller one): at their
        // widest 7 m across, the band about 0.5 m wide.
        for (int i = 0; i < rings; i++) Add(Kind.Ring, at + new Vector3(0, 0.05f, 0), Vector3.Zero, 0.9f, 1.8f, i == 0 ? WaterRing : WaterRing2, delay: i * 0.25f, grow: 1.8f);
        Sound("ripple");
    }

    /// <summary>A ripple where a boat moors or casts off (its phase changes, as RiverAudio hears them).</summary>
    private void Boats(Town t)
    {
        int n = t.BoatCount;
        if (_phase.Length != n) { _phase = new BoatPhase[n]; for (int i = 0; i < n; i++) _phase[i] = t.Boat(i).Phase; }
        for (int i = 0; i < n; i++)
        {
            var b = t.Boat(i);
            if (b.Phase == _phase[i]) continue;
            if (b.Phase == BoatPhase.Moored || (b.Phase == BoatPhase.Leaving && _phase[i] == BoatPhase.Moored)) Ripple(At(b.X, b.Y), 2);
            _phase[i] = b.Phase;
        }
    }

    /// <summary>A ring at each stepping stone of the ford as someone wades over it (one per cell crossed).</summary>
    private void Ford(Town t, float dt)
    {
        _fordBeat += dt;
        if (_fordBeat < 0.1) return;
        _fordBeat = 0;
        var rp = t.RiverPlan;
        if (rp == null || !t.CrossingExists(RiverLinkKind.Ford)) return;
        int f = rp.LinkOf(RiverLinkKind.Ford);
        if (f < 0) return;
        var l = rp.Links[f];
        double x0 = Math.Min(l.Px[0], l.Px[^1]) + 1, x1 = Math.Max(l.Px[0], l.Px[^1]) - 1, row = l.Py[0];
        if (_fordCell.Length != t.VillagerCount) { _fordCell = new int[t.VillagerCount]; Array.Fill(_fordCell, int.MinValue); }
        for (int i = 0; i < t.VillagerCount; i++)
        {
            var v = t.Villager(i);
            if (v.Road >= 0 || Math.Abs(v.Y - row) >= 1 || v.X <= x0 || v.X >= x1) { _fordCell[i] = int.MinValue; continue; }
            int cell = (int)Math.Floor(v.X);
            if (cell == _fordCell[i]) continue;
            _fordCell[i] = cell;
            StoneRing(cell + 0.5, row);
        }
    }

    /// <summary>A ring round one stepping stone of the ford (map cells).</summary>
    public void StoneRing(double x, double row)
    {
        if (!Room(1, true)) return;
        Started[(int)Reward.FordRing]++;
        // Wider than the stepping stone (a smaller ring hides under it): 3.4 m across at its widest.
        Add(Kind.Band, At(x, row, 0.05f), Vector3.Zero, 0.7f, 0.8f, WaterRing, grow: 0.9f);
        Sound("ripple-ford");
    }
}
