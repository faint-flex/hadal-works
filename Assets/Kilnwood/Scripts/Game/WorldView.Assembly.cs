using Kilnwood.Sim;
using UnityEngine;

namespace Kilnwood.Game
{
    /// <summary>0.14 Tier 7 "Assembly Line" models: the wood-fired steam turbine, the electric furnace and the assembly plant.</summary>
    public partial class WorldView
    {
        static readonly Color TurbineGreen = new Color(0.2f, 0.3f, 0.26f);
        static readonly Color TractorOrange = new Color(0.86f, 0.46f, 0.16f);
        static readonly Color Graphite = new Color(0.16f, 0.16f, 0.17f);

        // ------------------------------------------------------------------ steam turbine

        /// <summary>A brick boiler house with a tall chimney at the back, the turbine casing and generator on a stone bed in
        /// front of it, a wood pile by the firebox and a terminal mast like the dynamo's for the wires.</summary>
        void MakeTurbine(Transform B, float s, BView v)
        {
            Color brick = Look.Brickwork, stone = Look.StoneWall, iron = Look.Iron, dark = Look.MineDark;
            float h = s / 2f;
            Art.Cube("Plinth", B, new Vector3(0f, 0.15f, 0f), new Vector3(s - 0.3f, 0.3f, s - 0.3f), stone * 0.85f);
            // Boiler house along the back half.
            Art.Cube("Boiler house", B, new Vector3(0.1f, 1.2f, -0.55f), new Vector3(1.6f, 1.9f, 0.75f), brick);
            Art.Cube("Boiler roof", B, new Vector3(0.1f, 2.2f, -0.55f), new Vector3(1.75f, 0.1f, 0.9f), Look.Roof);
            Art.Cube("Window", B, new Vector3(0.1f, 1.45f, -0.17f), new Vector3(0.7f, 0.55f, 0.04f), new Color(0.3f, 0.37f, 0.42f));
            Art.Cube("Firebox door", B, new Vector3(-0.45f, 0.65f, -0.16f), new Vector3(0.42f, 0.4f, 0.05f), dark);
            v.Fire = Art.Cube("Fire", B, new Vector3(-0.45f, 0.65f, -0.13f), new Vector3(0.32f, 0.28f, 0.03f), Look.Fire);
            Art.Cube("Chimney", B, new Vector3(0.6f, 3.0f, -0.65f), new Vector3(0.5f, 4.2f, 0.5f), brick * 0.92f);
            Art.Cube("Chimney band", B, new Vector3(0.6f, 4.6f, -0.65f), new Vector3(0.6f, 0.12f, 0.6f), stone);
            Art.Cube("Chimney cap", B, new Vector3(0.6f, 5.15f, -0.65f), new Vector3(0.62f, 0.14f, 0.62f), dark);
            v.Smoke = Art.Smoke(B, new Vector3(0.6f, 5.35f, -0.65f), 1.2f);
            // Turbine and generator on a bed along the front: steam pipe from the boiler drops into the casing.
            Art.Cube("Turbine bed", B, new Vector3(0.1f, 0.42f, 0.45f), new Vector3(1.6f, 0.24f, 0.7f), stone * 0.75f);
            Art.Cyl("Turbine casing", B, new Vector3(-0.65f, 0.86f, 0.45f), 0.62f, 0.8f, TurbineGreen, new Vector3(0f, 0f, -90f));
            Art.Cyl("Casing flange", B, new Vector3(-0.27f, 0.86f, 0.45f), 0.7f, 0.06f, Brass * 0.9f, new Vector3(0f, 0f, -90f));
            Art.Cyl("Generator", B, new Vector3(0.35f, 0.86f, 0.45f), 0.7f, 0.5f, TurbineGreen * 1.1f, new Vector3(0f, 0f, -90f));
            for (int k = 0; k < 2; k++)
                Art.Cyl("Generator band", B, new Vector3(0.42f + k * 0.3f, 0.86f, 0.45f), 0.74f, 0.06f, Brass * 0.85f, new Vector3(0f, 0f, -90f));
            Art.Cyl("Steam pipe", B, new Vector3(-0.6f, 0.95f, 0.45f), 0.12f, 0.9f, dark);
            Art.Cyl("Steam pipe over", B, new Vector3(-0.6f, 1.85f, -0.2f), 0.12f, 0.7f, dark, new Vector3(90f, 0f, 0f));
            // The coupling between turbine and generator spins while it makes power (shows it working).
            var cpl = new GameObject("Coupling").transform;
            cpl.SetParent(B, false);
            cpl.localPosition = new Vector3(0.24f, 0.86f, 0.45f);
            Art.Cyl("Coupling disc", cpl, new Vector3(-0.04f, 0f, 0f), 0.5f, 0.08f, iron * 1.3f, new Vector3(0f, 0f, -90f));
            Art.Cube("Coupling mark", cpl, new Vector3(0f, 0.2f, 0f), new Vector3(0.1f, 0.1f, 0.1f), Look.Accent);
            v.Anim = cpl;
            // Split logs stacked by the firebox.
            for (int r = 0; r < 2; r++)
                for (int c = 0; c < 2 - r; c++)
                    Art.Cyl("Log", B, new Vector3(-0.9f + c * 0.15f + r * 0.075f, 0.37f + r * 0.13f, 0.15f), 0.14f, 0.6f, Look.Bark * (0.95f + 0.05f * c), new Vector3(90f, 0f, 0f));
            // Terminal mast for the line (same place as on the dynamo, so wires land the same way).
            Art.Cyl("Terminal mast", B, new Vector3(-1.4f + 0.55f, 0.3f, -1.4f + 0.55f), 0.2f, 4.1f, Look.Bark);
            Art.Cube("Terminal arm", B, new Vector3(-0.85f, 4.32f, -0.85f), new Vector3(1.3f, 0.14f, 0.14f), Look.Timber * 0.8f);
            for (int k = -1; k <= 1; k += 2)
                Art.Cyl("Insulator", B, new Vector3(-0.85f + k * ArmHalf, 4.39f, -0.85f), 0.14f, 0.2f, Insulator);
            Front(B, h);
        }

