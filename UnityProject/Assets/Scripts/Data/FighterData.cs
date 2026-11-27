using System;
using UnityEngine;

namespace LoanDau.Data
{
    /// <summary>Hướng bấm kèm nút Kỹ năng (GDD mục 2): L / →+L / ↓+L / ↑+L / K+L.</summary>
    public enum SkillInput { Neutral, Forward, Down, Up, Super }

    [Serializable]
    public class CableGripTable
    {
        public Vector2[] ground, rise, fall;
    }

    [Serializable]
    public class SkillData
    {
        public string name;
        public SkillInput input;
        [Min(0)] public int cost;
        [TextArea] public string effect;
        public SpriteClip clip;
        public SpriteClip risingArt, fallingArt, groundCastArt;
        public Sprite worldObject, cableSegment;
        public CableGripTable cableGrips;
        public bool dropsFromAbove;

        [Header("Nối đòn (đòn thường)")]
        [Tooltip("Khi được nối từ đòn trước: bắt đầu clip ở tick này (bỏ đoạn lấy đà từ tư thế đứng)")] public int chainStartTick;
        [Tooltip("Được nối / huỷ sang đòn khác tới tick này; sau đó là đoạn thu đòn (clip *_end ghép sau). 0 = cả clip")] public int chainEndTick;
        [Tooltip("Clip đang dùng hình tạm (chưa có asset riêng – xem ArtSource/PixelPack/SPEC_combo_va_tren_khong.md)")] public bool placeholderArt;

        [Header("Chiến đấu (sinh từ hình vẽ + bảng GDD; chỉnh tự do trong Inspector)")]
        public SkillKind kind;
        [Tooltip("Các nhịp đánh – hộp lấy từ phần tay/chân/hiệu ứng vươn ra trong từng frame")]
        public HitWindow[] hits = Array.Empty<HitWindow>();
        public EffectSpec effectSpec = new();
        [Tooltip("Lao tới: 1/16 px mỗi tick, từ đầu chiêu tới hết nhịp đánh cuối")] public int dashSpeed;
        [Tooltip("Bật nhảy khi ra chiêu (1/16 px mỗi tick)")] public int jumpVelocity;
        [Tooltip("Bất tử từ tick 0 tới tick này")] public int invulnTicks;
        [Tooltip("Siêu giáp: chịu đòn không bị ngắt chiêu")] public bool armor;
        [Tooltip("Đi xuyên qua đối thủ (không bị đẩy ra)")] public bool passThrough;
        [Tooltip("Thế chờ: phản đòn nếu bị đánh trong N tick đầu")] public int counterTicks;
        [Tooltip("Sát thương phản đòn = sát thương nhận × % này")] public int counterPercent = 150;
        [Tooltip("Màn hình tối trong N tick (tuyệt chiêu)")] public int screenDarkTicks;

        public int LastActiveTick => hits.Length == 0 ? 0 : hits[hits.Length - 1].endTick;
    }

    /// <summary>Bộ clip tương tác của một nhân vật với một vật dụng (PixelPack/Interactions/&lt;nv&gt;/&lt;prop&gt;).</summary>
    [Serializable]
    public class PropInteraction
    {
        public string propId;
        public SpriteClip pickup;   // nhat
        public SpriteClip hold;     // cam (lặp)
        public SpriteClip swing;    // vung (chỉ vũ khí Cùn/Dài/Nặng)
        public SpriteClip toss;     // nem
        public SpriteClip push;     // day (xe máy)
        [Tooltip("Frame của clip ném mà vật rời tay (vật trong clip từ frame này trở đi là vẽ sẵn → cắt clip tại đây)")]
        public int releaseFrame = 5;
        [Tooltip("Frame của clip vung mà đòn trúng")]
        public int swingHitFrame = 4;

        public bool CanPickUp => pickup != null && pickup.IsValid;
    }

    /// <summary>Bộ clip động tác chung (PixelPack/BasicActions/&lt;nv&gt;) – Asset Bible mục 4.</summary>
    [Serializable]
    public class MoveSet
    {
        public SpriteClip idle, walkFwd, walkBack, run, crouch, crouchHold;
        public SpriteClip jumpStart, jumpAir, jumpLand;
        public SpriteClip block, blockHit, rollFwd, rollBack;
        public SpriteClip hitLight, hitHeavy, stun, knockdown, getup;
        public SpriteClip pulledFront, pulledBack, pulledRecover, fallingPunch, fallingKick;
        public SpriteClip intro, win, lose, taunt;

        public bool IsValid => idle != null && idle.IsValid;
        public static bool Ok(SpriteClip c) => c != null && c.IsValid;
    }

    /// <summary>Một nhân vật (GDD mục 3). Tạo từ catalog.json + PixelPack bởi PixelPackBuilder.</summary>
    [CreateAssetMenu(menuName = "Loan Dau/Fighter Data", fileName = "Fighter_")]
    public class FighterData : ScriptableObject
    {
        public string id;
        public string displayName;
        public string playstyle;
        public Color color = Color.white;
        public Sprite idle;
        [Tooltip("PixelPack/UI/portrait_<id>.png – HUD + màn chọn nhân vật")] public Sprite portrait;
        [Tooltip("PixelPack/UI/super_<id>.png – banner cắt cảnh khi tung tuyệt chiêu")] public Sprite superArt;

        [Header("Chỉ số (GDD mục 3)")]
        [Min(1)] public int maxHp = 1000;
        [Tooltip("1/16 pixel mỗi tick")] [Min(1)] public int walkSpeed = 32;
        [Tooltip("Vận tốc nhảy, 1/16 pixel mỗi tick")] [Min(1)] public int jumpVelocity = 120;
        public string passiveName;
        [TextArea] public string passiveEffect;
        [TextArea] public string propAffinity;
        [Tooltip("Ái lực (Mai): mọi vật nhân vật này ném ra đều bốc cháy")] public bool throwsIgnite;
        [Tooltip("Ái lực / nội tại (Khang): vật Kim loại trong bán kính này (px) tự nhiễm điện; 0 = không")] public int electrifyRadiusPx;

        [Header("Hurtbox – vùng nhận đòn (px từ chân, đo từ tư thế đứng)")]
        [Min(4)] public int hurtHalfWidth = 18;
        [Min(20)] public int hurtHeight = 100;
        [Tooltip("Phiên bản dữ liệu chiến đấu do builder sinh; builder chỉ ghi đè khi bảng mặc định mới hơn")]
        public int combatVersion;

        public SkillData[] skills = Array.Empty<SkillData>();
        public PropInteraction[] interactions = Array.Empty<PropInteraction>();

        [Header("Động tác chung + đòn thường (BasicActions)")]
        public MoveSet moves = new();
        [Tooltip("0–2 đánh nhẹ J (combo) · 3 đánh mạnh K · 4 J trên không · 5 K trên không · 6 đá bổ ↓+K khi rơi · 7 tiếp đất sau đá bổ")]
        public SkillData[] normals = Array.Empty<SkillData>();
        public const int Light1 = 0, Light3 = 2, Heavy = 3, AirLight = 4, AirHeavy = 5, AirDive = 6, DiveLand = 7;

        public SkillData Normal(int i) => normals != null && i < normals.Length && normals[i]?.clip != null && normals[i].clip.IsValid ? normals[i] : null;

        public int FindSkill(SkillInput input) => Array.FindIndex(skills, s => s.input == input && s.clip != null && s.clip.IsValid);
        public PropInteraction FindInteraction(string propId) => Array.Find(interactions, i => i.propId == propId);
    }
}
