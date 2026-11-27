using System;

namespace TideToll.Game;

/// <summary>
/// One staged transition of the book (closing, opening, the settings spread, the question, a page turning): a single
/// timeline of fixed length in seconds, driven by real time, from which every part (camera, cover, town models,
/// interface, lettering, pages, sound cues) reads its place through eased curves. A frame never advances it by more
/// than <see cref="MaxStep"/> (a slow frame delays the motion rather than making it jump). Tests and frame strips can
/// hold it at any moment (<see cref="Hold"/>). Allocates nothing.
/// </summary>
public sealed class BookMotion
{
    /// <summary>The longest step one frame may take, seconds.</summary>
    public const float MaxStep = 1f / 20f;

    public float Length { get; private set; }
    /// <summary>Seconds since the start, 0..Length.</summary>
    public float Time { get; private set; }
    private float _prev;
    private bool _on;
    private float _held = -1f;

    /// <summary>A transition is under way (it has started and not yet reached its end).</summary>
    public bool Running => _on;
    /// <summary>0..1 of the way through.</summary>
    public float Progress => Length > 0 ? Time / Length : 1f;
    /// <summary>Held by a test at a moment (the clock stands still).</summary>
    public bool Held => _held >= 0f;

    public void Start(float length)
    {
        Length = Math.Max(0.001f, length);
        Time = _prev = 0f;
        _on = true;
        if (_held >= 0f) Time = _prev = Math.Min(_held, Length);
    }

    /// <summary>Advances by a frame's delta; true on the frame it reaches its end (once).</summary>
    public bool Advance(double delta)
    {
        if (!_on) return false;
        _prev = Time;
        if (_held >= 0f) { Time = Math.Min(_held, Length); return false; }
        Time = Math.Min(Length, Time + Math.Min((float)delta, MaxStep));
        if (Time < Length) return false;
        _on = false;
        return true;
    }

    /// <summary>Jumps to the end (the next Advance reports the end).</summary>
    public void Finish() { if (_on) { _prev = Time; Time = Length; } }

    /// <summary>Stops without reaching the end (another transition takes over).</summary>
    public void Stop() => _on = false;

    /// <summary>Tests and frame strips: the clock stands at this many seconds in until <see cref="Release"/>.</summary>
    public void Hold(float seconds) { _held = Math.Max(0f, seconds); if (_on) Time = Math.Min(_held, Length); }
    public void Release() => _held = -1f;

    /// <summary>0..1 through the window [t0, t1] (seconds), linear.</summary>
    public float Linear(float t0, float t1) => t1 <= t0 ? (Time >= t1 ? 1f : 0f) : Math.Clamp((Time - t0) / (t1 - t0), 0f, 1f);
    /// <summary>0..1 through [t0, t1], eased in and out (no speed and no acceleration at either end).</summary>
    public float Ease(float t0, float t1) => Smoother(Linear(t0, t1));
    /// <summary>True on the one frame the clock passes <paramref name="at"/> (sound cues).</summary>
    public bool Passed(float at) => _prev < at && Time >= at;
    /// <summary>A carried speed's term at the clock's time now (see <see cref="Carry(float, float, float)"/>).</summary>
    public float Carry(float v, float d) => Carry(v, d, Time);

    /// <summary>
    /// Speed carried over (round L3): a transition that takes over another mid-way adds this to each part's eased curve,
    /// so the part leaves the moment of the takeover at the speed it had (<paramref name="v"/>, units per second) instead
    /// of dropping to rest. It is v * d * u(1-u)^3(1+3u), u = t / d (the quintic Hermite term for a start speed): speed v
    /// and no acceleration at t = 0; no place, speed or acceleration left at t = d and after. With v = 0 it is nothing,
    /// so a curve from rest is the plain smootherstep.
    /// </summary>
    public static float Carry(float v, float d, float t)
    {
        if (v == 0f || d <= 0f || t >= d || t <= 0f) return 0f;
        float u = t / d, w = 1f - u;
        return v * d * u * w * w * w * (1f + 3f * u);
    }

