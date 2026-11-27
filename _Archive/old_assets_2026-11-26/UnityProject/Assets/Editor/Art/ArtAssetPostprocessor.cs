using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LoanDau.Art;
using LoanDau.Data;
using UnityEditor;
using UnityEngine;

namespace LoanDau.EditorTools
{
    /// <summary>
    /// Tự cấu hình asset từ Blender theo quy chuẩn Asset Bible khi import vào Assets/Art/.
    ///
    ///  palette.png      → Point filter, không nén, không mipmap; tự tạo Materials/M_Palette.
    ///  *.fbx            → scale 1, normal từ file (flat), không material nhúng → gán M_Palette.
    ///  *_col            → tắt hiển thị, thành collider (prop: MeshCollider convex, stage: BoxCollider).
    ///  *_broken         → ẩn; mỗi mảnh có Rigidbody kinematic + BoxCollider (mảnh vụn PhysX).
    ///  *_dented         → ẩn (bản móp, bật khi trúng đòn).
    ///  prop_*.fbx       → PropAssetInfo trên root + tạo/cập nhật Assets/Data/Props/PropData_*.asset.
    ///  char_*.fbx       → CharacterSockets (capsule tạm); nhân vật Mixamo: Rig = Humanoid.
    ///  stage_*.fbx      → StageMarker cho spawn_p*, spawn_prop_*, hazard_*; StageLayout trên root.
    /// </summary>
    public class ArtAssetPostprocessor : AssetPostprocessor
    {
        public const string ArtRoot = "Assets/Art/";
        public const string PalettePath = "Assets/Art/Textures/palette.png";
        public const string PaletteMatPath = "Assets/Art/Materials/M_Palette.mat";
        public const string PropDataFolder = "Assets/Data/Props";
        const int Version = 4;   // tăng khi đổi logic để Unity import lại mọi model

        public override uint GetVersion() => Version;

        static bool IsArt(string path) => path.StartsWith(ArtRoot, StringComparison.Ordinal);
        static bool IsProp(string path) => IsArt(path) && path.Contains("/Props/") && FileName(path).StartsWith("prop_");
        static bool IsCharacter(string path) => IsArt(path) && path.Contains("/Characters/");
        static bool IsStage(string path) => IsArt(path) && path.Contains("/Stages/");
        static string FileName(string path) => System.IO.Path.GetFileNameWithoutExtension(path);

        // ------------------------------------------------------------------ texture
        void OnPreprocessTexture()
        {
            if (!string.Equals(assetPath, PalettePath, StringComparison.OrdinalIgnoreCase)) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = true;
            ti.filterMode = FilterMode.Point;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.mipmapEnabled = false;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.alphaSource = TextureImporterAlphaSource.None;
        }

        // ------------------------------------------------------------------ model: settings
        void OnPreprocessModel()
        {
            if (!IsArt(assetPath)) return;
            var mi = (ModelImporter)assetImporter;
            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.bakeAxisConversion = false;
            mi.importNormals = ModelImporterNormals.Import;
            mi.importTangents = ModelImporterTangents.None;
            mi.importBlendShapes = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importVisibility = false;
            mi.isReadable = false;
            mi.meshCompression = ModelImporterMeshCompression.Off;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;   // dùng M_Palette dùng chung

            bool rigged = IsCharacter(assetPath) && !FileName(assetPath).StartsWith("char_proto");
            if (rigged)
            {
                // Nhân vật đã rig từ Mixamo (Asset Bible mục 3): Rig = Humanoid
                mi.animationType = ModelImporterAnimationType.Human;
                mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            }
            else
            {
                mi.animationType = ModelImporterAnimationType.None;
                mi.importAnimation = false;
                mi.importConstraints = false;
            }
        }

        // ------------------------------------------------------------------ model: custom props
        void OnPostprocessGameObjectWithUserProperties(GameObject go, string[] names, object[] values)
        {
            if (!IsArt(assetPath) || names == null || names.Length == 0) return;
            var store = go.GetComponent<FbxUserProperties>() ?? go.AddComponent<FbxUserProperties>();
            for (int i = 0; i < names.Length; i++)
                store.Set(names[i], ToInvariant(values[i]));
        }

        static string ToInvariant(object v) => v switch
        {
            null => "",
            float f => f.ToString(CultureInfo.InvariantCulture),
            double d => d.ToString(CultureInfo.InvariantCulture),
            IFormattable fm => fm.ToString(null, CultureInfo.InvariantCulture),
            _ => v.ToString(),
        };

