using System;
using System.IO;
using System.Linq;
using System.Text;
using LoanDau.Core;
using LoanDau.Data;
using LoanDau.Game;
using LoanDau.UI;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEngine;

namespace LoanDau.EditorTools
{
    /// <summary>
    /// Kiểm tra bộ PixelPack sau khi dựng: import pixel-perfect, dữ liệu đủ, scene có sẵn,
    /// và chạy thử mô phỏng không giao diện (đi, nhảy, chiêu, nhặt – ném trúng, tất định).
    /// Menu: Loan Dau → PixelPack → Kiểm tra. Batch: -executeMethod LoanDau.EditorTools.PixelPackSelfTest.RunBatch
    /// </summary>
    public static class PixelPackSelfTest
    {
        [MenuItem("Loan Dau/PixelPack/Kiểm tra")]
        public static void RunMenu()
        {
            bool ok = RunAndLog();
            EditorUtility.DisplayDialog("PixelPack", ok ? "Tất cả đạt." : "Có lỗi – xem Console.", "OK");
        }

        public static void RunBatch() => EditorApplication.Exit(RunAndLog() ? 0 : 1);

        public static bool RunAndLog()
        {
            var (ok, report) = Run();
            if (ok) Debug.Log(report);
            else Debug.LogError(report);
            return ok;
        }

        public static (bool ok, string report) Run()
        {
            var sb = new StringBuilder("[PixelPackSelfTest]\n");
            int fails = 0;
            void Check(bool c, string msg)
            {
                sb.AppendLine((c ? "  PASS " : "  FAIL ") + msg);
                if (!c) fails++;
            }

            // ---- import
            var pngs = Directory.GetFiles(PixelPackBuilder.Pack, "*.png", SearchOption.AllDirectories);
            var bad = pngs.Select(p => (p, ti: AssetImporter.GetAtPath(p.Replace('\\', '/')) as TextureImporter))
                .Where(x => x.ti == null || !Mathf.Approximately(x.ti.spritePixelsPerUnit, PixelPackTextureImporter.Ppu)
                            || x.ti.filterMode != (IsPaintedUi(x.p) ? FilterMode.Bilinear : FilterMode.Point)
                            || x.ti.textureCompression != TextureImporterCompression.Uncompressed)
                .Select(x => x.p).ToList();
            Check(pngs.Length > 0 && bad.Count == 0,
                  $"{pngs.Length} ảnh PixelPack: PPU 64, Point (ảnh vẽ UI: Bilinear), không nén{(bad.Count > 0 ? " – sai: " + string.Join(", ", bad.Take(3)) : "")}");

            // ---- dữ liệu
            var fighters = Load<FighterData>("Fighters");
            var props = Load<PropData>("Props");
            var stages = Load<StageData>("Stages");
            Check(fighters.Length == 6, $"{fighters.Length}/6 nhân vật");
            Check(props.Length == 12, $"{props.Length}/12 vật dụng");
            Check(stages.Length == 4, $"{stages.Length}/4 stage");
            foreach (var f in fighters)
            {
                bool skillsOk = f.skills.Length == 5 && f.skills.All(s => s.clip.IsValid)
                                && Enum.GetValues(typeof(SkillInput)).Cast<SkillInput>().All(i => f.FindSkill(i) >= 0);
                int pick = f.interactions.Count(i => i.CanPickUp && i.toss != null && i.toss.IsValid && i.hold != null && i.hold.loop);
                int push = f.interactions.Count(i => i.push != null && i.push.IsValid);
                // GDD: chỉ Bảo nhặt được vật Siêu lớn (xe máy) → Bảo có 12 vật nhặt-ném, người khác 11
                int expectPick = f.id == "bao" ? 12 : 11;
                Check(f.idle && skillsOk && pick == expectPick && push == 1,
                      $"{f.id}: idle, 5 chiêu đủ 5 hướng input ({string.Join("/", f.skills.Select(s => s.clip.frames.Length))} frame), " +
                      $"{pick} vật nhặt-ném + {push} vật đẩy, HP {f.maxHp}");
            }
            // tỷ lệ: vật dụng nằm sàn phải nhỏ hơn người (ghế nhựa ~ ngang gối, không cao bằng người như ảnh icon)
            var refFighter = fighters.FirstOrDefault(f => f.id == "long");
            float personH = refFighter && refFighter.idle ? refFighter.idle.bounds.size.y : 0;
            foreach (var p in props)
            {
                float h = p.sprite ? p.sprite.bounds.size.y : 0;
                bool derived = p.sprite && AssetDatabase.GetAssetPath(p.sprite).StartsWith(PixelPackTextureImporter.DerivedRoot);
                Check(derived && h > 0 && h <= personH * 0.7f && Mathf.Abs(p.sprite.pivot.y) < 0.5f,
                      $"{p.id}: sprite sàn {p.sprite?.rect.width}×{p.sprite?.rect.height}px = {h / Mathf.Max(personH, 0.01f):P0} chiều cao người, pivot đáy");
            }
            foreach (var p in props)
                Check(p.sprite && p.damagedSprite && p.durability >= 1,
                      $"{p.id}: {p.size}/{p.weaponType}/{p.traits} đánh {p.hitDamage} ném {p.throwDamage}{(p.explodeDamage > 0 ? "+nổ " + p.explodeDamage : "")} bền {p.durability}");
            foreach (var s in stages)
            {
                var b = s.background ? s.background.bounds.size : Vector3.zero;
                Check(s.background && Mathf.Approximately(b.x, 12f) && s.props.Length > 0 && s.props.Sum(x => x.xPx) == 0,
                      $"{s.id}: nền {b.x:F2}×{b.y:F2} unit, {s.props.Length} vật đặt đối xứng, sân nhà {s.homeFighterId}");
                // vị trí: không vật nào đè lên chỗ đứng xuất phát; vật Lớn/Siêu lớn chỉ ở giữa sân; không ra khỏi vùng đi được
                int half = Mathf.RoundToInt(b.x * PixelPackTextureImporter.Ppu / 2f) - s.sideMarginPx;
                bool clearOfPlayers = s.props.All(x => Mathf.Abs(Mathf.Abs(x.xPx) - s.playerStartPx) >= 40);
                bool bigCentered = s.props.Where(x => x.prop && x.prop.size >= PropSize.Large).All(x => x.xPx == 0);
                bool inside = s.props.All(x => Mathf.Abs(x.xPx) <= half);
                Check(clearOfPlayers && bigCentered && inside,
                      $"{s.id}: bố cục {string.Join(", ", s.props.Select(x => $"{x.prop?.id}@{x.xPx}"))}, người chơi ±{s.playerStartPx}");
            }

            // ---- UI (Asset Bible mục 7)
            Check(fighters.All(f => f.portrait && f.superArt), "6 nhân vật có chân dung + banner tuyệt chiêu");
            var skin = AssetDatabase.LoadAssetAtPath<UiSkin>(PixelPackBuilder.UiSkinPath);
            Check(skin && skin.font && skin.logo && skin.hpFrame && skin.hpFill && skin.energyFill && skin.guardFill && skin.roundWin,
                  $"UiSkin: font {(skin && skin.font ? skin.font.name : "-")}, logo, khung/thanh máu–năng lượng–đỡ, chấm hiệp");
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PixelPackBuilder.PanelPath);
            Check(panel && panel.themeStyleSheet && panel.referenceResolution == new Vector2Int(MatchRunner.RefWidth, MatchRunner.RefHeight),
                  $"PanelSettings: màn gốc {(panel ? panel.referenceResolution.ToString() : "-")}, theme {(panel && panel.themeStyleSheet ? "có" : "THIẾU")}");

