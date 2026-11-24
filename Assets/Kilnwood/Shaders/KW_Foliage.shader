// Kilnwood: tree foliage. Vertex colour carries self-shadowing (dark deep inside the crown, light at the tips);
// normals point out from the crown so it lights like a soft volume; leaves glow a little when the sun is behind
// them; the crown sways gently with height. Instanced so a forest is a handful of draw calls.
Shader "Kilnwood/Foliage"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _Wind ("Sway (metres at the top)", Float) = 0.06
        _Translucency ("Sun through leaves", Range(0,1)) = 0.35
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard vertex:vert fullforwardshadows addshadow
        #pragma multi_compile_instancing
        #pragma target 3.0

        UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_DEFINE_INSTANCED_PROP(fixed4, _Color)
        UNITY_INSTANCING_BUFFER_END(Props)
        float _Wind, _Translucency;

        struct Input { float4 color : COLOR; float3 viewDir; };

        void vert(inout appdata_full v)
        {
            float3 rootW = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
            float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
            float h = max(0, wp.y - rootW.y);
            float ph = rootW.x * 0.37 + rootW.z * 0.23;
            float s = sin(_Time.y * 1.1 + ph) + 0.35 * sin(_Time.y * 2.7 + ph * 1.7 + wp.y);
            float3 push = float3(1.0, 0.0, 0.45) * s * _Wind * h * 0.25;
            v.vertex.xyz += mul((float3x3)unity_WorldToObject, push);
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 tint = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
            float ao = IN.color.a;
            o.Albedo = IN.color.rgb * tint.rgb;
            o.Occlusion = lerp(0.35, 1.0, ao);
            o.Smoothness = 0.12;
            o.Metallic = 0;
            // Back-lit leaves: brighter where we look toward the sun through the crown.
            float3 L = normalize(_WorldSpaceLightPos0.xyz);
            float3 V = normalize(IN.viewDir);   // world space (surf doesn't write o.Normal), surface -> camera
            float back = pow(saturate(dot(-V, L)), 4.0);
            o.Emission = o.Albedo * float3(1.0, 0.95, 0.7) * back * _Translucency * ao;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
