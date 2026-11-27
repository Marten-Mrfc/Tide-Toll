using System;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// Night 3 (feel): small rewards on the map, fed by the simulation's events (TownFeel.Spawn.cs) and placed from the
/// town's plan. One pool of at most <see cref="Cap"/> pieces for all rewards together, drawn by four MultiMeshes
/// (coin, puff, ring, grit): nothing allocated per frame, nothing saved, nothing read back by the simulation. Every
/// piece lives under a second of real time (the same at 1x and 8x). When the pool is busy new rewards are skipped,
/// never queued: low-priority ones (trail dust, coins) first. Pieces live in map metres under MapOrigin.
/// </summary>
public partial class TownFeel : Node3D
{
    /// <summary>The most pieces on screen for all rewards together.</summary>
    public const int Cap = 200;
    /// <summary>Above this many live pieces the small, frequent rewards (trail dust, coins, grit) are skipped.</summary>
    public const int Busy = 140;

    public enum Kind : byte { Coin = 0, Puff = 1, Ring = 2, Grit = 3, Band = 4 }
    private const int Kinds = 5;

    private struct Piece
    {
        public Kind Kind;
        public Vector3 Pos, Vel;
        public float Age, Life, Delay, Size, Grow, Gravity, Spin;
        public Color Color;
        public bool Lands;      // a coin's landing plays its sound once
    }

    /// <summary>New pieces allowed in one frame: a frame with a burst of events (a long step, a loaded town, the bot)
    /// starts only this many and skips the rest.</summary>
    public const int FrameBudget = 40;
    private int _budget;
    private readonly Piece[] _p = new Piece[Cap];
    private int _n;
    private readonly MultiMesh[] _mm = new MultiMesh[Kinds];
    private readonly int[] _count = new int[Kinds];
    private Main _main = null!;
    private TownDriver _drv = null!;
    private readonly Random _rng = new(5);

    /// <summary>The strips: when 0 or more, the rewards' clock is held (each frame steps <see cref="StripNext"/>) (and trail dust shows while
    /// the town is paused); -1 in the game.</summary>
    public static float StripStep = -1f;
    /// <summary>The strips: the step the next frame takes (once), while <see cref="StripStep"/> holds the clock.</summary>
    public static float StripNext;

    /// <summary>Tests and the strips: pieces alive now, the most at once, rewards started and skipped by kind of action.</summary>
    public int Live => _n;
    public int MostLive { get; private set; }
    public int Skipped { get; private set; }
    public readonly int[] Started = new int[(int)Reward.Count];

    public void Init(Main main)
    {
        _main = main;
        _drv = main.Driver;
        Name = "Feel";
        // Coin: a small flat disc of money (Thatch), lit like the models so it takes the ink edge.
        _mm[0] = Make("Coins", new CylinderMesh { TopRadius = 0.5f, BottomRadius = 0.5f, Height = 0.16f, RadialSegments = 12, Rings = 1 }, Flat(), true);
        // Puff: a low sphere, soft and fading (dust, sparkle).
        _mm[1] = Make("Puffs", new SphereMesh { Radius = 0.5f, Height = 1f, RadialSegments = 8, Rings = 4 }, Flat(true), false);
        // Ring: a thin flat torus that widens and fades (water, ground).
        _mm[2] = Make("Rings", new TorusMesh { InnerRadius = 0.86f, OuterRadius = 1f, Rings = 24, RingSegments = 3 }, Flat(), false);
        // Grit: small stones.
        // Grit: small earth-coloured stones (the instance colour, unshaded: lit and small they read as ink).
        _mm[3] = Make("Grit", new BoxMesh { Size = Vector3.One }, Flat(true), false);
        // Band: a broad flat ring (a quarter of its radius wide) for the small rings - flourish, gift, water - so the
        // colour shows inside the ink edge from the first frame.
        _mm[4] = Make("Bands", new TorusMesh { InnerRadius = 0.75f, OuterRadius = 1f, Rings = 24, RingSegments = 3 }, Flat(true), false);
        _drv.Replaced += Clear;
    }

    /// <summary>Flat palette colour per piece (the instance colour), opaque so the ink edge draws round it as round
    /// everything else; pieces leave by shrinking or sinking, never by fading.</summary>
    private static StandardMaterial3D Flat(bool softInk = false)
    {
        var m = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            VertexColorUseAsAlbedo = true,
            AlbedoColor = Colors.White,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        if (softInk)
        {
            // The small pieces take the soft ink (as trees and reeds do, ModelLib): their creases are not inked, so the
            // ink does not swallow a piece a few pixels wide.
            m.StencilMode = BaseMaterial3D.StencilModeEnum.Custom;
            m.StencilFlags = 2; // write
            m.StencilCompare = BaseMaterial3D.StencilCompareEnum.Always;
            m.StencilReference = 1;
        }
        return m;
    }

    private MultiMesh Make(string name, Mesh mesh, Material mat, bool shadow)
    {
        mesh.SurfaceSetMaterial(0, mat);
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, Mesh = mesh, InstanceCount = Cap, VisibleInstanceCount = 0 };
        var mi = new MultiMeshInstance3D { Name = name, Multimesh = mm, CastShadow = shadow ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(mi);
        return mm;
    }

