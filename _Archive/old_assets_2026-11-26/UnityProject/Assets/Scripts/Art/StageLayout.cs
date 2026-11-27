using UnityEngine;

namespace LoanDau.Art
{
    /// <summary>Gắn lên root của stage_*_blockout.fbx: kích thước vùng đấu + danh sách marker.</summary>
    public class StageLayout : MonoBehaviour
    {
        public float arenaWidth = 24f;
        public float arenaDepth = 3f;
        public StageMarker[] markers;
    }
}
