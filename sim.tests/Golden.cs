using TideToll.Sim.Testing;

namespace TideToll.Sim.Tests;

/// <summary>The toy's golden data, loaded once (sim.tests/golden/toy-golden.json; see golden/export.mjs).</summary>
public static class Golden
{
    private static readonly Lazy<ToyGolden> Data = new(() => ToyGolden.Load(ToyGolden.DefaultPath()));
    public static ToyGolden Toy => Data.Value;
    public static System.Text.Json.JsonElement Root => Data.Value.Root;
}
