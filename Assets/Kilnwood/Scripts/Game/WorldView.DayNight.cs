using System.Collections.Generic;
using UnityEngine;

namespace Kilnwood.Game
{
    /// <summary>
    /// Day and night. One day is DayLength seconds of game time (so pausing stops the sun). Most of the cycle is day;
    /// dusk turns the light gold, then a short blue moonlit night where pole lamps, the lodge windows and fires
    /// glow. Night never gets so dark that the valley can't be played (the build menu and labels don't change).
    /// </summary>
    public partial class WorldView
    {
        public const float DayLength = 720f;
        public static bool DayNightOn = true;
        /// <summary>Hour to hold the clock at (screenshot runs); negative lets it run.</summary>
        public static float FixedHour = -1f;
        const float DayShare = 0.74f, Sunrise = 5.6f, Sunset = 19.2f;

        static readonly Color AmbSky = new Color(0.58f, 0.68f, 0.82f), AmbEq = new Color(0.5f, 0.52f, 0.46f), AmbGround = new Color(0.25f, 0.22f, 0.17f);
        static readonly Color NightTint = new Color(0.4f, 0.48f, 0.74f);
        static readonly Color DuskFog = new Color(0.95f, 0.66f, 0.44f), NightFog = new Color(0.14f, 0.17f, 0.26f);
        static readonly Color SunNoon = new Color(1f, 0.92f, 0.78f), SunLow = new Color(1f, 0.66f, 0.4f), Moon = new Color(0.58f, 0.68f, 0.95f);

        float _probeAt, _probeHour = -99f;
        public float Night { get; private set; }     // 0 day .. 1 full night
        public float Hour { get; private set; } = 9f;
        readonly List<Renderer> _windows = new List<Renderer>();
        int _windowCount = -1;

        /// <summary>Clock hour for this game time: day hours spread over DayShare of the cycle, night over the rest.</summary>
        public static float HourAt(float gameTime)
        {
            float start = (9f - Sunrise) / (Sunset - Sunrise) * DayShare;   // games start at 9 in the morning
            float t = Mathf.Repeat(gameTime / DayLength + start, 1f);
            if (t < DayShare) return Sunrise + t / DayShare * (Sunset - Sunrise);
            return Mathf.Repeat(Sunset + (t - DayShare) / (1f - DayShare) * (24f - Sunset + Sunrise), 24f);
        }

