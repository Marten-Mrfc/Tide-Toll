using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game.Tests;

/// <summary>
/// game-people (n7, villagers as people: docs/plans/n7/people-build-sheet.md, "Limits and judging"). A real town: the
/// payback bot plays to the mill and the market with a crowd of villagers, then the town runs frame by frame at 1x
/// (run with --fixed-fps 30, 60 and 120 by tools/people-check.sh):
/// <list type="bullet">
/// <item>the pick: pointing at a villager's drawn figure picks that villager, also in a crowd (only a figure drawn on
/// the same spot may win instead); real pointer motion hovers it; the hover does not flicker between two neighbours;</item>
/// <item>no pick in Pave and Extend, over the interface or while paused; a click on the interface never picks;</item>
/// <item>a click selects and docks the card under the ledger (name, home, leaning, now, counts), the ring stays under
/// the selected villager, the cross and a click on the empty map close it;</item>
/// <item>bodies: two walkers going opposite ways on a road are on opposite sides; a follower is never closer than a set
/// distance to the one ahead for more than a second; no two people standing about share a spot for more than a second;</item>
/// <item>nothing changes in one frame (rings, tag, card, figures); nothing allocated per frame by the villagers or the
/// pick; the whole frame inside game-perf's 1x gate.</item>
/// </list>
/// </summary>
public partial class PeopleTest : TownTestBase
{
    protected override string Marker => "PEOPLE PASS";
    protected override Vector2I WindowSize => new(1920, 1080);
    private int _fps = 60;
    private long _frames, _passPairs, _sideFails, _followFrames, _spotFrames;
    private readonly float[] _closeFor = new float[TownVillagers.Capacity];
    private readonly int[] _closeTo = new int[TownVillagers.Capacity];
    private readonly int[] _leg = new int[TownVillagers.Capacity];
    private readonly float[] _legFor = new float[TownVillagers.Capacity];
    /// <summary>A walker is held to its side once it has walked its road and way this long (the side change is eased).</summary>
    public const float SettleSeconds = 0.6f;
    private float _spotClose, _worstFollowHeld, _worstSpotHeld, _minFollow = 99, _minSpot = 99, _worstAlphaStep;
    private float _hoverA0, _selA0, _cardA0, _tagA0;

    private partial class AllocProbe : Node
    {
        public long Mark, Sum, Frames;
        public AllocProbe? Begin;
        public override void _Process(double delta)
        {
            long now = GC.GetAllocatedBytesForCurrentThread();
            if (Begin == null) Mark = now;
            else { Sum += now - Begin.Mark; Frames++; }
        }
    }