        // ------------------------------------------------------------------ electric furnace

        /// <summary>A squat round furnace shell on a brick hearth with a domed roof; three graphite electrodes rise through
        /// the roof from a gantry, a transformer box sits at the side and the tap spout at the front glows while it melts.</summary>
        void MakeArcFurnace(Transform B, float s, BView v)
        {
            Color brick = Look.Brickwork, stone = Look.StoneWall, iron = Look.Iron, dark = Look.MineDark;
            float h = s / 2f;
            Art.Cube("Floor", B, new Vector3(0f, 0.08f, 0f), new Vector3(s - 0.3f, 0.16f, s - 0.3f), stone * 0.85f);
            Art.Cube("Hearth", B, new Vector3(-0.15f, 0.4f, -0.1f), new Vector3(1.4f, 0.5f, 1.4f), brick);
            Art.Cyl("Shell", B, new Vector3(-0.15f, 0.65f, -0.1f), 1.3f, 0.9f, iron * 0.95f);
            for (int k = 0; k < 2; k++)
                Art.Cyl("Shell band", B, new Vector3(-0.15f, 0.78f + k * 0.5f, -0.1f), 1.36f, 0.08f, dark);
            Art.Ball("Dome", B, new Vector3(-0.15f, 1.55f, -0.1f), new Vector3(1.3f, 0.55f, 1.3f), iron * 1.1f);
            // Electrodes in a triangle with the gantry beam above them.
            for (int k = 0; k < 3; k++)
            {
                float a = k * 120f * Mathf.Deg2Rad;
                Art.Cyl("Electrode", B, new Vector3(-0.15f + Mathf.Cos(a) * 0.24f, 1.6f, -0.1f + Mathf.Sin(a) * 0.24f), 0.14f, 1.4f, Graphite);
                Art.Cyl("Clamp", B, new Vector3(-0.15f + Mathf.Cos(a) * 0.24f, 2.6f, -0.1f + Mathf.Sin(a) * 0.24f), 0.22f, 0.16f, Copper);
            }
            Art.Cube("Gantry beam", B, new Vector3(0.25f, 2.78f, -0.1f), new Vector3(1.1f, 0.14f, 0.3f), iron);
            Art.Cube("Gantry post", B, new Vector3(0.78f, 1.45f, -0.1f), new Vector3(0.16f, 2.6f, 0.16f), iron);
            // Transformer with its own insulators, where the drop line lands.
            Art.Cube("Transformer", B, new Vector3(0.75f, 0.6f, -0.7f), new Vector3(0.5f, 0.9f, 0.45f), TurbineGreen);
            for (int k = 0; k < 4; k++)
                Art.Cube("Cooling fin", B, new Vector3(1.01f, 0.6f, -0.88f + k * 0.12f), new Vector3(0.04f, 0.7f, 0.06f), TurbineGreen * 0.8f);
            Art.Cube("Service bracket", B, new Vector3(0.75f, 2.1f, -0.7f), new Vector3(0.1f, 2.2f, 0.1f), iron);
            Art.Cyl("Insulator", B, new Vector3(0.75f, 3.2f, -0.7f), 0.14f, 0.2f, Insulator);
            // Tap spout over a ladle at the front; the spout and ladle glow while it melts.
            Art.Cube("Tap spout", B, new Vector3(-0.15f, 0.85f, 0.6f), new Vector3(0.22f, 0.1f, 0.5f), dark, new Vector3(18f, 0f, 0f));
            Art.Cyl("Ladle", B, new Vector3(-0.15f, 0.16f, 0.85f), 0.5f, 0.4f, dark);
            v.Fire = Art.Cyl("Melt", B, new Vector3(-0.15f, 0.56f, 0.85f), 0.42f, 0.02f, Look.Fire);
            Front(B, h);
        }

