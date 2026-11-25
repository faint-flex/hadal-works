using System.Collections.Generic;
using UnityEngine;

namespace Kilnwood.Game
{
    /// <summary>Gray-box materials, colours and text labels. Every colour cue is paired with a letter or word.</summary>
    public static class Look
    {
        public static readonly Color Grass = new Color(0.36f, 0.46f, 0.25f);
        public static readonly Color FarHills = new Color(0.3f, 0.38f, 0.26f);
        public static readonly Color ForestFloor = new Color(0.27f, 0.3f, 0.17f);
        public static readonly Color Bark = new Color(0.36f, 0.25f, 0.16f);
        public static readonly Color Leaves = new Color(0.2f, 0.42f, 0.2f);
        public static readonly Color LeavesDark = new Color(0.14f, 0.32f, 0.17f);
        public static readonly Color OreGround = new Color(0.45f, 0.3f, 0.22f);
        public static readonly Color OreRock = new Color(0.5f, 0.24f, 0.17f);
        public static readonly Color StoneGround = new Color(0.55f, 0.54f, 0.5f);
        public static readonly Color Boulder = new Color(0.68f, 0.67f, 0.63f);
        public static readonly Color Water = new Color(0.2f, 0.42f, 0.55f);
        public static readonly Color Timber = new Color(0.55f, 0.4f, 0.26f);
        public static readonly Color Planks = new Color(0.62f, 0.5f, 0.34f);
        public static readonly Color Roof = new Color(0.32f, 0.22f, 0.18f);
        public static readonly Color StoneWall = new Color(0.58f, 0.56f, 0.52f);
        public static readonly Color Brickwork = new Color(0.62f, 0.32f, 0.24f);
        public static readonly Color MineDark = new Color(0.2f, 0.19f, 0.18f);
        public static readonly Color Iron = new Color(0.35f, 0.37f, 0.4f);
        public static readonly Color Fire = new Color(1f, 0.55f, 0.15f);
        public static readonly Color FireLight = new Color(1f, 0.6f, 0.25f);
        public static readonly Color BeltFrame = new Color(0.4f, 0.3f, 0.2f);
        public static readonly Color Belt = new Color(0.22f, 0.2f, 0.18f);
        public static readonly Color Accent = new Color(1f, 0.78f, 0.15f);
        public static readonly Color Ok = new Color(0.3f, 0.85f, 0.45f);
        public static readonly Color Bad = new Color(0.95f, 0.3f, 0.25f);
        public static readonly Color Select = new Color(1f, 0.92f, 0.2f);

        static Material _base;
        static readonly Dictionary<Color, Material> _cache = new Dictionary<Color, Material>();
        static Font _font;

        /// <summary>Text lying flat on the floor; turned each frame so it always reads upright from the camera.</summary>
        public static readonly List<Transform> FlatLabels = new List<Transform>();

        public static void TurnFlatLabels(float cameraYaw)
        {
            var rot = Quaternion.Euler(90f, cameraYaw, 0f);
            for (int i = FlatLabels.Count - 1; i >= 0; i--)
            {
                var t = FlatLabels[i];
                if (t == null) { FlatLabels.RemoveAt(i); continue; }
                t.rotation = rot;
            }
        }

        /// <summary>Upright text over machines; turned to face the camera fully so it never smears at low angles.</summary>
        public static readonly List<Transform> Billboards = new List<Transform>();

        /// <summary>Faces every billboard to the camera and grows far ones so they stay readable zoomed out.</summary>
        public static void TurnBillboards(Quaternion camRot, Vector3 camPos)
        {
            for (int i = Billboards.Count - 1; i >= 0; i--)
            {
                var t = Billboards[i];
                if (t == null) { Billboards.RemoveAt(i); continue; }
                t.rotation = camRot;
                float k = Mathf.Clamp(Vector3.Distance(camPos, t.position) / 45f, 1f, 2f);
                t.localScale = new Vector3(k, k, k);
            }
        }

        /// <summary>Text that floats upright and always faces the camera.</summary>
        public static TextMesh Billboard(string text, Transform parent, Vector3 localPos, float size, Color c)
        {
            var tm = Label(text, parent, localPos, size, c, faceUp: false);
            tm.anchor = TextAnchor.LowerCenter;
            tm.richText = true;
            // Dark drop shadow behind the text so it reads over pale ground (stone, grass, sky).
            var sh = Label(text, tm.transform, new Vector3(0.06f * size, -0.06f * size, 0.05f), size, new Color(0f, 0f, 0f, 0.85f), faceUp: false);
            sh.gameObject.name = "Shadow";
            sh.anchor = TextAnchor.LowerCenter;
            sh.richText = true;
            sh.transform.localPosition = new Vector3(0.06f, -0.06f, 0.05f);
            sh.characterSize = tm.characterSize;
            Billboards.Add(tm.transform);
            return tm;
        }

