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
        _BumpMap ("Scanned normal (0.17)", 2D) = "bump" {}
        _RoughMap ("Scanned roughness (0.17)", 2D) = "gray" {}
        _HasPBR ("Use the scanned normal and roughness", Range(0,1)) = 0
        _BumpScale ("Normal strength", Range(0,2)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex, _Grime, _BumpMap, _RoughMap;
        fixed4 _Color;
        float _Scale, _GrimeAmount, _GroundDirt, _Metallic, _Rust, _HasPBR, _BumpScale;
        half _Glossiness;

        struct Input
        {
            float3 worldPos;
            float3 worldNormal;
            INTERNAL_DATA
        };

        // World-space normal back into the surface shader's tangent space (needs mesh tangents).
        float3 WorldToTangentNormalVector(Input IN, float3 normal)
        {
            float3 t2w0 = WorldNormalVector(IN, float3(1, 0, 0));
            float3 t2w1 = WorldNormalVector(IN, float3(0, 1, 0));
            float3 t2w2 = WorldNormalVector(IN, float3(0, 0, 1));
            float3x3 t2w = float3x3(t2w0, t2w1, t2w2);
            return normalize(mul(t2w, normal));
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float3 wn = WorldNormalVector(IN, float3(0, 0, 1));
            float3 n = abs(wn);
            n = n * n * n * n;
            n /= max(n.x + n.y + n.z, 0.0001);
            float3 p = IN.worldPos * _Scale;
            // 0.17: mirror the projections on back faces so scanned bricks and boards never read backwards.
            float3 sgn = wn < 0 ? -1 : 1;
            float2 uvX = float2(p.z * sgn.x, p.y), uvY = float2(p.x * sgn.y, p.z), uvZ = float2(-p.x * sgn.z, p.y);
            fixed3 tx = tex2D(_MainTex, uvX).rgb;
            fixed3 ty = tex2D(_MainTex, uvY).rgb;
            fixed3 tz = tex2D(_MainTex, uvZ).rgb;
            float rough = 0.5;
            if (_HasPBR > 0.5)
            {
                // Triplanar normal map with a whiteout blend (scanned surfaces from Poly Haven, CC0).
                half3 nx = UnpackNormal(tex2D(_BumpMap, uvX)); nx.xy *= _BumpScale;
                half3 ny = UnpackNormal(tex2D(_BumpMap, uvY)); ny.xy *= _BumpScale;
                half3 nz = UnpackNormal(tex2D(_BumpMap, uvZ)); nz.xy *= _BumpScale;
                nx.x *= sgn.x; ny.x *= sgn.y; nz.x *= -sgn.z;   // the UVs were mirrored on back faces
                nx = half3(nx.xy + wn.zy, abs(nx.z) * wn.x);
                ny = half3(ny.xy + wn.xz, abs(ny.z) * wn.y);
                nz = half3(nz.xy + wn.xy, abs(nz.z) * wn.z);
                float3 nW = normalize(nx.zyx * n.x + ny.xzy * n.y + nz.xyz * n.z);
                o.Normal = WorldToTangentNormalVector(IN, nW);
                rough = tex2D(_RoughMap, uvX).r * n.x + tex2D(_RoughMap, uvY).r * n.y + tex2D(_RoughMap, uvZ).r * n.z;
            }
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
            float gloss = _HasPBR > 0.5 ? saturate(1.0 - rough) * 0.85 + _Glossiness * 0.3 : _Glossiness;
            o.Smoothness = gloss * lerp(1.0, 0.45, dirt) * (0.7 + 0.6 * blot);
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
