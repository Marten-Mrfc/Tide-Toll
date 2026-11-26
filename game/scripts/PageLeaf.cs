using System;
using Godot;

namespace TideToll.Game;

/// <summary>
/// A leaf of the book's paper turning, drawn flat: the lifted part of the leaf lies between <see cref="Spine"/> (where
/// it is bound, or where it hangs from) and <see cref="Edge"/> (its moving edge), across <see cref="Area"/>'s height.
/// Near its edge the paper curls away from the light (a shaded band with a pale fold line), and it throws a soft shadow
/// past the edge onto the page it is about to cover or has just left. <see cref="Opaque"/>: the leaf is solid paper and
/// hides everything under it (the age-change page); otherwise it is the printed page itself turning, so it only washes
/// toward paper as it stands up (<see cref="Lift"/>) and the pages' own text shows through it (the settings tabs).
/// Drawn with rectangles only: nothing is allocated per frame.
/// </summary>
public partial class PageLeaf : Control
{
    public Rect2 Area;
    public float Spine, Edge;
    /// <summary>0 lying flat, 1 standing up: the strength of the wash, the curl and the shadow.</summary>
    public float Lift = 1f;
    public bool Opaque;
    public float S = 1f;
    private const int Strips = 14;

    public PageLeaf()
    {
        MouseFilter = MouseFilterEnum.Ignore;
    }

    /// <summary>Sets the leaf and redraws; the leaf draws nothing while it has no width.</summary>
    public void Place(Rect2 area, float spine, float edge, float lift)
    {
        if (area == Area && spine == Spine && edge == Edge && lift == Lift) return;
        Area = area; Spine = spine; Edge = edge; Lift = lift;
        QueueRedraw();
    }

    public override void _Draw()
    {
        float x0 = Math.Min(Spine, Edge), x1 = Math.Max(Spine, Edge), w = x1 - x0;
        if (Lift <= 0.001f) return;
        float top = Area.Position.Y, h = Area.Size.Y;
        float dir = Edge >= Spine ? 1f : -1f;   // from the spine toward the edge
        var ink = UiKit.Ink;
        if (w < 0.5f) { }
        else if (Opaque) DrawRect(new Rect2(x0, top, w, h), UiKit.Paper);
        else
        {
            // The printed leaf washes toward paper the further it is from the spine (it stands up, foreshortened).
            // Strong early: a leaf a quarter lifted already turns most of its face away from the eye.
            float a = 0.9f * MathF.Pow(Lift, 0.6f);
            for (int i = 0; i < Strips; i++)
            {
                float t = (i + 0.5f) / Strips;           // 0 at the spine, 1 at the edge
                float sx = Spine + dir * w * i / Strips;
                var r = new Rect2(dir > 0 ? sx : sx - w / Strips, top, w / Strips + 0.75f, h);
                DrawRect(r, new Color(UiKit.Paper, a * MathF.Pow(t, 1.4f)));
            }
        }
        // The curl: the last part of the leaf before its edge turns away from the light.
        float cw = Math.Min(w, 56f * S);
        float curl = Opaque ? Lift : MathF.Pow(Lift, 0.6f);
        for (int i = 0; i < Strips && w >= 0.5f; i++)
        {
            float t = (i + 0.5f) / Strips;               // 0 at the edge, 1 inward
            float sx = dir > 0 ? Edge - cw * (i + 1) / Strips : Edge + cw * i / Strips;
            DrawRect(new Rect2(sx, top, cw / Strips + 0.75f, h), new Color(ink, 0.26f * curl * (1f - t) * (1f - t)));
        }
        // The fold line, pale paper catching the light.
        float fl = Math.Max(1f, 1.5f * S);
        DrawRect(new Rect2(Edge - (dir > 0 ? fl : 0f), top, fl, h), new Color(UiKit.PaperHover, 0.9f * Math.Min(1f, Lift * 2f)));
        // Its shadow on the page beyond the edge.
        float sw = 44f * S * MathF.Pow(Lift, 0.6f);
        if (sw < 1f) return;
        for (int i = 0; i < Strips; i++)
        {
            float t = (i + 0.5f) / Strips;
            float sx = dir > 0 ? Edge + sw * i / Strips : Edge - sw * (i + 1) / Strips;
            DrawRect(new Rect2(sx, top, sw / Strips + 0.75f, h), new Color(ink, 0.32f * Lift * (1f - t) * (1f - t)));
        }
    }
}
