from pathlib import Path
import hashlib
import json
import re
import wave

import numpy as np

ROOT = Path(__file__).resolve().parents[1]
AUDIO = ROOT/"UnityProject/Assets/Audio/PixelPack"
RATE = 22050
TAU = 2*np.pi


def render(name, duration, layers):
    seed = int.from_bytes(hashlib.sha256(name.encode()).digest()[:8],"big")
    rng = np.random.default_rng(seed)
    t = np.arange(round(duration*RATE))/RATE
    result = np.zeros(t.shape)
    for kind, offset, gain, freq in layers:
        active = t >= offset
        u = t[active]-offset
        span = duration-offset
        progress = u/span
        noise = rng.normal(size=len(u))
        soft = np.convolve(noise,np.ones(14)/4,"same")
        sine = lambda f: np.sin(TAU*f*u)
        env = np.exp(-4*progress)
        if kind == "wind":
            sound = soft * np.sin(np.pi*progress)**1.8
        elif kind == "thud":
            sound = (np.sin(TAU*(freq*u+2*(1-np.exp(-u*25))))+.42*soft*np.exp(-u*30))*env
        elif kind == "metal":
            sound = (sine(freq)+.57*sine(freq*2.71)+.32*sine(freq*4.13))*env
        elif kind == "grit":
            sound = noise * np.exp(-u*6/span)*(0.12+np.maximum(0,sine(freq))**5)
        elif kind == "spark":
            sound = (noise*.45+np.sign(sine(freq))*.3)*(0.16+np.maximum(0,sine(33))**3)*env
        elif kind == "fire":
            sound = soft*(.5+.35*sine(7)) + noise*.18
        elif kind == "motor":
            phase = TAU*(freq*u-0.24*freq*u*u/span)
            sound = (np.sin(phase)+.5*np.sin(3*phase)+.35*soft)*np.sin(np.pi*progress)**.65
        elif kind == "horn":
            sound = (sine(freq)+.5*sine(freq*1.21)+.25*sine(freq*3))*(np.sin(2*np.pi*progress*1.7)>-.2)
        elif kind == "hum":
            sound = sine(freq)+.36*sine(freq*2)+.16*sine(freq*3)
        elif kind == "rain":
            sound = .5*soft+.18*noise
        else:
            sound = sine(freq)*env
        result[active] += gain*sound
    if not name.endswith(("rain","powerbox_hum")):
        result *= np.minimum(1,t/.004)*np.minimum(1,(duration-t)/.016)
    samples = np.int16(result/max(.01,np.max(np.abs(result)))*24000)
    path = AUDIO/(name+".wav")
    path.parent.mkdir(parents=True,exist_ok=True)
    with wave.open(str(path),"wb") as wav:
        wav.setparams((1,2,RATE,0,"NONE","not compressed"))
        wav.writeframes(samples.astype("<i2").tobytes())
    def meta(target,template):
        file = Path(str(target)+".meta")
        if not file.exists():
            guid = hashlib.md5(target.relative_to(ROOT).as_posix().encode()).hexdigest()
            file.write_text(re.sub(r"(?m)^guid: .*","guid: "+guid,template.read_text()),encoding="utf8")
    meta(path,AUDIO/"SFX/hit_light1.wav.meta")
    for parent in path.parents:
        if parent == AUDIO: break
        meta(parent,AUDIO/"SFX.meta")


def main():
    for stem,n,seconds,layers in [
        ("whoosh_light",3,.15,["wind"]),("whoosh_heavy",3,.3,["wind","thud"]),
        ("block",3,.2,["thud","metal"]),("land",2,.3,["thud","grit"]),
        ("bodyfall",2,.5,["thud","grit","thud"]),("throw",2,.3,["grit","wind"])]:
        for i in range(n):
            render(f"SFX/{stem}{i+1}",seconds,[(k,j*.035,1/(j+1),100+i*26+j*275) for j,k in enumerate(layers)])
    cues = {
        "SFX/guard_break":(.6,"metal grit thud",320),"SFX/roll":(.35,"grit wind grit",65),
        "SFX/pickup":(.2,"grit tone",460),"SFX/ko_impact":(1,"thud grit metal",57),
        "SFX/super_flash":(.8,"wind spark metal",420),"SFX/brick_break":(.7,"thud grit grit",68),
        "SFX/Hazards/moto_horn":(2,"horn",415),"SFX/Hazards/moto_pass":(1.5,"motor",155),
        "SFX/Hazards/crane_chain":(1.5,"metal metal grit metal",630),"SFX/Hazards/brick_impact":(.8,"thud grit",72),
        "SFX/Hazards/burner_ignite":(.5,"spark fire",145),"SFX/Hazards/burner_roar":(1.5,"fire",85),
        "SFX/Hazards/powerbox_hum":(2,"hum",50),"SFX/Hazards/powerbox_zap":(.8,"spark thud",320),
        "Ambience/rain":(8,"rain",0),"UI/move":(.1,"tone",540),"UI/confirm":(.22,"tone tone",530),
        "UI/cancel":(.19,"tone tone",265),"UI/stage_pick":(.4,"metal tone tone",490)}
    patterns = [
        ["wind hum","wind thud","thud grit","wind metal","thud wind thud motor"],
        ["thud grit thud","wind thud","grit thud","wind thud grit","metal metal thud metal grit"],
        ["wind wind wind","wind","grit grit","wind metal","wind wind wind thud"],
        ["fire grit","fire wind","metal thud","metal metal","fire thud fire"],
        ["spark metal","wind metal","thud spark","hum spark","metal spark thud"],
        ["wind wind","wind thud","wind grit","wind metal","wind thud wind thud wind thud"]]
    catalog = json.loads((ROOT/"ArtSource/PixelPack/catalog.json").read_text(encoding="utf8"))
    for fighter,ch,sounds in zip(("long","bao","linh","mai","khang","ba_tu"),catalog["characters"],patterns):
        skills = [s["clip"] for s in ch["skills"] if s.get("clip")]
        for i,(skill,pattern) in enumerate(zip(skills,sounds)):
            cues[f"SFX/Skills/{fighter}/{skill}"] = ([.55,.63,.44,.7,1.1][i],pattern,100+75*i)
    for path,(duration,pattern,freq) in cues.items():
        parts = pattern.split()
        layers = [(kind,j*duration*.55/len(parts),1/(1+.25*j),freq*(1+.34*j)) for j,kind in enumerate(parts)]
        render(path,duration,layers)


if __name__ == "__main__":
    main()
