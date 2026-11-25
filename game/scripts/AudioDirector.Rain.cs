using System;
using Godot;
using TideToll.Sim;

namespace TideToll.Game;

/// <summary>
/// Chapter I's weather in sound, with the sounds that exist (no new assets): while it rains, a rain bed made from the
/// river loop pitched up and kept low and wide (non-positional, on the Ambience bus, so the pause sweep muffles it),
/// following the weather's rain level; and footsteps on mud are wet: the ford's splash set, softer and lower, wherever
/// most of a chunk's walkers tread mud (World.Mud while the ground is wet).
/// </summary>
public partial class AudioDirector
{
    private AudioStreamPlayer _rainPlayer = null!;
    private readonly int[] _mudN = new int[64];
    /// <summary>The rain bed's level (dB, -80 when dry) and wet footsteps played (tests).</summary>
    public float RainDb { get; private set; } = -80;
    public int MudSteps;

    private void InitRain()
    {
        var bed = GD.Load<AudioStream>("res://assets/audio/ambience/water-river-loop.ogg");
        _rainPlayer = new AudioStreamPlayer { Name = "Rain", Bus = "Ambience", Stream = bed, VolumeDb = -80, PitchScale = 1.45f };
        AddChild(_rainPlayer);
    }

    /// <summary>The rain bed follows the rain (eased; four times a second with the river's level).</summary>
    private void RainBed(World w)
    {
        float rain = w.RainOn ? (float)w.Weather.Rain : 0f;
        float db = rain < 0.02f ? -80f : Mathf.LinearToDb(rain) - 12f;
        RainDb = Mathf.MoveToward(RainDb, db, 5f);
        _rainPlayer.VolumeDb = RainDb;
        bool want = RainDb > -70f && !Silent;
        if (want && !_rainPlayer.Playing) _rainPlayer.Play();
        else if (!want && _rainPlayer.Playing) _rainPlayer.Stop();
    }

    /// <summary>Whether a walker on this cell treads wet mud now.</summary>
    private static bool OnMud(World w, int cell) => w.RainOn && w.MudCells > 0 && w.Weather.Wetness > 0.05 && w.Mud[cell] != 0;
}
