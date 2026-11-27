using System;
using LoanDau.Data;

namespace LoanDau.Core
{
    public enum FighterAction : byte
    {
        Idle, Walk, Jump, Skill, Pickup, Swing, Toss, Push,
        Hitstun, Blockstun, Knockdown, Getup, Roll,
        Charge,     // gồng nộ: giữ Đỡ + Kỹ năng, đứng yên tích năng lượng, không đỡ được đòn
    }

    /// <summary>Sự kiện cho lớp hiển thị (tia lửa, chữ BỐP, mảnh vỡ…) – không thuộc trạng thái trận.</summary>
    public enum SimEventKind : byte
    {
        HitLight, HitHeavy, Block, GuardBreak, Explosion, PropBreak, Land, Knockdown, KO, Super, RoundStart, TimeOver,
        HazardWarn,     // mối nguy sắp xảy ra (biển báo xe máy, bóng đổ cần cẩu, bếp sắp bùng, tủ điện tích điện)
        HazardStrike,   // mối nguy bắt đầu gây sát thương (xe lao vào sân, gạch chạm đất, lửa bùng, phóng điện)
        DoubleJump,
    }

    public struct SimEvent
    {
        public SimEventKind kind;
        public int x, y;          // đơn vị mô phỏng
        public int dir;           // hướng văng (+1 / -1)
        public int prop;          // PropBreak: chỉ số vật
        public int actor;         // HitLight/HitHeavy: người dính đòn (0/1); -1 = không rõ
        public HitFlags hitFlags;
    }

    public enum PropPhase : byte { Floor, Held, Flying, Broken }

    /// <summary>Trạng thái một nhân vật. Toạ độ: 1/16 pixel, y = 0 là mặt sàn, x = 0 giữa sân.</summary>
    [Serializable]
    public struct FighterState
    {
        public int x, y, vx, vy;
        public sbyte facing;              // +1 nhìn phải, -1 nhìn trái
        public FighterAction action;
        public int actionTick;
        public int skillIndex;
        public int heldProp;              // chỉ số trong MatchState.props, -1 = tay không
        public int actionProp;            // vật gắn với hành động hiện tại (nhặt/vung/ném/đẩy), -1 = không
        public int hp, energy, guard;
        public int stunTicks;             // còn bao nhiêu tick bị khống chế (choáng / đỡ / ngã / đứng dậy)
        public int invuln;                // còn bao nhiêu tick bất tử
        public int comboTaken;            // số đòn đã dính trong combo hiện tại (giảm sát thương)
        public int juggle;                // số lần bị tung lên trong combo
        public int hitMask;               // các nhịp đánh của chiêu hiện tại đã trúng (mỗi bit một nhịp)
        public bool armorUsed;
        public bool blocking, crouching;
        public int hitFlash;              // tick còn nháy đỏ khi trúng đòn
        public int lastDamage;            // sát thương đòn gần nhất (HUD)
        public int burnTicks;             // đang bỏng: mất 10 máu mỗi giây (GDD mục 4)
        public int shockFx;               // vừa bị điện giật (hiển thị)
        public bool normal;               // skillIndex trỏ vào FighterData.normals (đòn J/K) thay vì skills
        public bool hitHeavy;             // đòn vừa dính là đòn mạnh (chọn clip hit_heavy)
        public bool dizzy;                // choáng do vỡ thế đỡ / điện (clip stun)
        public int rollDir;               // lăn né: +1 / -1
        public int landTick;              // tick tiếp đất gần nhất (clip jump_land)
        public int pullTicks, pullTargetX;
        public bool pulledForward;
        public bool reeled;               // đòn vừa dính là dây kéo: hết cuộn dây thì diễn clip loạng choạng (pulled_recover)
        public bool airAttackUsed;        // mỗi lần nhảy chỉ 1 đòn trên không
        public int chargeEnd;             // gồng nộ: > 0 = đang thu thế, còn bấy nhiêu tick; 0 = còn đang giữ
        public int floorY;                // mặt đang đứng: 0 = sàn, > 0 = mặt bệ (đơn vị mô phỏng)
        public bool doubleJumpUsed;       // đã nhảy lần 2 trong lần bay này
        public int doubleJumpTick;        // tick bật nhảy lần 2 (hiển thị clip double_jump)
        public int dropTicks;             // ↓ + nhảy trên bệ: số tick còn đi xuyên bệ
        public Buttons prevButtons;
        public bool Grounded => y == floorY && vy == 0;
    }

    [Serializable]
    public struct PropState
    {
        public int dataIndex;
        public int x, y, vx, vy;
        public PropPhase phase;
        public int owner;                 // 0/1 khi đang cầm hoặc vừa ném, -1 = không ai
        public int durability;
        public sbyte facing;
        public int burnTicks;             // đang cháy (Dễ cháy, hoặc Mai ném)
        public int shockTicks;            // đang nhiễm điện (Kim loại)
        public int hardHits;              // Chất nổ: bị đòn mạnh trúng 2 lần → nổ
        public int hitCooldown;
    }

    /// <summary>Loại hiệu ứng trên sân.</summary>
    public enum EffectKind : byte
    {
        Skill,      // đạn / bẫy / vùng của chiêu
        Bolt,       // tia điện do bẫy bắn ra
        Explosion,  // vụ nổ (gây sát thương cả hai bên lúc xuất hiện)
        Puddle,     // vũng nước / sơn: trơn trượt
        FireZone,   // vùng cháy (dầu, sơn bắt lửa)
        ShockZone,  // vũng nước có điện: giật cả hai bên
    }

    /// <summary>Đạn / bẫy / vùng đang tồn tại trên sân.</summary>
    [Serializable]
    public struct EffectState
    {
        public bool active;
        public EffectKind kind;
        public bool flammable;            // vũng dễ cháy (sơn) – gặp lửa thành vùng cháy
        public int owner;                 // người tạo (0/1); -1 = môi trường (trúng cả hai bên)
        public int skill;                 // chỉ số chiêu của người tạo (kind = Skill)
        public int x, y, vx, vy;          // y = tâm hộp
        public int age, lifetime;
        public int scaffoldImpactAge, scaffoldImpactX, scaffoldImpactY;
        public bool returningCable;
        public int cooldown, emitTimer;
        public bool field;                // đã thành vùng đứng yên (vd dầu sôi tiếp đất)
    }

    /// <summary>Toàn bộ trạng thái trận – sao chép được để lưu/khôi phục (rollback, GDD mục 6).</summary>
    [Serializable]
    public struct MatchState
    {
        public int tick;
        public FighterState p0, p1;
        public PropState[] props;
        public EffectState[] effects;
        public int darkTicks;             // màn hình tối (tuyệt chiêu)
        public int koTick;                // tick kết thúc hiệp (hạ gục / hết giờ), 0 = hiệp đang diễn ra
        public int winner;                // người thắng hiệp, hợp lệ khi koTick > 0: 0 / 1, -1 = hoà
        public bool timeOver;             // hiệp kết thúc do hết giờ
        // ---- hiệp đấu (GDD mục 2: 3 hiệp, thắng 2, mỗi hiệp 99 giây)
        public int round;                 // hiệp hiện tại, từ 1
        public int wins0, wins1;
        public int roundStartTick;
        public int timer;                 // tick còn lại của hiệp
        public bool matchDecided;
        public int matchWinner;           // hợp lệ khi matchDecided: 0 / 1, -1 = hoà
        // ---- mối nguy môi trường (GDD mục 5)
        public int hazardTimer;           // tick tới lần kích hoạt kế tiếp (đang nghỉ)
        public int hazardAge;             // tick đã diễn ra; -1 = đang nghỉ
        public int hazardX, hazardDir;    // vị trí (đơn vị mô phỏng) / hướng chạy của xe máy
        public int hazardHitMask;         // bit 0/1: người đã dính mối nguy lần này
        public int hazardCount;           // số lần đã kích hoạt (đổi hướng xe / đổi mục tiêu cần cẩu)
        public int hazardCooldown;        // tủ điện: thời gian tích điện lại sau khi phóng
        public int rainTicks;             // > 0: đang mưa, sàn trơn
        public int rainTimer;             // tick tới khi đổi trời

        public MatchState Clone()
        {
            var c = this;
            c.props = (PropState[])props?.Clone();
            c.effects = (EffectState[])effects?.Clone();
            return c;
        }
    }

    /// <summary>Hộp va chạm theo đơn vị mô phỏng.</summary>
    public struct Box
    {
        public int x0, x1, y0, y1;
        public Box(int x0, int x1, int y0, int y1) { this.x0 = x0; this.x1 = x1; this.y0 = y0; this.y1 = y1; }
        public bool Overlaps(in Box o) => x0 < o.x1 && o.x0 < x1 && y0 < o.y1 && o.y0 < y1;
    }

    /// <summary>
    /// Lõi mô phỏng tất định (GDD mục 2, 6, 7): Simulate(p1, p2) mỗi tick, 60 tick/giây, số nguyên,
    /// không dùng Time.deltaTime hay PhysX. Phần hiển thị chỉ đọc State.
    /// Có: di chuyển, nhảy, đỡ (đứng/thấp), 30 chiêu với hitbox theo animation, đạn/bẫy/vùng, phản đòn,
    /// choáng/ngã/đứng dậy bất tử, vỡ thế đỡ, giảm sát thương combo, giới hạn tung, nhặt – vung – ném vật dụng.
    /// </summary>
    public sealed partial class FightSim
    {
        public const int TickRate = 60;
        public const int Sub = 16;                    // 1 pixel = 16 đơn vị mô phỏng
        public const int PixelsPerUnit = 64;

