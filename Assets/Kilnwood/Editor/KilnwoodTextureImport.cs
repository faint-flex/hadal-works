using UnityEditor;

namespace Kilnwood.EditorTools
{
    /// <summary>0.17 photoreal pass: import settings for the scanned surfaces (Resources/PBR) and the sky (Resources/Sky).
    /// Detail maps are stored linear (their average is tuned to the tint maths), normal maps as normal maps, roughness linear.</summary>
    public class KilnwoodTextureImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            var ti = (TextureImporter)assetImporter;
            string p = assetPath.Replace('\\', '/');
            if (p.Contains("/Resources/PBR/"))
            {
                ti.mipmapEnabled = true;
                ti.anisoLevel = 8;
                ti.maxTextureSize = 2048;
                ti.wrapMode = UnityEngine.TextureWrapMode.Repeat;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                if (p.EndsWith("_nor.jpg")) ti.textureType = TextureImporterType.NormalMap;
                else { ti.textureType = TextureImporterType.Default; ti.sRGBTexture = false; }
            }
            else if (p.Contains("/Resources/Foliage/"))
            {
                // 0.18: alpha-cut foliage cards; keep the leaf coverage in the small mips so far trees don't thin out.
                ti.textureType = TextureImporterType.Default;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = true;
                ti.mipMapsPreserveCoverage = true;
                ti.alphaTestReferenceValue = 0.45f;
                ti.anisoLevel = 4;
                ti.wrapMode = UnityEngine.TextureWrapMode.Clamp;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
            }
            else if (p.Contains("/Resources/Sky/"))
            {
                ti.textureType = TextureImporterType.Default;
                ti.textureShape = TextureImporterShape.Texture2D;
                ti.mipmapEnabled = false;
                ti.wrapModeU = UnityEngine.TextureWrapMode.Repeat;
                ti.wrapModeV = UnityEngine.TextureWrapMode.Clamp;
                ti.maxTextureSize = 4096;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
            }
        }
    }
}
