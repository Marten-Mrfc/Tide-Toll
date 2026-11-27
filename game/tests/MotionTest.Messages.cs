using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Godot;

namespace TideToll.Game.Tests;

/// <summary>
/// Night 3, round L2 (message-system.md section 5): the message system sampled every frame. For each kind (a banner,
/// notes in a queue, the line and its cross-fade, the choice, the goal's content): arrival and leave continuous and
/// eased with the book's bounds, Visible switched only at no opacity, the lengths of stay, one at a time per zone in
/// the queue's order, no allocation or CPU spike; "MOTION END msg-*" lines let motion-check.sh compare 30, 60 and 120
/// frames a second. Then the busiest moment (a choice open, a banner due, three achievements, a refusal): at most one
/// large message, one note and one line at any frame.
/// </summary>
public partial class MotionTest
{
    private struct MS
    {
        public float Big, Note, Line, Cross, Choice, Goal;
        public bool VBig, VNote, VLine, VChoice, VGoal, Refusing;
        public MessageQueue.Phase BigPhase, NotePhase;
        public string NoteText;
        public long Alloc;
        public double Cpu;
    }
    private readonly List<MS> _ms = new(4000);
    private long _msAllocPrev;
    private long _msAllocWorst;
    private double _msCpuWorst;

    private void SampleMsg()
    {
        long a = GC.GetAllocatedBytesForCurrentThread();
        var h = Main.Hud;
        _ms.Add(new MS
        {
            Big = h.LargeQueue.Showing ? h.LargeQueue.Alpha : 0f, VBig = h.BannerShown, BigPhase = h.LargeQueue.Now,
            Note = h.NoteQueue.Showing ? h.NoteQueue.Alpha : 0f, VNote = h.NoteLabelShown, NotePhase = h.NoteQueue.Now,
            NoteText = h.NoteShowing,
            Line = h.LineAlpha, Cross = h.LineCrossing ? h.LineCross : -1f, VLine = h.LineCardShown, Refusing = h.RefusalText.Length > 0,
            Choice = h.ChoiceAlpha, VChoice = h.ChoiceCardVisible, Goal = h.RequestAlpha, VGoal = h.RequestCardVisible,
            Alloc = _ms.Count == 0 ? 0 : a - _msAllocPrev, Cpu = _end.LastMs,
        });
        _msAllocPrev = a;
    }

    private bool MsgIdle()
    {
        var h = Main.Hud;
        // At rest means past the gap too (Phase.Idle): a case that began inside the last one's gap began later, by the
        // gap's real time left over, the fewer frames the rest took (its dismiss at a fixed time then fell elsewhere).
        return h.LargeQueue.Now == MessageQueue.Phase.Idle && h.LargeQueue.Waiting == 0 && h.NoteQueue.Now == MessageQueue.Phase.Idle && h.NoteQueue.Waiting == 0
            && (h.LineAlpha <= 0f || h.LineAlpha >= 1f) && !h.LineCrossing && h.RefusalText.Length == 0 && (h.ChoiceAlpha <= 0f || h.ChoiceAlpha >= 1f)
            && (h.RequestAlpha <= 0f || h.RequestAlpha >= 1f);
    }

    /// <summary>Samples every frame while <paramref name="step"/> acts, until every message zone is at rest.</summary>
    private async Task RecordMsg(Action<int> step, int maxFrames = 0)
    {
        if (maxFrames <= 0) maxFrames = 60 * _fps;
        _ms.Clear();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        await Frames(1);
        SampleMsg();
        int still = 0;
        for (int i = 0; i < maxFrames; i++)
        {
            step(i);
            await Frames(1);
            SampleMsg();
            if (MsgIdle() && i > 2) { if (++still >= 3) return; } else still = 0;
        }
        throw new Exception("the messages never came to rest");
    }

