using LoanDau.Core;
using LoanDau.Data;
using UnityEngine;

namespace LoanDau.Game
{
    /// <summary>
    /// Âm thanh của trận: chỉ đọc sự kiện + trạng thái mô phỏng (không ảnh hưởng tính tất định).
    /// Nhạc + nền theo stage, nhạc menu ở màn chọn, SFX va chạm / vật liệu / nổ / lửa / điện / mối nguy, thoại nhân vật.
    /// </summary>
    public sealed class MatchAudio
    {
        public float musicVolume = 0.45f, ambienceVolume = 0.35f, sfxVolume = 0.9f, voiceVolume = 1f;

        readonly AudioLibrary lib;
        readonly AudioSource music, ambience, rain;
        readonly AudioSource[] sfx = new AudioSource[10];
        readonly AudioSource[] voice = new AudioSource[2];
        int nextSfx;
        readonly System.Random rng = new(4321);

        // trạng thái tick trước để phát hiện "vừa bắt đầu" (tung chiêu, bốc cháy, nhiễm điện, kết thúc trận)
        readonly FighterAction[] prevAction = new FighterAction[2];
        readonly int[] prevSkill = new int[2];
        readonly bool[] prevNormal = new bool[2];
        readonly int[] prevBurn = new int[2];
        int[] prevPropBurn = System.Array.Empty<int>(), prevPropShock = System.Array.Empty<int>();
        bool prevDecided;
        string[] fighterIds = { "", "" };

        public MatchAudio(GameObject host, AudioLibrary lib)
        {
            this.lib = lib;
            var go = new GameObject("_Audio");
            go.transform.SetParent(host.transform, false);
            AudioSource Src(bool loop)
            {
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.loop = loop;
                s.spatialBlend = 0f;
                return s;
            }
            music = Src(true);
            ambience = Src(true);
            rain = Src(true);
            for (int i = 0; i < sfx.Length; i++) sfx[i] = Src(false);
            for (int i = 0; i < voice.Length; i++) voice[i] = Src(false);
            // scene mẫu không có AudioListener → gắn vào camera chính (hoặc chính host)
            if (!Object.FindAnyObjectByType<AudioListener>())
                (Camera.main ? Camera.main.gameObject : host).AddComponent<AudioListener>();
        }

        // ------------------------------------------------------------------ nhạc
        void Loop(AudioSource s, AudioClip clip, float volume)
        {
            s.volume = volume;
            if (s.clip == clip && s.isPlaying) return;
            s.Stop();
            s.clip = clip;
            if (clip) s.Play();
        }

        public void Menu()
        {
            rain.Stop();
            Loop(music, lib.menuMusic, musicVolume);
            Loop(ambience, null, 0);
            foreach (var v in voice) v.Stop();
        }

        /// <summary>Vào trận: nhạc + nền của stage, thoại vào trận của P1 rồi P2.</summary>
        public void StartMatch(FightSim sim, StageData stage)
        {
            rain.Stop();
            var sa = lib.ForStage(stage.id);
            Loop(music, sa?.music, musicVolume);
            Loop(ambience, sa?.ambience, ambienceVolume);
            fighterIds = new[] { sim.Fighters[0].id, sim.Fighters[1].id };
            for (int i = 0; i < 2; i++)
            {
                prevAction[i] = FighterAction.Idle;
                prevSkill[i] = -1;
                prevNormal[i] = false;
                prevBurn[i] = 0;
            }
            prevPropBurn = new int[sim.State.props.Length];
            prevPropShock = new int[sim.State.props.Length];
            prevDecided = false;
            Voice(0, lib.voiceIntro, 0.2f);
            Voice(1, lib.voiceIntro, 1.3f);
        }

        public void Stop()
        {
            rain.Stop();
            music.Stop();
            ambience.Stop();
            foreach (var v in voice) v.Stop();
        }

        // ------------------------------------------------------------------ phát
        AudioClip Pick(AudioClip[] set) => set == null || set.Length == 0 ? null : set[rng.Next(set.Length)];

        void Play(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (!clip) return;
            var s = sfx[nextSfx];
            nextSfx = (nextSfx + 1) % sfx.Length;
            s.pitch = pitch * (0.95f + 0.1f * (float)rng.NextDouble());
            s.PlayOneShot(clip, volume * sfxVolume);
        }