        // thông số chung (GDD mục 2)
        public const int Gravity = 6;
        public const int PickupRange = 72 * Sub;      // bán kính 1.2 m
        public const int SwingReach = 70 * Sub;
        public const int HitFlashTicks = 10;
        public const int GuardMax = 100;              // thanh đỡ; về 0 → choáng 1 giây
        public const int GuardBreakTicks = 60;
        public const int KnockdownTicks = 40;
        public const int GetupInvulnTicks = 30;       // 0.5 giây bất tử khi dậy
        public const int JuggleMax = 3;               // bị tung quá 3 lần → tự tiếp đất
        public const int LaunchVelocity = 88;
        public const int MaxEffects = 24;

        public const int RollSpeed = 5 * Sub;
        public const int DiveVx = 4 * Sub, DiveVy = 6 * Sub;  // đá bổ: lao chéo xuống
        public const int DiveBounce = 56;                    // trúng đá bổ: bật nhẹ lên
        public const int RoundTicks = 99 * TickRate;  // 99 giây
        public const int RoundEndDelay = 180;         // 3 giây sau K.O. thì sang hiệp mới
        public const int RoundsToWin = 2;
        public const int MaxRounds = 5;               // hoà liên tiếp: tối đa 5 hiệp
        public const int KoPauseTicks = 30;           // sau KO bao lâu người thắng diễn clip thắng

        public readonly FighterData[] Fighters = new FighterData[2];
        public readonly PropData[] Props;
        public readonly int MinX, MaxX;
        public bool TrainingMode = true;              // năng lượng vô hạn để thử chiêu
        [NonSerialized] public int IntroTicks;        // số tick đầu trận khoá điều khiển (clip intro); test để 0
        public MatchState State;
        /// <summary>Sự kiện hiển thị của tick vừa chạy (xoá mỗi tick, không thuộc State).</summary>
        public readonly System.Collections.Generic.List<SimEvent> Events = new();

        void Emit(SimEventKind k, int x, int y, int dir = 1, int prop = -1, int actor = -1, HitFlags hitFlags = HitFlags.None) =>
            Events.Add(new SimEvent { kind = k, x = x, y = y, dir = dir, prop = prop, actor = actor, hitFlags = hitFlags });

        public bool InIntro => State.tick - State.roundStartTick < IntroTicks;
        /// <summary>Hiệp vừa kết thúc (K.O. / hết giờ) – khoá điều khiển, người thắng diễn clip thắng.</summary>
        public bool RoundOver => State.koTick > 0;
        public bool MatchOver => State.matchDecided;
        public int Wins(int i) => i == 0 ? State.wins0 : State.wins1;
        public int SecondsLeft => (State.timer + TickRate - 1) / TickRate;
        readonly PropState[] initialProps;

        /// <summary>Tia điện do bẫy (Cọc Tiếp Địa) bắn ra.</summary>
        public static readonly EffectSpec BoltSpec = new()
        {
            enabled = true, speed = 6 * Sub, halfW = 8, halfH = 5, damage = 40, hitstun = 18, pushPx = 6,
            flags = HitFlags.Stun | HitFlags.Electric, lifetime = 90,
        };

        // ---- phản ứng môi trường (GDD mục 4)
        public const int ExplosionRadiusPx = 150;     // 2.5 m
        public const int ExplosionDamage = 150;
        public const int BurnTicks = 240;             // vật cháy 4 giây
        public const int FighterBurnTicks = 180;      // người bỏng 3 giây, 10 máu/giây
        public const int ShockTicks = 300;            // vật kim loại nhiễm điện 5 giây
        public const int ElectricStunTicks = 18;      // tê 0.3 giây

        public static readonly EffectSpec ExplosionSpec = new() { halfW = ExplosionRadiusPx, halfH = 70, lifetime = 24, drawScale = 3f };
        public static readonly EffectSpec PuddleSpec = new() { halfW = 40, halfH = 6, lifetime = 600 };
        public static readonly EffectSpec FireZoneSpec = new() { halfW = 40, halfH = 16, lifetime = 180, damage = 5, hitInterval = 30, flags = HitFlags.Fire };
        public static readonly EffectSpec ShockZoneSpec = new() { halfW = 40, halfH = 16, lifetime = 180, damage = 15, hitInterval = 30, hitstun = 18, flags = HitFlags.Electric };

        public FightSim(FighterData f0, FighterData f1, StageData stage, int halfWidthPx)
        {
            Fighters[0] = f0;
            Fighters[1] = f1;
            int half = (halfWidthPx - stage.sideMarginPx) * Sub;
            MinX = -half;
            MaxX = half;
            var spawns = stage.props ?? Array.Empty<StageData.PropSpawn>();
            Props = new PropData[spawns.Length];
            State.props = new PropState[spawns.Length];
            for (int i = 0; i < spawns.Length; i++)
            {
                Props[i] = spawns[i].prop;
                State.props[i] = new PropState
                {
                    dataIndex = i, x = spawns[i].xPx * Sub, phase = PropPhase.Floor, owner = -1,
                    durability = spawns[i].prop ? spawns[i].prop.durability : 1, facing = 1,
                };
            }
            State.effects = new EffectState[MaxEffects];
            startX = Math.Clamp(stage.playerStartPx, 20, halfWidthPx - stage.sideMarginPx) * Sub;
            State.p0 = NewFighter(f0, -startX, 1);
            State.p1 = NewFighter(f1, startX, -1);
            initialProps = (PropState[])State.props.Clone();
            State.round = 1;
            State.timer = RoundTicks;
            Platforms = BuildPlatforms(stage);
            Hazard = stage.hazardKind;
            HazardHomeX = Math.Clamp(stage.hazardXPx, -halfWidthPx, halfWidthPx) * Sub;
            stageEdge = halfWidthPx * Sub;
            ResetHazard();
        }

        readonly int startX;

        /// <summary>Sang hiệp mới: hồi máu, về vị trí, đặt lại vật dụng; giữ năng lượng và số hiệp thắng.</summary>
        void StartRound()
        {
            int e0 = State.p0.energy, e1 = State.p1.energy;
            State.p0 = NewFighter(Fighters[0], -startX, 1);
            State.p1 = NewFighter(Fighters[1], startX, -1);
            State.p0.energy = e0;
            State.p1.energy = e1;
            State.props = (PropState[])initialProps.Clone();
            State.effects = new EffectState[MaxEffects];
            State.darkTicks = 0;
            State.koTick = 0;
            State.timeOver = false;
            State.round++;
            State.roundStartTick = State.tick;
            State.timer = RoundTicks;
            ResetHazard();
            Emit(SimEventKind.RoundStart, 0, 0, 1, State.round);
        }

        void EndRound(int winner, bool timeOver)
        {
            State.koTick = Math.Max(1, State.tick);
            State.winner = winner;
            State.timeOver = timeOver;
            if (winner == 0) State.wins0++;
            else if (winner == 1) State.wins1++;
            if (State.wins0 >= RoundsToWin || State.wins1 >= RoundsToWin || State.round >= MaxRounds)
            {
                State.matchDecided = true;
                State.matchWinner = State.wins0 == State.wins1 ? -1 : State.wins0 > State.wins1 ? 0 : 1;
            }
        }

        static FighterState NewFighter(FighterData d, int x, sbyte facing) => new()
        {
            x = x, facing = facing, heldProp = -1, actionProp = -1, skillIndex = -1,
            hp = d ? d.maxHp : 1000, energy = 0, guard = GuardMax,
        };

        public ref FighterState Fighter(int i) => ref (i == 0 ? ref State.p0 : ref State.p1);

        public SkillData CurrentSkill(int i)
        {
            ref var f = ref Fighter(i);
            if (f.action != FighterAction.Skill || f.skillIndex < 0) return null;
            var arr = f.normal ? Fighters[i].normals : Fighters[i].skills;
            return f.skillIndex < arr.Length ? arr[f.skillIndex] : null;
        }

        public EffectSpec SpecOf(in EffectState e) => e.kind switch
        {
            EffectKind.Skill => Fighters[e.owner].skills[e.skill].effectSpec,
            EffectKind.Bolt => BoltSpec,
            EffectKind.Explosion => ExplosionSpec,
            EffectKind.Puddle => PuddleSpec,
            EffectKind.FireZone => FireZoneSpec,
            _ => ShockZoneSpec,
        };

        static bool IsAttack(in EffectState e) => e.kind == EffectKind.Skill || e.kind == EffectKind.Bolt;

        // ------------------------------------------------------------------ tick
        public void Simulate(InputFrame in0, InputFrame in1)
        {
            Events.Clear();
            if (InIntro || RoundOver) in0 = in1 = default;            // vào hiệp / hiệp đã xong: khoá điều khiển
            StepFighter(0, in0);
            StepFighter(1, in1);
            Separate();
            ResolveStrikes(0);
            ResolveStrikes(1);
            StepEffects();
            if (HazardsEnabled && !InIntro && !RoundOver) StepHazard();
            for (int i = 0; i < State.props.Length; i++) StepProp(i);
            ElectrifyAura();
            if (State.darkTicks > 0) State.darkTicks--;
            if (State.koTick == 0 && (State.p0.hp <= 0 || State.p1.hp <= 0))
            {
                int w = State.p0.hp > 0 ? 0 : State.p1.hp > 0 ? 1 : -1;
                EndRound(w, false);
                ref var loser = ref Fighter(w == 0 ? 1 : 0);
                Emit(SimEventKind.KO, loser.x, 60 * Sub);
            }
            else if (State.koTick == 0 && !InIntro && --State.timer <= 0)
            {
                // hết giờ: ai còn nhiều % máu hơn thắng (GDD mục 2)
                State.timer = 0;
                long a = (long)State.p0.hp * Fighters[1].maxHp, b = (long)State.p1.hp * Fighters[0].maxHp;
                EndRound(a > b ? 0 : b > a ? 1 : -1, true);
                Emit(SimEventKind.TimeOver, 0, 0);
            }
            else if (State.koTick > 0 && !State.matchDecided && State.tick - State.koTick >= RoundEndDelay)
                StartRound();
            State.tick++;
        }

