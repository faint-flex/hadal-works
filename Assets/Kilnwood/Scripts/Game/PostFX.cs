using UnityEngine;

namespace Kilnwood.Game
{
    /// <summary>
    /// Finishing pass on the HDR camera (built-in pipeline): ambient occlusion in creases and under eaves,
    /// soft bloom on fire and sun glints, then a filmic tone map, a light grade and a vignette.
    /// Shader: Assets/Kilnwood/Shaders/KW_Post.shader (material KW_Post in Resources, made by Setup).
    /// If the material is missing it passes the image straight through.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class PostFX : MonoBehaviour
    {
        public static bool Enabled = true;
        public static bool AO = true;

        public float Exposure = 0.9f;
        public float BloomThreshold = 1.1f;
        public float BloomIntensity = 0.35f;
        public float Saturation = 1.08f;
        public float Contrast = 1.06f;
        public float Vignette = 0.28f;
        public float AORadius = 1.1f;
        public float AOIntensity = 1.6f;
        public float AOFade = 140f;
        public Color Tint = new Color(1.02f, 1f, 0.97f);

        Material _mat;
        Camera _cam;
        readonly RenderTexture[] _levels = new RenderTexture[6];

        void Awake()
        {
            _cam = GetComponent<Camera>();
            var m = Resources.Load<Material>("KW_Post");
            if (m != null && m.shader != null && m.shader.isSupported) _mat = new Material(m);
        }

        void OnEnable()
        {
            if (_cam == null) _cam = GetComponent<Camera>();
            _cam.depthTextureMode |= DepthTextureMode.Depth | DepthTextureMode.DepthNormals;
        }

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (_mat == null || !Enabled) { Graphics.Blit(src, dst); return; }

            // Bloom: prefilter at half size, down a few levels, then back up adding each level.
            int w = src.width / 2, h = src.height / 2;
            var fmt = RenderTextureFormat.DefaultHDR;
            _mat.SetFloat("_Threshold", BloomThreshold);
            _mat.SetFloat("_Knee", BloomThreshold * 0.5f);
            int count = 0;
            var cur = RenderTexture.GetTemporary(w, h, 0, fmt);
            Graphics.Blit(src, cur, _mat, 0);
            _levels[count++] = cur;
            while (count < _levels.Length && w > 16 && h > 16)
            {
                w /= 2; h /= 2;
                var next = RenderTexture.GetTemporary(w, h, 0, fmt);
                Graphics.Blit(cur, next, _mat, 1);
                _levels[count++] = next;
                cur = next;
            }
            for (int i = count - 1; i > 0; i--) Graphics.Blit(_levels[i], _levels[i - 1], _mat, 2);

            // Ambient occlusion at half size, blurred across then down.
            RenderTexture ao = null;
            if (AO)
            {
                var p = _cam.projectionMatrix;
                _mat.SetVector("_ProjParams2", new Vector4(1f / p.m00, 1f / p.m11, p.m02, p.m12));
                _mat.SetFloat("_AORadius", AORadius);
                _mat.SetFloat("_AOIntensity", AOIntensity);
                _mat.SetFloat("_AOFade", AOFade);
                int aw = src.width / 2, ah = src.height / 2;
                ao = RenderTexture.GetTemporary(aw, ah, 0, RenderTextureFormat.R8);
                var tmp = RenderTexture.GetTemporary(aw, ah, 0, RenderTextureFormat.R8);
                Graphics.Blit(src, ao, _mat, 3);
                _mat.SetVector("_BlurDir", new Vector4(1f, 0f, 0f, 0f));
                Graphics.Blit(ao, tmp, _mat, 4);
                _mat.SetVector("_BlurDir", new Vector4(0f, 1f, 0f, 0f));
                Graphics.Blit(tmp, ao, _mat, 4);
                RenderTexture.ReleaseTemporary(tmp);
            }

            _mat.SetTexture("_BloomTex", _levels[0]);
            _mat.SetTexture("_AOTex", ao != null ? (Texture)ao : Texture2D.whiteTexture);
            _mat.SetFloat("_BloomIntensity", BloomIntensity);
            _mat.SetFloat("_Exposure", Exposure);
            _mat.SetFloat("_Saturation", Saturation);
            _mat.SetFloat("_Contrast", Contrast);
            _mat.SetFloat("_Vignette", Vignette);
            _mat.SetColor("_Tint", Tint);
            Graphics.Blit(src, dst, _mat, 5);

            for (int i = 0; i < count; i++) { RenderTexture.ReleaseTemporary(_levels[i]); _levels[i] = null; }
            if (ao != null) RenderTexture.ReleaseTemporary(ao);
        }
    }
}
