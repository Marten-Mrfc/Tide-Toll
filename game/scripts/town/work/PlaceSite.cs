using System;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// Night 5, the join: one place as the villagers see it (<see cref="IWorkSite"/>, WorkSites.cs), registered by
/// <see cref="WorkPlaces"/> for each well, field, mill, market and cottage plot. It passes the villagers' calls to the
/// place's drawing and gives them the places' positions: a field's worker at the front of their row with the row's
/// stook as the goods; the well's two pails on the rim; the mill's flour sack of that spot on the step; the market's
/// next free place on the table; a cottage's door. Nothing here allocates.
/// </summary>
public sealed class PlaceSite : IWorkSite
{
    private readonly WorkPlaces _w;
    private readonly int _plot;
    private readonly PlotKind _kind;

    public PlaceSite(WorkPlaces w, int plot, PlotKind kind) { _w = w; _plot = plot; _kind = kind; }

    public void WorkStarted(int villager, int slot, Good carried)
    {
        if (WorkPlaces.DetectFromSim) return;
        if (_kind == PlotKind.Well) _w.Well.Started(_plot);
        else if (_kind == PlotKind.Mill) _w.Mill.StartedAt(_plot, slot);
    }

    public void WorkFinished(int villager, int slot, Good good)
    {
        if (WorkPlaces.DetectFromSim) return;
        int wp = _w.WorkplaceOf(_plot);
        if (wp < 0) return;
        if (wp < _w.Units.Length) _w.Units[wp]++;
        switch (_kind)
        {
            case PlotKind.Well: _w.Well.UnitFinished(_plot, slot); _w.Well.Taken(_plot); break;
            case PlotKind.Field: _w.Fields.UnitFinished(_plot, slot); _w.Fields.Taken(_plot, slot); break;
            case PlotKind.Mill: _w.Mill.FinishedAt(_plot, slot); break;
        }
    }

    public void Delivered(int villager, Good good, int amount)
    {
        if (WorkPlaces.DetectFromSim) return;
        if (_kind == PlotKind.Market) _w.Stall.Landed(_plot, good);
        else if (_kind == PlotKind.Cottage) _w.Doors.CameHome(_plot, good);
    }

    public bool Spot(int slot, out float x, out float y, out float heading)
    {
        x = y = heading = 0f;
        if (_kind != PlotKind.Field) return false;
        var at = _w.Fields.WorkSpot(_plot, slot, out _);
        if (at == Vector3.Zero) return false;
        // map cells; facing the standing crop (+X on the map: heading 0)
        x = at.X / WorkPlaces.Cell; y = at.Z / WorkPlaces.Cell; heading = 0f;
        return true;
    }

    public bool GoodsAt(int slot, out Vector3 at)
    {
        at = Vector3.Zero;
        switch (_kind)
        {
            case PlotKind.Field: at = _w.Fields.StookAt(_plot, slot); return at != Vector3.Zero;
            case PlotKind.Well: if (_w.Buildings.BuiltBody(_plot) == null) return false; at = _w.Well.PailsMid(_plot); return true;
            case PlotKind.Mill:
                if (_w.Buildings.BuiltBody(_plot) == null) return false;
                at = _w.Mill.FlourAt(_plot, ((slot % 3) + 3) % 3) + new Vector3(0, 0.4f, 0); return true;
            case PlotKind.Market:
                if (_w.Buildings.BuiltBody(_plot) == null) return false;
                at = _w.Stall.PlaceAt(_plot, _w.Stall.NextPlace()); return true;
            case PlotKind.Cottage:
                if (_w.Buildings.BuiltBody(_plot) == null) return false;
                at = _w.Doors.DoorAt(_plot) + new Vector3(0, 0.9f, 0); return true;
        }
        return false;
    }
}
