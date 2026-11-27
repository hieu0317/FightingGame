using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LoanDau.Data;
using UnityEditor;
using UnityEngine;

namespace LoanDau.EditorTools
{
    /// <summary>
    /// Sinh dữ liệu chiến đấu cho 30 chiêu:
    ///   * thời điểm + hộp của từng nhịp đánh: đo từ hình vẽ (Tools/analyze_skill_frames.py → skill_frames.json)
    ///   * loại chiêu, sát thương, thuộc tính: bảng dưới đây, dựa trên mô tả chiêu trong GDD mục 3
    /// Chỉ ghi đè khi FighterData.combatVersion &lt; CombatVersion → số liệu chỉnh tay trong Inspector được giữ.
    /// Đổi bảng → tăng CombatVersion.
    /// </summary>
    public static class PixelPackCombat
    {
        public const int CombatVersion = 5;  // v2: tầm vươn ≥ 60% đỉnh · v3: Lửa/Điện · v4: đòn J/K · v5: nối combo, đòn trên không, đá bổ
        public const string FramesJson = "Assets/Art/PixelPackDerived/SkillFrames/skill_frames.json";
        const string Fx = "Assets/Art/PixelPack/InteractionFX";
        const int Sub = 16;
        const int MinReach = 10;            // px vươn ra trước thân tối thiểu để tính là ra đòn
        const int ActiveReachPercent = 60;  // frame đang ra đòn = tầm vươn ≥ 60% tầm vươn đỉnh

        // ---- JSON từ analyze_skill_frames.py (JsonUtility gán giá trị lúc chạy)
#pragma warning disable 0649
        [Serializable] class Root { public FighterFrames[] fighters; }
        [Serializable] class FighterFrames { public string id; public Measure idle; public ClipFrames[] clips; public Measure basicIdle; public ClipFrames[] basicClips; }
        [Serializable] class ClipFrames { public string clip; public Measure[] frames; }
        [Serializable] class Measure { public int front, back, top, bottom; public int[] ext; }
#pragma warning restore 0649

        /// <summary>Thông số một chiêu theo GDD.</summary>
        class Spec
        {
            public SkillKind kind = SkillKind.Strike;
            public int total;                         // tổng sát thương chia đều cho các nhịp
            public HitFlags flags, lastFlags;
            public int hitCount;                      // > 0: chia đều thành N nhịp (thay số nhịp đo từ hình)
            public bool single;                       // chỉ dùng nhịp lớn nhất (vật, chọc kìm...)
            public bool noStrike;                     // không có hitbox cận chiến (bắn đạn, lướt, thế chờ)
            public int dashPx, jump, invuln, counter, dark, pushPx = 10, lastPushPx = 20;
            public bool armor, pass, invulnWhole;
            public (int x0, int x1, int y0, int y1)? box;
            public int maxY = -1;                     // giới hạn đỉnh hộp (đòn quét thấp)
            public EffectSpec fx;
            public string fxFrames;                   // InteractionFX/<tên> hoặc "prop:<id>"
            public bool fxAtFirstHit = true;          // sinh đạn ở nhịp đầu (false = nhịp cuối)
        }

        static EffectSpec Proj(int speedPx, int halfW, int halfH, int offY, int dmg, int life, HitFlags f = HitFlags.None) => new()
        {
            enabled = true, speed = speedPx * Sub, halfW = halfW, halfH = halfH, offsetX = 40, offsetY = offY,
            damage = dmg, lifetime = life, flags = f,
        };

