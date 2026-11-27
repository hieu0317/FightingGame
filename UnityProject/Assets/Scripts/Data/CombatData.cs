using System;
using UnityEngine;

namespace LoanDau.Data
{
    /// <summary>Cách một chiêu gây sát thương (GDD mục 3).</summary>
    public enum SkillKind
    {
        Strike,      // đánh tầm gần theo các nhịp trong animation
        Dash,        // lao tới (dịch chuyển trong lúc ra đòn)
        Projectile,  // bắn đạn / sóng
        Grab,        // vật – không đỡ được
        Counter,     // thế chờ phản đòn
        Trap,        // đặt bẫy
        Field,       // vùng ảnh hưởng lớn (tuyệt chiêu cả sân)
    }

    /// <summary>Thuộc tính của một đòn.</summary>
    [Flags]
    public enum HitFlags
    {
        None = 0,
        Unblockable = 1 << 0,   // không đỡ được (vật, Đòn Chân Kẹp)
        Low = 1 << 1,           // phải đỡ thấp (giữ ↓ + đỡ)
        Overhead = 1 << 2,      // phải đỡ đứng
        Knockdown = 1 << 3,     // làm ngã
        Launch = 1 << 4,        // hất tung
        AirOnly = 1 << 5,       // chỉ trúng đối thủ đang ở trên không
        GroundOnly = 1 << 6,    // chỉ trúng đối thủ đang đứng đất
        Reflect = 1 << 7,       // hất ngược đạn / vật ném
        Homing = 1 << 8,        // trúng ở bất kỳ đâu trên sân (tuyệt chiêu)
        Steal = 1 << 9,         // cướp vật đối thủ đang cầm
        Pull = 1 << 10,         // kéo đối thủ về phía mình
        Stun = 1 << 11,         // choáng thêm 0.5 giây
        SafeNearOwner = 1 << 12,// vùng không gây sát thương gần người dùng (Lẩu Địa Ngục)
        Fire = 1 << 13,         // lửa: đốt vật Dễ cháy, kích nổ Chất nổ, gây bỏng; dầu/sơn → vùng cháy (GDD mục 4)
        Electric = 1 << 14,     // điện: nhiễm điện vật Kim loại, vũng nước → vùng giật, tê thêm 0.3 giây
        GuardCrush = 1 << 15,   // phá thế đỡ: trừ thanh đỡ gấp 3 (đánh mạnh K, GDD mục 2)
    }

    /// <summary>
    /// Một cửa sổ trúng đòn: hộp tính bằng pixel từ điểm chân, x dương = phía trước mặt, y lên trên.
    /// Thời gian tính theo tick của clip chiêu (60 tick/giây).
    /// </summary>
    [Serializable]
    public class HitWindow
    {
        public int startTick, endTick;
        public int x0, x1, y0, y1;
        [Min(0)] public int damage;
        [Tooltip("Số tick đối thủ bị khống chế khi trúng")] [Min(0)] public int hitstun = 18;
        [Tooltip("Đẩy lùi đối thủ (px)")] public int pushPx = 10;
        public HitFlags flags;
    }

    /// <summary>Đạn / bẫy / vùng do chiêu tạo ra.</summary>
    [Serializable]
    public class EffectSpec
    {
        public bool enabled;
        [Tooltip("Tick của clip chiêu lúc tạo ra")] public int spawnTick;
        [Tooltip("Vị trí sinh so với chân (px, x theo hướng mặt)")] public int offsetX = 40, offsetY = 50;
        [Tooltip("1/16 px mỗi tick")] public int speed, vy, gravity;
        public int halfW = 10, halfH = 10;
        [Min(0)] public int damage;
        public int hitstun = 18;
        public int pushPx = 10;
        public HitFlags flags;
        [Min(1)] public int lifetime = 90;
        [Tooltip("0 = trúng một lần rồi biến mất; >0 = gây sát thương lặp lại mỗi N tick")] public int hitInterval;
        [Tooltip(">0: bẫy bắn tia về phía đối thủ mỗi N tick")] public int emitInterval;
        [Min(1)] public int count = 1;
        [Tooltip("Độ toả theo chiều dọc giữa các viên (1/16 px mỗi tick)")] public int spreadVy;
        [Tooltip(">0: khi chạm đất biến thành vùng tồn tại N tick")] public int landFieldTicks;
        [Tooltip("Tạo ở phía đối thủ thay vì trước mặt người dùng (Sập Giàn Giáo)")] public bool atOpponent;
        public Sprite[] frames = Array.Empty<Sprite>();
        [Tooltip("Hệ số phóng hình khi vẽ (vùng lớn)")] public float drawScale = 1f;
    }
}
