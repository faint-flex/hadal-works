using System;

namespace Kilnwood.Sim
{
    /// <summary>
    /// 0.22 Bulldozers. A "Level" mark asks for ground to go back to its natural surface: the dozer pushes down trees on
    /// marked forest tiles (their logs go to the lodge), shaves spoil heaps down and pushes that dirt into holes. Spare
    /// dirt goes to the dump area; holes with nothing to fill them take dirt from the nearest spoil heap. A levelled tile
    /// can be built on again, and cleared forest can be dug.
    /// Uses the vehicle's excavator fields: DigTile = the tile being worked, Bucket/BucketCount = dirt on the blade.
    /// </summary>
    public partial class World
    {
        public readonly bool[] LevelMark;

        public string CanMarkLevel(int x, int y)
        {
            if (!InBounds(x, y)) return "Off the map";
            int i = y * W + x;
            if (Ground[i] == Terrain.Water) return "Can't level the river";
            if (_occ[i] >= 0 || _shaftOcc[i] >= 0) return "Something is built there";
            if (Units[i] == 0 && Ground[i] != Terrain.Forest) return "Already level";
            return null;
        }

        public int MarkLevel(int x0, int y0, int x1, int y1)
        {
            int n = 0;
            ForRect(x0, y0, x1, y1, (x, y) =>
            {
                if (CanMarkLevel(x, y) != null) return;
                int i = y * W + x;
                DigMark[i] = DumpMark[i] = false;
                if (!LevelMark[i]) { LevelMark[i] = true; ChangedGround.Add(i); n++; }
            });
            return n;
        }

        /// <summary>Done once the tile is back at the surface and has no trees on it.</summary>
        bool LevelDone(int i) => Units[i] == 0 && Ground[i] != Terrain.Forest;

        void FinishLevel(int i)
        {
            if (LevelMark[i] && LevelDone(i)) { LevelMark[i] = false; ChangedGround.Add(i); }
        }

        public int CountLevelMarks()
        {
            int n = 0;
            for (int i = 0; i < LevelMark.Length; i++) if (LevelMark[i]) n++;
            return n;
        }

        /// <summary>Another dozer is working this tile (or right beside it).</summary>
        bool DozerTaken(Vehicle v, int i)
        {
            foreach (var o in Vehicles)
                if (o != v && o.Type == VehicleType.Dozer && o.DigTile >= 0 && Near(o.DigTile, i, W)) return true;
            return false;
        }

        /// <summary>Nearest tile for which `want` is true and no other dozer works, or -1.</summary>
        int NearestFor(Vehicle v, Func<int, bool> want)
        {
            int me = TileOf(v), best = -1, bestD = int.MaxValue;
            for (int i = 0; i < Units.Length; i++)
            {
                if (!want(i) || DozerTaken(v, i)) continue;
                int d = Math.Abs(i % W - me % W) + Math.Abs(i / W - me / W);
                if (d < bestD) { best = i; bestD = d; }
            }
            return best;
        }

        /// <summary>A heap the dozer may take dirt from: raised ground with nothing built on it.</summary>
        bool SpoilAt(int i) => Units[i] > 0 && _occ[i] < 0 && _shaftOcc[i] < 0;

        bool GoBeside(Vehicle v, int tile, VehicleState after)
        {
            v.DigTile = tile;
            v.Timer = 0f;
            if (Near(TileOf(v), tile, W) && TileOf(v) != tile) { v.State = after; return true; }
            if (Go(v, n => n != tile && Near(n, tile, W) && !Occupied(n, v), after) || Go(v, n => n != tile && Near(n, tile, W), after)) return true;
            v.DigTile = -1;
            return false;
        }