            // ---- scene
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(PixelPackBuilder.ScenePath);
            Check(sceneAsset, $"Scene {PixelPackBuilder.ScenePath} tồn tại");
            Check(EditorBuildSettings.scenes.Any(s => s.path == PixelPackBuilder.ScenePath), "Scene có trong Build Settings");

            // ---- dữ liệu chiến đấu
            foreach (var f in fighters)
            {
                var desc = f.skills.Select(s =>
                    s.hits.Length > 0 ? $"{s.clip.name}:{s.hits.Length}×{s.hits.Sum(h => h.damage)}"
                    : s.effectSpec != null && s.effectSpec.enabled ? $"{s.clip.name}:{s.kind}"
                    : s.counterTicks > 0 ? $"{s.clip.name}:phản" : $"{s.clip.name}:{s.kind}");
                bool ok = f.combatVersion >= PixelPackCombat.CombatVersion
                          && f.hurtHeight is >= 60 and <= 140 && f.hurtHalfWidth is >= 10 and <= 40
                          && f.skills.All(s => s.hits.Length > 0 || (s.effectSpec?.enabled ?? false) || s.counterTicks > 0 || s.dashSpeed > 0)
                          && f.skills.SelectMany(s => s.hits).All(h => h.endTick >= h.startTick && h.x1 > h.x0 && h.y1 > h.y0 && h.endTick < 400);
                Check(ok, $"{f.id}: hurtbox {f.hurtHalfWidth * 2}×{f.hurtHeight}px · {string.Join(" · ", desc)}");
            }