        static readonly System.Text.RegularExpressions.Regex ColorTags = new System.Text.RegularExpressions.Regex("</?color[^>]*>");

        /// <summary>Sets a billboard's text and its shadow (shadow drops colour tags so it stays dark).</summary>
        public static void SetText(TextMesh tm, string text)
        {
            tm.text = text;
            if (tm.transform.childCount > 0)
            {
                var sh = tm.transform.GetChild(0).GetComponent<TextMesh>();
                if (sh != null) { sh.text = ColorTags.Replace(text, ""); sh.anchor = tm.anchor; }
            }
        }

        public static Material Mat(Color c)
        {
            if (_cache.TryGetValue(c, out var m)) return m;
            if (_base == null)
            {
                _base = Resources.Load<Material>("ND_Base");
                if (_base == null)
                {
                    var sh = Shader.Find("HDRP/Lit") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    _base = new Material(sh);
                }
            }
            m = new Material(_base);
            m.color = c;
            m.enableInstancing = true;   // thousands of trees and rocks share a few materials: draw them in batches
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            _cache[c] = m;
            return m;
        }

        /// <summary>Surface patterns for building parts (projected in world space by the KW_Tri material).</summary>
        public enum Surface { Plain, Wood, Brick, Stone, Shingle, Weathered }

        static readonly Dictionary<(Color, Surface), Material> _surf = new Dictionary<(Color, Surface), Material>();
        static Material _triBase;
        static bool _triLoaded;

        public static Material SurfMat(Color c, Surface s)
        {
            if (s == Surface.Plain) return Mat(c);
            if (_surf.TryGetValue((c, s), out var m)) return m;
            if (!_triLoaded) { _triBase = Resources.Load<Material>("KW_Tri"); _triLoaded = true; }
            if (_triBase != null && _triBase.shader != null && _triBase.shader.isSupported)
            {
                m = new Material(_triBase);
                m.SetFloat("_Scale", Art.PatternRepeats(s));
                if (m.HasProperty("_Grime"))
                {
                    m.SetTexture("_Grime", Art.Grain);
                    m.SetFloat("_GrimeAmount", s == Surface.Shingle ? 0.85f : 0.7f);
                    m.SetFloat("_GroundDirt", s == Surface.Shingle ? 0f : 0.75f);
                }
            }
            else
            {
                m = new Material(Mat(c));
                m.mainTextureScale = new Vector2(2f, 2f);
            }
            m.mainTexture = Art.PatternTexture(s);
            m.color = c * 1.2f;   // patterns average about 0.83, so lift the tint to keep the same overall colour
            m.enableInstancing = true;
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", s == Surface.Shingle ? 0.15f : 0.08f);
            // Plain grey-blue parts are iron: metallic, a bit of shine, rust in the streaks.
            float mx = Mathf.Max(c.r, Mathf.Max(c.g, c.b)), mn = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            if (s == Surface.Weathered && m.HasProperty("_Metallic") && mx - mn < 0.07f && c.b >= c.r && mx < 0.6f && mx > 0.12f)
            {
                m.SetFloat("_Metallic", 0.75f);
                m.SetFloat("_Rust", 0.55f);
                m.SetFloat("_Glossiness", 0.42f);
            }
            _surf[(c, s)] = m;
            return m;
        }

        static readonly Dictionary<Color, Material> _glow = new Dictionary<Color, Material>();

        /// <summary>A self-lit material (fire, embers) that glows even in shade.</summary>
        public static Material GlowMat(Color c)
        {
            if (_glow.TryGetValue(c, out var m)) return m;
            m = new Material(Mat(c));
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c * 2.2f);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            _glow[c] = m;
            return m;
        }

        public static Font Font
        {
            get
            {
                if (_font != null) return _font;
                try { _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
                if (_font == null) { try { _font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { } }
                return _font;
            }
        }

        public static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Color c, bool collider = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = Mat(c);
            if (!collider) Object.Destroy(go.GetComponent<Collider>());
            return go;
        }

        /// <summary>World-space text. faceUp = lies flat for a top-down camera.</summary>
        public static TextMesh Label(string text, Transform parent, Vector3 localPos, float size, Color c, bool faceUp = true, bool turnToCamera = true)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            if (faceUp)
            {
                go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                if (turnToCamera) FlatLabels.Add(go.transform);
            }
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.font = Font;
            tm.fontSize = 64;
            tm.characterSize = size / 10f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = c;
            var mr = go.GetComponent<MeshRenderer>();
            if (Font != null) mr.sharedMaterial = Font.material;
            return tm;
        }
    }
}
