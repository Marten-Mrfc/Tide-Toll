using TideToll.Sim.Testing;
using Xunit.Abstractions;

namespace TideToll.Sim.Tests;

/// <summary>
/// Runs the 20 held-out gate seeds once (in parallel) for all the gate tests: on the finished toy's trail model with rain
/// and growth off (SimParams.Toy, the gate's definition), and on the round-2 model for the comparison with the round-2
/// toy's table (toy-golden.json).
/// </summary>
public sealed class GateRowsFixture
{
    private static int Threads => Math.Max(1, Math.Min(4, Environment.ProcessorCount - 1));
    public List<SeedRow> Rows { get; } = GateTables.RunSeeds(GateTables.Range(101, 20), Threads, SimParams.Toy);
    private readonly Lazy<List<SeedRow>> _legacy = new(() => GateTables.RunSeeds(GateTables.Range(101, 20), Threads, SimParams.Legacy));
    public List<SeedRow> LegacyRows => _legacy.Value;
}

/// <summary>
/// The toy's gate (test/sim.test.ts main) on held-out seeds 101 to 120, and the comparison with the toy's
/// own table from golden/toy-golden.json. Long statistical runs: Category=Gate, run by ./check --gate.
/// They report what they measure; a failing gate fails here exactly as it fails in the toy.
/// </summary>
[Trait("Category", "Gate")]
public class GateTests : IClassFixture<GateRowsFixture>
{
    private readonly GateRowsFixture _f;
    private readonly ITestOutputHelper _out;
    public GateTests(GateRowsFixture f, ITestOutputHelper output) { _f = f; _out = output; }

    private string Table()
    {
        var sw = new StringWriter();
        GateTables.PrintTable(sw, "GATE G3 (held-out seeds 101 to 120): every daily sample must lie within 30% to 70%", _f.Rows);
        _out.WriteLine(sw.ToString());
        return sw.ToString();
    }

    [Fact]
    public void G3TrunkShareStaysInBandOnEveryHeldOutSeed()
    {
        string table = Table();
        var log = new CheckLog();
        GateTables.GateChecks(_f.Rows, log);
        var bad = _f.Rows.Where(r => r.Min < 0.3 || r.Max > 0.7).Select(r => $"seed {r.Seed} min {ToyHarness.Pct(r.Min)} max {ToyHarness.Pct(r.Max)} ({r.Below} days out of band)");
        Assert.True(log.Checks[0].Ok, log.Checks[0].What + " FAILED: " + string.Join("; ", bad) + "\n" + table);
    }

    [Fact]
    public void FirstTrailFormsWithin3DaysOnEveryHeldOutSeed()
    {
        var log = new CheckLog();
        GateTables.GateChecks(_f.Rows, log);
        Assert.True(log.Checks[1].Ok, log.Checks[1].What + "\n" + Table());
    }

    /// <summary>The round-2 model (SimParams.Legacy) against the round-2 toy's table.</summary>
    [Fact]
    public void MetricsAgreeWithTheToyWithinThePortPlanTolerances()
    {
        var log = new CheckLog();
        ToyComparison.Compare(Golden.Toy.Table(), _f.LegacyRows, log);
        _out.WriteLine(log.ToString());
        Console.WriteLine(log.ToString());
        Assert.True(log.AllPassed, "Failed: " + string.Join("; ", log.Checks.Where(c => !c.Ok).Select(c => c.What)) + "\n" + log);
    }
}
