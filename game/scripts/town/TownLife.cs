using System;
using System.Collections.Generic;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// Night 3, round 3 (asset track LF): life in the town, as decoration only. Hens pecking by a cottage's coop, a cat
/// asleep on a doorstep, a dog lying by the market, ducks drifting on the river, a heron in the shallows, birds gliding
/// in slow circles high over the map and a small flock that lifts from a field now and then; and each cottage's garden
/// (<c>TownLife.Gardens.cs</c>). Everything is placed from the plan and the seed (<see cref="TownSpace"/> keeps it off
/// lanes, roads, plots, crossings and queue spots), never read back by the simulation and never saved. Built once per
/// town; per frame only a few dozen transforms are set (no allocation).
/// </summary>
public partial class TownLife : Node3D
{
    private TownDriver _drv = null!;
    // Drawn larger than life, like the villagers (1.5x) and more for the smallest, so each reads at the normal view.
    private static float ScaleOf(int kind) => kind switch { Hen => 2.2f, Duck => 3.0f, Cat => 2.6f, Dog => 2.4f, Heron => 2.1f, Rocks => 1.0f, Gull => 2.5f, _ => 3.2f };
    private const float C = TownGround.Cell;

    // Animals: a node, its moving part (head, neck, wings), the plot it waits for (-1: always), its home and a phase.
    private struct Beast
    {
        public Node3D Node; public Node3D? Part, Part2; public int Kind; public int Plot; public Vector3 Home; public float Phase, Yaw;
    }
    private const int Hen = 0, Duck = 1, Heron = 2, Cat = 3, Dog = 4, Glider = 5, Flock = 6, Rocks = 7, Gull = 8;
    private readonly List<Beast> _beasts = new();
    private float _time;
    private float _flockAt = -1f;
    /// <summary>For the audit: hold the flock at this many seconds into its lift (negative: run free).</summary>
    public void HoldFlock(float seconds) => _flockAt = seconds;
    private Vector3 _sky;          // centre of the gliders' circles
    private TideToll.Sim.MapData? _map;

    /// <summary>For tests and the audit: animals and birds built for this town.</summary>
    public int Animals => _beasts.Count;
    public int GardensBuilt => _gardens.Count;
    /// <summary>For the audit: where the first shown garden stands (or null).</summary>
    public Vector3? ShownGarden(int skip = 0)
    {
        foreach (var g in _gardens) if (g != null && skip-- <= 0) return g.Position;
        return null;
    }
    /// <summary>For the audit: where the garden of a kind stands once shown (7: the market's bunting).</summary>
    public Vector3? ShownGardenOfKind(int kind)
    {
        for (int i = 0; i < _gardens.Count; i++) if (_gardens[i] != null && _gardenPlans[i].Kind == kind) return _gardens[i]!.Position;
        return null;
    }
    /// <summary>For the audit: the home of the first animal of a kind (0 hen, 1 duck, 2 heron, 3 cat, 4 dog, 6 flock, 7 rocks, 8 gull).</summary>
    public Vector3? Home(int kind)
    {
        foreach (var b in _beasts) if (b.Kind == kind) return b.Home;
        return null;
    }

    public void Init(TownDriver drv)
    {
        _drv = drv;
        Name = "Life";
        drv.Replaced += Build;
        Build();
    }

    public override void _ExitTree() { if (_drv != null) _drv.Replaced -= Build; }

    private static uint Hash(uint a, uint b)
    {
        uint h = a * 2654435761u ^ (b + 0x9E3779B9u + (a << 6) + (a >> 2));
        h ^= h >> 15; h *= 2246822519u; h ^= h >> 13; h *= 3266489917u; h ^= h >> 16;
        return h;
    }
    private static float Unit(uint h) => (h >> 8) / 16777216f;

