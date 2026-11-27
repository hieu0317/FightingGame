using System;
using UnityEngine;

namespace LoanDau.Data
{
    /// <summary>
    /// Âm thanh của trận (Assets/Audio/PixelPack): nhạc + nền theo stage, SFX va chạm / vật liệu / môi trường, thoại nhân vật.
    /// Tạo bởi PixelPackBuilder (Assets/Data/AudioLibrary.asset); trong Editor MatchRunner tự nạp nếu chưa gán.
    /// </summary>
    [CreateAssetMenu(menuName = "Loan Dau/Audio Library", fileName = "AudioLibrary")]
    public class AudioLibrary : ScriptableObject
    {
        [Serializable]
        public class StageAudio
        {
            public string stageId;
            public AudioClip music;
            public AudioClip ambience;
        }

        [Serializable]
        public class Voices
        {
            public string fighterId;
            [Tooltip("Voices/<id>/00.wav … 06.wav")] public AudioClip[] lines = Array.Empty<AudioClip>();
        }

        public const string Root = "Assets/Audio/PixelPack";

        [Header("Nhạc / nền")]
        public AudioClip menuMusic;
        public StageAudio[] stages = Array.Empty<StageAudio>();

        [Header("SFX")]
        public AudioClip[] hitLight = Array.Empty<AudioClip>();
        public AudioClip[] hitHeavy = Array.Empty<AudioClip>();
        public AudioClip[] explosion = Array.Empty<AudioClip>();
        public AudioClip[] fire = Array.Empty<AudioClip>();
        public AudioClip[] electric = Array.Empty<AudioClip>();
        public AudioClip metalHit, metalBreak, woodHit, woodBreak, plasticHit, plasticBreak, glassHit, glassBreak;

        [Header("Thoại (chỉ số file trong Voices/<id>/, -1 = không phát)")]
        public Voices[] voices = Array.Empty<Voices>();
        [Tooltip("Tiếng hô khi tung kỹ năng")] public int voiceSkill = 0;
        [Tooltip("Vào trận")] public int voiceIntro = 1;
        [Tooltip("Bị hạ gục")] public int voiceKo = 2;
        [Tooltip("Tung tuyệt chiêu")] public int voiceSuper = 3;
        [Tooltip("Thắng trận")] public int voiceWin = 6;

        public StageAudio ForStage(string id) => Array.Find(stages, s => s.stageId == id);

        public AudioClip[] cues = Array.Empty<AudioClip>();
        public AudioClip Cue(string name) => Array.Find(cues, c => c && c.name == name);

        public AudioClip Voice(string fighterId, int line)
        {
            if (line < 0) return null;
            var v = Array.Find(voices, x => x.fighterId == fighterId);
            return v != null && line < v.lines.Length ? v.lines[line] : null;
        }

#if UNITY_EDITOR
        /// <summary>Nạp toàn bộ âm thanh theo quy ước thư mục PixelPack vào lib (tạo mới nếu null).</summary>
        public static AudioLibrary Populate(AudioLibrary lib, string[] stageIds, string[] fighterIds)
        {
            if (!lib) lib = CreateInstance<AudioLibrary>();
            static AudioClip C(string path) => UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>($"{Root}/{path}.wav");
            static AudioClip[] Set(string name)
            {
                var l = new System.Collections.Generic.List<AudioClip>();
                for (int i = 1; i <= 9; i++) { var c = C($"SFX/{name}{i}"); if (c) l.Add(c); }
                return l.ToArray();
            }
            var cuePaths = UnityEditor.AssetDatabase.FindAssets("t:AudioClip", new[] { Root + "/SFX", Root + "/UI" });
            var cueList = new System.Collections.Generic.List<AudioClip>();
            foreach (var guid in cuePaths)
                cueList.Add(UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid)));
            cueList.Add(C("Ambience/rain"));
            lib.cues = cueList.ToArray();
            lib.menuMusic = C("Music/menu");
            lib.stages = Array.ConvertAll(stageIds, id => new StageAudio { stageId = id, music = C($"Music/{id}"), ambience = C($"Ambience/{id}") });
            lib.hitLight = Set("hit_light");
            lib.hitHeavy = Set("hit_heavy");
            lib.explosion = Set("explosion");
            lib.fire = Set("fire");
            lib.electric = Set("electric");
            lib.metalHit = C("SFX/metal_hit"); lib.metalBreak = C("SFX/metal_break");
            lib.woodHit = C("SFX/wood_hit"); lib.woodBreak = C("SFX/wood_break");
            lib.plasticHit = C("SFX/plastic_hit"); lib.plasticBreak = C("SFX/plastic_break");
            lib.glassHit = C("SFX/glass_hit"); lib.glassBreak = C("SFX/glass_break");
            lib.voices = Array.ConvertAll(fighterIds, id =>
            {
                var l = new System.Collections.Generic.List<AudioClip>();
                for (int i = 0; i < 16; i++) { var c = C($"Voices/{id}/{i:00}"); if (!c) break; l.Add(c); }
                return new Voices { fighterId = id, lines = l.ToArray() };
            });
            return lib;
        }
#endif
    }
}
