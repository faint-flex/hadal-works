// Kilnwood: world-space (triplanar) pattern shader for buildings. The pattern texture (planks, bricks,
// stone blocks, shingles) is projected from the three world axes, so it never stretches on scaled boxes.
Shader "Kilnwood/Triplanar"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Pattern", 2D) = "white" {}
        _Scale ("Repeats per metre", Float) = 1
        _Glossiness ("Smoothness", Range(0,1)) = 0.1
        _Grime ("Weathering noise", 2D) = "gray" {}
        _GrimeAmount ("Weathering", Range(0,1)) = 0.7
        _GroundDirt ("Dirt splashed up from the ground", Range(0,1)) = 0.75
        _Metallic ("Metal", Range(0,1)) = 0
        _Rust ("Rust in the streaks (metal)", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex, _Grime;
        fixed4 _Color;
        float _Scale, _GrimeAmount, _GroundDirt, _Metallic, _Rust;
        half _Glossiness;

        struct Input
        {
            float3 worldPos;
            float3 worldNormal;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float3 n = abs(IN.worldNormal);
            n = n * n * n * n;
            n /= max(n.x + n.y + n.z, 0.0001);
            float3 p = IN.worldPos * _Scale;
            fixed3 tx = tex2D(_MainTex, p.zy).rgb;
            fixed3 ty = tex2D(_MainTex, p.xz).rgb;
            fixed3 tz = tex2D(_MainTex, p.xy).rgb;
            fixed3 c = (tx * n.x + ty * n.y + tz * n.z) * _Color.rgb;
            // Weathering (realism pass 3): soft blotches everywhere, rain streaks running down walls,
            // and earthy dirt splashed up the bottom metre, with a ragged edge.
            float3 wp = IN.worldPos;
            float blot = tex2D(_Grime, wp.xz * 0.045 + wp.y * 0.031).r;
            float streak = tex2D(_Grime, float2((wp.x + wp.z) * 0.35, wp.y * 0.05)).r;
            float wall = saturate(1.0 - n.y);
            c *= lerp(1.0, 0.68 + 0.64 * blot, _GrimeAmount);
            float st = smoothstep(0.35, 0.75, streak);
            c *= lerp(1.0, 1.1 - 0.55 * st, wall * _GrimeAmount);
            float dirt = saturate(1.0 - (wp.y + (blot - 0.5) * 0.7) / 1.5);
            dirt = dirt * dirt * (3.0 - 2.0 * dirt) * _GroundDirt;
            c = lerp(c, c * fixed3(0.42, 0.34, 0.24), dirt);
            c *= lerp(1.0, 0.7, saturate(1.0 - wp.y / 0.35) * _GroundDirt);   // dark damp line right at the ground
            // Metal: rusty brown creeping down from the streaks and blotches.
            float rust = saturate(st * wall * 0.8 + (blot - 0.55) * 1.5) * _Rust;
            c = lerp(c, fixed3(0.32, 0.17, 0.09), rust);
            o.Albedo = c;
            o.Metallic = _Metallic * (1.0 - dirt) * (1.0 - rust);
            o.Smoothness = _Glossiness * lerp(1.0, 0.45, dirt) * (0.7 + 0.6 * blot);
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
