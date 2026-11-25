using System;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// Rules v2 on the left of the screen: the hint (one line of advice at the foot of the ledger, with a soft pulsing
/// ring on the road or plot it is about) and the request card under the ledger (who asks, for how much of which good,
/// how much is delivered, the time left as a bar, the reward). A met request flashes the card green with a strong
/// note; a missed one fades it with a plain note. A hint is held at least <see cref="HintHold"/> seconds so it never
/// flickers while a queue or a crowd comes and goes.
/// </summary>
public partial class TownHud
{
    public const float HintHold = 4f;
    private Label _hint = null!;
    private PanelContainer _req = null!;
    private Label _reqWho = null!, _reqAsk = null!, _reqDone = null!, _reqReward = null!;
    private Control _reqBarBack = null!;
    private ColorRect _reqBar = null!;
    private StyleBoxFlat _reqBox = null!;
    private MeshInstance3D? _marker;
    private float _hintAge, _reqFlash;
    private string _hintShown = "";
    private int _hintRoad = -1, _hintPlot = -1;
    private int _reqSig = int.MinValue;

    /// <summary>The hint line as shown ("" none), what its marker points at, and the request card (tests).</summary>
    public string HintText => _hintShown;
    public int HintMarkerRoad => _hintRoad;
    public int HintMarkerPlot => _hintPlot;
    public bool HintMarkerVisible => _marker != null && _marker.Visible;
    public bool RequestCardVisible => _req.Visible;
    public string RequestCardText => _req.Visible ? $"{_reqWho.Text} | {_reqAsk.Text} | {_reqDone.Text} | {_reqReward.Text}" : "";
    public float RequestTimeBar => _reqBar.Size.X / Math.Max(1f, _reqBarBack.Size.X - 2f);
    public Vector3 HintMarkerLocal => _marker?.Position ?? Vector3.Zero;

    private void BuildHintLine(VBoxContainer ledgerCol)
    {
        _hint = UiKit.Text("", _s, 16, UiKit.UiFont, UiKit.Ink);
        _hint.Name = "Hint";
        _hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _hint.CustomMinimumSize = new Vector2(UiKit.Px(_s, 310), 0);
        _hint.Visible = false;
        ledgerCol.AddChild(_hint);
        _hintShown = "";
    }

