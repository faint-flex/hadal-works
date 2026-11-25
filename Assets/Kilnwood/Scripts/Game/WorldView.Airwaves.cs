using Kilnwood.Sim;
using UnityEngine;

namespace Kilnwood.Game
{
    /// <summary>0.16 Tier 9 "Airwaves" models: the hydro dam, the glassworks, the radio works with its mast, and the radio set.</summary>
    public partial class WorldView
    {
        static readonly Color Concrete = new Color(0.66f, 0.65f, 0.6f);
        static readonly Color Walnut = new Color(0.5f, 0.32f, 0.18f);
        static readonly Color GlassTint = new Color(0.66f, 0.84f, 0.86f);
        static readonly Color SpeakerCloth = new Color(0.78f, 0.7f, 0.52f);
        static readonly Color MastRed = new Color(0.95f, 0.16f, 0.12f);

        // ------------------------------------------------------------------ hydro dam

        /// <summary>A concrete weir across the river behind the plot (the back faces the water), the held-back water higher
        /// on one side and white water tumbling down the other, two penstocks into a concrete powerhouse with tall
        /// windows, and a transformer yard whose mast takes the line. A governor wheel on the side turns all the time.</summary>
        void MakeHydroDam(Transform R, float s, BView v)
        {
            var B = Scaled(R); s /= T;
            Color iron = Look.Iron, dark = Look.MineDark, glass = new Color(0.3f, 0.37f, 0.42f);
            float h = s / 2f;
            Art.Cube("Footing", B, new Vector3(0f, 0.1f, 0f), new Vector3(s - 0.2f, 0.2f, s - 0.2f), Concrete * 0.85f);
            // Weir across the river: the river is up to three tiles wide behind the back edge.
            const float wz = -2.45f, wlen = 3.1f;
            Art.Cube("Weir", B, new Vector3(0f, 0.2f, wz), new Vector3(0.5f, 1.0f, wlen), Concrete);
            Art.Cube("Weir crest", B, new Vector3(0f, 0.72f, wz), new Vector3(0.62f, 0.08f, wlen + 0.1f), Concrete * 1.08f);
            for (int k = 0; k < 4; k++)
                Art.Cube("Pier", B, new Vector3(0f, 0.55f, wz - wlen / 2f + 0.25f + k * (wlen - 0.5f) / 3f), new Vector3(0.7f, 0.7f, 0.16f), Concrete * 0.95f);
            Art.Cube("Walkway", B, new Vector3(0f, 0.95f, wz), new Vector3(0.3f, 0.05f, wlen + 0.1f), iron);
            for (int k = -1; k <= 1; k += 2)
                Art.Cube("Rail", B, new Vector3(k * 0.14f, 1.12f, wz), new Vector3(0.025f, 0.03f, wlen + 0.1f), iron * 0.8f);
            // Held-back water on the upstream side, white water on the downstream side.
            Art.Cube("Head water", B, new Vector3(0.75f, 0.58f, wz), new Vector3(1.0f, 0.05f, wlen), Look.Water * 1.05f);
            for (int k = 0; k < 6; k++)
                Art.Cube("White water", B, new Vector3(-0.42f - 0.08f * (k % 2), 0.35f - 0.04f * k, wz - wlen / 2f + 0.3f + k * 0.5f), new Vector3(0.38f, 0.18f, 0.42f),
                    new Color(0.86f, 0.9f, 0.92f), new Vector3(0f, 0f, 30f));
            var spray = Art.Smoke(R, new Vector3(-0.8f, 0.2f, wz) * T, 1f);
            var main = spray.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 1.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            var shape = spray.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.6f, wlen * T * 0.9f, 0.2f);   // the smoke system points up, so its local y runs along the weir
            var em = spray.emission;
            em.rateOverTime = 16f;
            // Penstocks from the weir into the powerhouse.
            for (int k = -1; k <= 1; k += 2)
            {
                Art.Cyl("Penstock", B, new Vector3(k * 0.4f, 0.42f, -0.95f), 0.3f, 0.8f, iron * 0.9f, new Vector3(90f, 0f, 0f));
                Art.Cyl("Penstock flange", B, new Vector3(k * 0.4f, 0.42f, -0.62f), 0.38f, 0.05f, dark, new Vector3(90f, 0f, 0f));
            }
            // Powerhouse: concrete hall with tall arched-head windows down the front and sides, flat roof with a parapet.
            const float pw = 1.7f, pd = 1.2f, pz = 0.05f, ph = 1.5f;
            Art.Cube("Walls", B, new Vector3(0f, 0.2f + ph / 2f, pz), new Vector3(pw, ph, pd), Concrete * 1.02f);
            Art.Cube("Plinth course", B, new Vector3(0f, 0.32f, pz), new Vector3(pw + 0.06f, 0.24f, pd + 0.06f), Concrete * 0.82f);
            Art.Cube("Roof", B, new Vector3(0f, 0.2f + ph + 0.04f, pz), new Vector3(pw + 0.1f, 0.08f, pd + 0.1f), Concrete * 0.75f);
            Art.Cube("Parapet", B, new Vector3(0f, 0.2f + ph + 0.14f, pz + pd / 2f + 0.03f), new Vector3(pw + 0.1f, 0.14f, 0.06f), Concrete * 0.9f);
            for (int k = -1; k <= 1; k++)
            {
                Art.Cube("Window", B, new Vector3(k * 0.5f, 1.05f, pz + pd / 2f + 0.01f), new Vector3(0.3f, 0.85f, 0.03f), glass);
                Art.Cyl("Window", B, new Vector3(k * 0.5f, 1.47f, pz + pd / 2f + 0.01f), 0.3f, 0.03f, glass, new Vector3(90f, 0f, 0f));
                Art.Cube("Mullion", B, new Vector3(k * 0.5f, 1.05f, pz + pd / 2f + 0.03f), new Vector3(0.025f, 0.85f, 0.02f), Concrete * 0.8f);
            }
            for (int k = -1; k <= 1; k += 2)
                Art.Cube("Window", B, new Vector3(k * (pw / 2f + 0.01f), 1.05f, pz), new Vector3(0.03f, 0.85f, 0.5f), glass);
            Art.Cube("Name band", B, new Vector3(0f, 1.62f, pz + pd / 2f + 0.02f), new Vector3(1.2f, 0.12f, 0.02f), Concrete * 0.7f);
            // Governor wheel on the side wall: turns whenever the river does (always).
            var gov = new GameObject("Governor").transform;
            gov.SetParent(B, false);
            gov.localPosition = new Vector3(pw / 2f + 0.1f, 0.75f, pz + 0.3f);
            Art.Cyl("Rim", gov, Vector3.zero, 0.5f, 0.05f, dark, new Vector3(0f, 0f, -90f));
            Art.Cube("Spoke", gov, Vector3.zero, new Vector3(0.04f, 0.46f, 0.05f), iron * 1.2f);
            Art.Cube("Spoke", gov, Vector3.zero, new Vector3(0.04f, 0.05f, 0.46f), iron * 1.2f);
            Art.Cube("Rim mark", gov, new Vector3(0.02f, 0.22f, 0f), new Vector3(0.05f, 0.06f, 0.08f), Look.Accent);
            v.Anim = gov;
            // Transformer yard at the front corner, with the mast the line lands on.
            Art.Cube("Transformer", B, new Vector3(-0.68f, 0.5f, 0.82f), new Vector3(0.4f, 0.6f, 0.3f), TurbineGreen);
            for (int k = 0; k < 3; k++)
                Art.Cube("Cooling fin", B, new Vector3(-0.92f, 0.5f, 0.72f + k * 0.1f), new Vector3(0.04f, 0.5f, 0.05f), TurbineGreen * 0.8f);
            Art.Cube("Service bracket", B, new Vector3(0.75f, 1.9f, -0.55f), new Vector3(0.06f, 0.9f, 0.06f), iron);
            Art.Cyl("Insulator", B, new Vector3(0.75f, 2.4f, -0.55f), 0.08f, 0.12f, Insulator);
            Front(R, h * T);
        }

