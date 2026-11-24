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
        readonly List<Building> _netConductors = new List<Building>();
        readonly HashSet<Building> _netConsumers = new HashSet<Building>();

        static bool Conducts(Building b) => b.Type == BuildingType.Shaft || b.Type == BuildingType.Waterwheel;

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
            foreach (var b in Buildings.Values) { b.Power = -1f; b.NetSupply = b.NetDemand = 0; }
            _seen.Clear();
            foreach (var start in Buildings.Values)
            {
                if (!Conducts(start) || _seen.Contains(start)) continue;
                _netConductors.Clear();
                _netConsumers.Clear();
                _pq.Enqueue(start);
                _seen.Add(start);
                while (_pq.Count > 0)
                {
                    var c = _pq.Dequeue();
                    _netConductors.Add(c);
                    foreach (var n in Neighbours(c))
                    {
                        if (Conducts(n)) { if (_seen.Add(n)) _pq.Enqueue(n); }
                        else if (B.Defs[n.Type].Hp > 0 && n.Power < 0f) _netConsumers.Add(n);
                    }
                }
                int supply = 0, demand = 0;
                foreach (var c in _netConductors) if (c.Type == BuildingType.Waterwheel) supply += B.Defs[c.Type].Hp;
                foreach (var m in _netConsumers) demand += B.Defs[m.Type].Hp;
                float share = supply <= 0 ? -1f : demand <= 0 ? 1f : Math.Min(1f, supply / (float)demand);
                foreach (var c in _netConductors) { c.NetSupply = supply; c.NetDemand = demand; c.Power = share; }
                foreach (var m in _netConsumers) m.Power = share;
            }
        }

        /// <summary>Power or fuel for dt seconds of work. Powered machines run at their network's share (no fuel);
        /// sawmills and trip hammers can't run without power; others burn fuel if they need it.</summary>
        bool Run(Building m, ref float dt, bool burnsFuel)
        {
            var def = B.Defs[m.Type];
            if (def.NeedsPower)
            {
                if (m.Power <= 0f) { m.Status = "No power (join it to a waterwheel with line shafts)"; return false; }
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