        static readonly Dictionary<string, Spec> Table = new()
        {
            // ---- Long "Quyền Sắt" – toàn diện
            ["long_khi_chuong"] = new Spec { kind = SkillKind.Projectile, noStrike = true, fx = Proj(6, 12, 9, 62, 80, 110), fxFrames = "dien" },
            ["cuoc_bay"] = new Spec { kind = SkillKind.Dash, total = 90, dashPx = 3, lastFlags = HitFlags.Knockdown },
            ["don_chan_kep"] = new Spec { total = 100, jump = 70, flags = HitFlags.Unblockable, lastFlags = HitFlags.Knockdown },
            ["thang_long_quyen"] = new Spec { total = 90, invuln = 10, jump = 64, lastFlags = HitFlags.Launch },
            ["long_ho_hoi"] = new Spec { total = 300, hitCount = 8, dashPx = 1, lastFlags = HitFlags.Launch, dark = 40 },
            // ---- Bảo "Thép" – tank / vật
            ["quat_dat"] = new Spec { kind = SkillKind.Projectile, noStrike = true, fx = Proj(4, 16, 8, 8, 90, 90, HitFlags.Low), fxFrames = "no" },
            ["huc_vai"] = new Spec { kind = SkillKind.Dash, total = 100, dashPx = 4, armor = true, lastPushPx = 50 },
            ["be_khoa"] = new Spec { kind = SkillKind.Grab, total = 180, single = true, flags = HitFlags.Unblockable | HitFlags.GroundOnly | HitFlags.Knockdown },
            ["quang_bung"] = new Spec { kind = SkillKind.Grab, total = 160, single = true, jump = 90, flags = HitFlags.Unblockable | HitFlags.AirOnly | HitFlags.Knockdown },
            ["sap_gian_giao"] = new Spec
            {
                kind = SkillKind.Field, noStrike = true, dark = 40, fxAtFirstHit = false, fxFrames = "no",
                fx = new EffectSpec { enabled = true, atOpponent = true, halfW = 150, halfH = 70, offsetY = 70, damage = 300, lifetime = 40, flags = HitFlags.Knockdown, drawScale = 2f },
            },
            // ---- Linh "Bóng Mèo" – tốc độ
            ["phi_tieu"] = new Spec { kind = SkillKind.Projectile, noStrike = true, fxFrames = "dien", fx = Sprd(Proj(7, 6, 4, 60, 25, 70), 3, 1) },
            ["luot_bong"] = new Spec { kind = SkillKind.Dash, noStrike = true, dashPx = 7, pass = true, invulnWhole = true },
            ["moc_tui"] = new Spec { total = 30, single = true, flags = HitFlags.Steal },
            ["dap_trang"] = new Spec { total = 80, jump = 80, lastFlags = HitFlags.Launch },
            ["van_anh"] = new Spec { total = 300, hitCount = 6, flags = HitFlags.Homing, lastFlags = HitFlags.Knockdown, invulnWhole = true, dark = 60 },
            // ---- Mai "Lửa Bếp" – khống chế tầm xa
            ["tat_dau_soi"] = new Spec
            {
                kind = SkillKind.Projectile, noStrike = true, fxFrames = "lua",
                fx = new EffectSpec { enabled = true, speed = 4 * Sub, vy = 5 * Sub, gravity = 4, halfW = 8, halfH = 8, offsetX = 36, offsetY = 60, damage = 70, lifetime = 260, landFieldTicks = 180, flags = HitFlags.Fire },
            },
            // ngọn lửa trong hình chỉ vươn ~7 px → đặt vùng phun lửa phía trước, đốt 3 nhịp liên tiếp
            ["xao_lua"] = new Spec { total = 90, hitCount = 3, flags = HitFlags.Fire, box = (20, 72, 16, 86), pushPx = 4, lastPushPx = 14 },
            ["binh_gas_mini"] = new Spec
            {
                kind = SkillKind.Trap, noStrike = true, fxFrames = "prop:binh_gas",
                fx = new EffectSpec { enabled = true, offsetX = 44, offsetY = 12, halfW = 9, halfH = 12, damage = 120, lifetime = 600, flags = HitFlags.Knockdown | HitFlags.Fire, drawScale = 0.5f },
            },
            ["lat_chao"] = new Spec { total = 60, flags = HitFlags.Reflect, lastFlags = HitFlags.Launch },
            ["lau_dia_nguc"] = new Spec
            {
                kind = SkillKind.Field, noStrike = true, dark = 40, fxFrames = "lua",
                fx = new EffectSpec { enabled = true, offsetX = 0, offsetY = 16, halfW = 400, halfH = 16, damage = 30, hitInterval = 30, lifetime = 300, flags = HitFlags.SafeNearOwner | HitFlags.Fire },
            },
            // ---- Khang "Thợ Điện" – bẫy / khống chế
            ["kim_dien"] = new Spec { total = 70, single = true, flags = HitFlags.Stun | HitFlags.Electric },
            ["day_cap"] = new Spec { kind = SkillKind.Projectile, noStrike = true, fx = Proj(8, 10, 5, 60, 50, 45, HitFlags.Pull | HitFlags.Electric), fxFrames = "dien" },
            ["coc_tiep_dia"] = new Spec
            {
                kind = SkillKind.Trap, noStrike = true, fxFrames = "dien",
                fx = new EffectSpec { enabled = true, offsetX = 36, offsetY = 16, halfW = 6, halfH = 16, damage = 0, emitInterval = 120, lifetime = 480, flags = HitFlags.Electric },
            },
            ["chap_mach"] = new Spec { total = 60, invuln = 8, flags = HitFlags.Electric, box = (-42, 50, 0, 100), pushPx = 30, lastPushPx = 40 },
            ["mat_dien_toan_pho"] = new Spec { total = 280, hitCount = 4, flags = HitFlags.Homing | HitFlags.Stun | HitFlags.Electric, dark = 240 },
            // ---- Bà Tư "Gậy Trúc" – phản đòn
            ["quat_phong"] = new Spec { total = 30, flags = HitFlags.Reflect, pushPx = 30, lastPushPx = 40 },
            ["buoc_hac"] = new Spec { total = 80, jump = 100, dashPx = 3, pass = true, flags = HitFlags.Overhead },
            ["gay_quet"] = new Spec { total = 80, flags = HitFlags.Low, lastFlags = HitFlags.Knockdown, maxY = 44 },
            ["the_cho"] = new Spec { kind = SkillKind.Counter, noStrike = true, counter = 60 },
            ["thap_bat_con"] = new Spec { total = 300, hitCount = 18, dark = 40 },
        };

