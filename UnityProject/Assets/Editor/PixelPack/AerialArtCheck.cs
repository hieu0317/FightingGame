using System;
using System.IO;
using System.Linq;
using LoanDau.Core;
using LoanDau.Data;
using UnityEditor;
using UnityEngine;

namespace LoanDau.EditorTools
{
    public static class AerialArtCheck
    {
        static FighterData Load(string id) => AssetDatabase.LoadAssetAtPath<FighterData>($"Assets/Data/Fighters/Fighter_{id}.asset");
        static void Require(bool ok,string reason) { if(!ok)throw new InvalidDataException(reason); }
        static FightSim Match(FighterData a,FighterData b)
        {
            var stage=AssetDatabase.LoadAssetAtPath<StageData>("Assets/Data/Stages/Stage_chodem.asset");
            return new FightSim(a,b,stage,420){TrainingMode=true,IntroTicks=0};
        }

        public static void Run()
        {
            var khang=Load("khang");
            var bao=Load("bao");
            var missing=new System.Collections.Generic.List<string>();
            int variants=0;
            foreach(string id in new[]{"long","bao","linh","mai","khang","ba_tu"})
            {
                var f=Load(id);
                Require(f.skills.Length==5, id+" skills");
                foreach(var s in f.skills) {
                    if(MoveSet.Ok(s.risingArt)&&MoveSet.Ok(s.fallingArt)) variants+=2; else missing.Add(id+"/"+s.clip.name+" (trên không)");
                    // chiêu có vật thể rời: phải có bản thi triển không vẽ kèm vật thể, nếu không vật thể bị vẽ 2 lần
                    if((s.worldObject||s.cableSegment)&&!MoveSet.Ok(s.groundCastArt)) missing.Add(id+"/"+s.clip.name+" (dưới đất, không kèm vật thể)");
                }
                if(!MoveSet.Ok(f.moves.pulledFront)) missing.Add(id+"/pulled_front");
                if(!MoveSet.Ok(f.moves.pulledRecover)) missing.Add(id+"/pulled_recover");
                foreach(var buttons in new[]{Buttons.Light,Buttons.Heavy,Buttons.Down|Buttons.Heavy,Buttons.Skill})
                foreach(int velocity in new[]{48,-48})
                {
                    var sim=Match(f,bao);
                    sim.Fighter(0).action=FighterAction.Jump;
                    sim.Fighter(0).y=65*FightSim.Sub;sim.Fighter(0).vy=velocity;
                    sim.Simulate(new InputFrame(buttons),default);
                    var current=sim.CurrentSkill(0);
                    Require(current!=null, id+" aerial input "+buttons);
                    if(current.placeholderArt) missing.Add(id+"/"+current.clip.name+" (hình tạm)");
                }
                var pull=Match(khang,f);
                int before=pull.Fighter(1).x;
                Require(pull.TryHit(0,1,25,0,HitFlags.Pull),id+" hook contact");
                Require(pull.Fighter(1).x==before && pull.Fighter(1).pullTicks>0,id+" should reel over time");
                int goal=pull.Fighter(1).pullTargetX;
                for(int i=0;i<10;i++){
                    int prev=pull.Fighter(1).x;
                    pull.Simulate(default,default);
                    Require(pull.Fighter(1).x<prev,id+" reel step "+i);
                }
                Require(pull.Fighter(1).x==goal && pull.Fighter(1).pullTicks==0,id+" reel completion");
            }
            var miss=Match(khang,bao);
            miss.Fighter(0).x=-320*FightSim.Sub;miss.Fighter(1).x=310*FightSim.Sub;
            miss.Simulate(new InputFrame(Buttons.Right|Buttons.Skill),default);
            bool returned=false,attached=false;
            for(int i=0;i<170;i++){
                miss.Simulate(default,default);
                returned|=miss.State.effects.Any(e=>e.active&&e.returningCable);
                attached|=miss.Fighter(1).pullTicks>0;
            }
            Require(returned && !attached && !miss.State.effects.Any(e=>e.active),"missed hook must retract");
            var fall=Match(bao,khang);
            fall.Simulate(new InputFrame(Buttons.Heavy|Buttons.Skill),default);
            int lastY=int.MaxValue,steps=0,firstTick=-1;
            for(int i=0;i<240;i++){                     // tuyệt chiêu: tối màn hình + lấy đà trước khi gọi giàn giáo
                fall.Simulate(default,default);
                foreach(var e in fall.State.effects.Where(e=>e.active&&e.kind==EffectKind.Skill)){
                    if(firstTick<0)firstTick=i;
                    Require(e.x!=fall.Fighter(0).x,"drop is anchored to caster");
                    if(e.y<lastY)steps++;
                    lastY=e.y;
                }
            }
            Require(steps>=5,$"falling scaffold must descend through multiple world positions (xuất hiện tick {firstTick}, {steps} bước)");
            var scaffold=bao.skills.First(x=>x.dropsFromAbove);
            Require(scaffold.worldObject && MoveSet.Ok(scaffold.groundCastArt),"scaffold must be a separate sprite + clean cast art");
            var hook=khang.skills.First(x=>x.cableSegment);
            Require(hook.worldObject && hook.cableGrips!=null && hook.cableGrips.ground.Length>0 && hook.cableGrips.rise.Length>0 && MoveSet.Ok(hook.groundCastArt),"hook/cable sprites + grips");
            Debug.Log($"AERIAL_ART_VERIFIED: {variants} aerial skill variants, aerial inputs, 6 reel targets, cable miss, falling scaffold");
            var unique=missing.Distinct().ToList();
            Debug.Log(unique.Count==0?"AERIAL_ART_MISSING: none":"AERIAL_ART_MISSING ("+unique.Count+"): "+string.Join(", ",unique));
        }

        [MenuItem("Loan Dau/PixelPack/Dựng + kiểm tra đòn trên không & dây kéo")]
        public static void BuildAndCheck()
        {
            PixelPackBuilder.Build();
            Run();
            if(!PixelPackSelfTest.RunAndLog())throw new InvalidDataException("Existing PixelPack regression suite failed.");
            PixelArtLibraryBuilder.Build();
        }
    }
}
