using System;
using UnityEngine;

namespace LoanDau.Data
{
    /// <summary>Cỡ vật dụng (GDD mục 4) – quyết định cách cầm và ném.</summary>
    public enum PropSize { Small, Medium, Large, ExtraLarge }

    /// <summary>Loại vũ khí khi vung (GDD mục 4). None = chỉ ném.</summary>
    public enum WeaponType { None, Blunt, Long, Heavy }

    /// <summary>Thuộc tính phản ứng dây chuyền (GDD mục 4).</summary>
    [Flags]
    public enum PropTraits
    {
        None = 0,
        Flammable = 1 << 0,
        Explosive = 1 << 1,
        Metal = 1 << 2,
        Liquid = 1 << 3,
        Fragile = 1 << 4,
    }

    /// <summary>
    /// Một vật dụng (GDD mục 4 + 7). Tạo bởi menu Loan Dau → PixelPack → Dựng dữ liệu + scene.
    /// Nhóm "Cân bằng" chỉ điền lần đầu từ catalog.json, sau đó designer chỉnh trong Inspector.
    /// </summary>
    [CreateAssetMenu(menuName = "Loan Dau/Prop Data", fileName = "Prop_")]
    public class PropData : ScriptableObject
    {
        public string id;
        public string displayName;
        [Tooltip("Vật nằm sàn / đang bay, đúng tỷ lệ nhân vật (PixelPackDerived/FloorProps/<id>.png)")] public Sprite sprite;
        [Tooltip("Khi hết độ bền, cùng tỷ lệ (PixelPackDerived/FloorProps/<id>_damaged.png)")] public Sprite damagedSprite;
        [Tooltip("Ảnh lớn gốc PixelPack/Props/<id>.png – dùng cho UI (chọn đồ, gợi ý), không đặt lên sàn")] public Sprite icon;
        [Tooltip("Viền sáng khi người chơi đứng đủ gần để nhặt (GDD mục 1)")] public Sprite outline;
        [Tooltip("Mảnh vụn văng ra khi vỡ (PixelPack/Debris, cùng tỷ lệ sprite sàn)")] public Sprite[] debris = Array.Empty<Sprite>();

        [Header("Cân bằng (GDD mục 4)")]
        public PropSize size = PropSize.Medium;
        public WeaponType weaponType = WeaponType.None;
        public PropTraits traits = PropTraits.None;
        [Min(0)] public int hitDamage;
        [Min(0)] public int throwDamage;
        [Tooltip("Sát thương nổ thêm (bình gas, xe máy)")] [Min(0)] public int explodeDamage;
        [Min(1)] public int durability = 1;

        [Header("Hiển thị")]
        [Tooltip("Hệ số phóng sprite trên sàn (1 = pixel-perfect). Sprite đã đúng tỷ lệ, chỉ chỉnh khi thật cần.")]
        [Range(0.25f, 2f)] public float floorScale = 1f;

        /// <summary>Tốc độ di chuyển khi cầm (GDD mục 4), tính theo phần trăm.</summary>
        public int MoveSpeedPercent => size switch
        {
            PropSize.Small => 100,
            PropSize.Medium => 90,
            PropSize.Large => 60,
            _ => 40,
        };
    }
}