        // ------------------------------------------------------------------ model: hierarchy
        void OnPostprocessModel(GameObject root)
        {
            if (!IsArt(assetPath)) return;

            context.DependsOnSourceAsset(PaletteMatPath);
            var palette = AssetDatabase.LoadAssetAtPath<Material>(PaletteMatPath);
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (palette) r.sharedMaterials = Enumerable.Repeat(palette, Mathf.Max(1, r.sharedMaterials.Length)).ToArray();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            if (!palette)
                context.LogImportWarning($"Chưa có {PaletteMatPath} – model sẽ tự import lại khi material được tạo.");

            RestoreHierarchy(root);

            bool stage = IsStage(assetPath);
            foreach (var t in root.GetComponentsInChildren<Transform>(true).ToArray())
            {
                if (!t) continue;
                string n = t.name;
                if (n.EndsWith("_col", StringComparison.Ordinal)) MakeCollider(t.gameObject, stage);
                else if (n.EndsWith("_broken", StringComparison.Ordinal)) MakeBroken(t.gameObject);
                else if (n.EndsWith("_dented", StringComparison.Ordinal)) t.gameObject.SetActive(false);
            }

            if (IsProp(assetPath)) SetupProp(root);
            else if (IsCharacter(assetPath)) SetupCharacter(root);
            else if (stage) SetupStage(root);
        }

        /// <summary>
        /// prop_utils.export_fbx làm phẳng cây (mọi object là con trực tiếp của root) để tránh lỗi bake trục
        /// của exporter Blender; tên cha gốc nằm trong custom property 'fbx_parent'. Dựng lại cây ở đây.
        /// </summary>
        static void RestoreHierarchy(GameObject root)
        {
            var all = root.GetComponentsInChildren<Transform>(true);
            var byName = new Dictionary<string, Transform>();
            foreach (var t in all) byName.TryAdd(t.name, t);
            foreach (var t in all)
            {
                var p = t.GetComponent<FbxUserProperties>();
                if (!p || !p.TryGet("fbx_parent", out var parentName)) continue;
                if (byName.TryGetValue(parentName, out var parent) && parent != t)
                    t.SetParent(parent, true);
                if (p.Keys.Count == 1) UnityEngine.Object.DestroyImmediate(p);   // chỉ có fbx_parent → bỏ
            }
        }

        static void MakeCollider(GameObject go, bool stage)
        {
            var mf = go.GetComponent<MeshFilter>();
            var mr = go.GetComponent<MeshRenderer>();
            if (!mf) return;
            var mesh = mf.sharedMesh;
            if (stage)
            {
                var bc = go.AddComponent<BoxCollider>();
                bc.center = mesh.bounds.center;
                bc.size = mesh.bounds.size;
            }
            else
            {
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
                mc.convex = true;
            }
            if (mr) UnityEngine.Object.DestroyImmediate(mr);
            UnityEngine.Object.DestroyImmediate(mf);
        }

        static void MakeBroken(GameObject go)
        {
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                var bc = mf.gameObject.AddComponent<BoxCollider>();
                bc.center = mf.sharedMesh.bounds.center;
                bc.size = Vector3.Max(mf.sharedMesh.bounds.size, Vector3.one * 0.01f);
                var rb = mf.gameObject.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.mass = 0.2f;
            }
            go.SetActive(false);
        }

        // ------------------------------------------------------------------ prop
        void SetupProp(GameObject root)
        {
            string key = FileName(assetPath).Substring("prop_".Length);
            var props = FindProps(root, "prop_size");
            var info = root.AddComponent<PropAssetInfo>();
            info.key = key;
            Transform Find(string suffix) => root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == key + suffix);

            var vis = Find("");
            info.visual = vis ? vis.GetComponent<Renderer>() : null;
            info.grip = Find("_grip");
            var col = Find("_col");
            info.debrisCollider = col ? col.GetComponent<Collider>() : null;
            var broken = Find("_broken");
            info.brokenRoot = broken ? broken.gameObject : null;
            info.brokenPieces = broken ? broken.GetComponentsInChildren<Rigidbody>(true) : Array.Empty<Rigidbody>();
            var dented = Find("_dented");
            info.dentedVariant = dented ? dented.gameObject : null;

