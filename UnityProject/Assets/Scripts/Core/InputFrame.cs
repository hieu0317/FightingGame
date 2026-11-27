using System;

namespace LoanDau.Core
{
    /// <summary>Nút của một khung input (GDD mục 2: 7 nút).</summary>
    [Flags]
    public enum Buttons : ushort
    {
        None = 0,
        Left = 1 << 0,
        Right = 1 << 1,
        Up = 1 << 2,      // nhảy
        Down = 1 << 3,    // cúi
        Light = 1 << 4,   // J – đánh nhẹ / vung vật đang cầm
        Heavy = 1 << 5,   // K – đánh mạnh (K + L = tuyệt chiêu)
        Skill = 1 << 6,   // L – kỹ năng
        Grab = 1 << 7,    // U – nhặt / ném
        Guard = 1 << 8,   // I – đỡ
    }

    /// <summary>
    /// Một khung input mỗi tick (GDD mục 6). Người chơi, AI và đối thủ mạng đều chỉ là
    /// 3 cách tạo ra InputFrame; mô phỏng không biết ai đang điều khiển.
    /// </summary>
    [Serializable]
    public struct InputFrame : IEquatable<InputFrame>
    {
        public Buttons buttons;

        public InputFrame(Buttons b) { buttons = b; }
        public bool Has(Buttons b) => (buttons & b) != 0;
        public bool Equals(InputFrame o) => buttons == o.buttons;
        public override bool Equals(object obj) => obj is InputFrame o && Equals(o);
        public override int GetHashCode() => (int)buttons;
    }
}
