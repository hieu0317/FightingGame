using System;
using LoanDau.Data;

namespace LoanDau.Core
{
    /// <summary>Hình của mối nguy ở tick hiện tại (lớp hiển thị chỉ đọc).</summary>
    public struct HazardView
    {
        public int frame;     // chỉ số frame trong StageData.hazardArt; -1 = không vẽ
        public int x;         // điểm chân (pivot) của sprite, đơn vị mô phỏng
        public int dir;       // +1 vẽ như gốc, -1 lật ngang
        public bool warn;     // đang báo trước (biển báo xe máy nhấp nháy)
        public int warnX;     // phía báo trước: -1 mép trái, +1 mép phải
    }

    /// <summary>
    /// Mối nguy môi trường theo stage (GDD mục 5), tất định, trạng thái nằm trong MatchState.
    /// Chỉ chạy khi HazardsEnabled (MatchRunner bật; bài test cũ để tắt để không bị ảnh hưởng).
    /// </summary>
    public sealed partial class FightSim
    {
        public readonly HazardKind Hazard;
        /// <summary>Vị trí vật cố định của mối nguy (bếp, tủ điện).</summary>
        public readonly int HazardHomeX;
        readonly int stageEdge;
        public bool HazardsEnabled;

        // ---- Chợ Đêm: xe máy chạy ngang mỗi 30 giây, biển báo trước 2 giây
        public const int BikeFirst = 20 * TickRate, BikeInterval = 30 * TickRate, BikeWarn = 2 * TickRate;
        public const int BikeSpeed = 10 * Sub, BikeDamage = 90;
        // ---- Công Trường: cần cẩu thả gạch xuống chỗ có bóng đổ (nhắm luân phiên P1 / P2)
        public const int CraneFirst = 12 * TickRate, CraneInterval = 18 * TickRate;
        public const int CraneWarn = 90, CraneImpact = 102, CraneEnd = 150, CraneHalfW = 28, CraneDamage = 120;
        // ---- Bếp Quán Lẩu: bếp bùng lửa theo chu kỳ, đốt người và vật Dễ cháy gần đó
        public const int BurnerFirst = 8 * TickRate, BurnerInterval = 12 * TickRate;
        public const int BurnerWarn = 60, BurnerFlare = 90, BurnerFade = 20, BurnerHalfW = 36, BurnerDamage = 25;
        // ---- Sân Thượng: mưa làm trơn cả sân; tủ điện bị đánh trúng → phóng điện (mưa thì lan xa hơn)
        public const int RainFirst = 10 * TickRate, RainLength = 20 * TickRate, RainGap = 15 * TickRate;
        public const int BoxCharge = 8, BoxDischarge = 48, BoxRecharge = 4 * TickRate, BoxDamage = 60;
        public const int BoxRadiusPx = 90, BoxRadiusRainPx = 130;

        public bool Raining => State.rainTicks > 0;

        /// <summary>Độ mưa 0..1 (mờ dần đầu / cuối cơn mưa) – cho lớp hiển thị.</summary>
        public float RainLevel => State.rainTicks <= 0 ? 0f
            : Math.Min(1f, Math.Min(State.rainTicks, RainLength - State.rainTicks) / 60f);

        void ResetHazard()
        {
            State.hazardAge = -1;
            State.hazardHitMask = 0;
            State.hazardCooldown = 0;
            State.rainTicks = 0;
            State.hazardX = HazardHomeX;
            State.hazardDir = 1;
            State.hazardTimer = Hazard switch
            {
                HazardKind.Motorbike => BikeFirst,
                HazardKind.Crane => CraneFirst,
                HazardKind.Burner => BurnerFirst,
                _ => 0,
            };
            State.rainTimer = RainFirst;
        }

        void StepHazard()
        {
            switch (Hazard)
            {
                case HazardKind.Motorbike: StepBike(); break;
                case HazardKind.Crane: StepCrane(); break;
                case HazardKind.Burner: StepBurner(); break;
                case HazardKind.PowerBox: StepPowerBox(); break;
            }
        }

        /// <summary>Bắt đầu lượt mới khi hết thời gian nghỉ; trả về true nếu đang diễn ra.</summary>
        bool Advance(int interval)
        {
            if (State.hazardAge < 0)
            {
                if (--State.hazardTimer > 0) return false;
                State.hazardAge = 0;
                State.hazardHitMask = 0;
                State.hazardTimer = interval;
                return true;
            }
            State.hazardAge++;
            return true;
        }

        void Finish()
        {
            State.hazardAge = -1;
            State.hazardCount++;
        }

