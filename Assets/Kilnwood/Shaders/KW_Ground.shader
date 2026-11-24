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
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex, _Detail, _DetailNormal;
        float _DetailScale, _DetailStrength, _NormalStrength, _FadeDist;
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
            o.Albedo = base * lerp(1, d * 2, k);
            float3 n = UnpackNormal(tex2D(_DetailNormal, p));
            n.xy *= _NormalStrength * near;
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
