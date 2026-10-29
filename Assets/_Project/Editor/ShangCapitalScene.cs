using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ThreeBodyProblem.Editor
{
    public static partial class TravellerSceneBuilder
    {
        [MenuItem("Tools/Three Body Traveller/Rebuild Gobi And Shang Capital")]
        public static void RebuildSeparatedCapital()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before rebuilding worlds.");
            var active = SceneManager.GetActiveScene();
            string original = active.path;
            if (active.isDirty && !string.IsNullOrEmpty(original)) EditorSceneManager.SaveScene(active);
            BackupScenes();
            foreach (string folder in new[] { SceneFolder, MatFolder, MeshFolder, PrefabFolder }) Directory.CreateDirectory(folder);
            AssetDatabase.Refresh(); ConfigureImports(); Materials.Clear(); Meshes.Clear();
            CreateMaterials(); CreateCapitalMaterials(); CreateEnvironmentMaterials();
            QualitySettings.shadowDistance = 180f; QualitySettings.shadowCascades = 2; QualitySettings.pixelLightCount = 2;
            BuildGobi(); BuildChaoge(); AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(string.IsNullOrEmpty(original) ? SceneFolder + "/01_GobiJourney.unity" : original);
            Debug.Log("SHANG_CAPITAL: independent Gobi and capital rebuilt; medieval scene and build order preserved.");
        }

        [MenuItem("Tools/Three Body Traveller/Rebuild Shang Capital Only")]
        public static void RebuildShangCapitalOnly()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play mode before rebuilding the capital.");
            var active=SceneManager.GetActiveScene();string original=active.path;
            if(active.isDirty&&!string.IsNullOrEmpty(original))EditorSceneManager.SaveScene(active);
            BackupScenes();
            foreach(string folder in new[]{SceneFolder,MatFolder,MeshFolder,PrefabFolder})Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();ConfigureImports();Materials.Clear();Meshes.Clear();
            CreateMaterials();CreateCapitalMaterials();CreateEnvironmentMaterials();
            BuildChaoge();AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(string.IsNullOrEmpty(original)?SceneFolder+"/01_GobiJourney.unity":original);
            Debug.Log("PYRAMID_CAPITAL: stone palace rebuilt; Gobi and medieval scenes preserved.");
        }

        private static void CreateCapitalMaterials()
        {
            CapitalMaterial("CapitalEarth", "clay_plaster", new Color(0.87f, 0.75f, 0.57f));
            CapitalMaterial("CapitalWall", "clay_plaster", new Color(0.83f, 0.69f, 0.49f));
            CapitalMaterial("CapitalRoad", "dry_riverbed_rock", new Color(0.92f, 0.88f, 0.75f));
            CapitalMaterial("CapitalPlaster", "clay_plaster", new Color(0.91f, 0.86f, 0.70f));
            CapitalMaterial("CapitalThatch", "reed_roof_03", new Color(0.76f, 0.64f, 0.42f));
            CapitalMaterial("CapitalDarkThatch", "reed_roof_03", new Color(0.48f, 0.43f, 0.31f));
            CapitalMaterial("CapitalTimber", "wood_planks", new Color(0.32f, 0.24f, 0.17f));
            CapitalMaterial("CapitalRedWall", "red_plaster_weathered", new Color(0.69f, 0.52f, 0.40f));
            CapitalMaterial("CapitalPaving", "stone_wall", new Color(0.62f, 0.59f, 0.48f));
            CapitalMaterial("RoyalLacquer", "wood_planks", new Color(0.40f, 0.095f, 0.035f));
            Materials["RoyalLacquer"].mainTexture = null;
            Materials["RoyalLacquer"].SetFloat("_BumpScale", 0.25f);
            Materials["RoyalLacquer"].SetFloat("_Glossiness", 0.28f);
            CapitalMaterial("RoyalRoof", "reed_roof_03", new Color(0.47f, 0.39f, 0.25f));
            Material("RoyalBronzeGold", new Color(0.56f, 0.40f, 0.14f)).SetFloat("_Metallic", 0.45f);
            CapitalMaterial("PyramidStone", "sandstone_blocks_05", new Color(0.57f,0.55f,0.49f));
            CapitalMaterial("PyramidInterior", "sandstone_blocks_05", new Color(0.32f,0.31f,0.28f));
            CapitalMaterial("PyramidFloor", "stone_wall", new Color(0.40f,0.39f,0.34f));
            Material("CapitalMeadow", new Color(0.30f, 0.35f, 0.20f));
            Material("CapitalFoliage", new Color(0.21f, 0.31f, 0.14f));
            Material("CapitalField", new Color(0.51f, 0.48f, 0.25f));
            Material("CapitalOchreCloth", new Color(0.48f, 0.30f, 0.15f));
            Material("CapitalIndigoCloth", new Color(0.12f, 0.21f, 0.24f));
            Material("CapitalBronze", new Color(0.24f, 0.29f, 0.21f)).SetFloat("_Metallic", 0.65f);
        }

        private static void CapitalMaterial(string key, string asset, Color tint)
        {
            var material = Material(key, tint);
            string folder = "Assets/ThirdParty/PolyHaven/" + asset + "/textures/";
            if (!File.Exists(folder + asset + "_diff_1k.jpg")) folder = "Assets/ThirdParty/PolyHaven/" + asset + "/";
            material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + asset + "_diff_1k.jpg");
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + asset + "_nor_gl_1k.jpg");
            if (material.mainTexture == null) throw new FileNotFoundException("Capital material needs " + asset);
            material.SetTexture("_BumpMap", normal); material.SetFloat("_BumpScale", 0.6f); material.EnableKeyword("_NORMALMAP");
            material.SetTexture("_OcclusionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + asset + "_ao_1k.jpg"));
            material.SetFloat("_Glossiness", 0.10f);
        }

        private static void BuildChaoge()
        {
            if (!Materials.ContainsKey("CapitalEarth")) CreateCapitalMaterials();
            var arrival = Moment("朝歌 · 殷商王都", "数日行路之后，朝歌出现在河谷平原。巨摆环绕石砌金字塔；狭长甬道穿过厚墙，通向纣王所在的内殿。", "随周文王入城，穿过金字塔低矮入口与石砌甬道。", new Vector3(0f, 1.75f, -238f), new Vector3(0f, 35f, 20f), "周文王：到了。巨摆之间的金字塔就是王宫，跟我走进甬道。");
            arrival.sunA = new Vector3(-0.012f, 1f, 0.018f); arrival.zenith = new Color(0.11f, 0.33f, 0.53f);
            arrival.horizon = new Color(0.72f, 0.81f, 0.82f); arrival.fog = new Color(0.65f, 0.73f, 0.72f);
            arrival.ambient = new Color(0.48f, 0.53f, 0.56f); arrival.lightColor = new Color(1f, 0.94f, 0.82f);
            arrival.lightIntensity = 1.6f; arrival.fogDensity = 0.00105f;
            var revival = Moment("恒纪元 · 王都复苏池", "金字塔的侧面甬道通向复苏池苑。日照暂时与推演相合，人们在池边恢复活动，水道将王庭与河流相连。", "走下东侧坡道，在复苏池边记录短期应验。", new Vector3(82f, 1.75f, -20f), new Vector3(106f, 6f, 8f), "周文王：这一次，它与推演相合。人们可以复苏了。");
            revival.sunA = new Vector3(-0.3f, 0.65f, 0.5f); revival.zenith = arrival.zenith; revival.horizon = arrival.horizon;
            revival.ambient = arrival.ambient; revival.fog = arrival.fog; revival.fogDensity = arrival.fogDensity;
            revival.lightColor = arrival.lightColor; revival.lightIntensity = arrival.lightIntensity;
            revival.observation = "朝歌王都的短期日照与推演接近，复苏池苑出现恢复活动的人们。";
            revival.interpretation = "周文王将这次应验视作历法可靠的支持，但尚未保证长期稳定。";
            var night = Night(Moment("长夜 · 没有到来的日出", "晨光始终没有越过王都。池水结霜，街巷沉寂，三颗飞星成为推演之外的新线索。", "在池边抬头看清三颗飞星，记录长夜这一反例。", new Vector3(82f, 1.75f, -20f), new Vector3(142f, 86f, 205f), "周文王：不该是这样……再等一会儿。"), 3);
            night.frost = 1f; night.ambient = new Color(0.24f, 0.31f, 0.39f); night.fogDensity = 0.0028f;
            night.observation = "预期的日出未发生，朝歌复苏池苑结霜，长夜持续，天空出现三颗飞星。";
            var profile = Profile(1, "第一幕 / 朝歌 · 殷商王都", "巨摆环绕 · 石砌金字塔 · 复苏与长夜", arrival, revival, night);
            profile.hasCapitalRoute = true; profile.companionWalkSpeed = 3.4f;
            profile.companionArrival = new Vector3(-3f, 0f, -230f);
            profile.courtGoal = new Vector3(0f, 4f, 13f); profile.waterGoal = new Vector3(82f, 0f, -20f);
            profile.courtWaypoints = new[] { new Vector3(-3,0,-208), new Vector3(-3,0,-160), new Vector3(-1.2f,0,-94), new Vector3(-1.2f,4,-55), new Vector3(-1.2f,4,-39), new Vector3(-1.2f,4,-9), new Vector3(-1.2f,4,13) };
            profile.waterWaypoints = new[] { new Vector3(-1.2f,4,6), new Vector3(-1.2f,4,-9), new Vector3(8,4,-9), new Vector3(40,4,-9), new Vector3(64,4,-9), new Vector3(82,0,-9), new Vector3(82,0,-20) };
            EditorUtility.SetDirty(profile);
            BeginScene(profile); world.gameObject.name = "Shang Capital";
            // The massive pyramid must leave all three long-night stars visible from the pool.
            stage.flyingStars[0].localPosition = new Vector3(100,70,200);
            stage.flyingStars[1].localPosition = new Vector3(160,98,220);
            stage.flyingStars[2].localPosition = new Vector3(215,67,195);
            var land = CapitalDistrict("00 River plain and agricultural outskirts");
            var gate = CapitalDistrict("01 Rammed-earth capital gate");
            var avenue = CapitalDistrict("02 Processional avenue and market");
            var palace = CapitalDistrict("03 Stone pyramid palace");
            var temples = CapitalDistrict("04 Ancestral temple precinct");
            var houses = CapitalDistrict("05 Residential wards");
            var workshops = CapitalDistrict("06 Bronze workshops and granaries");
            var pool = CapitalDistrict("07 Revival water gardens");
            var observatory = CapitalDistrict("08 Great pendulum precinct");
            BuildCapitalNaturalLandscape(land);
            BuildCapitalGate(gate);
            Box("Royal processional road", new Vector3(0,0.018f,-137), new Vector3(24,0.035f,202), "CapitalRoad", avenue, false);
            Box("Ceremonial forecourt", new Vector3(0,0.026f,-76), new Vector3(100,0.05f,66), "CapitalRoad", avenue, false);
            foreach (float side in new[] { -1f, 1f })
            {
                for (int i=0;i<10;i++)
                {
                    float z=-195+i*13;
                    Box("Avenue drainage run", new Vector3(side*14,0.02f,z), new Vector3(0.65f,0.06f,12.5f), "CapitalTimber", avenue, false);
                    if(i%2==0) CapitalStandard(new Vector3(side*17,0,z), avenue, i%4==0?"CapitalOchreCloth":"CapitalIndigoCloth");
                }
                for(int row=0;row<3;row++)
                {
                    float z=-173+row*40;
                    Box("Ward cross street",new Vector3(side*86,0.015f,z+19),new Vector3(132,0.03f,12),"CapitalRoad",avenue,false);
                    for(int column=0;column<3;column++)
                        CapitalBuilding("Market courtyard house",new Vector3(side*(39+column*28),0,z),18,17,4.3f,"CapitalThatch",houses,true);
                    CapitalStall(new Vector3(side*25,0,z+9),avenue,row%2==0?"CapitalOchreCloth":"CapitalIndigoCloth");
                }
            }
            BuildRoyalCourt(palace);
            BuildTemplePrecinct(temples);
            for(int row=0;row<4;row++) for(int column=0;column<5;column++)
            {
                float x=-180+column*27; float z=-35+row*43;
                CapitalBuilding("Residential timber-and-earth house",new Vector3(x,0,z),15+(column%2)*2,14,3.5f+(row%2)*0.5f,"CapitalThatch",houses,true);
                CapitalBuilding("Residential rear shed",new Vector3(x+5,0,z+22),9,8,2.8f,"CapitalDarkThatch",houses,false);
            }
            for(int i=0;i<8;i++)
            {
                CapitalBuilding("Royal granary",new Vector3(-90+i%4*28,0,163+i/4*39),18,23,5,"CapitalDarkThatch",workshops,true);
                for(int jar=0;jar<3;jar++) Shape("Grain storage jar",PrimitiveType.Sphere,new Vector3(-95+i%4*28+jar*3,1.1f,150+i/4*39),new Vector3(1.8f,2.2f,1.8f),"CapitalWall",workshops,false);
            }
            for(int i=0;i<5;i++)
            {
                CapitalBuilding("Bronze casting workshop",new Vector3(137,0,-150+i*38),24,22,4.5f,"CapitalDarkThatch",workshops,true);
                Shape("Clay casting furnace",PrimitiveType.Cylinder,new Vector3(118,1.1f,-153+i*38),new Vector3(2.6f,1.1f,2.6f),"CapitalRedWall",workshops,false);
                BronzeDing(new Vector3(119,0,-143+i*38),1.0f,workshops);
            }
            BuildRevivalGarden(pool);
            var previousWorld = world; world = observatory;
            foreach(var position in new[]{new Vector3(-80,0,-63),new Vector3(80,0,-63),new Vector3(-84,0,126),new Vector3(84,0,126),new Vector3(0,0,147),new Vector3(-94,0,-115),new Vector3(94,0,-115)})
                Pendulum(position,40,0);
            foreach(var motion in observatory.GetComponentsInChildren<PendulumMotion>()) {motion.period=18;motion.angle=10;motion.offset=0;}
            world=previousWorld;
            for(int i=0;i<12;i++)
            {
                CharacterRoot("王都居民",new Vector3((i%2==0?-1:1)*(25+i%3*4),0,-184+i*9),i%3==0?"Robe":"Ivory",avenue);
            }
            FinishScene("02_ChaogeCourt");
        }

        private static Transform CapitalDistrict(string name)
        {
            var value=new GameObject(name);value.transform.SetParent(world);return value.transform;
        }

        private static void BuildCapitalGate(Transform parent)
        {
            foreach(float side in new[]{-1f,1f})
            {
                Box("Outer earth wall",new Vector3(side*119,4,-210),new Vector3(190,8,10),"CapitalWall",parent);
                Box("Sloped wall footing",new Vector3(side*119,1.2f,-210),new Vector3(196,2.4f,14),"CapitalEarth",parent);
                Box("Gate earth bastion",new Vector3(side*20,5.5f,-210),new Vector3(16,11,20),"CapitalWall",parent);
                Box("Gate earth course",new Vector3(side*20,11.1f,-210),new Vector3(18,0.5f,22),"CapitalEarth",parent,false);
                CapitalHippedRoof("Gate watch hall roof",new Vector3(side*20,15.5f,-210),24,28,4,"CapitalDarkThatch",parent);
                foreach(float x in new[]{-5f,5f}) foreach(float z in new[]{-7f,7f})
                    Shape("Gate timber post",PrimitiveType.Cylinder,new Vector3(side*20+x,13.4f,-210+z),new Vector3(0.75f,2.2f,0.75f),"CapitalTimber",parent,false);
                Box("East-west city enclosure",new Vector3(side*218,3.6f,0),new Vector3(8,7.2f,420),"CapitalWall",parent);
                for(int i=0;i<9;i++) Box("Earth wall buttress",new Vector3(side*214,3.5f,-174+i*44),new Vector3(11,7,8),"CapitalEarth",parent,false);
            }
            Box("North city enclosure",new Vector3(0,3.6f,213),new Vector3(444,7.2f,8),"CapitalWall",parent);
            Box("Timber gate bridge",new Vector3(0,11.5f,-210),new Vector3(25,2,9),"CapitalTimber",parent);
            CapitalHippedRoof("Gate central shelter",new Vector3(0,12.7f,-210),28,16,2.8f,"CapitalDarkThatch",parent);
            foreach(float side in new[]{-1f,1f})
            {
                Box("Gate timber leaf open",new Vector3(side*11.6f,4.3f,-202),new Vector3(0.6f,8.6f,10),"CapitalTimber",parent);
                CapitalStandard(new Vector3(side*11,0,-227),parent,"RedCloth");
                CharacterRoot("城门卫兵",new Vector3(side*8.5f,0,-217),"Charcoal",parent,true);
            }
        }

        private static void BuildTemplePrecinct(Transform parent)
        {
            Box("Ancestral court earth precinct",new Vector3(-107,0.08f,45),new Vector3(90,0.16f,88),"CapitalRoad",parent,false);
            Box("Ancestral hall terrace",new Vector3(-107,1.5f,74),new Vector3(62,3,34),"CapitalWall",parent);
            CapitalBuilding("Ancestral temple hall",new Vector3(-107,3,74),53,24,8,"CapitalDarkThatch",parent,false);
            Box("Ritual altar",new Vector3(-107,0.9f,27),new Vector3(17,1.8f,14),"CapitalEarth",parent);
            foreach(float side in new[]{-1f,1f})
            {
                CapitalBuilding("Ancestral precinct side hall",new Vector3(-107+side*35,0,43),12,38,5,"CapitalThatch",parent,true);
                for(int i=0;i<3;i++) BronzeDing(new Vector3(-107+side*16,0,20+i*10),1.3f,parent);
            }
            CapitalStandard(new Vector3(-107,1.8f,27),parent,"CapitalOchreCloth");
        }

        private static void BuildRevivalGarden(Transform parent)
        {
            Box("Revival basin bed",new Vector3(104,-0.15f,3),new Vector3(36,0.3f,58),"CapitalPaving",parent,false);
            Box("Revival pool water",new Vector3(104,0.025f,3),new Vector3(35,0.035f,57),"Water",parent,false);
            Box("Revival pool ice",new Vector3(104,0.06f,3),new Vector3(35,0.035f,57),"Snow",stage.frostDetails.transform,false);
            foreach(float side in new[]{-1f,1f}) Box("Pool timber edging",new Vector3(104+side*18.2f,0.2f,3),new Vector3(0.55f,0.4f,59),"CapitalTimber",parent);
            Box("Water garden approach",new Vector3(81.5f,0.015f,-2),new Vector3(7,0.03f,67),"CapitalRoad",parent,false);
            Box("Pool north edge",new Vector3(104,0.2f,32.2f),new Vector3(37,0.4f,0.55f),"CapitalTimber",parent);
            Box("Pool south edge",new Vector3(104,0.2f,-26.2f),new Vector3(37,0.4f,0.55f),"CapitalTimber",parent);
            for(int i=0;i<7;i++) CharacterRoot("复苏者",new Vector3(92+i%3*9,0.05f,-13+i/3*14),i%2==0?"Ivory":"Robe",parent);
            CapitalBuilding("Pool shelter",new Vector3(104,0,46),30,12,4.5f,"CapitalThatch",parent,false);
            Box("Canal to river",new Vector3(154,0.03f,8),new Vector3(60,0.04f,5),"Water",parent,false);
            for(int i=0;i<12;i++)
            {
                NaturalTree(new Vector3(136+i%2*13,0,-35+i*7),8+i%3*1.5f,parent,i*53f);
            }
            var before=world;world=parent;Torch(new Vector3(82,0,-29));Torch(new Vector3(82,0,35));world=before;
        }

        private static void CapitalBuilding(string name,Vector3 position,float width,float depth,float height,string roofMaterial,Transform parent,bool collision)
        {
            var root=new GameObject(name);root.transform.SetParent(parent);root.transform.position=position;
            var detail=new GameObject("Detailed timber earth and reed structure");detail.transform.SetParent(root.transform,false);
            var d=detail.transform;
            Box("Low earth foundation",position+Vector3.up*0.25f,new Vector3(width+1,0.5f,depth+1),"CapitalEarth",d,false);
            Box("Plastered earth walls",position+Vector3.up*(height*0.5f+0.5f),new Vector3(width,height,depth),"CapitalPlaster",d,false);
            Box("Dark timber doorway",position+new Vector3(0,height*0.37f,-depth*0.5f-0.025f),new Vector3(Mathf.Min(3,width*0.22f),height*0.74f,0.08f),"CapitalTimber",d,false);
            foreach(float side in new[]{-1f,1f})
            {
                for(int i=0;i<3;i++) Box("Exposed timber post",position+new Vector3(-width*0.5f+i*width*0.5f,height*0.5f+0.5f,side*(depth*0.5f+0.05f)),new Vector3(0.24f,height,0.24f),"CapitalTimber",d,false);
                Box("Timber sill",position+new Vector3(0,0.7f,side*(depth*0.5f+0.08f)),new Vector3(width,0.22f,0.22f),"CapitalTimber",d,false);
                Box("High ventilation panel",position+new Vector3(side*width*0.29f,height*0.65f,-depth*0.5f-0.07f),new Vector3(width*0.19f,height*0.23f,0.12f),"CapitalTimber",d,false);
            }
            CapitalHippedRoof("Broad reed roof",position+Vector3.up*(height+0.5f),width+2.5f,depth+2.5f,Mathf.Min(width,depth)*0.25f,roofMaterial,d);
            var high=CombineCapitalBuilding(detail,"ShangHouse_"+width+"_"+depth+"_"+height+"_"+roofMaterial);
            var low=new GameObject("Distant mass");low.transform.SetParent(root.transform,false);
            Box("Distant earth walls",position+Vector3.up*(height*0.5f+0.5f),new Vector3(width,height,depth),"CapitalPlaster",low.transform,false);
            CapitalHippedRoof("Distant roof",position+Vector3.up*(height+0.5f),width+2.5f,depth+2.5f,Mathf.Min(width,depth)*0.25f,roofMaterial,low.transform,false);
            var lod=root.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(0.065f,high),new LOD(0.009f,low.GetComponentsInChildren<Renderer>())});lod.RecalculateBounds();
            if(collision){var box=root.AddComponent<BoxCollider>();box.center=new Vector3(0,height*0.5f+0.5f,0);box.size=new Vector3(width,height+1,depth);}
        }

        private static Renderer[] CombineCapitalBuilding(GameObject detail,string key,float cellSize=0)
        {
            var filters=detail.GetComponentsInChildren<MeshFilter>();var renderers=new List<Renderer>();
            // Spatial batches keep an entire palace from being submitted again
            // for a small brazier or a sliver of the camera frustum.
            var groups=filters.GroupBy(f=>
            {
                var renderer=f.GetComponent<Renderer>();var p=detail.transform.InverseTransformPoint(renderer.bounds.center);
                string cell=cellSize<=0?"":"_Cell_"+Mathf.FloorToInt(p.x/cellSize)+"_"+Mathf.FloorToInt(p.z/cellSize);
                return renderer.sharedMaterial.name+cell;
            });
            foreach(var group in groups)
            {
                var material=group.First().GetComponent<Renderer>().sharedMaterial;
                string meshKey=key+"_"+group.Key;
                Mesh mesh;
                if(!Meshes.TryGetValue(meshKey,out mesh))
                {
                    var combines=group.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=detail.transform.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray();
                    var generated=new Mesh{name=meshKey};generated.indexFormat=IndexFormat.UInt32;generated.CombineMeshes(combines,true,true);
                    string path=MeshFolder+"/"+meshKey+".asset";mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if(mesh==null){mesh=generated;AssetDatabase.CreateAsset(mesh,path);}else{EditorUtility.CopySerialized(generated,mesh);Object.DestroyImmediate(generated);}
                    Meshes[meshKey]=mesh;
                }
                var batch=new GameObject(group.Key,typeof(MeshFilter),typeof(MeshRenderer));batch.transform.SetParent(detail.transform,false);
                batch.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=batch.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderers.Add(renderer);
                GameObjectUtility.SetStaticEditorFlags(batch,StaticEditorFlags.BatchingStatic|StaticEditorFlags.OccludeeStatic);
            }
            foreach(var filter in filters){Object.DestroyImmediate(filter.GetComponent<Renderer>());Object.DestroyImmediate(filter);}
            return renderers.ToArray();
        }

        private static GameObject CharacterRoot(string name,Vector3 position,string material,Transform parent,bool guard=false,bool crown=false)
        {
            var previous=world;world=parent;Character(name,position,material,crown,false,guard);world=previous;
            return parent.GetChild(parent.childCount-1).gameObject;
        }

        private static void CapitalStall(Vector3 position,Transform parent,string cloth)
        {
            foreach(float x in new[]{-2f,2f}) Shape("Market stall post",PrimitiveType.Cylinder,position+new Vector3(x,1.5f,0),new Vector3(0.18f,1.5f,0.18f),"CapitalTimber",parent,false);
            Box("Market woven awning",position+new Vector3(0,2.9f,0),new Vector3(5,0.1f,3.8f),cloth,parent,false);
            Box("Market goods bench",position+new Vector3(0,0.75f,-0.2f),new Vector3(4,1.5f,1.2f),"CapitalTimber",parent);
            for(int i=0;i<4;i++) Shape("Earthenware trade jar",PrimitiveType.Sphere,position+new Vector3(-1.4f+i*0.9f,1.7f,-0.2f),Vector3.one*0.6f,"CapitalWall",parent,false);
        }

        private static void CapitalStandard(Vector3 position,Transform parent,string cloth)
        {
            Shape("Processional standard pole",PrimitiveType.Cylinder,position+Vector3.up*4.5f,new Vector3(0.16f,4.5f,0.16f),"CapitalTimber",parent,false);
            Box("Woven processional standard",position+new Vector3(0.8f,6.6f,0),new Vector3(1.6f,3.2f,0.05f),cloth,parent,false);
            Box("Standard bronze clasp",position+Vector3.up*8.4f,new Vector3(2,0.16f,0.18f),"CapitalBronze",parent,false);
        }

        private static void BronzeDing(Vector3 position,float scale,Transform parent)
        {
            Cone("Bronze ritual vessel",position+Vector3.up*scale*0.75f,scale*0.64f,scale*0.85f,scale*0.9f,"CapitalBronze",parent,16);
            for(int i=0;i<3;i++){float a=i*Mathf.PI*2/3;Shape("Ding tripod leg",PrimitiveType.Cylinder,position+new Vector3(Mathf.Cos(a)*scale*0.48f,scale*0.43f,Mathf.Sin(a)*scale*0.48f),new Vector3(scale*0.18f,scale*0.43f,scale*0.18f),"CapitalBronze",parent,false);}
            foreach(float side in new[]{-1f,1f}){Box("Ding upright handle",position+new Vector3(side*scale*0.65f,scale*1.78f,0),new Vector3(scale*0.18f,scale*0.48f,scale*0.46f),"CapitalBronze",parent,false);}
        }

        private static void BronzeMotif(Vector3 position,float width,Transform parent)
        {
            foreach(float side in new[]{-1f,1f})
            {
                Box("Abstract bronze eye",position+new Vector3(side*width*0.23f,0,0),new Vector3(width*0.22f,0.22f,0.09f),"CapitalBronze",parent,false);
                Box("Abstract bronze brow",position+new Vector3(side*width*0.31f,0.45f,0),new Vector3(width*0.32f,0.13f,0.09f),"CapitalBronze",parent,false);
            }
            Box("Abstract bronze central ridge",position,new Vector3(0.13f,1.25f,0.1f),"CapitalBronze",parent,false);
        }

        private static void CapitalHippedRoof(string name,Vector3 position,float width,float depth,float height,string material,Transform parent,bool trim=true)
        {
            bool rotate=depth>width;
            if(rotate){float swap=width;width=depth;depth=swap;}
            string key="ShangHip_"+width+"_"+depth+"_"+height;
            var vertices=new List<Vector3>();var triangles=new List<int>();var uv=new List<Vector2>();
            float ridge=width*0.27f;
            var fl=new Vector3(-width/2,0,-depth/2);var fr=new Vector3(width/2,0,-depth/2);var bl=new Vector3(-width/2,0,depth/2);var br=new Vector3(width/2,0,depth/2);
            var rl=new Vector3(-ridge,height,0);var rr=new Vector3(ridge,height,0);
            RoofFace(vertices,triangles,uv,fl,rl,rr,fr);RoofFace(vertices,triangles,uv,bl,br,rr,rl);
            RoofFace(vertices,triangles,uv,fl,bl,rl);RoofFace(vertices,triangles,uv,fr,rr,br);
            var root=new GameObject(name);root.transform.SetParent(parent);root.transform.position=position;
            MeshObject("Hipped reed roof",SaveMesh(key,vertices,triangles,uv),position,material,root.transform);
            if(trim)
            {
                Box("Plain timber roof ridge",position+Vector3.up*(height+0.1f),new Vector3(ridge*2+0.5f,0.3f,0.4f),"CapitalTimber",root.transform,false);
                foreach(float side in new[]{-1f,1f})
                {
                    Box("Roof eave fascia",position+new Vector3(0,-0.15f,side*depth*0.5f),new Vector3(width,0.35f,0.35f),"CapitalTimber",root.transform,false);
                    Box("Roof eave fascia",position+new Vector3(side*width*0.5f,-0.15f,0),new Vector3(0.35f,0.35f,depth),"CapitalTimber",root.transform,false);
                }
            }
            if(rotate)root.transform.rotation=Quaternion.Euler(0,90,0);
        }

        private static void RoofFace(List<Vector3> vertices,List<int> triangles,List<Vector2> uv,params Vector3[] face)
        {
            int start=vertices.Count;
            foreach(var v in face){vertices.Add(v);uv.Add(new Vector2(v.x,v.z)*0.35f);}
            for(int i=1;i<face.Length-1;i++)triangles.AddRange(new[]{start,start+i,start+i+1});
            start=vertices.Count;
            foreach(var v in face){vertices.Add(v);uv.Add(new Vector2(v.x,v.z)*0.35f);}
            for(int i=1;i<face.Length-1;i++)triangles.AddRange(new[]{start,start+i+1,start+i});
        }

        private static void CapitalRamp(string name,Vector3 from,Vector3 to,string material,Transform parent,bool alongX)
        {
            var a=new Vector3(from.x,from.y,from.z);var b=new Vector3(to.x,from.y,from.z);var c=new Vector3(to.x,to.y,to.z);var d=new Vector3(from.x,to.y,to.z);
            // Across-x processional ramp, or across-z side descent.
            if(alongX){a=new Vector3(from.x,from.y,from.z);b=new Vector3(to.x,to.y,from.z);c=new Vector3(to.x,to.y,to.z);d=new Vector3(from.x,from.y,to.z);}
            var vertices=new List<Vector3>{a,d,c,b};var triangles=new List<int>{0,1,2,0,2,3};
            var uv=new List<Vector2>{new Vector2(a.x,a.z)*0.35f,new Vector2(d.x,d.z)*0.35f,new Vector2(c.x,c.z)*0.35f,new Vector2(b.x,b.z)*0.35f};
            MeshObject(name,SaveMesh(name.Replace(" ","_")+"_Surface",vertices,triangles,uv),Vector3.zero,material,parent,true);
        }
    }
}