            // ---- mô phỏng
            var longF = fighters.FirstOrDefault(f => f.id == "long");
            var baoF = fighters.FirstOrDefault(f => f.id == "bao");
            var batuF = fighters.FirstOrDefault(f => f.id == "ba_tu");
            var cho = stages.FirstOrDefault(s => s.id == "chodem");
            if (longF && baoF && batuF && cho)
            {
                var a = Scenario(longF, baoF, cho, sb, Check);
                var b2 = Scenario(longF, baoF, cho, null, null);
                Check(a.tick == b2.tick && a.p0.x == b2.p0.x && a.p1.hp == b2.p1.hp && a.p0.y == b2.p0.y
                      && a.props.Select(p => (p.x, p.y, p.phase, p.durability)).SequenceEqual(b2.props.Select(p => (p.x, p.y, p.phase, p.durability))),
                      "Tất định: chạy lại cùng chuỗi input cho kết quả giống hệt");
                CombatScenarios(longF, baoF, batuF, cho, Check);
                var c1 = CombatScenarios(longF, baoF, batuF, cho, null);
                var c2 = CombatScenarios(longF, baoF, batuF, cho, null);
                Check(c1 == c2, $"Tất định chiến đấu: 5 tình huống chạy lại cho cùng kết quả ({c1})");

                foreach (var f in fighters)
                {
                    var mv = f.moves;
                    var clips = mv == null ? Array.Empty<SpriteClip>() : new[]
                    {
                        mv.idle, mv.walkFwd, mv.walkBack, mv.run, mv.crouch, mv.crouchHold, mv.jumpStart, mv.jumpAir, mv.jumpLand,
                        mv.block, mv.blockHit, mv.rollFwd, mv.rollBack, mv.hitLight, mv.hitHeavy, mv.stun, mv.knockdown, mv.getup,
                        mv.intro, mv.win, mv.lose, mv.taunt,
                    };
                    int okClips = clips.Count(MoveSet.Ok);
                    bool normalsOk = f.normals.Length == 8 && f.normals.All(n => n.clip.IsValid)
                                     && f.normals.Take(FighterData.DiveLand).All(n => n.hits.Length > 0);
                    Check(okClips == 22 && normalsOk,
                          $"{f.id}: {okClips}/22 clip động tác · đòn thường {string.Join(" ", f.normals.Take(4).Select(n => $"{n.clip.name}[{(n.hits.Length > 0 ? $"{n.hits[0].startTick}-{n.hits[n.hits.Length - 1].endTick}t {n.hits.Sum(h => h.damage)}" : "—")}]"))}"
                          + $" · nối từ tick {string.Join("/", f.normals.Skip(1).Take(3).Select(n => n.chainStartTick))}");
                    var tam = f.normals.Where(n => n.placeholderArt).Select(n => n.clip.name).ToArray();
                    sb.AppendLine($"  INFO {f.id}: " + (tam.Length == 0 ? "đủ asset combo + trên không"
                        : $"đang dùng HÌNH TẠM cho {string.Join(", ", tam)} – chờ asset theo ArtSource/PixelPack/SPEC_combo_va_tren_khong.md"));
                }
                NormalScenarios(longF, baoF, cho, Check);
                var nA = NormalScenarios(longF, baoF, cho, null);
                var nB = NormalScenarios(longF, baoF, cho, null);
                Check(nA == nB, $"Tất định đòn thường: chạy lại cho cùng kết quả ({nA})");

                RoundScenarios(longF, baoF, cho, Check);
                Check(RoundScenarios(longF, baoF, cho, null) == RoundScenarios(longF, baoF, cho, null), "Tất định luật trận");

                var maiF = fighters.FirstOrDefault(f => f.id == "mai");
                var khangF = fighters.FirstOrDefault(f => f.id == "khang");
                Check(maiF && maiF.throwsIgnite && khangF && khangF.electrifyRadiusPx > 0,
                      "Ái lực: Mai ném vật bốc cháy, Khang nhiễm điện vật kim loại quanh mình");
                Check(AssetDatabase.LoadAssetAtPath<FxLibrary>(PixelPackCombat.FxLibraryPath) is { } lib
                      && lib.fire.Length > 0 && lib.electric.Length > 0 && lib.explosion.Length > 0 && lib.puddle.Length > 0,
                      "FxLibrary: đủ hình lửa / điện / nổ / vũng");
                if (maiF && khangF)
                {
                    ElementScenarios(longF, baoF, maiF, khangF, props, cho, Check);
                    var e1 = ElementScenarios(longF, baoF, maiF, khangF, props, cho, null);
                    var e2 = ElementScenarios(longF, baoF, maiF, khangF, props, cho, null);
                    Check(e1 == e2, $"Tất định môi trường: chạy lại cho cùng kết quả ({e1})");
                }
            }

            sb.AppendLine(fails == 0 ? "KẾT QUẢ: TẤT CẢ ĐẠT" : $"KẾT QUẢ: {fails} LỖI");
            return (fails == 0, sb.ToString());
        }

