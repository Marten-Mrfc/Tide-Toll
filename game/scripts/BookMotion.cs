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
