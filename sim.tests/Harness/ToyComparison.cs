using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using static TideToll.Sim.Testing.ToyHarness;

namespace TideToll.Sim.Testing;

/// <summary>The toy's golden data (sim.tests/golden/toy-golden.json, written by sim.tests/golden/export.mjs).</summary>
public sealed class ToyGolden
{
    public JsonElement Root { get; }
    public string ToyCommit => Root.GetProperty("toyCommit").GetString() ?? "";

    private ToyGolden(JsonElement root) => Root = root;

    public static ToyGolden Load(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return new ToyGolden(doc.RootElement.Clone());
    }

    /// <summary>
    /// Finds sim.tests/golden/<paramref name="name"/> (toy-golden.json, the round-2 toy; toy-golden-v2.json, the finished
    /// toy) next to the binaries or by walking up to the repository root.
    /// </summary>
    public static string DefaultPath(string name = "toy-golden.json")
    {
        string local = Path.Combine(AppContext.BaseDirectory, "golden", name);
        if (File.Exists(local)) return local;
        for (var d = new DirectoryInfo(Directory.GetCurrentDirectory()); d != null; d = d.Parent)
        {
            string p = Path.Combine(d.FullName, "sim.tests", "golden", name);
            if (File.Exists(p)) return p;
        }
        throw new FileNotFoundException($"sim.tests/golden/{name} not found");
    }

    public List<SeedRow> Table() => Root.GetProperty("table").EnumerateArray().Select(r => new SeedRow(
        r.GetProperty("seed").GetInt32(), r.GetProperty("walkers").GetInt32(), r.GetProperty("ft").GetDouble(), r.GetProperty("det").GetDouble(),
        r.GetProperty("cov").GetDouble(), r.GetProperty("mean").GetDouble(), r.GetProperty("min").GetDouble(), r.GetProperty("max").GetDouble(),
        r.GetProperty("meanD").GetDouble(), r.GetProperty("below").GetInt32(),
        // Version 2 records a fingerprint of the row's values instead of the day-45 wear hash (0: not compared).
        r.TryGetProperty("wearFnv", out var wf) ? wf.GetUInt32() : 0)).ToList();
}

/// <summary>
/// The C# port against the toy on the same seeds (port plan, section 3). Where the runs are bit-identical
/// every number matches exactly; the tolerances are what the plan allows once floating-point drift has
/// made a run diverge (the toy's own seed-to-seed spread is far wider).
/// </summary>
public static class ToyComparison
{
    public const double FtMeanTol = 1.0, DetMeanTol = 0.02, CovMeanTol = 0.004, TrunkMeanTol = 0.015, TrunkMinTol = 0.015;
    public const double FtSeedTol = 3.0, DetSeedTol = 0.05;

    public static void Compare(IReadOnlyList<SeedRow> toy, IReadOnlyList<SeedRow> cs, CheckLog log)
    {
        var t = toy.OrderBy(r => r.Seed).ToList();
        var c = cs.OrderBy(r => r.Seed).ToList();
        log.Log($"\nC# PORT AGAINST THE TOY (seeds {t[0].Seed} to {t[^1].Seed}, {t[0].Walkers} walkers, days {GateTables.SampleFrom} to {GateTables.Days})");
        log.Log("seed | first trail toy/C# | detour toy/C#   | coverage toy/C#  | trunk mean toy/C#  | trunk min toy/C#   | day-45 wear grid");
        int exact = 0, exactRows = 0;
        for (int i = 0; i < t.Count; i++)
        {
            var a = t[i]; var b = c.First(r => r.Seed == a.Seed);
            bool same = a.WearFnv != 0 && a.WearFnv == b.WearFnv;
            if (same) exact++;
            if (a with { WearFnv = 0 } == b with { WearFnv = 0 } && same) exactRows++;
            log.Log($"{a.Seed,4} | {F(a.Ft, 1),6} {F(b.Ft, 1),6} s   | {F(a.Det, 3)} {F(b.Det, 3)}   | {Pct(a.Cov)} {Pct(b.Cov)}   | {Pct(a.Mean)} {Pct(b.Mean)}     | {Pct(a.Min)} {Pct(b.Min)}     | {(a.WearFnv == 0 ? "n/a" : same ? "identical" : "differs")}");
        }
        double M(IEnumerable<SeedRow> rows, Func<SeedRow, double> f) => GateTables.Mean(rows.Select(f).ToList());
        (string name, double toyV, double csV, double tol, double scale, string unit)[] metrics =
        {
            ("first trail", M(t, r => r.Ft), M(c, r => r.Ft), FtMeanTol, 1, " s"),
            ("detour ratio", M(t, r => r.Det), M(c, r => r.Det), DetMeanTol, 1, ""),
            ("trail coverage", M(t, r => r.Cov), M(c, r => r.Cov), CovMeanTol, 100, " pts"),
            ("trunk share, mean", M(t, r => r.Mean), M(c, r => r.Mean), TrunkMeanTol, 100, " pts"),
            ("trunk share, daily min", M(t, r => r.Min), M(c, r => r.Min), TrunkMinTol, 100, " pts"),
        };
        log.Log($"Exact parity: day-45 wear grid bit-identical on {exact} of {t.Count} seeds; every row value (first trail, detour, coverage, trunk mean/min/max/D, days out of band) bit-identical on {exactRows} of {t.Count}.");
        log.Log("20-seed means (toy | C# | difference | tolerance):");
        foreach (var m in metrics)
        {
            double diff = (m.csV - m.toyV) * m.scale;
            log.Log($"  {m.name,-24} {F(m.toyV * m.scale, 3),8} | {F(m.csV * m.scale, 3),8} | {(diff >= 0 ? "+" : "")}{F(diff, 3)}{m.unit} | +/-{F(m.tol * m.scale, 2)}{m.unit}");
            log.Check(Math.Abs(m.csV - m.toyV) <= m.tol, $"compare: 20-seed mean {m.name} within {F(m.tol * m.scale, 2)}{m.unit} of the toy");
        }
        var ftBad = t.Where(a => Math.Abs(a.Ft - c.First(r => r.Seed == a.Seed).Ft) > FtSeedTol).Select(a => a.Seed).ToList();
        var detBad = t.Where(a => Math.Abs(a.Det - c.First(r => r.Seed == a.Seed).Det) > DetSeedTol).Select(a => a.Seed).ToList();
        log.Check(ftBad.Count == 0, $"compare: first trail within {FtSeedTol} s of the toy on every seed{(ftBad.Count > 0 ? " (off on " + string.Join(", ", ftBad) + ")" : "")}");
        log.Check(detBad.Count == 0, $"compare: detour within {DetSeedTol} of the toy on every seed{(detBad.Count > 0 ? " (off on " + string.Join(", ", detBad) + ")" : "")}");
    }
}