    private void BuildRequestCard()
    {
        _req = new PanelContainer { Name = "Request", MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        _reqBox = UiKit.Card(_s, new Color(UiKit.Paper, 0.96f), 1.5f);
        _reqBox.SetContentMarginAll(UiKit.Px(_s, 9));
        _req.AddThemeStyleboxOverride("panel", _reqBox);
        _req.CustomMinimumSize = new Vector2(UiKit.Px(_s, 330), 0);
        _root.AddChild(_req);
        var col = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", UiKit.Px(_s, 2));
        _req.AddChild(col);
        _reqWho = UiKit.Text("", _s, 18, UiKit.Serif);
        _reqAsk = UiKit.Text("", _s, 17, UiKit.UiBold);
        _reqDone = UiKit.Text("", _s, 15, UiKit.UiFont);
        _reqReward = UiKit.Text("", _s, 15, UiKit.UiFont, UiKit.FadedInk);
        col.AddChild(_reqWho);
        col.AddChild(_reqAsk);
        col.AddChild(_reqDone);
        _reqBarBack = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(0, UiKit.Px(_s, 8)) };
        var back = UiKit.Card(_s, UiKit.PaperDark, 1f);
        back.SetContentMarginAll(0);
        _reqBarBack.AddThemeStyleboxOverride("panel", back);
        col.AddChild(_reqBarBack);
        _reqBar = new ColorRect { Color = UiKit.Focus, MouseFilter = Control.MouseFilterEnum.Ignore };
        _reqBarBack.AddChild(_reqBar);
        col.AddChild(_reqReward);
        _reqSig = int.MinValue;
        if (_marker == null || !IsInstanceValid(_marker))
        {
            var ring = new TorusMesh { InnerRadius = 1.6f, OuterRadius = 2.2f, Rings = 24, RingSegments = 8 };
            var mat = Palette.Unlit(new Color(UiKit.Warn, 0.6f), transparent: true);
            mat.NoDepthTest = true;
            _marker = new MeshInstance3D { Name = "HintMarker", Mesh = ring, MaterialOverride = mat, Visible = false,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            _main.MapOrigin.AddChild(_marker);
        }
    }

    public static string GoodName(Good g) => g switch { Good.Water => "Water", Good.Grain => "Grain", Good.Flour => "Flour", _ => "goods" };

    private void RequestEvent(in TownEvent e)
    {
        string who = e.Index >= 0 && e.Index < Town.RequestWho.Length ? Town.RequestWho[e.Index] : "Someone";
        switch (e.Kind)
        {
            case TownEventKind.RequestAsked: Note($"{who} asks for {e.Amount} {GoodName(e.Good)}."); break;
            case TownEventKind.RequestMet:
                Note($"{who} is pleased: +{Money(e.Coin)} coin.", true);
                _reqFlash = 1.5f;
                break;
            case TownEventKind.RequestMissed: Note($"Too late for {Lower(who)}. Maybe next time."); _reqFlash = -1.5f; break;
        }
    }

    private void RequestFrame(float dt)
    {
        var t = _drv.Town;
        var r = t.Request;
        if (_reqFlash != 0)
        {
            // Met: the card stays a moment, green; missed: it fades out grey.
            _reqFlash = _reqFlash > 0 ? Math.Max(0, _reqFlash - dt) : Math.Min(0, _reqFlash + dt);
        }
        bool show = (r.Active || _reqFlash != 0) && _fullA > 0.01f;
        _req.Visible = show;
        if (!show) return;
        _req.Position = new Vector2(UiKit.Px(_s, 16), (_ledger != null ? _ledger.GetRect().End.Y : 180) + UiKit.Px(_s, 8));
        _req.Modulate = _reqFlash < 0 ? new Color(1, 1, 1, Math.Clamp(-_reqFlash / 1.5f, 0, 1) * _fullA) : new Color(1, 1, 1, _fullA);
        _reqBox.BgColor = _reqFlash > 0 ? new Color(UiKit.Good.Lerp(UiKit.Paper, 0.7f), 0.97f) : new Color(UiKit.Paper, 0.96f);
        if (r.Active)
        {
            int secs = (int)Math.Ceiling(r.SecondsLeft);
            int sig = HashCode.Combine(r.Who, r.Good, r.Amount, r.Delivered, secs, (long)r.RewardCoin);
            if (sig != _reqSig)
            {
                _reqSig = sig;
                _reqWho.Text = $"Request from {Lower(r.WhoName ?? "someone")}";
                _reqAsk.Text = $"{r.Amount} {GoodName(r.Good)} within {secs} s";
                _reqDone.Text = $"Delivered {Math.Min(r.Delivered, r.Amount)} of {r.Amount}";
                _reqReward.Text = $"Reward: {Money(r.RewardCoin)} coin and renown";
            }
            float k = (float)Math.Clamp(r.Seconds > 0 ? r.SecondsLeft / r.Seconds : 0, 0, 1);
            var sz = _reqBarBack.Size;
            _reqBar.Position = new Vector2(1, 1);
            _reqBar.Size = new Vector2(Math.Max(0, (sz.X - 2) * k), Math.Max(0, sz.Y - 2));
            _reqBar.Color = k < 0.25f ? UiKit.Warn : UiKit.Focus;
        }
        else if (_reqFlash > 0 && _reqDone.Text.Length > 0 && !_reqDone.Text.StartsWith("Done"))
        {
            _reqDone.Text = "Done. Thank you!";
            _reqSig = int.MinValue;
        }
        else if (_reqFlash < 0 && !_reqDone.Text.StartsWith("Time"))
        {
            _reqDone.Text = "Time ran out";
            _reqSig = int.MinValue;
        }
    }

    private void HintFrame(float dt)
    {
        var t = _drv.Town;
        string h = t.Hint;
        int road = t.HintRoad, plot = t.HintPlot;
        _hintAge += dt;
        bool change = h.Length > 0 ? h != _hintShown && (_hintShown.Length == 0 || _hintAge >= HintHold) : _hintShown.Length > 0 && _hintAge >= HintHold;
        if (h.Length > 0 && h == _hintShown) { _hintAge = Math.Min(_hintAge, 0f); _hintRoad = road; _hintPlot = plot; }
        if (change)
        {
            _hintShown = h;
            _hintAge = 0;
            _hintRoad = road; _hintPlot = plot;
            _hint.Text = h.Length > 0 ? "Hint: " + h : "";
            _hint.Visible = h.Length > 0;
        }
        if (_marker == null) return;
        bool on = _hintShown.Length > 0 && (_hintRoad >= 0 || _hintPlot >= 0) && _fullA > 0.01f && !PaveMode && !ExtendMode;
        _marker.Visible = on;
        if (!on) return;
        Vector3 at;
        if (_hintRoad >= 0)
        {
            var rd = t.Plan.Roads[_hintRoad];
            rd.At(rd.Length / 2, out double x, out double y, out _);
            at = new Vector3((float)x * TownGround.Cell, 0.4f, (float)y * TownGround.Cell);
        }
        else at = PlotCentre(t.Plan.Plots[_hintPlot]) + new Vector3(0, 0.4f, 0);
        float size = _hintPlot >= 0 ? Math.Max(t.Plan.Plots[_hintPlot].W, t.Plan.Plots[_hintPlot].H) * 0.55f : 0.9f;
        float pulse = 1f + 0.12f * MathF.Sin((float)_drv.AnimTime * 3f);
        _marker.Position = at;
        _marker.Scale = new Vector3(size * pulse, 0.15f, size * pulse);
    }
}