        /// <summary>Đòn thường (BasicActions): J combo 3 nhịp, K đánh mạnh phá thế đỡ + đánh bay (GDD mục 2).</summary>
        public static readonly (string clip, string name)[] NormalClips =
        {
            ("light_1", "Đánh nhẹ 1"), ("light_2", "Đánh nhẹ 2"), ("light_3", "Đánh nhẹ 3"), ("heavy", "Đánh mạnh"),
            ("air_light", "Đánh nhẹ trên không"), ("air_heavy", "Đánh mạnh trên không"), ("air_dive", "Đá bổ"),
            ("air_dive_land", "Tiếp đất sau đá bổ"),
        };

        /// <summary>Hộp + thời điểm ra đòn dùng khi clip trên không còn là hình tạm (chưa vẽ – không đo được từ hình).</summary>
        static readonly Dictionary<string, (int start, int end, int x0, int x1, int y0, int y1)> PlaceholderHits = new()
        {
            ["air_light"] = (5, 12, 8, 44, 24, 80),
            ["air_heavy"] = (9, 18, 10, 56, 16, 72),
            ["air_dive"] = (2, 600, 0, 40, -12, 40),
        };

        static readonly Dictionary<string, Spec> NormalTable = new()
        {
            ["light_1"] = new Spec { total = 25, pushPx = 6, lastPushPx = 6 },
            ["light_2"] = new Spec { total = 25, pushPx = 6, lastPushPx = 6 },
            ["light_3"] = new Spec { total = 40, pushPx = 14, lastPushPx = 14 },
            ["heavy"] = new Spec { total = 80, lastPushPx = 20, lastFlags = HitFlags.Launch | HitFlags.GuardCrush },
            // trên không: phải đỡ ĐỨNG (đòn từ trên xuống)
            ["air_light"] = new Spec { total = 30, flags = HitFlags.Overhead, lastPushPx = 8 },
            ["air_heavy"] = new Spec { total = 60, flags = HitFlags.Overhead, lastPushPx = 16 },
            ["air_dive"] = new Spec { total = 50, flags = HitFlags.Overhead, lastPushPx = 10 },
            ["air_dive_land"] = new Spec { noStrike = true },
        };

        static EffectSpec Sprd(EffectSpec e, int count, int spreadPx)
        {
            e.count = count;
            e.spreadVy = spreadPx * Sub;
            return e;
        }

        static Root cached;

        static Root LoadFrames()
        {
            var full = Path.GetFullPath(FramesJson);
            if (!File.Exists(full)) return null;
            return cached = JsonUtility.FromJson<Root>(File.ReadAllText(full));
        }

