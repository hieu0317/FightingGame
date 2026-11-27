using System;
using System.IO;
using LoanDau.Art;
using UnityEditor;
using UnityEngine;

namespace LoanDau.EditorTools
{
    /// <summary>
    /// Import sprite prerender từ Blender (Assets/Art/Sprites/**.png):
    /// Sprite đơn, Pixels Per Unit + pivot lấy từ &lt;nhóm&gt;.sprites.json cùng thư mục,
    /// không mipmap, lọc Bilinear, không giới hạn kích thước dưới 8192 (lớp stage rộng 8192 px).
    /// Sửa JSON → ảnh tự import lại (khai báo phụ thuộc).
    /// </summary>
    public class SpriteAssetPostprocessor : AssetPostprocessor
    {
        public const string SpriteRoot = "Assets/Art/Sprites/";
        public const int DefaultPpu = 256;

        public override uint GetVersion() => 1;

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(SpriteRoot, StringComparison.Ordinal)) return;
            var ti = (TextureImporter)assetImporter;
            string dir = Path.GetDirectoryName(assetPath)!.Replace('\\', '/');
            string file = Path.GetFileName(assetPath);

            SpriteMeta meta = null;
            float ppu = DefaultPpu;
            foreach (var jsonPath in Directory.GetFiles(dir, "*.sprites.json"))
            {
                string p = jsonPath.Replace('\\', '/');
                context.DependsOnSourceAsset(p);
                var mf = SpriteMetaFile.Parse(File.ReadAllText(p));
                meta = mf?.Find(file);
                if (meta == null) continue;
                ppu = meta.ppu > 0 ? meta.ppu : (mf.ppu > 0 ? mf.ppu : DefaultPpu);
                break;
            }
            if (meta == null)
                context.LogImportWarning($"{assetPath}: không thấy mục '{file}' trong *.sprites.json cùng thư mục – dùng pivot đáy giữa.");

            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = ppu;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.filterMode = FilterMode.Bilinear;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.maxTextureSize = 8192;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;

            var s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            s.spriteAlignment = (int)SpriteAlignment.Custom;
            s.spritePivot = meta?.Pivot ?? new Vector2(0.5f, 0f);
            bool large = meta?.size_px is { Length: 2 } && meta.size_px[0] * meta.size_px[1] > 1024 * 1024;
            s.spriteMeshType = large ? SpriteMeshType.FullRect : SpriteMeshType.Tight;
            s.spriteGenerateFallbackPhysicsShape = false;
            ti.SetTextureSettings(s);
        }
    }
}
