using System;
using System.Globalization;
using System.IO;
using System.Text;
using TideToll.Sim.Town;

// Usage: dotnet run -c Release -- SEED OUTDIR [MAXBOATS]
// Plays seed SEED with the Cheapest bot (PlayOn) through the first age and the second, and writes
//   river.json : the river plan (water edges per row, CourseX, berths, waiting places, crossing rows, plots)
//   boats.csv  : every tick, every boat not Gone: t (s since the ring), slot, id, kind, phase, berth, x, y, heading, draw (drawbridge built), angle
// It stops when MAXBOATS boats have come and gone (default 40) or at the seal.
var inv = CultureInfo.InvariantCulture;
int seed = args.Length > 0 ? int.Parse(args[0], inv) : 1;
string outDir = args.Length > 1 ? args[1] : ".";
int maxBoats = args.Length > 2 ? int.Parse(args[2], inv) : 40;
Directory.CreateDirectory(outDir);
var t = new Town(seed);
var bot = new TownBot(BotKind.Cheapest, seed) { PlayOn = true };
var csv = new StringBuilder("t,slot,id,kind,phase,berth,x,y,heading,draw,angle,berths,crane\n");
var ev = new System.Collections.Generic.List<TownEvent>();
double ring = -1; long maxTicks = 130L * 60 * TownRules.TickHz;
for (long k = 0; k < maxTicks; k++)
{
    t.Step(); t.DrainEvents(ev);
    foreach (var e in ev) if (e.Kind == TownEventKind.NextAge) ring = e.Time;
    bot.Act(t);
    if (!t.InRiverAge) continue;
    bool draw = t.CrossingExists(RiverLinkKind.Drawbridge);
    for (int i = 0; i < t.BoatCount; i++)
    {
        var b = t.Boat(i);
        if (b.Phase == BoatPhase.Gone) continue;
        csv.Append(inv, $"{t.Time - ring:0.0},{i},{b.Id},{b.Kind},{b.Phase},{b.Berth},{b.X:0.###},{b.Y:0.###},{b.Heading:0.####},{(draw ? 1 : 0)},{t.DrawbridgeAngle:0.###},0,0\n");
    }
    if (t.BoatsCome > maxBoats) { bool any = false; for (int i = 0; i < t.BoatCount; i++) if (t.Boat(i).Phase != BoatPhase.Gone && t.Boat(i).Id <= maxBoats) any = true; if (!any) break; }
}
File.WriteAllText(Path.Combine(outDir, "boats.csv"), csv.ToString());
var rp = t.RiverPlan!; var map = t.Plan.Map; int n = map.Size;
var js = new StringBuilder("{");
js.Append(inv, $"\"seed\":{seed},\"size\":{n},\"landingRow\":{rp.LandingRow},\"fordRow\":{rp.FordRow},\"ferryRow\":{rp.FerryRow},\"footRow\":{rp.FootRow},\"drawRow\":{rp.DrawRow},");
js.Append("\"west\":["); for (int y = 0; y < n; y++) { int w = -1; for (int x = 0; x < n; x++) if (map.Water[y * n + x] != 0) { w = x; break; } js.Append(inv, $"{(y > 0 ? "," : "")}{w}"); }
js.Append("],\"east\":["); for (int y = 0; y < n; y++) { int e2 = -1; for (int x = n - 1; x >= 0; x--) if (map.Water[y * n + x] != 0) { e2 = x + 1; break; } js.Append(inv, $"{(y > 0 ? "," : "")}{e2}"); }
js.Append("],\"courseX\":["); for (int y = 0; y < n; y++) js.Append(inv, $"{(y > 0 ? "," : "")}{rp.CourseX[y]:0.###}");
js.Append(inv, $"],\"berthX\":[{rp.BerthX[0]},{rp.BerthX[1]}],\"berthY\":[{rp.BerthY[0]},{rp.BerthY[1]}],\"waitY\":[{rp.WaitY[0]},{rp.WaitY[1]},{rp.WaitY[2]}],\"plots\":[");
for (int i = 0; i < rp.Plots.Length; i++) { var p = rp.Plots[i]; js.Append(inv, $"{(i > 0 ? "," : "")}{{\"kind\":\"{p.Kind}\",\"x\":{p.X},\"y\":{p.Y},\"w\":{p.W},\"h\":{p.H},\"far\":{(p.FarBank ? "true" : "false")}}}"); }
js.Append("],\"links\":[");
for (int i = 0; i < rp.Links.Length; i++) { var l = rp.Links[i]; js.Append(inv, $"{(i > 0 ? "," : "")}{{\"kind\":\"{l.Kind}\",\"px\":["); for (int j = 0; j < l.Px.Length; j++) js.Append(inv, $"{(j > 0 ? "," : "")}{l.Px[j]:0.##}"); js.Append("],\"py\":["); for (int j = 0; j < l.Py.Length; j++) js.Append(inv, $"{(j > 0 ? "," : "")}{l.Py[j]:0.##}"); js.Append("]}"); }
js.Append(inv, $"],\"ringAt\":{ring:0.0},\"boatsCome\":{t.BoatsCome}}}");
File.WriteAllText(Path.Combine(outDir, "river.json"), js.ToString());
Console.WriteLine($"ring at {ring:0}s, boats {t.BoatsCome}, age time {t.Time - ring:0}s");
