using System;
using UnityEditor;
using UnityEngine;

namespace LoanDau.EditorTools
{
    /// <summary>
    /// Import pixel art của Assets/Art/PixelPack theo một chuẩn duy nhất:
    /// PPU 64 cho MỌI ảnh (nhân vật, prop, stage – trước đây stage để 32 làm pixel nền to gấp đôi pixel nhân vật),
    /// lọc Point, không nén, không mipmap. Giữ nguyên cách cắt sprite và pivot do bộ PixelPack đặt.
    /// </summary>
    public class PixelPackTextureImporter : AssetPostprocessor
    {
        public const string Root = "Assets/Art/PixelPack/";
        /// <summary>Ảnh sinh từ PixelPack bằng Tools/extract_floor_props.py (vật dụng nằm sàn đúng tỷ lệ).</summary>
        public const string DerivedRoot = "Assets/Art/PixelPackDerived/";
        public const int Ppu = 64;

        public override uint GetVersion() => 3;

        /// <summary>Ảnh vẽ độ phân giải cao hiển thị thu nhỏ trong UI (chân dung 512 px, banner tuyệt chiêu, logo).</summary>
        static bool IsPaintedUi(string path)
        {
            if (!path.Contains("/UI/")) return false;
            string f = System.IO.Path.GetFileName(path);
            return f.StartsWith("portrait_") || f.StartsWith("super_") || f.StartsWith("logo") || f.StartsWith("character_select");
        }

        void OnPreprocessTexture()
        {
            bool derived = assetPath.StartsWith(DerivedRoot, StringComparison.Ordinal);
            if (!derived && !assetPath.StartsWith(Root, StringComparison.Ordinal)) return;
            var ti = (TextureImporter)assetImporter;
            if (ti.textureType != TextureImporterType.Sprite)
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
            }
            if (derived)
            {
                // vật dụng nằm sàn: pivot giữa đáy = điểm chạm sàn
                var s = new TextureImporterSettings();
                ti.ReadTextureSettings(s);
                s.spriteAlignment = (int)SpriteAlignment.BottomCenter;
                s.spritePivot = new UnityEngine.Vector2(0.5f, 0f);
                ti.SetTextureSettings(s);
            }
            ti.spritePixelsPerUnit = Ppu;
            bool painted = IsPaintedUi(assetPath);
            ti.filterMode = painted ? FilterMode.Bilinear : FilterMode.Point;      // ảnh vẽ thu nhỏ: lọc mịn, không răng cưa
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = painted;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.maxTextureSize = Math.Max(ti.maxTextureSize, 4096);
        }
    }
}
