using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using LoanDau.Data;
using LoanDau.Game;
using LoanDau.UI;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LoanDau.EditorTools
{
    /// <summary>
    /// Dựng dữ liệu game từ bộ PixelPack + catalog.json, và scene chơi thử Assets/Scenes/Prototype.unity.
    ///   Assets/Data/Fighters/Fighter_&lt;id&gt;.asset  – 6 nhân vật: chỉ số, 5 chiêu, clip tương tác với 12 vật dụng
    ///   Assets/Data/Props/Prop_&lt;id&gt;.asset        – 12 vật dụng (số liệu cân bằng chỉ điền lần đầu)
    ///   Assets/Data/Stages/Stage_&lt;id&gt;.asset      – 4 stage + vị trí vật dụng đối xứng
    /// Chạy lại an toàn: cập nhật clip/sprite, giữ số liệu designer đã chỉnh.
    /// Menu: Loan Dau → PixelPack → Dựng dữ liệu + scene. Tự chạy một lần khi chưa có scene.
    /// </summary>
    [InitializeOnLoad]
    public static class PixelPackBuilder
    {
        public const string Pack = "Assets/Art/PixelPack";
        public const string DataRoot = "Assets/Data";
        public const string ScenePath = "Assets/Scenes/Prototype.unity";
        const int Tick = 60;

        static PixelPackBuilder()
        {
            if (Application.isBatchMode) return;
            EditorApplication.delayCall += () =>
            {
                if (File.Exists(ScenePath) || !AssetDatabase.IsValidFolder(Pack)) return;
                Debug.Log("[PixelPack] Chưa có scene Prototype – tự dựng dữ liệu + scene lần đầu.");
                Build();
                PixelPackSelfTest.RunAndLog();
            };
        }

        [MenuItem("Loan Dau/PixelPack/Dựng dữ liệu + scene")]
        public static void BuildMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("PixelPack", "Đang ở chế độ Play – thoát Play rồi chạy lại menu này.", "OK");
                return;
            }
            Build();
            var ok = PixelPackSelfTest.RunAndLog();
            EditorUtility.DisplayDialog("PixelPack", ok
                ? "Đã dựng dữ liệu + scene Assets/Scenes/Prototype.unity.\nKiểm tra: tất cả đạt."
                : "Đã dựng xong nhưng kiểm tra có lỗi – xem Console.", "OK");
        }

        public static void BuildBatch()
        {
            Build();
            EditorApplication.Exit(PixelPackSelfTest.RunAndLog() ? 0 : 1);
        }

        public static void Build()
        {
            var catalog = LoadCatalog();
            var props = BuildProps(catalog);
            var fighters = BuildFighters(catalog, props);
            var stages = BuildStages(catalog, props, fighters);
            var fx = PixelPackCombat.BuildFxLibrary();
            var (skin, panel) = BuildUi();
            AssetDatabase.SaveAssets();
            BuildScene(fighters, stages, fx, skin, panel);
            AssetDatabase.SaveAssets();
            Debug.Log($"[PixelPack] Dựng xong: {fighters.Count} nhân vật, {props.Count} vật dụng, {stages.Count} stage → {ScenePath}");
        }

        // ------------------------------------------------------------------ catalog
        static Dictionary<string, object> LoadCatalog()
        {
            var candidates = new[]
            {
                Path.GetFullPath(Path.Combine(Application.dataPath, "../../ArtSource/PixelPack/catalog.json")),
                Path.GetFullPath(Path.Combine(Application.dataPath, "Art/PixelPack/catalog.json")),
            };
            var path = candidates.FirstOrDefault(File.Exists)
                       ?? throw new FileNotFoundException("Không thấy catalog.json (ArtSource/PixelPack hoặc Assets/Art/PixelPack).");
            return (Dictionary<string, object>)MiniJson.Parse(File.ReadAllText(path));
        }

        static readonly Dictionary<string, string> FighterIdByName = new()
        {
            { "Long", "long" }, { "Bảo", "bao" }, { "Linh", "linh" }, { "Mai", "mai" }, { "Khang", "khang" }, { "Bà Tư", "ba_tu" },
        };

        static readonly Dictionary<string, string> PropIdByName = new()
        {
            { "Ghế nhựa đỏ", "ghe_nhua" }, { "Chảo gang", "chao_gang" }, { "Ống tuýp", "ong_tuyp" }, { "Chổi tre", "choi_tre" },
            { "Chai thủy tinh", "chai_thuy_tinh" }, { "Gạch", "gach" }, { "Nồi phở", "noi_pho" }, { "Thùng phi", "thung_phi" },
            { "Bình gas", "binh_gas" }, { "Thùng sơn", "thung_son" }, { "Búa tạ", "bua_ta" }, { "Xe máy cũ", "xe_may" },
        };

        static readonly Dictionary<string, string> StageIdByName = new()
        {
            { "Chợ Đêm", "chodem" }, { "Công Trường", "congtruong" }, { "Bếp Quán Lẩu", "bep" }, { "Sân Thượng Chung Cư", "santhuong" },
        };

        // Bố cục vật dụng (px từ giữa sân; x > 0 đặt đối xứng hai bên, x = 0 ở giữa) – GDD mục 5:
        //   người chơi xuất phát ±110 · vật Lớn ở giữa để tranh giành · vũ khí Vừa ngay sau lưng mỗi người (±175)
        //   · vật ném ở gần mép (±260). Xe máy không đặt sẵn – GDD dùng cho mối nguy "xe chạy ngang" ở Chợ Đêm.
        // Tăng PropLayoutVersion khi đổi bảng này để builder ghi đè bố cục của các StageData đã có.
        const int PropLayoutVersion = 2;
        static readonly Dictionary<string, (string prop, int x)[]> StageProps = new()
        {
            { "chodem", new[] { ("noi_pho", 0), ("ghe_nhua", 175), ("chai_thuy_tinh", 260) } },
            { "congtruong", new[] { ("thung_son", 0), ("bua_ta", 175), ("ong_tuyp", 260) } },
            { "bep", new[] { ("binh_gas", 0), ("chao_gang", 175), ("chai_thuy_tinh", 260) } },
            { "santhuong", new[] { ("thung_phi", 0), ("ong_tuyp", 175), ("choi_tre", 260) } },
        };
        const string FloorProps = "Assets/Art/PixelPackDerived/FloorProps";

        static string FighterIdFromName(string name)
        {
            foreach (var kv in FighterIdByName.OrderByDescending(k => k.Key.Length))
                if (name.StartsWith(kv.Key, StringComparison.Ordinal)) return kv.Value;
            return null;
        }

        // ------------------------------------------------------------------ props
        static Dictionary<string, PropData> BuildProps(Dictionary<string, object> catalog)
        {
            var result = new Dictionary<string, PropData>();
            foreach (Dictionary<string, object> row in catalog.L("Vật dụng"))
            {
                string name = row.S("Vật dụng");
                if (!PropIdByName.TryGetValue(name, out var id)) { Debug.LogWarning($"[PixelPack] Bỏ qua vật dụng lạ: {name}"); continue; }
                var data = LoadOrCreate<PropData>($"{DataRoot}/Props/Prop_{id}.asset", out bool created);
                data.id = id;
                data.displayName = name;
                data.icon = AssetDatabase.LoadAssetAtPath<Sprite>($"{Pack}/Props/{id}.png");
                // sprite nằm sàn đúng tỷ lệ (Tools/extract_floor_props.py); thiếu thì tạm dùng ảnh gốc và cảnh báo
                data.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{FloorProps}/{id}.png");
                data.damagedSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{FloorProps}/{id}_damaged.png");
                data.outline = AssetDatabase.LoadAssetAtPath<Sprite>($"{FloorProps}/{id}_outline.png");
                data.debris = Enumerable.Range(0, 12)
                    .Select(i => AssetDatabase.LoadAssetAtPath<Sprite>($"{FloorProps}/{id}_debris_{i}.png")).Where(x => x).ToArray();
                if (!data.sprite || !data.damagedSprite)
                {
                    Debug.LogWarning($"[PixelPack] Thiếu sprite sàn của {id} – chạy: python3 Tools/extract_floor_props.py");
                    data.sprite = data.sprite ? data.sprite : data.icon;
                    data.damagedSprite = data.damagedSprite ? data.damagedSprite
                        : AssetDatabase.LoadAssetAtPath<Sprite>($"{Pack}/DamagedProps/{id}.png");
                }
                if (created)
                {
                    data.size = row.S("Cỡ") switch { "Nhỏ" => PropSize.Small, "Lớn" => PropSize.Large, "Siêu lớn" => PropSize.ExtraLarge, _ => PropSize.Medium };
                    data.weaponType = row.S("Loại") switch { "Cùn" => WeaponType.Blunt, "Dài" => WeaponType.Long, "Nặng" => WeaponType.Heavy, _ => WeaponType.None };
                    data.hitDamage = LeadingInt(row.S("Sát thương đánh"));
                    data.throwDamage = LeadingInt(row.S("Sát thương ném"));
                    var m = Regex.Match(row.S("Sát thương ném"), @"nổ\s*(\d+)");
                    data.explodeDamage = m.Success ? int.Parse(m.Groups[1].Value) : 0;
                    data.durability = Math.Max(1, LeadingInt(row.S("Độ bền (số lần trúng)")));
                    data.traits = ParseTraits(row.S("Thuộc tính"));
                }
                EditorUtility.SetDirty(data);
                result[id] = data;
            }
            return result;
        }

        static int LeadingInt(string s)
        {
            var m = Regex.Match(s ?? "", @"\d+");
            return m.Success ? int.Parse(m.Value) : 0;
        }

        static PropTraits ParseTraits(string s)
        {
            s = (s ?? "").ToLowerInvariant();
            var t = PropTraits.None;
            if (s.Contains("cháy")) t |= PropTraits.Flammable;
            if (s.Contains("nổ")) t |= PropTraits.Explosive;
            if (s.Contains("kim loại")) t |= PropTraits.Metal;
            if (s.Contains("lỏng")) t |= PropTraits.Liquid;
            if (s.Contains("vỡ")) t |= PropTraits.Fragile;
            return t;
        }

        // ------------------------------------------------------------------ fighters
        static List<FighterData> BuildFighters(Dictionary<string, object> catalog, Dictionary<string, PropData> props)
        {
            var list = new List<FighterData>();
            foreach (Dictionary<string, object> ch in catalog.L("characters"))
            {
                string name = ch.S("Nhân vật");
                string id = FighterIdFromName(name);
                if (id == null || !AssetDatabase.IsValidFolder($"{Pack}/Characters/{id}"))
                {
                    Debug.LogWarning($"[PixelPack] Bỏ qua nhân vật không có thư mục: {name}");
                    continue;
                }
                var data = LoadOrCreate<FighterData>($"{DataRoot}/Fighters/Fighter_{id}.asset", out bool created);
                data.id = id;
                data.displayName = name;
                data.playstyle = ch.S("Lối chơi");
                data.propAffinity = ch.S("Ái lực vật dụng");
                data.color = ColorFor(ch.S("Màu chủ đạo"));
                data.idle = AssetDatabase.LoadAssetAtPath<Sprite>($"{Pack}/Fighters/{id}.png");
                data.moves = BuildMoves(id);
                data.portrait = AssetDatabase.LoadAssetAtPath<Sprite>($"{Pack}/UI/portrait_{id}.png");
                data.superArt = AssetDatabase.LoadAssetAtPath<Sprite>($"{Pack}/UI/super_{id}.png");
                // đòn thường J/K: clip từ BasicActions; giữ số liệu chiến đấu đã chỉnh tay
                var normals = new List<SkillData>();
                foreach (var (clipId, nName) in PixelPackCombat.NormalClips)
                {
                    string dirB = $"{Pack}/BasicActions/{id}";
                    var c = ClipFromAnim($"{dirB}/{clipId}.anim", false);
                    bool placeholder = c == null;
                    if (placeholder) c = PlaceholderClip(clipId, data.moves);   // chưa có asset (SPEC_combo_va_tren_khong.md)
                    if (c == null) continue;
                    var n = new SkillData { name = nName, clip = c, placeholderArt = placeholder };
                    // đoạn thu đòn riêng (light_1_end…): ghép sau clip; chỉ phát khi KHÔNG nối đòn
                    var end = ClipFromAnim($"{dirB}/{clipId}_end.anim", false);
                    if (end != null && !placeholder)
                    {
                        n.chainEndTick = c.lengthTicks;
                        n.clip = Concat(c, end);
                    }
                    var previous = data.normals?.FirstOrDefault(o => o.clip?.name == clipId);
                    CopyCombat(previous, n);
                    if (previous?.placeholderArt == true && !placeholder)
                        PixelPackCombat.ReplacePlaceholderHitboxes(id, n, props);
                    normals.Add(n);
                }
                data.normals = normals.ToArray();
                if (created)
                {
                    data.maxHp = Math.Max(1, LeadingInt(ch.S("HP")));
                    (data.walkSpeed, data.jumpVelocity) = ch.S("Tốc độ") switch
                    {
                        "Chậm" => (24, 108),
                        "Chậm – trung bình" => (28, 114),
                        "Rất nhanh" => (44, 132),
                        "Nhanh" => (38, 126),
                        _ => (32, 120),
                    };
                }

                var skills = new List<SkillData>();
                foreach (Dictionary<string, object> s in ch.L("skills"))
                {
                    if (s.S("Loại") == "Nội tại")
                    {
                        data.passiveName = s.S("Tên");
                        data.passiveEffect = s.S("Hiệu ứng");
                        continue;
                    }
                    string clipId = s.S("clip");
                    if (string.IsNullOrEmpty(clipId)) continue;
                    var clip = ClipFromAnim($"{Pack}/Characters/{id}/{clipId}/{clipId}.anim", false);
                    if (clip == null) { Debug.LogWarning($"[PixelPack] {id}: thiếu clip {clipId}"); continue; }
                    var sd = new SkillData
                    {
                        name = s.S("Tên"),
                        input = ParseInput(s.S("Input"), s.S("Loại")),
                        cost = LeadingInt(s.S("Chi phí")),
                        effect = s.S("Hiệu ứng"),
                        clip = clip,
                        risingArt = ClipFromAnim($"{Pack}/AerialSkills/{id}/{clipId}/rise.anim", false),
                        fallingArt = ClipFromAnim($"{Pack}/AerialSkills/{id}/{clipId}/fall.anim", false),
                    };
                    CopyCombat(data.skills?.FirstOrDefault(o => o.clip?.name == clipId), sd);
                    string item = clipId switch {
                        "day_cap"=>"hook", "sap_gian_giao"=>"scaffold", "lau_dia_nguc"=>"hotpot",
                        "long_khi_chuong"=>"qi", "quat_dat"=>"shockwave", "phi_tieu"=>"dart",
                        "tat_dau_soi"=>"fireball", "binh_gas_mini"=>"gas", "coc_tiep_dia"=>"stake", _=>null
                    };
                    // bản thi triển không vẽ kèm vật thể/dây (vật thể do engine vẽ riêng); chiêu nào có thì dùng
                    sd.groundCastArt = ClipFromAnim($"{Pack}/AerialSkills/{id}/{clipId}/ground.anim", false);
                    if (item != null) {
                        sd.worldObject = AssetDatabase.LoadAssetAtPath<Sprite>($"{Pack}/SkillObjects/{item}.png");
                        sd.dropsFromAbove = item == "scaffold" || item == "hotpot";
                        if (item == "hook") {
                            sd.cableSegment = AssetDatabase.LoadAssetAtPath<Sprite>($"{Pack}/SkillObjects/cable.png");
                            var grips=AssetDatabase.LoadAssetAtPath<TextAsset>($"{Pack}/SkillObjects/cable_grips.json");
                            if(grips)sd.cableGrips=JsonUtility.FromJson<CableGripTable>(grips.text);
                        }
                    }
                    skills.Add(sd);
                }
                data.skills = skills.ToArray();
                // hitbox / hurtbox: chỉ sinh lại khi bảng chiến đấu mới hơn, hoặc có chiêu chưa có dữ liệu
                bool missing = data.skills.Any(x => x.hits.Length == 0 && !(x.effectSpec?.enabled ?? false)
                                                    && x.counterTicks == 0 && x.dashSpeed == 0)
                               || data.normals.Any(x => x.hits.Length == 0 && x.clip?.name != "air_dive_land");   // tiếp đất: không có đòn
                PixelPackCombat.Apply(data, props, force: missing);

                var inters = new List<PropInteraction>();
                foreach (var propId in props.Keys)
                {
                    string dir = $"{Pack}/Interactions/{id}/{propId}";
                    if (!AssetDatabase.IsValidFolder(dir)) continue;
                    var old = data.interactions?.FirstOrDefault(x => x.propId == propId);
                    inters.Add(new PropInteraction
                    {
                        propId = propId,
                        pickup = ClipFromAnim($"{dir}/nhat.anim", false),
                        hold = ClipFromAnim($"{dir}/cam.anim", true),
                        swing = ClipFromAnim($"{dir}/vung.anim", false),
                        toss = ClipFromAnim($"{dir}/nem.anim", false),
                        push = ClipFromAnim($"{dir}/day.anim", false),
                        releaseFrame = old?.releaseFrame ?? 5,
                        swingHitFrame = old?.swingHitFrame ?? 4,
                    });
                }
                data.interactions = inters.ToArray();
                EditorUtility.SetDirty(data);
                list.Add(data);
            }
            return list;
        }

        // ------------------------------------------------------------------ UI (Asset Bible mục 7)
        public const string UiSkinPath = "Assets/Data/UiSkin.asset";
        public const string PanelPath = "Assets/UI/LoanDauPanel.asset";
        public const string ThemePath = "Assets/UI/LoanDauTheme.tss";

        static (UiSkin, PanelSettings) BuildUi()
        {
            var skin = LoadOrCreate<UiSkin>(UiSkinPath, out _);
            string ui = $"{Pack}/UI";
            Sprite S(string n) => AssetDatabase.LoadAssetAtPath<Sprite>($"{ui}/{n}.png");
            skin.font = AssetDatabase.LoadAssetAtPath<Font>($"{ui}/Fonts/Inter-SemiBold.ttf");
            skin.logo = S("logo");
            skin.hpFrame = S("hp_frame");
            skin.hpFill = S("hp_fill");
            skin.energyFrame = S("energy_frame");
            skin.energyFill = S("energy_fill");
            skin.guardFrame = S("guard_frame");
            skin.guardFill = S("guard_fill");
            skin.roundEmpty = S("round_empty");
            skin.roundWin = S("round_win");
            EditorUtility.SetDirty(skin);

            // PanelSettings: màn hình gốc 640×360 (cùng camera pixel-perfect) → phóng ×3 lên 1080p
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            if (!panel)
            {
                EnsureFolder("Assets/UI");
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, PanelPath);
            }
            panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(MatchRunner.RefWidth, MatchRunner.RefHeight);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 1f;
            EditorUtility.SetDirty(panel);
            return (skin, panel);
        }

        /// <summary>Đối tượng "UI" (UIDocument + GameUI) trong scene; tạo nếu chưa có.</summary>
        static GameUI EnsureUi(Scene scene, UiSkin skin, PanelSettings panel)
        {
            var ui = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<GameUI>(true)).FirstOrDefault();
            if (!ui)
            {
                var go = new GameObject("UI");
                SceneManager.MoveGameObjectToScene(go, scene);
                go.AddComponent<UIDocument>();
                ui = go.AddComponent<GameUI>();
            }
            var doc = ui.GetComponent<UIDocument>();
            doc.panelSettings = panel;
            ui.skin = skin;
            EditorUtility.SetDirty(ui);
            EditorUtility.SetDirty(doc);
            return ui;
        }

        /// <summary>Bộ clip động tác chung PixelPack/BasicActions/&lt;nv&gt;/&lt;tên&gt;.anim.</summary>
        static MoveSet BuildMoves(string id)
        {
            string dir = $"{Pack}/BasicActions/{id}";
            SpriteClip C(string n, bool loop) => ClipFromAnim($"{dir}/{n}.anim", loop);
            return new MoveSet
            {
                idle = C("idle_fight", true), walkFwd = C("walk_fwd", true), walkBack = C("walk_back", true), run = C("run", true),
                crouch = C("crouch", false), crouchHold = C("crouch_hold", true),
                jumpStart = C("jump_start", false), jumpAir = C("jump_air", true), jumpLand = C("jump_land", false),
                block = C("block", true), blockHit = C("block_hit", false), rollFwd = C("roll_fwd", false), rollBack = C("roll_back", false),
                pulledFront = C("pulled_front", false), pulledBack = C("pulled_back", false), pulledRecover = C("pulled_recover", false),
                fallingPunch = C("fall_punch", false), fallingKick = C("fall_kick", false),
                hitLight = C("hit_light", false), hitHeavy = C("hit_heavy", false), stun = C("stun", true),
                knockdown = C("knockdown", false), getup = C("getup", false),
                intro = C("intro", false), win = C("win", false), lose = C("lose", false), taunt = C("taunt", false),
            };
        }

        /// <summary>Hình tạm khi chưa vẽ clip đánh trên không: dáng jump_air (đòn) / jump_land (tiếp đất).</summary>
        static SpriteClip PlaceholderClip(string clipId, MoveSet m)
        {
            if (m == null) return null;
            SpriteClip Copy(SpriteClip src, int length, string name)
            {
                if (!MoveSet.Ok(src)) return null;
                return new SpriteClip
                {
                    name = name, frames = (Sprite[])src.frames.Clone(), startTicks = (int[])src.startTicks.Clone(),
                    lengthTicks = Math.Max(length, 1), loop = false,
                };
            }
            return clipId switch
            {
                "air_light" => Copy(m.jumpAir, 22, clipId),
                "air_heavy" => Copy(m.jumpAir, 28, clipId),
                "air_dive" => Copy(m.jumpAir, 12, clipId),
                "air_dive_land" => Copy(m.jumpLand, m.jumpLand?.lengthTicks ?? 15, clipId),
                _ => null,
            };
        }

        /// <summary>Ghép 2 clip nối tiếp (đòn + đoạn thu đòn); tên giữ của clip đầu để khớp số đo hitbox.</summary>
        static SpriteClip Concat(SpriteClip a, SpriteClip b) => new()
        {
            name = a.name,
            frames = a.frames.Concat(b.frames).ToArray(),
            startTicks = a.startTicks.Concat(b.startTicks.Select(t => t + a.lengthTicks)).ToArray(),
            lengthTicks = a.lengthTicks + b.lengthTicks,
            loop = false,
        };

        /// <summary>Giữ dữ liệu chiến đấu (có thể đã chỉnh tay) khi dựng lại danh sách chiêu từ catalog.</summary>
        static void CopyCombat(SkillData from, SkillData to)
        {
            if (from == null) return;
            to.kind = from.kind;
            to.hits = from.hits ?? Array.Empty<HitWindow>();
            to.effectSpec = from.effectSpec ?? new EffectSpec();
            to.dashSpeed = from.dashSpeed;
            to.jumpVelocity = from.jumpVelocity;
            to.invulnTicks = from.invulnTicks;
            to.armor = from.armor;
            to.passThrough = from.passThrough;
            to.counterTicks = from.counterTicks;
            to.counterPercent = from.counterPercent;
            to.screenDarkTicks = from.screenDarkTicks;
            to.chainStartTick = from.chainStartTick;
        }

        static SkillInput ParseInput(string input, string kind)
        {
            if (kind == "Tuyệt chiêu" || input.Contains("K")) return SkillInput.Super;
            if (input.Contains("→")) return SkillInput.Forward;
            if (input.Contains("↓")) return SkillInput.Down;
            if (input.Contains("↑")) return SkillInput.Up;
            return SkillInput.Neutral;
        }

        static Color ColorFor(string name)
        {
            string hex = name switch
            {
                "Đỏ" => "#D7263D",
                "Vàng công trường" => "#F4B400",
                "Tím" => "#7B2CBF",
                "Cam" => "#F77F00",
                "Xanh dương" => "#1E88E5",
                "Xanh lá" => "#43A047",
                _ => "#F8F9FA",
            };
            return ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.white;
        }

        /// <summary>.anim (đường cong m_Sprite) → SpriteClip theo tick, giữ nhịp frame do tác giả đặt.</summary>
        public static SpriteClip ClipFromAnim(string path, bool loop)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (!clip) return null;
            var binding = AnimationUtility.GetObjectReferenceCurveBindings(clip)
                .FirstOrDefault(b => b.propertyName == "m_Sprite");
            if (binding.propertyName != "m_Sprite") return null;
            var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            if (keys == null || keys.Length == 0) return null;
            var frames = new List<Sprite>();
            var starts = new List<int>();
            foreach (var k in keys.OrderBy(k => k.time))
            {
                if (k.value is not Sprite sp) continue;
                int t = Mathf.RoundToInt(k.time * Tick);
                if (starts.Count > 0 && t <= starts[^1]) t = starts[^1] + 1;
                frames.Add(sp);
                starts.Add(t);
            }
            if (frames.Count == 0) return null;
            int avg = frames.Count > 1 ? Mathf.Max(1, Mathf.RoundToInt(starts[^1] / (float)(frames.Count - 1))) : 6;
            int length = Mathf.Max(starts[^1] + avg, Mathf.RoundToInt(clip.length * Tick));
            return new SpriteClip
            {
                name = Path.GetFileNameWithoutExtension(path),
                frames = frames.ToArray(),
                startTicks = starts.ToArray(),
                lengthTicks = Mathf.Max(1, length),
                loop = loop,
            };
        }

        // ------------------------------------------------------------------ stages
        static List<StageData> BuildStages(Dictionary<string, object> catalog, Dictionary<string, PropData> props, List<FighterData> fighters)
        {
            var list = new List<StageData>();
            foreach (Dictionary<string, object> row in catalog.L("Stage"))
            {
                string name = row.S("Stage");
                if (!StageIdByName.TryGetValue(name, out var id)) { Debug.LogWarning($"[PixelPack] Bỏ qua stage lạ: {name}"); continue; }
                var bg = AssetDatabase.LoadAssetAtPath<Sprite>($"{Pack}/Stages/{id}.png");
                if (!bg) { Debug.LogWarning($"[PixelPack] Thiếu ảnh stage {id}"); continue; }
                var data = LoadOrCreate<StageData>($"{DataRoot}/Stages/Stage_{id}.asset", out bool created);
                data.id = id;
                data.displayName = name;
                data.background = bg;
                data.layout = row.S("Bố cục");
                data.hazard = row.S("Mối nguy môi trường");
                string home = row.Keys.FirstOrDefault(k => k.StartsWith("Nhân vật"));
                data.homeFighterId = home != null ? FighterIdFromName(row.S(home)) : null;
                if ((created || data.propLayoutVersion < PropLayoutVersion) && StageProps.TryGetValue(id, out var spawns))
                {
                    data.propLayoutVersion = PropLayoutVersion;
                    var l = new List<StageData.PropSpawn>();
                    foreach (var (pid, x) in spawns)
                    {
                        if (!props.TryGetValue(pid, out var pd)) continue;
                        if (x == 0) l.Add(new StageData.PropSpawn { prop = pd, xPx = 0 });
                        else
                        {
                            l.Add(new StageData.PropSpawn { prop = pd, xPx = -x });
                            l.Add(new StageData.PropSpawn { prop = pd, xPx = x });
                        }
                    }
                    data.props = l.ToArray();
                }
                EditorUtility.SetDirty(data);
                list.Add(data);
            }
            return list;
        }

        // ------------------------------------------------------------------ scene
        static void BuildScene(List<FighterData> fighters, List<StageData> stages, FxLibrary fx, UiSkin skin, PanelSettings panel)
        {
            EnsureFolder("Assets/Scenes");
            var loaded = SceneManager.GetSceneByPath(ScenePath);
            if (loaded.IsValid() && loaded.isLoaded)
            {
                // scene đang mở: cập nhật tại chỗ, không đóng scene của người dùng
                var runner = loaded.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MatchRunner>(true)).FirstOrDefault();
                if (runner)
                {
                    runner.fighters = fighters.ToArray();
                    runner.stages = stages.ToArray();
                    runner.fx = fx;
                    runner.ui = EnsureUi(loaded, skin, panel);
                    EditorUtility.SetDirty(runner);
                    EditorSceneManager.MarkSceneDirty(loaded);
                    EditorSceneManager.SaveScene(loaded);
                    return;
                }
            }

            // Unity không cho tạo scene additive khi đang mở scene Untitled chưa lưu → mở scene mới thay thế,
            // hỏi lưu scene hiện tại nếu có thay đổi (batch mode: không hỏi).
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogWarning("[PixelPack] Bỏ qua dựng scene vì bạn chưa lưu scene đang mở. Dữ liệu đã dựng xong.");
                return;
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = MatchRunner.RefHeight / 2f / MatchRunner.Ppu;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.07f, 0.12f);
            camGo.transform.position = new Vector3(0, 1.875f, -10);

            var runnerGo = new GameObject("Match");
            var r = runnerGo.AddComponent<MatchRunner>();
            r.fighters = fighters.ToArray();
            r.stages = stages.ToArray();
            r.fx = fx;
            r.ui = EnsureUi(scene, skin, panel);
            r.p1Fighter = Math.Max(0, fighters.FindIndex(f => f.id == "long"));
            r.p2Fighter = Math.Max(0, fighters.FindIndex(f => f.id == "bao"));
            r.stageIndex = Math.Max(0, stages.FindIndex(s => s.id == "chodem"));

            EditorSceneManager.SaveScene(scene, ScenePath);       // scene Prototype giữ nguyên mở để bấm Play ngay

            var list = EditorBuildSettings.scenes.ToList();
            if (list.All(s => s.path != ScenePath))
            {
                list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = list.ToArray();
            }
        }

        // ------------------------------------------------------------------ helpers
        static T LoadOrCreate<T>(string path, out bool created) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            created = !a;
            if (a) return a;
            EnsureFolder(Path.GetDirectoryName(path)!.Replace('\\', '/'));
            a = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(a, path);
            return a;
        }

        public static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            var parent = Path.GetDirectoryName(folder)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
