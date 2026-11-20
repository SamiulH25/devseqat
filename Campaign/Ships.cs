using Microsoft.Xna.Framework;

namespace Ironfield;

/// Campaign: ship travel (docs/archive/WORLDMAP.md WM7). From a port you pay a passage to a port its sea lanes reach; the company
/// sails the lane (`Party.Voyage`, quicker than any march and the same in winter), lands at the far quay and walks into the
/// town. Nothing meets a ship at sea, and only you sail: the realms' lords still go by land.
public partial class Campaign
{
    public bool Sailing => Player.Voyage.Count > 0;

    /// Is this place a port ships call at?
    public bool IsPort(Settlement s) => Map.Quays.ContainsKey(s.Id);

    /// The ports a ship sails to from this one, each with its lane.
    public IEnumerable<(Settlement To, SeaLane Lane)> Sailings(Settlement s) =>
        Map.SeaLanes.Where(l => l.A == s.Id || l.B == s.Id).Select(l => (SettlementById(l.A == s.Id ? l.B : l.A)!, l));

    /// The passage for the whole company: the master's fee, and so much a man for each hundred units of sea.
    public int Fare(SeaLane l) => (int)MathF.Ceiling(B.ShipFare + Rules.PerMan(B.ShipFareMan) * (Player.Men + Player.Wounded) * l.Length / 100);

    public float SailHours(SeaLane l) => l.Length / B.ShipSpeed;

    /// Why no ship will take you from one port to the other now (null: one will).
    public string? CannotSail(Settlement from, Settlement to)
    {
        if (Sailing) return "ALREADY AT SEA";
        if (Sailings(from).All(x => x.To != to)) return "NO SHIP SAILS THERE";
        if (Refuses(from) || Refuses(to)) return "PORT CLOSED TO YOU";
        if (Player.SiegeTarget >= 0) return "LIFT YOUR SIEGE";
        return null;
    }

    public string? Sail(Settlement from, Settlement to)
    {
        if (CannotSail(from, to) is string why) return why;
        var lane = Sailings(from).First(x => x.To == to).Lane;
        int fare = Fare(lane);
        if (S.Gold < fare) return $"NEEDS {fare} CROWNS";
        S.Gold -= fare;
        Player.Voyage = (lane.A == from.Id ? lane.Way : lane.Way.Reverse()).ToList();
        Player.VoyageTo = to.Id;
        Player.Pos = Player.Voyage[0]; // aboard at the quay
        Player.Voyage.RemoveAt(0);
        S.GoalSite = -1; Player.Goal = to.Pos; Player.Moving = true; Player.ChaseId = -1; Player.GoalSettlement = -1; Player.Resting = false;
        Log($"You take ship for {to.Name}.");
        return null;
    }

    /// The ship runs down its lane; at the far berth the company lands and marches into the port.
    void Sail(Party p, float h)
    {
        float step = B.ShipSpeed * h;
        while (step > 0 && p.Voyage.Count > 0)
        {
            var d = p.Voyage[0] - p.Pos;
            float dist = d.Length();
            if (dist > 1e-3f) p.Facing = MathF.Atan2(d.Y, d.X);
            if (dist <= step) { p.Pos = p.Voyage[0]; p.Voyage.RemoveAt(0); step -= dist; }
            else { p.Pos += d / dist * step; step = 0; }
        }
        if (p.Voyage.Count > 0) return;
        p.Pos = Map.InLand(p.Pos);
        var port = SettlementById(p.VoyageTo);
        p.VoyageTo = -1;
        if (port != null) { Log($"You land near {port.Name}."); GoTo(port); }
    }
}