        public void PlayCue(string name) => Play(lib.Cue(name));
        void Variant(string name, int count) => PlayCue(name + rng.Next(1, count + 1));

        void Voice(int idx, int line, float delay = 0f)
        {
            var clip = lib.Voice(fighterIds[idx], line);
            if (!clip) return;
            var s = voice[idx];
            s.Stop();
            s.clip = clip;
            s.volume = voiceVolume;
            if (delay > 0) s.PlayDelayed(delay); else s.Play();
        }

        enum Material { Metal, Wood, Plastic, Glass }

        static Material MaterialOf(PropData d)
        {
            if (!d) return Material.Wood;
            switch (d.id)
            {
                case "ghe_nhua": return Material.Plastic;
                case "chai_thuy_tinh": return Material.Glass;
                case "choi_tre": case "gach": return Material.Wood;
            }
            if ((d.traits & PropTraits.Metal) != 0) return Material.Metal;
            if ((d.traits & PropTraits.Fragile) != 0) return Material.Glass;
            return Material.Wood;
        }

        AudioClip HitOf(Material m) => m switch
        {
            Material.Metal => lib.metalHit, Material.Plastic => lib.plasticHit, Material.Glass => lib.glassHit, _ => lib.woodHit,
        };

        AudioClip BreakOf(Material m) => m switch
        {
            Material.Metal => lib.metalBreak, Material.Plastic => lib.plasticBreak, Material.Glass => lib.glassBreak, _ => lib.woodBreak,
        };

        /// <summary>Vật người đánh đang dùng để đánh trúng (vung trên tay hoặc vừa ném), null nếu đánh tay không.</summary>
        static PropData WeaponOf(FightSim sim, int attacker)
        {
            ref var f = ref sim.Fighter(attacker);
            if (f.action == FighterAction.Swing && f.actionProp >= 0) return sim.Props[sim.State.props[f.actionProp].dataIndex];
            var props = sim.State.props;
            int best = -1, bestDist = int.MaxValue;
            ref var def = ref sim.Fighter(1 - attacker);
            for (int i = 0; i < props.Length; i++)
            {
                if (props[i].phase != PropPhase.Flying || props[i].owner != attacker) continue;
                int d = System.Math.Abs(props[i].x - def.x);
                if (d < bestDist) { bestDist = d; best = i; }
            }
            return best >= 0 && bestDist < 80 * FightSim.Sub ? sim.Props[props[best].dataIndex] : null;
        }

        // ------------------------------------------------------------------ mỗi tick mô phỏng
        public void OnTick(FightSim sim)
        {
            if (sim.State.rainTicks > 0)
            {
                if (!rain.isPlaying) Loop(rain, lib.Cue("rain"), ambienceVolume * .65f);
            }
            else if (rain.isPlaying) rain.Stop();
            foreach (var ev in sim.Events) OnEvent(sim, ev);

            for (int i = 0; i < 2; i++)
            {
                ref var f = ref sim.Fighter(i);
                bool started = f.action == FighterAction.Skill && !f.normal
                               && (prevAction[i] != FighterAction.Skill || prevSkill[i] != f.skillIndex || prevNormal[i]);
                if (f.action != prevAction[i])
                {
                    if (f.action == FighterAction.Roll) PlayCue("roll");
                    if (f.action == FighterAction.Pickup) PlayCue("pickup");
                    if (f.action == FighterAction.Toss) Variant("throw", 2);
                }
                if (f.normal && f.action == FighterAction.Skill && f.skillIndex != FighterData.DiveLand &&
                    (!prevNormal[i] || prevAction[i] != f.action || prevSkill[i] != f.skillIndex))
                {
                    bool light = f.skillIndex <= FighterData.Light3 || f.skillIndex == FighterData.AirLight;
                    Variant(light ? "whoosh_light" : "whoosh_heavy", 3);
                }
                if (started)
                {
                    var s = sim.CurrentSkill(i);
                    if (s?.clip != null) PlayCue(s.clip.name);
                    if (s != null && s.input != SkillInput.Super) Voice(i, lib.voiceSkill);   // tuyệt chiêu: có thoại riêng (sự kiện Super)
                }
                if (f.burnTicks > 0 && prevBurn[i] == 0) Play(Pick(lib.fire), 0.6f);
                if (f.doubleJumpUsed && f.doubleJumpTick == sim.State.tick)
                    Play(lib.Cue("double_jump") ?? lib.Cue("whoosh_light1"), 0.7f, 1.2f);   // chưa có tiếng riêng: mượn tiếng vung nhẹ
                prevAction[i] = f.action;
                prevSkill[i] = f.skillIndex;
                prevNormal[i] = f.normal;
                prevBurn[i] = f.burnTicks;
            }

            var props = sim.State.props;
            if (prevPropBurn.Length != props.Length)
            {
                prevPropBurn = new int[props.Length];
                prevPropShock = new int[props.Length];
            }
            for (int k = 0; k < props.Length; k++)
            {
                if (props[k].burnTicks > 0 && prevPropBurn[k] == 0) Play(Pick(lib.fire), 0.55f);
                if (props[k].shockTicks > 60 && prevPropShock[k] <= 60) Play(Pick(lib.electric), 0.35f);   // bỏ qua nhiễm điện ngắn do ái lực Khang
                prevPropBurn[k] = props[k].burnTicks;
                prevPropShock[k] = props[k].shockTicks;
            }

            if (sim.MatchOver && !prevDecided && sim.State.matchWinner >= 0)
                Voice(sim.State.matchWinner, lib.voiceWin, 1.2f);
            prevDecided = sim.MatchOver;
        }