        /// <summary>Các tình huống đối kháng; trả về chuỗi tóm tắt máu/trạng thái để so tính tất định.</summary>
        static string CombatScenarios(FighterData lng, FighterData bao, FighterData batu, StageData stage, Action<bool, string> check)
        {
            void C(bool c, string m) => check?.Invoke(c, m);
            int half = Mathf.RoundToInt(stage.background.bounds.size.x * MatchRunner.Ppu / 2f);
            var summary = new StringBuilder();
            const int S = FightSim.Sub;

            FightSim New(FighterData a, FighterData b, int xa, int xb)
            {
                var sim = new FightSim(a, b, stage, half) { TrainingMode = true };
                sim.State.p0.x = xa * S;
                sim.State.p1.x = xb * S;
                for (int i = 0; i < sim.State.props.Length; i++) sim.State.props[i].phase = PropPhase.Broken;   // sân trống
                sim.Simulate(default, default);
                return sim;
            }
            void Run(FightSim sim, Buttons b0, Buttons b1, int ticks)
            {
                for (int i = 0; i < ticks; i++) sim.Simulate(new InputFrame(b0), new InputFrame(b1));
            }
            SkillData Skill(FighterData f, SkillInput i) => f.skills[f.FindSkill(i)];

            // 1) Cước Bay (→+L) lao tới, trúng nhiều nhịp
            {
                var sim = New(lng, bao, -60, 30);
                var sk = Skill(lng, SkillInput.Forward);
                Run(sim, Buttons.Right | Buttons.Skill, Buttons.None, 1);
                Run(sim, Buttons.None, Buttons.None, sk.clip.lengthTicks + 2);
                int lost = bao.maxHp - sim.State.p1.hp;
                C(lost > 0 && sim.State.p0.x > -60 * S,
                  $"Đòn: '{sk.name}' lao {(sim.State.p0.x / S) + 60}px, trúng Bảo -{lost} máu ({sk.hits.Length} nhịp, trạng thái {sim.State.p1.action})");
                summary.Append($"{sim.State.p1.hp},");
            }

            // 2) Đỡ: Bảo giữ nút đỡ trước đòn Thăng Long Quyền → không mất máu, thanh đỡ giảm
            {
                var sim = New(lng, bao, -20, 20);
                Run(sim, Buttons.None, Buttons.Guard, 3);
                Run(sim, Buttons.Up | Buttons.Skill, Buttons.Guard, 1);
                Run(sim, Buttons.None, Buttons.Guard, Skill(lng, SkillInput.Up).clip.lengthTicks + 2);
                C(sim.State.p1.hp == bao.maxHp && sim.State.p1.guard < FightSim.GuardMax,
                  $"Đỡ: Bảo giữ đỡ trước '{Skill(lng, SkillInput.Up).name}' → máu {sim.State.p1.hp}/{bao.maxHp}, thanh đỡ {sim.State.p1.guard}");
                summary.Append($"{sim.State.p1.hp}/{sim.State.p1.guard},");
            }

            // 3) Không đỡ được: Đòn Chân Kẹp (↓+L) xuyên thế đỡ
            {
                var sim = New(lng, bao, -20, 20);
                Run(sim, Buttons.None, Buttons.Guard, 3);
                Run(sim, Buttons.Down | Buttons.Skill, Buttons.Guard, 1);
                Run(sim, Buttons.None, Buttons.Guard, Skill(lng, SkillInput.Down).clip.lengthTicks + 2);
                int lost = bao.maxHp - sim.State.p1.hp;
                C(lost > 0, $"Không đỡ được: '{Skill(lng, SkillInput.Down).name}' xuyên thế đỡ, Bảo -{lost} máu");
                summary.Append($"{sim.State.p1.hp},");
            }

            // 4) Đạn: Long Khí Chưởng (L) bay trúng từ xa
            {
                var sim = New(lng, bao, -150, 150);
                var sk = Skill(lng, SkillInput.Neutral);
                Run(sim, Buttons.Skill, Buttons.None, 1);
                Run(sim, Buttons.None, Buttons.None, sk.effectSpec.spawnTick + sk.effectSpec.lifetime + 2);
                int lost = bao.maxHp - sim.State.p1.hp;
                C(lost == sk.effectSpec.damage, $"Đạn: '{sk.name}' bay {300}px trúng Bảo -{lost} máu (bảng: {sk.effectSpec.damage})");
                summary.Append($"{sim.State.p1.hp},");
            }

            // 5) Phản đòn: Bà Tư vào Thế Chờ (↑+L), Long lao tới bằng Cước Bay → Long mất máu, Bà Tư không
            {
                var sim = New(batu, lng, -30, 60);
                Run(sim, Buttons.Up | Buttons.Skill, Buttons.None, 1);
                Run(sim, Buttons.None, Buttons.Left | Buttons.Skill, 1);
                Run(sim, Buttons.None, Buttons.None, 60);
                int lostLong = lng.maxHp - sim.State.p1.hp, lostBatu = batu.maxHp - sim.State.p0.hp;
                C(lostLong > 0 && lostBatu == 0, $"Phản đòn: '{Skill(batu, SkillInput.Up).name}' – Long -{lostLong} máu, Bà Tư -{lostBatu}");
                summary.Append($"{sim.State.p0.hp}/{sim.State.p1.hp}");
            }
            return summary.ToString();
        }

