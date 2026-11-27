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
    /// Dữ liệu một vật dụng (GDD mục 7). Tự tạo khi import prop_*.fbx.
    /// Nhóm "Cân bằng" chỉ được điền lần đầu từ Blender, sau đó do designer chỉnh trong Inspector;
    /// nhóm "Từ Blender" được ghi đè mỗi lần import lại model.
    /// </summary>
    [CreateAssetMenu(menuName = "Loan Dau/Prop Data", fileName = "PropData_")]
    public class PropData : ScriptableObject
    {
        [Header("Cân bằng (designer chỉnh)")]
        public PropSize size = PropSize.Medium;
        public WeaponType weaponType = WeaponType.None;
        public PropTraits traits = PropTraits.None;
        [Min(0)] public int hitDamage;
        [Min(0)] public int throwDamage;
        [Min(1)] public int durability = 1;

        [Header("Từ Blender (tự cập nhật khi import)")]
        public GameObject model;
        [Tooltip("Vị trí/hướng điểm cầm so với pivot. Pivot = điểm cầm nên thường bằng 0.")]
        public Vector3 gripOffset;
        public Quaternion gripRotation = Quaternion.identity;
        [Tooltip("Hộp va chạm AABB trong không gian prop (lấy từ mesh _col) – dùng cho va chạm tất định.")]
        public Bounds localBounds;
        [Tooltip("Xoay để prop đứng tự nhiên trên sàn (tư thế trong file là tư thế cầm tay).")]
        public Quaternion restRotation = Quaternion.identity;
        [Tooltip("Cộng vào điểm spawn trên sàn khi đặt ở tư thế nằm sàn.")]
        public Vector3 restOffset;
        public int triangleCount;

        public float MoveSpeedMultiplier => size switch
        {
            PropSize.Small => 1f,
            PropSize.Medium => 0.9f,
            PropSize.Large => 0.6f,
            _ => 0.4f,
        };
    }
}
