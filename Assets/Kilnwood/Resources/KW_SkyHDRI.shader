// Kilnwood 0.17 photoreal pass: a photographed sky (Poly Haven "Kloofendal 48d Partly Cloudy" pure sky, CC0) for the daytime,
// with the photo's own sun dimmed out and the game's sun drawn where the light really is. Dusk warms the horizon toward the
// sun; night fades to a deep blue with stars. Lives in Resources so the build always includes it.
Shader "Kilnwood/SkyHDRI"
{
    Properties
    {
        _Tex ("Sky (equirectangular HDR)", 2D) = "grey" {}
        _Rotation ("Rotation", Range(-180, 180)) = -34
        _Exposure ("Exposure", Float) = 1
        _SunDir ("Direction toward the sun", Vector) = (0.3, 0.7, -0.6, 0)
        _SunColor ("Sun colour", Color) = (1, 0.95, 0.85, 1)
        _Dusk ("Dusk", Range(0, 1)) = 0
        _Night ("Night", Range(0, 1)) = 0
        _Haze ("Horizon haze colour", Color) = (0.62, 0.68, 0.74, 1)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _Tex;
            float _Rotation, _Exposure, _Dusk, _Night;
            float4 _SunDir, _SunColor, _Haze;

            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                // Sample the photo turned by -_Rotation (so its sky shows turned by +_Rotation, like Skybox/Panoramic).
                float a = -_Rotation * UNITY_PI / 180.0, sa = sin(a), ca = cos(a);
                float3 r = float3(ca * d.x - sa * d.z, d.y, sa * d.x + ca * d.z);
                float u = 0.5 - atan2(r.z, r.x) / (2.0 * UNITY_PI);
                float up = max(d.y, 0.0);
                float v = 0.5 + asin(clamp(up, -1.0, 1.0)) / UNITY_PI;   // below the horizon, keep the horizon row
                float3 sky = tex2Dlod(_Tex, float4(u, v, 0, 0)).rgb * _Exposure;
                float lum = dot(sky, float3(0.3, 0.59, 0.11));
                sky *= min(1.0, 2.5 / max(lum, 1e-4));   // dim the photo's own sun: ours is drawn below
                // Haze toward the horizon and a soft ground below it, so the far hills sit in air.
                float hz = 1.0 - saturate(up * 6.0);
                sky = lerp(sky, _Haze.rgb * _Exposure * 0.95, hz * 0.55);
                if (d.y < 0.0) sky = lerp(_Haze.rgb * _Exposure * 0.8, _Haze.rgb * _Exposure * 0.45, saturate(-d.y * 3.0));
                // Dusk: warmer overall, a glowing band along the horizon, brightest toward the sun.
                float3 sd = normalize(_SunDir.xyz);
                float toward = saturate(dot(normalize(float3(d.x, 0, d.z)), normalize(float3(sd.x, 0, sd.z))) * 0.5 + 0.5);
                float3 warm = float3(1.25, 0.72, 0.42);
                sky = lerp(sky, sky * warm, _Dusk * 0.7);
                sky += float3(1.0, 0.45, 0.18) * _Dusk * pow(hz, 3.0) * (0.25 + 0.9 * toward * toward) * _Exposure;
                // The game's sun: a small bright disc with a glow, coloured like the light.
                float s = dot(d, sd);
                float disc = smoothstep(0.99985, 0.99993, s);
                float glow = pow(saturate(s), 300.0) * 0.6 + pow(saturate(s), 18.0) * 0.12;
                sky += _SunColor.rgb * (disc * 30.0 + glow) * (1.0 - _Night);
                // Night: deep blue, lighter at the horizon, with stars.
                float3 night = lerp(float3(0.012, 0.018, 0.04), float3(0.05, 0.065, 0.1), hz);
                // 0.18.1: small round stars (a soft dot at a random spot in each sky cell), many faint, a few bright.
                float2 sc = float2(atan2(d.z, d.x) * 320.0, asin(clamp(d.y, -1.0, 1.0)) * 320.0);
                float2 cell = floor(sc);
                float h = Hash(cell);
                float2 off = float2(Hash(cell + 3.1), Hash(cell + 5.7)) * 0.5 + 0.25;
                float dist = length(frac(sc) - off);
                float bright = Hash(cell + 7.0);
                float star = step(0.975, h) * smoothstep(0.22, 0.0, dist) * (0.25 + 0.75 * bright * bright);
                star *= 0.75 + 0.25 * sin(_Time.y * (1.5 + 3.0 * bright) + h * 40.0);   // a slow twinkle
                night += star * saturate(up * 4.0) * float3(0.95, 0.97, 1.0) * 1.4;
                sky = lerp(sky, night, _Night);
                return fixed4(sky, 1);
            }
            ENDCG
        }
    }
    FallBack Off
}