        /// <summary>Ái lực Khang: vật Kim loại trong bán kính quanh người tự nhiễm điện (còn 0.5 giây sau khi rời xa).</summary>
        void ElectrifyAura()
        {
            for (int i = 0; i < 2; i++)
            {
                int r = Fighters[i].electrifyRadiusPx * Sub;
                if (r <= 0) continue;
                ref var f = ref Fighter(i);
                for (int k = 0; k < State.props.Length; k++)
                {
                    ref var p = ref State.props[k];
                    if (p.phase == PropPhase.Broken || (Props[p.dataIndex].traits & PropTraits.Metal) == 0) continue;
                    if (Math.Abs(p.x - f.x) <= r) p.shockTicks = Math.Max(p.shockTicks, 30);
                }
            }
        }

        bool OnSlipperyFloor(in FighterState f)
        {
            if (!f.Grounded) return false;
            if (Raining) return true;                                  // mưa: cả sân trơn
            foreach (var e in State.effects)
                if (e.active && (e.kind == EffectKind.Puddle || e.kind == EffectKind.ShockZone)
                    && Math.Abs(f.x - e.x) < SpecOf(e).halfW * Sub) return true;
            return false;
        }

        // ------------------------------------------------------------------ nhân vật
        void StepFighter(int idx, InputFrame input)
        {
            ref var f = ref Fighter(idx);
            ref var o = ref Fighter(1 - idx);
            var data = Fighters[idx];
            var b = input.buttons;
            var pressed = b & ~f.prevButtons;
            f.prevButtons = b;
            if (f.hitFlash > 0) f.hitFlash--;
            if (f.invuln > 0) f.invuln--;
            if (f.shockFx > 0) f.shockFx--;
            if (f.burnTicks > 0 && --f.burnTicks % TickRate == 0 && f.hp > 0)
            {
                f.hp = Math.Max(0, f.hp - 10);                     // bỏng 10/giây
                f.lastDamage = 10;
            }
            if (TrainingMode) f.energy = 100;
            bool slippery = OnSlipperyFloor(f);

            var held = f.heldProp >= 0 ? Props[State.props[f.heldProp].dataIndex] : null;
            var inter = held ? data.FindInteraction(held.id) : null;
            bool neutral = f.action == FighterAction.Idle || f.action == FighterAction.Walk;
            f.crouching = f.Grounded && (b & Buttons.Down) != 0 && (neutral || f.action == FighterAction.Blockstun);
            f.blocking = f.Grounded && (b & Buttons.Guard) != 0 && (neutral || f.action == FighterAction.Blockstun);

            switch (f.action)
            {
                case FighterAction.Hitstun:
                case FighterAction.Blockstun:
                    f.actionTick++;
                    if (--f.stunTicks <= 0 && f.Grounded)
                    {
                        if (f.hp <= 0) StartKnockdown(ref f);          // hết máu: ngã luôn (KO)
                        else Recover(ref f);
                    }
                    break;
                case FighterAction.Knockdown:
                    f.actionTick++;
                    f.invuln = Math.Max(f.invuln, 2);                  // nằm sàn: không bị đánh tiếp
                    if (f.hp <= 0) break;                              // KO: nằm luôn
                    if (f.Grounded && --f.stunTicks <= 0)
                    {
                        f.action = FighterAction.Getup;
                        f.actionTick = 0;
                        f.stunTicks = MoveSet.Ok(data.moves?.getup) ? data.moves.getup.lengthTicks : 20;
                        f.invuln = Math.Max(GetupInvulnTicks, f.stunTicks + 4);
                    }
                    break;
                case FighterAction.Getup:
                    f.actionTick++;
                    if (--f.stunTicks <= 0) Recover(ref f);
                    break;
                case FighterAction.Roll:
                    f.actionTick++;
                    if (f.actionTick >= RollLength(data, f)) EndAction(ref f);
                    break;
                case FighterAction.Charge:
                    StepCharge(ref f, data, b);
                    break;
                case FighterAction.Skill:
                case FighterAction.Pickup:
                case FighterAction.Swing:
                case FighterAction.Toss:
                case FighterAction.Push:
                    StepBusy(idx, ref f, ref o, data, held, b, pressed);
                    break;
                default:
                    StepNeutral(idx, ref f, ref o, data, held, inter, b, pressed, slippery);
                    break;
            }

            // vật lý (số nguyên)
            var skill = CurrentSkill(idx);
            bool diving = f.action == FighterAction.Skill && f.normal && f.skillIndex == FighterData.AirDive;
            if (diving)
            {
                f.vx = f.facing * DiveVx;
                f.vy = -DiveVy + Gravity;                              // sau khi trừ trọng lực: lao xuống đều DiveVy
            }
            else if (f.action == FighterAction.Roll)
                f.vx = f.rollDir * RollSpeed;
            else if (skill != null && skill.dashSpeed != 0 && f.actionTick <= Math.Max(skill.LastActiveTick, 8))
                f.vx = f.facing * skill.dashSpeed;
            else if (slippery && f.Grounded)
                f.vx = f.action == FighterAction.Walk ? f.vx : f.vx * 15 / 16;   // vũng trơn: trượt dần mới dừng
            else if (f.Grounded && f.action != FighterAction.Walk && f.action != FighterAction.Jump)
                f.vx = 0;
            if (f.pullTicks > 0) {
                f.vx = (f.pullTargetX - f.x) / f.pullTicks;
                f.pullTicks--;
            }
            f.x += f.vx;
            if (f.dropTicks > 0) f.dropTicks--;
            // bước ra khỏi mép bệ (đi bộ / bị đẩy / lướt): rơi xuống
            if (f.floorY > 0 && f.y == f.floorY && f.vy == 0 && PlatformAt(f.x, f.floorY) < 0)
                f.floorY = 0;
            if (f.y > f.floorY || f.vy > 0)
            {
                int prevY = f.y;
                f.vy -= Gravity;
                f.y += f.vy;
                int land = LandingHeight(f, prevY);
                if (land >= 0)
                {
                    f.y = land;
                    f.floorY = land;
                    f.vy = 0;
                    f.doubleJumpUsed = false;
                    if (f.action == FighterAction.Jump)
                    {
                        f.action = FighterAction.Idle;
                        f.landTick = State.tick;
                        Emit(SimEventKind.Land, f.x, f.y);
                    }
                    else if (IsAirNormal(f))
                    {
                        // chạm đất khi đang đánh trên không: đá bổ → clip tiếp đất; đòn khác → dừng, tiếp đất
                        bool wasDive = f.skillIndex == FighterData.AirDive;
                        EndAction(ref f);
                        f.landTick = State.tick;
                        Emit(wasDive ? SimEventKind.Knockdown : SimEventKind.Land, f.x, f.y);
                        if (wasDive) StartNormal(ref f, data, FighterData.DiveLand);
                    }
                    else if (f.action == FighterAction.Hitstun && (f.juggle > 0 || f.hp <= 0))
                    {
                        StartKnockdown(ref f);
                        Emit(SimEventKind.Knockdown, f.x, f.y);
                    }
                    if (f.action != FighterAction.Skill) f.vx = 0;
                    if (f.Grounded) f.airAttackUsed = false;
                }
            }
            f.x = Math.Clamp(f.x, MinX, MaxX);

            if (f.heldProp >= 0)
            {
                ref var p = ref State.props[f.heldProp];
                p.x = f.x;
                p.y = f.y;
                p.facing = f.facing;
            }
        }

        public const int ChargeGainInterval = 3;      // gồng nộ: +1 năng lượng mỗi 3 tick (đầy thanh ~5 giây)
        public const int ChargeMinTicks = 12;         // giữ ít nhất bấy nhiêu tick (gồng hụt không huỷ ngay)

        static int ClipTicks(SpriteClip c, int fallback) => MoveSet.Ok(c) ? c.lengthTicks : fallback;

        /// <summary>Gồng nộ: vào thế (rageStart) → giữ (rageLoop) tới khi nhả nút / đầy thanh → thu thế (rageEnd).</summary>
        static void StepCharge(ref FighterState f, FighterData data, Buttons b)
        {
            f.actionTick++;
            f.vx = 0;
            if (f.chargeEnd > 0)
            {
                if (--f.chargeEnd <= 0) EndAction(ref f);
                return;
            }
            int start = ClipTicks(data.moves?.rageStart, 10);
            if (f.actionTick > start && f.actionTick % ChargeGainInterval == 0) AddEnergy(ref f, 1);
            bool holding = (b & Buttons.Guard) != 0 && (b & Buttons.Skill) != 0;
            if ((!holding && f.actionTick >= ChargeMinTicks) || f.energy >= 100)
                f.chargeEnd = ClipTicks(data.moves?.rageEnd, 10);
        }

        /// <summary>Hiển thị gồng nộ: 0 vào thế, 1 đang giữ, 2 thu thế; tick tính trong giai đoạn đó.</summary>
        public static int ChargePhase(FighterData data, in FighterState f, out int tick)
        {
            if (f.chargeEnd > 0)
            {
                tick = ClipTicks(data.moves?.rageEnd, 10) - f.chargeEnd;
                return 2;
            }
            int start = ClipTicks(data.moves?.rageStart, 10);
            tick = f.actionTick < start ? f.actionTick : f.actionTick - start;
            return f.actionTick < start ? 0 : 1;
        }