    protected override async Task Run()
    {
        foreach (var a in OS.GetCmdlineUserArgs()) if (a.StartsWith("--fps=")) _fps = int.Parse(a.Substring(6));
        var bot = new TownBot(BotKind.Payback, 2);
        Main.Driver.SetPaused(true);
        Main.Driver.RunBot(bot, t => t.VillagerCount >= 14 && MillOpen(t), 20 * 60 * 40);
        Check(T.VillagerCount >= 14, $"a crowd of villagers ({T.VillagerCount})");
        Main.Driver.Advance(20);
        Drain();
        var vil = Main.Villagers;
        var hud = Main.Hud;
        Main.Driver.SetSpeed(1);
        Main.Driver.SetPaused(false);
        await Frames(_fps * 2);

        // ---- The pick, on a still drawing: every villager on screen, at its figure's middle.
        Main.Driver.SetPaused(true);
        await Frames(3);
        int tried = 0, exact = 0, shared = 0;
        float r = hud.PickRadius;
        for (int i = 0; i < Math.Min(T.VillagerCount, TownVillagers.Capacity); i++)
        {
            var p = vil.ScreenOf(Main.Rig, i);
            if (!OnMap(p)) continue;
            tried++;
            int got = vil.Pick(Main.Rig, p, r);
            if (got == i) { exact++; continue; }
            // In a crowd only a figure drawn on the same spot (within the tie) may win instead.
            Check(got >= 0, $"pointing at villager {i} picked nobody");
            float di = Dist(vil, i, p), dg = Dist(vil, got, p);
            Check(dg <= di + TownVillagers.PickTiePx + 0.01f, $"pointing at villager {i} picked {got} ({dg:0.0} px against {di:0.0} px)");
            shared++;
        }
        Check(tried >= 8 && exact >= tried * 0.75, $"pointing picks the villager pointed at ({exact} of {tried}; {shared} drawn on the same spot as another)");
        // Pick allocates nothing.
        long b0 = GC.GetAllocatedBytesForCurrentThread();
        for (int k = 0; k < 200; k++) vil.Pick(Main.Rig, new Vector2(400 + k, 500), r, k % 7);
        long pickBytes = GC.GetAllocatedBytesForCurrentThread() - b0;
        Check(pickBytes == 0, $"the pick allocates nothing ({pickBytes} B in 200 picks)");

        // ---- Real input: hover, the tag after the delay, a click selects and docks the card; the cross closes it.
        int who = OnScreenVillager(vil);
        Check(who >= 0, "a villager on screen, clear of the interface");
        TestInput.Motion(vil.ScreenOf(Main.Rig, who));
        await Frames(2);
        Check(hud.HoverVillager == who, $"the pointer on villager {who} hovers it (hover {hud.HoverVillager})");
        Check(hud.PersonTagText == "", "no name before the tooltip's delay");
        await Frames((int)(_fps * 0.6f));
        // n7 trades: the tag names the trade too ("Joss, reaper"; was the name alone).
        Check(hud.PersonTagText == T.VillagerName(who) + ", " + TownHud.TradeOne[(int)T.Villager(who).Trade], $"the name tag after the delay ('{hud.PersonTagText}', want '{T.VillagerName(who)}, {TownHud.TradeOne[(int)T.Villager(who).Trade]}')");
        Check(vil.HoverRingAlpha > 0.99f, $"the hover ring under the feet ({vil.HoverRingAlpha:0.00})");
        await Click(vil.ScreenOf(Main.Rig, who));
        await Frames(_fps / 2);
        Check(hud.SelectedVillager == who && hud.PersonCardVisible, $"a click selects villager {who} and docks its card (selected {hud.SelectedVillager})");
        var z = new HudZones(GetViewport().GetVisibleRect().Size, hud.UiScale);
        var cr = hud.PersonCardRect;
        Check(Near(new Rect2(cr.Position, cr.Size.X, z.Person.Size.Y), z.Person) && cr.Size.Y <= z.Person.Size.Y + 1, $"the card in its zone under the ledger ({cr} vs {z.Person})");
        Check(cr.End.Y - hud.PersonLastLineBottom <= UiKit.U(hud.UiScale, 16), $"the card hugs its lines (foot {cr.End.Y - hud.PersonLastLineBottom:0} px of paper)");
        string card = hud.PersonCardText;
        // n7 trades: the title is "Joss · Reaper"; line 2 is the like and the home ("Likes reaping · by the well lane", or
        // "Lives by ..." while the like's trade is not open); the leaning line is gone (was: "A good hand" while it counted).
        Check(card.StartsWith(T.VillagerName(who) + " · ") && (card.Contains("|Likes ") || card.Contains("|Lives ")) && !card.Contains("A good hand") && card.Contains("trip"), $"the card says who it is ({card})");
        Check(vil.SelectedRingAlpha > 0.99f, $"the selected villager keeps its ring ({vil.SelectedRingAlpha:0.00})");
        Check(hud.OverInterface(hud.PersonCardRect.GetCenter()), "the card stops the map (OverInterface)");
        await Click(hud.PersonClose.GetGlobalRect().GetCenter());
        await Frames(_fps / 2);
        Check(hud.SelectedVillager < 0 && !hud.PersonCardVisible, "the cross closes the card");
        // A click on the empty map closes it too.
        await Click(vil.ScreenOf(Main.Rig, who));
        await Frames(3);
        Check(hud.SelectedVillager == who, "selected again");
        var empty = EmptyMapPoint(vil, r);
        await Click(empty);
        await Frames(3);
        Check(hud.SelectedVillager < 0, $"a click on the empty map at {empty} closes the card");

        // ---- Off over the interface, in the modes and while paused.
        TestInput.Motion(hud.LedgerRect.GetCenter());
        await Frames(2);
        Check(hud.HoverVillager < 0, "no hover over the ledger");
        Check(hud.PersonTarget(hud.LedgerRect.GetCenter()) < 0, "no pick over the ledger");
        await Click(hud.LedgerRect.GetCenter());
        Check(hud.SelectedVillager < 0, "a click on the interface never picks");
        hud.SetExtendMode(true);
        await Frames(2);
        if (hud.ExtendMode)
        {
            TestInput.Motion(vil.ScreenOf(Main.Rig, who));
            await Frames(2);
            Check(hud.HoverVillager < 0 && hud.PersonTarget(vil.ScreenOf(Main.Rig, who)) < 0, "no pick in Extend mode");
            await Click(vil.ScreenOf(Main.Rig, who));
            Check(hud.SelectedVillager < 0, "a click in Extend mode never selects a villager");
            hud.SetExtendMode(false);
        }
        T.ShopItems(_items);
        hud.SetPaveMode(true);
        await Frames(2);
        bool paveSeen = hud.PaveMode;
        if (hud.PaveMode)
        {
            TestInput.Motion(vil.ScreenOf(Main.Rig, who));
            await Frames(2);
            Check(hud.HoverVillager < 0 && hud.PersonTarget(vil.ScreenOf(Main.Rig, who)) < 0, "no pick in Pave mode");
            hud.SetPaveMode(false);
        }
        Main.Menu!.Pause();
        await Frames(_fps * 2);
        Check(hud.PersonTarget(vil.ScreenOf(Main.Rig, who)) < 0, "no pick while paused");
        Main.Menu.Resume();
        await Frames(_fps * 3);

        // ---- The town runs: bodies, the hover held over a crowd, nothing in one frame, allocation.
        Main.Driver.SetPaused(false);
        var parent = vil.GetParent();
        var begin = new AllocProbe { Name = "PeopleAllocBegin" };
        var end = new AllocProbe { Name = "PeopleAllocEnd" };
        end.Begin = begin;
        parent.AddChild(begin); parent.MoveChild(begin, vil.GetIndex());
        parent.AddChild(end); parent.MoveChild(end, vil.GetIndex() + 1);
        hud.Select(OnScreenVillager(vil));
        await Frames(10);
        begin.Sum = end.Sum = 0; end.Frames = 0;
        long g0 = GC.GetAllocatedBytesForCurrentThread(); long f0 = _frames;
        int flicker = 0, lastH = -1, prevH = -1; float sinceChange = 99;
        double until = T.Time + 60;
        // The pointer rests on the busiest spot of the map (a crowd), then follows a walker.
        var rest = BusyPoint(vil);
        TestInput.Motion(rest);
        for (int f = 0; f < 200000 && T.Time < until; f++)
        {
            await Frames(1);
            Sample();
            int h = hud.HoverVillager;
            sinceChange += 1f / _fps;
            if (h != lastH)
            {
                // A flicker: back to the one hovered just before, within a third of a second.
                if (h >= 0 && h == prevH && sinceChange < 0.33f) flicker++;
                prevH = lastH; lastH = h; sinceChange = 0;
            }
        }
        long perFrame = (GC.GetAllocatedBytesForCurrentThread() - g0) / Math.Max(1, _frames - f0);
        long villagerBytes = end.Sum / Math.Max(1, end.Frames);
        Check(flicker == 0, $"the hover does not flicker between neighbours ({flicker} times)");
        Check(_passPairs > 10, $"enough passing walkers seen ({_passPairs} pairs)");
        Check(_sideFails == 0, $"two walkers going opposite ways on a road are on opposite sides ({_sideFails} of {_passPairs} pairs not)");
        Check(_worstFollowHeld <= 1f, $"a follower is never closer than {0.5f * vil.FollowGapMetres:0.0} m to the one ahead for more than a second (longest {_worstFollowHeld:0.00} s)");
        Check(_worstSpotHeld <= 1f, $"no two standing people share a spot for more than a second (longest {_worstSpotHeld:0.00} s)");
        Check(villagerBytes <= 16, $"the villagers allocate nothing per frame ({villagerBytes} B)");
        Check(perFrame < Perf.AllocGate1x, $"a frame allocates under game-perf's 1x gate ({perFrame} B, gate {Perf.AllocGate1x})");
        Check(_worstAlphaStep <= 0.5f, $"rings, tag and card fade, never switch in one frame (largest step {_worstAlphaStep:0.00})");
        Notes.Add($"fps={_fps} villagers={T.VillagerCount} picked={exact}/{tried} shared={shared} passpairs={_passPairs} sidefails={_sideFails} follow={_minFollow:0.00}m/{_worstFollowHeld:0.00}s " +
                  $"spot={_minSpot:0.00}m/{_worstSpotHeld:0.00}s flicker={flicker} bytes={perFrame} villagerbytes={villagerBytes} pickbytes={pickBytes} pave={(paveSeen ? "on" : "none")} r={r:0}px");
    }