        void UpdateDayNight(float now)
        {
            if (_sun == null) return;
            float h = FixedHour >= 0f ? FixedHour : DayNightOn ? HourAt(_w.Time) : 10.5f;
            Hour = h;
            // Sun height in degrees: 0 at sunrise and sunset, about 55 at midday, below the horizon at night.
            float mid = (Sunrise + Sunset) / 2f, halfDay = (Sunset - Sunrise) / 2f;
            float dh = Mathf.DeltaAngle(mid * 15f, h * 15f) / 15f;        // hours from midday, -12..12
            float elev = Mathf.Cos(dh / halfDay * Mathf.PI / 2f) * 47f;     // 10:30 gives the classic 42 degree sun
            if (Mathf.Abs(dh) > halfDay) elev = -Mathf.Min(30f, (Mathf.Abs(dh) - halfDay) * 12f);
            float dayW = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-2f, 4f, elev));
            float moonW = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-10f, -2f, elev));
            Night = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-8f, 6f, elev));
            float low = 1f - Mathf.InverseLerp(4f, 28f, elev);           // golden hour

            if (elev > -2f)
            {
                _sun.transform.rotation = Quaternion.Euler(Mathf.Max(elev, 1f), -40f + (h - 10.5f) * 9f, 0f);
                _sun.color = Color.Lerp(SunNoon, SunLow, low);
                _sun.intensity = Mathf.Lerp(1.25f, 1.6f, low) * dayW;   // a low gold sun stays strong
            }
            else
            {
                _sun.transform.rotation = Quaternion.Euler(38f, 150f, 0f);
                _sun.color = Moon;
                _sun.intensity = 0.6f * moonW;
            }
            _sun.shadowStrength = elev > -2f ? 0.8f : 0.55f;

            var dusk = Color.Lerp(Color.white, new Color(1.2f, 0.86f, 0.62f), low * dayW);
            float amb = Mathf.Lerp(1f, 0.62f, Night);
            RenderSettings.ambientSkyColor = Mul(Color.Lerp(AmbSky, NightTint, Night) * amb, dusk);
            RenderSettings.ambientEquatorColor = Mul(Color.Lerp(AmbEq, NightTint * 0.9f, Night) * amb, dusk);
            RenderSettings.ambientGroundColor = Color.Lerp(AmbGround, NightTint * 0.35f, Night) * amb;
            var fog = Color.Lerp(Color.Lerp(CameraRig.Horizon, DuskFog, low * dayW), NightFog, Night);
            RenderSettings.fogColor = fog;
            var cam = Camera.main;
            if (cam != null) cam.backgroundColor = fog;
            var skyMat = RenderSettings.skybox;
            if (skyMat != null && skyMat.HasProperty("_SunDir"))
            {
                // Photographed sky: our sun (or none at night), dusk glow and the night fade.
                skyMat.SetFloat("_Exposure", Mathf.Lerp(1.0f, 0.3f, Night));
                skyMat.SetVector("_SunDir", elev > -2f ? -_sun.transform.forward : Vector3.down);
                skyMat.SetColor("_SunColor", Color.Lerp(SunNoon, SunLow, low));
                skyMat.SetFloat("_Dusk", low * dayW);
                skyMat.SetFloat("_Night", Night);
                skyMat.SetColor("_Haze", Color.Lerp(CameraRig.Horizon, DuskFog, low * dayW));
            }
            else if (skyMat != null && skyMat.HasProperty("_Exposure"))
                skyMat.SetFloat("_Exposure", Mathf.Lerp(1.15f, 0.28f, Night));
            // Smoke is unlit: shade it with the light so it doesn't glow white at dusk and night.
            Art.ShadeSmoke(Color.Lerp(Mul(Color.white, dusk) * Mathf.Lerp(1f, 0.85f, low * dayW), new Color(0.24f, 0.27f, 0.36f), Night));
            if (_waterMat != null && _waterMat.HasProperty("_SkyGlow")) _waterMat.SetFloat("_SkyGlow", Mathf.Lerp(0.11f, 0.02f, Night));

            // The sky reflection follows the light every few seconds.
            if (_skyProbe != null && (DayNightOn || FixedHour >= 0f) && (Time.unscaledTime >= _probeAt && Mathf.Abs(Mathf.DeltaAngle(h * 15f, _probeHour * 15f)) > 4f || Mathf.Abs(Mathf.DeltaAngle(h * 15f, _probeHour * 15f)) > 10f))
            {
                _probeAt = Time.unscaledTime + 3f;
                _probeHour = h;
                _skyProbe.RenderProbe();
            }

            // Lodge and cabin windows glow after dusk.
            if (_windowCount != _views.Count)
            {
                _windowCount = _views.Count;
                _windows.Clear();
                foreach (var v in _views.Values)
                {
                    if (v.Type != Kilnwood.Sim.BuildingType.Lodge && v.Type != Kilnwood.Sim.BuildingType.Cabin && v.Type != Kilnwood.Sim.BuildingType.Workshop && v.Type != Kilnwood.Sim.BuildingType.WireMill && v.Type != Kilnwood.Sim.BuildingType.MachineShop && v.Type != Kilnwood.Sim.BuildingType.Assembly && v.Type != Kilnwood.Sim.BuildingType.Turbine && v.Type != Kilnwood.Sim.BuildingType.Hangar && v.Type != Kilnwood.Sim.BuildingType.RollingMill && v.Type != Kilnwood.Sim.BuildingType.HydroDam && v.Type != Kilnwood.Sim.BuildingType.Glassworks && v.Type != Kilnwood.Sim.BuildingType.RadioWorks) continue;
                    foreach (var r in v.Root.GetComponentsInChildren<Renderer>(true))
                        if (r.gameObject.name == "Window") _windows.Add(r);
                }
                _windowsLit = !_windowsLit;   // force a refresh below
            }
            bool lit = Night > 0.45f;
            UpdateHomeLights(lit);
            if (lit != _windowsLit)
            {
                _windowsLit = lit;
                if (_windowGlow == null)
                {
                    _windowGlow = new Material(Look.GlowMat(new Color(1f, 0.7f, 0.35f))) { name = "Window glow" };
                    _windowGlow.EnableKeyword("_EMISSION");
                    if (_windowGlow.HasProperty("_EmissionColor")) _windowGlow.SetColor("_EmissionColor", new Color(1f, 0.7f, 0.35f) * 4f);
                }
                var glow = _windowGlow;
                foreach (var r in _windows)
                {
                    if (r == null) continue;
                    if (!_windowDay.ContainsKey(r)) _windowDay[r] = r.sharedMaterial;
                    r.sharedMaterial = lit ? glow : _windowDay[r];
                }
            }
        }

        bool _windowsLit;
        Material _windowGlow;
        readonly Dictionary<GameObject, Light> _homeLights = new Dictionary<GameObject, Light>();

        /// <summary>A warm light inside the lodge (and any cabin) after dark, so lit windows spill onto the porch.</summary>
        void UpdateHomeLights(bool lit)
        {
            foreach (var v in _views.Values)
            {
                if (v.Type != Kilnwood.Sim.BuildingType.Lodge && v.Type != Kilnwood.Sim.BuildingType.Cabin) continue;
                if (!_homeLights.TryGetValue(v.Root, out var l) || l == null)
                {
                    l = new GameObject("Home light").AddComponent<Light>();
                    l.transform.SetParent(v.Root.transform, false);
                    l.transform.localPosition = new Vector3(0f, 2.2f, 0f);
                    l.type = LightType.Point;
                    l.color = new Color(1f, 0.62f, 0.3f);
                    l.intensity = v.Type == Kilnwood.Sim.BuildingType.Lodge ? 2.4f : 1.4f;
                    l.range = v.Type == Kilnwood.Sim.BuildingType.Lodge ? 11f : 6f;
                    l.shadows = LightShadows.None;
                    _homeLights[v.Root] = l;
                }
                if (l.enabled != lit) l.enabled = lit;
            }
        }
        readonly Dictionary<Renderer, Material> _windowDay = new Dictionary<Renderer, Material>();

        static Color Mul(Color a, Color b) => new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a);
    }
}