        void HazardHit(int t, in Box box, int damage, int hitstun, int pushPx, HitFlags flags, int sourceX)
        {
            if ((State.hazardHitMask & (1 << t)) != 0 || !box.Overlaps(HurtBox(t))) return;
            if (EnvHit(t, damage, hitstun, pushPx, flags, sourceX)) State.hazardHitMask |= 1 << t;
        }

        // ------------------------------------------------------------------ Chợ Đêm
        void StepBike()
        {
            if (!Advance(BikeInterval)) return;
            int age = State.hazardAge;
            if (age == 0)
            {
                State.hazardDir = State.hazardCount % 2 == 0 ? 1 : -1;      // lượt sau chạy chiều ngược lại
                State.hazardX = -State.hazardDir * (stageEdge + 90 * Sub);
                Emit(SimEventKind.HazardWarn, -State.hazardDir * stageEdge, 0, State.hazardDir);
            }
            if (age < BikeWarn) return;
            if (age == BikeWarn) Emit(SimEventKind.HazardStrike, State.hazardX, 0, State.hazardDir);
            int dir = State.hazardDir;
            State.hazardX += dir * BikeSpeed;
            // sprite nhìn sang phải, bánh trước ở pivot, thân xe nằm sau pivot ~78 px
            int front = State.hazardX + dir * 4 * Sub, back = State.hazardX - dir * 78 * Sub;
            var box = new Box(Math.Min(front, back), Math.Max(front, back), 0, 44 * Sub);
            for (int t = 0; t < 2; t++)
                HazardHit(t, box, BikeDamage, 30, 40, HitFlags.Knockdown | HitFlags.Unblockable, State.hazardX - dir * 200 * Sub);
            // vật nằm trên đường: vật dễ vỡ vỡ, vật nổ thì nổ
            for (int k = 0; k < State.props.Length; k++)
            {
                ref var p = ref State.props[k];
                if (p.phase != PropPhase.Floor || !PropBox(p).Overlaps(box)) continue;
                var traits = Props[p.dataIndex].traits;
                if ((traits & PropTraits.Explosive) != 0) Explode(k, -1);
                else if ((traits & PropTraits.Fragile) != 0) BreakProp(k, -1);
            }
            if (dir * State.hazardX > stageEdge + 90 * Sub) Finish();
        }

        // ------------------------------------------------------------------ Công Trường
        void StepCrane()
        {
            if (!Advance(CraneInterval)) return;
            int age = State.hazardAge;
            if (age == 0)
            {
                ref var target = ref Fighter(State.hazardCount % 2);
                State.hazardX = Math.Clamp(target.x, MinX, MaxX);
                Emit(SimEventKind.HazardWarn, State.hazardX, 0);
            }
            if (age == CraneImpact)
            {
                int x = State.hazardX;
                var box = new Box(x - CraneHalfW * Sub, x + CraneHalfW * Sub, 0, 80 * Sub);
                Emit(SimEventKind.HazardStrike, x, 0);
                Emit(SimEventKind.Knockdown, x, 0);                         // bụi
                for (int t = 0; t < 2; t++)
                    HazardHit(t, box, CraneDamage, 30, 20, HitFlags.Knockdown | HitFlags.Unblockable, x);
                for (int k = 0; k < State.props.Length; k++)
                {
                    ref var p = ref State.props[k];
                    if (p.phase != PropPhase.Floor || !PropBox(p).Overlaps(box)) continue;
                    var traits = Props[p.dataIndex].traits;
                    if ((traits & PropTraits.Explosive) != 0) Explode(k, -1);
                    else if ((traits & (PropTraits.Fragile | PropTraits.Liquid)) != 0) BreakProp(k, -1);
                }
            }
            if (age >= CraneEnd) Finish();
        }

        // ------------------------------------------------------------------ Bếp Quán Lẩu
        void StepBurner()
        {
            if (!Advance(BurnerInterval)) return;
            int age = State.hazardAge;
            int x = HazardHomeX;
            if (age == 0) Emit(SimEventKind.HazardWarn, x, 0);
            if (age == BurnerWarn) Emit(SimEventKind.HazardStrike, x, 40 * Sub);
            if (age >= BurnerWarn && age < BurnerWarn + BurnerFlare)
            {
                if ((age - BurnerWarn) % 30 == 0) State.hazardHitMask = 0;  // đứng trong lửa: trúng lại mỗi 0.5 giây
                var flame = new Box(x - BurnerHalfW * Sub, x + BurnerHalfW * Sub, 0, 96 * Sub);
                for (int t = 0; t < 2; t++)
                    HazardHit(t, flame, BurnerDamage, 14, 16, HitFlags.Fire, x);
                // chỉ đốt vật Dễ cháy (GDD) – bình gas đặt giữa bếp không bị kích nổ mỗi chu kỳ
                var reach = new Box(x - 60 * Sub, x + 60 * Sub, 0, 110 * Sub);
                for (int k = 0; k < State.props.Length; k++)
                {
                    ref var p = ref State.props[k];
                    var traits = Props[p.dataIndex].traits;
                    if (p.phase == PropPhase.Broken || (traits & PropTraits.Flammable) == 0 || (traits & PropTraits.Explosive) != 0) continue;
                    if (PropBox(p).Overlaps(reach)) p.burnTicks = Math.Max(p.burnTicks, BurnTicks);
                }
            }
            if (age >= BurnerWarn + BurnerFlare + BurnerFade) Finish();
        }

