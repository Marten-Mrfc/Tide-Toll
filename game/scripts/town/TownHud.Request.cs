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
    private Control _req = null!;
    private Label _reqWho = null!, _reqAsk = null!, _reqDone = null!, _reqReward = null!;
    private Control _reqBarBack = null!;
    private ColorRect _reqBar = null!;
    private long _reqMetCoin;
    private MeshInstance3D? _marker;
    private float _hintAge, _reqFlash;
    private string _hintShown = "";
    private int _hintRoad = -1, _hintPlot = -1;
    private int _reqSig = int.MinValue;

    /// <summary>The hint line as shown ("" none), what its marker points at, and the request card (tests).</summary>
    public string HintText => _hintShown;
    public int HintMarkerRoad => _hintRoad;
    public int HintMarkerPlot => _hintPlot;
    public bool HintMarkerVisible => _marker != null && _marker.Visible && _markerOn;
    private bool _markerOn;
    private float _markerA;
    private StandardMaterial3D? _markerMat;
    public bool RequestCardVisible => _req.Visible;
    public string RequestCardText => _req.Visible ? $"{_reqWho.Text} | {_reqAsk.Text} | {_reqDone.Text} | {_reqReward.Text}" : "";
    /// <summary>The goal bar's fill (the count: delivered or loaded of the amount, sheet 3 "the bar with 27 of 117").</summary>
    public float RequestCountBar => _reqBar.Size.X / Math.Max(1f, _reqBarBack.Size.X - 2f);
    /// <summary>The share of the request's time still left (it tints the "within" text brick under a quarter).</summary>
    public float RequestTimeBar => _reqTimeShare;
    private float _reqTimeShare = 1f;
    private bool _reqLate, _reqTintDirty = true;

    /// <summary>Night 3 L1 fix: the bar shows the count; time only tints its text brick in the last quarter.</summary>
    private void SetGoalBar(float count, float timeShare, Label timeText, Color? rest = null)
    {
        _reqTimeShare = timeShare;
        UiKit.SetBar(_reqBarBack, _reqBar, Math.Clamp(count, 0, 1));
        _reqBar.Color = UiKit.RiverBlue;
        bool late = timeShare < 0.25f;
        if (late != _reqLate || _reqTintDirty)
        {
            _reqLate = late;
            _reqTintDirty = false;
            if (late) timeText.AddThemeColorOverride("font_color", UiKit.BrickRed);
            else if (rest is Color c) timeText.AddThemeColorOverride("font_color", c);
            else timeText.RemoveThemeColorOverride("font_color");
        }
    }
    public Vector3 HintMarkerLocal => _marker?.Position ?? Vector3.Zero;

    private void BuildRequestCard()
    {
        // Night 3: the goal section of the ledger (who asks, what and by when, the bar with its count, the reward).
        float s = _s;
        int pad = UiKit.U(s, 12), w = (int)_goalBox.Size.X - 2 * pad;
        _req = new Control { Name = "Request", MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false, Size = _goalBox.Size };
        _goalBox.AddChild(_req);
        Label At(Label l, string name, float y, float h)
        {
            l.Name = name; l.Position = new Vector2(pad, UiKit.U(s, y)); l.Size = new Vector2(w, UiKit.U(s, h));
            _req.AddChild(l);
            return l;
        }
        _reqWho = At(UiKit.Fixed(s, UiKit.Caption, UiKit.UiFont, UiKit.Muted), "Who", 4, 20);
        _reqAsk = At(UiKit.Fixed(s, UiKit.Body, UiKit.UiBold), "Ask", 24, 24);
        _reqDone = At(UiKit.Fixed(s, UiKit.Caption, UiKit.UiFont, UiKit.MutedNumber), "Done", 48, 20);
        _reqBarBack = UiKit.Bar(s, UiKit.RiverBlue, out _reqBar);
        _reqBarBack.Name = "Bar";
        _reqBarBack.Position = new Vector2(pad, UiKit.U(s, 70));
        _reqBarBack.Size = new Vector2(w, UiKit.U(s, 8));
        _req.AddChild(_reqBarBack);
        _reqReward = At(UiKit.Fixed(s, UiKit.Caption, UiKit.UiFont, UiKit.Muted), "Reward", 80, 20);
        _reqSig = int.MinValue;
        if (_marker == null || !IsInstanceValid(_marker))
        {
            // Night 3 L1 fix (sheet 5): a thin wheat ring, not a filled disc; it fades in and out with its hint.
            var mat = Palette.Unlit(new Color(UiKit.Wheat, 0f), transparent: true);
            mat.NoDepthTest = true;
            mat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
            _markerMat = mat;
            _marker = new MeshInstance3D { Name = "HintMarker", Mesh = ThinRing(0.95f, 48), MaterialOverride = mat, Visible = false,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            _main.MapOrigin.AddChild(_marker);
        }
    }

    public static string GoodName(Good g) => g switch { Good.Water => "Water", Good.Grain => "Grain", Good.Flour => "Flour", Good.Wool => "Wool", Good.Cloth => "Cloth", _ => "goods" };

    private void RequestEvent(in TownEvent e)
    {
        string who = e.Index >= 0 && e.Index < Town.RequestWho.Length ? Town.RequestWho[e.Index] : "Someone";
        switch (e.Kind)
        {
            case TownEventKind.RequestAsked: Note($"{who} asks for {e.Amount} {GoodName(e.Good)}."); break;
            case TownEventKind.RequestMet:
                Note($"{who} is pleased: +{Money(e.Coin)} coin.", true);
                _reqFlash = 1.6f; _reqMetCoin = (long)Math.Floor(e.Coin);
                break;
            case TownEventKind.RequestMissed: Note($"Too late for {Lower(who)}. Maybe next time."); _reqFlash = -1.6f; break;
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
        if (!r.Active && _reqFlash == 0 && RiverRequestFrame()) return;
        bool show = (r.Active || _reqFlash != 0) && _fullA > 0.01f;
        _req.Visible = show;
        if (!show) return;
        _req.Modulate = _reqFlash < 0 ? new Color(1, 1, 1, Math.Clamp(-_reqFlash / 0.4f, 0, 1)) : Colors.White;
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
                _reqReward.Text = $"Reward: {Money(r.RewardCoin)} and renown";
                _reqDone.AddThemeColorOverride("font_color", UiKit.MutedNumber);
                _reqTintDirty = true;
            }
            float k = (float)Math.Clamp(r.Seconds > 0 ? r.SecondsLeft / r.Seconds : 0, 0, 1);
            SetGoalBar(r.Amount > 0 ? Math.Min(r.Delivered, r.Amount) / (float)r.Amount : 0, k, _reqAsk);
        }
        else if (_reqFlash > 0 && !_reqDone.Text.StartsWith("Done"))
        {
            // Met: moss fill and "Done: +160" for 1.6 s.
            _reqDone.Text = $"Done: +{Money(_reqMetCoin)}";
            _reqDone.AddThemeColorOverride("font_color", UiKit.Moss);
            UiKit.SetBar(_reqBarBack, _reqBar, 1f);
            _reqBar.Color = UiKit.Moss;
            _reqSig = int.MinValue;
        }
        else if (_reqFlash < 0 && !_reqDone.Text.StartsWith("Too late"))
        {
            // Missed: "Too late" in brick for 1.6 s.
            _reqDone.Text = "Too late";
            _reqDone.AddThemeColorOverride("font_color", UiKit.BrickRed);
            _reqSig = int.MinValue;
        }
    }

    private void HintFrame(float dt)
    {
        var t = _drv.Town;
        string rh = t.RiverHint;
        string h = rh.Length > 0 ? rh : t.Hint;
        int road = rh.Length > 0 ? -1 : t.HintRoad, plot = rh.Length > 0 ? -1 : t.HintPlot;
        _hintAge += dt;
        bool change = h.Length > 0 ? h != _hintShown && (_hintShown.Length == 0 || _hintAge >= HintHold) : _hintShown.Length > 0 && _hintAge >= HintHold;
        if (h.Length > 0 && h == _hintShown) { _hintAge = Math.Min(_hintAge, 0f); _hintRoad = road; _hintPlot = plot; }
        if (change)
        {
            _hintShown = h;
            _hintAge = 0;
            _hintRoad = road; _hintPlot = plot;
            _hint.Text = h.Length > 0 ? h : "";
        }
        if (_marker == null) return;
        bool on = _hintShown.Length > 0 && (_hintRoad >= 0 || _hintPlot >= 0) && _fullA > 0.01f && !PaveMode && !ExtendMode;
        _markerOn = on;
        _markerA = on ? Math.Min(1f, _markerA + dt / 0.4f) : Math.Max(0f, _markerA - dt / 0.4f);
        _marker.Visible = _markerA > 0f;
        if (_markerMat != null && _markerMat.AlbedoColor.A != 0.9f * _markerA) _markerMat.AlbedoColor = new Color(UiKit.Wheat, 0.9f * _markerA);
        if (!on) return;
        Vector3 at;
        if (_hintRoad >= 0)
        {
            var rd = t.Plan.Roads[_hintRoad];
            rd.At(rd.Length / 2, out double x, out double y, out _);
            at = new Vector3((float)x * TownGround.Cell, 0.4f, (float)y * TownGround.Cell);
        }
        else at = PlotCentre(t.Plan.Plots[_hintPlot]) + new Vector3(0, 0.4f, 0);
        // Radius in metres: just outside the plot's corners (the old torus was 2.2 times this and read as a disc).
        float size = (_hintPlot >= 0 ? Math.Max(t.Plan.Plots[_hintPlot].W, t.Plan.Plots[_hintPlot].H) * 0.75f : 1.2f) * TownGround.Cell;
        float pulse = 1f + 0.03f * MathF.Sin((float)_drv.AnimTime * 2f);
        _marker.Position = at;
        _marker.Scale = new Vector3(size * pulse, 1f, size * pulse);
    }

    /// <summary>A flat ring in the ground plane, radius 1, from <paramref name="inner"/> to 1 (the hint ring).</summary>
    private static ArrayMesh ThinRing(float inner, int segs)
    {
        var v = new Vector3[segs * 2];
        var idx = new int[segs * 6];
        for (int i = 0; i < segs; i++)
        {
            float a = i * MathF.Tau / segs;
            float c = MathF.Cos(a), sn = MathF.Sin(a);
            v[i * 2] = new Vector3(c * inner, 0, sn * inner);
            v[i * 2 + 1] = new Vector3(c, 0, sn);
            int j = (i + 1) % segs;
            idx[i * 6] = i * 2; idx[i * 6 + 1] = i * 2 + 1; idx[i * 6 + 2] = j * 2 + 1;
            idx[i * 6 + 3] = i * 2; idx[i * 6 + 4] = j * 2 + 1; idx[i * 6 + 5] = j * 2;
        }
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = v;
        arrays[(int)Mesh.ArrayType.Index] = idx;
        var m = new ArrayMesh();
        m.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return m;
    }
}