    private void Build()
    {
        foreach (var b in _beasts) { RemoveChild(b.Node); b.Node.QueueFree(); }
        _beasts.Clear();
        ClearGardens();
        var plan = _drv.Town.Plan;
        uint seed = (uint)plan.Seed;
        PlanGardens(plan, seed);
        var map = plan.Map;
        int n = TownGround.N;
        _sky = new Vector3((plan.WindowX + plan.WindowW * 0.5f) * C, 15f, (plan.WindowY + plan.WindowH * 0.5f) * C);

        // Hens by the coop's cottage (the coop is that cottage's garden), a cat on another cottage's doorstep.
        int coop = CoopGarden();
        if (coop >= 0)
        {
            var g = _gardenPlans[coop];
            for (int i = 0; i < 3; i++)
            {
                float a = i * 2.1f + Unit(Hash(seed, 50u + (uint)i)) * 0.8f;
                var at = g.At + new Vector3(MathF.Cos(a) * 1.9f, 0, MathF.Sin(a) * 1.9f);
                Add(Hen, "animal-hen", "Head", g.Plot, g.River, at, a * 1.7f, i * 1.3f);
            }
        }
        int cats = 0;
        for (int i = 0; i < plan.Plots.Length && cats < 1; i++)
        {
            var p = plan.Plots[i];
            if (p.Kind != PlotKind.Cottage || Hash(seed, 70u + (uint)i) % 3 != 0 || (coop >= 0 && _gardenPlans[coop].Plot == i)) continue;
            // On the step: between the plot's middle and its door, just inside the plot's edge.
            float cx = p.X + p.W * 0.5f, cy = p.Y + p.H * 0.5f, dx = p.DoorX + 0.5f - cx, dy = p.DoorY + 0.5f - cy;
            float len = MathF.Max(0.01f, MathF.Sqrt(dx * dx + dy * dy));
            float reach = MathF.Max(p.W, p.H) * 0.5f + 0.05f;
            var at = new Vector3((cx + dx / len * reach) * C, 0.02f, (cy + dy / len * reach) * C);
            Add(Cat, "animal-cat", null, i, false, at, MathF.Atan2(dx, dy) + 0.6f, 0);
            cats++;
        }
        // A dog lying at the market stall's back, just off the plot's edge (away from the door and its queue).
        for (int i = 0; i < plan.Plots.Length; i++)
        {
            var p = plan.Plots[i];
            if (p.Kind != PlotKind.Market) continue;
            float cx = p.X + p.W * 0.5f, cy = p.Y + p.H * 0.5f, dx = p.DoorX + 0.5f - cx, dy = p.DoorY + 0.5f - cy;
            float len = MathF.Max(0.01f, MathF.Sqrt(dx * dx + dy * dy));
            float reach = MathF.Max(p.W, p.H) * 0.5f + 0.1f, side = Hash(seed, 90u) % 2 == 0 ? 0.35f : -0.35f;
            var spot = new Vector3((cx - dx / len * reach - dy / len * side * p.W) * C, 0, (cy - dy / len * reach + dx / len * side * p.H) * C);
            Add(Dog, "animal-dog", "Head", i, false, spot, MathF.Atan2(-dx, -dy) + 0.5f, 0);
            break;
        }
        // Ducks and the heron on a quiet stretch of the river, away from the crossings and the landing.
        var rp = TownSpace.River(plan);
        _map = map;
        int duckRow = QuietRow(rp, n, Hash(seed, 110u)), heronRow = QuietRow(rp, n, Hash(seed, 111u) ^ 0x5555u);
        if (duckRow >= 0)
            for (int i = 0; i < 2; i++)   // a loose pair, not a line
            {
                float y = duckRow + 0.3f + i * 0.9f;
                var at = new Vector3((map.RiverX[duckRow] + (i == 0 ? -0.4f : 0.5f)) * C, 0.02f, y * C);
                Add(Duck, "animal-duck", null, -1, false, at, 0, i * 1.7f);
            }
        if (heronRow >= 0)
        {
            float side = Hash(seed, 112u) % 2 == 0 ? -1f : 1f;
            var at = new Vector3((map.RiverX[heronRow] + side * ((float)map.RiverHalfW - 1.0f)) * C, 0.0f, (heronRow + 0.5f) * C);
            Add(Heron, "animal-heron", "Neck", -1, false, at, side > 0 ? -1.2f : 1.9f, 0);
        }
        // Rock outcrops on the river bank on quiet stretches, and a gull standing on the first.
        for (int k = 0; k < 4; k++)
        {
            if (!RockSpot(plan, k, out float cx, out int row, out float side)) continue;
            var at = new Vector3(cx * C, 0, (row + 0.5f) * C);
            float ry = Unit(Hash(seed, 190u + (uint)k)) * 6.28f;
            Add(Rocks, "dress-rocks", null, -1, false, at, ry, 0);
            if (k == 0) Add(Gull, "dress-gull", null, -1, false, at + new Basis(Vector3.Up, ry) * new Vector3(-0.5f, 0.88f, 0.1f), side > 0 ? -1.5f : 1.5f, 0);
        }
        // Gliders high over the town, and a flock that rests on a field and lifts now and then.
        for (int i = 0; i < 5; i++)
            Add(Glider, "animal-bird", null, -1, false, _sky, 0, i * 1.256f + Unit(Hash(seed, 130u + (uint)i)) * 0.4f);
        for (int i = 0; i < plan.Plots.Length; i++)
        {
            var p = plan.Plots[i];
            if (p.Kind != PlotKind.Field) continue;
            var home = new Vector3((p.X + p.W * 0.5f) * C, 0.1f, (p.Y + p.H * 0.5f) * C);
            for (int k = 0; k < 9; k++)
            {
                uint h = Hash(seed, 150u + (uint)k);
                var at = home + new Vector3((Unit(h) - 0.5f) * p.W * C * 0.6f, 0, (Unit(h * 7u + 3u) - 0.5f) * p.H * C * 0.6f);
                Add(Flock, "animal-bird", null, i, false, at, Unit(h) * 6.28f, Unit(h * 13u + 1u));
            }
            break;
        }
    }