    private readonly List<ShopItem> _items = new();

    private void Sample()
    {
        _frames++;
        var vil = Main.Villagers;
        var hud = Main.Hud;
        var t = T;
        float dt = 1f / _fps;
        // Nothing in one frame: the rings, the tag and the card change by at most half their range in one frame.
        float tagA = hud.PersonTagText.Length > 0 ? 1f : 0f;
        if (_frames > 1) _worstAlphaStep = Math.Max(_worstAlphaStep, Math.Max(Math.Abs(vil.HoverRingAlpha - _hoverA0), Math.Abs(vil.SelectedRingAlpha - _selA0)));
        _hoverA0 = vil.HoverRingAlpha; _selA0 = vil.SelectedRingAlpha; _tagA0 = tagA;
        if (Main.Driver.Speed <= 1 && !(vil.MaxStepCells < 0.5f)) Check(false, $"a villager jumped {vil.MaxStepCells:0.00} cells in one frame");
        int n = Math.Min(t.VillagerCount, TownVillagers.Capacity);
        var roads = t.Plan.Roads;
        // Each walker's leg (road and way) and how long it has walked it: one who has just turned onto a road, or turned
        // round on it, crosses to its side over a moment (eased), as a person would.
        for (int i = 0; i < n; i++)
        {
            int ri = vil.RoadOf(i), key = ri < 0 ? -1 : ri * 2 + (t.Villager(i).Dir > 0 ? 1 : 0);
            if (key != _leg[i]) { _leg[i] = key; _legFor[i] = 0; } else _legFor[i] += dt;
        }
        // Lanes: walkers going opposite ways on one road, both clear of its ends and settled on it, are on opposite sides.
        for (int i = 0; i < n; i++)
        {
            int ri = vil.RoadOf(i);
            if (ri < 0 || _legFor[i] < SettleSeconds) continue;
            var vi = t.Villager(i);
            var rd = roads[ri];
            if (vi.Dist < 1.5 || vi.Dist > rd.Length - 1.5) continue;
            float si = SideOf(vil, rd, i, vi.Dist);
            for (int j = i + 1; j < n; j++)
            {
                if (vil.RoadOf(j) != ri) continue;
                var vj = t.Villager(j);
                if (vj.Dir == vi.Dir || vj.Dist < 1.5 || vj.Dist > rd.Length - 1.5 || _legFor[j] < SettleSeconds) continue;
                if (Math.Abs(vj.Dist - vi.Dist) > 3) continue; // passing each other now
                _passPairs++;
                float sj = SideOf(vil, rd, j, vj.Dist);
                if (si * sj >= 0) { _sideFails++; if (_sideFails < 4) GD.Print($"PEOPLE side: {i} ({si:0.00}) and {j} ({sj:0.00}) on road {ri}"); }
            }
            // Following distance, held per follower while it follows the same leader.
            int l = vil.LeaderOf(i);
            float limit = 0.5f * vil.FollowGapMetres;
            if (l >= 0)
            {
                float d = vil.DrawnAt(i).DistanceTo(vil.DrawnAt(l)) * TownGround.Cell;
                _minFollow = Math.Min(_minFollow, d);
                if (d < limit && _closeTo[i] == l) _closeFor[i] += dt;
                else { _closeFor[i] = d < limit ? dt : 0; _closeTo[i] = l; }
                _worstFollowHeld = Math.Max(_worstFollowHeld, _closeFor[i]);
            }
            else _closeFor[i] = 0;
        }
        _minSpot = Math.Min(_minSpot, vil.MinSpotGap);
        _spotClose = vil.MinSpotGap < 0.25f ? _spotClose + dt : 0;
        _worstSpotHeld = Math.Max(_worstSpotHeld, _spotClose);
    }

