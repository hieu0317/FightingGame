using System;
using UnityEngine;

namespace LoanDau.Data
{
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
        public string homeFighterId;
        public PropSpawn[] props = Array.Empty<PropSpawn>();
    }
}