            if (props)
            {
                info.size = ParseSize(props.Get("prop_size"));
                info.weaponType = ParseWeapon(props.Get("weapon_type"));
                info.traits = ParseTraits(props.Get("traits"));
                info.hitDamage = props.GetInt("hit_dmg");
                info.throwDamage = props.GetInt("throw_dmg");
                info.durability = Mathf.Max(1, props.GetInt("durability", 1));
                info.restRotation = BlenderSpace.EulerXYZDegToUnity(ParseVec(props.Get("rest_euler_blender_deg")));
                info.restOffset = BlenderSpace.PointToUnity(ParseVec(props.Get("rest_offset_blender")));
            }
            else context.LogImportWarning($"{assetPath}: không thấy custom property 'prop_size' – kiểm tra export FBX có bật Custom Properties.");

            // AABB trong không gian prop: ưu tiên mesh _col, không có thì dùng mesh hiển thị
            var src = col ? col.GetComponent<Collider>() as MeshCollider : null;
            Mesh m = src ? src.sharedMesh : vis ? vis.GetComponent<MeshFilter>()?.sharedMesh : null;
            Transform mt = src ? col : vis;
            if (m && mt) info.localBounds = TransformBounds(m.bounds, root.transform.worldToLocalMatrix * mt.localToWorldMatrix);
            if (!info.grip) context.LogImportWarning($"{assetPath}: thiếu Empty {key}_grip.");
            if (!info.visual) context.LogImportWarning($"{assetPath}: thiếu mesh hiển thị tên '{key}'.");
        }

        // ------------------------------------------------------------------ character
        static void SetupCharacter(GameObject root)
        {
            var all = root.GetComponentsInChildren<Transform>(true);
            var r = all.FirstOrDefault(t => t.name.EndsWith("_hand_r_socket"));
            var l = all.FirstOrDefault(t => t.name.EndsWith("_hand_l_socket"));
            if (!r && !l) return;
            var s = root.AddComponent<CharacterSockets>();
            s.handRight = r;
            s.handLeft = l;
        }

        // ------------------------------------------------------------------ stage
        static void SetupStage(GameObject root)
        {
            // Blender: camera ở -Y, hậu cảnh ở +Y → sau đổi trục, hậu cảnh nằm ở Unity -Z.
            // Xoay root 180° quanh Y để theo quy ước Unity: camera ở -Z nhìn +Z, hậu cảnh ở +Z, P1 bên trái (-X).
            root.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * root.transform.localRotation;
            var markers = new List<StageMarker>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                StageMarker mk = null;
                var p = t.GetComponent<FbxUserProperties>();
                if (n == "spawn_p1" || n == "spawn_p2")
                {
                    mk = t.gameObject.AddComponent<StageMarker>();
                    mk.kind = StageMarkerKind.PlayerSpawn;
                    mk.playerIndex = n[^1] - '0';
                }
                else if (n.StartsWith("spawn_prop_"))
                {
                    mk = t.gameObject.AddComponent<StageMarker>();
                    mk.kind = StageMarkerKind.PropSpawn;
                    mk.propSize = ParseSize(p ? p.Get("prop_size") : "");
                    mk.defaultProp = p ? p.Get("default_prop") : "";
                }
                else if (n.StartsWith("hazard_"))
                {
                    mk = t.gameObject.AddComponent<StageMarker>();
                    mk.kind = StageMarkerKind.HazardPath;
                    mk.intervalSeconds = p ? p.GetFloat("interval_s") : 0f;
                }
                if (mk) markers.Add(mk);
            }
            if (markers.Count == 0) return;
            var layout = root.AddComponent<StageLayout>();
            var rp = FindProps(root, "arena_width_m");
            if (rp)
            {
                layout.arenaWidth = rp.GetFloat("arena_width_m", 24f);
                layout.arenaDepth = rp.GetFloat("arena_depth_m", 3f);
            }
            layout.markers = markers.ToArray();
        }

        // ------------------------------------------------------------------ all assets: M_Palette, PropData
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            bool paletteImported = imported.Any(p => string.Equals(p, PalettePath, StringComparison.OrdinalIgnoreCase));
            if (paletteImported && !AssetDatabase.LoadAssetAtPath<Material>(PaletteMatPath))
            {
                CreatePaletteMaterial();
                // các model đã import trước khi có material → import lại để gán M_Palette
                foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Art" }))
                    AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
            }

            foreach (var path in imported.Where(p => IsProp(p) && p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)))
                UpdatePropData(path);
        }

        public static Material CreatePaletteMaterial()
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(PalettePath);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader) { name = "M_Palette", mainTexture = tex };
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.1f);
            EnsureFolder("Assets/Art/Materials");
            AssetDatabase.CreateAsset(mat, PaletteMatPath);
            Debug.Log($"[Art] Tạo {PaletteMatPath} (shader {shader.name}).");
            return mat;
        }

        static void UpdatePropData(string modelPath)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var info = model ? model.GetComponent<PropAssetInfo>() : null;
            if (!info) return;
            EnsureFolder(PropDataFolder);
            string dataPath = $"{PropDataFolder}/PropData_{info.key}.asset";
            var data = AssetDatabase.LoadAssetAtPath<PropData>(dataPath);
            bool created = !data;
            if (created)
            {
                data = ScriptableObject.CreateInstance<PropData>();
                // nhóm cân bằng: chỉ điền lần đầu
                data.size = info.size;
                data.weaponType = info.weaponType;
                data.traits = info.traits;
                data.hitDamage = info.hitDamage;
                data.throwDamage = info.throwDamage;
                data.durability = info.durability;
                AssetDatabase.CreateAsset(data, dataPath);
            }
            data.model = model;
            data.gripOffset = info.grip ? info.grip.localPosition : Vector3.zero;
            data.gripRotation = info.grip ? info.grip.localRotation : Quaternion.identity;
            data.localBounds = info.localBounds;
            data.restRotation = info.restRotation;
            data.restOffset = info.restOffset;
            var mf = info.visual ? info.visual.GetComponent<MeshFilter>() : null;
            data.triangleCount = mf && mf.sharedMesh ? (int)(mf.sharedMesh.GetIndexCount(0) / 3) : 0;
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
            if (created) Debug.Log($"[Art] Tạo {dataPath}");
        }

        // ------------------------------------------------------------------ helpers
        static FbxUserProperties FindProps(GameObject root, string key) =>
            root.GetComponentsInChildren<FbxUserProperties>(true).FirstOrDefault(p => p.TryGet(key, out _));

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            var parent = System.IO.Path.GetDirectoryName(folder)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
        }

        static Bounds TransformBounds(Bounds b, Matrix4x4 m)
        {
            var c = m.MultiplyPoint3x4(b.center);
            var e = b.extents;
            var ax = m.MultiplyVector(new Vector3(e.x, 0, 0));
            var ay = m.MultiplyVector(new Vector3(0, e.y, 0));
            var az = m.MultiplyVector(new Vector3(0, 0, e.z));
            var ext = new Vector3(
                Mathf.Abs(ax.x) + Mathf.Abs(ay.x) + Mathf.Abs(az.x),
                Mathf.Abs(ax.y) + Mathf.Abs(ay.y) + Mathf.Abs(az.y),
                Mathf.Abs(ax.z) + Mathf.Abs(ay.z) + Mathf.Abs(az.z));
            return new Bounds(c, ext * 2f);
        }

        static Vector3 ParseVec(string s)
        {
            var v = Vector3.zero;
            if (string.IsNullOrEmpty(s)) return v;
            var parts = s.Split(',');
            for (int i = 0; i < Mathf.Min(3, parts.Length); i++)
                v[i] = float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 0f;
            return v;
        }

        public static PropSize ParseSize(string s) => s switch
        {
            "nho" => PropSize.Small,
            "vua" => PropSize.Medium,
            "lon" => PropSize.Large,
            "sieu_lon" => PropSize.ExtraLarge,
            _ => PropSize.Medium,
        };

        public static WeaponType ParseWeapon(string s) => s switch
        {
            "blunt" => WeaponType.Blunt,
            "long" => WeaponType.Long,
            "heavy" => WeaponType.Heavy,
            _ => WeaponType.None,
        };

        public static PropTraits ParseTraits(string s)
        {
            var t = PropTraits.None;
            if (string.IsNullOrWhiteSpace(s)) return t;
            foreach (var part in s.Split(',', ' ', ';'))
            {
                switch (part.Trim())
                {
                    case "flammable": t |= PropTraits.Flammable; break;
                    case "explosive": t |= PropTraits.Explosive; break;
                    case "metal": t |= PropTraits.Metal; break;
                    case "liquid": t |= PropTraits.Liquid; break;
                    case "fragile": t |= PropTraits.Fragile; break;
                }
            }
            return t;
        }
    }
}
