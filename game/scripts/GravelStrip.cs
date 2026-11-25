using System;

namespace TideToll.Game;

/// <summary>
/// The gravel layer drawn as the toy draws it (ink.ts drawGravel): a strip 1.15 cells wide with round ends, along every
/// gravel cell and joining each one to its gravel neighbours east, south and on both diagonals, so a strip brushed along
/// a slanting trail is one smooth band, not a staircase of squares. Rasterised at <see cref="Res"/> texels per cell into
/// an edge ramp (0.5 on the strip's edge, rising one cell's width inside it), which the ground shader thresholds with
/// screen-space antialiasing for the strip, its rim and its stones. Runs only on an edit; no allocation after the first.
/// </summary>
public static class GravelStrip
{
    /// <summary>Texels per cell.</summary>
    public const int Res = 2;
    /// <summary>Half the strip's width, in cells (the toy's 1.15-cell stroke).</summary>
    public const float Half = 0.575f;

    private static readonly (int dx, int dy)[] Links = { (1, 0), (0, 1), (1, 1), (-1, 1) };

    /// <summary>Fills <paramref name="texels"/> ((n * Res)^2 bytes) from the gravel grid; returns the gravel cells.</summary>
    public static int Rasterise(ReadOnlySpan<byte> gravel, int n, byte[] texels)
    {
        Array.Clear(texels);
        int count = 0;
        for (int c = 0; c < gravel.Length; c++)
        {
            if (gravel[c] == 0) continue;
            count++;
            int x = c % n, y = c / n;
            Capsule(texels, n, x + 0.5f, y + 0.5f, x + 0.5f, y + 0.5f);
            foreach (var (dx, dy) in Links)
            {
                int vx = x + dx, vy = y + dy;
                if (vx < 0 || vx >= n || vy >= n || gravel[vy * n + vx] == 0) continue;
                Capsule(texels, n, x + 0.5f, y + 0.5f, vx + 0.5f, vy + 0.5f);
            }
        }
        return count;
    }

    /// <summary>A round-capped segment from (ax, ay) to (bx, by) in cells, merged into the ramp by maximum.</summary>
    private static void Capsule(byte[] t, int n, float ax, float ay, float bx, float by)
    {
        int size = n * Res;
        const float reach = Half + 0.6f; // the ramp runs half a cell outside the edge
        int x0 = Math.Max(0, (int)MathF.Floor((MathF.Min(ax, bx) - reach) * Res));
        int x1 = Math.Min(size - 1, (int)MathF.Ceiling((MathF.Max(ax, bx) + reach) * Res));
        int y0 = Math.Max(0, (int)MathF.Floor((MathF.Min(ay, by) - reach) * Res));
        int y1 = Math.Min(size - 1, (int)MathF.Ceiling((MathF.Max(ay, by) + reach) * Res));
        float ex = bx - ax, ey = by - ay, l2 = ex * ex + ey * ey;
        for (int py = y0; py <= y1; py++)
            for (int px = x0; px <= x1; px++)
            {
                float x = (px + 0.5f) / Res, y = (py + 0.5f) / Res;
                float tt = l2 > 0 ? Math.Clamp(((x - ax) * ex + (y - ay) * ey) / l2, 0f, 1f) : 0f;
                float qx = ax + ex * tt - x, qy = ay + ey * tt - y;
                float d = MathF.Sqrt(qx * qx + qy * qy);
                // 0.5 on the edge, 1 a half-cell inside, 0 a half-cell outside.
                float v = Math.Clamp(0.5f + (Half - d), 0f, 1f);
                byte b = (byte)(v * 255f + 0.5f);
                int i = py * size + px;
                if (b > t[i]) t[i] = b;
            }
    }
}