        /// <summary>Gán hurtbox + dữ liệu chiến đấu cho 1 nhân vật. Trả về false nếu giữ nguyên (đã chỉnh tay).</summary>
        public static bool Apply(FighterData data, Dictionary<string, PropData> props, bool force = false)
        {
            if (!force && data.combatVersion >= CombatVersion) return false;
            var root = LoadFrames();
            var ff = root?.fighters?.FirstOrDefault(f => f.id == data.id);
            if (ff == null)
            {
                Debug.LogWarning($"[PixelPack] Chưa có số đo frame của {data.id} – chạy: python3 Tools/analyze_skill_frames.py");
                return false;
            }
            int idleFront = ff.idle.front;
            data.hurtHalfWidth = Mathf.Max(12, Mathf.RoundToInt((ff.idle.front - ff.idle.back) * 0.3f));
            data.hurtHeight = Mathf.Max(40, ff.idle.top - 4);

            foreach (var s in data.skills)
            {
                string clipId = s.clip?.name;
                if (clipId == null || !Table.TryGetValue(clipId, out var spec)) { Debug.LogWarning($"[PixelPack] Chiêu {clipId} không có trong bảng chiến đấu"); continue; }
                var cf = ff.clips?.FirstOrDefault(c => c.clip == clipId);
                var runs = cf != null ? FindRuns(s.clip, cf, idleFront) : new List<Run>();
                Build(s, spec, runs, idleFront, props);
            }
            // ái lực vật dụng (GDD mục 3): Mai – mọi vật ném ra bốc cháy; Khang – nhiễm điện vật kim loại trong 2 m
            data.throwsIgnite = data.id == "mai";
            data.electrifyRadiusPx = data.id == "khang" ? 120 : 0;
            // đòn thường: hitbox đo từ dải sprite BasicActions, so với tư thế idle_fight
            if (ff.basicIdle != null && data.normals != null)
                foreach (var n in data.normals)
                {
                    string clipId = n?.clip?.name;
                    if (clipId == null || !NormalTable.TryGetValue(clipId, out var spec)) continue;
                    var cf = ff.basicClips?.FirstOrDefault(c => c.clip == clipId);
                    var runs = cf != null ? FindRuns(n.clip, cf, ff.basicIdle.front) : new List<Run>();
                    Build(n, spec, runs, ff.basicIdle.front, props);
                    if (n.placeholderArt && PlaceholderHits.TryGetValue(clipId, out var ph) && n.hits.Length > 0)
                    {
                        // hình tạm (dáng jump_air) không vươn tay/chân → đặt hộp theo bảng
                        n.hits = new[] { n.hits[n.hits.Length - 1] };
                        var w0 = n.hits[0];
                        w0.damage = spec.total;
                        (w0.startTick, w0.endTick, w0.x0, w0.x1, w0.y0, w0.y1) = ph;
                    }
                    foreach (var w in n.hits)
                        w.hitstun = clipId is "heavy" or "air_heavy" ? 26 : clipId is "light_3" or "air_dive" ? 20 : 14;
                    if (clipId == "air_dive") foreach (var w in n.hits) w.y0 = Math.Min(w.y0, -12);   // lao xuống: hộp thò dưới chân
                }
            // nối combo: có tư thế nối (asset *_end) → đòn sau bắt đầu từ frame 0; chưa có → bỏ đoạn lấy đà từ tư thế đứng
            for (int k = 1; k <= FighterData.Heavy && data.normals != null && k < data.normals.Length; k++)
            {
                var n = data.normals[k];
                var prev = data.normals[k - 1];
                bool designed = prev != null && prev.chainEndTick > 0 && prev.chainEndTick < prev.clip.lengthTicks;
                n.chainStartTick = designed || n.hits.Length == 0 ? 0 : Math.Max(0, n.hits[0].startTick - 4);
            }
            data.combatVersion = CombatVersion;
            return true;
        }

        internal static void ReplacePlaceholderHitboxes(string fighterId, SkillData attack, Dictionary<string, PropData> props)
        {
            var measurements = LoadFrames()?.fighters?.FirstOrDefault(f => f.id == fighterId);
            var measured = measurements?.basicClips?.FirstOrDefault(c => c.clip == attack.clip.name);
            if (measured == null || !NormalTable.TryGetValue(attack.clip.name, out var spec)) return;
            int front = measurements.basicIdle.front;
            Build(attack, spec, FindRuns(attack.clip, measured, front), front, props);
            foreach (var hit in attack.hits)
            {
                hit.hitstun = attack.clip.name == "air_heavy" ? 26 : attack.clip.name == "air_dive" ? 20 : 14;
                if (attack.clip.name == "air_dive") hit.y0 = Math.Min(hit.y0, -12);
            }
        }

