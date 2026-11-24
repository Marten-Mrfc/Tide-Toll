using System;

namespace TideToll.Sim;

/// <summary>
/// The few JavaScript maths functions whose .NET counterparts give different bits, ported so the
/// C# simulation can be compared with the toy run for run.
/// </summary>
public static class JsMath
{
    /// <summary>
    /// V8's Math.hypot for two arguments: scale by the larger magnitude, sum the squares with Kahan
    /// compensation, then sqrt(sum) * max. The naive Math.Sqrt(x*x + y*y) differs from V8 in the last
    /// bit on about 38% of inputs; this replica matched 200,000 of 200,000 (port plan, section 2).
    /// </summary>
    public static double Hypot(double x, double y)
    {
        double ax = Math.Abs(x), ay = Math.Abs(y);
        if (double.IsPositiveInfinity(ax) || double.IsPositiveInfinity(ay)) return double.PositiveInfinity;
        if (double.IsNaN(ax) || double.IsNaN(ay)) return double.NaN;
        double max = Math.Max(ax, ay);
        if (max == 0) return 0;
        double sum = 0, comp = 0;
        double n = ax / max;
        double summand = n * n - comp;
        double prelim = sum + summand;
        comp = (prelim - sum) - summand;
        sum = prelim;
        n = ay / max;
        summand = n * n - comp;
        prelim = sum + summand;
        sum = prelim;
        return Math.Sqrt(sum) * max;
    }

    /// <summary>JavaScript's Math.round: halves round towards +infinity (C#'s Math.Round rounds to even).</summary>
    public static double Round(double v)
    {
        double f = Math.Floor(v);
        return v - f >= 0.5 ? f + 1 : f;
    }
}
