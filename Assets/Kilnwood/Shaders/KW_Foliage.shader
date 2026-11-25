// Kilnwood: tree foliage. Vertex colour carries self-shadowing (dark deep inside the crown, light at the tips);
// normals point out from the crown so it lights like a soft volume; leaves glow a little when the sun is behind
// them; the crown sways gently with height. Instanced so a forest is a handful of draw calls.
Shader "Kilnwood/Foliage"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _Wind ("Sway (metres at the top)", Float) = 0.06
        _Translucency ("Sun through leaves", Range(0,1)) = 0.2
        _LeafTex ("Leaf canopy detail (linear, mean 0.5)", 2D) = "grey" {}
        _LeafScale ("Leaf detail repeats per unit", Float) = 0.6
        _LeafAmount ("Leaf detail amount (0 = off)", Range(0,1)) = 0
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
        float _Wind, _Translucency, _LeafScale, _LeafAmount;
        sampler2D _LeafTex;

        // leafPos: the vertex before sway (object space + the tree's root), so the leaf pattern sticks to the crown.
        struct Input { float4 color : COLOR; float3 viewDir; float3 leafPos; float3 leafNrm; };

        void vert(inout appdata_full v, out Input data)
        {
            UNITY_INITIALIZE_OUTPUT(Input, data);
            float3 rootW = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
            data.leafPos = v.vertex.xyz + rootW * 0.37;
            data.leafNrm = v.normal;
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
            // 0.19.1: a leaf canopy pattern, projected from three sides (triplanar), on near broadleaf crowns.
            if (_LeafAmount > 0)
            {
                float3 w = pow(abs(normalize(IN.leafNrm)), 4.0); w /= (w.x + w.y + w.z);
                float3 p = IN.leafPos * _LeafScale;
                float leaf = tex2D(_LeafTex, p.zy).g * w.x + tex2D(_LeafTex, p.xz + 0.31).g * w.y + tex2D(_LeafTex, p.xy + 0.67).g * w.z;
                float k = lerp(1.0, leaf * 2.0, _LeafAmount);
                o.Albedo *= k;
                o.Occlusion *= lerp(1.0, saturate(leaf * 2.2), _LeafAmount * 0.6);   // the gaps between leaves sit in shade
            }
            o.Smoothness = 0.12;
            o.Metallic = 0;
            // Back-lit leaves: brighter where we look toward the sun through the crown.
            float3 L = normalize(_WorldSpaceLightPos0.xyz);
            float3 V = normalize(IN.viewDir);   // world space (surf doesn't write o.Normal), surface -> camera
            float back = pow(saturate(dot(-V, L)), 4.0);
            o.Emission = o.Albedo * float3(0.9, 1.0, 0.75) * back * _Translucency * ao;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
