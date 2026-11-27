using System;
using System.Threading.Tasks;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game.Tests;

/// <summary>
/// n7 trades: pictures from the real game for the report (tools/trades-shots.sh: xvfb, Mobile renderer, at the window
/// size given). A town played by the payback bot to the mill (seed 2); every villager given a trade by the calls (the
/// rules arrive on the other branch); then: the left column with the Trades card, a line hovered (its members ringed),
/// a tooltip, the villager's card with its Trade caps, a cap's tooltip; a strip of a count being changed (the plus
/// clicked, frames every 0.05 s); a street at the play view with everyone a Helper ("before": the heads of before the
/// trades are gone, so hair only) and with the trades' heads ("after"). --size=150 sets the interface size.
/// </summary>
public partial class TradesShots : TownTestBase
{
    protected override string Marker => "TRADES SHOTS PASS";
    protected override Vector2I WindowSize => GetWindow().Size;
    private string _dir = "", _tag = "";

    protected override async Task Run()
    {
        _dir = ProjectSettings.GlobalizePath("res://../build/n7/trades");
        int size = 100;
        foreach (var a in OS.GetCmdlineUserArgs())
        {
            if (a.StartsWith("--out-dir=")) _dir = a.Substring(10);
            if (a.StartsWith("--size=")) size = int.Parse(a.Substring(7));
        }
        System.IO.Directory.CreateDirectory(_dir);
        var win = GetWindow().Size;
        _tag = $"{win.X}x{win.Y}" + (size != 100 ? $"-{size}" : "");
        if (size != 100) { Settings.GameSettings.Current?.SetInterfaceSize(size); await Frames(10); }
        var bot = new TownBot(BotKind.Payback, 2);
        Main.Driver.SetPaused(true);
        int streetLevel = 0;
        foreach (var a in OS.GetCmdlineUserArgs()) if (a.StartsWith("--street=")) streetLevel = int.Parse(a.Substring(9));
        if (streetLevel > 0)
        {
            // Fix round: the street alone, at a level of the town's own filling (no count set by hand).
            Main.Driver.RunBot(bot, t => t.Level >= streetLevel && t.TradesShown, 20 * 60 * 60);
            _tag += $"-level{T.Level}";
            Main.Driver.SetSpeed(1);
            Main.Driver.SetPaused(false);
            await Frames(60);
            await StreetShots();
            Notes.Add($"street level={T.Level} carriers={T.TradeInfo(Trade.Water).Members} reapers={T.TradeInfo(Trade.Grain).Members} millers={T.TradeInfo(Trade.Flour).Members} helpers={T.Helpers}");
            return;
        }
        Main.Driver.RunBot(bot, t => t.VillagerCount >= 14 && t.TradeOpen(Trade.Flour), 20 * 60 * 40);
        Main.Driver.Advance(20);
        var t = T;
        int n = t.VillagerCount;
        // Fix round: the town as the game is: the town fills the trades by itself and no count is set by hand (only the
        // count strip sets one). The earlier first hints are marked seen (a level 13 town is past its first minute), so
        // the line shows the trades' own hint when the card first shows.
        Main.Driver.Advance(10);
        for (int h = 0; h < 4; h++) Settings.GameSettings.Current?.SetHintSeen(h);
        Main.Driver.SetSpeed(1);
        Main.Driver.SetPaused(false);
        await Frames(30);
        var hud = Main.Hud; var vil = Main.Villagers;
        Main.Driver.SetPaused(true);
        Park();
        await Frames(16);
        Check(hud.TradesCardAlpha > 0.99f, $"the Trades card shows ({hud.TradesCardAlpha:0.00})");
        for (int k = 0; k < 600 && hud.FirstHintNow != 4; k++) await Frames(1);
        await Frames(16);
        if (hud.FirstHintNow == 4) Save("trades-hint-whole", Shot());
        else GD.Print($"TRADES SHOTS: the trades' hint did not show (now {hud.FirstHintNow}, shown {hud.FirstHintsShown})");
        Save("trades-card-whole", Shot());
        Column("trades-card");
        // A line hovered: its band and its members ringed; then its tooltip.
        var line = hud.TradeLineRect(Trade.Grain);
        Point(line.Position + new Vector2(line.Size.X * 0.3f, line.Size.Y / 2));
        await Frames(8);
        Save("trades-hover-whole", Shot());
        Column("trades-hover");
        await Frames(26);
        Column("trades-tip", 2);
        // The plus's states: hovered.
        Point(hud.TradePlusHit(Trade.Water).GetCenter());
        await Frames(26);
        Column("trades-plus-hover", 2);
        // The villager's card with the caps; a cap's tooltip.
        int who = Clearest(vil);
        Check(who >= 0, "a villager to click");
        Point(vil.ScreenOf(Main.Rig, who));
        await Frames(2);
        await Click(GetViewport().GetFinalTransform() * vil.ScreenOf(Main.Rig, who));
        Park();
        await Frames(30);
        Check(hud.SelectedVillager == who, $"selected (got {hud.SelectedVillager})");
        Column("trades-villager");
        var likes = t.Villager(who).Likes;
        Point(hud.TradeCapRect(likes == Trade.None ? Trade.Grain : likes).GetCenter());
        await Frames(26);
        Column("trades-cap-tip", 2);
        // The crossfade, mid way.
        hud.Select(-1);
        Park();
        await Frames(6);
        Column("trades-crossfade");
        await Frames(30);
        // A strip of a count being changed: the plus of the carriers clicked, frames every 0.05 s.
        var strip = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
        var plus = hud.TradePlusHit(Trade.Water).GetCenter();
        var r = GetViewport().GetFinalTransform() * hud.TradesCardRect.Grow(UiKit.U(hud.UiScale, 8));
        Main.Driver.SetPaused(false);
        Point(plus);
        await Click(GetViewport().GetFinalTransform() * plus);
        Image[] frames = new Image[8];
        for (int k = 0; k < frames.Length; k++)
        {
            frames[k] = Shot().GetRegion(new Rect2I((Vector2I)r.Position, (Vector2I)r.Size));
            await Frames(3);
        }
        strip = Image.CreateEmpty(frames[0].GetWidth() * 4, frames[0].GetHeight() * 2, false, frames[0].GetFormat());
        for (int k = 0; k < frames.Length; k++) strip.BlitRect(frames[k], new Rect2I(Vector2I.Zero, frames[k].GetSize()), new Vector2I((k % 4) * frames[0].GetWidth(), (k / 4) * frames[0].GetHeight()));
        Save("trades-count-strip", strip);
        await StreetShots();
        Notes.Add($"dir={_dir} tag={_tag} villagers={n} who={who}");
    }

