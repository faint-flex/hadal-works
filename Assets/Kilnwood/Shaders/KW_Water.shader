// Kilnwood: the river. Two drifting ripple normal maps break up the surface; high smoothness makes it reflect the
// sky (from the scene's reflection probe) and catch the sun, darker where you look straight down into it.
Shader "Kilnwood/Water"
{
    Properties
    {
        _MainTex ("Water colour", 2D) = "white" {}
        _Color ("Tint", Color) = (0.55,0.68,0.72,1)
        _Deep ("Looking straight down", Color) = (0.06,0.14,0.16,1)
        _Waves ("Ripple normal", 2D) = "bump" {}
        _WaveScale ("Ripple repeats per metre", Float) = 0.12
        _WaveStrength ("Ripple strength", Range(0,2)) = 0.55
        _Flow ("Flow (xy, zw)", Vector) = (0.01, 0.06, -0.02, 0.035)
        _Glossiness ("Smoothness", Range(0,1)) = 0.93
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex, _Waves;
        fixed4 _Color, _Deep;
        float _WaveScale, _WaveStrength;
        float4 _Flow;
        half _Glossiness;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
            float3 viewDir;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float2 p = IN.worldPos.xz * _WaveScale;
            float3 n1 = UnpackNormal(tex2D(_Waves, p + _Time.y * _Flow.xy));
            float3 n2 = UnpackNormal(tex2D(_Waves, p * 1.9 + 0.5 + _Time.y * _Flow.zw));
            float3 n = float3((n1.xy + n2.xy) * _WaveStrength, 1);
            o.Normal = normalize(n);
            fixed3 c = tex2D(_MainTex, IN.uv_MainTex).rgb * _Color.rgb;
            // Steep views see into the water (dark); grazing views see the sky reflection more.
            half facing = saturate(dot(normalize(IN.viewDir), float3(0, 0, 1)));
            o.Albedo = lerp(c, _Deep.rgb, facing * 0.6);
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
