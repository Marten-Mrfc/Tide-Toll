using TideToll.Sim.Testing;

namespace TideToll.Sim.Tests;

/// <summary>
/// The clock of the timing tests (PreviewSliceTimingTests, PerfTests through TickBench): the calling thread's CPU time,
/// so time the thread spends off the CPU (another job has the core) does not count against the code being timed.
/// </summary>
public class CpuClockTests
{
    [Fact]
    public void TimeOffTheCpuDoesNotCount()
    {
        if (!OperatingSystem.IsLinux()) return; // elsewhere CpuClock falls back to wall time, as documented
        Assert.True(CpuClock.IsCpuTime, "thread CPU time is not available: the timing tests fall back to wall time");
        double t0 = CpuClock.NowMs();
        Thread.Sleep(200);
        double slept = CpuClock.NowMs() - t0;
        Assert.True(slept < 50, $"200 ms asleep counted {slept:F1} ms");
        // And work does count.
        t0 = CpuClock.NowMs();
        double x = 0;
        for (int i = 0; i < 20_000_000; i++) x += Math.Sqrt(i);
        Assert.True(CpuClock.NowMs() - t0 > 1 && x > 0);
    }
}
