// Kilnwood: grass tufts drawn with GPU instancing. Blades are plain triangles coloured by vertex colour
// (alpha = 0 at the root, 1 at the tip) that sway in the wind. Normals point up so the tufts light like the ground.
Shader "Kilnwood/Grass"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _Wind ("Wind strength", Float) = 0.09
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard vertex:vert
        #pragma multi_compile_instancing
        #pragma target 3.0

        fixed4 _Color;
        float _Wind;

        struct Input { float4 color : COLOR; };

        void vert(inout appdata_full v)
        {
            float tip = v.color.a;
            float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
            float s = sin(_Time.y * 1.6 + wp.x * 0.31 + wp.z * 0.22) + 0.45 * sin(_Time.y * 3.3 + wp.x * 1.1 - wp.z * 0.7);
            float3 push = float3(1.0, 0.0, 0.6) * s * _Wind * tip * tip;
            v.vertex.xyz += mul((float3x3)unity_WorldToObject, push);
            v.normal = mul((float3x3)unity_WorldToObject, float3(0, 1, 0));
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            o.Albedo = IN.color.rgb * _Color.rgb;
            o.Smoothness = 0.05;
            o.Metallic = 0;
            o.Occlusion = lerp(0.55, 1.0, IN.color.a);
        }
        ENDCG
    }
    FallBack "Diffuse"
}
