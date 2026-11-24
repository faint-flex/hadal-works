using System;
using System.Collections.Generic;

namespace Kilnwood.Sim
{
    /// <summary>0.6 Tier 2 "Water Power": waterwheels and line shafts. A network is shafts and wheels joined edge to edge;
    /// every machine touching it shares its power: speed = min(1, supply / demand).</summary>
    public partial class World
    {
        bool _powerDirty = true;
        readonly Queue<Building> _pq = new Queue<Building>();
        readonly HashSet<Building> _seen = new HashSet<Building>();

        /// <summary>One shaft network: what drives it (wheels, engines), what carries it, and what it runs.</summary>
        class Net
        {
            public readonly List<Building> Parts = new List<Building>();      // shafts, wheels, engines
            public readonly List<Building> Machines = new List<Building>();   // consumers touching it
            public int Demand;
        }
        readonly List<Net> _nets = new List<Net>();

        static bool Conducts(Building b) => b.Type == BuildingType.Shaft || b.Type == BuildingType.Waterwheel || b.Type == BuildingType.SteamEngine;

        /// <summary>Buildings and shafts touching this one on any of its edges (not diagonals).</summary>
        IEnumerable<Building> Neighbours(Building b)
        {
            for (int i = 0; i < b.Size; i++)
            {
                foreach (var (x, y) in new[] { (b.X + i, b.Y - 1), (b.X + i, b.Y + b.Size), (b.X - 1, b.Y + i), (b.X + b.Size, b.Y + i) })
                {
                    var n = At(x, y); if (n != null) yield return n;
                    var s = ShaftAt(x, y); if (s != null) yield return s;
                }
            }
        }

        void RebuildPower()
        {
            _powerDirty = false;
            _nets.Clear();
            foreach (var b in Buildings.Values) { b.Power = -1f; b.NetSupply = b.NetDemand = 0; }
            _seen.Clear();
            var claimed = new HashSet<Building>();
            foreach (var start in Buildings.Values)
            {
                if (!Conducts(start) || _seen.Contains(start)) continue;
                var net = new Net();
                _pq.Enqueue(start);
                _seen.Add(start);
                while (_pq.Count > 0)
                {
                    var c = _pq.Dequeue();
                    net.Parts.Add(c);
                    foreach (var n in Neighbours(c))
                    {
                        if (Conducts(n)) { if (_seen.Add(n)) _pq.Enqueue(n); }
                        else if (B.Defs[n.Type].Hp > 0 && claimed.Add(n)) net.Machines.Add(n);
                    }
                }
                foreach (var m in net.Machines) net.Demand += B.Defs[m.Type].Hp;
                _nets.Add(net);
            }
        }

        /// <summary>Every step: wheels always give their hp; engines give theirs while they have fire, burning fuel
        /// only while something on the network wants power. Machines get min(1, supply / demand).</summary>
        void StepPower(float dt)
        {
            foreach (var net in _nets)
            {
                int supply = 0;
                bool any = false;
                foreach (var p in net.Parts)
                {
                    if (p.Type == BuildingType.Waterwheel) { supply += B.Defs[p.Type].Hp; any = true; }
                    else if (p.Type == BuildingType.SteamEngine)
                    {
                        any = true;
                        bool fired = net.Demand <= 0 ? (p.FuelLeft > 0f || p.FuelCount > 0) : Burn(p, dt * B.EngineBurnRate);
                        if (fired) supply += B.Defs[p.Type].Hp;
                        p.Status = !fired ? "No fuel (belt in coal, charcoal or wood)" : net.Demand <= 0 ? "Fired, nothing connected" : "Running";
                    }
                }
                float share = !any || supply <= 0 ? (any ? 0f : -1f) : net.Demand <= 0 ? 1f : Math.Min(1f, supply / (float)net.Demand);
                foreach (var p in net.Parts) { p.NetSupply = supply; p.NetDemand = net.Demand; p.Power = share; }
                foreach (var m in net.Machines) m.Power = share;
            }
        }

        /// <summary>Power or fuel for dt seconds of work. Powered machines run at their network's share (no fuel);
        /// sawmills and trip hammers can't run without power; others burn fuel if they need it.</summary>
        bool Run(Building m, ref float dt, bool burnsFuel)
        {
            var def = B.Defs[m.Type];
            if (def.NeedsPower)
            {
                if (m.Power <= 0f) { m.Status = m.Power == 0f ? "No power (the engine is out of fuel)" : "No power (join it to a waterwheel with line shafts)"; return false; }
                dt *= m.Power;
                return true;
            }
            if (def.Hp > 0 && m.Power > 0f) { dt *= m.Power; return true; }
            if (burnsFuel && !Burn(m, dt)) { m.Status = "No fuel"; return false; }
            return true;
        }

        void SetWorking(Building m)
        {
            m.Status = m.Power > 0f && m.Power < 0.995f && B.Defs[m.Type].Hp > 0
                ? "Low power " + (int)Math.Round(m.Power * 100f) + "%"
                : "Working";
        }

        string WheelStatus(Building w)
        {
            if (w.NetDemand == 0) return "Turning, nothing connected";
            return w.NetDemand > w.NetSupply
                ? "Overloaded: " + w.NetDemand + " hp wanted, " + w.NetSupply + " hp given"
                : "Turning: " + w.NetDemand + " of " + w.NetSupply + " hp used";
        }

        /// <summary>hp this machine uses on its network (0 if it isn't powered).</summary>
        public bool IsPowered(Building b) => b.Power > 0f;
    }
}