    /// <summary>One channel's motion: continuous (bounded step), eased ends, Visible only at no opacity.</summary>
    private float MsgChannel(string name, Func<MS, float> v, Func<MS, bool>? vis, float seconds)
    {
        float bound = 1.875f / seconds / _fps * 1.2f, peak = 0f;
        int moving = 0;
        for (int i = 1; i < _ms.Count; i++)
        {
            if (v(_ms[i]) < 0f || v(_ms[i - 1]) < 0f) continue; // not sampled (a cross-fade not running)
            float d = MathF.Abs(v(_ms[i]) - v(_ms[i - 1]));
            Check(d <= bound, $"{name}: jumps {d:0.000} in one frame at frame {i} (bound {bound:0.000})");
            peak = Math.Max(peak, d);
            if (d > 1e-5f) moving++;
            if (vis != null && vis(_ms[i]) != vis(_ms[i - 1]))
                Check(vis(_ms[i]) ? v(_ms[i]) <= bound && v(_ms[i - 1]) <= 0f : v(_ms[i]) <= 0f,
                    $"{name}: switched {(vis(_ms[i]) ? "on" : "off")} at opacity {v(_ms[i - 1]):0.000}/{v(_ms[i]):0.000} (frame {i})");
        }
        // Eased ends: every run of motion starts and stops slowly (60 fps and more: at 30 a 0.2 s fade is six frames).
        if (_fps >= 60)
        {
            for (int i = 1; i < _ms.Count; i++)
            {
                if (v(_ms[i]) < 0f || v(_ms[i - 1]) < 0f || i >= 2 && v(_ms[i - 2]) < 0f) continue;
                float d = MathF.Abs(v(_ms[i]) - v(_ms[i - 1])), before = MathF.Abs(v(_ms[i - 1]) - (i >= 2 ? v(_ms[i - 2]) : v(_ms[i - 1])));
                bool start = d > 1e-5f && before <= 1e-5f;
                if (start) Check(d <= peak * 0.25f, $"{name}: a run starts at {d / Math.Max(1e-6f, peak):0.00} of its top speed (frame {i})");
            }
        }
        for (int i = 1; i < _ms.Count; i++) { _msAllocWorst = Math.Max(_msAllocWorst, _ms[i].Alloc); _msCpuWorst = Math.Max(_msCpuWorst, _ms[i].Cpu); }
        for (int i = 1; i < _ms.Count; i++)
        {
            Check(_ms[i].Alloc <= AllocGate, $"{name}: frame {i} allocated {_ms[i].Alloc} B (gate {AllocGate}; first hints shown {Main.Hud.FirstHintsShown}, now {Main.Hud.FirstHintNow})");
            // --cpu-info (working switch, never used by ./check): printed, not fatal, as in the book's cases.
            if (_cpuInfo && _ms[i].Cpu > CpuGate) { _cpuOver++; GD.Print($"MOTION CPU over the gate: {name}: frame {i} took {_ms[i].Cpu:0.00} ms (gate {CpuGate})"); }
            else Check(_ms[i].Cpu <= CpuGate, $"{name}: frame {i} took {_ms[i].Cpu:0.00} ms of CPU (gate {CpuGate})");
        }
        GD.Print($"MOTION the message {name}: {moving} moving frames, largest step {peak:0.000} (bound {bound:0.000})");
        return peak;
    }

    /// <summary>Seconds a channel stood at full (each run), in order.</summary>
    private List<float> Holds(Func<MS, float> v)
    {
        var r = new List<float>();
        int run = 0;
        foreach (var s in _ms)
        {
            if (v(s) >= 1f) run++;
            else if (run > 0) { r.Add(run / (float)_fps); run = 0; }
        }
        if (run > 0) r.Add(run / (float)_fps);
        return r;
    }

    private void MsgEnd(string name, string state)
    {
        int first = -1, last = -1;
        for (int i = 1; i < _ms.Count; i++)
        {
            bool moving = _ms[i].Big != _ms[i - 1].Big || _ms[i].Note != _ms[i - 1].Note || _ms[i].Line != _ms[i - 1].Line || _ms[i].Cross != _ms[i - 1].Cross
                || _ms[i].Choice != _ms[i - 1].Choice || _ms[i].Goal != _ms[i - 1].Goal || _ms[i].Big >= 1f || _ms[i].Note >= 1f;
            if (moving) { if (first < 0) first = i; last = i; }
        }
        float secs = first < 0 ? 0f : (last - first + 1) / (float)_fps;
        GD.Print($"MOTION END {name} secs={secs:0.000} state={state}");
    }

