using System;
using LoanDau.Core;
using LoanDau.Data;
using LoanDau.UI;
using UnityEngine;

namespace LoanDau.Game
{
    /// <summary>
    /// Scene chơi thử: 2 người trên 1 bàn phím (GDD mục 6 – làm chế độ này đầu tiên).
    /// Đọc bàn phím → InputFrame → FightSim.Simulate() đúng 60 tick/giây → vẽ sprite theo State.
    /// Lớp hiển thị chỉ đọc trạng thái; đổi asset không ảnh hưởng mô phỏng.
    /// Ở chế độ chỉnh sửa: hiện bản xem trước (stage + 2 nhân vật + vật dụng ở vị trí xuất phát), không lưu vào scene.
    /// </summary>
    [ExecuteAlways]
    public class MatchRunner : MonoBehaviour
    {
        public const int Ppu = FightSim.PixelsPerUnit;
        public const int RefWidth = 640, RefHeight = 360;   // ×3 = 1920×1080, pixel-perfect

        [Header("Dữ liệu")]
        public FighterData[] fighters = Array.Empty<FighterData>();
        public StageData[] stages = Array.Empty<StageData>();
        [Tooltip("Hình lửa / điện / nổ / vũng (Assets/Data/FxLibrary.asset)")] public FxLibrary fx;
        [Tooltip("HUD + màn chọn nhân vật (UI Toolkit). Để trống = HUD đơn giản bằng OnGUI")] public GameUI ui;
        [Tooltip("Nhạc / SFX / thoại (Assets/Data/AudioLibrary.asset). Trong Editor để trống = tự nạp từ Assets/Audio/PixelPack")]
        public AudioLibrary audioLibrary;
        [Tooltip("Mối nguy môi trường của stage (xe máy, cần cẩu, bếp lửa, tủ điện + mưa)")] public bool stageHazards = true;
        MatchAudio matchAudio;
        SpriteRenderer hazardRenderer, hazardWarnRenderer;
        SpriteRenderer[] rainRenderers = Array.Empty<SpriteRenderer>();
        Sprite hazardWarnSprite;
        [Tooltip("Bấm Play: mở màn chọn nhân vật trước (tắt để vào trận ngay với lựa chọn bên dưới)")] public bool startWithSelect = true;
        bool inSelect;

        [Header("Lựa chọn (F1/F2: đổi nhân vật P1/P2 · F3: đổi stage · F5: chơi lại)")]
        public int p1Fighter;
        public int p2Fighter = 1;
        public int stageIndex;
        public bool trainingMode = true;

        FightSim sim;
        float accumulator;
        Camera cam;
        SpriteRenderer stageRenderer;
        readonly SpriteRenderer[] fighterRenderers = new SpriteRenderer[2];
        SpriteRenderer[] propRenderers = Array.Empty<SpriteRenderer>();
        System.Collections.Generic.List<SpriteRenderer>[] effectViews = Array.Empty<System.Collections.Generic.List<SpriteRenderer>>();
        SpriteRenderer darkRenderer;
        SpriteRenderer[] cableLines, cableTips;
        SpriteRenderer[] propOverlays = Array.Empty<SpriteRenderer>();
        SpriteRenderer[] propOutlines = Array.Empty<SpriteRenderer>();

        /// <summary>Hiệu ứng chỉ để nhìn (tia lửa, chữ BỐP, mảnh vỡ…) – chạy theo thời gian thực, không ảnh hưởng mô phỏng.</summary>
        class Vfx
        {
            public SpriteRenderer r;
            public Sprite[] frames;
            public SpriteClip hitClip;
            public int target;
            public int heightPx;
            public float t0, dur, scale, vx, vy, gravity, spin;
            public Vector3 pos;
            public bool text;
            public Color tint = Color.white;
        }
        readonly System.Collections.Generic.List<Vfx> vfx = new();
        readonly System.Collections.Generic.Stack<SpriteRenderer> vfxPool = new();
        readonly System.Random vfxRandom = new(1234);
        [Tooltip("Số tick đầu trận diễn clip vào trận, khoá điều khiển")] public int introTicks = 70;
        readonly SpriteRenderer[] fighterOverlays = new SpriteRenderer[2];
        readonly SpriteRenderer[] rageAuras = new SpriteRenderer[2];
        static Sprite whiteSprite;
        [Tooltip("F4: hiện hộp nhận đòn (xanh), nhịp đánh đang hoạt động (đỏ), đạn/bẫy (cam)")]
        public bool showHitboxes;
        float stageHalfWidth;
        float stageBottomY;
        Transform root;
        bool previewDirty;

        static bool Playing => Application.isPlaying;

        void Start()
        {
            if (!Playing) return;
#if UNITY_EDITOR
            if (!audioLibrary && fighters != null && stages != null)
                audioLibrary = AudioLibrary.Populate(null,
                    Array.ConvertAll(stages, s => s ? s.id : ""), Array.ConvertAll(fighters, f => f ? f.id : ""));
#endif
            if (audioLibrary) matchAudio = new MatchAudio(gameObject, audioLibrary);
            if (ui && matchAudio != null) ui.PlaySelectSound = matchAudio.PlayCue;
            if (ui && startWithSelect) OpenSelect();
            else Rebuild();
        }

        /// <summary>Màn chọn nhân vật + sân → khi xong thì vào trận (đấu thật: tắt chế độ luyện tập).</summary>
        public void OpenSelect()
        {
            inSelect = true;
            ClearRoot();
            sim = null;
            matchAudio?.Menu();
            ui.ShowSelect(fighters, stages, p1Fighter, p2Fighter, (a, b, st) =>
            {
                p1Fighter = a;
                p2Fighter = b;
                stageIndex = st;
                trainingMode = false;
                inSelect = false;
                Rebuild();
            });
        }

        // ---- xem trước trong Editor (không chạy mô phỏng, không lưu vào scene)
        void OnEnable()
        {
            if (!Playing) RequestPreview();
        }

        void OnValidate()
        {
            if (!Playing) RequestPreview();           // không được tạo object trong OnValidate → hoãn
        }