        static int RollLength(FighterData d, in FighterState f)
        {
            var clip = f.rollDir == f.facing ? d.moves?.rollFwd : d.moves?.rollBack;
            return MoveSet.Ok(clip) ? clip.lengthTicks : 18;
        }

        void StepNeutral(int idx, ref FighterState f, ref FighterState o, FighterData data, PropData held,
                         PropInteraction inter, Buttons b, Buttons pressed, bool slippery)
        {
            if (f.Grounded) f.facing = (sbyte)(o.x >= f.x ? 1 : -1);
            int dir = ((b & Buttons.Right) != 0 ? 1 : 0) - ((b & Buttons.Left) != 0 ? 1 : 0);
            int pressedDir = ((pressed & Buttons.Right) != 0 ? 1 : 0) - ((pressed & Buttons.Left) != 0 ? 1 : 0);

            if ((pressed & Buttons.Grab) != 0 && f.Grounded)
            {
                if (f.heldProp >= 0) StartToss(ref f, inter);
                else TryPickup(idx, ref f, data);
            }
            else if ((pressed & Buttons.Light) != 0 && f.Grounded && inter?.swing != null && inter.swing.IsValid)
            {
                StartAction(ref f, FighterAction.Swing);
                f.actionProp = f.heldProp;
            }
            else if ((pressed & Buttons.Skill) != 0 && (b & Buttons.Guard) != 0 && f.Grounded && f.heldProp < 0 && f.energy < 100)
            {
                // Đỡ + Kỹ năng: gồng nộ (đứng yên tích năng lượng, bỏ thế đỡ)
                StartAction(ref f, FighterAction.Charge);
                f.chargeEnd = 0;
            }
            else if ((pressed & Buttons.Skill) != 0 && f.heldProp < 0)
            {
                TrySkill(idx, ref f, data, b, dir);
            }
            else if ((pressed & (Buttons.Light | Buttons.Heavy)) != 0 && f.heldProp < 0 && !f.Grounded)
            {
                // trên không: J nhẹ, K mạnh; đang RƠI + ↓ → đá bổ. Mỗi lần nhảy 1 đòn
                if (!f.airAttackUsed)
                {
                    bool dive = (b & Buttons.Down) != 0 && f.vy <= 0 && data.Normal(FighterData.AirDive) != null;
                    int n = dive ? FighterData.AirDive : (pressed & Buttons.Heavy) != 0 ? FighterData.AirHeavy : FighterData.AirLight;
                    if (StartNormal(ref f, data, n)) f.airAttackUsed = true;
                }
            }
            else if ((pressed & Buttons.Light) != 0 && f.heldProp < 0)
            {
                StartNormal(ref f, data, FighterData.Light1);            // J: đánh nhẹ
            }
            else if ((pressed & Buttons.Heavy) != 0 && f.heldProp < 0 && f.Grounded)
            {
                StartNormal(ref f, data, FighterData.Heavy);             // K: đánh mạnh
            }
            else if (f.Grounded && (b & Buttons.Guard) != 0 && pressedDir != 0 && f.heldProp < 0)
            {
                // đỡ + hướng = lăn né (GDD mục 2): bất tử, đi xuyên đối thủ
                StartAction(ref f, FighterAction.Roll);
                f.rollDir = pressedDir;
                f.invuln = Math.Max(f.invuln, RollLength(data, f));
            }
            else if (!f.Grounded && (pressed & Buttons.Up) != 0 && TryDoubleJump(ref f, data, held, dir))
            {
                f.doubleJumpTick = State.tick;                           // nhảy lần 2 giữa không
                Emit(SimEventKind.DoubleJump, f.x, f.y);
            }
            else if (f.Grounded && f.floorY > 0 && (pressed & Buttons.Up) != 0 && (b & Buttons.Down) != 0)
            {
                // ↓ + nhảy trên bệ: rơi xuyên xuống
                f.floorY = 0;
                f.dropTicks = DropThroughTicks;
                f.action = FighterAction.Jump;
                f.vx = 0;
            }
            else if (f.Grounded)
            {
                if (f.blocking || f.crouching)
                {
                    f.vx = 0;
                    f.action = FighterAction.Idle;
                    return;
                }
                int speed = data.walkSpeed * (held ? held.MoveSpeedPercent : 100) / 100;
                if (slippery)
                {
                    // vũng trơn: tăng tốc chậm, khó dừng (GDD mục 4)
                    f.vx = Math.Clamp(f.vx + dir * speed / 6, -speed * 3 / 2, speed * 3 / 2);
                    if (dir == 0) f.vx = f.vx * 15 / 16;
                }
                else f.vx = dir * speed;
                f.action = dir != 0 ? FighterAction.Walk : FighterAction.Idle;
                if ((pressed & Buttons.Up) != 0 && (held == null || held.size <= PropSize.Medium))
                {
                    f.vy = data.jumpVelocity;
                    f.action = FighterAction.Jump;
                }
            }
        }

        static bool StartNormal(ref FighterState f, FighterData data, int index, int startTick = 0)
        {
            if (data.Normal(index) == null) return false;
            bool air = !f.Grounded;
            int vx = f.vx;
            StartAction(ref f, FighterAction.Skill);
            f.normal = true;
            f.skillIndex = index;
            f.actionTick = startTick;
            if (air) f.vx = vx;                                         // đánh trên không: giữ đà nhảy
            return true;
        }

        static bool IsAirNormal(in FighterState f) =>
            f.action == FighterAction.Skill && f.normal && f.skillIndex >= FighterData.AirLight && f.skillIndex <= FighterData.AirDive;

        void StepBusy(int idx, ref FighterState f, ref FighterState o, FighterData data, PropData held, Buttons b, Buttons pressed)
        {
            f.actionTick++;
            var act = ActionInteraction(idx);
            switch (f.action)
            {
                case FighterAction.Skill:
                {
                    var s = CurrentSkill(idx);
                    if (s == null) { EndAction(ref f); break; }
                    if (f.normal)
                    {
                        int first = s.hits.Length > 0 ? s.hits[0].startTick : 6;
                        bool connected = f.hitMask != 0;
                        int dir = ((b & Buttons.Right) != 0 ? 1 : 0) - ((b & Buttons.Left) != 0 ? 1 : 0);
                        // K + L bấm lệch vài tick vẫn ra tuyệt chiêu; đòn thường trúng → huỷ sang chiêu đặc biệt
                        if ((pressed & Buttons.Skill) != 0 && ((f.skillIndex == FighterData.Heavy && f.actionTick <= 4) || connected))
                        {
                            // K ngay trước L → coi là K + L (tuyệt chiêu)
                            var bb = f.skillIndex == FighterData.Heavy && !connected ? b | Buttons.Heavy : b;
                            TrySkill(idx, ref f, data, bb, dir);
                            if (f.actionTick == 0) break;                  // chiêu mới đã bắt đầu (StartAction đặt lại tick)
                        }
                        // combo J-J-J: nối trong cửa sổ nối (trước đoạn thu đòn), đòn sau bắt đầu từ tư thế nối
                        int chainEnd = s.chainEndTick > 0 ? s.chainEndTick : s.clip.lengthTicks;
                        bool chainWindow = f.actionTick >= first && f.actionTick <= chainEnd;
                        if ((pressed & Buttons.Light) != 0 && f.skillIndex < FighterData.Light3 && chainWindow)
                        {
                            var next = data.Normal(f.skillIndex + 1);
                            if (next != null) { StartNormal(ref f, data, f.skillIndex + 1, next.chainStartTick); break; }
                        }
                        if ((pressed & Buttons.Heavy) != 0 && f.skillIndex <= FighterData.Light3 && connected && f.Grounded && chainWindow)
                        { StartNormal(ref f, data, FighterData.Heavy, data.Normal(FighterData.Heavy)?.chainStartTick ?? 0); break; }
                        // đá bổ: giữ tư thế lao tới khi chạm đất (không kết thúc theo độ dài clip)
                        if (f.skillIndex == FighterData.AirDive) break;
                        if (f.actionTick >= s.clip.lengthTicks) EndAction(ref f);
                        break;
                    }
                    var e = s.effectSpec;
                    if (e != null && e.enabled && f.actionTick == e.spawnTick) SpawnEffect(idx, f.skillIndex);
                    if (f.actionTick >= s.clip.lengthTicks) EndAction(ref f);
                    break;
                }
                case FighterAction.Pickup:
                    if (act == null || f.actionTick >= act.pickup.lengthTicks) EndAction(ref f);
                    break;
                case FighterAction.Swing:
                    if (act == null) { EndAction(ref f); break; }
                    if (f.actionTick == act.swing.TickOfFrame(act.swingHitFrame) && held) SwingHit(idx, ref f, ref o, held);
                    if (f.actionTick >= act.swing.lengthTicks) EndAction(ref f);
                    break;
                case FighterAction.Toss:
                    if (act == null) { EndAction(ref f); break; }
                    int release = act.toss.TickOfFrame(act.releaseFrame);
                    if (f.actionTick == release) ReleaseProp(idx, ref f);
                    if (f.actionTick >= release + 8) EndAction(ref f);
                    break;
                case FighterAction.Push:
                    // clip "day" vẽ sẵn chiếc xe → chỉ cho xe lăn đi khi clip kết thúc
                    int len = act?.push != null && act.push.IsValid ? act.push.lengthTicks : 24;
                    if (f.actionTick >= len)
                    {
                        LaunchPushed(idx, ref f);
                        EndAction(ref f);
                    }
                    break;
            }
        }