        /// <summary>Đòn thường J/K, lăn né, hạ gục (GDD mục 2).</summary>
        static string NormalScenarios(FighterData lng, FighterData bao, StageData stage, Action<bool, string> check)
        {
            void C(bool c, string m) => check?.Invoke(c, m);
            const int S = FightSim.Sub;
            int half = Mathf.RoundToInt(stage.background.bounds.size.x * MatchRunner.Ppu / 2f);
            var sum = new StringBuilder();
            FightSim New(int xa, int xb)
            {
                var sim = new FightSim(lng, bao, stage, half) { TrainingMode = true };
                sim.State.p0.x = xa * S;
                sim.State.p1.x = xb * S;
                for (int i = 0; i < sim.State.props.Length; i++) sim.State.props[i].phase = PropPhase.Broken;
                sim.Simulate(default, default);
                return sim;
            }
            int maxCombo = 0;
            void Run(FightSim sim, Buttons b0, Buttons b1, int n)
            {
                for (int i = 0; i < n; i++)
                {
                    sim.Simulate(new InputFrame(b0), new InputFrame(b1));
                    maxCombo = Math.Max(maxCombo, sim.State.p1.comboTaken);
                }
            }

            // 1) combo J-J-J: mỗi lần bấm J sau khi đòn trước vào nhịp đánh → nối đòn kế tiếp
            {
                var sim = New(-20, 20);
                maxCombo = 0;
                for (int k = 0; k < 3; k++)
                {
                    Run(sim, Buttons.Light, Buttons.None, 1);
                    Run(sim, Buttons.None, Buttons.None, lng.normals[k].hits[0].startTick + 1);
                }
                Run(sim, Buttons.None, Buttons.None, 40);
                int lost = bao.maxHp - sim.State.p1.hp;
                C(maxCombo >= 3 && lost > 0, $"Combo J-J-J: {maxCombo} hit liên tiếp, Bảo -{lost} máu (giảm 10%/hit)");
                sum.Append(sim.State.p1.hp).Append(',');
            }

            // 2) đánh mạnh K phá thế đỡ: Bảo giữ đỡ → không mất máu nhưng vỡ thế đỡ, choáng
            {
                var sim = New(-20, 20);
                Run(sim, Buttons.None, Buttons.Guard, 3);
                Run(sim, Buttons.Heavy, Buttons.Guard, 1);
                bool broke = false;
                for (int i = 0; i < lng.normals[FighterData.Heavy].clip.lengthTicks + 2; i++)
                {
                    Run(sim, Buttons.None, Buttons.Guard, 1);
                    broke |= sim.State.p1.dizzy && sim.State.p1.action == FighterAction.Hitstun;
                }
                C(broke && sim.State.p1.hp == bao.maxHp, $"Đánh mạnh K: phá thế đỡ của Bảo (choáng), máu giữ nguyên {sim.State.p1.hp}");
                sum.Append(broke ? 1 : 0).Append(',');
            }

            // 3) lăn né: Bảo lăn (đỡ + hướng) đúng lúc Long đánh J → bất tử, không mất máu
            {
                var sim = New(-20, 24);
                Run(sim, Buttons.Light, Buttons.Guard, 1);
                Run(sim, Buttons.None, Buttons.Guard | Buttons.Left, 1);
                Run(sim, Buttons.None, Buttons.None, 30);
                C(sim.State.p1.hp == bao.maxHp && sim.State.p1.x < 24 * S,
                  $"Lăn né: Bảo lăn qua đòn J (x {24} → {sim.State.p1.x / S}px), không mất máu");
                sum.Append(sim.State.p1.x).Append(',');
            }

            // 5) nối combo mượt: đòn sau bắt đầu ở tư thế nối (bỏ đoạn lấy đà), không quay về frame 0
            {
                var sim = New(-20, 20);
                Run(sim, Buttons.Light, Buttons.None, 1);
                Run(sim, Buttons.None, Buttons.None, lng.normals[0].hits[0].startTick + 1);
                Run(sim, Buttons.Light, Buttons.None, 1);
                var st = sim.State.p0;
                C(st.normal && st.skillIndex == 1 && st.actionTick == lng.normals[1].chainStartTick,
                  $"Nối J→J: đòn 2 bắt đầu ở tick {lng.normals[1].chainStartTick} (bỏ lấy đà), không về tư thế đứng");
                sum.Append(st.actionTick).Append(',');
            }

            // 6) đánh trên không: nhảy, tới đỉnh bấm K → đòn trên không trúng Bảo (phải đỡ đứng)
            {
                var sim = New(-30, 10);
                Run(sim, Buttons.Up, Buttons.None, 1);
                int guard = 0;
                while (guard++ < 80 && sim.State.p0.vy > 0) Run(sim, Buttons.None, Buttons.None, 1);   // tới đỉnh nhảy
                Run(sim, Buttons.Heavy, Buttons.None, 1);
                bool air = sim.State.p0.normal && sim.State.p0.skillIndex == FighterData.AirHeavy && !sim.State.p0.Grounded;
                Run(sim, Buttons.None, Buttons.None, 40);
                int lost = bao.maxHp - sim.State.p1.hp;
                C(air && lost > 0 && sim.State.p0.Grounded && sim.State.p0.action != FighterAction.Skill,
                  $"Đánh trên không: K khi rơi → '{lng.normals[FighterData.AirHeavy].name}', Bảo -{lost} máu, tiếp đất bình thường");
                sum.Append(lost).Append(',');
            }

            // 7) đá bổ: đang rơi bấm ↓+K → lao chéo xuống, đòn từ trên (Bảo ngồi đỡ vẫn dính), tiếp đất có clip riêng
            {
                var sim = New(-60, 10);
                Run(sim, Buttons.Up, Buttons.None, 1);
                int guard = 0;
                while (guard++ < 80 && sim.State.p0.vy >= 0) Run(sim, Buttons.None, Buttons.Down | Buttons.Guard, 1);
                Run(sim, Buttons.Down | Buttons.Heavy, Buttons.Down | Buttons.Guard, 1);
                bool dive = sim.State.p0.normal && sim.State.p0.skillIndex == FighterData.AirDive;
                bool landed = false;
                for (int i = 0; i < 60; i++)
                {
                    Run(sim, Buttons.None, Buttons.Down | Buttons.Guard, 1);
                    landed |= sim.State.p0.normal && sim.State.p0.skillIndex == FighterData.DiveLand;
                }
                int lost = bao.maxHp - sim.State.p1.hp;
                C(dive && lost > 0, $"Đá bổ ↓+K: lao xuống trúng Bảo đang ngồi đỡ -{lost} máu (đòn từ trên)" + (landed ? ", tiếp đất có clip riêng" : ", bật lên sau khi trúng"));
                sum.Append(lost).Append(',');
            }

            // 4) hạ gục: Bảo còn 20 máu, dính J → KO, Long thắng
            {
                var sim = New(-20, 20);
                sim.State.p1.hp = 20;
                Run(sim, Buttons.Light, Buttons.None, 1);
                Run(sim, Buttons.None, Buttons.None, 80);
                C(sim.State.koTick > 0 && sim.State.winner == 0 && sim.State.p1.action == FighterAction.Knockdown,
                  $"Hạ gục: Bảo hết máu → nằm (KO) ở tick {sim.State.koTick}, Long thắng");
                sum.Append(sim.State.koTick);
            }
            return sum.ToString();
        }

