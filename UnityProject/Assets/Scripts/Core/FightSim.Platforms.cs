using System;
using LoanDau.Data;

namespace LoanDau.Core
{
    /// <summary>Mặt bệ một chiều: nhảy xuyên từ dưới lên, đáp xuống từ trên, ↓ + nhảy để xuống.</summary>
    public readonly struct PlatformSpan
    {
        public readonly int x1, x2, top;   // đơn vị mô phỏng; top tính từ mặt sàn
        public PlatformSpan(int x1, int x2, int top) { this.x1 = x1; this.x2 = x2; this.top = top; }
    }

    public sealed partial class FightSim
    {
        public const int DropThroughTicks = 12;       // ↓ + nhảy: bao lâu đi xuyên bệ
        public const int PlatformEdgeGrace = 4 * Sub; // đứng chênh ra mép bệ tối đa 4 px vẫn không rơi

        /// <summary>Bệ của stage (StageData.platforms), không đổi trong trận.</summary>
        public readonly PlatformSpan[] Platforms;

        static PlatformSpan[] BuildPlatforms(StageData stage)
        {
            var src = stage.platforms ?? Array.Empty<StageData.Platform>();
            var list = new System.Collections.Generic.List<PlatformSpan>();
            foreach (var p in src)
                if (p != null && p.topPx > 0 && p.x2Px > p.x1Px)
                    list.Add(new PlatformSpan(p.x1Px * Sub, p.x2Px * Sub, p.topPx * Sub));
            return list.ToArray();
        }

        /// <summary>Chỉ số bệ có mặt trên đúng độ cao y chứa x; -1 nếu không có.</summary>
        int PlatformAt(int x, int y)
        {
            for (int i = 0; i < Platforms.Length; i++)
            {
                var p = Platforms[i];
                if (p.top == y && x >= p.x1 - PlatformEdgeGrace && x <= p.x2 + PlatformEdgeGrace) return i;
            }
            return -1;
        }

        /// <summary>
        /// Độ cao đáp xuống trong tick này (đang rơi qua mặt bệ hoặc chạm sàn); -1 = còn bay.
        /// Bệ chỉ đỡ khi rơi từ trên xuống (prevY ≥ mặt bệ) và không đang ↓ + nhảy xuyên bệ.
        /// </summary>
        int LandingHeight(in FighterState f, int prevY)
        {
            if (f.vy <= 0 && f.dropTicks == 0)
            {
                int best = -1;
                foreach (var p in Platforms)
                    if (prevY >= p.top && f.y <= p.top && f.x >= p.x1 && f.x <= p.x2 && p.top > best) best = p.top;
                if (best > 0) return best;
            }
            return f.y <= 0 ? 0 : -1;
        }

        /// <summary>Nhảy lần 2 giữa không (FighterData.canDoubleJump), đổi hướng bay theo phím đang giữ.</summary>
        static bool TryDoubleJump(ref FighterState f, FighterData data, PropData held, int dir)
        {
            if (!data.canDoubleJump || f.doubleJumpUsed || f.action != FighterAction.Jump) return false;
            if (held != null && held.size > PropSize.Medium) return false;
            f.vy = data.doubleJumpVelocity > 0 ? data.doubleJumpVelocity : data.jumpVelocity;
            if (dir != 0) f.vx = dir * data.walkSpeed;
            f.doubleJumpUsed = true;
            f.airAttackUsed = false;                   // lần nhảy 2 được thêm 1 đòn trên không
            return true;
        }
    }
}