        static void StartAction(ref FighterState f, FighterAction a)
        {
            f.action = a;
            f.actionTick = 0;
            f.vx = 0;
            f.hitMask = 0;
            f.armorUsed = false;
            f.normal = false;
        }

        static void EndAction(ref FighterState f)
        {
            f.action = f.Grounded ? FighterAction.Idle : FighterAction.Jump;
            f.actionTick = 0;
            f.skillIndex = -1;
            f.actionProp = -1;
            f.hitMask = 0;
            f.normal = false;
        }

        static void Recover(ref FighterState f)
        {
            f.action = FighterAction.Idle;
            f.actionTick = 0;
            f.stunTicks = 0;
            f.comboTaken = 0;
            f.juggle = 0;
            f.dizzy = false;
            f.airAttackUsed = false;
        }

        static void StartKnockdown(ref FighterState f)
        {
            f.action = FighterAction.Knockdown;
            f.stunTicks = KnockdownTicks;
            f.vx = 0;
        }

        /// <summary>Clip tương tác của hành động hiện tại (theo actionProp, không theo vật đang cầm).</summary>
        public PropInteraction ActionInteraction(int idx)
        {
            ref var f = ref Fighter(idx);
            if (f.actionProp < 0 || f.actionProp >= State.props.Length) return null;
            return Fighters[idx].FindInteraction(Props[State.props[f.actionProp].dataIndex].id);
        }

        void TrySkill(int idx, ref FighterState f, FighterData data, Buttons b, int dir)
        {
            SkillInput input =
                (b & Buttons.Heavy) != 0 ? SkillInput.Super :
                (b & Buttons.Up) != 0 ? SkillInput.Up :
                (b & Buttons.Down) != 0 ? SkillInput.Down :
                dir != 0 && dir == f.facing ? SkillInput.Forward : SkillInput.Neutral;
            int i = data.FindSkill(input);
            if (i < 0) return;
            var s = data.skills[i];
            if (f.energy < s.cost) return;
            f.energy -= s.cost;
            StartAction(ref f, FighterAction.Skill);
            f.skillIndex = i;
            if (s.invulnTicks > 0) f.invuln = Math.Max(f.invuln, s.invulnTicks);
            if (s.jumpVelocity > 0 && f.Grounded) f.vy = s.jumpVelocity;
            if (s.screenDarkTicks > 0) State.darkTicks = Math.Max(State.darkTicks, s.screenDarkTicks);
            if (s.input == SkillInput.Super) Emit(SimEventKind.Super, f.x, 0, f.facing, idx);
            if (s.effectSpec != null && s.effectSpec.enabled && s.effectSpec.spawnTick <= 0) SpawnEffect(idx, i);
        }

        // ------------------------------------------------------------------ hitbox / hurtbox
        public Box HurtBox(int i)
        {
            ref var f = ref Fighter(i);
            var d = Fighters[i];
            int h = d.hurtHeight * Sub;
            if (f.action == FighterAction.Knockdown) h /= 3;
            else if (f.crouching) h = h * 2 / 3;
            int hw = d.hurtHalfWidth * Sub;
            return new Box(f.x - hw, f.x + hw, f.y, f.y + h);
        }

        public static Box WindowBox(in FighterState f, HitWindow w)
        {
            int a = f.x + f.facing * w.x0 * Sub, c = f.x + f.facing * w.x1 * Sub;
            return new Box(Math.Min(a, c), Math.Max(a, c), f.y + w.y0 * Sub, f.y + w.y1 * Sub);
        }

        public static Box EffectBox(in EffectState e, EffectSpec s) =>
            new(e.x - s.halfW * Sub, e.x + s.halfW * Sub, e.y - s.halfH * Sub, e.y + s.halfH * Sub);

        /// <summary>Nhịp đánh đang hoạt động của nhân vật i ở tick hiện tại (cho hiển thị F4).</summary>
        public bool IsWindowActive(int i, int w)
        {
            var s = CurrentSkill(i);
            ref var f = ref Fighter(i);
            return s != null && w < s.hits.Length && f.actionTick >= s.hits[w].startTick && f.actionTick <= s.hits[w].endTick;
        }

