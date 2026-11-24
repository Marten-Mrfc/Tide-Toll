using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace TideToll.Sim.Testing;

// Shared by sim.tests and tools/TideToll.Tools (compiled into both): the helpers of the toy's
// test/sim.test.ts, ported one for one so the C# checks measure exactly what the toy's do.

/// <summary>Collects check lines the way the toy's test prints them ("  PASS  what").</summary>
public sealed class CheckLog
{
    public List<string> Lines { get; } = new();
    public List<(bool Ok, string What)> Checks { get; } = new();
    public void Log(string s) => Lines.Add(s);
    public void Check(bool ok, string what)
    {
        Lines.Add($"  {(ok ? "PASS" : "FAIL")}  {what}");
        Checks.Add((ok, what));
    }
    public bool AllPassed => Checks.TrueForAll(c => c.Ok);
    public override string ToString() => string.Join("\n", Lines);
}

public static class ToyHarness
{
    public static int TicksPerDay(World w) => w.Params.TicksPerDay;

    /// <summary>The toy's run(w, days): `for (i = 0; i &lt; days * TPD; i++) tick()` (so 0.1 days is 16 ticks, as in the toy).</summary>
    public static void Run(World w, double days)
    {
        double n = days * w.Params.TicksPerDay;
        for (int i = 0; i < n; i++) w.Tick();
    }

    public static string KindName(BuildingKind k) => k switch
    {
        BuildingKind.House => "house", BuildingKind.Well => "well", BuildingKind.Field => "field", _ => "market",
    };

    /// <summary>FNV-1a over the little-endian bytes of an array (as the golden exporter hashes typed arrays).</summary>
    public static uint Fnv<T>(ReadOnlySpan<T> a) where T : unmanaged
    {
        uint h = 0x811c9dc5;
        foreach (byte b in MemoryMarshal.AsBytes(a)) h = unchecked((h ^ b) * 16777619u);
        return h;
    }

    public static List<int> Line4(World w, int x0, int y0, int x1, int y1)
    {
        var cells = new List<int>();
        w.Grid.Line4(x0, y0, x1, y1, cells);
        return cells;
    }

    public static double DoorDistance(World w, int c)
    {
        int n = w.Grid.Width;
        double best = double.PositiveInfinity;
        foreach (var b in w.Buildings) best = Math.Min(best, JsMath.Hypot(b.Door % n - c % n, b.Door / n - c / n));
        return best;
    }

    /// <summary>The typical route from a house to the market now, and a fence across it about <paramref name="at"/> of the way along.</summary>
    public static int[] FenceAcrossRoute(World w, int house, double at, int half = 7)
    {
        int n = w.Grid.Width;
        int dest = w.OfKind(BuildingKind.Market)[0];
        var B = w.Buildings;
        float[] r = new FlowPredictor(w).TraceRoute(dest, B[house].Door);
        int np = r.Length / 2;
        // Walk along from `at` until a spot that is well away from the river and doors.
        for (int k = Math.Max(1, (int)Math.Floor(np * at)); k < np - 1; k++)
        {
            double x = r[2 * k], y = r[2 * k + 1];
            if (Math.Abs(x - w.RiverX[(int)Math.Floor(y)]) < w.Map.RiverHalfW + 6 || DoorDistance(w, (int)Math.Floor(y) * n + (int)Math.Floor(x)) < 8) continue;
            double dx = (double)r[2 * k + 2] - r[2 * k - 2], dy = (double)r[2 * k + 3] - r[2 * k - 1];
            double L = JsMath.Hypot(dx, dy);
            if (L == 0 || double.IsNaN(L)) L = 1;
            double px = -dy / L, py = dx / L;
            var cells = Line4(w, (int)JsMath.Round(x - px * half), (int)JsMath.Round(y - py * half), (int)JsMath.Round(x + px * half), (int)JsMath.Round(y + py * half));
            return cells.FindAll(c => w.Grid.Inside(c % n, c / n) && w.CanFence(c)).ToArray();
        }
        return Array.Empty<int>();
    }

    public static double DistToPts(double x, double y, ReadOnlySpan<float> pts)
    {
        double best = double.PositiveInfinity;
        for (int i = 0; i < pts.Length; i += 2)
        {
            double dx = x - pts[i], dy = y - pts[i + 1];
            best = Math.Min(best, dx * dx + dy * dy);
        }
        return Math.Sqrt(best);
    }

    /// <summary>The well's footprint ringed two cells out (the toy's fence ring).</summary>
    public static List<int> RingAround(World w, Building h)
    {
        var ring = new List<int>();
        var g = w.Grid;
        for (int x = h.X - 2; x <= h.X + h.W + 1; x++) { ring.Add(g.Idx(x, h.Y - 2)); ring.Add(g.Idx(x, h.Y + h.H + 1)); }
        for (int y = h.Y - 2; y <= h.Y + h.H + 1; y++) { ring.Add(g.Idx(h.X - 2, y)); ring.Add(g.Idx(h.X + h.W + 1, y)); }
        return ring;
    }

    /// <summary>The toy's pct(): "(v*100).toFixed(1).padStart(5) + '%'".</summary>
    public static string Pct(double v) => (v * 100).ToString("F1", System.Globalization.CultureInfo.InvariantCulture).PadLeft(5) + "%";
    public static string F(double v, int d) => v.ToString("F" + d, System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>Counts river crossings by whether the walker crossed on the ford or on a bridge.</summary>
public sealed class CrossingCounter
{
    private readonly sbyte[] _side = new sbyte[4096], _via = new sbyte[4096];
    public int Ford, Bridge;

    public CrossingCounter(World w)
    {
        var bank = w.Map.Bank;
        foreach (ref readonly var k in w.Walkers) _side[k.Id] = bank[k.Cell];
    }

    public void Sample(World w)
    {
        var bank = w.Map.Bank;
        foreach (ref readonly var k in w.Walkers)
        {
            sbyte b = bank[k.Cell];
            if (b == 0) { _via[k.Id] = w.BridgeAt[k.Cell] >= 0 ? (sbyte)2 : w.Ford[k.Cell] != 0 ? (sbyte)1 : _via[k.Id]; continue; }
            if (_side[k.Id] != 0 && b != _side[k.Id] && k.Visible)
            {
                if (_via[k.Id] == 2) Bridge++; else if (_via[k.Id] == 1) Ford++;
            }
            _side[k.Id] = b;
        }
    }
}
