using System;
using Godot;

namespace TideToll.Game;

/// <summary>
/// n10 (performance research Q4): the "near wear" mask. For each cell, 255 when any cell within <see cref="NearReach"/>
/// cells (a square) has wear, else 0: an R8 128 x 128 texture read with nearest filtering. The ground shader runs its
/// wear block (27 reads of the wear grid, the ring's trigonometry, the stroke noise) only where the mask is set; every
/// wear read of that block lies within 4 cells of the pixel's own cell (warp under 0.66 cell, ring 2.2 cells, bilinear
/// footprint 1 cell), so where the mask is 0 every one of them reads 0 and the block's results are exactly what the
/// shader then sets (no wear, no stroke direction): the picture does not change. Built from the same bytes as the wear
/// texture each time it is uploaded, by two sliding-window passes (no allocation); uploaded only when it changes.
/// </summary>
public partial class TownGround
{
    public const int NearReach = 5;
    private readonly byte[] _near = new byte[N * N], _nearRows = new byte[N * N], _nearNext = new byte[N * N];
    private Image _nearImg = null!;
    private ImageTexture _nearTex = null!;
    /// <summary>Cells the mask marks as near wear (tests and the notes).</summary>
    public int NearCells { get; private set; }

    private void NearInit()
    {
        _nearImg = Image.CreateFromData(N, N, false, Image.Format.R8, _near);
        _nearTex = ImageTexture.CreateFromImage(_nearImg);
        Material.SetShaderParameter("near_tex", _nearTex);
    }

    private void NearUpload()
    {
        const int r = NearReach;
        // Rows: a cell is set when a worn cell lies within r along its row.
        for (int y = 0; y < N; y++)
        {
            int row = y * N, count = 0;
            for (int x = 0; x <= Math.Min(r, N - 1); x++) if (_rg[2 * (row + x)] != 0) count++;
            for (int x = 0; x < N; x++)
            {
                _nearRows[row + x] = count > 0 ? (byte)1 : (byte)0;
                int add = x + r + 1, drop = x - r;
                if (add < N && _rg[2 * (row + add)] != 0) count++;
                if (drop >= 0 && _rg[2 * (row + drop)] != 0) count--;
            }
        }
        // Columns over the rows' result: the square of side 2r + 1.
        int set = 0;
        for (int x = 0; x < N; x++)
        {
            int count = 0;
            for (int y = 0; y <= Math.Min(r, N - 1); y++) count += _nearRows[y * N + x];
            for (int y = 0; y < N; y++)
            {
                byte v = count > 0 ? (byte)255 : (byte)0;
                _nearNext[y * N + x] = v;
                if (v != 0) set++;
                int add = y + r + 1, drop = y - r;
                if (add < N) count += _nearRows[add * N + x];
                if (drop >= 0) count -= _nearRows[drop * N + x];
            }
        }
        NearCells = set;
        if (_nearNext.AsSpan().SequenceEqual(_near)) return;
        Array.Copy(_nearNext, _near, _near.Length);
        _nearImg.SetData(N, N, false, Image.Format.R8, _near);
        _nearTex.Update(_nearImg);
    }
}