        class Run { public int start, end, x0, x1, y0, y1, reach; }

        /// <summary>
        /// Nhịp đánh = các frame liên tiếp (theo THỨ TỰ PHÁT của clip, không theo tên file – PixelPack xếp 4 keyframe gốc
        /// ở file 0–3, frame nội suy ở 4–15) có tầm vươn ≥ 60% tầm vươn đỉnh của chiêu. Frame thu chân/tay về bị loại.
        /// </summary>
        static List<Run> FindRuns(SpriteClip clip, ClipFrames cf, int idleFront)
        {
            var runs = new List<Run>();
            int peak = 0;
            for (int k = 0; k < clip.frames.Length; k++)
            {
                var m = MeasureFor(clip.frames[k], cf);
                if (m?.ext != null && m.ext.Length == 4) peak = Math.Max(peak, m.ext[2] - idleFront);
            }
            int threshold = Math.Max(MinReach, peak * ActiveReachPercent / 100);
            Run cur = null;
            for (int k = 0; k < clip.frames.Length; k++)
            {
                var m = MeasureFor(clip.frames[k], cf);
                bool ext = m?.ext != null && m.ext.Length == 4 && m.ext[2] - idleFront >= threshold;
                if (!ext) { cur = null; continue; }
                int start = clip.startTicks[k];
                int end = (k + 1 < clip.startTicks.Length ? clip.startTicks[k + 1] : clip.lengthTicks) - 1;
                if (cur == null)
                {
                    cur = new Run { start = start, end = end, x0 = m.ext[0], y0 = m.ext[1], x1 = m.ext[2], y1 = m.ext[3] };
                    runs.Add(cur);
                }
                else
                {
                    cur.end = end;
                    cur.x0 = Math.Min(cur.x0, m.ext[0]);
                    cur.y0 = Math.Min(cur.y0, m.ext[1]);
                    cur.x1 = Math.Max(cur.x1, m.ext[2]);
                    cur.y1 = Math.Max(cur.y1, m.ext[3]);
                }
                cur.reach = Math.Max(cur.reach, m.ext[2] - idleFront);
            }
            return runs;
        }

        static Measure MeasureFor(Sprite sp, ClipFrames cf)
        {
            if (!sp || cf.frames == null) return null;
            // sprite đơn lẻ của PixelPack tên theo số frame: "0", "1", ...
            string name = sp.name;
            int us = name.LastIndexOf('_');
            if (!int.TryParse(name, out int n) && !(us >= 0 && int.TryParse(name.Substring(us + 1), out n))) return null;
            return n >= 0 && n < cf.frames.Length ? cf.frames[n] : null;
        }