    private void Add(int kind, string model, string? part, int plot, bool river, Vector3 at, float yaw, float phase)
    {
        if (!ModelLib.Has(model)) return;
        var node = new Node3D { Name = model + _beasts.Count, Position = at, Rotation = new Vector3(0, yaw, 0), Scale = Vector3.One * ScaleOf(kind) };
        Node3D? moving = null, second = null;
        if (kind is Glider or Flock)
        {
            node.AddChild(ModelLib.PartInstance(model, "Body", "Body"));
            moving = ModelLib.PartInstance(model, "WingL", "WingL"); node.AddChild(moving);
            second = ModelLib.PartInstance(model, "WingR", "WingR"); node.AddChild(second);
        }
        else if (part != null && ModelLib.HasPart(model, part))
        {
            node.AddChild(ModelLib.PartInstance(model, "Body", "Body"));
            moving = ModelLib.PartInstance(model, part, part); node.AddChild(moving);
        }
        else node.AddChild(ModelLib.Instance(model, "Model"));
        // Birds cast no shadow: from above a bird's shadow on the grass reads as a dark speck of dirt.
        if (kind is Glider or Flock)
            foreach (var c in node.GetChildren()) if (c is GeometryInstance3D gi) gi.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        node.Visible = plot < 0 || river;   // waits for its plot: shown when the plot is given (Process)
        AddChild(node);
        _beasts.Add(new Beast { Node = node, Part = moving, Part2 = second, Kind = kind, Plot = river ? -2 - plot : plot, Home = at, Phase = phase, Yaw = yaw });
    }

    /// <summary>A free cell in the ring of the rectangle, picked from the hash; its centre in metres.</summary>
    private static bool FreeNear(TownPlan plan, int x0, int y0, int x1, int y1, uint h, out Vector3 at)
    {
        int w = x1 - x0, hgt = y1 - y0, ring = 2 * (w + hgt), start = (int)(h % (uint)Math.Max(1, ring));
        for (int k = 0; k < ring; k++)
        {
            int s = (start + k) % ring, x, y;
            if (s < w) { x = x0 + s; y = y0; }
            else if (s < w + hgt) { x = x1 - 1; y = y0 + s - w; }
            else if (s < 2 * w + hgt) { x = x1 - 1 - (s - w - hgt); y = y1 - 1; }
            else { x = x0; y = y1 - 1 - (s - 2 * w - hgt); }
            if (TownSpace.Free(plan, x, y)) { at = new Vector3((x + 0.5f) * C, 0, (y + 0.5f) * C); return true; }
        }
        at = default;
        return false;
    }

    /// <summary>The k-th rock outcrop on the bank (0..3), from the plan and seed; also keeps trees off it
    /// (<see cref="TownTrees"/> asks before planting).</summary>
    public static bool RockSpot(TownPlan plan, int k, out float cx, out int row, out float side)
    {
        uint seed = (uint)plan.Seed;
        var map = plan.Map;
        cx = 0; side = 1f;
        row = QuietRow(TownSpace.River(plan), TownGround.N, Hash(seed, 170u + (uint)k));
        if (row < 0) return false;
        side = Hash(seed, 180u + (uint)k) % 2 == 0 ? -1f : 1f;
        cx = map.RiverX[row] + side * ((float)map.RiverHalfW + 1.3f);
        if (!TownSpace.Free(plan, (int)cx, row)) return false;
        TownSpace.KeepClear(plan, (int)cx, row, 4);   // 4 cells: no tree crown reaches the outcrop
        return true;
    }

    /// <summary>A river row at least 10 cells from every crossing and from the landing (-1 if none).</summary>
    private static int QuietRow(RiverPlan rp, int n, uint h)
    {
        int start = 8 + (int)(h % (uint)(n - 16));
        for (int k = 0; k < n - 16; k++)
        {
            int y = 8 + (start - 8 + k) % (n - 16);
            if (Math.Abs(y - rp.LandingRow) < 12 || Math.Abs(y - rp.FordRow) < 10 || Math.Abs(y - rp.FerryRow) < 10
                || Math.Abs(y - rp.FootRow) < 10 || Math.Abs(y - rp.DrawRow) < 10) continue;
            return y;
        }
        return -1;
    }