    private async Task Messages()
    {
        var h = Main.Hud;
        // A still town: nothing new is raised by the simulation while the kinds are measured one by one.
        if (T.ChoiceOpen) { TestInput.Key(Godot.Key.Z); }
        Main.Driver.SetPaused(true);
        await Until(MsgIdle, 2400, "the messages at rest before their test");
        await Frames(5);

        // ---- a banner arriving, staying 3.2 s, leaving
        await RecordMsg(i => { if (i == 0) h.Large("Level 99", "A test banner.", TownHud.PrioLevel, TownHud.BannerStay, ""); });
        MsgChannel("banner", s => s.Big, s => s.VBig, TownHud.LargeSeconds);
        var hb = Holds(s => s.Big);
        Check(hb.Count == 1 && MathF.Abs(hb[0] - TownHud.BannerStay) <= 2.5f / _fps, $"banner: stays {string.Join(", ", hb)} s, not {TownHud.BannerStay}");
        MsgEnd("msg-banner", $"holds={hb.Count}");

        // ---- notes in a queue: three at once, highest priority first, one at a time, 0.15 s apart
        await RecordMsg(i =>
        {
            if (i != 0) return;
            h.Note("Test note one.", false, TownHud.NoteOther, "");
            h.Note("Test note three.", false, TownHud.NoteAchievement, "");
            h.Note("Test note two.", false, TownHud.NoteBuilding, "");
        });
        MsgChannel("notes", s => s.Note, s => s.VNote, TownHud.NoteInSeconds);
        var order = new List<string>();
        foreach (var s in _ms) if (s.NoteText.Length > 0 && (order.Count == 0 || order[^1] != s.NoteText)) order.Add(s.NoteText);
        string ord = string.Join(" / ", order);
        Check(ord == "Test note three. / Test note two. / Test note one.", $"notes: shown in the order {ord}");
        var hn = Holds(s => s.Note);
        Check(hn.Count == 3 && hn.TrueForAll(x => MathF.Abs(x - TownHud.NoteStay) <= 2.5f / _fps), $"notes: stay {string.Join(", ", hn)} s, not {TownHud.NoteStay}");
        int gapMin = 9999, gap = -1;
        for (int i = 1; i < _ms.Count; i++)
        {
            if (_ms[i].Note <= 0f && _ms[i - 1].Note > 0f) gap = 0;
            else if (gap >= 0 && _ms[i].Note <= 0f) gap++;
            else if (gap >= 0 && _ms[i].Note > 0f && i < _ms.Count - 1) { gapMin = Math.Min(gapMin, gap); gap = -1; }
        }
        Check(gapMin >= (int)(TownHud.MessageGap * _fps) - 1, $"notes: {gapMin} empty frames between two notes (at least {TownHud.MessageGap} s)");
        MsgEnd("msg-notes", $"order={order.Count}");

        // ---- a crowd: with four or more waiting each stays 2 s; beyond six waiting the lowest are dropped
        int noted = h.NoteQueue.ShownCount;
        for (int k = 0; k < 9; k++) h.Note($"Crowd note {k}.", false, TownHud.NoteOther, "");
        Check(h.NoteQueue.Waiting <= TownHud.NoteCap, $"crowd: {h.NoteQueue.Waiting} notes wait (at most {TownHud.NoteCap})");
        await RecordMsg(_ => { });
        var hc = Holds(s => s.Note);
        Check(hc.Count >= 3 && MathF.Abs(hc[0] - TownHud.NoteCrowdStay) <= 2.5f / _fps, $"crowd: the first note stays {(hc.Count > 0 ? hc[0] : 0):0.00} s, not {TownHud.NoteCrowdStay}");
        Check(h.NoteQueue.ShownCount - noted <= TownHud.NoteCap + 1, $"crowd: {h.NoteQueue.ShownCount - noted} of nine shown");
        MsgEnd("msg-crowd", $"shown={h.NoteQueue.ShownCount - noted}");

        // ---- a note clicked away: it leaves by its fade, the next follows
        await RecordMsg(i =>
        {
            if (i == 0) { h.Note("Click me away.", false, TownHud.NoteOther, ""); h.Note("Then me.", false, TownHud.NoteOther, ""); }
            if (i == _fps * 2 / 3) h.NoteQueue.Dismiss();
        });
        MsgChannel("note-clicked", s => s.Note, s => s.VNote, TownHud.NoteInSeconds);
        var hk = Holds(s => s.Note);
        Check(hk.Count == 2 && hk[0] < 1f, $"note-clicked: holds {string.Join(", ", hk)}");
        GD.Print($"MOTION the message note-clicked: holds {string.Join(", ", hk)} s");
        MsgEnd("msg-note-clicked", $"holds={hk.Count}");

        // ---- the line: a refusal (2.5 s), a second one cross-fades over it, then the card leaves
        await RecordMsg(i => { if (i == 0) h.Refuse("A test refusal."); if (i == _fps) h.Refuse("Another test refusal."); });
        MsgChannel("line", s => s.Line, s => s.VLine, TownHud.LineSeconds);
        MsgChannel("line-cross", s => s.Cross, null, TownHud.LineSeconds);
        var hl = Holds(s => s.Refusing ? 1f : 0f);
        float lineSecs = hl.Count > 0 ? hl[0] : 0f, lineWant = 1f + TownHud.RefusalSeconds;
        Check(hl.Count == 1 && MathF.Abs(lineSecs - lineWant) <= 2.5f / _fps, $"line: the refusals said for {lineSecs:0.00} s, not {lineWant:0.00}");
        MsgEnd("msg-line", $"holds={hl.Count}");

        // ---- the busiest moment: a choice open (level and request as the town has them), a banner due, three
        // achievements, a refusal. At most one large message, one note and one line at every frame.
        Main.Driver.SetPaused(false);
        var bot = new TideToll.Sim.Town.TownBot(TideToll.Sim.Town.BotKind.Payback, 1);
        Main.Driver.RunBotUntil(bot, t => t.ChoiceOpen, 20 * 60 * 60 * 10);
        Check(T.ChoiceOpen, "busiest: the bot played on to a choice");
        bool request = T.Request.Active;
        Main.Driver.SetPaused(true);
        int bigMax = 0, noteMax = 0, lineMax = 0, overlap = 0;
        await RecordMsg(i =>
        {
            if (i == 0)
            {
                h.Large("Level 98", "A banner due while the choice is open.", TownHud.PrioLevel, TownHud.BannerStay, "");
                for (int k = 0; k < 3; k++) h.Note($"Achievement: Busy {k}.", false, TownHud.NoteAchievement, "");
                h.Refuse("A refusal in the busiest moment.");
            }
            if (i == 2 * _fps) TestInput.Key(Godot.Key.Z);
        });
        foreach (var s in _ms)
        {
            int big = (s.Big > 0f ? 1 : 0) + (s.Choice > 0f ? 1 : 0);
            bigMax = Math.Max(bigMax, big); if (big > 1) overlap++;
            noteMax = Math.Max(noteMax, s.VNote ? 1 : 0);
            lineMax = Math.Max(lineMax, s.VLine ? 1 : 0);
        }
        Check(bigMax <= 1, $"busiest: {overlap} frames show two large messages");
        MsgChannel("choice", s => s.Choice, s => s.VChoice, TownHud.LargeSeconds);
        MsgChannel("busiest-banner", s => s.Big, s => s.VBig, TownHud.LargeSeconds);
        MsgChannel("busiest-goal", s => s.Goal, s => s.VGoal, TownHud.LargeSeconds);
        Check(h.BannerLog.Exists(l => l.StartsWith("Level 98")), "busiest: the banner was raised");
        GD.Print($"MOTION the busiest moment: request running {request}; at most {bigMax} large, {noteMax} note, {lineMax} line at a frame over {_ms.Count} frames");
        MsgEnd("msg-busiest", $"big={bigMax}");
        Check(Main.Audio.LeastLargeGap >= TownAudio.LargeGap - 1e-6, $"two large sounds {Main.Audio.LeastLargeGap:0.000} s apart (at least {TownAudio.LargeGap})");
        GD.Print($"MOTION large sounds: {Main.Audio.LargeSounds} played, least gap {Main.Audio.LeastLargeGap:0.000} s (bound {TownAudio.LargeGap})");
        GD.Print($"MOTION messages worst frame: alloc {_msAllocWorst} B, cpu {_msCpuWorst:0.00} ms (gates {AllocGate} B, {CpuGate} ms)");
        Main.Driver.SetPaused(false);
    }
}