        void ResolveStrikes(int a)
        {
            var s = CurrentSkill(a);
            if (s == null) return;
            ref var f = ref Fighter(a);
            for (int w = 0; w < s.hits.Length && w < 31; w++)
            {
                var win = s.hits[w];
                if (f.actionTick < win.startTick || f.actionTick > win.endTick) continue;
                var box = WindowBox(f, win);
                if ((win.flags & HitFlags.Reflect) != 0) ReflectInBox(a, box);
                ApplyElements(box, win.flags, a);
                StrikeExplosives(box, a);
                StrikePowerBox(box);
                if ((f.hitMask & (1 << w)) != 0) continue;
                bool reach = (win.flags & HitFlags.Homing) != 0 || box.Overlaps(HurtBox(1 - a));
                if (!reach) continue;
                if (TryHit(a, win.damage, win.hitstun, win.pushPx, win.flags))
                {
                    f.hitMask |= 1 << w;
                    if (f.normal && f.skillIndex == FighterData.AirDive && f.action == FighterAction.Skill)
                    {
                        EndAction(ref f);                              // đá bổ trúng: bật nhẹ lên, rơi tự do
                        f.vy = DiveBounce;
                        f.vx = -f.facing * Sub;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Một đòn chạm tới đối thủ của người đánh a. Trả về true nếu đòn "ăn" (trúng, bị đỡ hoặc bị phản),
        /// false nếu trượt (đối thủ bất tử, sai độ cao trên không/mặt đất).
        /// </summary>
        public bool TryHit(int a, int damage, int hitstun, int pushPx, HitFlags flags)
        {
            ref var att = ref Fighter(a);
            ref var def = ref Fighter(1 - a);
            if (def.invuln > 0 || def.hp <= 0) return false;
            bool defAir = !def.Grounded;
            if ((flags & HitFlags.AirOnly) != 0 && !defAir) return false;
            if ((flags & HitFlags.GroundOnly) != 0 && defAir) return false;

            // phản đòn (Thế Chờ): bị đánh trong cửa sổ chờ → tự đánh trả, không mất máu
            var defSkill = CurrentSkill(1 - a);
            if (defSkill != null && defSkill.counterTicks > 0 && def.actionTick <= defSkill.counterTicks)
            {
                int dmg = Math.Max(1, damage) * defSkill.counterPercent / 100;
                EndAction(ref def);
                def.invuln = 12;
                AddEnergy(ref def, 8);
                ApplyHit(1 - a, dmg, 24, 30, HitFlags.Knockdown);
                return true;
            }

            // đỡ: giữ nút đỡ; đòn thấp phải ngồi đỡ, đòn từ trên phải đứng đỡ
            bool canBlock = def.blocking && (flags & HitFlags.Unblockable) == 0
                            && !((flags & HitFlags.Low) != 0 && !def.crouching)
                            && !((flags & HitFlags.Overhead) != 0 && def.crouching);
            if (canBlock)
            {
                def.action = FighterAction.Blockstun;
                def.stunTicks = hitstun * 2 / 3 + 4;
                def.x = Math.Clamp(def.x + att.facing * pushPx * Sub, MinX, MaxX);
                def.guard -= Math.Max(5, damage / 2) * ((flags & HitFlags.GuardCrush) != 0 ? 3 : 1);
                def.actionTick = 0;
                AddEnergy(ref att, 2);
                Emit(SimEventKind.Block, def.x - att.facing * Fighters[1 - a].hurtHalfWidth * Sub, def.y + 60 * Sub, att.facing);
                if (def.guard <= 0)
                {
                    // vỡ thế đỡ → choáng 1 giây (GDD mục 2)
                    def.guard = GuardMax;
                    def.action = FighterAction.Hitstun;
                    def.stunTicks = GuardBreakTicks;
                    def.hitFlash = HitFlashTicks;
                    def.dizzy = true;
                    Emit(SimEventKind.GuardBreak, def.x, def.y + 60 * Sub);
                }
                return true;
            }

            // siêu giáp: chịu 1 đòn không bị ngắt (vật thì vẫn ăn)
            var ds = CurrentSkill(1 - a);
            if (ds != null && ds.armor && !def.armorUsed && (flags & HitFlags.Unblockable) == 0)
            {
                def.armorUsed = true;
                int dmg = damage * ComboScale(def) / 100;
                def.hp = Math.Max(0, def.hp - dmg);
                def.lastDamage = dmg;
                def.hitFlash = HitFlashTicks;
                return true;
            }

            ApplyHit(a, damage, hitstun, pushPx, flags);
            return true;
        }

        static int ComboScale(in FighterState def) => Math.Max(30, 100 - 10 * def.comboTaken);

        /// <summary>Người a đánh trúng đối thủ (1 - a).</summary>
        void ApplyHit(int a, int damage, int hitstun, int pushPx, HitFlags flags)
        {
            ref var att = ref Fighter(a);
            ref var def = ref Fighter(1 - a);
            AddEnergy(ref att, 5);
            // Móc Túi: cướp vật đối thủ đang cầm
            if ((flags & HitFlags.Steal) != 0 && def.heldProp >= 0 && att.heldProp < 0)
            {
                att.heldProp = def.heldProp;
                State.props[att.heldProp].owner = a;
                def.heldProp = -1;
            }
            def.pullTicks = 0;
            if ((flags & HitFlags.Pull) != 0)
            {
                // kéo về sát thân người kéo (không lọt vào vùng đẩy thân → không bị Separate đẩy lệch đích)
                int contact = Math.Max(40, Fighters[a].hurtHalfWidth + Fighters[1 - a].hurtHalfWidth + 2);
                def.pullTargetX = Math.Clamp(att.x + att.facing * contact * Sub, MinX, MaxX);
                def.pullTicks = 10;
                def.pulledForward = (def.pullTargetX - def.x) * def.facing >= 0;
                pushPx = 0;
            }
            ReceiveHit(1 - a, ref def, damage, hitstun, att.facing, pushPx, flags);
        }

        /// <summary>
        /// Vụ nổ / vùng cháy / vùng giật đánh trúng nhân vật t (không có người đánh, trúng cả hai bên).
        /// hitstun = 0: chỉ mất máu + trạng thái (bỏng), không bị khống chế.
        /// </summary>
        bool EnvHit(int t, int damage, int hitstun, int pushPx, HitFlags flags, int sourceX)
        {
            ref var def = ref Fighter(t);
            if (def.invuln > 0 || def.hp <= 0) return false;
            if (def.blocking && (flags & HitFlags.Unblockable) == 0 && hitstun > 0)
            {
                def.action = FighterAction.Blockstun;
                def.stunTicks = hitstun / 2 + 4;
                def.actionTick = 0;
                def.guard -= Math.Max(5, damage / 2);
                Emit(SimEventKind.Block, def.x, def.y + 60 * Sub, def.x >= sourceX ? 1 : -1);
                return true;
            }
            if (hitstun == 0)
            {
                def.hp = Math.Max(0, def.hp - damage);
                def.lastDamage = damage;
                if ((flags & HitFlags.Fire) != 0) def.burnTicks = Math.Max(def.burnTicks, FighterBurnTicks);
                return true;
            }
            ReceiveHit(t, ref def, damage, hitstun, def.x >= sourceX ? 1 : -1, pushPx, flags);
            return true;
        }

        /// <summary>Lõi nhận đòn: mất máu (giảm theo combo), rơi vật, đẩy lùi, choáng / ngã / tung, bỏng / tê điện.</summary>
        void ReceiveHit(int defIdx, ref FighterState def, int damage, int hitstun, int pushDir, int pushPx, HitFlags flags)
        {
            int dmg = damage * ComboScale(def) / 100;
            def.hp = Math.Max(0, def.hp - dmg);
            def.lastDamage = dmg;
            def.comboTaken++;
            def.hitFlash = HitFlashTicks;
            AddEnergy(ref def, 3);
            if ((flags & HitFlags.Fire) != 0) def.burnTicks = Math.Max(def.burnTicks, FighterBurnTicks);
            if ((flags & HitFlags.Electric) != 0) def.shockFx = 24;
            def.hitHeavy = hitstun >= 22 || (flags & (HitFlags.Knockdown | HitFlags.Launch | HitFlags.GuardCrush)) != 0;
            def.dizzy = (flags & (HitFlags.Stun | HitFlags.Electric)) != 0;
            def.reeled = (flags & HitFlags.Pull) != 0;
            int hd = Fighters[defIdx].hurtHalfWidth;
            Emit(def.hitHeavy ? SimEventKind.HitHeavy : SimEventKind.HitLight,
                 def.x - pushDir * hd * Sub, def.y + 60 * Sub, pushDir, -1, defIdx, flags);

            // vật đang cầm rơi khi trúng đòn mạnh hoặc đang cầm vật Lớn (GDD mục 4)
            if (def.heldProp >= 0)
            {
                ref var p = ref State.props[def.heldProp];
                if ((flags & (HitFlags.Knockdown | HitFlags.Launch)) != 0 || Props[p.dataIndex].size >= PropSize.Large
                    || def.action == FighterAction.Pickup)
                {
                    p.phase = PropPhase.Floor;
                    p.owner = -1;
                    p.y = 0;
                    def.heldProp = -1;
                }
            }

            def.x = Math.Clamp(def.x + pushDir * pushPx * Sub, MinX, MaxX);
            def.actionTick = 0;
            def.skillIndex = -1;
            def.actionProp = -1;
            def.hitMask = 0;
            bool air = !def.Grounded;
            if ((flags & HitFlags.Launch) != 0 || (air && (flags & HitFlags.Knockdown) == 0))
            {
                def.action = FighterAction.Hitstun;
                def.stunTicks = hitstun + 20;
                if (def.juggle < JuggleMax)
                {
                    def.vy = LaunchVelocity;
                    def.juggle++;
                }
                else def.invuln = 90;                         // quá 3 lần tung → tự tiếp đất, không bị đánh tiếp
            }
            else if ((flags & HitFlags.Knockdown) != 0)
            {
                if (air) { def.action = FighterAction.Hitstun; def.juggle = Math.Max(def.juggle, 1); }
                else StartKnockdown(ref def);
            }
            else if (def.hp <= 0)
            {
                // hạ gục: tung nhẹ rồi nằm luôn
                def.action = FighterAction.Hitstun;
                def.stunTicks = 10;
                def.vy = LaunchVelocity / 2;
                def.juggle = Math.Max(def.juggle, 1);
            }
            else
            {
                def.action = FighterAction.Hitstun;
                def.stunTicks = hitstun + ((flags & HitFlags.Stun) != 0 ? 30 : 0)
                                + ((flags & HitFlags.Electric) != 0 ? ElectricStunTicks : 0);
            }
        }

        static void AddEnergy(ref FighterState f, int v) => f.energy = Math.Min(100, f.energy + v);

        void ReflectInBox(int a, in Box box)
        {
            var es = State.effects;
            for (int i = 0; i < es.Length; i++)
            {
                ref var e = ref es[i];
                if (!e.active || !IsAttack(e) || e.owner == a || e.field || e.vx == 0) continue;
                if (!EffectBox(e, SpecOf(e)).Overlaps(box)) continue;
                e.owner = a;
                e.vx = -e.vx;
                e.age = 0;
            }
            for (int i = 0; i < State.props.Length; i++)
            {
                ref var p = ref State.props[i];
                if (p.phase != PropPhase.Flying || p.owner == a) continue;
                var pb = PropBox(p);
                if (!pb.Overlaps(box)) continue;
                p.owner = a;
                p.vx = -p.vx;
                p.vy = Math.Max(p.vy, 3 * Sub);
                p.facing = (sbyte)-p.facing;
            }
        }

        // ------------------------------------------------------------------ đạn / bẫy / vùng
        void SpawnEffect(int owner, int skill)
        {
            ref var f = ref Fighter(owner);
            ref var o = ref Fighter(1 - owner);
            var s = Fighters[owner].skills[skill].effectSpec;
            // bẫy: mỗi loại tối đa 1 cái
            if (s.speed == 0 && !s.atOpponent)
                for (int i = 0; i < State.effects.Length; i++)
                    if (State.effects[i].active && State.effects[i].owner == owner && State.effects[i].skill == skill)
                        State.effects[i].active = false;
            for (int k = 0; k < s.count; k++)
            {
                int slot = FreeEffectSlot();
                if (slot < 0) return;
                int x = s.atOpponent ? o.x : f.x + f.facing * s.offsetX * Sub;
                State.effects[slot] = new EffectState
                {
                    active = true, kind = EffectKind.Skill, owner = owner, skill = skill,
                    x = Math.Clamp(x, MinX - 40 * Sub, MaxX + 40 * Sub),
                    y = (s.atOpponent ? 0 : f.y) + s.offsetY * Sub
                        + (Fighters[owner].skills[skill].dropsFromAbove ? 144 * Sub : 0),
                    vx = f.facing * s.speed,
                    vy = s.vy + (2 * k - (s.count - 1)) * s.spreadVy / 2,
                    lifetime = s.lifetime,
                };
            }
        }

        int FreeEffectSlot()
        {
            for (int i = 0; i < State.effects.Length; i++)
                if (!State.effects[i].active) return i;
            return -1;
        }

        void StepEffects()
        {
            var es = State.effects;
            for (int i = 0; i < es.Length; i++)
            {
                ref var e = ref es[i];
                if (!e.active) continue;
                var s = SpecOf(e);
                e.age++;
                bool cable = e.kind == EffectKind.Skill && (s.flags & HitFlags.Pull) != 0;
                if (cable && (e.returningCable || e.age >= e.lifetime)) {
                    ref var caster = ref Fighter(e.owner);
                    int left = e.lifetime + 8 - e.age;
                    if (left <= 0) e.active = false;
                    else {
                        e.x += (caster.x + caster.facing * 22 * Sub - e.x) / left;
                        e.y += (caster.y + 55 * Sub - e.y) / left;
                        e.returningCable = true;
                    }
                    continue;
                }
                if (e.age >= e.lifetime) { e.active = false; continue; }
                bool drops = e.kind == EffectKind.Skill && Fighters[e.owner].skills[e.skill].dropsFromAbove;
                if (drops && e.age <= 12)
                {
                    int oldBottom = e.y - s.halfH * Sub;
                    e.y -= 12 * Sub;
                    if (e.scaffoldImpactAge == 0 && Fighters[e.owner].skills[e.skill].clip?.name == "sap_gian_giao")
                    {
                        int bottom = e.y - s.halfH * Sub;
                        int top = 0, contactX = e.x;
                        // Mảnh giàn vẽ rộng 46 px, cao 65 px. Vùng sát thương lớn hơn phần khung sắt.
                        int visualHalfWidth = s.halfH * Sub * 46 / 65;
                        foreach (var prop in State.props)
                        {
                            if (prop.phase != PropPhase.Floor && prop.phase != PropPhase.Flying) continue;
                            var box = PropBox(prop);
                            if (box.x1 < e.x - visualHalfWidth || box.x0 > e.x + visualHalfWidth ||
                                box.y1 > oldBottom || box.y1 < bottom || box.y1 <= top) continue;
                            top = box.y1;
                            contactX = Math.Clamp(prop.x, e.x - visualHalfWidth, e.x + visualHalfWidth);
                        }
                        if (top >= bottom)
                        {
                            e.scaffoldImpactAge = e.age;
                            e.scaffoldImpactX = contactX;
                            e.scaffoldImpactY = top;
                        }
                    }
                }
                if (!IsAttack(e))
                {
                    StepEnvironment(ref e, s);
                    continue;
                }

                if (!e.field)
                {
                    e.x += e.vx;
                    e.vy -= s.gravity;
                    e.y += e.vy;
                    if (s.gravity > 0 && e.y - s.halfH * Sub <= 0)
                    {
                        e.y = s.halfH * Sub;
                        if (s.landFieldTicks > 0)
                        {
                            e.field = true;           // dầu sôi tiếp đất → vùng cháy
                            e.vx = e.vy = 0;
                            e.age = 0;
                            e.lifetime = s.landFieldTicks;
                            e.cooldown = 0;
                        }
                        else { e.active = false; continue; }
                    }
                    if (e.x < MinX - 120 * Sub || e.x > MaxX + 120 * Sub) {
                        if(cable) { e.returningCable=true; e.age=e.lifetime-1; }
                        else e.active=false;
                        continue;
                    }
                }

                // bẫy bắn tia về phía đối thủ
                if (s.emitInterval > 0 && ++e.emitTimer >= s.emitInterval)
                {
                    e.emitTimer = 0;
                    int slot = FreeEffectSlot();
                    if (slot >= 0)
                    {
                        ref var tgt = ref Fighter(1 - e.owner);
                        int dir = tgt.x >= e.x ? 1 : -1;
                        es[slot] = new EffectState
                        {
                            active = true, kind = EffectKind.Bolt, owner = e.owner, skill = -1, x = e.x + dir * 12 * Sub, y = 40 * Sub,
                            vx = dir * BoltSpec.speed, lifetime = BoltSpec.lifetime,
                        };
                    }
                }

                // va chạm đạn với đạn của đối thủ: triệt tiêu nhau
                if (!e.field && e.vx != 0)
                {
                    var eb = EffectBox(e, s);
                    for (int j = 0; j < es.Length; j++)
                    {
                        if (j == i || !es[j].active || !IsAttack(es[j]) || es[j].owner == e.owner || es[j].field || es[j].vx == 0) continue;
                        if (!EffectBox(es[j], SpecOf(es[j])).Overlaps(eb)) continue;
                        es[j].active = false;
                        e.active = false;
                        break;
                    }
                    if (!e.active) continue;
                }

                // đạn / vùng mang lửa hoặc điện: đốt, nhiễm điện vật dụng và vũng trên đường đi
                ApplyElements(EffectBox(e, s), s.flags, e.owner);

                if (e.cooldown > 0) { e.cooldown--; continue; }
                if (drops && e.age <= 12) continue;               // vật rơi (giàn giáo, nồi lẩu): chỉ gây sát thương khi chạm đất
                if (s.damage <= 0) continue;                       // bẫy chỉ bắn tia (Cọc Tiếp Địa), tự nó không gây sát thương
                int target = 1 - e.owner;
                if (!EffectBox(e, s).Overlaps(HurtBox(target))) continue;
                if ((s.flags & HitFlags.SafeNearOwner) != 0 && Math.Abs(Fighter(target).x - Fighter(e.owner).x) < 60 * Sub) continue;
                bool field = e.field || s.hitInterval > 0;
                int dmg = e.field ? Math.Max(5, s.damage / 6) : s.damage;
                if (!TryHit(e.owner, dmg, s.hitstun, s.pushPx, s.flags)) continue;
                if(cable && Fighter(target).pullTicks==0) { e.returningCable=true; e.age=e.lifetime-1; }
                else if (field) e.cooldown = Math.Max(10, s.hitInterval > 0 ? s.hitInterval : 30);
                else if (drops) e.cooldown = e.lifetime;          // trúng 1 lần rồi nằm lại trên sân tới hết thời gian
                else e.active = false;
            }
        }

        // ------------------------------------------------------------------ phản ứng môi trường (GDD mục 4)
        /// <summary>Vùng cháy / vùng giật: gây sát thương cả hai bên theo chu kỳ, lan lửa/điện sang vật dụng.</summary>
        void StepEnvironment(ref EffectState e, EffectSpec s)
        {
            if (e.kind != EffectKind.FireZone && e.kind != EffectKind.ShockZone) return;   // nổ: đã gây sát thương lúc xuất hiện; vũng: chỉ trơn
            var box = EffectBox(e, s);
            ApplyElements(box, s.flags, -1);
            if (e.cooldown > 0) { e.cooldown--; return; }
            bool any = false;
            for (int t = 0; t < 2; t++)
            {
                if (!box.Overlaps(HurtBox(t))) continue;
                any |= e.kind == EffectKind.FireZone
                    ? EnvHit(t, s.damage, 0, 0, s.flags, e.x)                 // cháy: mất máu + bỏng, không khống chế
                    : EnvHit(t, s.damage, s.hitstun, 6, s.flags, e.x);        // giật: tê
            }
            if (any) e.cooldown = s.hitInterval;
        }

        /// <summary>Hộp va chạm của vật dụng, cao theo cỡ (khớp sprite sàn đúng tỷ lệ: ghế ~21 px, thùng phi ~48 px).</summary>
        Box PropBox(in PropState p)
        {
            int h = Props[p.dataIndex].size switch
            {
                PropSize.Small => 24,
                PropSize.Medium => 40,
                PropSize.Large => 48,
                _ => 64,
            };
            return new Box(p.x - 14 * Sub, p.x + 14 * Sub, p.y, p.y + h * Sub);
        }

        /// <summary>Lửa / điện chạm vào vật dụng và vũng trong hộp.</summary>
        void ApplyElements(in Box box, HitFlags flags, int owner)
        {
            bool fire = (flags & HitFlags.Fire) != 0, elec = (flags & HitFlags.Electric) != 0;
            if (!fire && !elec) return;
            for (int k = 0; k < State.props.Length; k++)
            {
                ref var p = ref State.props[k];
                if (p.phase == PropPhase.Broken || !PropBox(p).Overlaps(box)) continue;
                var d = Props[p.dataIndex];
                if (fire) Ignite(k, owner);
                if (elec && (d.traits & PropTraits.Metal) != 0) p.shockTicks = Math.Max(p.shockTicks, ShockTicks);
            }
            // vũng: điện → vùng giật; lửa gặp vũng dễ cháy (sơn) → vùng cháy (chỉ xét theo chiều ngang: tia lan xuống sàn)
            var es = State.effects;
            for (int i = 0; i < es.Length; i++)
            {
                ref var e = ref es[i];
                if (!e.active || e.kind != EffectKind.Puddle) continue;
                int hw = PuddleSpec.halfW * Sub;
                if (box.x1 < e.x - hw || box.x0 > e.x + hw) continue;
                if (elec) Convert(ref e, EffectKind.ShockZone);
                else if (fire && e.flammable) Convert(ref e, EffectKind.FireZone);
            }
        }

        void Convert(ref EffectState e, EffectKind kind)
        {
            e.kind = kind;
            e.age = 0;
            e.cooldown = 0;
            e.lifetime = SpecOf(e).lifetime;
            e.y = SpecOf(e).halfH * Sub;
        }

        void Ignite(int k, int owner)
        {
            ref var p = ref State.props[k];
            var d = Props[p.dataIndex];
            if ((d.traits & PropTraits.Explosive) != 0) Explode(k, owner);
            else if ((d.traits & PropTraits.Flammable) != 0) p.burnTicks = Math.Max(p.burnTicks, BurnTicks);
        }

        /// <summary>Đòn của chiêu trúng vật nổ đang nằm sàn: 2 lần → nổ (GDD mục 4).</summary>
        void StrikeExplosives(in Box box, int owner)
        {
            for (int k = 0; k < State.props.Length; k++)
            {
                ref var p = ref State.props[k];
                if (p.phase != PropPhase.Floor || p.hitCooldown > 0) continue;
                if ((Props[p.dataIndex].traits & PropTraits.Explosive) == 0 || !PropBox(p).Overlaps(box)) continue;
                p.hitCooldown = 20;
                if (++p.hardHits >= 2) Explode(k, owner);
            }
        }

        /// <summary>Nổ: bán kính 2.5 m, sát thương + hất tung CẢ HAI BÊN, đốt / kích nổ vật xung quanh (dây chuyền).</summary>
        void Explode(int k, int owner)
        {
            ref var p = ref State.props[k];
            if (p.phase == PropPhase.Broken) return;
            var d = Props[p.dataIndex];
            if (p.phase == PropPhase.Held)
                for (int t = 0; t < 2; t++)
                    if (Fighter(t).heldProp == k) Fighter(t).heldProp = -1;
            p.phase = PropPhase.Broken;
            p.owner = -1;
            p.vx = p.vy = 0;
            p.y = 0;
            p.burnTicks = p.shockTicks = 0;
            int x = p.x;
            SpawnEnv(EffectKind.Explosion, x, false);
            Emit(SimEventKind.Explosion, x, 40 * Sub);
            int dmg = d.explodeDamage > 0 ? d.explodeDamage : ExplosionDamage;
            int r = ExplosionRadiusPx * Sub;
            for (int t = 0; t < 2; t++)
            {
                ref var f = ref Fighter(t);
                if (Math.Abs(f.x - x) <= r && f.y <= 120 * Sub)
                    EnvHit(t, dmg, 24, 30, HitFlags.Launch | HitFlags.Unblockable | HitFlags.Fire, x);
            }
            ApplyElements(new Box(x - r, x + r, 0, 140 * Sub), HitFlags.Fire, owner);
        }

        /// <summary>Vật hết độ bền: vật nổ thì nổ; vật chứa chất lỏng đổ ra thành vũng.</summary>
        void BreakProp(int k, int owner)
        {
            ref var p = ref State.props[k];
            var d = Props[p.dataIndex];
            if ((d.traits & PropTraits.Explosive) != 0) { Explode(k, owner); return; }
            Emit(SimEventKind.PropBreak, p.x, p.y + 12 * Sub, p.vx >= 0 ? 1 : -1, k);
            p.phase = PropPhase.Broken;
            p.owner = -1;
            p.y = 0;
            p.vx = p.vy = 0;
            if ((d.traits & PropTraits.Liquid) != 0)
                SpawnEnv(EffectKind.Puddle, p.x, (d.traits & PropTraits.Flammable) != 0);
        }

        int SpawnEnv(EffectKind kind, int x, bool flammable)
        {
            int slot = FreeEffectSlot();
            if (slot < 0) return -1;
            var e = new EffectState { active = true, kind = kind, owner = -1, skill = -1, x = Math.Clamp(x, MinX, MaxX), flammable = flammable };
            var s = SpecOf(e);
            e.y = s.halfH * Sub;
            e.lifetime = s.lifetime;
            State.effects[slot] = e;
            return slot;
        }

        // ------------------------------------------------------------------ vật dụng
        /// <summary>Vật gần nhất nhân vật idx nhặt (hoặc đẩy) được ngay lúc này; -1 nếu không có. Dùng cho viền sáng.</summary>
        public int NearestPickable(int idx)
        {
            ref var f = ref Fighter(idx);
            var data = Fighters[idx];
            if (f.heldProp >= 0 || !f.Grounded || f.floorY != 0) return -1;   // đứng trên bệ: vật nằm dưới sàn, không với tới
            int best = -1, bestDist = int.MaxValue;
            for (int i = 0; i < State.props.Length; i++)
            {
                ref var p = ref State.props[i];
                if (p.phase != PropPhase.Floor) continue;
                int d = Math.Abs(p.x - f.x);
                if (d > PickupRange || d >= bestDist) continue;
                var inter = data.FindInteraction(Props[p.dataIndex].id);
                if (inter == null) continue;
                if (!inter.CanPickUp && !(inter.push != null && inter.push.IsValid)) continue;
                best = i;
                bestDist = d;
            }
            return best;
        }

        void TryPickup(int idx, ref FighterState f, FighterData data)
        {
            int best = NearestPickable(idx);
            if (best < 0) return;
            ref var prop = ref State.props[best];
            var it = data.FindInteraction(Props[prop.dataIndex].id);
            if (!it.CanPickUp)
            {
                // vật Siêu lớn (xe máy): chỉ đẩy – lăn ngang màn hình
                StartAction(ref f, FighterAction.Push);
                f.actionProp = best;
                prop.phase = PropPhase.Held;            // ẩn trong lúc clip "day" chạy
                prop.owner = idx;
                return;
            }
            StartAction(ref f, FighterAction.Pickup);
            f.heldProp = best;
            f.actionProp = best;
            prop.phase = PropPhase.Held;
            prop.owner = idx;
        }

        static void StartToss(ref FighterState f, PropInteraction inter)
        {
            if (inter?.toss == null || !inter.toss.IsValid) return;
            StartAction(ref f, FighterAction.Toss);
            f.actionProp = f.heldProp;
        }

        void ReleaseProp(int idx, ref FighterState f)
        {
            if (f.heldProp < 0) return;
            ref var p = ref State.props[f.heldProp];
            var d = Props[p.dataIndex];
            bool big = d.size >= PropSize.Large;
            p.phase = PropPhase.Flying;
            p.owner = idx;
            p.x = f.x + f.facing * 30 * Sub;
            p.y = f.y + 70 * Sub;
            p.vx = f.facing * (big ? 4 : 7) * Sub;
            p.vy = (big ? 4 : 2) * Sub;
            p.facing = f.facing;
            f.heldProp = -1;
            if (Fighters[idx].throwsIgnite) p.burnTicks = Math.Max(p.burnTicks, BurnTicks);   // Mai: vật ném ra đều bốc cháy
        }

        /// <summary>Cờ thêm vào đòn đánh/ném bằng vật dụng theo trạng thái của nó.</summary>
        static HitFlags StatusFlags(in PropState p) =>
            (p.burnTicks > 0 ? HitFlags.Fire : HitFlags.None) | (p.shockTicks > 0 ? HitFlags.Electric : HitFlags.None);

        void LaunchPushed(int idx, ref FighterState f)
        {
            if (f.actionProp < 0) return;
            ref var p = ref State.props[f.actionProp];
            p.phase = PropPhase.Flying;
            p.owner = idx;
            p.x = Math.Clamp(f.x + f.facing * 40 * Sub, MinX, MaxX);
            p.y = 0;
            p.vx = f.facing * 6 * Sub;
            p.vy = 0;
            p.facing = f.facing;
        }

        void SwingHit(int idx, ref FighterState f, ref FighterState o, PropData held)
        {
            int dx = (o.x - f.x) * f.facing;
            if (dx < 0 || dx > SwingReach || Math.Abs(o.y - f.y) > Fighters[1 - idx].hurtHeight * Sub / 2) return;
            ref var p = ref State.props[f.heldProp];
            var flags = (held.weaponType == WeaponType.Heavy ? HitFlags.Knockdown : HitFlags.None) | StatusFlags(p);
            if (!TryHit(idx, held.hitDamage, 20, 12, flags)) return;
            if (--p.durability <= 0)
            {
                int k = f.heldProp;
                f.heldProp = -1;
                BreakProp(k, idx);
            }
        }

        void StepProp(int i)
        {
            ref var p = ref State.props[i];
            if (p.burnTicks > 0) p.burnTicks--;
            if (p.shockTicks > 0) p.shockTicks--;
            if (p.hitCooldown > 0) p.hitCooldown--;
            if (p.phase != PropPhase.Flying) return;
            var d = Props[p.dataIndex];
            bool explosive = (d.traits & PropTraits.Explosive) != 0;
            p.x += p.vx;
            bool rolling = d.size == PropSize.ExtraLarge;          // xe máy lăn sát đất
            if (!rolling)
            {
                p.vy -= Gravity / 2;
                p.y += p.vy;
            }
            // trúng đối thủ của người ném – vật Lớn phá thế đỡ (GDD: ném vật thắng đỡ)
            if (p.owner >= 0)
            {
                int target = 1 - p.owner;
                if (PropBox(p).Overlaps(HurtBox(target)))
                {
                    var flags = (d.size >= PropSize.Large ? HitFlags.Unblockable | HitFlags.Knockdown : HitFlags.None) | StatusFlags(p);
                    int thrower = p.owner;
                    if (TryHit(thrower, d.throwDamage, 20, 14, flags))
                    {
                        // vật nổ: nổ ngay khi trúng (bình gas "ném 60 + nổ 150", GDD mục 4)
                        if (explosive) { Explode(i, thrower); return; }
                        p.owner = -1;
                        p.vx = -p.vx / 4;
                        p.durability--;
                    }
                }
            }
            bool landed = !rolling && p.y <= 0;
            bool outOfBounds = p.x <= MinX || p.x >= MaxX;
            if (landed || (rolling && (outOfBounds || Math.Abs(p.vx) < Sub)))
            {
                int thrower = p.owner;
                p.y = 0;
                p.vy = 0;
                p.vx = 0;
                p.owner = -1;
                // ném vật nổ xuống đất: va đập → nổ; xe máy chỉ lăn rồi dừng
                if (landed && explosive) { Explode(i, thrower); return; }
                // độ bền = số lần TRÚNG (GDD mục 4); rơi xuống sàn không tính, trừ vật Dễ vỡ
                if (p.durability <= 0 || (d.traits & PropTraits.Fragile) != 0) BreakProp(i, thrower);
                else p.phase = PropPhase.Floor;
            }
            if (rolling) p.vx -= Math.Sign(p.vx) * 2;              // ma sát
            p.x = Math.Clamp(p.x, MinX, MaxX);
        }

        /// <summary>Hai nhân vật không đi xuyên nhau khi cùng đứng đất (trừ chiêu lướt xuyên / đang nằm).</summary>
        void Separate()
        {
            ref var a = ref State.p0;
            ref var b = ref State.p1;
            if (!a.Grounded || !b.Grounded || a.floorY != b.floorY) return;
            if (a.action == FighterAction.Knockdown || b.action == FighterAction.Knockdown) return;
            if (a.action == FighterAction.Roll || b.action == FighterAction.Roll) return;
            if (CurrentSkill(0)?.passThrough == true || CurrentSkill(1)?.passThrough == true) return;
            int min = (Fighters[0].hurtHalfWidth + Fighters[1].hurtHalfWidth) * Sub;
            int d = b.x - a.x;
            if (Math.Abs(d) >= min) return;
            int push = (min - Math.Abs(d) + 1) / 2;
            int s = d >= 0 ? 1 : -1;
            a.x = Math.Clamp(a.x - s * push, MinX, MaxX);
            b.x = Math.Clamp(b.x + s * push, MinX, MaxX);
        }
    }
}