        void OnEvent(FightSim sim, in SimEvent ev)
        {
            switch (ev.kind)
            {
                case SimEventKind.HitLight:
                case SimEventKind.HitHeavy:
                {
                    bool heavy = ev.kind == SimEventKind.HitHeavy;
                    Play(Pick(heavy ? lib.hitHeavy : lib.hitLight), heavy ? 1f : 0.85f);
                    if (ev.actor >= 0)
                    {
                        var weapon = WeaponOf(sim, 1 - ev.actor);
                        if (weapon) Play(HitOf(MaterialOf(weapon)), 0.8f);
                        if (sim.Fighter(ev.actor).shockFx >= 20) Play(Pick(lib.electric), 0.7f);
                    }
                    break;
                }
                case SimEventKind.Land: Variant("land", 2); break;
                case SimEventKind.Block: Variant("block", 3); break;
                case SimEventKind.GuardBreak:
                    PlayCue("guard_break");
                    break;
                case SimEventKind.Explosion: Play(Pick(lib.explosion), 1f); break;
                case SimEventKind.PropBreak:
                    if (ev.prop >= 0 && ev.prop < sim.State.props.Length)
                        {
                            var prop = sim.Props[sim.State.props[ev.prop].dataIndex];
                            Play(prop.id == "gach" ? lib.Cue("brick_break") : BreakOf(MaterialOf(prop)), .9f);
                        }
                    break;
                case SimEventKind.Knockdown: Variant("bodyfall", 2); break;
                case SimEventKind.KO:
                    PlayCue("ko_impact");
                    if (sim.State.winner >= 0) Voice(1 - sim.State.winner, lib.voiceKo);
                    break;
                case SimEventKind.Super:
                    PlayCue("super_flash");
                    if (ev.prop == 0 || ev.prop == 1) Voice(ev.prop, lib.voiceSuper);
                    break;
                case SimEventKind.HazardWarn:
                    switch (sim.Hazard)
                    {
                        case HazardKind.Motorbike: PlayCue("moto_horn"); break;
                        case HazardKind.Crane: PlayCue("crane_chain"); break;
                        case HazardKind.Burner: PlayCue("burner_ignite"); break;
                        case HazardKind.PowerBox: PlayCue("powerbox_hum"); break;

                    }
                    break;
                case SimEventKind.HazardStrike:
                    switch (sim.Hazard)
                    {
                        case HazardKind.Crane:
                            PlayCue("brick_impact");
                            break;
                        case HazardKind.Motorbike: PlayCue("moto_pass"); break;
                        case HazardKind.Burner: PlayCue("burner_roar"); break;
                        case HazardKind.PowerBox: PlayCue("powerbox_zap"); break;

                    }
                    break;
            }
        }
    }
}
