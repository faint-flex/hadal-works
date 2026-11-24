// Kilnwood: the camera's finishing pass (built-in pipeline, HDR camera).
//   pass 0  bloom prefilter: keep what's brighter than the threshold, 2x downsample
//   pass 1  bloom downsample (4 taps)
//   pass 2  bloom upsample (tent), added onto the next level up
//   pass 3  ambient occlusion from the depth + normals texture (half resolution)
//   pass 4  occlusion blur (depth-aware, separable: _BlurDir)
//   pass 5  final: occlusion, bloom, exposure, filmic (ACES) tone map, grade, vignette
Shader "Kilnwood/Post"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
    }
    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    sampler2D _BloomTex;
    sampler2D _AOTex;
    float4 _AOTex_TexelSize;
    sampler2D_float _CameraDepthTexture;
    sampler2D _CameraDepthNormalsTexture;

    float _Threshold, _Knee, _BloomIntensity, _Exposure, _Saturation, _Contrast, _Vignette, _Lift;
    float _AORadius, _AOIntensity, _AOFade;
    float4 _Tint;
    float4 _BlurDir;
    float4 _ProjParams2;     // x = 1/P00, y = 1/P11, z = P02, w = P12

    struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
    v2f vert(appdata_img v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord; return o; }

    half3 Box4(float2 uv, float2 t)
    {
        float4 d = t.xyxy * float4(-1, -1, 1, 1);
        return (tex2D(_MainTex, uv + d.xy).rgb + tex2D(_MainTex, uv + d.zy).rgb + tex2D(_MainTex, uv + d.xw).rgb + tex2D(_MainTex, uv + d.zw).rgb) * 0.25;
    }

    half4 fragPrefilter(v2f i) : SV_Target
    {
        half3 c = min(Box4(i.uv, _MainTex_TexelSize.xy), 6.0);   // clamp: one hot pixel must not bloom into a white star
        half br = max(c.r, max(c.g, c.b));
        half soft = clamp(br - _Threshold + _Knee, 0, 2 * _Knee);
        soft = soft * soft / (4 * _Knee + 1e-4);
        half contrib = max(soft, br - _Threshold) / max(br, 1e-4);
        return half4(c * contrib, 1);
    }

    half4 fragDown(v2f i) : SV_Target { return half4(Box4(i.uv, _MainTex_TexelSize.xy), 1); }

    half4 fragUp(v2f i) : SV_Target
    {
        float4 d = _MainTex_TexelSize.xyxy * float4(1, 1, -1, 0);
        half3 s = tex2D(_MainTex, i.uv - d.xy).rgb;
        s += tex2D(_MainTex, i.uv - d.wy).rgb * 2;
        s += tex2D(_MainTex, i.uv - d.zy).rgb;
        s += tex2D(_MainTex, i.uv + d.zw).rgb * 2;
        s += tex2D(_MainTex, i.uv).rgb * 4;
        s += tex2D(_MainTex, i.uv + d.xw).rgb * 2;
        s += tex2D(_MainTex, i.uv + d.zy).rgb;
        s += tex2D(_MainTex, i.uv + d.wy).rgb * 2;
        s += tex2D(_MainTex, i.uv + d.xy).rgb;
        return half4(s * (1.0 / 16), 1);
    }

    // View-space position (z forward, metres) at a screen uv.
    float3 ViewPos(float2 uv, float eye)
    {
        float2 ndc = uv * 2 - 1;
        return float3((ndc.x - _ProjParams2.z) * _ProjParams2.x * eye, (ndc.y - _ProjParams2.w) * _ProjParams2.y * eye, eye);
    }

    float Hash(float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }

    half4 fragAO(v2f i) : SV_Target
    {
        float eye = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv));
        if (eye > _AOFade) return 1;
        float3 n = DecodeViewNormalStereo(tex2D(_CameraDepthNormalsTexture, i.uv));
        n.z = -n.z;                       // to z-forward view space
        float3 p = ViewPos(i.uv, eye);
        float a = Hash(i.uv * _ScreenParams.xy) * 6.2831853;
        float2 rot = float2(cos(a), sin(a));
        const int N = 12;
        float occ = 0;
        [unroll]
        for (int k = 0; k < N; k++)
        {
            // Spiral of directions, pushed into the hemisphere around the normal.
            float t = (k + 0.5) / N;
            float ang = k * 2.39996;
            float2 d2 = float2(cos(ang), sin(ang));
            d2 = float2(d2.x * rot.x - d2.y * rot.y, d2.x * rot.y + d2.y * rot.x);
            float3 dir = normalize(float3(d2 * sqrt(1 - t * t), t * 2 - 1));
            dir *= sign(dot(dir, n)) ;
            float3 s = p + (dir * (0.25 + 0.75 * t) + n * 0.05) * _AORadius;
            float2 suv = float2(s.x / (s.z * _ProjParams2.x) + _ProjParams2.z, s.y / (s.z * _ProjParams2.y) + _ProjParams2.w) * 0.5 + 0.5;
            float sd = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, suv));
            float range = saturate(_AORadius / max(abs(p.z - sd), 1e-3));
            occ += (sd < s.z - 0.04 ? 1.0 : 0.0) * range;
        }
        float ao = 1 - occ / N;
        ao = lerp(1, ao, saturate(1 - eye / _AOFade));
        return pow(saturate(ao), _AOIntensity);
    }

    half4 fragBlur(v2f i) : SV_Target
    {
        float c0 = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv));
        float sum = 0, wsum = 0;
        [unroll]
        for (int k = -3; k <= 3; k++)
        {
            float2 uv = i.uv + _BlurDir.xy * _MainTex_TexelSize.xy * k;
            float d = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv));
            float w = exp(-k * k / 8.0) * saturate(1 - abs(d - c0) / (0.05 * c0 + 0.1));
            sum += tex2D(_MainTex, uv).r * w;
            wsum += w;
        }
        return sum / max(wsum, 1e-4);
    }

    half ACES1(half x)
    {
        const half a = 2.51, b = 0.03, c = 2.43, d = 0.59, e = 0.14;
        return saturate((x * (a * x + b)) / (x * (c * x + d) + e));
    }

    // Hue-preserving filmic curve: the curve is applied to the brightest channel and the colour scaled with it,
    // so mid tones keep their natural saturation (the per-channel curve made grass neon), and only very bright
    // colours roll gently toward white.
    half3 Filmic(half3 x)
    {
        half m = max(max(x.r, x.g), x.b);
        half3 hue = x * (ACES1(m) / max(m, 1e-4));
        half3 white = ACES1(m).xxx;
        half over = saturate((m - 1.5) / 6.0);
        return lerp(hue, white, over * 0.6);
    }

    half4 fragFinal(v2f i) : SV_Target
    {
        half3 c = tex2D(_MainTex, i.uv).rgb;
        c *= tex2D(_AOTex, i.uv).r;
        c += tex2D(_BloomTex, i.uv).rgb * _BloomIntensity;
        c *= _Exposure * _Tint.rgb;
        c = Filmic(c);
        half l = dot(c, half3(0.2126, 0.7152, 0.0722));
        c = lerp(l.xxx, c, _Saturation);
        // Contrast around mid grey in linear light (0.18), so it deepens darks without dimming the mid tones.
        c = 0.18 * pow(max(c, 0) / 0.18, _Contrast);
        c = saturate(c);
        half3 k = 1 - c;
        c += _Lift * k * k * k;   // lift the deepest shadows so they keep detail
        float2 v = i.uv - 0.5;
        c *= 1 - _Vignette * dot(v, v) * 1.6;
        return half4(c, 1);
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragPrefilter
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragDown
            ENDCG }
        Pass { Blend One One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragUp
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragAO
            #pragma target 3.0
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragBlur
            #pragma target 3.0
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragFinal
            ENDCG }
    }
}
