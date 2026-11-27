using System;
using UnityEngine;

namespace LoanDau.Data
{
    /// <summary>Mối nguy môi trường của stage (GDD mục 5).</summary>
    public enum HazardKind : byte
    {
        None,
        Motorbike,  // Chợ Đêm: xe máy chạy ngang, biển báo trước 2 giây
        Crane,      // Công Trường: cần cẩu thả gạch xuống chỗ có bóng đổ
        Burner,     // Bếp Quán Lẩu: bếp lửa bùng lên theo chu kỳ
        PowerBox,   // Sân Thượng: mưa làm trơn sàn, tủ điện bị đánh thì phóng điện
    }

    /// <summary>Một stage (GDD mục 5). Ảnh nền PixelPack/Stages/&lt;id&gt;.png (768×432 px = 12 × 6.75 unit ở PPU 64).</summary>
    [CreateAssetMenu(menuName = "Loan Dau/Stage Data", fileName = "Stage_")]
    public class StageData : ScriptableObject
    {
        [Serializable]
        public class PropSpawn
        {
            public PropData prop;
            [Tooltip("Pixel tính từ giữa sân; đặt đối xứng hai nửa sân (GDD mục 5)")] public int xPx;
        }

        /// <summary>Bệ nhảy một chiều (ArtSource/PixelPack/stage-platforms.json + StageModules/&lt;id&gt;/*.png).</summary>
        [Serializable]
        public class Platform
        {
            public string name;
            [Tooltip("Mép trái / phải mặt bệ, px từ giữa sân")] public int x1Px, x2Px;
            [Tooltip("Mặt bệ cao hơn mặt sàn bao nhiêu px")] public int topPx;
            [Tooltip("Vẽ riêng sprite bệ (khi bệ được hạ thấp hơn chỗ vẽ trên ảnh nền); null = bệ nằm sẵn trong ảnh nền")]
            public Sprite art;
            [Tooltip("Vị trí vẽ sprite: tâm đáy ảnh, px từ giữa sân / từ mặt sàn")] public int artXPx, artBottomPx;
        }

        public string id;
        public string displayName;
        public Sprite background;
        [Tooltip("Mặt sàn (chân nhân vật) cách mép dưới ảnh nền bao nhiêu pixel")] public int floorFromBottomPx = 60;
        [Tooltip("Lề hai bên: nhân vật không đi sát mép ảnh")] public int sideMarginPx = 40;
        [Tooltip("Vị trí xuất phát của P1/P2 (±px từ giữa sân)")] public int playerStartPx = 110;
        [Tooltip("Phiên bản bố cục vật dụng do builder đặt; builder chỉ ghi đè khi bố cục mặc định mới hơn")]
        public int propLayoutVersion;
        [TextArea] public string layout;
        [TextArea] public string hazard;
        [Header("Mối nguy môi trường")]
        public HazardKind hazardKind;
        [Tooltip("PixelPack/Hazards/<id>/hazard.png – 8 frame, pivot chân (80, 160)")] public SpriteClip hazardArt;
        [Tooltip("Lớp mưa phủ màn hình (StageModules/santhuong/rain.png)")] public Sprite rainOverlay;
        [Tooltip("Vị trí vật cố định của mối nguy (bếp, tủ điện), px từ giữa sân")] public int hazardXPx;
        public string homeFighterId;
        public Sprite[] warningFrames = Array.Empty<Sprite>();
        public PropSpawn[] props = Array.Empty<PropSpawn>();
        public Platform[] platforms = Array.Empty<Platform>();
    }
}