        /// <summary>Phản ứng thuộc tính vật dụng (GDD mục 4) trên sân tạm chỉ có các vật cần thử.</summary>
        static string ElementScenarios(FighterData lng, FighterData bao, FighterData mai, FighterData khang,
                                       PropData[] props, StageData baseStage, Action<bool, string> check)
        {
            void C(bool c, string m) => check?.Invoke(c, m);
            const int S = FightSim.Sub;
            int half = Mathf.RoundToInt(baseStage.background.bounds.size.x * MatchRunner.Ppu / 2f);
            var sum = new StringBuilder();
            PropData P(string id) => props.First(p => p.id == id);

            FightSim New(FighterData a, FighterData b, int xa, int xb, params (string id, int x)[] items)
            {
                var st = ScriptableObject.CreateInstance<StageData>();
                st.sideMarginPx = baseStage.sideMarginPx;
                st.props = items.Select(it => new StageData.PropSpawn { prop = P(it.id), xPx = it.x }).ToArray();
                var sim = new FightSim(a, b, st, half) { TrainingMode = true };
                UnityEngine.Object.DestroyImmediate(st);
                sim.State.p0.x = xa * S;
                sim.State.p1.x = xb * S;
                sim.Simulate(default, default);
                return sim;
            }
            void Run(FightSim sim, Buttons b0, Buttons b1, int n)
            {
                for (int i = 0; i < n; i++) sim.Simulate(new InputFrame(b0), new InputFrame(b1));
            }
            int Count(FightSim sim, EffectKind k) => sim.State.effects.Count(e => e.active && e.kind == k);

            // 1) Lửa đốt vật Dễ cháy: Mai "Xào Lửa" (→+L) phun vào chổi tre
            {
                var sim = New(mai, lng, -120, 150, ("choi_tre", -70));
                Run(sim, Buttons.Right | Buttons.Skill, Buttons.None, 1);
                Run(sim, Buttons.None, Buttons.None, 30);
                C(sim.State.props[0].burnTicks > 0, $"Cháy: 'Xào Lửa' đốt chổi tre (còn cháy {sim.State.props[0].burnTicks} tick)");
                sum.Append(sim.State.props[0].burnTicks).Append(',');
            }

            // 2) Lửa kích nổ Chất nổ – nổ trúng cả hai bên: Mai phun lửa vào bình gas sát mình
            {
                var sim = New(mai, lng, -120, 0, ("binh_gas", -70));
                Run(sim, Buttons.Right | Buttons.Skill, Buttons.None, 1);
                Run(sim, Buttons.None, Buttons.None, 40);
                int lostMai = mai.maxHp - sim.State.p0.hp, lostLong = lng.maxHp - sim.State.p1.hp;
                C(sim.State.props[0].phase == PropPhase.Broken && lostMai > 0 && lostLong > 0,
                  $"Nổ: lửa kích nổ bình gas, trúng cả hai bên (Mai -{lostMai}, Long -{lostLong}, bán kính {FightSim.ExplosionRadiusPx}px)");
                sum.Append(sim.State.p0.hp).Append('/').Append(sim.State.p1.hp).Append(',');
            }

            // 3) Ném bình gas trúng đối thủ: 60 + nổ 150; người ném đứng ngoài bán kính không bị gì
            {
                var sim = New(lng, bao, -190, 20, ("binh_gas", -180));
                Run(sim, Buttons.Grab, Buttons.None, 1);
                Run(sim, Buttons.None, Buttons.None, 60);
                Run(sim, Buttons.Grab, Buttons.None, 1);
                Run(sim, Buttons.None, Buttons.None, 120);
                int lost = bao.maxHp - sim.State.p1.hp;
                C(sim.State.props[0].phase == PropPhase.Broken && lost >= 60 + 100 && sim.State.p0.hp == lng.maxHp,
                  $"Ném bình gas: Bảo -{lost} máu (60 ném + nổ), Long đứng xa không mất máu");
                sum.Append(sim.State.p1.hp).Append(',');
            }

            // 4) Ái lực Khang: vật Kim loại trong 2 m tự nhiễm điện; Long nhặt ống tuýp nhiễm điện vung trúng → tê thêm
            {
                var sim = New(khang, lng, -100, 60, ("ong_tuyp", -40));
                Run(sim, Buttons.None, Buttons.None, 2);
                bool charged = sim.State.props[0].shockTicks > 0;
                C(charged, $"Nhiễm điện: ống tuýp cách Khang 60px tự nhiễm điện ({sim.State.props[0].shockTicks} tick)");
                sum.Append(sim.State.props[0].shockTicks).Append(',');
            }

            // 5) Vũng nước + điện: nồi phở vỡ đổ ra vũng (trơn), Khang "Kìm Điện" chạm vũng → vùng giật làm Long mất máu
            {
                var sim = New(khang, lng, -60, 10, ("noi_pho", 0));
                ref var pot = ref sim.State.props[0];
                pot.phase = PropPhase.Flying;                 // giả lập nồi bị ném trúng rồi rơi: hết bền → vỡ khi chạm đất
                pot.durability = 0;
                pot.y = 4 * S;
                for (int i = 0; i < 30 && Count(sim, EffectKind.Puddle) == 0; i++)   // chờ nồi rơi chạm đất
                    Run(sim, Buttons.None, Buttons.None, 1);
                bool puddle = Count(sim, EffectKind.Puddle) == 1;
                // trơn: Long đi rồi thả nút vẫn trượt tiếp
                Run(sim, Buttons.None, Buttons.Right, 12);
                int xStop = sim.State.p1.x;
                Run(sim, Buttons.None, Buttons.None, 4);
                bool slides = sim.State.p1.x != xStop;
                sim.State.p1.x = 10 * S;
                sim.State.p1.vx = 0;
                Run(sim, Buttons.Skill, Buttons.None, 1);
                Run(sim, Buttons.None, Buttons.None, 60);
                int lost = lng.maxHp - sim.State.p1.hp;
                C(puddle && slides && Count(sim, EffectKind.ShockZone) == 1 && lost > 0,
                  $"Vũng: nồi phở vỡ → vũng nước (trơn: {(slides ? "có trượt" : "không trượt")}), gặp điện → vùng giật, Long -{lost}");
                sum.Append(sim.State.p1.hp).Append(',');
            }

            // 6) Mai ném vật nào cũng bốc cháy → trúng gây bỏng 10/giây
            {
                var sim = New(mai, bao, -170, 0, ("ghe_nhua", -160));
                Run(sim, Buttons.Grab, Buttons.None, 1);
                Run(sim, Buttons.None, Buttons.None, 60);
                Run(sim, Buttons.Grab, Buttons.None, 1);
                Run(sim, Buttons.None, Buttons.None, 40);
                int burn = sim.State.p1.burnTicks;
                Run(sim, Buttons.None, Buttons.None, 120);
                int lost = bao.maxHp - sim.State.p1.hp;
                C(burn > 0 && lost > P("ghe_nhua").throwDamage,
                  $"Bỏng: ghế Mai ném bốc cháy, trúng Bảo -{lost} máu ({P("ghe_nhua").throwDamage} ném + bỏng 10/giây)");
                sum.Append(sim.State.p1.hp);
            }
            return sum.ToString();
        }

