// Kilnwood: valley floor. The painted map texture gives the big picture (grass, ore, stone, dug pits); a tiling
// world-space detail texture adds soil grain, pebbles and small bumps up close, fading out with distance.
Shader "Kilnwood/Ground"
{
    Properties
    {
        _MainTex ("Painted ground", 2D) = "white" {}
        _Detail ("Detail grain (gray, 0.5 = no change)", 2D) = "gray" {}
        _DetailNormal ("Detail normal", 2D) = "bump" {}
        _DetailScale ("Detail repeats per metre", Float) = 0.45
        _DetailStrength ("Detail strength", Range(0,1)) = 0.6
        _NormalStrength ("Bump strength", Range(0,2)) = 0.8
        _FadeDist ("Detail fades out by", Float) = 110
        _Glossiness ("Smoothness", Range(0,1)) = 0.06
        // 0.17 photoreal pass: scanned ground (Poly Haven, CC0), picked per pixel by how close the painted colour is to each.
        _HasScans ("Use scanned ground", Range(0,1)) = 0
        _ScanScale ("Scan repeats per metre", Float) = 0.33
        _GrassD ("Grass", 2D) = "gray" {}
        _GrassN ("Grass normal", 2D) = "bump" {}
        _ForestD ("Forest floor", 2D) = "gray" {}
        _ForestN ("Forest floor normal", 2D) = "bump" {}
        _RockD ("Stone ground", 2D) = "gray" {}
        _RockN ("Stone ground normal", 2D) = "bump" {}
        _OreD ("Ore ground", 2D) = "gray" {}
        _OreN ("Ore ground normal", 2D) = "bump" {}
        _MudD ("Dug earth", 2D) = "gray" {}
        _MudN ("Dug earth normal", 2D) = "bump" {}
        _RefGrass ("Grass colour (linear)", Color) = (0.1,0.18,0.05,1)
        _RefForest ("Forest colour (linear)", Color) = (0.06,0.07,0.02,1)
        _RefRock ("Stone colour (linear)", Color) = (0.26,0.25,0.21,1)
        _RefOre ("Ore colour (linear)", Color) = (0.17,0.07,0.04,1)
        _RefMud ("Dirt colour (linear)", Color) = (0.17,0.09,0.04,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex, _Detail, _DetailNormal;
        float _DetailScale, _DetailStrength, _NormalStrength, _FadeDist, _HasScans, _ScanScale;
        UNITY_DECLARE_TEX2D(_GrassD);
        UNITY_DECLARE_TEX2D_NOSAMPLER(_GrassN);
        UNITY_DECLARE_TEX2D_NOSAMPLER(_ForestD);
        UNITY_DECLARE_TEX2D_NOSAMPLER(_ForestN);
        UNITY_DECLARE_TEX2D_NOSAMPLER(_RockD);
        UNITY_DECLARE_TEX2D_NOSAMPLER(_RockN);
        UNITY_DECLARE_TEX2D_NOSAMPLER(_OreD);
        UNITY_DECLARE_TEX2D_NOSAMPLER(_OreN);
        UNITY_DECLARE_TEX2D_NOSAMPLER(_MudD);
        UNITY_DECLARE_TEX2D_NOSAMPLER(_MudN);
        float4 _RefGrass, _RefForest, _RefRock, _RefOre, _RefMud;

        float Near(float3 c, float3 r) { float3 d = c - r; float q = dot(d, d) + 0.0004; return 1.0 / (q * q); }
        half _Glossiness;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed3 base = tex2D(_MainTex, IN.uv_MainTex).rgb;
            float2 p = IN.worldPos.xz * _DetailScale;
            float2 p2 = IN.worldPos.xz * (_DetailScale * 0.21) + 0.37;
            half d = tex2D(_Detail, p).r * 0.6 + tex2D(_Detail, p2).r * 0.4;
            float dist = distance(IN.worldPos, _WorldSpaceCameraPos);
            half near = saturate(1 - dist / _FadeDist);
            // Large-scale variation stays a little even far away; fine grain only up close.
            half k = _DetailStrength * lerp(0.35, 1, near);
            float3 n;
            if (_HasScans > 0.5)
            {
                // Weights: inverse distance from the painted colour to each ground type's colour (sums to 1, blends at edges).
                float w0 = Near(base, _RefGrass.rgb), w1 = Near(base, _RefForest.rgb), w2 = Near(base, _RefRock.rgb), w3 = Near(base, _RefOre.rgb), w4 = Near(base, _RefMud.rgb);
                float ws = w0 + w1 + w2 + w3 + w4; w0 /= ws; w1 /= ws; w2 /= ws; w3 /= ws; w4 /= ws;
                float2 q = IN.worldPos.xz * _ScanScale;
                float2 q2 = float2(q.y, -q.x) * 0.27 + 0.41;   // a second, larger, rotated copy hides the tiling
                half3 a = UNITY_SAMPLE_TEX2D(_GrassD, q).rgb * w0 + UNITY_SAMPLE_TEX2D_SAMPLER(_ForestD, _GrassD, q).rgb * w1
                        + UNITY_SAMPLE_TEX2D_SAMPLER(_RockD, _GrassD, q).rgb * w2 + UNITY_SAMPLE_TEX2D_SAMPLER(_OreD, _GrassD, q).rgb * w3
                        + UNITY_SAMPLE_TEX2D_SAMPLER(_MudD, _GrassD, q).rgb * w4;
                half3 a2 = UNITY_SAMPLE_TEX2D(_GrassD, q2).rgb * w0 + UNITY_SAMPLE_TEX2D_SAMPLER(_ForestD, _GrassD, q2).rgb * w1
                        + UNITY_SAMPLE_TEX2D_SAMPLER(_RockD, _GrassD, q2).rgb * (w2 + w3 + w4);
                half3 scan = a * 0.65 + a2 * 0.35;
                o.Albedo = base * lerp(1, scan * 2, lerp(0.7, 1.0, near));   // 0.18: stronger, it barely showed
                n = UnpackNormal(UNITY_SAMPLE_TEX2D_SAMPLER(_GrassN, _GrassD, q)) * w0 + UnpackNormal(UNITY_SAMPLE_TEX2D_SAMPLER(_ForestN, _GrassD, q)) * w1
                  + UnpackNormal(UNITY_SAMPLE_TEX2D_SAMPLER(_RockN, _GrassD, q)) * w2 + UnpackNormal(UNITY_SAMPLE_TEX2D_SAMPLER(_OreN, _GrassD, q)) * w3
                  + UnpackNormal(UNITY_SAMPLE_TEX2D_SAMPLER(_MudN, _GrassD, q)) * w4;
                n.xy *= 1.1 * lerp(0.3, 1, near);
            }
            else
            {
                o.Albedo = base * lerp(1, d * 2, k);
                n = UnpackNormal(tex2D(_DetailNormal, p));
                n.xy *= _NormalStrength * near;
            }
            o.Normal = normalize(n);
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
            o.Occlusion = lerp(1, saturate(d * 1.6), 0.5 * near);
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