    private bool Shown(in Beast b, Town t)
    {
        if (b.Plot == -1) return true;
        if (b.Plot >= 0) return t.PlotGiven(b.Plot);
        return t.InRiverAge && t.RiverPlotGiven(-2 - b.Plot);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _time += dt;
        var t = _drv.Town;
        ProcessGardens(t, dt);
        for (int i = 0; i < _beasts.Count; i++)
        {
            var b = _beasts[i];
            bool show = Shown(b, t);
            if (b.Node.Visible != show) b.Node.Visible = show;
            if (!show) continue;
            float tt = _time + b.Phase * 3f;
            switch (b.Kind)
            {
                case Hen:      // peck: quick dips with pauses; a small step now and then
                {
                    float peck = MathF.Max(0f, MathF.Sin(tt * 5.5f)) * (MathF.Sin(tt * 0.9f) > 0.2f ? 1f : 0f);
                    if (b.Part != null) b.Part.Rotation = new Vector3(0.9f * peck, 0, 0);
                    b.Node.Rotation = new Vector3(0, b.Yaw + 0.6f * MathF.Sin(tt * 0.31f), 0);
                    break;
                }
                case Duck:     // drift slowly along the river and back, bobbing
                {
                    float s = MathF.Sin(tt * 0.06f);
                    float z = b.Home.Z + s * 4f * C;
                    int row = Math.Clamp((int)(z / C), 0, TownGround.N - 1);
                    float x = _map != null ? _map.RiverX[row] * C + (b.Phase - 0.9f) * 0.8f : b.Home.X;
                    b.Node.Position = new Vector3(x + MathF.Sin(tt * 0.21f) * 0.6f, b.Home.Y + 0.03f * MathF.Sin(tt * 2.1f), z);
                    float dir = MathF.Cos(tt * 0.06f) >= 0 ? 0f : MathF.PI;
                    b.Node.Rotation = new Vector3(0.04f * MathF.Sin(tt * 1.7f), dir + 0.25f * MathF.Cos(tt * 0.21f), 0);
                    break;
                }
                case Heron:    // stands still; now and then the neck stretches down to the water
                    if (b.Part != null) b.Part.Rotation = new Vector3(0.7f * MathF.Max(0f, MathF.Sin(tt * 0.35f) - 0.6f) * 2.5f, 0, 0);
                    break;
                case Gull:     // turns its head now and then (the whole bird: it is small)
                    b.Node.Rotation = new Vector3(0, b.Yaw + (MathF.Sin(tt * 0.5f) > 0.3f ? 0.8f : 0f), 0);
                    break;
                case Cat:      // asleep: breathes
                {
                    float cs = ScaleOf(Cat);
                    b.Node.Scale = new Vector3(cs, cs * (1f + 0.04f * MathF.Sin(tt * 1.6f)), cs);
                    break;
                }
                case Dog:      // looks round
                    if (b.Part != null) b.Part.Rotation = new Vector3(0, 0.7f * MathF.Sin(tt * 0.4f) * MathF.Abs(MathF.Sin(tt * 0.13f)), 0);
                    break;
                case Glider:   // slow circles high up, wings mostly still
                {
                    float a = tt * 0.09f + b.Phase;
                    float r = 26f + 6f * MathF.Sin(b.Phase * 2.3f);
                    b.Node.Position = _sky + new Vector3(MathF.Cos(a) * r, b.Phase * 0.8f, MathF.Sin(a) * r);
                    b.Node.Rotation = new Vector3(0, -a, 0.25f);
                    Flap(b, 0.25f * MathF.Max(0f, MathF.Sin(tt * 0.5f) - 0.7f) * MathF.Sin(tt * 9f) + 0.08f);
                    break;
                }
                case Flock:    // rest and peck on the field; every 40 s lift, wheel round and come back down
                {
                    float cyc = (_flockAt >= 0f ? _flockAt : (_time % 40f) - 30f) + b.Phase * 0.8f;   // lift lasts about 9 s
                    if (cyc <= 0f || cyc >= 9f)
                    {
                        b.Node.Position = b.Home;
                        b.Node.Rotation = new Vector3(0, b.Yaw, 0);
                        Flap(b, -0.9f);   // wings folded down
                    }
                    else
                    {
                        float u = cyc / 9f, a = u * MathF.Tau;
                        float up = MathF.Sin(u * MathF.PI) * 9f;
                        b.Node.Position = b.Home + new Vector3(MathF.Sin(a) * 10f, up, (1f - MathF.Cos(a)) * 6f);
                        b.Node.Rotation = new Vector3(0, a + MathF.PI / 2f, -0.3f);
                        Flap(b, 0.5f * MathF.Sin(_time * 14f + b.Phase * 5f));
                    }
                    break;
                }
            }
        }
    }

    private static void Flap(in Beast b, float angle)
    {
        if (b.Part == null) return;
        b.Part.Rotation = new Vector3(0, 0, -angle);
        if (b.Part2 != null) b.Part2.Rotation = new Vector3(0, 0, angle);
    }
}
