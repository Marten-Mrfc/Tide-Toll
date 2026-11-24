using TideToll.Sim.Testing;
using Xunit.Abstractions;

namespace TideToll.Sim.Tests;

/// <summary>
/// The toy's headless checks (test/sim.test.ts), ported one for one in Harness/ToyChecks.cs and
/// Harness/ToyPreviewChecks.cs: same setups, same measurements, same pass criteria. Each test prints the
/// toy's lines and fails naming every failed check.
/// </summary>
public class ToyCheckTests
{
    private readonly ITestOutputHelper _out;
    public ToyCheckTests(ITestOutputHelper output) => _out = output;

    private void Verify(Action<CheckLog> body, int expectedChecks)
    {
        var log = new CheckLog();
        body(log);
        _out.WriteLine(log.ToString());
        Console.WriteLine(log.ToString());
        Assert.Equal(expectedChecks, log.Checks.Count);
        Assert.True(log.AllPassed, "Failed: " + string.Join("; ", log.Checks.Where(c => !c.Ok).Select(c => c.What)) + "\n" + log);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Reroute(int seed) => Verify(l => ToyChecks.Reroute(seed, l), 4);

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Bridge(int seed) => Verify(l => ToyChecks.BridgeCheck(seed, l), 2);

    [Fact]
    public void Robustness() => Verify(ToyChecks.Robustness, 3);

    [Fact]
    public void Determinism() => Verify(ToyChecks.Determinism, 1);

    [Fact]
    public void PreviewIsolation() => Verify(ToyPreviewChecks.Isolation, 1);

    [Fact]
    public void PreviewBasics() => Verify(ToyPreviewChecks.Basics, TickBench.IsRelease ? 5 : 4);

    // Six seeds of 26 days each with tracking: the slowest toy check, so it runs with the gate.
    [Fact]
    [Trait("Category", "Gate")]
    public void PreviewAgreement() => Verify(l => ToyPreviewChecks.Agreement(new[] { 1, 2, 3, 4, 5, 6 }, l), 3);
}