        // ------------------------------------------------------------------ glassworks

        /// <summary>A brick glass cone (a tall tapering kiln house) over the melting furnace, a lean-to packing shed at the
        /// front with a window, the furnace mouth glowing while it melts, and racks of finished panes.</summary>
        void MakeGlassworks(Transform R, float s, BView v)
        {
            var B = Scaled(R); s /= T;
            Color brick = Look.Brickwork, stone = Look.StoneWall, iron = Look.Iron, dark = Look.MineDark;
            float h = s / 2f;
            Art.Cube("Floor", B, new Vector3(0f, 0.08f, 0f), new Vector3(s - 0.3f, 0.16f, s - 0.3f), stone * 0.85f);
            // The cone: a brick drum, then a long frustum to a narrow open top.
            Art.Cyl("Cone base", B, new Vector3(-0.15f, 0.45f, -0.25f), 1.35f, 0.6f, brick * 0.95f);
            Art.Part("Cone", B, Art.Frustum, new Vector3(-0.15f, 0.75f, -0.25f), new Vector3(1.3f, 2.4f, 1.3f), brick);
            Art.Cyl("Cone band", B, new Vector3(-0.15f, 1.2f, -0.25f), 1.18f, 0.08f, stone);
            Art.Cyl("Cone band", B, new Vector3(-0.15f, 2.1f, -0.25f), 0.98f, 0.08f, stone);
            Art.Cyl("Cone lip", B, new Vector3(-0.15f, 3.14f, -0.25f), 0.86f, 0.1f, dark);
            v.Smoke = Art.Smoke(R, new Vector3(-0.15f, 3.25f, -0.25f) * T, 1.1f);
            // Furnace mouth at the front of the cone, glowing while it melts.
            Art.Cube("Arch", B, new Vector3(-0.15f, 0.58f, 0.43f), new Vector3(0.62f, 0.74f, 0.08f), stone * 0.9f);
            Art.Cube("Mouth", B, new Vector3(-0.15f, 0.52f, 0.46f), new Vector3(0.46f, 0.56f, 0.04f), dark);
            v.Fire = Art.Cube("Melt glow", B, new Vector3(-0.15f, 0.52f, 0.49f), new Vector3(0.4f, 0.5f, 0.03f), Look.Fire);
            // Lean-to packing shed on the right with a window and a sloping roof.
            Art.Cube("Shed", B, new Vector3(0.62f, 0.55f, 0.2f), new Vector3(0.6f, 0.8f, 1.1f), brick * 0.9f);
            Art.Cube("Shed roof", B, new Vector3(0.62f, 1.02f, 0.2f), new Vector3(0.72f, 0.06f, 1.2f), Look.Roof, new Vector3(0f, 0f, 12f));
            Art.Cube("Window", B, new Vector3(0.62f, 0.62f, 0.76f), new Vector3(0.36f, 0.32f, 0.03f), new Color(0.3f, 0.37f, 0.42f));
            // Racks of finished panes leaning by the shed.
            for (int k = 0; k < 4; k++)
                Art.Cube("Pane", B, new Vector3(0.45f + k * 0.1f, 0.42f, 0.84f), new Vector3(0.03f, 0.45f, 0.3f), GlassTint * (1f - 0.04f * k), new Vector3(0f, 0f, -12f));
            Art.Cube("Rack", B, new Vector3(0.6f, 0.2f, 0.84f), new Vector3(0.5f, 0.05f, 0.34f), Look.Timber);
            Art.Cube("Service bracket", B, new Vector3(0.75f, 1.45f, -0.7f), new Vector3(0.06f, 1.3f, 0.06f), iron);
            Art.Cyl("Insulator", B, new Vector3(0.75f, 2.15f, -0.7f), 0.08f, 0.12f, Insulator);
            Front(R, h * T);
        }

