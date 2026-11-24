using System;

namespace HadalWorks.Sim
{
    /// <summary>Tier 1 "The Shelf": a fixed hand-made map so it can be balanced. Hub sits in the middle.</summary>
    public static class MapGen
    {
        public const int HubX = 30, HubY = 30;

        /// <summary>Names painted on the seabed at each patch (tile x, tile y, text).</summary>
        public static readonly (int x, int y, string text)[] Labels =
        {
            (15, 25, "FERRITE  (F)"), (47, 25, "SILICA  (S)"), (30, 53, "GLOWKELP  (K)"),
            (51, 4, "FERRITE  (F)"), (9, 48, "SILICA  (S)"), (14, 10, "GLOWKELP  (K)"), (53, 50, "GLOWKELP  (K)"),
        };

        public static World NewTier1(Balance b)
        {
            var w = new World(b);
            var rng = new Random(1977);

            // Main patches near the hub (rows line up with the hub so first lines are easy).
            Patch(w, rng, 12, 27, 8, 13, Terrain.Ferrite);   // west
            Patch(w, rng, 44, 27, 8, 13, Terrain.Silica);    // east
            Patch(w, rng, 26, 45, 10, 7, Terrain.Kelp);      // north
            // Farther patches for later expansion.
            Patch(w, rng, 48, 6, 7, 7, Terrain.Ferrite);
            Patch(w, rng, 6, 50, 7, 7, Terrain.Silica);
            Patch(w, rng, 10, 12, 8, 6, Terrain.Kelp);
            Patch(w, rng, 50, 52, 6, 6, Terrain.Kelp);

            // Thermal vents.
            int[,] vents = { { 24, 26 }, { 40, 26 }, { 33, 22 }, { 22, 42 }, { 42, 43 }, { 10, 8 }, { 55, 46 }, { 36, 58 } };
            for (int i = 0; i < vents.GetLength(0); i++) w.SetGround(vents[i, 0], vents[i, 1], Terrain.Vent);

            w.Place(BuildingType.Hub, HubX, HubY, 0, 0, free: true);
            return w;
        }

        /// <summary>A rectangle of resource with ragged edges (corners trimmed at random).</summary>
        static void Patch(World w, Random rng, int x0, int y0, int wd, int ht, Terrain t)
        {
            for (int y = y0; y < y0 + ht; y++)
                for (int x = x0; x < x0 + wd; x++)
                {
                    bool edge = x == x0 || y == y0 || x == x0 + wd - 1 || y == y0 + ht - 1;
                    if (edge && rng.NextDouble() < 0.35) continue;
                    w.SetGround(x, y, t);
                }
        }
    }
}
