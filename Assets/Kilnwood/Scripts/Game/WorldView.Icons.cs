using System.Collections.Generic;
using Kilnwood.Sim;
using UnityEngine;

namespace Kilnwood.Game
{
    /// <summary>
    /// Build-menu and research icons: each building's real 3D model, photographed once in the game's own light
    /// (three-quarter view, transparent background). So icons always match what you place.
    /// </summary>
    public partial class WorldView
    {
        const int IconLayer = 31;
        readonly Dictionary<BuildingType, Texture2D> _icons = new Dictionary<BuildingType, Texture2D>();

        /// <summary>Icon if it has been made already (safe to call from OnGUI); null otherwise.</summary>
        public Texture2D IconIfReady(BuildingType t) => _icons.TryGetValue(t, out var tex) ? tex : null;

        /// <summary>Makes at most one missing icon (call from Update, not OnGUI). True when all are done.</summary>
        public bool WarmIcons(IList<BuildingType> types)
        {
            foreach (var t in types)
                if (!_icons.ContainsKey(t)) { Icon(t); return false; }
            if (!_vehicleIconsTried) { MakeVehicleIcons(); return false; }
            return true;
        }

        /// <summary>Icon for a building type (null if it has no model of its own, e.g. track pieces).</summary>
        public Texture2D Icon(BuildingType t)
        {
            if (_icons.TryGetValue(t, out var tex)) return tex;
            tex = null;
            try { tex = RenderIcon(t, 128); }
            catch (System.Exception e) { Debug.LogWarning("Kilnwood icon " + t + ": " + e.Message); }
            _icons[t] = tex;
            return tex;
        }

        Texture2D RenderIcon(BuildingType t, int px)
        {
            var def = _w.B.Defs[t];
            // A stand-in building far outside the map; it is never added to the world.
            var fake = new Building { Id = -1000 - (int)t, Type = t, X = -600, Y = -600, Size = def.Size, Facing = 2, Status = "" };
            var v = MakeView(fake);
            var root = v.Root;
            try
            {
                if (v.Title != null) v.Title.gameObject.SetActive(false);
                if (v.Post != null) v.Post.gameObject.SetActive(false);
                foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true)) ps.gameObject.SetActive(false);
                foreach (var tm in root.GetComponentsInChildren<TextMesh>(true)) tm.gameObject.SetActive(false);
                if (t == BuildingType.Rail) { HalfTrack(v.Body, 0); HalfTrack(v.Body, 2); }   // a straight piece of track
                return Snapshot(root, px);
            }
            finally
            {
                root.SetActive(false);
                Destroy(root);
            }
        }


        /// <summary>Photographs everything under root (three-quarter view, transparent background).</summary>
        Texture2D Snapshot(GameObject root, int px)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return null;
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            SetLayer(root.transform, IconLayer);

            var camGo = new GameObject("Icon camera");
            var cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            cam.cullingMask = 1 << IconLayer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.fieldOfView = 26f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            cam.allowHDR = false;
            // Three-quarter view from the front-left and above, like the game camera.
            var dir = Quaternion.Euler(32f, 35f, 0f) * Vector3.forward;
            float radius = bounds.extents.magnitude;
            float dist = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.02f;
            cam.transform.position = bounds.center - dir * dist;
            cam.transform.rotation = Quaternion.LookRotation(dir);

            var rt = RenderTexture.GetTemporary(px, px, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, 4);
            cam.targetTexture = rt;
            bool fog = RenderSettings.fog;
            RenderSettings.fog = false;
            cam.Render();
            RenderSettings.fog = fog;
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(px, px, TextureFormat.RGBA32, false) { name = "Icon " + root.name, wrapMode = TextureWrapMode.Clamp };
            tex.ReadPixels(new Rect(0, 0, px, px), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
            Destroy(camGo);
            return tex;
        }

        Texture2D _excavatorIcon, _truckIcon, _dozerIcon;
        /// <summary>Bulldozer picture for the Level tool (null until made).</summary>
        public Texture2D DozerIcon => _dozerIcon;
        bool _vehicleIconsTried;

        /// <summary>Excavator and truck pictures for the Dig and Dump tools (null until made).</summary>
        public Texture2D ToolIcon(bool dig) => dig ? _excavatorIcon : _truckIcon;

        void MakeVehicleIcons()
        {
            if (_vehicleIconsTried) return;
            _vehicleIconsTried = true;
            foreach (int kind in new[] { 0, 1, 2 })
            {
                bool ex = kind == 0;
                VView vv = null;
                try
                {
                    vv = kind == 0 ? MakeExcavator() : kind == 1 ? MakeTruck() : MakeDozer();
                    vv.Root.position = new Vector3(-1200f, 0f, -1200f);
                    vv.Root.rotation = Quaternion.Euler(0f, ex ? 200f : 160f, 0f);
                    foreach (var ps in vv.Root.GetComponentsInChildren<ParticleSystem>(true)) ps.gameObject.SetActive(false);
                    foreach (var tm in vv.Root.GetComponentsInChildren<TextMesh>(true)) tm.gameObject.SetActive(false);
                    var tex = Snapshot(vv.Root.gameObject, 128);
                    if (tex != null && _w.Era == 0) Lift(tex);   // 0.23.1: dark iron steam machines read as a greyed-out button
                    if (kind == 0) _excavatorIcon = tex; else if (kind == 1) _truckIcon = tex; else _dozerIcon = tex;
                }
                catch (System.Exception e) { Debug.LogWarning("Kilnwood vehicle icon: " + e.Message); }
                finally { if (vv != null) { vv.Root.gameObject.SetActive(false); Destroy(vv.Root.gameObject); } }
            }
        }

        /// <summary>Brightens the shadows of an icon (keeps its alpha) so a dark machine doesn't look disabled.</summary>
        static void Lift(Texture2D tex)
        {
            var px = tex.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                if (px[i].a == 0) continue;
                px[i].r = LiftByte(px[i].r); px[i].g = LiftByte(px[i].g); px[i].b = LiftByte(px[i].b);
            }
            tex.SetPixels32(px);
            tex.Apply();
        }

        static byte LiftByte(byte v)
        {
            float f = v / 255f;
            f = 1f - (1f - f) * (1f - f);   // shadows up a lot, highlights barely
            return (byte)Mathf.Clamp(Mathf.RoundToInt(f * 255f), 0, 255);
        }

        static void SetLayer(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++) SetLayer(t.GetChild(i), layer);
        }
    }
}
