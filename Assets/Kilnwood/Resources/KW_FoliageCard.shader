// Kilnwood 0.18 photoreal pass 2: alpha-cut foliage cards (fir twigs, leaf sprays) textured from Poly Haven scans (CC0).
// Normals point out of the crown (set by the mesh), so the cards light like a soft volume; vertex alpha carries
// self-shadowing; leaves glow a little with the sun behind them; the crown sways like KW_Foliage.
// Lives in Resources so the build always includes it.
Shader "Kilnwood/FoliageCard"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _MainTex ("Cards (RGBA)", 2D) = "white" {}
        _Cutoff ("Alpha cut", Range(0,1)) = 0.45
        _Wind ("Sway (metres at the top)", Float) = 0.06
        _Translucency ("Sun through leaves", Range(0,1)) = 0.3
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Cull Off
        LOD 200

        CGPROGRAM
        #pragma surface surf Lambert vertex:vert alphatest:_Cutoff addshadow fullforwardshadows
        #pragma multi_compile_instancing
        #pragma target 3.0

        sampler2D _MainTex;
        UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_DEFINE_INSTANCED_PROP(fixed4, _Color)
        UNITY_INSTANCING_BUFFER_END(Props)
        float _Wind, _Translucency;

        struct Input { float2 uv_MainTex; float4 color : COLOR; float3 viewDir; };

        void vert(inout appdata_full v)
        {
            float3 rootW = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
            float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
            float h = max(0, wp.y - rootW.y);
            float ph = rootW.x * 0.37 + rootW.z * 0.23 + (wp.x * 0.7 + wp.z) * 0.05;   // a wave rolls across big meshes (grass chunks)
            float s = sin(_Time.y * 1.1 + ph) + 0.35 * sin(_Time.y * 2.7 + ph * 1.7 + wp.y);
            float flutter = sin(_Time.y * 6.0 + wp.x * 3.1 + wp.z * 2.3) * 0.015 * v.color.a;
            float3 push = float3(1.0, 0.0, 0.45) * (s * _Wind * h * 0.25 + flutter);
            v.vertex.xyz += mul((float3x3)unity_WorldToObject, push);
        }

        // 0.18.2: diffuse only. With the Standard model, cards seen edge-on mirrored the bright dusk horizon
        // (Fresnel) and showed as rows of white dashes down the pines.
        void surf(Input IN, inout SurfaceOutput o)
        {
            fixed4 tex = tex2D(_MainTex, IN.uv_MainTex);
            fixed4 tint = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
            float ao = IN.color.a;
            o.Albedo = tex.rgb * tint.rgb * IN.color.rgb * lerp(0.55, 1.0, ao);
            o.Alpha = tex.a;
            o.Specular = 0;
            o.Gloss = 0;
            float3 L = normalize(_WorldSpaceLightPos0.xyz);
            float3 V = normalize(IN.viewDir);
            float back = pow(saturate(dot(-V, L)), 4.0);
            o.Emission = o.Albedo * float3(0.9, 1.0, 0.7) * back * _Translucency * ao;
        }
        ENDCG
    }
    FallBack "Legacy Shaders/Transparent/Cutout/VertexLit"
}