    /// <summary>
    /// Speed and acceleration carried over: <see cref="Carry(float, float, float)"/> plus a * d^2 * u^2(1-u)^3 / 2 (the
    /// quintic Hermite term for a start acceleration <paramref name="a"/>, units per second squared): the part leaves
    /// the takeover with both, and has neither left at t = d.
    /// </summary>
    public static float Carry(float v, float a, float d, float t)
    {
        if (d <= 0f || t >= d || t <= 0f) return 0f;
        float u = t / d, w = 1f - u, w3 = w * w * w;
        return v * d * u * w3 * (1f + 3f * u) + 0.5f * a * d * d * u * u * w3;
    }

    /// <summary>The speed of <see cref="Carry(float, float, float)"/> at t (units per second).</summary>
    public static float CarrySpeed(float v, float d, float t)
    {
        if (v == 0f || d <= 0f || t >= d) return 0f;
        float u = Math.Max(0f, t / d), u2 = u * u;
        return v * (1f - 18f * u2 + 32f * u2 * u - 15f * u2 * u2);
    }

    /// <summary>The speed of <see cref="Carry(float, float, float, float)"/> at t (units per second).</summary>
    public static float CarrySpeed(float v, float a, float d, float t)
    {
        if (d <= 0f || t >= d) return 0f;
        float u = Math.Max(0f, t / d), u2 = u * u;
        return v * (1f - 18f * u2 + 32f * u2 * u - 15f * u2 * u2) + a * d * (u - 4.5f * u2 + 6f * u2 * u - 2.5f * u2 * u2);
    }

    /// <summary>The acceleration of <see cref="Carry(float, float, float, float)"/> at t (units per second squared).</summary>
    public static float CarryAccel(float v, float a, float d, float t)
    {
        if (d <= 0f || t >= d) return 0f;
        float u = Math.Max(0f, t / d), u2 = u * u;
        return v / d * (-36f * u + 96f * u2 - 60f * u2 * u) + a * (1f - 9f * u + 18f * u2 - 10f * u2 * u);
    }

    /// <summary>The acceleration of smootherstep in u (d2/du2).</summary>
    public static float SmootherAccel(float u)
    {
        if (u <= 0f || u >= 1f) return 0f;
        return 60f * u * (1f - u) * (1f - 2f * u);
    }

    /// <summary>The speed of smootherstep in u (d/du).</summary>
    public static float SmootherSpeed(float u)
    {
        if (u <= 0f || u >= 1f) return 0f;
        float w = 1f - u;
        return 30f * u * u * w * w;
    }

    /// <summary>Smootherstep: 6u^5 - 15u^4 + 10u^3, speed and acceleration zero at 0 and 1.</summary>
    public static float Smoother(float u)
    {
        u = Math.Clamp(u, 0f, 1f);
        return u * u * u * (u * (u * 6f - 15f) + 10f);
    }

    /// <summary>Smoothstep, speed zero at both ends.</summary>
    public static float Smooth(float u)
    {
        u = Math.Clamp(u, 0f, 1f);
        return u * u * (3f - 2f * u);
    }

    /// <summary>
    /// A board coming down: it gathers speed under its own weight, slows in the last part, touches (1 at
    /// <paramref name="touch"/>, with no speed), lifts a hair (<paramref name="settle"/>) and comes to rest at 1.
    /// Continuous in position and speed everywhere.
    /// </summary>
    public static float Land(float u, float touch = 0.8f, float settle = 0.012f)
    {
        u = Math.Clamp(u, 0f, 1f);
        if (u < touch)
        {
            float w = u / touch;
            // Weighted toward the end: slow to leave, quick through the middle-late part.
            return Smoother(MathF.Pow(w, 1.25f));
        }
        float v = (u - touch) / Math.Max(1e-4f, 1f - touch);
        float s = MathF.Sin(MathF.PI * v);
        return 1f - settle * s * s;
    }

    /// <summary>Linear interpolation.</summary>
    public static float Mix(float a, float b, float t) => t >= 1f ? b : t <= 0f ? a : a + (b - a) * t;

    /// <summary>A view height eased in log space (a zoom reads evenly at every scale).</summary>
    public static float MixSize(float a, float b, float t) => t >= 1f ? b : t <= 0f ? a : MathF.Exp(Mix(MathF.Log(Math.Max(1e-3f, a)), MathF.Log(Math.Max(1e-3f, b)), t));
}