        // ------------------------------------------------------------------ radio works

        /// <summary>A two-storey brick works with a sawtooth roof of north lights, rows of windows, finished radio sets on a
        /// bench by the door, and a lattice radio mast at the back corner with a red light that blinks while it has power.</summary>
        void MakeRadioWorks(Transform R, float s, BView v)
        {
            var B = Scaled(R); s /= T;
            Color brick = Look.Brickwork, stone = Look.StoneWall, iron = Look.Iron, glass = new Color(0.3f, 0.37f, 0.42f);
            float h = s / 2f;
            const float ww = 1.6f, wd = 1.3f, wz = 0.05f, eave = 1.6f;
            Art.Cube("Floor", B, new Vector3(0f, 0.08f, 0f), new Vector3(s - 0.3f, 0.16f, s - 0.3f), stone * 0.85f);
            Art.Cube("Walls", B, new Vector3(-0.1f, eave / 2f + 0.1f, wz), new Vector3(ww, eave, wd), brick);
            Art.Cube("Plinth course", B, new Vector3(-0.1f, 0.25f, wz), new Vector3(ww + 0.06f, 0.2f, wd + 0.06f), stone * 0.8f);
            Art.Cube("String course", B, new Vector3(-0.1f, 0.92f, wz), new Vector3(ww + 0.04f, 0.05f, wd + 0.04f), stone);
            // Two rows of windows on the front.
            for (int row = 0; row < 2; row++)
                for (int k = -2; k <= 2; k++)
                    Art.Cube("Window", B, new Vector3(-0.1f + k * 0.3f, 0.55f + row * 0.65f, wz + wd / 2f + 0.01f), new Vector3(0.18f, 0.34f, 0.03f), glass);
            // Sawtooth roof: three teeth, steep glazed faces toward the back.
            for (int k = 0; k < 3; k++)
            {
                float z = wz - wd / 2f + 0.22f + k * 0.43f;
                Art.Cube("Roof", B, new Vector3(-0.1f, eave + 0.24f, z + 0.04f), new Vector3(ww + 0.06f, 0.05f, 0.5f), Look.Roof, new Vector3(-30f, 0f, 0f));
                Art.Cube("Window", B, new Vector3(-0.1f, eave + 0.24f, z - 0.2f), new Vector3(ww - 0.1f, 0.3f, 0.03f), glass);
            }
            Art.Cube("Door", B, new Vector3(0.45f, 0.42f, wz + wd / 2f + 0.02f), new Vector3(0.3f, 0.55f, 0.03f), Look.Timber * 0.7f);
            // A bench of finished radio sets by the door.
            Art.Cube("Bench", B, new Vector3(-0.45f, 0.27f, 0.86f), new Vector3(0.7f, 0.06f, 0.22f), Look.Timber);
            for (int k = 0; k < 3; k++)
            {
                var r = new GameObject("Radio on show").transform;
                r.SetParent(B, false);
                r.localPosition = new Vector3(-0.68f + k * 0.23f, 0.3f, 0.86f);
                r.localScale = Vector3.one * 0.32f;
                BuildRadio(r, Walnut * (1f - 0.05f * k));
            }
            // Lattice mast at the back-right corner: four legs leaning in, cross braces, and the red light on top.
            const float mh = 4.4f, mx = 0.68f, mz = -0.68f;   // 0.16.1: taller and stouter so it reads as a mast
            for (int k = 0; k < 4; k++)
            {
                float sx = (k & 1) == 0 ? -1f : 1f, sz = (k & 2) == 0 ? -1f : 1f;
                Art.Cube("Mast leg", B, new Vector3(mx + sx * 0.1f, 0.15f + mh / 2f, mz + sz * 0.1f), new Vector3(0.05f, mh, 0.05f), MastRed * 0.85f,
                    new Vector3(sz * 2.2f, 0f, -sx * 2.2f));
            }
            for (int k = 0; k < 10; k++)
            {
                float y = 0.4f + k * 0.42f;
                Art.Cube("Mast brace", B, new Vector3(mx, y, mz), new Vector3(0.34f - k * 0.022f, 0.035f, 0.035f), (k % 2 == 0 ? Color.white * 0.9f : MastRed * 0.85f), new Vector3(0f, 0f, (k % 2 == 0 ? 35f : -35f)));
                Art.Cube("Mast brace", B, new Vector3(mx, y, mz), new Vector3(0.035f, 0.035f, 0.34f - k * 0.022f), (k % 2 == 0 ? Color.white * 0.9f : MastRed * 0.85f), new Vector3((k % 2 == 0 ? 35f : -35f), 0f, 0f));
            }
            Art.Cyl("Mast top", B, new Vector3(mx, 0.15f + mh + 0.08f, mz), 0.1f, 0.16f, iron);
            v.Extra = Art.Ball("Mast light", B, new Vector3(mx, 0.15f + mh + 0.22f, mz), new Vector3(0.22f, 0.22f, 0.22f), MastRed);
            var lr = v.Extra.GetComponent<Renderer>();
            lr.sharedMaterial = Look.GlowMat(MastRed);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Art.Cube("Aerial wire", B, new Vector3(mx - 0.55f, 0.15f + mh * 0.62f, mz), new Vector3(1.2f, 0.012f, 0.012f), Look.MineDark, new Vector3(0f, 0f, -62f));
            Art.Cube("Service bracket", B, new Vector3(-0.85f, 1.6f, -0.7f), new Vector3(0.06f, 1.3f, 0.06f), iron);
            Art.Cyl("Insulator", B, new Vector3(-0.85f, 2.3f, -0.7f), 0.08f, 0.12f, Insulator);
            Front(R, h * T);
        }