        // ------------------------------------------------------------------ Sân Thượng
        Box PowerBoxBox => new(HazardHomeX - 18 * Sub, HazardHomeX + 18 * Sub, 0, 64 * Sub);

        /// <summary>Đòn đánh chạm tủ điện → tủ phóng điện (gọi từ ResolveStrikes).</summary>
        void StrikePowerBox(in Box strike)
        {
            if (!HazardsEnabled || Hazard != HazardKind.PowerBox || State.hazardAge >= 0 || State.hazardCooldown > 0) return;
            if (!strike.Overlaps(PowerBoxBox)) return;
            State.hazardAge = 0;
            State.hazardHitMask = 0;
            Emit(SimEventKind.HazardWarn, HazardHomeX, 40 * Sub);
        }

        void StepPowerBox()
        {
            // trời: khô → mưa → khô…
            if (State.rainTicks > 0)
            {
                if (--State.rainTicks == 0) State.rainTimer = RainGap;
            }
            else if (--State.rainTimer <= 0) State.rainTicks = RainLength;

            if (State.hazardCooldown > 0) State.hazardCooldown--;
            if (State.hazardAge < 0) return;
            int age = ++State.hazardAge;
            int x = HazardHomeX;
            if (age == BoxCharge)
            {
                int r = (Raining ? BoxRadiusRainPx : BoxRadiusPx) * Sub;
                var zone = new Box(x - r, x + r, 0, 110 * Sub);
                Emit(SimEventKind.HazardStrike, x, 40 * Sub);
                for (int t = 0; t < 2; t++)
                    HazardHit(t, zone, BoxDamage, 24, 24, HitFlags.Electric | HitFlags.Stun | HitFlags.Unblockable, x);
                ApplyElements(zone, HitFlags.Electric, -1);              // vũng → vùng giật, kim loại nhiễm điện
            }
            if (age >= BoxDischarge)
            {
                Finish();
                State.hazardCooldown = BoxRecharge;
            }
        }

        // ------------------------------------------------------------------ hiển thị
        /// <summary>Frame + vị trí của sprite mối nguy (StageData.hazardArt, 8 frame) ở tick hiện tại.</summary>
        public HazardView ViewHazard()
        {
            var v = new HazardView { frame = -1, x = State.hazardX, dir = 1 };
            int age = State.hazardAge, t = State.tick;
            switch (Hazard)
            {
                case HazardKind.Motorbike:
                    if (age < 0) break;
                    v.dir = State.hazardDir;
                    if (age < BikeWarn) { v.warn = true; v.warnX = -State.hazardDir; break; }
                    v.frame = 2 + (age / 4) % 6;                               // frame 0–1 có biển báo, 2–7 xe chạy
                    break;
                case HazardKind.Crane:
                    if (age < 0) break;
                    v.warn = age < CraneWarn;
                    v.frame = age < CraneWarn ? age * 4 / CraneWarn
                        : age < CraneImpact ? 4 + Math.Min(2, (age - CraneWarn) / 4)
                        : 7;
                    break;
                case HazardKind.Burner:
                    v.x = HazardHomeX;
                    if (age < 0) v.frame = (t / 20) % 2 == 0 ? 3 : 7;          // nghỉ: tàn lửa nhỏ
                    else if (age < BurnerWarn) { v.warn = true; v.frame = (age / 8) % 2 == 0 ? 0 : 4; }
                    else if (age < BurnerWarn + BurnerFlare) v.frame = new[] { 1, 2, 5, 6 }[(age / 6) % 4];
                    else v.frame = (age / 6) % 2 == 0 ? 3 : 7;
                    break;
                case HazardKind.PowerBox:
                    v.x = HazardHomeX;
                    if (age < 0) v.frame = State.hazardCooldown > 0 ? 7 : new[] { 0, 4, 7 }[(t / 12) % 3];
                    else v.frame = new[] { 1, 2, 5, 6 }[(age / 4) % 4];
                    break;
            }
            return v;
        }
    }
}
