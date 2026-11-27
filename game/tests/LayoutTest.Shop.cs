using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace TideToll.Game.Tests;

/// <summary>
/// Follow-up to sheet v3: the shop's moving parts sampled every frame on the live interface (1920 x 1080): the tall /
/// compact switch eases (row heights pass through values between, rows never overlap one another, line 2 fades) and
/// the foot line never lies over a row while it arrives or leaves.
/// </summary>
public partial class LayoutTest
{
    private async Task ShopSamples()
    {
        var hud = Main.Hud;
        hud.ForceCanvas(new Vector2(1920, 1080), 1f);
        await Frames(4);
        foreach (bool toCompact in new[] { !hud.CompactShop, hud.CompactShop })
        {
            hud.CompactForce = toCompact;
            var heights = new HashSet<int>();
            int frames = 0, overlaps = 0, between = 0;
            float lo = 40, hi = ShopRow.TallUnits;
            while (frames < 400)
            {
                await Frames(1);
                frames++;
                if (hud.ShownItems.Count == 0) break;
                float h = hud.RowNode(0).Button.Size.Y;
                heights.Add((int)MathF.Round(h));
                if (h > lo + 0.5f && h < hi - 0.5f) between++;
                for (int i = 0; i + 1 < hud.ShownItems.Count; i++)
                {
                    var a = hud.RowNode(i); var b = hud.RowNode(i + 1);
                    if (a.Leaving || b.Leaving || a.Alpha < 0.5f || b.Alpha < 0.5f) continue;
                    if (a.Button.Position.Y + a.Button.Size.Y > b.Button.Position.Y + 0.5f) overlaps++;
                }
                if (!hud.ShopSwitching && frames > 2) break;
            }
            string what = $"switch to {(toCompact ? "compact" : "tall")}: {frames} frames, {between} with a height between, heights [{string.Join(",", heights)}], overlaps {overlaps}";
            GD.Print("LAYOUT shop " + what);
            Notes.Add(what);
            Check(hud.CompactShop == toCompact && hud.ShownItems.Count > 0, $"the shop switched ({what})");
            Check(between >= 3, $"the switch eases over several frames ({what})");
            Check(overlaps == 0, $"no row overlaps the next during the switch ({what})");
        }
        hud.CompactForce = null;
        await Frames(30);
        // The foot arriving and leaving, every frame.
        int before = hud.FootOverRowFrames;
        foreach (string text in new[] { "Later: Sampled at level 99", "" })
        {
            hud.FootForce = text;
            int frames = 0;
            float maxA = 0, minA = 1;
            while (frames < 400)
            {
                await Frames(1);
                frames++;
                maxA = Math.Max(maxA, hud.FootAlpha); minA = Math.Min(minA, hud.FootAlpha);
                if (frames > 30 && (text.Length > 0 ? hud.FootAlpha >= 1 : hud.FootAlpha <= 0)) break;
            }
            string what = $"foot {(text.Length > 0 ? "in" : "out")}: {frames} frames, alpha {minA:0.00}..{maxA:0.00}";
            GD.Print("LAYOUT shop " + what);
            Check(text.Length > 0 ? hud.FootAlpha >= 1 : hud.FootAlpha <= 0, $"the foot arrived or left ({what})");
        }
        hud.FootForce = null;
        await Frames(30);
        Check(hud.FootOverRowFrames == before, $"the foot never lay over a row while it came and went ({hud.FootOverRowFrames - before} frames)");
        hud.ForceCanvas(null);
        await Frames(2);
    }
}
