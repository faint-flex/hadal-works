using System.Collections.Generic;
using UnityEngine;

namespace HadalWorks.Game
{
    /// <summary>Gray-box materials, colours and text labels. Every colour cue is paired with a letter or word.</summary>
    public static class Look
    {
        public static readonly Color Seabed = new Color(0.16f, 0.19f, 0.2f);
        public static readonly Color FerriteGround = new Color(0.42f, 0.22f, 0.16f);
        public static readonly Color SilicaGround = new Color(0.72f, 0.68f, 0.55f);
        public static readonly Color KelpGround = new Color(0.08f, 0.32f, 0.3f);
        public static readonly Color KelpGlow = new Color(0.25f, 0.95f, 0.85f);
        public static readonly Color Vent = new Color(0.08f, 0.07f, 0.07f);
        public static readonly Color VentGlow = new Color(1f, 0.45f, 0.12f);
        public static readonly Color Hub = new Color(0.42f, 0.5f, 0.58f);
        public static readonly Color Machine = new Color(0.55f, 0.6f, 0.66f);
        public static readonly Color MachineDark = new Color(0.22f, 0.25f, 0.3f);
        public static readonly Color Accent = new Color(1f, 0.78f, 0.15f);
        public static readonly Color Tube = new Color(0.45f, 0.62f, 0.7f);
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

        public static void TurnBillboards(Quaternion camRot)
        {
            for (int i = Billboards.Count - 1; i >= 0; i--)
            {
                var t = Billboards[i];
                if (t == null) { Billboards.RemoveAt(i); continue; }
                t.rotation = camRot;
            }
        }

        /// <summary>Text that floats upright and always faces the camera.</summary>
        public static TextMesh Billboard(string text, Transform parent, Vector3 localPos, float size, Color c)
        {
            var tm = Label(text, parent, localPos, size, c, faceUp: false);
            tm.anchor = TextAnchor.LowerCenter;
            tm.richText = true;
            Billboards.Add(tm.transform);
            return tm;
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
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            _cache[c] = m;
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
