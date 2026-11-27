using System;
using UnityEngine;

namespace LoanDau.Data
{
    /// <summary>
    /// Bố cục 2D của một stage (&lt;stage&gt;_layout.json do render_stage_layers.py xuất).
    /// Đơn vị mét trên mặt phẳng game: x ngang, y = độ cao đã chiếu; sàn ở y = 0.
    /// Dùng cho mô phỏng tất định (AABB) – không phụ thuộc Physics2D.
    /// </summary>
    [Serializable]
    public class StageLayout2D
    {
        [Serializable] public class Arena { public float x_min, x_max, floor_y; }
        [Serializable] public class Platform { public string name; public float x_min, x_max, top_y; public bool one_way; }
        [Serializable] public class Wall { public string name; public float x_min, x_max; }
        [Serializable] public class PlayerSpawn { public int player; public float x, y; public string facing; }
        [Serializable] public class PropSpawn { public string name; public float x, y; public string size, default_prop; }
        [Serializable] public class Hazard { public string name; public float x, y, interval_s; }
        [Serializable] public class Layer { public string file; public int order; public float parallax; }

        public string stage;
        public float ppu, tilt_deg;
        public Arena arena;
        public Platform[] platforms;
        public Wall[] walls;
        public PlayerSpawn[] player_spawns;
        public PropSpawn[] prop_spawns;
        public Hazard[] hazards;
        public Layer[] layers;

        public static StageLayout2D Parse(TextAsset json) => JsonUtility.FromJson<StageLayout2D>(json.text);
    }
}
