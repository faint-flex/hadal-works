using System;
using System.Collections.Generic;

namespace Kilnwood.Sim
{
    /// <summary>
    /// What gets saved: buildings, hub storage and delivered counts, play time.
    /// Items in flight (tubes, machine buffers) are not saved in M1; they refill within seconds.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public int Version = 2;
        public float Time;
        public List<SavedBuilding> Buildings = new List<SavedBuilding>();
        public List<int> StorageCounts = new List<int>();
        public List<int> DeliveredCounts = new List<int>();
        public List<int> Logs = new List<int>();
        public List<int> Units = new List<int>();          // ground dug / filled per tile
        public List<int> Marks = new List<int>();          // 1 dig, 2 dump, per tile
        public List<SavedVehicle> Vehicles = new List<SavedVehicle>();

        [Serializable]
        public class SavedVehicle
        {
            public int Type, GarageX, GarageY;
            public float X, Y, Tank;
        }

        [Serializable]
        public class SavedBuilding
        {
            public int Type, X, Y, Facing, Recipe;
        }

        public static SaveData From(World w)
        {
            var s = new SaveData { Time = w.Time };
            foreach (var b in w.Buildings.Values)
            {
                if (b.Type == BuildingType.Lodge) continue;
                s.Buildings.Add(new SavedBuilding { Type = (int)b.Type, X = b.X, Y = b.Y, Facing = b.Facing, Recipe = b.Recipe });
            }
            foreach (Item it in Enum.GetValues(typeof(Item)))
            {
                s.StorageCounts.Add(w.Storage[it]);
                s.DeliveredCounts.Add(w.Delivered[it]);
            }
            s.Logs.AddRange(w.Logs);
            s.Units.AddRange(w.Units);
            for (int i = 0; i < w.DigMark.Length; i++) s.Marks.Add(w.DigMark[i] ? 1 : w.DumpMark[i] ? 2 : 0);
            foreach (var v in w.Vehicles)
            {
                if (!w.Buildings.TryGetValue(v.GarageId, out var g)) continue;
                s.Vehicles.Add(new SavedVehicle { Type = (int)v.Type, GarageX = g.X, GarageY = g.Y, X = v.X, Y = v.Y, Tank = v.Tank });
            }
            return s;
        }

        public World ToWorld(Balance b)
        {
            var w = MapGen.NewTier1(b);
            // Ground first (buildings only sit on level ground, so this can't block them), marks after buildings.
            if (Units.Count == w.Units.Length) for (int i = 0; i < Units.Count; i++) w.Units[i] = Units[i];
            foreach (var sb in Buildings)
                w.Place((BuildingType)sb.Type, sb.X, sb.Y, sb.Facing, sb.Recipe, free: true);
            if (Marks.Count == w.DigMark.Length)
                for (int i = 0; i < Marks.Count; i++) { w.DigMark[i] = Marks[i] == 1; w.DumpMark[i] = Marks[i] == 2; }
            for (int i = 0; i < w.Units.Length; i++) if (w.Units[i] != 0 || w.DigMark[i] || w.DumpMark[i]) w.ChangedGround.Add(i);
            foreach (var sv in Vehicles)
            {
                var g = w.At(sv.GarageX, sv.GarageY);
                if (g != null && g.Type == BuildingType.Garage) w.RestoreVehicle((VehicleType)sv.Type, g.Id, sv.X, sv.Y, sv.Tank);
            }
            var items = (Item[])Enum.GetValues(typeof(Item));
            for (int i = 0; i < items.Length && i < StorageCounts.Count; i++) w.Storage[items[i]] = StorageCounts[i];
            for (int i = 0; i < items.Length && i < DeliveredCounts.Count; i++) w.Delivered[items[i]] = DeliveredCounts[i];
            if (Logs.Count == w.Logs.Length) w.RestoreLogs(Logs);
            w.RestoreTime(Time);
            w.UpdateTier();
            return w;
        }
    }
}