    /// <summary>A street at the play view: everyone a Helper, then the trades' heads, then the trial (larger kerchief and
    /// cap), the same moment with the town held still.</summary>
    private async Task StreetShots()
    {
        // A street at the play view: everyone a Helper, then the trades' heads (the same moment, the town held still).
        Main.Driver.SetPaused(true);
        Park();
        var vil = Main.Villagers; var t = T; int n = t.VillagerCount;
        var at = Busiest(vil);
        Main.Rig.UserMoved = true;
        Main.Rig.LookAt(at, 46f, instant: true);
        await Frames(10);
        var keep = new Trade[n];
        for (int i = 0; i < n; i++) keep[i] = t.Villager(i).Trade;
        for (int i = 0; i < n; i++) t.SetVillagerTrade(i, Trade.None);
        await Frames(26);
        Crop("trades-street-before", Main.Rig.LocalToScreen(at), 960, 540);
        Crop("trades-street-before-close", Main.Rig.LocalToScreen(at), 480, 270, 2);
        for (int i = 0; i < n; i++) t.SetVillagerTrade(i, keep[i]);
        await Frames(26);
        Crop("trades-street-after", Main.Rig.LocalToScreen(at), 960, 540);
        Crop("trades-street-after-close", Main.Rig.LocalToScreen(at), 480, 270, 2);
        // The trial: the kerchief and the cap 15 percent larger, the same moment.
        TownVillagers.WearScale = 1.15f;
        await Frames(3);
        Crop("trades-street-larger", Main.Rig.LocalToScreen(at), 960, 540);
        Crop("trades-street-larger-close", Main.Rig.LocalToScreen(at), 480, 270, 2);
        TownVillagers.WearScale = 1f;
    }