    /// <summary>Across-the-road offset of a walker's drawn feet from the road's drawn curve (nearest point), positive
    /// right of A to B (cells). The drawn feet may lag the simulation along the road (the following distance), so the
    /// side is taken where the figure is drawn, not where the simulation has it.</summary>
    private static float SideOf(TownVillagers vil, Road rd, int i, double dist)
    {
        var p = vil.DrawnAt(i);
        float best = float.MaxValue, side = 0;
        for (int k = 0; k + 1 < rd.Px.Length; k++)
        {
            var a = new Vector2((float)rd.Px[k] + 0.5f, (float)rd.Py[k] + 0.5f);
            var e = new Vector2((float)rd.Px[k + 1] + 0.5f, (float)rd.Py[k + 1] + 0.5f) - a;
            float l2 = e.LengthSquared();
            float u = l2 > 1e-8f ? Math.Clamp((p - a).Dot(e) / l2, 0f, 1f) : 0f;
            var d = p - (a + e * u);
            if (d.LengthSquared() < best) { best = d.LengthSquared(); side = (e.X * d.Y - e.Y * d.X) >= 0 ? d.Length() : -d.Length(); }
        }
        return side;
    }

    private float Dist(TownVillagers vil, int i, Vector2 p)
    {
        var foot = vil.FeetLocal(i);
        return TownVillagers.SegmentDistance(p, Main.Rig.LocalToScreen(foot), Main.Rig.LocalToScreen(foot + new Vector3(0, vil.FigureHeight, 0)));
    }

