using System;
using UnityEngine;

namespace LoanDau.Art
{
    /// <summary>Một mục trong &lt;nhóm&gt;.sprites.json do Blender (sprite_utils.py) xuất ra.</summary>
    [Serializable]
    public class SpriteMeta
    {
        public string file;
        public string role;          // rest / held / piece / dented / idle / bg / mid / play / fg
        public int[] size_px;
        public float ppu;
        public float[] pivot;        // 0..1, gốc dưới-trái
        public float[] size_m;
        public float[] offset_m;     // mảnh vỡ: vị trí so với pivot sprite gốc (mét)
        public float[] grip_px;      // prop nằm sàn: điểm cầm (px, gốc dưới-trái)
        public float[] hand_r_px;    // nhân vật: socket tay
        public float[] hand_l_px;

        public Vector2 Pivot => pivot is { Length: 2 } ? new Vector2(pivot[0], pivot[1]) : new Vector2(0.5f, 0f);

        /// <summary>Đổi toạ độ px (gốc dưới-trái) sang mét, tương đối với pivot của sprite.</summary>
        public Vector2 PixelToLocal(float[] px) => px is { Length: 2 } && size_px is { Length: 2 }
            ? new Vector2(px[0] - Pivot.x * size_px[0], px[1] - Pivot.y * size_px[1]) / ppu
            : Vector2.zero;
    }

    [Serializable]
    public class SpriteMetaFile
    {
        public string key;
        public float ppu;
        public float tilt_deg;
        public string facing;
        public string prop_size, weapon_type, traits;
        public int hit_dmg, throw_dmg, durability;
        public SpriteMeta[] sprites;

        public static SpriteMetaFile Parse(string json) => JsonUtility.FromJson<SpriteMetaFile>(json);

        public SpriteMeta Find(string fileName) =>
            sprites == null ? null : Array.Find(sprites, s => string.Equals(s.file, fileName, StringComparison.OrdinalIgnoreCase));
    }
}
