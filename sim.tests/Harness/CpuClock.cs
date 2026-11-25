using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TideToll.Sim.Testing;

/// <summary>
/// CPU time of the calling thread, for timing checks on a shared machine: other processes and test threads
/// taking the cores do not count against the code being timed. Linux only (clock_gettime with
/// CLOCK_THREAD_CPUTIME_ID); elsewhere it falls back to wall time.
/// </summary>
public static class CpuClock
{
    [StructLayout(LayoutKind.Sequential)]
    private struct TimeSpec { public long Sec; public long Nsec; }

    [DllImport("libc", EntryPoint = "clock_gettime")]
    private static extern int ClockGetTime(int clock, out TimeSpec t);

    private const int ClockThreadCpuTime = 3;
    private static readonly bool Linux = OperatingSystem.IsLinux() && Probe();

    private static bool Probe()
    {
        try { return ClockGetTime(ClockThreadCpuTime, out _) == 0; }
        catch (Exception) { return false; }
    }

    /// <summary>True when <see cref="NowMs"/> is the thread's CPU time, false when it is wall time.</summary>
    public static bool IsCpuTime => Linux;

    /// <summary>Milliseconds of this thread's CPU time (or wall time where that is not available), from an arbitrary origin.</summary>
    public static double NowMs()
    {
        if (Linux && ClockGetTime(ClockThreadCpuTime, out var t) == 0) return t.Sec * 1000.0 + t.Nsec / 1e6;
        return Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency;
    }
}
