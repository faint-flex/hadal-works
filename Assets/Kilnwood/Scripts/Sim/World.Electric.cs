using System;
using System.Collections.Generic;

namespace Kilnwood.Sim
{
    /// <summary>0.10 Tier 5 "Electricity". A dynamo sits on the shaft network and turns shaft power into electricity.
    /// Dynamos and power poles within PoleReach tiles of each other form one grid; electric machines within PoleCover
    /// tiles of a pole or dynamo join it. Every machine on a grid runs at min(1, supply / demand).</summary>
    public partial class World
    {
        /// <summary>One electric grid: its dynamos and poles, and the machines it powers.</summary>
        public class Grid
        {
            public readonly List<Building> Nodes = new List<Building>();      // dynamos and poles
            public readonly List<Building> Machines = new List<Building>();   // electric consumers
            public int Demand;
            public float Supply;
        }
        readonly List<Grid> _grids = new List<Grid>();
        public IReadOnlyList<Grid> Grids => _grids;

        static bool IsNode(Building b) => b.Type == BuildingType.Pole || b.Type == BuildingType.Dynamo || b.Type == BuildingType.Turbine;

        /// <summary>Tiles between two footprints along the longer axis (0 if they overlap, 1 if they touch).</summary>
        public static int Gap(Building a, Building b)
        {
            int dx = Math.Max(0, Math.Max(a.X - (b.X + b.Size - 1), b.X - (a.X + a.Size - 1)));
            int dy = Math.Max(0, Math.Max(a.Y - (b.Y + b.Size - 1), b.Y - (a.Y + a.Size - 1)));
            return Math.Max(dx, dy);
        }

        public bool Links(Building a, Building b) => Gap(a, b) <= B.PoleReach;

        /// <summary>Goes up every time the grids are rebuilt (the view re-strings its wires then).</summary>
        public int GridVersion { get; private set; }

        void RebuildGrids()
        {
            GridVersion++;
            _grids.Clear();
            var nodes = new List<Building>();
            var consumers = new List<Building>();
            foreach (var b in Buildings.Values)
            {
                b.Elec = -1f; b.GridSupply = 0f; b.GridDemand = 0; b.GridId = -1;
                if (IsNode(b)) nodes.Add(b);
                else if (B.Defs[b.Type].NeedsElectric) consumers.Add(b);
            }
            nodes.Sort((p, q) => p.Id.CompareTo(q.Id));
            consumers.Sort((p, q) => p.Id.CompareTo(q.Id));
            var seen = new HashSet<Building>();
            var queue = new Queue<Building>();
            foreach (var start in nodes)
            {
                if (!seen.Add(start)) continue;
                var g = new Grid();
                queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    var c = queue.Dequeue();
                    c.GridId = _grids.Count;
                    g.Nodes.Add(c);
                    foreach (var n in nodes) if (!seen.Contains(n) && Links(c, n)) { seen.Add(n); queue.Enqueue(n); }
                }
                _grids.Add(g);
            }
            foreach (var m in consumers)
            {
                foreach (var n in nodes)
                {
                    if (Gap(m, n) > B.PoleCover) continue;
                    var g = _grids[n.GridId];
                    m.GridId = n.GridId;
                    g.Machines.Add(m);
                    g.Demand += B.Defs[m.Type].Kw;
                    break;
                }
            }
        }

        /// <summary>After the shaft networks: each dynamo gives its kW times its shaft share; machines share the total.</summary>
        void StepGrids(float dt)
        {
            foreach (var n in Buildings.Values)
                if (n.Type == BuildingType.Turbine && n.GridId < 0) n.Fired = n.FuelLeft > 0f || n.FuelCount > 0;   // not on a grid: holds its fire
            foreach (var g in _grids)
            {
                float supply = 0f;
                foreach (var n in g.Nodes)
                {
                    if (n.Type == BuildingType.Dynamo && n.Power > 0f) supply += B.Defs[n.Type].Kw * n.Power;
                    else if (n.Type == BuildingType.Turbine)
                    {
                        // Burns only while something on its grid wants electricity.
                        n.Fired = g.Demand <= 0 ? n.FuelLeft > 0f || n.FuelCount > 0 : Burn(n, dt * B.TurbineBurnRate);
                        if (n.Fired) supply += B.Defs[n.Type].Kw;
                    }
                }
                g.Supply = supply;
                float share = supply <= 0f ? 0f : g.Demand <= 0 ? 1f : Math.Min(1f, supply / g.Demand);
                foreach (var n in g.Nodes) { n.GridSupply = supply; n.GridDemand = g.Demand; n.Elec = share; }
                foreach (var m in g.Machines) { m.GridSupply = supply; m.GridDemand = g.Demand; m.Elec = share; }
            }
        }

        string DynamoStatus(Building d)
        {
            if (d.Power < 0f) return "Not turning: needs a line shaft";
            if (d.Power == 0f) return "Not turning: engine out of fuel";
            string made = Kw(B.Defs[d.Type].Kw * d.Power);
            if (d.GridDemand == 0) return "Making " + made + " kW, nothing connected";
            return d.GridDemand > d.GridSupply + 0.01f
                ? "Overloaded: " + d.GridDemand + " kW wanted, " + Kw(d.GridSupply) + " kW made"
                : "Making " + made + " kW, grid uses " + d.GridDemand + " of " + Kw(d.GridSupply);
        }

        string TurbineStatus(Building t)
        {
            if (!t.Fired) return "No fuel: feed it wood, charcoal or coal";
            if (t.GridDemand == 0) return "Fired, nothing connected";
            return t.GridDemand > t.GridSupply + 0.01f
                ? "Overloaded: " + t.GridDemand + " kW wanted, " + Kw(t.GridSupply) + " kW made"
                : "Making " + B.Defs[t.Type].Kw + " kW, grid uses " + t.GridDemand + " of " + Kw(t.GridSupply);
        }

        string PoleStatus(Building p)
        {
            if (p.GridSupply <= 0f) return p.GridDemand > 0 ? "No electricity: no dynamo turning or turbine fired on this grid" : "Not linked to a dynamo or turbine";
            return p.GridDemand == 0 ? "Live, nothing connected" : "Live: " + p.GridDemand + " of " + Kw(p.GridSupply) + " kW used";
        }

        static string Kw(float kw) => kw >= 9.95f ? Math.Round(kw).ToString("0") : kw.ToString("0.0");
    }
}
