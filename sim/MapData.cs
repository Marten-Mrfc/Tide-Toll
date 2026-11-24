using System;

namespace TideToll.Sim;

public enum BuildingKind : byte { House = 0, Well = 1, Field = 2, Market = 3 }

/// <summary>A building on the map: its footprint's top-left cell, size and door cell (the toy's Building).</summary>
public readonly struct Building
{
    public readonly int Id;
    public readonly BuildingKind Kind;
    public readonly int X, Y, W, H;
    /// <summary>Cell index walkers go to and leave from.</summary>
    public readonly int Door;

    public Building(int id, BuildingKind kind, int x, int y, int w, int h, int door)
    {
        Id = id; Kind = kind; X = x; Y = y; W = w; H = h; Door = door;
    }
}

/// <summary>
/// The generated map (the toy's MapData): flat layers indexed y * Size + x. Built once by
/// <see cref="MapGen.Generate"/> and never changed by the simulation afterwards.
/// </summary>
public sealed class MapData
{
    public int Seed { get; }
    public int Size { get; }
    /// <summary>River cells: impassable unless ford or bridge.</summary>
    public byte[] Water { get; }
    /// <summary>Natural shallows: walkable but slow.</summary>
    public byte[] Ford { get; }
    /// <summary>Building footprints that block walking (doors excluded).</summary>
    public byte[] Footprint { get; }
    /// <summary>Field cells: walkable with a crop penalty.</summary>
    public byte[] Crop { get; }
    /// <summary>Smooth fixed noise in [-1, 1].</summary>
    public float[] Noise { get; internal set; }
    /// <summary>River centre x per row, in cell units.</summary>
    public float[] RiverX { get; }
    public double RiverHalfW { get; internal set; }
    /// <summary>Centre row of the ford.</summary>
    public int FordY { get; internal set; }
    public Building[] Buildings { get; internal set; }
    /// <summary>Side of the river each cell is on: -1 west, +1 east, 0 water.</summary>
    public sbyte[] Bank { get; }

    internal MapData(int seed, int size)
    {
        Seed = seed;
        Size = size;
        int nc = size * size;
        Water = new byte[nc];
        Ford = new byte[nc];
        Footprint = new byte[nc];
        Crop = new byte[nc];
        Bank = new sbyte[nc];
        RiverX = new float[size];
        Noise = Array.Empty<float>();
        Buildings = Array.Empty<Building>();
    }
}
