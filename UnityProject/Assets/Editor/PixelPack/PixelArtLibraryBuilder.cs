using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace LoanDau.EditorTools
{
    public static class PixelArtLibraryBuilder
    {
        const string Art = "Assets/Art/PixelPack/";
        const string Built = Art + "LibraryPrefabs/";
        static readonly string[] People = { "long", "bao", "linh", "mai", "khang", "ba_tu" };
        static Sprite SpriteAt(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(Art + path + ".png")
            ?? throw new InvalidDataException(path);

        static GameObject Child(Transform parent, string name, Vector3 position)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            return obj;
        }

        static GameObject Picture(Transform parent, string name, Sprite sprite, Vector2 position, float width, int order)
        {
            var obj = Child(parent, name, position);
            var renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            obj.transform.localScale = Vector3.one * (width / sprite.bounds.size.x);
            return obj;
        }

        static void Save(GameObject obj)
        {
            try { PrefabUtility.SaveAsPrefabAsset(obj, Built + obj.name + ".prefab"); }
            finally { UnityEngine.Object.DestroyImmediate(obj); }
        }

        static void Arena(string id, string floor, string platform, string decor, string hazard)
        {
            var root = new GameObject("stage_" + id);
            string path = "StageModules/" + id + "/";
            var back = Child(root.transform, "Hậu cảnh", Vector3.zero);
            var mid = Child(root.transform, "Trung cảnh", Vector3.zero);
            var play = Child(root.transform, "Gameplay", Vector3.zero);
            var backdrop = Picture(back.transform, "Backdrop", SpriteAt("Stages/" + id), Vector2.zero, 24, -30);
            backdrop.transform.localPosition = -backdrop.GetComponent<SpriteRenderer>().bounds.center + new Vector3(0, 3, 0);
            for (int i = 0; i < 6; i++)
            {
                var tile = Picture(play.transform, "Floor_" + i, SpriteAt(path + floor), new Vector2(-10 + 4*i,0),4,-8);
                var bounds = tile.GetComponent<SpriteRenderer>().sprite.bounds;
                tile.transform.localPosition -= new Vector3(bounds.center.x,bounds.max.y,0)*tile.transform.localScale.x;
            }
            var ground = Child(play.transform, "Ground", new Vector2(0,-.12f));
            ground.AddComponent<BoxCollider2D>().size = new Vector2(24,.24f);
            for (int i = 0; i < 2; i++)
            {
                float x = id=="bep" ? (i==0 ? 0 : 7) : (i==0 ? -6 : 6);
                float y = id=="chodem" ? 2f : (i==0 ? 1.55f : 2.35f);
                var visual = Picture(play.transform, "PlatformArt_" + i, SpriteAt(path + platform), new Vector2(x,y),4,-3);
                var bounds = visual.GetComponent<SpriteRenderer>().sprite.bounds;
                visual.transform.localPosition -= new Vector3(bounds.center.x,bounds.max.y,0)*visual.transform.localScale.x;
                var obj = Child(play.transform, "Platform_" + i, new Vector2(x,y-.1f));
                var col = obj.AddComponent<BoxCollider2D>(); col.size = new Vector2(4,.2f); col.usedByEffector = true;
                obj.AddComponent<PlatformEffector2D>();
            }
            int n=0;
            foreach (string prop in decor.Split(' '))
            {
                var item=Picture(mid.transform,prop,SpriteAt(path+prop),new Vector2(-10+(n%6)*4, n<6?1:4), n<6?1.4f:2,-15);
                n++;
            }
            var warning=Picture(mid.transform,"Hazard_"+hazard,SpriteAt(path+hazard),new Vector2(7,2),2,-12);
            warning.name = "HazardAnchor_" + hazard;
            Child(play.transform,"spawn_p1",new Vector2(-4,0)); Child(play.transform,"spawn_p2",new Vector2(4,0));
            for(int i=0;i<8;i++)Child(play.transform,"spawn_prop_"+i,new Vector2(-10.5f+i*3,0));
            Save(root);
        }

        static void Fighter(string id)
        {
            string path=Built+id+".controller";
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if(controller==null) controller=AnimatorController.CreateAnimatorControllerAtPath(path);
            if(controller.layers.Length==0)controller.AddLayer("Base Layer");
            var machine=controller.layers[0].stateMachine;
            foreach(string file in Directory.GetFiles(Art+"BasicActions/"+id,"*.anim").OrderBy(x=>x))
            {
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(file);
                var binding=AnimationUtility.GetObjectReferenceCurveBindings(clip).Single(b=>b.propertyName=="m_Sprite");
                var frames=AnimationUtility.GetObjectReferenceCurve(clip,binding);
                if(frames.Any(k=>!(k.value is Sprite)))throw new InvalidDataException(file+" contains missing sprites");
                var state=machine.states.Select(s=>s.state).FirstOrDefault(s=>s.name==clip.name)??machine.AddState(clip.name);
                state.motion=clip;
                if(clip.name=="idle_fight")machine.defaultState=state;
            }
            var obj=new GameObject("char_"+id);obj.AddComponent<SpriteRenderer>().sprite=SpriteAt("Fighters/"+id);
            obj.AddComponent<Animator>().runtimeAnimatorController=controller;
            Save(obj);
        }

        static void Prop(string id, float width, Vector2 grip)
        {
            var obj = new GameObject("prop_"+id);
            var intact = Picture(obj.transform,"Intact",SpriteAt("Props/"+id),Vector2.zero,width,0);
            var rect = intact.GetComponent<SpriteRenderer>().sprite.bounds;
            var size = rect.size * intact.transform.localScale.x;
            intact.transform.localPosition -= rect.center * intact.transform.localScale.x;
            var damaged = Picture(obj.transform,"Damaged",SpriteAt("DamagedProps/"+id),Vector2.zero,width,0);
            var damagedBounds = damaged.GetComponent<SpriteRenderer>().sprite.bounds;
            damaged.transform.localPosition -= damagedBounds.center * damaged.transform.localScale.x;
            damaged.SetActive(false);
            obj.AddComponent<BoxCollider2D>().size = new Vector2(size.x,size.y);
            Child(obj.transform,"Grip",new Vector2((grip.x-.5f)*size.x,(.5f-grip.y)*size.y));
            Save(obj);
        }

        static void ValidateAssets()
        {
            foreach(string file in Directory.GetFiles(Art,"*.anim",SearchOption.AllDirectories))
            {
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(file);
                var bindings=AnimationUtility.GetObjectReferenceCurveBindings(clip);
                if(!bindings.Any(b=>b.propertyName=="m_Sprite"))throw new InvalidDataException(file);
                foreach(var binding in bindings)
                    if(AnimationUtility.GetObjectReferenceCurve(clip,binding).Any(key=>key.value==null))
                        throw new InvalidDataException("Missing frame in "+file);
            }
            foreach(string file in Directory.GetFiles("Assets/Audio/PixelPack","*.wav",SearchOption.AllDirectories))
            {
                var audio=AssetDatabase.LoadAssetAtPath<AudioClip>(file);
                if(audio==null || audio.length<=0)throw new InvalidDataException(file);
            }
        }

        static void Gallery()
        {
            bool batch = Application.isBatchMode;
            var scene=UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                batch ? UnityEditor.SceneManagement.NewSceneMode.Single : UnityEditor.SceneManagement.NewSceneMode.Additive);
            var root=new GameObject("Basic motion gallery");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
            var camera=Child(root.transform,"Camera",new Vector3(0,1,-10)).AddComponent<Camera>();
            camera.orthographic=true; camera.orthographicSize=2.3f;
            camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=new Color(.12f,.10f,.18f);
            var subject=Child(root.transform,"Fighter",Vector3.zero);
            subject.AddComponent<SpriteRenderer>().sprite=SpriteAt("Fighters/long");
            var gallery=root.AddComponent<LoanDau.Art.BasicMotionGallery>();
            gallery.subject=subject.AddComponent<Animator>();
            gallery.fighters=People.Select(id=>AssetDatabase.LoadAssetAtPath<AnimatorController>(Built+id+".controller")).ToArray();
            gallery.fighterNames=new[]{"Long","Bảo","Linh","Mai","Khang","Bà Tư"};
            gallery.subject.runtimeAnimatorController=gallery.fighters[0];
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene,Built+"BasicMotionGallery.unity");
            if(!batch)UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);
        }

        [MenuItem("Loan Dau/Pixel Art/Dựng thư viện asset 2D")]
        public static void Build()
        {
            if(!AssetDatabase.IsValidFolder(Built.TrimEnd('/')))AssetDatabase.CreateFolder(Art.TrimEnd('/'),"LibraryPrefabs");
            ValidateAssets();
            foreach(var id in People)Fighter(id);
            string[] items={"ghe_nhua","chao_gang","ong_tuyp","choi_tre","chai_thuy_tinh","gach","noi_pho","thung_phi","binh_gas","thung_son","bua_ta","xe_may"};
            float[] widths={.40f,.55f,1.20f,1.10f,.07f,.20f,.45f,.58f,.30f,.30f,.90f,1.85f};
            Vector2[] grips={new(.8f,.9f),new(.92f,.08f),new(.55f,.45f),new(.82f,.26f),new(.52f,.25f),new(.5f,.5f),new(.5f,.5f),new(.5f,.5f),new(.5f,.5f),new(.5f,.5f),new(.16f,.85f),new(.5f,.5f)};
            for(int i=0;i<items.Length;i++)Prop(items[i],widths[i],grips[i]);
            Arena("chodem","pavement","awning","counter facade lanterns cables sign table cart stool","headlight");
            Arena("congtruong","concrete","scaffold","ladder column safety_net cement bricks sign worklight barrier sand","crane");
            Arena("bep","tiles","worktop","wall shelf bowls fish_sauce hood ladle basket spice rice_cooker","burner");
            Arena("santhuong","slab","steps","parapet tank tree laundry antenna neon railing vent rain","cabinet");
            Gallery();AssetDatabase.SaveAssets();
            Debug.Log("PIXEL_LIBRARY_VERIFIED: 6 characters, all basic clip bindings, 4 stages");
        }
    }
}