        // ------------------------------------------------------------------ assembly plant

        /// <summary>A long brick hall (3x3) with a gabled roof and a glazed roof monitor along the ridge, tall windows down
        /// both sides, a big door at the front with a finished tractor parked beside it, and an overhead crane rail.</summary>
        void MakeAssembly(Transform B, float s, BView v)
        {
            Color brick = Look.Brickwork, stone = Look.StoneWall, iron = Look.Iron, glass = new Color(0.3f, 0.37f, 0.42f);
            float h = s / 2f;
            const float wallW = 2.6f, wallD = 2.6f, wallZ = -0.2f, eave = 3.0f;   // 0.14.1: bigger, so it fills its 3x3 pad
            Art.Cube("Floor", B, new Vector3(0f, 0.08f, 0f), new Vector3(s - 0.3f, 0.16f, s - 0.3f), stone * 0.85f);
            Art.Cube("Walls", B, new Vector3(0f, eave / 2f + 0.1f, wallZ), new Vector3(wallW, eave, wallD), brick);
            Art.Cube("Plinth course", B, new Vector3(0f, 0.3f, wallZ), new Vector3(wallW + 0.1f, 0.3f, wallD + 0.1f), stone * 0.8f);
            Art.Cube("Cornice", B, new Vector3(0f, eave + 0.08f, wallZ), new Vector3(wallW + 0.12f, 0.12f, wallD + 0.12f), brick * 0.85f);
            // Gabled roof running front to back (ridge along z), brick gable ends.
            const float pitch = 26f;
            float run = wallW / 2f + 0.15f, rise = run * Mathf.Tan(pitch * Mathf.Deg2Rad), slab = run / Mathf.Cos(pitch * Mathf.Deg2Rad);
            for (int k = -1; k <= 1; k += 2)
                Art.Cube("Roof", B, new Vector3(k * run / 2f, eave + 0.15f + rise / 2f, wallZ), new Vector3(slab + 0.05f, 0.09f, wallD + 0.3f), Look.Roof, new Vector3(0f, 0f, -k * pitch));
            for (int k = -1; k <= 1; k += 2)
            {
                var gable = new GameObject("Gable").transform;
                gable.SetParent(B, false);
                gable.localPosition = new Vector3(0f, eave + 0.1f, wallZ + k * (wallD / 2f - 0.06f));
                gable.localScale = new Vector3(wallW / 1.414f * 0.98f, rise / 0.707f * 0.98f, 1f);
                Art.Cube("Gable fill", gable, Vector3.zero, new Vector3(1f, 1f, 0.1f), brick, new Vector3(0f, 0f, 45f));
            }
            // Roof monitor: a raised glazed lantern along the ridge, lit at night.
            Art.Cube("Monitor", B, new Vector3(0f, eave + rise + 0.32f, wallZ), new Vector3(0.7f, 0.32f, wallD - 0.4f), Look.Roof * 0.9f);
            for (int k = -1; k <= 1; k += 2)
                Art.Cube("Window", B, new Vector3(k * 0.36f, eave + rise + 0.3f, wallZ), new Vector3(0.04f, 0.22f, wallD - 0.5f), glass);
            Art.Cube("Monitor cap", B, new Vector3(0f, eave + rise + 0.52f, wallZ), new Vector3(0.9f, 0.07f, wallD - 0.3f), Look.Roof * 0.8f);
            // Tall side windows with stone sills and glazing bars.
            for (int k = -1; k <= 1; k += 2)
                for (int j = 0; j < 3; j++)
                {
                    float z = wallZ - 0.8f + j * 0.8f;
                    Art.Cube("Window", B, new Vector3(k * (wallW / 2f + 0.01f), 1.75f, z), new Vector3(0.04f, 1.4f, 0.5f), glass);
                    Art.Cube("Glazing bar", B, new Vector3(k * (wallW / 2f + 0.03f), 1.75f, z), new Vector3(0.03f, 1.4f, 0.04f), iron);
                    Art.Cube("Sill", B, new Vector3(k * (wallW / 2f + 0.05f), 0.97f, z), new Vector3(0.1f, 0.06f, 0.6f), stone);
                }
            // Big doors at the front and a finished tractor waiting beside them.
            Art.Cube("Door frame", B, new Vector3(0f, 1.15f, wallZ + wallD / 2f + 0.02f), new Vector3(1.4f, 2.0f, 0.06f), stone * 0.9f);
            Art.Cube("Door", B, new Vector3(0f, 1.05f, wallZ + wallD / 2f + 0.05f), new Vector3(1.2f, 1.8f, 0.05f), Look.Roof);
            Art.Cube("Door line", B, new Vector3(0f, 1.05f, wallZ + wallD / 2f + 0.08f), new Vector3(0.04f, 1.8f, 0.02f), Look.Roof * 0.6f);
            var t = new GameObject("Tractor on show").transform;
            t.SetParent(B, false);
            t.localPosition = new Vector3(1.0f, 0.16f, h - 0.17f);
            t.localScale = Vector3.one * 1.1f;
            BuildTractor(t, TractorOrange);
            // Overhead crane rail out of the side wall for loading.
            Art.Cube("Crane rail", B, new Vector3(-1.5f, 2.6f, wallZ + 0.4f), new Vector3(0.6f, 0.12f, 0.12f), iron);
            Art.Cube("Crane hook", B, new Vector3(-1.7f, 2.25f, wallZ + 0.4f), new Vector3(0.05f, 0.6f, 0.05f), Look.MineDark);
            // Service bracket at the back corner for the drop line.
            Art.Cube("Service bracket", B, new Vector3(-1.4f, 3.6f, -1.42f), new Vector3(0.1f, 1.6f, 0.1f), iron);
            Art.Cyl("Insulator", B, new Vector3(-1.4f, 4.4f, -1.42f), 0.14f, 0.2f, Insulator);
            Front(B, h);
        }

