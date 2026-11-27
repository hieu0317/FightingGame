using UnityEngine;

namespace LoanDau.Art
{
    public sealed class BasicMotionGallery : MonoBehaviour
    {
        public RuntimeAnimatorController[] fighters;
        public string[] fighterNames;
        public Animator subject;
        static readonly string[] Motion = { "walk_fwd", "run", "jump", "crouch", "punch", "kick" };
        static readonly string[] Labels = { "Đi bộ", "Chạy", "Nhảy", "Cúi người", "Đấm thường", "Đá thường" };
        int person, motion;

        void Start() => Show();
        void Show()
        {
            subject.runtimeAnimatorController = fighters[person];
            subject.Play(Motion[motion], 0, 0f);
        }

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(16,16,Screen.width-32,96),GUI.skin.box);
            int next = GUILayout.Toolbar(person,fighterNames);
            int nextMotion = GUILayout.Toolbar(motion,Labels);
            if(next!=person || nextMotion!=motion || GUILayout.Button("Phát lại"))
            {
                person=next;
                motion=nextMotion;
                Show();
            }
            GUILayout.EndArea();
        }
    }
}