    public void Clear()
    {
        _n = 0;
        for (int k = 0; k < Kinds; k++) { _count[k] = 0; _mm[k].VisibleInstanceCount = 0; }
    }

    /// <summary>Room for <paramref name="need"/> more pieces; low-priority rewards also keep under <see cref="Busy"/>.</summary>
    private bool Room(int need, bool low)
    {
        if (_n + need <= (low ? Busy : Cap) && _budget >= need) { _budget -= need; return true; }
        Skipped++;
        return false;
    }

    private void Add(Kind k, Vector3 pos, Vector3 vel, float life, float size, Color c, float delay = 0, float grow = 0, float gravity = 0)
    {
        if (_n >= Cap) return;
        _p[_n++] = new Piece { Kind = k, Pos = pos, Vel = vel, Life = life, Size = size, Color = c, Delay = delay, Grow = grow, Gravity = gravity,
            Spin = (float)_rng.NextDouble() * 6.28f, Lands = k == Kind.Coin };
        if (_n > MostLive) MostLive = _n;
    }

    private float R(float a, float b) => a + (float)_rng.NextDouble() * (b - a);

    /// <summary>Map cells to map metres (MapOrigin's space).</summary>
    private static Vector3 At(double x, double y, float h = 0) => new((float)x * TownGround.Cell, h, (float)y * TownGround.Cell);

    public override void _Process(double delta)
    {
        float dt = (float)Math.Min(delta, 0.1);
        if (StripStep >= 0) { dt = StripNext; StripNext = 0f; }
        _budget = FrameBudget;
        Spawn(dt);
        for (int k = 0; k < Kinds; k++) _count[k] = 0;
        int w = 0;
        for (int i = 0; i < _n; i++)
        {
            ref var p = ref _p[i];
            if (p.Delay > 0) { p.Delay -= dt; _p[w++] = p; continue; }
            p.Age += dt;
            if (p.Age >= p.Life) continue;
            p.Vel.Y -= p.Gravity * dt;
            p.Pos += p.Vel * dt;
            if (p.Kind is Kind.Coin or Kind.Grit && p.Pos.Y < 0.1f && p.Vel.Y < 0)
            {
                p.Pos.Y = 0.1f;
                if (p.Kind == Kind.Coin && p.Lands) { p.Lands = false; Sound("coin-hop"); }
                p.Vel = Vector3.Zero;
            }
            Draw(in p);
            _p[w++] = p;
        }
        _n = w;
        for (int k = 0; k < Kinds; k++) _mm[k].VisibleInstanceCount = _count[k];
    }

    private void Draw(in Piece p)
    {
        int k = (int)p.Kind, slot = _count[k]++;
        float u = p.Age / p.Life;
        Color c = p.Color;
        Transform3D xf;
        switch (p.Kind)
        {
            case Kind.Coin:
            {
                // Lies nearly flat (its face to the view from above) and rocks as it flips over in the air.
                var b = new Basis(Vector3.Right, MathF.Sin(p.Age * 14f) * 0.6f).Rotated(Vector3.Up, p.Spin);
                float s = p.Size * Math.Min(1f, 0.5f + u * 5f) * (u > 0.85f ? (1f - u) / 0.15f : 1f);
                // The hop also carries it up the screen (the view looks down steeply, so height alone hardly shows).
                xf = new Transform3D(b.Scaled(new Vector3(s, s, s)), p.Pos + new Vector3(0, 0, -(p.Pos.Y - 0.1f) * 2.0f));
                break;
            }
            case Kind.Ring:
            case Kind.Band:
            {
                float r = p.Size + p.Grow * EaseOut(u);
                // Widens, then sinks into the ground or the water over its last third.
                float sink = u > 0.65f ? (u - 0.65f) / 0.35f * 0.35f : 0f;
                xf = new Transform3D(Basis.Identity.Scaled(new Vector3(r, 0.5f, r)), p.Pos - new Vector3(0, sink, 0));
                break;
            }
            case Kind.Puff:
            {
                // Swells quickly and shrinks away.
                float s = p.Size * MathF.Pow(MathF.Sin(MathF.PI * Math.Min(1f, u * 1.15f)), 0.6f);
                xf = new Transform3D(Basis.Identity.Scaled(new Vector3(s, s * 0.75f, s)), p.Pos);
                break;
            }
            default:
            {
                float s = p.Size * (u > 0.8f ? (1f - u) / 0.2f : 1f);
                xf = new Transform3D(Basis.Identity.Rotated(Vector3.Up, p.Spin).Scaled(new Vector3(s, s * 0.7f, s)), p.Pos);
                break;
            }
        }
        _mm[k].SetInstanceTransform(slot, xf);
        // Palette colours are sRGB; the instance colour is read as linear (unshaded), so convert, or every piece shows
        // paler than its palette colour (gold read as cream).
        _mm[k].SetInstanceColor(slot, c.SrgbToLinear());
    }

    private static float EaseOut(float u) => 1f - (1f - u) * (1f - u);

    private void Sound(string id) => _main.Audio?.Feel(id);
}
