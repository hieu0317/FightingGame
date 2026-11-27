using System.Collections.Generic;
using System.Linq;
using System.Text;
using LoanDau.Art;
using LoanDau.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LoanDau.EditorTools
{
    /// <summary>
    /// Kiểm tra asset Giai đoạn 1 sau khi import (tỷ lệ, pivot, trục, collider, vật liệu, PropData, stage).
    /// Menu: Loan Dau/Art/Kiểm tra asset · Batch:
    ///   Unity -batchmode -projectPath UnityProject -executeMethod LoanDau.EditorTools.ArtImportSelfTest.RunBatch
    /// </summary>
    public static class ArtImportSelfTest
    {
        // chiều cao khi đứng trên sàn (m) theo Asset Bible mục 5
        static readonly Dictionary<string, float> PropHeights = new()
        {
            { "ghe_nhua", 0.45f }, { "chai_thuy_tinh", 0.25f }, { "thung_phi", 0.88f },
        };

        [MenuItem("Loan Dau/Art/Kiểm tra asset")]
        public static void RunMenu()
        {
            var (ok, report) = Run();
            Debug.Log(report);
            EditorUtility.DisplayDialog("Kiểm tra asset", ok ? "Tất cả đạt." : "Có lỗi – xem Console.", "OK");
        }

        public static void RunBatch()
        {
            AssetDatabase.Refresh();
            var (ok, report) = Run();
            Debug.Log(report);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        public static (bool ok, string report) Run()
        {
            var sb = new StringBuilder("[ArtImportSelfTest]\n");
            int fails = 0;
            void Check(bool cond, string msg)
            {
                sb.AppendLine((cond ? "  PASS " : "  FAIL ") + msg);
                if (!cond) fails++;
            }

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---- palette + material
            var ti = AssetImporter.GetAtPath(ArtAssetPostprocessor.PalettePath) as TextureImporter;
            Check(ti && ti.filterMode == FilterMode.Point && !ti.mipmapEnabled, "palette.png: Point filter, không mipmap");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(ArtAssetPostprocessor.PaletteMatPath);
            Check(mat && mat.mainTexture, $"M_Palette tồn tại và có texture (shader {(mat ? mat.shader.name : "-")})");
            var rp = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
            Check(rp && rp.GetType().Name == "UniversalRenderPipelineAsset" && mat && mat.shader.name.StartsWith("Universal Render Pipeline"),
                  $"Render pipeline = URP ({(rp ? rp.name : "Built-in")}), M_Palette dùng shader URP");

            // ---- props
            foreach (var kv in PropHeights)
            {
                string path = $"Assets/Art/Props/prop_{kv.Key}.fbx";
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Check(model, $"{path} import được");
                if (!model) continue;
                var info = model.GetComponent<PropAssetInfo>();
                Check(info, $"{kv.Key}: có PropAssetInfo");
                if (!info) continue;
                Check(model.transform.localRotation == Quaternion.identity && model.transform.localScale == Vector3.one,
                      $"{kv.Key}: root rotation 0, scale 1");
                Check(info.grip, $"{kv.Key}: có _grip");
                Check(info.visual && info.visual.sharedMaterial == mat, $"{kv.Key}: mesh hiển thị dùng M_Palette");
                Check(info.debrisCollider && !info.debrisCollider.GetComponent<Renderer>(), $"{kv.Key}: _col là collider, không render");
                if (info.brokenRoot)
                    Check(!info.brokenRoot.activeSelf && info.brokenPieces.Length >= 4,
                          $"{kv.Key}: _broken ẩn, {info.brokenPieces.Length} mảnh có Rigidbody");
                if (info.dentedVariant) Check(!info.dentedVariant.activeSelf, $"{kv.Key}: _dented ẩn");

                var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
                var pai = go.GetComponent<PropAssetInfo>();
                var r = pai.visual;
                // tư thế cầm tay: đầu đánh hướng +Y (Unity) tính từ pivot = điểm cầm
                if (pai.size != PropSize.Large && pai.size != PropSize.ExtraLarge)
                    Check(r.bounds.center.y > 0.05f, $"{kv.Key}: cầm tay – thân vật nằm phía +Y của điểm cầm (center.y={r.bounds.center.y:F3})");
                Check(NoStrayRotation(go), $"{kv.Key}: mọi mesh con có rotation 0 (không dư xoay 90° do exporter)");
                // tư thế nằm sàn. Renderer.bounds / Mesh.bounds là AABB của hộp cục bộ đã xoay (phình ra khi xoay
                // lệch trục). PhysX tính AABB của collider convex từ đỉnh thật của bao lồi → chính xác.
                pai.PlaceAtRest(new Vector3(2f, 0f, 1f));
                Physics.SyncTransforms();
                var b = pai.debrisCollider.bounds;
                Check(Mathf.Abs(b.min.y) < 0.02f, $"{kv.Key}: đặt trên sàn – đáy ở y=0 (min.y={b.min.y:F3})");
                Check(Mathf.Abs(b.size.y - kv.Value) < 0.03f, $"{kv.Key}: cao {b.size.y:F3} m (spec {kv.Value})");
                Check(Mathf.Abs(b.center.x - 2f) < 0.3f && Mathf.Abs(b.center.z - 1f) < 0.3f,
                      $"{kv.Key}: đứng quanh điểm spawn (center {b.center.x:F2},{b.center.z:F2})");
                Object.DestroyImmediate(go);

                var data = AssetDatabase.LoadAssetAtPath<PropData>($"{ArtAssetPostprocessor.PropDataFolder}/PropData_{kv.Key}.asset");
                Check(data && data.model == model, $"{kv.Key}: PropData tồn tại, trỏ đúng model");
                if (data)
                    sb.AppendLine($"        PropData: {data.size} / {data.weaponType} / {data.traits} / đánh {data.hitDamage} / ném {data.throwDamage} / bền {data.durability} / {data.triangleCount} tam giác");
            }

            // ---- nhân vật tạm
            foreach (var p in new[] { "p1", "p2" })
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Art/Characters/char_proto_{p}.fbx");
                Check(model, $"char_proto_{p} import được");
                if (!model) continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
                var rend = go.GetComponentsInChildren<Renderer>().First();
                Check(Mathf.Abs(rend.bounds.size.y - 1.8f) < 0.02f && Mathf.Abs(rend.bounds.min.y) < 0.02f,
                      $"char_proto_{p}: cao {rend.bounds.size.y:F3} m, chân ở y=0");
                var s = go.GetComponent<CharacterSockets>();
                Check(s && s.handRight && s.handLeft, $"char_proto_{p}: có socket tay");
                // nhân vật nhìn +Z → tay phải ở +X
                if (s && s.handRight) Check(s.handRight.position.x > 0f, $"char_proto_{p}: tay phải ở +X (mặt nhìn +Z)");
                Object.DestroyImmediate(go);
            }

            // ---- stage
            var stage = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Stages/ChoDem/stage_chodem_blockout.fbx");
            Check(stage, "stage_chodem_blockout import được");
            if (stage)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(stage);
                var layout = go.GetComponent<StageLayout>();
                Check(layout && layout.markers.Length == 12, $"Chợ Đêm: StageLayout có {(layout ? layout.markers.Length : 0)}/12 marker");
                if (layout)
                {
                    var p1 = layout.markers.FirstOrDefault(m => m.kind == StageMarkerKind.PlayerSpawn && m.playerIndex == 1);
                    var p2 = layout.markers.FirstOrDefault(m => m.kind == StageMarkerKind.PlayerSpawn && m.playerIndex == 2);
                    Check(p1 && p2 && p1.transform.position.x < 0 && p2.transform.position.x > 0,
                          "Chợ Đêm: P1 bên trái (-X), P2 bên phải (+X) khi camera ở -Z");
                    var props = layout.markers.Where(m => m.kind == StageMarkerKind.PropSpawn).ToArray();
                    Check(props.Length == 8 && props.All(m => !string.IsNullOrEmpty(m.defaultProp)), "Chợ Đêm: 8 điểm spawn prop có default_prop");
                    float sum = props.Sum(m => m.transform.position.x);
                    Check(Mathf.Abs(sum) < 0.01f, "Chợ Đêm: spawn prop đối xứng hai nửa sân");
                }
                Check(NoStrayRotation(go), "Chợ Đêm: mọi mesh con có rotation 0 (không dư xoay 90° do exporter)");
                var facade = go.GetComponentsInChildren<Transform>().First(t => t.name == "stage_chodem_nhapho_01").GetComponent<Renderer>();
                Check(facade.bounds.center.z > 3f && facade.bounds.min.y > -0.01f,
                      $"Chợ Đêm: hậu cảnh đứng thẳng ở +Z (z={facade.bounds.center.z:F2}, min.y={facade.bounds.min.y:F2})");
                Check(facade.transform.parent && facade.transform.parent.name == "Background", "Chợ Đêm: cây phân cấp được dựng lại (nhà phố nằm dưới Background)");
                var floorCol = go.GetComponentsInChildren<BoxCollider>().FirstOrDefault(c => c.name == "stage_chodem_san_col");
                var fb = floorCol ? MeshCornersAabb(floorCol.transform, new Bounds(floorCol.center, floorCol.size)) : default;
                Check(floorCol && Mathf.Abs(fb.max.y) < 0.01f && Mathf.Abs(fb.size.x - 24f) < 0.05f,
                      $"Chợ Đêm: collider sàn rộng {fb.size.x:F2} m, mặt trên y={fb.max.y:F3}");
                var plat = go.GetComponentsInChildren<BoxCollider>().FirstOrDefault(c => c.name == "stage_chodem_maihien_01_col");
                var pb = plat ? MeshCornersAabb(plat.transform, new Bounds(plat.center, plat.size)) : default;
                Check(plat && Mathf.Abs(pb.max.y - 2f) < 0.01f, $"Chợ Đêm: bục mái hiên cao {pb.max.y:F3} m (spec 2.0)");
                Object.DestroyImmediate(go);
            }

            // ---- sprite 2D (prerender)
            foreach (var (path, json) in new[]
            {
                ("Assets/Art/Sprites/Props/ghe_nhua/ghe_nhua.png", "Assets/Art/Sprites/Props/ghe_nhua/ghe_nhua.sprites.json"),
                ("Assets/Art/Sprites/Props/ghe_nhua/ghe_nhua_held.png", "Assets/Art/Sprites/Props/ghe_nhua/ghe_nhua.sprites.json"),
                ("Assets/Art/Sprites/Characters/char_proto_p1/char_proto_p1_idle.png", "Assets/Art/Sprites/Characters/char_proto_p1/char_proto_p1.sprites.json"),
                ("Assets/Art/Sprites/Stages/ChoDem/chodem_play.png", "Assets/Art/Sprites/Stages/ChoDem/chodem.sprites.json"),
            })
            {
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                var mf = SpriteMetaFile.Parse(System.IO.File.ReadAllText(json));
                var meta = mf.Find(System.IO.Path.GetFileName(path));
                Check(sp && meta != null, $"{System.IO.Path.GetFileName(path)}: import là Sprite, có metadata");
                if (!sp || meta == null) continue;
                var pivotN = new Vector2(sp.pivot.x / sp.rect.width, sp.pivot.y / sp.rect.height);
                Check(Mathf.Approximately(sp.pixelsPerUnit, 256f) && (pivotN - meta.Pivot).magnitude < 0.01f
                      && (int)sp.rect.width == meta.size_px[0],
                      $"{meta.file}: PPU {sp.pixelsPerUnit}, pivot {pivotN} (json {meta.Pivot}), {sp.rect.width}px");
            }
            var chair = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/Props/ghe_nhua/ghe_nhua.png");
            if (chair) Check(Mathf.Abs(chair.bounds.size.y - 0.45f * Mathf.Cos(12f * Mathf.Deg2Rad) - 0.40f * Mathf.Sin(12f * Mathf.Deg2Rad)) < 0.06f,
                             $"ghe_nhua sprite cao {chair.bounds.size.y:F3} unit (≈ 0.45 m chiều cao + mặt trên do camera nghiêng 12°)");
            var layoutAsset = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Art/Sprites/Stages/ChoDem/chodem_layout.json");
            var chodemLayout = layoutAsset ? StageLayout2D.Parse(layoutAsset) : null;
            Check(chodemLayout != null && chodemLayout.platforms.Length == 2 && chodemLayout.prop_spawns.Length == 8 && chodemLayout.player_spawns.Length == 2,
                  "chodem_layout.json: 2 bục, 8 spawn prop, 2 spawn người chơi");

            sb.AppendLine(fails == 0 ? "KẾT QUẢ: TẤT CẢ ĐẠT" : $"KẾT QUẢ: {fails} LỖI");
            return (fails == 0, sb.ToString());
        }

        /// <summary>AABB thế giới của 8 góc một hộp cục bộ.</summary>
        static Bounds MeshCornersAabb(Transform t, Bounds local)
        {
            var m = t.localToWorldMatrix;
            var b = new Bounds(m.MultiplyPoint3x4(local.min), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                var c = new Vector3(
                    (i & 1) == 0 ? local.min.x : local.max.x,
                    (i & 2) == 0 ? local.min.y : local.max.y,
                    (i & 4) == 0 ? local.min.z : local.max.z);
                b.Encapsulate(m.MultiplyPoint3x4(c));
            }
            return b;
        }

        static bool NoStrayRotation(GameObject root) =>
            root.GetComponentsInChildren<MeshFilter>(true).All(mf =>
                Quaternion.Angle(mf.transform.localRotation, Quaternion.identity) < 0.1f)
            && root.GetComponentsInChildren<Collider>(true).All(c =>
                Quaternion.Angle(c.transform.localRotation, Quaternion.identity) < 0.1f);
    }
}
