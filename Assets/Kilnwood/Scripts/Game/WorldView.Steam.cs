using Kilnwood.Sim;
using UnityEngine;

namespace Kilnwood.Game
{
    /// <summary>0.7 Tier 3 "Steam" models: steam engine (boiler, chimney, flywheel) and steel furnace, and their motion.</summary>
    public partial class WorldView
    {
        // ------------------------------------------------------------------ steam engine

        void MakeSteamEngine(Transform B, float s, BView v)
        {
            Color brick = Look.Brickwork, stone = Look.StoneWall, iron = Look.Iron, dark = Look.MineDark;
            float h = s / 2f;
            Art.Cube("Plinth", B, new Vector3(0f, 0.15f, 0f), new Vector3(s - 0.3f, 0.3f, s - 0.3f), stone * 0.85f);
            // Boiler: a long riveted drum lying front to back on a brick setting, firebox door at the front.
            Art.Cube("Setting", B, new Vector3(-0.5f, 0.75f, 0f), new Vector3(1.9f, 0.9f, s - 0.8f), brick);
            Art.Cyl("Boiler", B, new Vector3(-0.5f, 1.85f, 0f), 1.6f, s - 0.9f, iron * 1.25f, new Vector3(90f, 0f, 0f));
            for (int k = -1; k <= 1; k++)
                Art.Cyl("Boiler band", B, new Vector3(-0.5f, 1.85f, k * 1.0f), 1.66f, 0.1f, dark, new Vector3(90f, 0f, 0f));
            Art.Cyl("Steam dome", B, new Vector3(-0.5f, 2.6f, -0.4f), 0.55f, 0.5f, iron * 1.4f);
            Art.Ball("Dome cap", B, new Vector3(-0.5f, 2.85f, -0.4f), new Vector3(0.55f, 0.3f, 0.55f), iron * 1.4f);
            Art.Cube("Firebox door", B, new Vector3(-0.5f, 0.75f, h - 0.38f), new Vector3(0.8f, 0.6f, 0.06f), dark);
            v.Fire = Art.Cube("Fire", B, new Vector3(-0.5f, 0.75f, h - 0.34f), new Vector3(0.55f, 0.38f, 0.04f), Look.Fire);
            // Chimney at the back: tall brick stack with an iron cap.
            Art.Cube("Chimney", B, new Vector3(-0.5f, 3.0f, -h + 0.6f), new Vector3(0.75f, 5.4f, 0.75f), brick * 0.92f);
            Art.Cube("Chimney cap", B, new Vector3(-0.5f, 5.75f, -h + 0.6f), new Vector3(0.95f, 0.2f, 0.95f), dark);
            v.Smoke = Art.Smoke(B, new Vector3(-0.5f, 6.0f, -h + 0.6f), 1.4f);
            // Engine: cylinder on a stone bed beside the boiler, rod to a big flywheel.
            Art.Cube("Engine bed", B, new Vector3(1.15f, 0.5f, 0f), new Vector3(1.1f, 0.4f, s - 0.8f), stone * 0.75f);
            Art.Cyl("Cylinder", B, new Vector3(1.15f, 1.05f, 0.7f), 0.6f, 1.2f, iron, new Vector3(90f, 0f, 0f));
            Art.Cyl("Steam pipe", B, new Vector3(0.3f, 2.4f, -0.4f), 0.14f, 1.6f, dark, new Vector3(0f, 0f, 90f));
            Art.Cyl("Steam pipe down", B, new Vector3(1.1f, 1.75f, -0.4f), 0.14f, 1.3f, dark);
            var fly = new GameObject("Flywheel").transform;
            fly.SetParent(B, false);
            fly.localPosition = new Vector3(1.15f, 1.45f, -0.75f);
            Art.Cyl("Rim", fly, Vector3.zero, 2.2f, 0.18f, dark, new Vector3(0f, 0f, 90f));
            Art.Cyl("Hub", fly, Vector3.zero, 0.36f, 0.3f, iron * 1.3f, new Vector3(0f, 0f, 90f));
            for (int k = 0; k < 3; k++)
                Art.Cube("Spoke", fly, Vector3.zero, new Vector3(0.1f, 1.9f, 0.12f), iron * 1.15f, new Vector3(k * 60f, 0f, 0f));
            Art.Cube("Rim light", fly, new Vector3(0f, 1.0f, 0f), new Vector3(0.22f, 0.2f, 0.3f), Look.Accent);   // shows it turning
            v.Anim = fly;
            Art.Cube("Coal bunker", B, new Vector3(1.2f, 0.45f, h - 0.55f), new Vector3(1.0f, 0.6f, 0.7f), Look.Timber * 0.8f);
            Art.RockAt("Coal", B, new Vector3(1.2f, 0.8f, h - 0.55f), new Vector3(0.8f, 0.3f, 0.55f), WorldView.ItemColor(Item.Coal), 0.4f);
            Front(B, h);
        }