        /// <summary>A table radio, front toward +z: a walnut cabinet with a rounded top, speaker cloth, a brass dial and two knobs.</summary>
        static void BuildRadio(Transform t, Color wood)
        {
            Art.Cube("Cabinet", t, new Vector3(0f, 0.32f, 0f), new Vector3(0.8f, 0.6f, 0.42f), wood);
            Art.Cyl("Cabinet top", t, new Vector3(0f, 0.62f, 0f), 0.8f, 0.42f, wood * 1.05f, new Vector3(90f, 0f, 0f));
            Art.Cube("Plinth", t, new Vector3(0f, 0.03f, 0f), new Vector3(0.86f, 0.06f, 0.46f), wood * 0.7f);
            Art.Cube("Speaker cloth", t, new Vector3(-0.12f, 0.46f, 0.215f), new Vector3(0.44f, 0.38f, 0.02f), SpeakerCloth);
            for (int k = 0; k < 3; k++)
                Art.Cube("Grille bar", t, new Vector3(-0.12f, 0.36f + k * 0.1f, 0.228f), new Vector3(0.46f, 0.025f, 0.015f), wood * 0.8f);
            Art.Cyl("Dial", t, new Vector3(0.24f, 0.48f, 0.215f), 0.2f, 0.02f, Brass, new Vector3(90f, 0f, 0f));
            for (int k = -1; k <= 1; k += 2)
                Art.Cyl("Knob", t, new Vector3(0.24f + k * 0.08f, 0.2f, 0.23f), 0.08f, 0.05f, Look.MineDark, new Vector3(90f, 0f, 0f));
        }

        // ------------------------------------------------------------------ animation

        /// <summary>The dam's governor always turns; the radio mast's light blinks while the works has electricity.</summary>
        void UpdateAirwavesViews()
        {
            float dt = Time.deltaTime, now = Time.time;
            foreach (var b in _w.Buildings.Values)
            {
                if (b.Type != BuildingType.HydroDam && b.Type != BuildingType.RadioWorks) continue;
                if (!_views.TryGetValue(b.Id, out var v)) continue;
                if (b.Type == BuildingType.HydroDam)
                {
                    if (v.Anim != null && Time.timeScale > 0f) { v.AnimAngle += dt * 180f; v.Anim.localRotation = Quaternion.Euler(v.AnimAngle, 0f, 0f); }
                }
                else if (v.Extra != null)
                {
                    bool on = b.Elec > 0f && Mathf.Repeat(now + v.Seed, 1.6f) < 0.8f;
                    if (v.Extra.activeSelf != on) v.Extra.SetActive(on);
                }
            }
        }
    }
}
