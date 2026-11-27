using System;
using UnityEngine;

namespace LoanDau.Data
{
    /// <summary>
    /// Clip sprite đã quy đổi sang tick (60 tick/giây) từ file .anim của PixelPack.
    /// Mô phỏng chỉ dùng số tick (tất định); hiển thị lấy sprite theo tick hiện tại.
    /// </summary>
    [Serializable]
    public class SpriteClip
    {
        public string name;
        public Sprite[] frames = Array.Empty<Sprite>();
        [Tooltip("Tick bắt đầu của từng frame (frame 0 luôn ở tick 0)")] public int[] startTicks = Array.Empty<int>();
        [Min(1)] public int lengthTicks = 1;
        public bool loop;

        public bool IsValid => frames != null && frames.Length > 0 && startTicks != null && startTicks.Length == frames.Length;

        public int FrameIndexAt(int tick)
        {
            if (!IsValid) return -1;
            if (loop) tick = ((tick % lengthTicks) + lengthTicks) % lengthTicks;
            int idx = 0;
            for (int i = 1; i < startTicks.Length; i++)
                if (tick >= startTicks[i]) idx = i;
                else break;
            return idx;
        }

        public Sprite FrameAt(int tick)
        {
            int i = FrameIndexAt(tick);
            return i < 0 ? null : frames[i];
        }

        /// <summary>Tick bắt đầu frame thứ index (kẹp trong phạm vi).</summary>
        public int TickOfFrame(int index) =>
            IsValid ? startTicks[Mathf.Clamp(index, 0, startTicks.Length - 1)] : 0;
    }
}