        static MatchState Scenario(FighterData p0, FighterData p1, StageData stage, StringBuilder sb, Action<bool, string> check)
        {
            void C(bool c, string m) => check?.Invoke(c, m);
            var sim = new FightSim(p0, p1, stage, Mathf.RoundToInt(stage.background.bounds.size.x * MatchRunner.Ppu / 2f));
            int Run(Buttons b0, int ticks, Buttons b1 = Buttons.None)
            {
                for (int i = 0; i < ticks; i++) sim.Simulate(new InputFrame(b0), new InputFrame(b1));
                return sim.State.tick;
            }

            // đi sang phải
            int x0 = sim.State.p0.x;
            Run(Buttons.Right, 30);
            C(sim.State.p0.x > x0 && sim.State.p0.facing == 1, $"Đi: P1 x {x0 / FightSim.Sub}px → {sim.State.p0.x / FightSim.Sub}px");
            Run(Buttons.None, 2);

            // nhảy
            Run(Buttons.Up, 1);
            int peak = 0;
            for (int i = 0; i < 90 && (i == 0 || sim.State.p0.y > 0); i++) { Run(Buttons.None, 1); peak = Math.Max(peak, sim.State.p0.y); }
            C(peak > 40 * FightSim.Sub && sim.State.p0.y == 0 && sim.State.p0.action == FighterAction.Idle,
              $"Nhảy: cao {peak / FightSim.Sub}px rồi tiếp đất");

            // chiêu tiến (→ + L)
            Run(Buttons.Right | Buttons.Skill, 1);
            var fwd = p0.skills[p0.FindSkill(SkillInput.Forward)];
            C(sim.State.p0.action == FighterAction.Skill && sim.State.p0.skillIndex == p0.FindSkill(SkillInput.Forward),
              $"Chiêu →+L ra '{fwd.name}' ({fwd.clip.frames.Length} frame, {fwd.clip.lengthTicks} tick)");
            Run(Buttons.None, fwd.clip.lengthTicks + 1);
            C(sim.State.p0.action == FighterAction.Idle, "Hết chiêu trở về đứng");

            // nhặt ghế gần nhất bên phải rồi ném vào P2
            int chair = Array.FindIndex(sim.State.props, p => sim.Props[p.dataIndex].id == "ghe_nhua" && p.x > 0);
            if (chair < 0) { C(false, "Không có ghế để thử"); return sim.State.Clone(); }
            sim.State.p0.x = sim.State.props[chair].x - 20 * FightSim.Sub;
            sim.State.p1.x = sim.State.props[chair].x + 110 * FightSim.Sub;
            // chiêu lao tới ở trên có thể đã trúng P2 → đặt lại P2 đứng yên, đủ máu, không bất tử
            sim.State.p1.action = FighterAction.Idle;
            sim.State.p1.stunTicks = sim.State.p1.invuln = sim.State.p1.comboTaken = sim.State.p1.juggle = 0;
            sim.State.p1.y = sim.State.p1.vy = 0;
            sim.State.p1.hp = p1.maxHp;
            Run(Buttons.None, 1);
            Run(Buttons.Grab, 1);
            var pickupClip = sim.ActionInteraction(0)?.pickup;
            C(sim.State.p0.action == FighterAction.Pickup && sim.State.p0.heldProp == chair, "Nhặt: bắt đầu clip nhặt ghế");
            Run(Buttons.None, (pickupClip?.lengthTicks ?? 30) + 1);
            C(sim.State.p0.heldProp == chair && sim.State.props[chair].phase == PropPhase.Held && sim.State.p0.action == FighterAction.Idle,
              "Nhặt xong: đang cầm ghế");
            int hp0 = sim.State.p1.hp;
            Run(Buttons.Grab, 1);
            Run(Buttons.None, 120);
            C(sim.State.p0.heldProp == -1 && sim.State.p1.hp == hp0 - sim.Props[sim.State.props[chair].dataIndex].throwDamage,
              $"Ném: trúng P2, máu {hp0} → {sim.State.p1.hp}");
            var chairData = sim.Props[sim.State.props[chair].dataIndex];
            C(sim.State.props[chair].phase == PropPhase.Floor && sim.State.props[chair].y == 0
              && sim.State.props[chair].durability == chairData.durability - 1,
              $"Ghế rơi xuống sàn, độ bền {chairData.durability} → {sim.State.props[chair].durability} (chỉ trừ khi trúng)");
            return sim.State.Clone();
        }