    private bool OnMap(Vector2 p)
    {
        var vp = GetViewport().GetVisibleRect();
        return vp.Grow(-40).HasPoint(p) && !Main.Hud.OverInterface(p) && !Main.Hud.OverInterface(p + new Vector2(0, 30));
    }

    /// <summary>A villager on screen whose figure is clear of every other figure by twice the pick radius.</summary>
    private int OnScreenVillager(TownVillagers vil)
    {
        float r = Main.Hud.PickRadius;
        int n = Math.Min(T.VillagerCount, TownVillagers.Capacity), best = -1; float bestClear = 0;
        for (int i = 0; i < n; i++)
        {
            var p = vil.ScreenOf(Main.Rig, i);
            if (!OnMap(p)) continue;
            float clear = float.MaxValue;
            for (int j = 0; j < n; j++) if (j != i) clear = Math.Min(clear, Dist(vil, j, p));
            if (clear > bestClear) { bestClear = clear; best = i; }
        }
        return bestClear >= 2 * r ? best : -1;
    }

    private Vector2 EmptyMapPoint(TownVillagers vil, float r)
    {
        var vp = GetViewport().GetVisibleRect();
        for (int gy = 3; gy < 12; gy++)
            for (int gx = 3; gx < 14; gx++)
            {
                var p = new Vector2(vp.Size.X * gx / 16f, vp.Size.Y * gy / 14f);
                if (!OnMap(p)) continue;
                bool clear = true;
                for (int i = 0; i < Math.Min(T.VillagerCount, TownVillagers.Capacity) && clear; i++) if (Dist(vil, i, p) < 3 * r) clear = false;
                if (clear) return p;
            }
        return new Vector2(vp.Size.X / 2, vp.Size.Y / 2);
    }

    /// <summary>The screen point with the most figures within three pick radii (a crowd).</summary>
    private Vector2 BusyPoint(TownVillagers vil)
    {
        float r = Main.Hud.PickRadius;
        int n = Math.Min(T.VillagerCount, TownVillagers.Capacity), most = -1; Vector2 at = new(960, 540);
        for (int i = 0; i < n; i++)
        {
            var p = vil.ScreenOf(Main.Rig, i);
            if (!OnMap(p)) continue;
            int c = 0;
            for (int j = 0; j < n; j++) if (Dist(vil, j, p) < 3 * r) c++;
            if (c > most) { most = c; at = p; }
        }
        return at;
    }

    private static bool Near(Rect2 a, Rect2 b) => a.Position.DistanceTo(b.Position) <= 1.5f && (a.Size - b.Size).Length() <= 1.5f;

    private static bool MillOpen(Town t)
    {
        for (int i = 0; i < t.WorkplaceCount; i++) { var w = t.Workplace(i); if (w.Open && w.Kind == PlotKind.Mill) return true; }
        return false;
    }

    private readonly List<TownEvent> _scratch = new(4096);
    private void Drain() { _scratch.Clear(); T.DrainEvents(_scratch); _scratch.Clear(); }
}
