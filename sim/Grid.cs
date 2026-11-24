using System;
using System.Collections.Generic;

namespace TideToll.Sim;

/// <summary>
/// Shape of a flat cell grid indexed y * Width + x (one array per layer, no node per cell).
/// The toy fixed the size at 128; the game needs 512 x 384 and larger, so the size is a value here.
/// </summary>
public readonly struct Grid
{
    public readonly int Width;
    public readonly int Height;

    public Grid(int width, int height)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Grid sides must be positive.");
        Width = width;
        Height = height;
    }

    public int Count => Width * Height;

    // Eight neighbours; the first four are orthogonal, which the corner rule relies on.
    public static readonly int[] DX = { 1, -1, 0, 0, 1, 1, -1, -1 };
    public static readonly int[] DY = { 0, 0, 1, -1, 1, -1, 1, -1 };
    public static readonly double[] DL = { 1, 1, 1, 1, Math.Sqrt(2), Math.Sqrt(2), Math.Sqrt(2), Math.Sqrt(2) };

    public int Idx(int x, int y) => y * Width + x;
    public int X(int cell) => cell % Width;
    public int Y(int cell) => cell / Width;
    public bool Inside(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    /// <summary>
    /// 4-connected line between two cells, appended to <paramref name="cells"/>, so a fence
    /// drawn diagonally has no gap to slip through. Same cells, same order as the toy's line4.
    /// </summary>
    public void Line4(int x0, int y0, int x1, int y1, List<int> cells)
    {
        cells.Add(Idx(x0, y0));
        int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0);
        int sx = x1 > x0 ? 1 : -1, sy = y1 > y0 ? 1 : -1;
        int err = dx + dy, x = x0, y = y0;
        while (x != x1 || y != y1)
        {
            int e2 = 2 * err;
            bool stepX = e2 >= dy, stepY = e2 <= dx;
            if (stepX && stepY)
            {
                // A diagonal Bresenham step: fill the corner so the line stays 4-connected.
                x += sx;
                cells.Add(Idx(x, y));
                err += dy + dx;
                y += sy;
            }
            else if (stepX) { err += dy; x += sx; }
            else { err += dx; y += sy; }
            cells.Add(Idx(x, y));
        }
    }
}