        static void Build(SkillData s, Spec spec, List<Run> runs, int idleFront, Dictionary<string, PropData> props)
        {
            s.kind = spec.kind;
            s.dashSpeed = spec.dashPx * Sub;
            s.jumpVelocity = spec.jump;
            s.armor = spec.armor;
            s.passThrough = spec.pass;
            s.counterTicks = spec.counter;
            s.screenDarkTicks = spec.dark;
            s.invulnTicks = spec.invulnWhole ? s.clip.lengthTicks : spec.invuln;

            // ---- nhịp đánh
            var wins = new List<HitWindow>();
            if (!spec.noStrike)
            {
                var src = runs.Count > 0 ? runs : new List<Run>
                {
                    new() { start = s.clip.lengthTicks / 3, end = s.clip.lengthTicks / 2, x0 = idleFront, x1 = idleFront + 24, y0 = 30, y1 = 80 },
                };
                if (spec.single) src = new List<Run> { src.OrderByDescending(r => r.reach).ThenBy(r => r.start).First() };
                if (spec.hitCount > 0)
                {
                    var u = new Run { start = src.Min(r => r.start), end = src.Max(r => r.end), x0 = src.Min(r => r.x0), x1 = src.Max(r => r.x1), y0 = src.Min(r => r.y0), y1 = src.Max(r => r.y1) };
                    int span = Math.Max(spec.hitCount, u.end - u.start + 1);
                    src = new List<Run>();
                    for (int i = 0; i < spec.hitCount; i++)
                    {
                        int a = u.start + span * i / spec.hitCount;
                        int b = u.start + span * (i + 1) / spec.hitCount - 1;
                        src.Add(new Run { start = a, end = Math.Max(a, b), x0 = u.x0, x1 = u.x1, y0 = u.y0, y1 = u.y1 });
                    }
                }
                int n = src.Count;
                for (int i = 0; i < n; i++)
                {
                    var r = src[i];
                    bool last = i == n - 1;
                    var w = new HitWindow
                    {
                        startTick = r.start, endTick = r.end,
                        // hộp từ sát thân (để đòn gần vẫn trúng) tới mút phần vươn ra
                        x0 = Math.Min(r.x0, idleFront - 8), x1 = Math.Max(r.x1, idleFront + 12),
                        y0 = Math.Max(0, r.y0), y1 = Math.Max(r.y1, r.y0 + 16),
                        damage = spec.total / n + (last ? spec.total % n : 0),
                        hitstun = last ? 24 : 16,
                        pushPx = last ? spec.lastPushPx : spec.pushPx,
                        flags = spec.flags | (last ? spec.lastFlags : HitFlags.None),
                    };
                    if (spec.box.HasValue) (w.x0, w.x1, w.y0, w.y1) = spec.box.Value;
                    if (spec.maxY > 0) w.y1 = Math.Min(w.y1, spec.maxY);
                    if (spec.kind == SkillKind.Grab) w.x1 = Math.Max(w.x1, idleFront + 16);
                    wins.Add(w);
                }
            }
            s.hits = wins.ToArray();

            // ---- đạn / bẫy / vùng
            if (spec.fx != null)
            {
                var e = Clone(spec.fx);
                var anchor = runs.Count == 0 ? null : spec.fxAtFirstHit ? runs[0] : runs[runs.Count - 1];
                e.spawnTick = anchor?.start ?? s.clip.lengthTicks / 3;
                e.frames = LoadFxFrames(spec.fxFrames, props);
                s.effectSpec = e;
            }
            else s.effectSpec = new EffectSpec();
        }

        static EffectSpec Clone(EffectSpec e) => JsonUtility.FromJson<EffectSpec>(JsonUtility.ToJson(e));

        static Sprite[] LoadFxFrames(string name, Dictionary<string, PropData> props)
        {
            if (string.IsNullOrEmpty(name)) return Array.Empty<Sprite>();
            if (name.StartsWith("prop:"))
                return props.TryGetValue(name.Substring(5), out var p) && p.sprite ? new[] { p.sprite } : Array.Empty<Sprite>();
            return Enumerable.Range(0, 8)
                .Select(i => AssetDatabase.LoadAssetAtPath<Sprite>($"{Fx}/{name}/{i}.png"))
                .Where(x => x).ToArray();
        }

        public static IEnumerable<string> KnownClips => Table.Keys;

        public const string FxLibraryPath = "Assets/Data/FxLibrary.asset";

        /// <summary>Thư viện hình hiệu ứng môi trường (lửa, điện, nổ, vũng) từ PixelPack/InteractionFX.</summary>
        public static FxLibrary BuildFxLibrary()
        {
            var lib = AssetDatabase.LoadAssetAtPath<FxLibrary>(FxLibraryPath);
            if (!lib)
            {
                lib = ScriptableObject.CreateInstance<FxLibrary>();
                AssetDatabase.CreateAsset(lib, FxLibraryPath);
            }
            lib.fire = LoadFxFrames("lua", null);
            lib.electric = LoadFxFrames("dien", null);
            lib.explosion = LoadFxFrames("no", null);
            lib.puddle = LoadFxFrames("vung_nuoc", null);
            Sprite[] Seq(string folder) => Enumerable.Range(0, 12)
                .Select(i => AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/PixelPack/CombatFX/{folder}/{i}.png")).Where(x => x).ToArray();
            lib.sparkLight = Seq("spark_light");
            lib.sparkHeavy = Seq("spark_heavy");
            lib.landingDust = Seq("landing_dust");
            lib.smoke = Seq("smoke");
            lib.shockRing = Seq("shock_ring");
            lib.bop = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/PixelPack/CombatFX/bop.png");
            lib.rac = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/PixelPack/CombatFX/rac.png");
            EditorUtility.SetDirty(lib);
            return lib;
        }
    }
}