        void StepDozer(Vehicle v, float dt)
        {
            if (HandleFuel(v, dt)) return;
            float push = B.DozerPushSeconds / B.EraWork[Era];
            switch (v.State)
            {
                case VehicleState.Driving:
                    if (Drive(v, dt)) { v.State = v.After; v.Timer = 0f; }
                    if (v.State == VehicleState.Refuelling) HandleFuel(v, 0f);
                    return;
                case VehicleState.Idle:
                case VehicleState.Waiting:
                {
                    v.DigTile = -1;
                    if (v.BucketCount > 0)
                    {
                        // Dirt on the blade: into the nearest marked hole, else onto the dump.
                        int sink = NearestFor(v, i => LevelMark[i] && Units[i] < 0);
                        if (sink < 0) sink = NearestFor(v, i => DumpMark[i]);
                        if (sink < 0) { v.Status = "Nowhere to push the dirt (mark a dump)"; v.State = VehicleState.Waiting; return; }
                        if (GoBeside(v, sink, VehicleState.Tipping)) v.Status = LevelMark[sink] ? "Pushing dirt into a hole" : "Pushing dirt to the dump";
                        else { v.Status = "Can't reach the hole"; v.State = VehicleState.Waiting; }
                        return;
                    }
                    // Trees first (they block the rest), then heaps inside the marked ground.
                    int job = NearestFor(v, i => LevelMark[i] && Ground[i] == Terrain.Forest);
                    if (job < 0) job = NearestFor(v, i => LevelMark[i] && Units[i] > 0);
                    // Only holes left: fetch dirt from the nearest spoil heap outside the marks.
                    if (job < 0 && NearestFor(v, i => LevelMark[i] && Units[i] < 0) >= 0) job = NearestFor(v, i => !LevelMark[i] && !DumpMark[i] && SpoilAt(i));
                    if (job < 0)
                    {
                        bool holes = false;
                        for (int i = 0; i < LevelMark.Length && !holes; i++) if (LevelMark[i] && Units[i] < 0) holes = true;
                        v.Status = holes ? "No spoil heap to fill the holes from" : "Nothing marked to level";
                        v.State = VehicleState.Waiting;
                        return;
                    }
                    if (GoBeside(v, job, VehicleState.Digging))
                        v.Status = Ground[job] == Terrain.Forest ? "Driving to clear trees" : LevelMark[job] ? "Driving to level a heap" : "Fetching dirt from a spoil heap";
                    else { v.Status = "Can't reach the marked ground"; v.State = VehicleState.Waiting; }
                    return;
                }
                case VehicleState.Digging:
                {
                    int i = v.DigTile;
                    if (i < 0) { v.State = VehicleState.Idle; return; }
                    Face(v, i);
                    UseFuel(v, dt);
                    v.Timer += dt * (v.Tank > 0f ? 1f : 0.25f);
                    if (Ground[i] == Terrain.Forest && LevelMark[i])
                    {
                        v.Status = "Clearing trees";
                        if (v.Timer < B.DozerClearSeconds / B.EraWork[Era]) return;
                        int logs = (int)Math.Round(Logs[i] * B.DozerLogShare);
                        Storage[Item.Wood] += logs;
                        Delivered[Item.Wood] += logs;
                        SetGround(i % W, i / W, Terrain.Grass);
                        ChangedLogs.Add(i);
                        ChangedGround.Add(i);
                        TreesCleared++;
                        ClearedForest.Add(i);
                        FinishLevel(i);
                        v.State = VehicleState.Idle;
                        return;
                    }
                    // Shave the heap onto the blade. A marked heap goes down to the surface; a heap fetched for a hole
                    // gives only what the holes still need.
                    v.Status = "Pushing dirt";
                    while (v.Timer >= push)
                    {
                        v.Timer -= push;
                        if (Units[i] <= 0 || v.BucketCount >= B.DozerBlade || (!LevelMark[i] && v.BucketCount >= HolesNeed())) break;
                        Units[i]--;
                        ChangedGround.Add(i);
                        v.Bucket = Item.Dirt;
                        v.BucketCount++;
                        DozerMoved++;
                    }
                    FinishLevel(i);
                    if (Units[i] <= 0 || v.BucketCount >= B.DozerBlade || (!LevelMark[i] && v.BucketCount >= HolesNeed())) v.State = VehicleState.Idle;
                    return;
                }
                case VehicleState.Tipping:
                {
                    int i = v.DigTile;
                    if (i < 0 || v.BucketCount == 0) { v.State = VehicleState.Idle; return; }
                    Face(v, i);
                    UseFuel(v, dt);
                    v.Timer += dt * (v.Tank > 0f ? 1f : 0.25f);
                    v.Status = LevelMark[i] ? "Filling a hole" : "Pushing dirt to the dump";
                    while (v.Timer >= push && v.BucketCount > 0)
                    {
                        v.Timer -= push;
                        if (LevelMark[i] && Units[i] < 0) { Units[i]++; ChangedGround.Add(i); }
                        else if (!LevelMark[i] && DumpMark[i] && FillOne(i)) { }
                        else break;
                        v.BucketCount--;
                    }
                    FinishLevel(i);
                    if (v.BucketCount == 0) { v.Bucket = Item.None; v.State = VehicleState.Idle; }
                    else if (!(LevelMark[i] && Units[i] < 0) && !(!LevelMark[i] && DumpMark[i])) v.State = VehicleState.Idle;
                    return;
                }
            }
        }

        /// <summary>Dirt the marked holes still need, less what other dozers already carry.</summary>
        int HolesNeed()
        {
            int n = 0;
            for (int i = 0; i < LevelMark.Length; i++) if (LevelMark[i] && Units[i] < 0) n -= Units[i];
            return n;
        }

        /// <summary>Stats for the HUD and the checks: trees pushed down and dirt units moved by dozers.</summary>
        public int TreesCleared, DozerMoved;
        public readonly System.Collections.Generic.HashSet<int> ClearedForest = new System.Collections.Generic.HashSet<int>();

        public void RestoreCleared(int i)
        {
            if (i < 0 || i >= Ground.Length || Ground[i] != Terrain.Forest) return;
            SetGround(i % W, i / W, Terrain.Grass);
            ClearedForest.Add(i);
            ChangedLogs.Add(i);
            ChangedGround.Add(i);
        }
    }
}
