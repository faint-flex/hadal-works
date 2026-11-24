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
        public int Version = 1;
        public float Time;
        public List<SavedBuilding> Buildings = new List<SavedBuilding>();
        public List<int> StorageCounts = new List<int>();
        public List<int> DeliveredCounts = new List<int>();
        public List<int> Logs = new List<int>();

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
            return s;
        }

        public World ToWorld(Balance b)
        {
            var w = MapGen.NewTier1(b);
            foreach (var sb in Buildings)
                w.Place((BuildingType)sb.Type, sb.X, sb.Y, sb.Facing, sb.Recipe, free: true);
            var items = (Item[])Enum.GetValues(typeof(Item));
            for (int i = 0; i < items.Length && i < StorageCounts.Count; i++) w.Storage[items[i]] = StorageCounts[i];
            for (int i = 0; i < items.Length && i < DeliveredCounts.Count; i++) w.Delivered[items[i]] = DeliveredCounts[i];
            if (Logs.Count == w.Logs.Length) w.RestoreLogs(Logs);
            w.RestoreTime(Time);
            return w;
        }
    }
}
