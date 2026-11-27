using System;
using UnityEngine;

namespace LoanDau.Data
{
    /// <summary>Hình hiệu ứng môi trường dùng chung (PixelPack/InteractionFX).</summary>
    [CreateAssetMenu(menuName = "Loan Dau/FX Library", fileName = "FxLibrary")]
    public class FxLibrary : ScriptableObject
    {
        public Sprite[] fire = Array.Empty<Sprite>();       // lua
        public Sprite[] electric = Array.Empty<Sprite>();   // dien
        public Sprite[] explosion = Array.Empty<Sprite>();  // no
        public Sprite[] puddle = Array.Empty<Sprite>();     // vung_nuoc

        [Header("CombatFX")]
        public Sprite[] sparkLight = Array.Empty<Sprite>();
        public Sprite[] sparkHeavy = Array.Empty<Sprite>();
        public Sprite[] landingDust = Array.Empty<Sprite>();
        public Sprite[] smoke = Array.Empty<Sprite>();
        public Sprite[] shockRing = Array.Empty<Sprite>();
        public Sprite bop;   // chữ "BỐP!" – đòn mạnh
        public Sprite rac;   // chữ "RẮC!" – vật vỡ
        public Sprite[] scaffoldContact = Array.Empty<Sprite>();   // giàn giáo (Sập Giàn Giáo) chạm đất
        public Sprite[] scaffoldBreak = Array.Empty<Sprite>();     // giàn giáo đổ sụp, tan khi hết thời gian
    }
}
