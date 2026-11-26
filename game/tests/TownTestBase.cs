using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game.Tests;

/// <summary>
/// Shared frame for the town's test scenes: loads main.tscn at a real window size, runs an async script, and ends with
/// Quit(0) after printing the pass marker, or Quit(1) after "FAIL: ..." (Godot only logs unhandled C# exceptions,
/// so every failure is caught and turned into an exit code; tools/godot-test.sh also fails any error line).
/// </summary>
public abstract partial class TownTestBase : Node
{
    protected Main Main = null!;
    protected Town T => Main.Driver.Town;
    protected readonly List<string> Notes = new();
    protected virtual Vector2I WindowSize => new(1280, 720);

    public override void _Ready()
    {
        GetWindow().Size = WindowSize;
        // Each test plays in a folder of its own (settings and save), emptied first: the player's own user:// files
        // never reach a test, and no test continues another's town.
        Main.Profile = ProfileFor(Marker);
        Main.KeepWindow = true;
        WipeProfile();
        Main = GD.Load<PackedScene>("res://main.tscn").Instantiate<Main>();
        AddChild(Main);
        Go();
    }

    private async void Go()
    {
        try
        {
            await TestInput.Frames(this, 3);
            await Run();
            GD.Print(Marker + " " + string.Join(" ", Notes));
            TestInput.QuitClean(this, 0);
        }
        catch (Exception e)
        {
            GD.Print("FAIL: " + e.Message);
            GD.Print(e.StackTrace ?? "");
            TestInput.QuitClean(this, 1);
        }
    }

    protected abstract string Marker { get; }

    public static string ProfileFor(string marker) => "user://tests/" + marker.Replace(' ', '-').ToLowerInvariant() + "/";

    protected static void WipeProfile()
    {
        var dir = DirAccess.Open(Main.Profile);
        if (dir != null) foreach (var f in dir.GetFiles()) dir.Remove(f);
        DirAccess.MakeDirRecursiveAbsolute(Main.Profile);
    }
    protected abstract Task Run();

    protected static void Check(bool ok, string what)
    {
        if (!ok) throw new Exception(what);
    }

    protected Task Frames(int n) => TestInput.Frames(this, n);

    /// <summary>Waits frames until a condition holds; fails after maxFrames.</summary>
    protected async Task Until(Func<bool> cond, int maxFrames, string what)
    {
        for (int i = 0; i < maxFrames; i++)
        {
            if (cond()) return;
            await Frames(1);
        }
        throw new Exception("timed out waiting for: " + what);
    }

    /// <summary>A real left click (press, then release a frame later) at a screen point.</summary>
    protected async Task Click(Vector2 at)
    {
        TestInput.Motion(at);
        await Frames(1);
        TestInput.Button(at, MouseButton.Left, true);
        await Frames(1);
        TestInput.Button(at, MouseButton.Left, false);
        await Frames(2);
    }

    protected async Task Key(Key k)
    {
        TestInput.Key(k);
        await Frames(2);
    }
}