        void RequestPreview()
        {
            previewDirty = true;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this && !Application.isPlaying && previewDirty && isActiveAndEnabled)
                {
                    previewDirty = false;
                    Rebuild();
                }
            };
#endif
        }

        void OnDisable()
        {
            if (!Playing) ClearRoot();
        }

        void ClearRoot()
        {
            if (!root) return;
            if (Playing) Destroy(root.gameObject);
            else DestroyImmediate(root.gameObject);
            root = null;
        }

        public void Rebuild()
        {
            bool preview = !Playing;
            if (fighters == null || stages == null || fighters.Length == 0 || stages.Length == 0
                || Array.Exists(fighters, f => !f) || Array.Exists(stages, s => !s || !s.background))
            {
                if (preview) return;
                Debug.LogError("[Match] Chưa có dữ liệu – chạy menu Loan Dau → PixelPack → Dựng dữ liệu + scene.");
                enabled = false;
                return;
            }
            p1Fighter = Mod(p1Fighter, fighters.Length);
            p2Fighter = Mod(p2Fighter, fighters.Length);
            stageIndex = Mod(stageIndex, stages.Length);
            var stage = stages[stageIndex];

            ClearRoot();
            if (preview)
            {
                // bản xem trước cũ còn sót sau khi Unity nạp lại code (mất tham chiếu) → dọn
                for (var old = GameObject.Find("_MatchPreview"); old; old = GameObject.Find("_MatchPreview"))
                    DestroyImmediate(old);
            }
            root = new GameObject(preview ? "_MatchPreview" : "_Match").transform;
            if (preview) root.gameObject.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;

            // nền: mép dưới ảnh nằm dưới mặt sàn (y = 0) đúng floorFromBottomPx
            stageRenderer = NewRenderer("Stage", stage.background, -100);
            var b = stage.background.bounds;
            stageBottomY = -stage.floorFromBottomPx / (float)Ppu;
            stageRenderer.transform.position = new Vector3(-b.center.x, stageBottomY - b.min.y, 0);
            stageHalfWidth = b.size.x / 2f;
            // bệ được hạ thấp hơn chỗ vẽ trên nền: vẽ riêng sprite bệ ở chỗ đứng được (sau lưng nhân vật)
            foreach (var pl in stage.platforms ?? Array.Empty<StageData.Platform>())
                if (pl != null && pl.art)
                    NewRenderer("Platform_" + pl.name, pl.art, -4).transform.position = ToWorld(pl.artXPx * FightSim.Sub, pl.artBottomPx * FightSim.Sub);

            sim = new FightSim(fighters[p1Fighter], fighters[p2Fighter], stage, Mathf.RoundToInt(stageHalfWidth * Ppu))
            {
                TrainingMode = trainingMode,
                HazardsEnabled = stageHazards && !preview,
            };
            for (int i = 0; i < 2; i++)
                fighterRenderers[i] = NewRenderer($"P{i + 1}_{sim.Fighters[i].id}", sim.Fighters[i].idle, 10 + i);
            propRenderers = new SpriteRenderer[sim.Props.Length];
            for (int i = 0; i < propRenderers.Length; i++)
                propRenderers[i] = NewRenderer($"Prop_{sim.Props[i].id}", sim.Props[i].sprite, 0);
            propOutlines = new SpriteRenderer[sim.Props.Length];
            for (int i = 0; i < propOutlines.Length; i++) propOutlines[i] = NewRenderer($"PropOutline_{i}", sim.Props[i].outline, 1);
            foreach (var v in vfx) if (v.r) v.r.enabled = false;
            vfx.Clear();
            vfxPool.Clear();
            if (!preview) sim.IntroTicks = introTicks;
            propOverlays = new SpriteRenderer[sim.Props.Length];
            for (int i = 0; i < propOverlays.Length; i++) propOverlays[i] = NewRenderer($"PropFx_{i}", null, 21);
            for (int i = 0; i < 2; i++) fighterOverlays[i] = NewRenderer($"P{i + 1}_Fx", null, 13);
            for (int i = 0; i < 2; i++) rageAuras[i] = NewRenderer($"P{i + 1}_Rage", null, 12);
            effectViews = new System.Collections.Generic.List<SpriteRenderer>[FightSim.MaxEffects];
            for (int i = 0; i < effectViews.Length; i++) effectViews[i] = new System.Collections.Generic.List<SpriteRenderer>();

            // lớp tối màn hình khi tung tuyệt chiêu (giữa nền và nhân vật)
            if (!whiteSprite) whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
            cableLines = new SpriteRenderer[2];
            cableTips = new SpriteRenderer[2];
            for(int i=0;i<2;i++) {
                cableLines[i]=NewRenderer("Cable_"+i,null,14);
                cableTips[i]=NewRenderer("Hook_"+i,null,15);
            }
            darkRenderer = NewRenderer("ScreenDark", whiteSprite, -50);
            darkRenderer.color = new Color(0.02f, 0.02f, 0.06f, 0f);
            darkRenderer.transform.localScale = new Vector3(64, 32, 1);
            BuildHazardViews(stage, preview);

            cam = preview ? null : Camera.main;           // xem trước: không đụng camera (tránh làm bẩn scene)
            if (cam)
            {
                cam.orthographic = true;
                cam.orthographicSize = RefHeight / 2f / Ppu;
                cam.backgroundColor = new Color(0.07f, 0.07f, 0.12f);
                cam.clearFlags = CameraClearFlags.SolidColor;
            }
            accumulator = 0;
            if (!preview && ui) ui.ShowHud(sim, new[] { fighters[p1Fighter], fighters[p2Fighter] });
            if (!preview) matchAudio?.StartMatch(sim, stage);
            Render();
        }

        SpriteRenderer NewRenderer(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name) { hideFlags = root.gameObject.hideFlags };
            go.transform.SetParent(root, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = order;
            return r;
        }

        static int Mod(int a, int n) => ((a % n) + n) % n;

        // ------------------------------------------------------------------ vòng lặp cố định 60 tick
        void Update()
        {
            if (!Playing)
            {
                if (previewDirty)
                {
                    previewDirty = false;
                    Rebuild();
                }
                return;
            }
            if (inSelect) return;                                         // màn chọn: GameUI tự đọc phím
            if (ui && (Input.GetKeyDown(KeyCode.Escape) || (sim != null && sim.MatchOver && Input.GetKeyDown(KeyCode.Return))))
            {
                OpenSelect();
                return;
            }
            if (Input.GetKeyDown(KeyCode.F1)) { p1Fighter++; Rebuild(); return; }
            if (Input.GetKeyDown(KeyCode.F2)) { p2Fighter++; Rebuild(); return; }
            if (Input.GetKeyDown(KeyCode.F3)) { stageIndex++; Rebuild(); return; }
            if (Input.GetKeyDown(KeyCode.F5)) { Rebuild(); return; }
            if (Input.GetKeyDown(KeyCode.F4)) showHitboxes = !showHitboxes;
            if (Input.GetKeyDown(KeyCode.F6) && sim != null) trainingMode = sim.TrainingMode = !sim.TrainingMode;
            if (sim == null) return;

            accumulator += Mathf.Min(Time.unscaledDeltaTime, 0.25f);
            const float dt = 1f / FightSim.TickRate;
            var in0 = ReadP1();
            var in1 = ReadP2();
            while (accumulator >= dt)
            {
                sim.Simulate(in0, in1);
                foreach (var ev in sim.Events)
                {
                    SpawnVfx(ev);
                    if (ui) ui.OnSimEvent(ev);
                }
                matchAudio?.OnTick(sim);
                accumulator -= dt;
            }
            Render();
        }

        static InputFrame ReadP1()
        {
            Buttons b = 0;
            if (Input.GetKey(KeyCode.A)) b |= Buttons.Left;
            if (Input.GetKey(KeyCode.D)) b |= Buttons.Right;
            if (Input.GetKey(KeyCode.W)) b |= Buttons.Up;
            if (Input.GetKey(KeyCode.S)) b |= Buttons.Down;
            if (Input.GetKey(KeyCode.J)) b |= Buttons.Light;
            if (Input.GetKey(KeyCode.K)) b |= Buttons.Heavy;
            if (Input.GetKey(KeyCode.L)) b |= Buttons.Skill;
            if (Input.GetKey(KeyCode.U)) b |= Buttons.Grab;
            if (Input.GetKey(KeyCode.I)) b |= Buttons.Guard;
            return new InputFrame(b);
        }

        static InputFrame ReadP2()
        {
            Buttons b = 0;
            if (Input.GetKey(KeyCode.LeftArrow)) b |= Buttons.Left;
            if (Input.GetKey(KeyCode.RightArrow)) b |= Buttons.Right;
            if (Input.GetKey(KeyCode.UpArrow)) b |= Buttons.Up;
            if (Input.GetKey(KeyCode.DownArrow)) b |= Buttons.Down;
            if (Input.GetKey(KeyCode.Comma) || Input.GetKey(KeyCode.Keypad1)) b |= Buttons.Light;
            if (Input.GetKey(KeyCode.Period) || Input.GetKey(KeyCode.Keypad2)) b |= Buttons.Heavy;
            if (Input.GetKey(KeyCode.Slash) || Input.GetKey(KeyCode.Keypad3)) b |= Buttons.Skill;
            if (Input.GetKey(KeyCode.Semicolon) || Input.GetKey(KeyCode.Keypad4)) b |= Buttons.Grab;
            if (Input.GetKey(KeyCode.Quote) || Input.GetKey(KeyCode.Keypad5)) b |= Buttons.Guard;
            return new InputFrame(b);
        }

        // ------------------------------------------------------------------ hiển thị
        static Vector3 ToWorld(int x, int y) =>
            new(Mathf.Round(x / (float)FightSim.Sub) / Ppu, Mathf.Round(y / (float)FightSim.Sub) / Ppu, 0);

        void Render()
        {
            for (int i = 0; i < 2; i++)
            {
                var f = sim.Fighter(i);
                var r = fighterRenderers[i];
                r.sprite = ChooseSprite(i, f) ?? sim.Fighters[i].idle;
                r.flipX = f.facing < 0;
                var pos = ToWorld(f.x, f.y);
                if (sim.Fighters[i].moves?.IsValid != true && f.action == FighterAction.Walk && f.heldProp < 0 && (sim.State.tick / 8) % 2 == 1)
                    pos.y += 1f / Ppu;                                   // nhún 1 px khi đi (chưa có clip đi)
                // PixelPack chưa có clip bị đánh / ngã → tạm: rung khi choáng, nằm (xoay 90° – vẫn pixel-perfect) khi ngã
                bool hasMoves = sim.Fighters[i].moves != null && sim.Fighters[i].moves.IsValid;
                if (!hasMoves && f.action == FighterAction.Hitstun && f.Grounded && f.stunTicks % 4 < 2)
                    pos.x -= f.facing * 1f / Ppu;
                r.transform.rotation = !hasMoves && f.action == FighterAction.Knockdown
                    ? Quaternion.Euler(0, 0, f.facing > 0 ? 90 : -90)
                    : Quaternion.identity;
                r.transform.position = pos;
                Color c = Color.white;
                if (f.hitFlash > 0 && f.hitFlash % 4 < 2) c = new Color(1f, 0.45f, 0.45f);
                else if (f.action == FighterAction.Blockstun || f.blocking) c = new Color(0.75f, 0.85f, 1f);
                else if (f.action == FighterAction.Getup && f.stunTicks % 4 < 2) c = new Color(1, 1, 1, 0.5f);
                r.color = c;
            }

            RenderEffects();
            RenderHazard();
            RenderCables();
            RenderStatusOverlays();
            RenderOutlines();
            RenderVfx();
            if (ui && Playing) ui.UpdateHud(sim, sim.TrainingMode);
            if (darkRenderer)
            {
                float a = Mathf.Clamp01(sim.State.darkTicks / 20f) * 0.65f;
                darkRenderer.color = new Color(0.02f, 0.02f, 0.06f, a);
            }

            for (int i = 0; i < propRenderers.Length; i++)
            {
                var p = sim.State.props[i];
                var d = sim.Props[p.dataIndex];
                var r = propRenderers[i];
                r.enabled = p.phase != PropPhase.Held;                  // khi cầm: vật đã vẽ sẵn trong clip tương tác
                r.sprite = p.phase == PropPhase.Broken && d.damagedSprite ? d.damagedSprite : d.sprite;
                r.flipX = p.facing < 0;
                r.sortingOrder = p.phase == PropPhase.Flying ? 20 : 0;
                r.transform.position = ToWorld(p.x, p.y);
                r.transform.localScale = Vector3.one * d.floorScale;
            }

            if (cam)
            {
                float viewHalf = RefWidth / 2f / Ppu;
                float limit = Mathf.Max(0, stageHalfWidth - viewHalf);
                float mid = (sim.State.p0.x + sim.State.p1.x) / 2f / FightSim.Sub / Ppu;
                float cx = Mathf.Clamp(mid, -limit, limit);
                float cy = stageBottomY + RefHeight / 2f / Ppu;
                cam.transform.position = new Vector3(Mathf.Round(cx * Ppu) / Ppu, Mathf.Round(cy * Ppu) / Ppu, -10);
            }
        }

        /// <summary>Đạn / bẫy / vùng: vùng rộng được lát nhiều sprite hiệu ứng dọc theo chiều ngang.</summary>
        void RenderEffects()
        {
            var es = sim.State.effects;
            for (int i = 0; i < effectViews.Length; i++)
            {
                var views = effectViews[i];
                var e = es[i];
                int need = 0;
                EffectSpec s = null;
                Sprite[] frames = null;
                SkillData worldSkill = e.active && e.kind==EffectKind.Skill ? sim.Fighters[e.owner].skills[e.skill] : null;
                if (e.active)
                {
                    s = sim.SpecOf(e);
                    frames = e.kind switch
                    {
                        EffectKind.Skill => s.frames,
                        EffectKind.Bolt => BoltFrames(e.owner) ?? fx?.electric,
                        EffectKind.Explosion => fx?.explosion,
                        EffectKind.Puddle => fx?.puddle,
                        EffectKind.FireZone => fx?.fire,
                        _ => fx?.electric,
                    };
                    if (worldSkill != null && worldSkill.worldObject && !e.field)
                        need = worldSkill.cableSegment ? 0 : 1;
                    else if (frames != null && frames.Length > 0)
                    {
                        float spriteW = Mathf.Max(8f, frames[0].rect.width * s.drawScale);
                        need = e.kind == EffectKind.Explosion ? 1 : Mathf.Clamp(Mathf.CeilToInt(s.halfW * 2 / spriteW), 1, 24);
                    }
                }
                int impactTick = e.age - e.scaffoldImpactAge;
                bool collapsing = e.active && e.scaffoldImpactAge > 0 && fx && fx.scaffoldBreak.Length > 0;
                if (collapsing && impactTick < 22 && fx.scaffoldContact.Length > 0) need = 2;
                while (views.Count < need) views.Add(NewRenderer($"Effect_{i}_{views.Count}", null, 15));
                for (int k = 0; k < views.Count; k++)
                {
                    var r = views[k];
                    r.enabled = k < need;
                    if (!r.enabled) continue;
                    r.sortingOrder = collapsing && k == 1 ? 16 : 15;
                    if(worldSkill != null && worldSkill.worldObject && !e.field) {
                        if (collapsing) {
                            bool dust = k == 1;
                            var sequence = dust ? fx.scaffoldContact : fx.scaffoldBreak;
                            int index = dust ? impactTick * sequence.Length / 22
                                : impactTick * sequence.Length / Mathf.Max(1, e.lifetime - e.scaffoldImpactAge);
                            r.sprite = sequence[Mathf.Min(index, sequence.Length - 1)];
                            r.transform.localScale = Vector3.one * (2f * s.halfH / worldSkill.worldObject.rect.height);
                            r.transform.position = dust ? ToWorld(e.scaffoldImpactX, e.scaffoldImpactY)
                                : ToWorld(e.x, e.y - s.halfH * FightSim.Sub);
                            r.flipX = false;
                            r.color = Color.white;
                            continue;
                        }
                        r.sprite=worldSkill.worldObject;
                        float w=worldSkill.dropsFromAbove ? s.halfW*2f : Mathf.Clamp(s.halfW*2f,12,80);
                        r.transform.localScale=Vector3.one*(w/r.sprite.rect.width);
                        if (worldSkill.dropsFromAbove && r.sprite.name == "scaffold")
                            r.transform.localScale = Vector3.one * (2f * s.halfH / r.sprite.rect.height);
                        r.transform.position=ToWorld(e.x,e.y)-Vector3.Scale(r.sprite.bounds.center,r.transform.localScale);
                        r.flipX=e.vx<0; r.color=Color.white;
                        continue;
                    }
                    r.sprite = e.kind == EffectKind.Explosion
                        ? frames[Mathf.Min(frames.Length - 1, e.age * frames.Length / Mathf.Max(1, e.lifetime))]   // nổ: chạy 1 lượt
                        : frames[(e.age / (e.kind == EffectKind.Puddle ? 10 : 4) + k) % frames.Length];
                    r.flipX = e.vx < 0;
                    float spanPx = s.halfW * 2f;
                    float offPx = need == 1 ? 0 : -spanPx / 2 + spanPx * (k + 0.5f) / need;
                    var p = ToWorld(e.x, e.y - s.halfH * FightSim.Sub);   // vẽ từ đáy hộp
                    p.x += Mathf.Round(offPx) / Ppu;
                    r.transform.position = p;
                    r.transform.localScale = Vector3.one * s.drawScale;
                    r.color = e.field ? new Color(1, 1, 1, 0.85f) : Color.white;
                }
            }
        }


        // ------------------------------------------------------------------ mối nguy môi trường (Hazards/<stage>/hazard.png)
        void BuildHazardViews(StageData stage, bool preview)
        {
            var art = stage.hazardArt;
            bool has = stage.hazardKind != HazardKind.None && MoveSet.Ok(art);
            int order = stage.hazardKind switch
            {
                HazardKind.Motorbike => 12,   // xe chạy trước mặt nhân vật
                HazardKind.Crane => 16,       // gạch rơi đè lên người
                _ => -5,                      // bếp, tủ điện: đồ vật phía sau nhân vật
            };
            hazardRenderer = has ? NewRenderer("Hazard_" + stage.id, null, order) : null;
            hazardWarnRenderer = null;
            if (has && stage.hazardKind == HazardKind.Motorbike)
            {
                // biển báo "!" cắt từ frame 0 (vẽ cạnh xe) để hiện ở mép màn hình phía xe sắp lao vào
                var f0 = art.frames[0];
                var r = f0.rect;
                hazardWarnSprite = stage.warningFrames?.Length > 0 ? stage.warningFrames[0]
                    : Sprite.Create(f0.texture, new Rect(r.x + 166, r.y + 80, 44, 42), new Vector2(0.5f, 0f), Ppu);
                hazardWarnRenderer = NewRenderer("HazardWarning", hazardWarnSprite, 40);
                hazardWarnRenderer.enabled = false;
            }
            rainRenderers = Array.Empty<SpriteRenderer>();
            if (stage.rainOverlay && stage.hazardKind == HazardKind.PowerBox && !preview)
            {
                rainRenderers = new SpriteRenderer[4 * 4];
                for (int i = 0; i < rainRenderers.Length; i++)
                {
                    rainRenderers[i] = NewRenderer("Rain_" + i, stage.rainOverlay, 25);
                    rainRenderers[i].enabled = false;
                }
            }
        }

        void RenderHazard()
        {
            var stage = stages[stageIndex];
            if (hazardRenderer)
            {
                var v = sim.ViewHazard();
                var art = stage.hazardArt;
                hazardRenderer.enabled = v.frame >= 0 && v.frame < art.frames.Length;
                if (hazardRenderer.enabled)
                {
                    hazardRenderer.sprite = art.frames[v.frame];
                    hazardRenderer.flipX = v.dir < 0;
                    // bếp / tủ điện: tâm vật nằm lệch phải pivot trong frame 224×176 → dời để tâm vật trùng vị trí mối nguy
                    int offPx = stage.hazardKind switch { HazardKind.Burner => 30, HazardKind.PowerBox => 15, _ => 0 };
                    hazardRenderer.transform.position = ToWorld(v.x - offPx * FightSim.Sub, 0);
                }
                if (hazardWarnRenderer)
                {
                    bool animated = stage.warningFrames?.Length > 1;
                    hazardWarnRenderer.enabled = v.warn && (animated || (sim.State.tick / 8) % 2 == 0);
                    if (v.warn && animated)
                        hazardWarnRenderer.sprite = stage.warningFrames[(sim.State.tick / 8) % stage.warningFrames.Length];
                    if (hazardWarnRenderer.enabled)
                    {
                        float camX = cam ? cam.transform.position.x : 0f;
                        float x = camX + v.warnX * (RefWidth / 2f - 28) / Ppu;
                        hazardWarnRenderer.transform.position = new Vector3(Mathf.Round(x * Ppu) / Ppu, 70f / Ppu, 0);
                    }
                }
            }
            if (rainRenderers.Length > 0 && cam)
            {
                float level = sim.RainLevel;
                var rs = stage.rainOverlay.bounds.size;
                float t = Time.time;
                var c = cam.transform.position;
                float ox = Mathf.Repeat(-t * 1.2f, rs.x), oy = Mathf.Repeat(-t * 6f, rs.y);
                for (int i = 0; i < rainRenderers.Length; i++)
                {
                    var r = rainRenderers[i];
                    r.enabled = level > 0.01f;
                    if (!r.enabled) continue;
                    int gx = i % 4, gy = i / 4;
                    float x = c.x - RefWidth / 2f / Ppu + (gx - 1) * rs.x + ox;
                    float y = c.y - RefHeight / 2f / Ppu + (gy - 1) * rs.y + oy;
                    r.transform.position = new Vector3(Mathf.Round(x * Ppu) / Ppu, Mathf.Round(y * Ppu) / Ppu, 0)
                                           + rs / 2f - r.sprite.bounds.center;
                    r.color = new Color(1, 1, 1, 0.55f * level);
                }
            }
        }

        void RenderCables()
        {
            for(int owner=0;owner<2;owner++)
            {
                var line=cableLines[owner]; var tip=cableTips[owner];
                line.enabled=tip.enabled=false;
                var caster=sim.Fighter(owner); var target=sim.Fighter(1-owner);
                SkillData cable=null;
                Vector3 end=default;
                if(target.pullTicks>0) {
                    foreach(var skill in sim.Fighters[owner].skills)
                        if(skill.cableSegment) { cable=skill; break; }
                    end=ToWorld(target.x,target.y+sim.Fighters[1-owner].hurtHeight*FightSim.Sub/2);
                }
                if(cable==null) foreach(var e in sim.State.effects) {
                    if(!e.active || e.kind!=EffectKind.Skill || e.owner!=owner)continue;
                    var skill=sim.Fighters[owner].skills[e.skill];
                    if(!skill.cableSegment)continue;
                    cable=skill; end=ToWorld(e.x,e.y); break;
                }
                if(cable==null)continue;
                var grip = new Vector2(25,43);
                if(sim.CurrentSkill(owner)==cable && cable.cableGrips!=null) {
                    var visual=caster.Grounded?cable.groundCastArt:caster.vy>0?cable.risingArt:cable.fallingArt;
                    var anchors=caster.Grounded?cable.cableGrips.ground:caster.vy>0?cable.cableGrips.rise:cable.cableGrips.fall;
                    if(MoveSet.Ok(visual) && anchors!=null && anchors.Length>0) {
                        int at=caster.actionTick*visual.lengthTicks/Mathf.Max(1,cable.clip.lengthTicks);
                        grip=anchors[Mathf.Clamp(visual.FrameIndexAt(at),0,anchors.Length-1)];
                    }
                }
                var start=ToWorld(caster.x+caster.facing*Mathf.RoundToInt(grip.x*FightSim.Sub),
                    caster.y+Mathf.RoundToInt(grip.y*FightSim.Sub));
                var delta=end-start; if(delta.sqrMagnitude<.00001f)continue;
                line.sprite=cable.cableSegment; tip.sprite=cable.worldObject;
                line.enabled=tip.enabled=true;
                line.transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
                line.transform.localScale=new Vector3(delta.magnitude/line.sprite.bounds.size.x,
                    3f/Ppu/line.sprite.bounds.size.y,1);
                line.transform.position=(end+start)*.5f-line.transform.TransformVector(line.sprite.bounds.center);
                tip.transform.rotation=line.transform.rotation;
                tip.transform.localScale=Vector3.one*(14f/tip.sprite.rect.width);
                tip.transform.position=end-tip.transform.TransformVector(tip.sprite.bounds.center);
            }
        }

        // ------------------------------------------------------------------ hiệu ứng va chạm (CombatFX)
        void SpawnVfx(in SimEvent ev)
        {
            if (!fx) return;
            var pos = ToWorld(ev.x, ev.y);
            var up = Vector3.up;
            switch (ev.kind)
            {
                case SimEventKind.DoubleJump: Anim(fx.shockRing, pos, 0.38f, 0.16f, new Color(0.75f, 0.9f, 1f)); break;
                case SimEventKind.HitLight:
                case SimEventKind.HitHeavy:
                    bool heavy = ev.kind == SimEventKind.HitHeavy;
                    bool special = false;
                    if ((ev.hitFlags & HitFlags.Fire) != 0) special |= HitVfx(fx.fireHit, ev.actor, 35);
                    if ((ev.hitFlags & HitFlags.Electric) != 0) special |= HitVfx(fx.electricHit, ev.actor, 48);
                    if ((ev.hitFlags & HitFlags.Launch) != 0) special |= HitVfx(fx.launchHit, ev.actor, 28);
                    if ((ev.hitFlags & HitFlags.Stun) != 0 && ev.actor >= 0)
                        special |= HitVfx(fx.stunStars, ev.actor, sim.Fighters[ev.actor].hurtHeight + 12);
                    if (!special) Anim(heavy ? fx.sparkHeavy : fx.sparkLight, pos, heavy ? 0.6f : 0.45f, heavy ? 0.25f : 0.18f);
                    if (heavy) Text(fx.bop, pos + up * 0.45f, 0.55f);
                    break;
                case SimEventKind.Block: Anim(fx.sparkLight, pos, 0.35f, 0.15f, new Color(0.6f, 0.8f, 1f)); break;
                case SimEventKind.GuardBreak: Anim(fx.shockRing, pos, 0.7f, 0.3f, new Color(0.7f, 0.85f, 1f)); break;
                case SimEventKind.Explosion:
                    Anim(fx.shockRing, pos, 1.4f, 0.35f);
                    Anim(fx.smoke, pos + up * 0.3f, 1.1f, 0.7f);
                    break;
                case SimEventKind.PropBreak:
                {
                    var prop = ev.prop >= 0 && ev.prop < sim.Props.Length ? sim.Props[sim.State.props[ev.prop].dataIndex] : null;
                    if (prop && prop.debris != null)
                        foreach (var piece in prop.debris)
                        {
                            var v = Make(new[] { piece }, pos, 1f, 0.8f);
                            v.vx = (float)(vfxRandom.NextDouble() * 2.4 - 1.2) + ev.dir * 0.6f;
                            v.vy = 2.2f + (float)vfxRandom.NextDouble() * 1.6f;
                            v.gravity = 9f;
                            v.spin = (float)(vfxRandom.NextDouble() * 720 - 360);
                        }
                    Text(fx.rac, pos + up * 0.5f, 0.5f);
                    break;
                }
                case SimEventKind.Land: Anim(fx.landingDust, pos, 0.35f, 0.25f); break;
                case SimEventKind.Knockdown: Anim(fx.landingDust, pos, 0.6f, 0.3f); break;
            }
        }

        bool HitVfx(SpriteClip clip, int target, int heightPx)
        {
            if (target < 0 || !MoveSet.Ok(clip)) return false;
            // Đòn liên hoàn làm mới cùng hiệu ứng trên cùng người.
            var v = vfx.Find(item => item.hitClip == clip && item.target == target);
            if (v == null) v = Make(clip.frames, Vector3.zero, 1f, clip.lengthTicks / (float)FightSim.TickRate);
            v.hitClip = clip;
            v.target = target;
            v.heightPx = heightPx;
            v.t0 = Time.time;
            return true;
        }

        Vfx Make(Sprite[] frames, Vector3 pos, float scale, float dur)
        {
            var r = vfxPool.Count > 0 ? vfxPool.Pop() : NewRenderer("Vfx", null, 30);
            r.enabled = true;
            var v = new Vfx { r = r, frames = frames, pos = pos, scale = scale, dur = dur, t0 = Time.time };
            vfx.Add(v);
            return v;
        }

        void Anim(Sprite[] frames, Vector3 pos, float scale, float dur, Color? tint = null)
        {
            if (frames == null || frames.Length == 0) return;
            Make(frames, pos, scale, dur).tint = tint ?? Color.white;
        }

        void Text(Sprite sprite, Vector3 pos, float scale)
        {
            if (!sprite) return;
            var v = Make(new[] { sprite }, pos, scale, 0.5f);
            v.text = true;
            v.vy = 0.6f;
        }

        void RenderVfx()
        {
            float now = Time.time;
            for (int i = vfx.Count - 1; i >= 0; i--)
            {
                var v = vfx[i];
                float k = (now - v.t0) / v.dur;
                if (k >= 1f || !v.r)
                {
                    if (v.r) { v.r.enabled = false; vfxPool.Push(v.r); }
                    vfx.RemoveAt(i);
                    continue;
                }
                float dt = now - v.t0;
                v.r.sprite = v.hitClip != null ? v.hitClip.FrameAt((int)(dt * FightSim.TickRate))
                    : v.frames[Mathf.Min(v.frames.Length - 1, (int)(k * v.frames.Length))];
                var p = v.pos + new Vector3(v.vx * dt, v.vy * dt - 0.5f * v.gravity * dt * dt, 0);
                if (v.hitClip != null)
                {
                    ref var fighter = ref sim.Fighter(v.target);
                    p = ToWorld(fighter.x, fighter.y + v.heightPx * FightSim.Sub);
                }
                v.r.transform.position = new Vector3(Mathf.Round(p.x * Ppu) / Ppu, Mathf.Round(p.y * Ppu) / Ppu, 0);
                v.r.transform.rotation = Quaternion.Euler(0, 0, v.spin * dt);
                // chữ truyện tranh: bật to rồi thu lại (Asset Bible mục 7)
                float s = v.text ? v.scale * (k < 0.15f ? Mathf.Lerp(0.4f, 1.25f, k / 0.15f) : Mathf.Lerp(1.25f, 1f, (k - 0.15f) / 0.85f)) : v.scale;
                v.r.transform.localScale = Vector3.one * s;
                var c = v.tint;
                c.a = v.text || v.gravity > 0 ? 1f - Mathf.Max(0, k - 0.6f) / 0.4f : 1f;
                v.r.color = c;
            }
        }

        /// <summary>Viền sáng nhấp nháy trên vật gần nhất người chơi nhặt được (GDD mục 1).</summary>
        void RenderOutlines()
        {
            int n0 = Playing ? sim.NearestPickable(0) : -1, n1 = Playing ? sim.NearestPickable(1) : -1;
            float pulse = 0.55f + 0.45f * Mathf.Sin(Time.time * 8f);
            for (int i = 0; i < propOutlines.Length; i++)
            {
                var r = propOutlines[i];
                var p = sim.State.props[i];
                r.enabled = r.sprite && (i == n0 || i == n1) && p.phase == PropPhase.Floor;
                if (!r.enabled) continue;
                var pos = ToWorld(p.x, p.y);
                pos.y -= 1f / Ppu;                                     // viền rộng hơn sprite 1 px mỗi bên
                r.transform.position = pos;
                r.color = new Color(1, 1, 1, pulse);
            }
        }

        /// <summary>Lửa / tia điện vẽ chồng lên vật dụng đang cháy / nhiễm điện và người đang bỏng / bị giật.</summary>
        void RenderStatusOverlays()
        {
            int t = sim.State.tick;
            for (int i = 0; i < propOverlays.Length; i++)
            {
                var p = sim.State.props[i];
                var r = propOverlays[i];
                Sprite[] frames = p.phase == PropPhase.Broken ? null
                    : p.burnTicks > 0 ? fx?.fire
                    : p.shockTicks > 0 && (t / 3) % 3 != 0 ? fx?.electric : null;
                r.enabled = frames != null && frames.Length > 0;
                if (!r.enabled) continue;
                r.sprite = frames[(t / 4 + i) % frames.Length];
                var pos = ToWorld(p.x, p.y);
                if (p.phase == PropPhase.Held && p.owner >= 0)
                {
                    var f = sim.Fighter(p.owner);                       // vật đang cầm: vẽ ở tầm tay
                    pos = ToWorld(f.x + f.facing * 18 * FightSim.Sub, f.y + 50 * FightSim.Sub);
                }
                r.transform.position = pos;
                r.transform.localScale = Vector3.one * 0.75f;
            }
            for (int i = 0; i < 2; i++)
            {
                var f = sim.Fighter(i);
                var r = fighterOverlays[i];
                Sprite[] frames = f.shockFx > 0 ? fx?.electric : f.burnTicks > 0 ? fx?.fire : null;
                r.enabled = frames != null && frames.Length > 0;
                if (!r.enabled) continue;
                r.sprite = frames[(t / 4) % frames.Length];
                r.transform.position = ToWorld(f.x, f.y + 30 * FightSim.Sub);
                r.transform.localScale = Vector3.one * (f.shockFx > 0 ? 1.5f : 0.8f);
                r.color = new Color(1, 1, 1, f.shockFx > 0 ? 0.9f : 0.75f);
            }
            // hào quang gồng nộ: cùng khung + pivot với clip nhân vật → đặt đúng vị trí chân, lật theo hướng
            for (int i = 0; i < 2; i++)
            {
                var f = sim.Fighter(i);
                var r = rageAuras[i];
                var m = sim.Fighters[i].moves;
                SpriteClip clip = null;
                int k = 0;
                if (f.action == FighterAction.Charge && m != null)
                {
                    int phase = FightSim.ChargePhase(sim.Fighters[i], f, out k);
                    clip = phase == 0 ? m.rageFxStart : phase == 1 ? m.rageFxLoop : m.rageFxEnd;
                }
                r.enabled = MoveSet.Ok(clip);
                if (!r.enabled) continue;
                r.sprite = clip.FrameAt(k);
                r.flipX = f.facing < 0;
                r.transform.position = ToWorld(f.x, f.y);
            }
        }

        /// <summary>Tia do bẫy bắn ra dùng hình của chính bẫy (Cọc Tiếp Địa).</summary>
        Sprite[] BoltFrames(int owner)
        {
            foreach (var sk in sim.Fighters[owner].skills)
                if (sk.effectSpec != null && sk.effectSpec.emitInterval > 0 && sk.effectSpec.frames.Length > 0)
                    return sk.effectSpec.frames;
            return null;
        }

        /// <summary>Chọn sprite theo trạng thái mô phỏng: clip chiêu / tương tác / động tác chung (BasicActions).</summary>
        Sprite ChooseSprite(int idx, in FighterState f)
        {
            var data = sim.Fighters[idx];
            var m = data.moves;
            bool has = m != null && m.IsValid;
            int t = sim.State.tick;
            Sprite Play(SpriteClip c, int tick) => MoveSet.Ok(c) ? c.FrameAt(tick) : null;
            Sprite Last(SpriteClip c) => MoveSet.Ok(c) ? c.frames[c.frames.Length - 1] : null;

            // vào trận / kết thúc trận
            if (sim.InIntro && has) return Play(m.intro, t) ?? Play(m.idle, t);
            if (sim.RoundOver && has && f.hp > 0 && t - sim.State.koTick > FightSim.KoPauseTicks)
            {
                var c = sim.State.winner == idx ? m.win : m.lose;
                int k = t - sim.State.koTick - FightSim.KoPauseTicks;
                return k < c.lengthTicks ? Play(c, k) : Last(c);
            }

            switch (f.action)
            {
                case FighterAction.Skill:
                {
                    var skill=sim.CurrentSkill(idx);
                    if(skill==null)return null;
                    SpriteClip visual=null;
                    if(f.normal && !f.Grounded && f.vy<0) {
                        if(f.skillIndex==FighterData.AirLight) visual=m?.fallingPunch;
                        if(f.skillIndex==FighterData.AirHeavy) visual=m?.fallingKick;
                    } else if(!f.normal) {
                        visual=f.Grounded?skill.groundCastArt:(f.vy>0?skill.risingArt:skill.fallingArt);
                        // chưa vẽ bản trên không: ưu tiên bản dưới đất không kèm vật thể (giàn giáo/dây không bị vẽ 2 lần)
                        if(!MoveSet.Ok(visual) && (skill.worldObject || skill.cableSegment)) visual=skill.groundCastArt;
                    }
                    if(MoveSet.Ok(visual))
                        return visual.FrameAt(Mathf.Clamp(f.actionTick*visual.lengthTicks/Mathf.Max(1,skill.clip.lengthTicks),0,visual.lengthTicks-1));
                    return skill.clip.FrameAt(f.actionTick);
                }
                case FighterAction.Pickup:
                    return sim.ActionInteraction(idx)?.pickup?.FrameAt(f.actionTick);
                case FighterAction.Swing:
                    return sim.ActionInteraction(idx)?.swing?.FrameAt(f.actionTick);
                case FighterAction.Push:
                    return sim.ActionInteraction(idx)?.push?.FrameAt(f.actionTick);
                case FighterAction.Toss:
                {
                    var it = sim.ActionInteraction(idx);
                    if (it?.toss == null) return null;
                    // từ frame thả tay, clip "nem" vẽ sẵn vật đang bay → chuyển về tư thế đứng, vật thật do mô phỏng vẽ
                    return f.actionTick < it.toss.TickOfFrame(it.releaseFrame) ? it.toss.FrameAt(f.actionTick) : Play(m?.idle, t) ?? data.idle;
                }
            }
            if (has)
            {
                switch (f.action)
                {
                    case FighterAction.Roll:
                        return Play(f.rollDir == f.facing ? m.rollFwd : m.rollBack, f.actionTick);
                    case FighterAction.Blockstun:
                        return Play(m.blockHit, f.actionTick);
                    case FighterAction.Hitstun:
                        if(f.pullTicks>0) return Play(f.pulledForward?m.pulledFront:m.pulledBack,10-f.pullTicks)
                                                ?? Play(m.pulledFront,10-f.pullTicks) ?? Play(m.hitHeavy,f.actionTick);
                        if(f.reeled && f.pulledForward && MoveSet.Ok(m.pulledRecover)) {   // vừa bị kéo tới: loạng choạng
                            int k=f.actionTick-10;
                            if(k>=0 && k<m.pulledRecover.lengthTicks) return Play(m.pulledRecover,k);
                            if(k>=0) return Last(m.pulledRecover);
                        }
                        if (f.dizzy && f.Grounded) return Play(m.stun, f.actionTick);
                        if (!f.Grounded || f.hitHeavy)
                            return f.actionTick < m.hitHeavy.lengthTicks ? Play(m.hitHeavy, f.actionTick) : Last(m.hitHeavy);
                        return f.actionTick < m.hitLight.lengthTicks ? Play(m.hitLight, f.actionTick) : Last(m.hitLight);
                    case FighterAction.Knockdown:
                        return f.actionTick < m.knockdown.lengthTicks ? Play(m.knockdown, f.actionTick) : Last(m.knockdown);
                    case FighterAction.Getup:
                        return Play(m.getup, f.actionTick);
                    case FighterAction.Charge:
                    {
                        int phase = FightSim.ChargePhase(data, f, out int k);
                        var c = phase == 0 ? m.rageStart : phase == 1 ? m.rageLoop : m.rageEnd;
                        return Play(c, k) ?? (phase == 1 ? Play(m.crouchHold, t) : Play(m.idle, t));
                    }
                }
            }
            if (f.heldProp >= 0)
            {
                var held = sim.Props[sim.State.props[f.heldProp].dataIndex];
                return data.FindInteraction(held.id)?.hold?.FrameAt(t);
            }
            if (!has) return data.idle;
            if (!f.Grounded && f.doubleJumpUsed && MoveSet.Ok(m.doubleJump) && t - f.doubleJumpTick < m.doubleJump.lengthTicks)
                return Play(m.doubleJump, t - f.doubleJumpTick);           // lộn người khi nhảy lần 2
            if (!f.Grounded) return f.vy > data.jumpVelocity / 2 ? Play(m.jumpStart, Mathf.Max(0, (data.jumpVelocity - f.vy) / FightSim.Gravity)) : Play(m.jumpAir, t);
            if (f.blocking) return f.crouching ? Play(m.crouchHold, t) : Play(m.block, t);
            if (f.crouching) return Play(m.crouchHold, t);
            if (f.action == FighterAction.Walk)
                return f.vx * f.facing >= 0 ? Play(m.walkFwd, t) : Play(m.walkBack, t);
            int sinceLand = t - f.landTick;
            if (f.landTick > 0 && sinceLand >= 0 && sinceLand < m.jumpLand.lengthTicks) return Play(m.jumpLand, sinceLand);
            return Play(m.idle, t);
        }

        // ------------------------------------------------------------------ HUD
        GUIStyle style;

        void OnGUI()
        {
            if (sim == null || !Playing) return;
            if (ui)
            {
                if (showHitboxes) DrawHitboxes();                        // HUD do GameUI vẽ; OnGUI chỉ còn khung debug
                return;
            }
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 16, richText = true };
            float s = Screen.height / 1080f;
            var m = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1));
            float w = Screen.width / s;
            DrawBar(0, new Rect(30, 24, 640, 22), false);
            DrawBar(1, new Rect(w - 670, 24, 640, 22), true);
            var stage = stages[stageIndex];
            GUI.Label(new Rect(w / 2 - 300, 52, 600, 24),
                $"<b>{stage.displayName}</b>  ·  tick {sim.State.tick}{(sim.TrainingMode ? "  ·  luyện tập (năng lượng vô hạn)" : "")}", Center());
            GUI.Label(new Rect(30, 1080 - 92, 1900, 80),
                "<b>P1</b> A/D đi · W nhảy · S cúi · J đánh nhẹ (J-J-J) · K đánh mạnh · I đỡ (+A/D lăn) · L kỹ năng (+W/S/hướng tới, K+L tuyệt chiêu) · U nhặt/ném\n" +
                "<b>P2</b> ←/→ · ↑ · ↓ · , nhẹ · . mạnh · ' đỡ · / kỹ năng (.+/ tuyệt chiêu) · ; nhặt/ném   ·   " +
                "F1/F2 nhân vật · F3 stage · F4 hitbox · F5 chơi lại · F6 luyện tập", style);
            if (sim.InIntro || sim.RoundOver)
            {
                var big = new GUIStyle(style) { fontSize = 72, alignment = TextAnchor.MiddleCenter };
                string msg = sim.InIntro
                    ? (sim.State.tick < sim.IntroTicks - 20 ? "<b>SẴN SÀNG…</b>" : "<b><color=#FFD23F>ĐÁNH!</color></b>")
                    : sim.State.winner < 0 ? "<b>HOÀ</b>"
                    : $"<b><color=#FF3CAC>K.O.</color></b>\n<size=34>{sim.Fighters[sim.State.winner].displayName} thắng · F5 đấu lại</size>";
                GUI.Label(new Rect(0, 360, w, 260), msg, big);
            }
            GUI.matrix = m;
            if (showHitboxes) DrawHitboxes();
        }

        // ---- F4: khung va chạm (vẽ trên màn hình, không ảnh hưởng mô phỏng)
        void DrawHitboxes()
        {
            if (!cam) return;
            for (int i = 0; i < 2; i++)
            {
                var hb = sim.HurtBox(i);
                DrawBox(hb, new Color(0.2f, 1f, 0.4f), sim.Fighter(i).invuln > 0);
                var s = sim.CurrentSkill(i);
                if (s == null) continue;
                var f = sim.Fighter(i);
                for (int w = 0; w < s.hits.Length; w++)
                {
                    bool active = sim.IsWindowActive(i, w);
                    if (!active && f.actionTick > s.hits[w].endTick) continue;
                    DrawBox(FightSim.WindowBox(f, s.hits[w]), active ? new Color(1f, 0.15f, 0.15f) : new Color(1f, 0.6f, 0.6f, 0.35f), false);
                }
            }
            foreach (var e in sim.State.effects)
                if (e.active) DrawBox(FightSim.EffectBox(e, sim.SpecOf(e)), new Color(1f, 0.6f, 0.1f), false);
        }

        void DrawBox(Box b, Color c, bool dashed)
        {
            Vector2 S(int x, int y)
            {
                var p = cam.WorldToScreenPoint(new Vector3(x / (float)FightSim.Sub / Ppu, y / (float)FightSim.Sub / Ppu, 0));
                return new Vector2(p.x, Screen.height - p.y);
            }
            var a = S(b.x0, b.y1);
            var z = S(b.x1, b.y0);
            var r = Rect.MinMaxRect(a.x, a.y, z.x, z.y);
            GUI.color = new Color(c.r, c.g, c.b, c.a * 0.18f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = dashed && (Time.frameCount / 6) % 2 == 0 ? new Color(c.r, c.g, c.b, 0.3f) : c;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - 2, r.width, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, 2, r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - 2, r.y, 2, r.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        GUIStyle Center() => new(style) { alignment = TextAnchor.MiddleCenter };

        void DrawBar(int i, Rect r, bool right)
        {
            var f = sim.Fighter(i);
            var d = sim.Fighters[i];
            float t = Mathf.Clamp01(f.hp / (float)d.maxHp);
            GUI.color = new Color(0, 0, 0, 0.6f);
            GUI.DrawTexture(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), Texture2D.whiteTexture);
            GUI.color = d.color;
            float fw = r.width * t;
            GUI.DrawTexture(new Rect(right ? r.xMax - fw : r.x, r.y, fw, r.height), Texture2D.whiteTexture);
            // năng lượng (4 vạch) + thanh đỡ
            SmallBar(new Rect(r.x, r.yMax + 4, r.width * 0.6f, 8), f.energy / 100f, new Color(1f, 0.8f, 0.2f), right, r, 4);
            SmallBar(new Rect(r.x, r.yMax + 14, r.width * 0.3f, 5), f.guard / (float)FightSim.GuardMax, new Color(0.5f, 0.8f, 1f), right, r, 0);
            GUI.color = Color.white;
            string action = f.action switch
            {
                FighterAction.Skill when sim.CurrentSkill(i) != null => sim.CurrentSkill(i).name,
                FighterAction.Roll => "lăn né",
                FighterAction.Hitstun => "choáng",
                FighterAction.Blockstun => "đỡ",
                FighterAction.Knockdown => "ngã",
                FighterAction.Getup => "đứng dậy",
                FighterAction.Charge => "gồng nộ",
                _ => f.heldProp >= 0 ? "cầm " + sim.Props[sim.State.props[f.heldProp].dataIndex].displayName : f.blocking ? "thủ" : "",
            };
            var st = new GUIStyle(style) { alignment = right ? TextAnchor.UpperRight : TextAnchor.UpperLeft };
            GUI.Label(new Rect(r.x, r.yMax + 22, r.width, 24), $"<b>P{i + 1} {d.displayName}</b>  {f.hp}/{d.maxHp}   <i>{action}</i>", st);
            // combo: hiện ở phía người đánh khi đối thủ dính ≥ 2 đòn liên tiếp
            var o = sim.Fighter(1 - i);
            if (o.comboTaken >= 2)
            {
                var big = new GUIStyle(style) { fontSize = 34, alignment = right ? TextAnchor.UpperRight : TextAnchor.UpperLeft };
                GUI.Label(new Rect(r.x, r.yMax + 50, r.width, 44), $"<b><color=#FFD23F>{o.comboTaken} HIT</color></b>  <size=18>-{o.lastDamage}</size>", big);
            }
        }

        static void SmallBar(Rect bar, float t, Color c, bool right, Rect outer, int ticks)
        {
            if (right) bar.x = outer.xMax - bar.width;
            GUI.color = new Color(0, 0, 0, 0.6f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = c;
            float fw = bar.width * Mathf.Clamp01(t);
            GUI.DrawTexture(new Rect(right ? bar.xMax - fw : bar.x, bar.y, fw, bar.height), Texture2D.whiteTexture);
            GUI.color = new Color(0, 0, 0, 0.8f);
            for (int k = 1; k < ticks; k++)
                GUI.DrawTexture(new Rect(bar.x + bar.width * k / ticks - 1, bar.y, 2, bar.height), Texture2D.whiteTexture);
        }
    }
}
