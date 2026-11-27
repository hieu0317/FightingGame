using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LoanDau.EditorTools
{
    public class PixelPackImporter : AssetPostprocessor
    {
        const string Root = "Assets/Art/PixelPack/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root, System.StringComparison.Ordinal)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = assetPath.Contains("/Stages/") ? 32 : 64;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(.5f, assetPath.Contains("/Characters/") ? 6f / 144f : 0f);
            importer.SetTextureSettings(settings);
        }

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (imported.Any(path => path.StartsWith(Root + "Characters/", System.StringComparison.Ordinal)
                    && path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase)))
                EditorApplication.delayCall += BuildMissingClips;
        }

        static void BuildMissingClips()
        {
            var folder = Root + "Characters";
            if (Directory.Exists(folder) && Directory.GetFiles(folder, "*.png", SearchOption.AllDirectories).Length == 120
                && Directory.GetFiles(folder, "*.anim", SearchOption.AllDirectories).Length < 30)
                BuildClips();
        }

        [MenuItem("Loan Dau/Pixel Art/Tạo clip kỹ năng")]
        public static void BuildClips()
        {
            foreach (var first in Directory.GetFiles(Root + "Characters", "0.png", SearchOption.AllDirectories))
            {
                var dir = Path.GetDirectoryName(first).Replace('\\', '/');
                var path = dir + "/" + Path.GetFileName(dir) + ".anim";
                var keys = Enumerable.Range(0, 4).Select(i => new ObjectReferenceKeyframe {
                    time = i / 10f, value = AssetDatabase.LoadAssetAtPath<Sprite>(dir + "/" + i + ".png")
                }).ToArray();
                if (keys.Any(k => k.value == null)) throw new InvalidDataException(dir);
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                bool create = clip == null;
                if (create) clip = new AnimationClip();
                clip.frameRate = 10;
                AnimationUtility.SetObjectReferenceCurve(clip,
                    EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
                if (create) AssetDatabase.CreateAsset(clip, path);
                else EditorUtility.SetDirty(clip);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