        // ------------------------------------------------------------------ steel furnace

        void MakeFurnace(Transform B, float s, BView v)
        {
            Color brick = Look.Brickwork, stone = Look.StoneWall, iron = Look.Iron, dark = Look.MineDark;
            float h = s / 2f;
            Art.Cube("Plinth", B, new Vector3(0f, 0.15f, 0f), new Vector3(s - 0.3f, 0.3f, s - 0.3f), stone * 0.8f);
            // A squat square brick furnace with a tall tapered stack, tie rods at the corners.
            Art.Cube("Hearth", B, new Vector3(0f, 1.2f, -0.2f), new Vector3(2.7f, 1.9f, 2.5f), brick * 0.95f);
            for (int i = 0; i < 4; i++)
                Art.Cube("Tie rod", B, new Vector3(i % 2 == 0 ? -1.38f : 1.38f, 1.2f, i < 2 ? -1.47f : 1.07f), new Vector3(0.1f, 2.0f, 0.1f), dark);
            Art.Cube("Crown", B, new Vector3(0f, 2.25f, -0.2f), new Vector3(2.9f, 0.25f, 2.7f), stone * 0.7f);
            Art.Part("Stack", B, Art.Frustum, new Vector3(0f, 2.35f, -0.5f), new Vector3(1.5f, 4.2f, 1.5f), brick * 0.85f);
            Art.Cyl("Stack band", B, new Vector3(0f, 4.2f, -0.5f), 1.2f, 0.12f, iron);
            v.Smoke = Art.Smoke(B, new Vector3(0f, 6.6f, -0.5f), 1.3f);
            // Pouring mouth, glowing, and a ladle hanging from a little crane.
            Art.Cube("Mouth", B, new Vector3(0f, 0.95f, 1.02f), new Vector3(1.0f, 0.8f, 0.2f), dark);
            v.Fire = Art.Cube("Glow", B, new Vector3(0f, 0.95f, 1.14f), new Vector3(0.75f, 0.55f, 0.04f), Look.Fire);
            Art.Cube("Crane post", B, new Vector3(1.55f, 1.4f, 1.3f), new Vector3(0.18f, 2.5f, 0.18f), Look.Timber * 0.8f);
            Art.Cube("Crane arm", B, new Vector3(1.1f, 2.6f, 1.3f), new Vector3(1.1f, 0.14f, 0.14f), Look.Timber * 0.8f);
            Art.Cyl("Ladle", B, new Vector3(0.75f, 1.6f, 1.3f), 0.55f, 0.45f, dark);
            // Finished steel bars on a rack at the side.
            for (int k = 0; k < 4; k++)
                Art.Part("Steel bar", B, Art.Ingot, new Vector3(-1.45f, 0.4f + (k / 2) * 0.18f, 1.2f + (k % 2) * 0.38f), new Vector3(0.32f, 0.16f, 0.75f), WorldView.ItemColor(Item.Steel));
            Front(B, h);
        }

        void UpdateSteamViews()
        {
            float dt = Time.deltaTime;
            foreach (var b in _w.Buildings.Values)
            {
                if (b.Type != BuildingType.SteamEngine || b.Status != "Running") continue;
                if (!_views.TryGetValue(b.Id, out var v) || v.Anim == null) continue;
                v.AnimAngle += dt * 140f;
                v.Anim.localRotation = Quaternion.Euler(v.AnimAngle, 0f, 0f);
            }
        }
    }
}
