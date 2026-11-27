using System;
using Godot;

namespace TideToll.Game;

/// <summary>
/// Night 9: the one way out of the process, for the game (the cover's Quit, the window's close button) and for every
/// test (TestInput.QuitClean). It takes the scene down itself, while the engine and the .NET bridge are still whole,
/// and only then asks the tree to quit.
///
/// Why: a C# wrapper of an engine object that nothing holds any more (a mesh, a material, a MultiMesh made in code
/// per scene) is released by .NET's finalizer thread whenever the collector next runs. If the scene is first freed by
/// the engine's own shutdown, the wrappers it drops become garbage during that shutdown, and a collection then can run
/// their finalizers while the bridge is being torn down: "Handle is not initialized" in SetGodotObjectPtr. Which
/// wrapper loses the race is chance (about three runs in twenty of game-save), so the fix is the order, not any one
/// resource: free every node under the root, let the frame end so the engine drops its references, then collect and
/// wait for the finalizers, a few times (a released object can release others), then quit. By the time the engine
/// shuts down, nothing made by the scene is left for the finalizer; what static fields still hold is never finalized
/// (the bridge disposes it on the main thread at shutdown).
///
/// A new town or a load frees a scene mid-run too, but the bridge is whole then and the finalizer releases those
/// wrappers normally; the race needs the shutdown.
/// </summary>
public static class CleanExit
{
    private static bool _leaving;

    /// <summary>True once a quit has started (the scene is being taken down).</summary>
    public static bool Leaving => _leaving;

    /// <summary>Frees the scene, drains the finalizers, then quits with the given exit code. A second call is ignored.</summary>
    public static async void Quit(SceneTree tree, int code = 0)
    {
        if (_leaving) return;
        _leaving = true;
        var root = tree.Root;
        for (int i = root.GetChildCount() - 1; i >= 0; i--) root.GetChild(i).QueueFree();
        // The delete queue is flushed at the end of this frame; each later frame lets the engine drop what the
        // finalized wrappers held, so the next round can find the objects that only those held.
        for (int i = 0; i < 3; i++)
        {
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        tree.Quit(code);
    }
}