        static bool IsPaintedUi(string path)
        {
            string p = path.Replace('\\', '/');
            string f = System.IO.Path.GetFileName(p);
            return p.Contains("/UI/") && (f.StartsWith("portrait_") || f.StartsWith("super_") || f.StartsWith("logo") || f.StartsWith("character_select"));
        }

        /// <summary>Luật trận (GDD mục 2): 3 hiệp thắng 2, 99 giây, hết giờ so % máu.</summary>
        static string RoundScenarios(FighterData lng, FighterData bao, StageData stage, Action<bool, string> check)
        {
            void C(bool c, string m) => check?.Invoke(c, m);
            int half = Mathf.RoundToInt(stage.background.bounds.size.x * MatchRunner.Ppu / 2f);
            var sim = new FightSim(lng, bao, stage, half) { TrainingMode = true };
            void Run(Buttons b0, int n) { for (int i = 0; i < n; i++) sim.Simulate(new InputFrame(b0), default); }
            void KoP2()
            {
                sim.State.p0.x = -20 * FightSim.Sub;
                sim.State.p1.x = 20 * FightSim.Sub;
                sim.State.p1.hp = 10;
                Run(Buttons.None, 1);
                Run(Buttons.Light, 1);
                Run(Buttons.None, 40);
            }
            C(sim.State.round == 1 && sim.SecondsLeft == 99, $"Hiệp 1 bắt đầu, đồng hồ {sim.SecondsLeft} giây");
            KoP2();
            bool r1 = sim.RoundOver && sim.Wins(0) == 1 && !sim.MatchOver;
            Run(Buttons.None, FightSim.RoundEndDelay);
            bool r2Start = sim.State.round == 2 && !sim.RoundOver && sim.State.p1.hp == bao.maxHp && sim.State.p0.x == -sim.State.p1.x;   // về vị trí đối xứng
            C(r1 && r2Start, $"K.O. hiệp 1 → Long thắng hiệp, sau {FightSim.RoundEndDelay / 60} giây sang hiệp 2, hồi máu, về vị trí");
            KoP2();
            C(sim.MatchOver && sim.State.matchWinner == 0 && sim.Wins(0) == 2, $"Thắng 2 hiệp → Long thắng trận ({sim.Wins(0)}-{sim.Wins(1)})");

            var t = new FightSim(lng, bao, stage, half) { TrainingMode = true };
            t.State.p0.hp = lng.maxHp / 2;                                 // Long 50% máu, Bảo 100%
            t.State.timer = 3;
            for (int i = 0; i < 5; i++) t.Simulate(default, default);
            C(t.RoundOver && t.State.timeOver && t.State.winner == 1, "Hết giờ: Bảo còn nhiều % máu hơn → thắng hiệp");
            return $"{sim.State.tick},{sim.Wins(0)},{t.State.winner}";
        }

        static T[] Load<T>(string sub) where T : UnityEngine.Object =>
            AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { $"{PixelPackBuilder.DataRoot}/{sub}" })
                .Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g))).Where(x => x).ToArray();
    }
}
