using System.Text.RegularExpressions;

namespace TideToll.Sim.Tests;

public class ParamsAndRulesTests
{
    [Fact]
    public void TimeScaleMatchesTheToy()
    {
        var p = SimParams.Default;
        Assert.Equal(10, p.TickHz);
        Assert.Equal(150, p.TicksPerDay);
        Assert.Equal(600, p.TicksPerSeason);
        Assert.Equal(2.0, p.CellMeters);
        Assert.Equal(128 * 128, p.Grid.Count);
        Assert.Equal(512, (p with { GridWidth = 512 }).Grid.Width);
    }

    // The sim must stay engine-free and deterministic; this guards the rule in source, not by review.
    [Fact]
    public void SimSourceUsesNoEngineRandomOrClock()
    {
        string dir = FindSimDir();
        var banned = new Regex(@"\bGodot\b|System\.Random|new Random\(|DateTime|Stopwatch|Environment\.TickCount|Guid\.NewGuid");
        var bad = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .SelectMany(f => File.ReadLines(f).Select((l, i) => (f, i, l)))
            .Where(t => !t.l.TrimStart().StartsWith("//") && !t.l.TrimStart().StartsWith("///") && banned.IsMatch(t.l))
            .Select(t => $"{Path.GetFileName(t.f)}:{t.i + 1}: {t.l.Trim()}")
            .ToList();
        Assert.True(bad.Count == 0, "Banned API in sim/: " + string.Join("; ", bad));
    }

    private static string FindSimDir()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d != null && !File.Exists(Path.Combine(d.FullName, "sim", "TideToll.Sim.csproj"))) d = d.Parent;
        Assert.NotNull(d);
        return Path.Combine(d!.FullName, "sim");
    }
}