    private void Park() { Point(new Vector2(GetViewport().GetVisibleRect().Size.X * 0.6f, GetViewport().GetVisibleRect().Size.Y * 0.8f)); }

    /// <summary>The pointer to a canvas point: the system pointer in window pixels (the interface size scales the canvas).</summary>
    private void Point(Vector2 p) { Input.WarpMouse(GetViewport().GetFinalTransform() * p); TestInput.Motion(GetViewport().GetFinalTransform() * p); }

    private Image Shot() => GetViewport().GetTexture().GetImage();

    /// <summary>The left column from the ledger's foot to below the cards, with room for a tooltip at the right.</summary>
    private void Column(string name, int scale = 1)
    {
        var hud = Main.Hud;
        var z = new HudZones(GetViewport().GetVisibleRect().Size, hud.UiScale);
        var p = z.Person;
        var r = GetViewport().GetFinalTransform() * new Rect2(0, p.Position.Y - z.P(120), p.End.X + z.P(340), p.Size.Y + z.P(200));
        var img = Shot();
        var ri = new Rect2I((Vector2I)r.Position, (Vector2I)r.Size).Intersection(new Rect2I(Vector2I.Zero, img.GetSize()));
        var c = img.GetRegion(ri);
        if (scale > 1) c.Resize(c.GetWidth() * scale, c.GetHeight() * scale, Image.Interpolation.Nearest);
        Save(name, c);
    }

    private void Crop(string name, Vector2 centre, int w, int h, int scale = 1)
    {
        var img = Shot();
        var sz = img.GetSize();
        centre = GetViewport().GetFinalTransform() * centre;
        w = Math.Min(w, sz.X); h = Math.Min(h, sz.Y);
        int x = Math.Clamp((int)centre.X - w / 2, 0, sz.X - w), y = Math.Clamp((int)centre.Y - h / 2, 0, sz.Y - h);
        var c = img.GetRegion(new Rect2I(x, y, w, h));
        if (scale > 1) c.Resize(w * scale, h * scale, Image.Interpolation.Nearest);
        Save(name, c);
    }

    private void Save(string name, Image img) => img.SavePng(System.IO.Path.Combine(_dir, $"{name}-{_tag}.png"));

    private int Clearest(TownVillagers vil)
    {
        int n = Math.Min(T.VillagerCount, TownVillagers.Capacity), best = -1; float bestScore = 0;
        var vr = GetViewport().GetVisibleRect();
        var c = vr.GetCenter();
        for (int i = 0; i < n; i++)
        {
            var p = vil.ScreenOf(Main.Rig, i);
            if (!vr.Grow(-vr.Size.Y * 0.18f).HasPoint(p) || Main.Hud.OverInterface(p)) continue;
            float clear = float.MaxValue;
            for (int j = 0; j < n; j++) if (j != i) clear = Math.Min(clear, vil.ScreenOf(Main.Rig, j).DistanceTo(p));
            float score = Math.Min(clear, 60f) - p.DistanceTo(c) * 0.02f;
            if (score > bestScore) { bestScore = score; best = i; }
        }
        return best;
    }

    /// <summary>The map point with the most villagers within three cells (a street).</summary>
    private Vector3 Busiest(TownVillagers vil)
    {
        int n = Math.Min(T.VillagerCount, TownVillagers.Capacity), most = -1;
        Vector3 at = Vector3.Zero;
        for (int i = 0; i < n; i++)
        {
            var a = vil.FeetLocal(i); int c = 0;
            for (int j = 0; j < n; j++) if (vil.FeetLocal(j).DistanceTo(a) < 3 * TownGround.Cell) c++;
            if (c > most) { most = c; at = a; }
        }
        return at;
    }
}