        /// <summary>A small farm tractor, nose toward +x: big rear wheels, small front wheels, bonnet, cab seat and exhaust.
        /// Used for the tractor item on belts and the one parked outside the assembly plant.</summary>
        static void BuildTractor(Transform t, Color body)
        {
            var tyre = new Color(0.13f, 0.12f, 0.12f);
            var hub = new Color(0.82f, 0.74f, 0.5f);
            Art.Cube("Chassis", t, new Vector3(0.02f, 0.2f, 0f), new Vector3(0.56f, 0.1f, 0.18f), Look.MineDark);
            Art.Cube("Bonnet", t, new Vector3(0.12f, 0.3f, 0f), new Vector3(0.34f, 0.16f, 0.18f), body);
            Art.Cube("Grille", t, new Vector3(0.3f, 0.3f, 0f), new Vector3(0.03f, 0.14f, 0.16f), Look.Iron);
            Art.Cube("Seat box", t, new Vector3(-0.14f, 0.32f, 0f), new Vector3(0.16f, 0.12f, 0.2f), body * 0.9f);
            Art.Cube("Seat back", t, new Vector3(-0.21f, 0.42f, 0f), new Vector3(0.03f, 0.12f, 0.14f), Look.MineDark);
            Art.Cyl("Steering", t, new Vector3(-0.06f, 0.37f, 0f), 0.02f, 0.12f, Look.MineDark, new Vector3(0f, 0f, 30f));
            Art.Cyl("Exhaust", t, new Vector3(0.18f, 0.38f, 0.05f), 0.035f, 0.16f, Look.MineDark);
            for (int k = -1; k <= 1; k += 2)
            {
                // Cyl grows from its base along +z here, so start each wheel at its inner (k < 0: outer) face.
                Art.Cyl("Rear wheel", t, new Vector3(-0.15f, 0.17f, k * 0.15f - 0.04f), 0.34f, 0.08f, tyre, new Vector3(90f, 0f, 0f));
                Art.Cyl("Rear hub", t, new Vector3(-0.15f, 0.17f, k > 0 ? 0.19f : -0.205f), 0.14f, 0.015f, hub, new Vector3(90f, 0f, 0f));
                Art.Cyl("Front wheel", t, new Vector3(0.22f, 0.09f, k * 0.1f - 0.03f), 0.18f, 0.06f, tyre, new Vector3(90f, 0f, 0f));
            }
        }
    }
}
