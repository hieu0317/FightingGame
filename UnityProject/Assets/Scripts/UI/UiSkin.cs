using System;
using UnityEngine;

namespace LoanDau.UI
{
    /// <summary>Hình + font cho HUD và màn chọn nhân vật (PixelPack/UI). Tạo bởi PixelPackBuilder.</summary>
    [CreateAssetMenu(menuName = "Loan Dau/UI Skin", fileName = "UiSkin")]
    public class UiSkin : ScriptableObject
    {
        public Font font;                                   // Inter SemiBold – đủ dấu tiếng Việt
        public Sprite logo;
        public Sprite hpFrame, hpFill, energyFrame, energyFill, guardFrame, guardFill;
        public Sprite roundEmpty, roundWin;
        public Color panel = new(0.07f, 0.07f, 0.12f, 0.92f);
        public Color text = new(1f, 0.95f, 0.8f);
        public Color accent = new(1f, 0.82f, 0.25f);
    }
}
