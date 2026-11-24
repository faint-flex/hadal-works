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
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        float _Scale;
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
            o.Albedo = (tx * n.x + ty * n.y + tz * n.z) * _Color.rgb;
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
