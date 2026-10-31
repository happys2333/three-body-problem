using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ThreeBodyProblem.Editor
{
    public static partial class TravellerSceneBuilder
    {
        // Novel-inspired proportions, not dimensions specified by the novel.
        // The 120 x 156 m stone mass encloses a deliberately much smaller hall.
        private static GameObject BuildRoyalStructure(Transform parent)
        {
            var structure = new GameObject("Pyramid masonry and enclosed palace");
            structure.transform.SetParent(parent); var a = structure.transform;
            Box("Pyramid solid foundation", new Vector3(0, 2, 30), new Vector3(120, 4, 156), "PyramidStone", a);
            BuildPyramidSkin(a);
            Box("Low entrance landing", new Vector3(0, 3.8f, -51.5f), new Vector3(14, 0.4f, 7), "PyramidFloor", a);
            CapitalRamp("Pyramid entrance ascent", new Vector3(-6, 0, -94), new Vector3(6, 4, -55), "CapitalRoad", a, false);
            foreach (float side in new[] { -1f, 1f })
            {
                // The sloping ends meet the pyramid face; the doorway remains genuinely open.
                // The east wall is built as two leaves below, leaving the garden junction open.
                // Building it whole and deleting only its core would leave dressed stone across the exit.
                if (side < 0)
                    StonePrism("Thick entrance tunnel wall", new[] { new Vector2(-48,4), new Vector2(5,4), new Vector2(5,7.4f), new Vector2(-44.6f,7.4f) }, -4.3f, -2.3f, false, "PyramidInterior", a);
                MasonryArchitectureLibrary.Wall("Audience chamber front wall", new Vector3(side * 8.15f,8.2f,4), new Vector3(11.7f,8.4f,2), Materials["PyramidInterior"], a);
                MasonryArchitectureLibrary.Wall("Audience chamber side wall", new Vector3(side * 15.5f,8.2f,24), new Vector3(3,8.4f,40), Materials["PyramidInterior"], a);
                foreach (float z in new[] { 12f, 37f })
                {
                    MasonryArchitectureLibrary.Wall("Monolithic hall buttress", new Vector3(side * 12.7f,8.2f,z), new Vector3(2.2f,8.4f,2.6f), Materials["PyramidStone"], a);
                    Box("Buttress dressed stone capital", new Vector3(side * 12.7f,11.85f,z), new Vector3(2.8f,1.1f,3.2f), "PyramidStone", a,false);
                }
                Box("Throne backdrop stone slab", new Vector3(side*5.7f,8.5f,42.2f), new Vector3(4.6f,6.2f,0.5f), "PyramidStone",a,false);
                RitualRelief(new Vector3(side*5.7f,8.5f,41.86f),1.1f,a);
                BronzeDing(new Vector3(side*9.4f,4,30),1.7f,a);
            }
            StonePrism("Tunnel roof several metres thick", new[] {new Vector2(-44.6f,7.4f),new Vector2(5,7.4f),new Vector2(5,10.6f),new Vector2(-41.4f,10.6f)},-4.3f,4.3f,false,"PyramidInterior",a);
            MasonryArchitectureLibrary.Wall("Audience chamber rear wall", new Vector3(0,8.2f,45), new Vector3(34,8.4f,4), Materials["PyramidInterior"], a);
            Box("Enclosed palace stone floor", new Vector3(0,3.85f,24), new Vector3(34,0.3f,42), "PyramidFloor", a);
            Box("Tunnel stone floor",new Vector3(0,3.88f,-22),new Vector3(8.6f,0.24f,52),"PyramidFloor",a);
            // The ceiling surrounds a 4 x 4 m shaft. No hidden solid roof blocks the opening.
            foreach(float side in new[]{-1f,1f})
                Box("Chamber ceiling beside skylight",new Vector3(side*9.5f,14.4f,24),new Vector3(15,4,42),"PyramidInterior",a);
            Box("Chamber ceiling south of skylight",new Vector3(0,14.4f,12.25f),new Vector3(4,4,18.5f),"PyramidInterior",a);
            Box("Chamber ceiling north of skylight",new Vector3(0,14.4f,35.25f),new Vector3(4,4,19.5f),"PyramidInterior",a);
            foreach(float side in new[]{-1f,1f})
                StonePrism("Vertical skylight stone reveal",new[]{new Vector2(21.5f,12.4f),new Vector2(25.5f,12.4f),new Vector2(25.5f,77.5f),new Vector2(21.5f,73.5f)},side<0?-3.1f:2,side<0?-2:3.1f,false,"PyramidInterior",a);
            Box("Skylight south reveal",new Vector3(0,42.95f,21),new Vector3(6.2f,61.1f,1),"PyramidInterior",a);
            Box("Skylight north reveal",new Vector3(0,44.95f,26),new Vector3(6.2f,65.1f,1),"PyramidInterior",a);
            foreach(float side in new[]{-1f,1f})
                Box("Heavy sliding slab channel",new Vector3(3,11.55f,23.5f+side*2.5f),new Vector3(11,0.45f,0.5f),"PyramidStone",a,false);
            // A side passage branches off the entrance tunnel, then slopes to the pool.
            foreach(float z in new[]{-11.8f,-6.2f})
                StonePrism("Thick east passage wall",new[]{new Vector2(4.3f,4),new Vector2(60,4),new Vector2(57.3846f,7.4f),new Vector2(4.3f,7.4f)},z-0.8f,z+0.8f,true,"PyramidInterior",a);
            StonePrism("East passage stone roof",new[]{new Vector2(2.3f,7.4f),new Vector2(57.3846f,7.4f),new Vector2(54.923f,10.6f),new Vector2(2.3f,10.6f)},-12.6f,-5.4f,true,"PyramidInterior",a);
            // Separate cores and dressed faces both stop at the open junction.
            StonePrism("Tunnel east wall south of junction",new[]{new Vector2(-48,4),new Vector2(-11,4),new Vector2(-11,7.4f),new Vector2(-44.6f,7.4f)},2.3f,4.3f,false,"PyramidInterior",a);
            MasonryArchitectureLibrary.Wall("Tunnel east wall north of junction",new Vector3(3.3f,5.7f,-1),new Vector3(2,3.4f,12),Materials["PyramidInterior"],a);
            Box("East passage stone floor",new Vector3(32.2f,3.85f,-9),new Vector3(64.4f,0.3f,7.2f),"PyramidFloor",a);
            CapitalRamp("Pyramid east garden descent",new Vector3(64,4,-13),new Vector3(82,0,-5),"CapitalRoad",a,true);
            for(int i=0;i<4;i++)
            {
                float height=(i+1)*0.45f;
                Box("Royal stone dais step",new Vector3(0,4+height*0.5f,31.7f+i*0.6f),new Vector3(13-i*1.3f,height,8-i*1.2f),"PyramidStone",a);
            }
            Box("Royal stone seat",new Vector3(0,6.05f,34),new Vector3(3.8f,0.5f,2.1f),"PyramidStone",a);
            Box("Royal stone seat back",new Vector3(0,7.1f,35),new Vector3(4.2f,2.6f,0.65f),"PyramidStone",a);
            Box("Calendar presentation table",new Vector3(-5,4.65f,17),new Vector3(3,1.3f,1.5f),"CapitalTimber",a);
            for(int i=0;i<7;i++)Box("Bamboo calendar slat",new Vector3(-5.05f+i*0.21f,5.33f,17),new Vector3(0.17f,0.03f,0.85f),"Ivory",a,false);
            foreach(float side in new[]{-1f,1f})
                Box("Sunlight measuring floor inlay",new Vector3(side*2.2f,4.018f,23.5f),new Vector3(0.055f,0.03f,5),"CapitalBronze",a,false);
            CombineCapitalBuilding(structure,"DressedPyramidPalaceV2",24f);
            return structure;
        }

        private static void BuildRoyalCourt(Transform parent)
        {
            BuildRoyalStructure(parent);
            // This collider moves with the roof plate and must not join a static batch.
            var cover=Box("Movable stone skylight cover",new Vector3(0,12.03f,23.5f),new Vector3(4.65f,0.72f,4.65f),"PyramidStone",parent);
            UnityEditor.GameObjectUtility.SetStaticEditorFlags(cover,0);
            var skylight=parent.gameObject.AddComponent<PalaceSkylight>();skylight.stage=stage;skylight.cover=cover.transform;
            skylight.closedPosition=cover.transform.localPosition;skylight.openPosition=skylight.closedPosition+Vector3.right*6.8f;skylight.SetOpening(0);
            CharacterRoot("纣王",new Vector3(0,5.8f,33),"CapitalOchreCloth",parent);
            CharacterRoot("周文王",new Vector3(-3,0,-230),"Robe",parent);
            var previous=world;world=parent;Hourglass(new Vector3(-5.8f,5.31f,17),1.2f);
            foreach(float side in new[]{-1f,1f})
            {
                Character("石殿卫兵",new Vector3(side*7,4,26),"Charcoal",false,false,true);
                Character("入口卫兵",new Vector3(side*4.6f,4,-50),"Charcoal",false,false,true);
                Torch(new Vector3(side*9.5f,4,9));Torch(new Vector3(side*9.5f,4,34));
            }
            foreach(float z in new[]{-39f,-23f,-5f})Torch(new Vector3(-1.65f,4,z));
            Torch(new Vector3(25,4,-10.2f));Torch(new Vector3(48,4,-10.2f));
            world=previous;
        }

        // A 2D y/profile extruded across x (tunnel) or z (east exit), with real collision.
        private static void StonePrism(string name,Vector2[] profile,float from,float to,bool alongX,string material,Transform parent)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();var uv=new List<Vector2>();
            Func<Vector2,float,Vector3> point=(v,t)=>alongX?new Vector3(v.x,v.y,t):new Vector3(t,v.y,v.x);
            var low=new Vector3[profile.Length];var high=new Vector3[profile.Length];
            for(int i=0;i<profile.Length;i++){low[i]=point(profile[i],from);high[i]=point(profile[i],to);}
            // A volume's centre lets every cap and side choose its outward winding.
            Vector3 center=Vector3.zero;foreach(var p in low)center+=p;foreach(var p in high)center+=p;center/=profile.Length*2;
            Action<Vector3[]> face=poly=>
            {
                Vector3 middle=Vector3.zero;foreach(var p in poly)middle+=p;middle/=poly.Length;
                AddStonePolygon(vertices,triangles,uv,poly,(middle-center).normalized);
            };
            face(low);face(high);
            for(int i=0;i<profile.Length;i++){int j=(i+1)%profile.Length;face(new[]{low[i],low[j],high[j],high[i]});}
            string key="Pyramid_"+name.Replace(" ","_")+"_"+from+"_"+to+"_"+profile[0]+"_"+profile[1];
            MeshObject(name,SaveMesh(key,vertices,triangles,uv),Vector3.zero,material,parent,true);
            if(material=="PyramidInterior" && !name.Contains("roof"))
                DressStoneProfile(name,profile,from,to,alongX,material,parent);
        }

        private static void DressStoneProfile(string name,Vector2[] profile,float from,float to,bool alongX,string material,Transform parent)
        {
            var chunks=new Dictionary<string,MasonryArchitectureLibrary.Chunk>();
            float min=profile.Min(p=>p.x),max=profile.Max(p=>p.x),low=profile.Min(p=>p.y),high=profile.Max(p=>p.y);
            for(int side=0;side<2;side++)
            {
                float plane=side==0?from:to;Vector3 normal=(alongX?Vector3.back:Vector3.left)*(side==0?1:-1);
                Func<Vector2,Vector3> map=p=>alongX?new Vector3(p.x,p.y,plane):new Vector3(plane,p.y,p.x);
                int row=0;
                for(float y=low;y<high-.001f;y+=1.4f,row++)for(float x=min-(row%2)*1.4f;x<max-.001f;x+=2.8f)
                {
                    var poly=new List<Vector2>(profile);poly=ClipStonePolygon(poly,0,Mathf.Max(x,min),true);poly=ClipStonePolygon(poly,0,Mathf.Min(x+2.8f,max),false);
                    poly=ClipStonePolygon(poly,1,y,true);poly=ClipStonePolygon(poly,1,Mathf.Min(y+1.4f,high),false);if(poly.Count<3)continue;
                    float area=0;for(int i=0;i<poly.Count;i++){var n=poly[(i+1)%poly.Count];area+=poly[i].x*n.y-n.x*poly[i].y;}if(Mathf.Abs(area)<.002f)continue;
                    string key="Dressed_"+name.Replace(" ","_")+"_"+from+"_"+to+"_S"+side+"_X"+Mathf.FloorToInt(x/12)+"_Y"+Mathf.FloorToInt(y/12);
                    MasonryArchitectureLibrary.Chunk chunk;if(!chunks.TryGetValue(key,out chunk)){chunk=new MasonryArchitectureLibrary.Chunk();chunks.Add(key,chunk);}
                    chunk.SlopingStone(poly.Select(map).ToArray(),normal,(to-from)*.5f);
                }
            }
            foreach(var pair in chunks)pair.Value.Save(pair.Key,parent,Materials[material]);
        }

        private static void BuildPyramidSkin(Transform parent)
        {
            for(int face=0;face<4;face++)
            {
                float half=face<2?60:78;
                Vector3 normal=(face==0?new Vector3(0,1,-1):face==1?new Vector3(0,1,1):face==2?new Vector3(1,60f/78,0):new Vector3(-1,60f/78,0)).normalized;
                Func<Vector2,Vector3> map=p=>face==0?new Vector3(p.x,4+p.y,-48+p.y):face==1?new Vector3(-p.x,4+p.y,108-p.y):face==2?new Vector3(60*(1-p.y/78),4+p.y,30+p.x):new Vector3(-60*(1-p.y/78),4+p.y,30-p.x);
                var chunks=new Dictionary<string,MasonryArchitectureLibrary.Chunk>();
                var heights=new SortedSet<float>{0,3.4f,69.5f,73.5f,78};for(float y=2.6f;y<78;y+=2.6f)heights.Add(y);
                var rows=new List<float>(heights);
                for(int row=1;row<rows.Count;row++)
                {
                    float bottom=rows[row-1],top=rows[row];var spans=new SortedSet<float>{-half,half};
                    for(float x=-half+(row%2)*2.75f;x<half;x+=5.5f)spans.Add(x);
                    if(face==0&&bottom<3.4f){spans.Add(-2.3f);spans.Add(2.3f);}
                    if(face==0&&bottom>=69.5f&&top<=73.5f){spans.Add(-2);spans.Add(2);}
                    if(face==2&&bottom<3.4f){spans.Add(-41);spans.Add(-37);}
                    var columns=new List<float>(spans);
                    for(int col=1;col<columns.Count;col++)
                    {
                        float left=columns[col-1],right=columns[col],middle=(left+right)*.5f;
                        if(face==0&&bottom<3.4f&&middle>-2.3f&&middle<2.3f)continue;
                        if(face==0&&bottom>=69.5f&&top<=73.5f&&middle>-2&&middle<2)continue;
                        if(face==2&&bottom<3.4f&&middle>-41&&middle<-37)continue;
                        var poly=new List<Vector2>{new Vector2(-half,0),new Vector2(half,0),new Vector2(0,78)};
                        poly=ClipStonePolygon(poly,0,left,true);poly=ClipStonePolygon(poly,0,right,false);poly=ClipStonePolygon(poly,1,bottom,true);poly=ClipStonePolygon(poly,1,top,false);
                        if(poly.Count<3)continue;
                        float area=0;for(int i=0;i<poly.Count;i++){var next=poly[(i+1)%poly.Count];area+=poly[i].x*next.y-next.x*poly[i].y;}
                        if(Mathf.Abs(area)<.002f)continue;
                        string key="PalaceExterior_F"+face+"_X"+Mathf.FloorToInt(middle/16)+"_Y"+Mathf.FloorToInt(bottom/12);
                        MasonryArchitectureLibrary.Chunk chunk;if(!chunks.TryGetValue(key,out chunk)){chunk=new MasonryArchitectureLibrary.Chunk();chunks.Add(key,chunk);}
                        chunk.SlopingStone(poly.Select(map).ToArray(),normal,1.8f);
                    }
                }
                foreach(var pair in chunks)pair.Value.Save(pair.Key,parent,Materials["PyramidStone"]);
            }
        }

        private static List<Vector2> ClipStonePolygon(List<Vector2> input,int axis,float boundary,bool greater)
        {
            var output=new List<Vector2>();if(input.Count==0)return output;
            Vector2 previous=input[input.Count-1];bool wasInside=greater?previous[axis]>=boundary:previous[axis]<=boundary;
            foreach(var current in input)
            {
                bool inside=greater?current[axis]>=boundary:current[axis]<=boundary;
                if(inside!=wasInside)
                {
                    float t=(boundary-previous[axis])/(current[axis]-previous[axis]);output.Add(Vector2.Lerp(previous,current,t));
                }
                if(inside)output.Add(current);previous=current;wasInside=inside;
            }
            for(int i=output.Count-1;i>=0;i--)
                if(output.Count>1&&(output[i]-output[(i+1)%output.Count]).sqrMagnitude<0.000001f)output.RemoveAt(i);
            return output;
        }

        private static void AddStonePolygon(List<Vector3> vertices,List<int> triangles,List<Vector2> uv,Vector3[] poly,Vector3 outward)
        {
            if(poly.Length<3)return;int start=vertices.Count;
            Vector3 normal=Vector3.Cross(poly[1]-poly[0],poly[2]-poly[0]);
            Vector3 u=Mathf.Abs(outward.x)>0.7f?Vector3.forward:Vector3.right;
            Vector3 v=Vector3.Cross(outward,u).normalized;
            foreach(var p in poly){vertices.Add(p);uv.Add(new Vector2(Vector3.Dot(p,u),Vector3.Dot(p,v))*0.16f);}
            for(int i=1;i<poly.Length-1;i++)
                if(Vector3.Dot(normal,outward)>=0)triangles.AddRange(new[]{start,start+i,start+i+1});
                else triangles.AddRange(new[]{start,start+i+1,start+i});
        }

        // Original abstract paired eyes and scrolls; decorative, not claimed to
        // reproduce a particular excavated vessel or to be readable oracle script.
        private static void RitualRelief(Vector3 p, float scale, Transform parent)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                var eye = Shape("Ritual bronze eye surround", PrimitiveType.Sphere, p + new Vector3(side * scale * 0.45f, 0, 0), new Vector3(scale * 0.59f, scale * 0.32f, 0.12f), "RoyalBronzeGold", parent, false);
                Shape("Ritual eye inset", PrimitiveType.Sphere, eye.transform.position + new Vector3(0, 0, -0.08f), new Vector3(scale * 0.22f, scale * 0.22f, 0.09f), "CapitalBronze", parent, false);
                for (int i = 0; i < 4; i++)
                {
                    float x = side * (0.22f + i * 0.18f) * scale;
                    Box("Geometric bronze scroll", p + new Vector3(x, scale * (0.35f + (i % 2) * 0.12f), 0), new Vector3(scale * 0.15f, scale * 0.13f, 0.1f), "RoyalBronzeGold", parent, false);
                }
                Beam("Swept ritual horn", p + new Vector3(side * 0.35f * scale, 0.52f * scale, 0), p + new Vector3(side * 0.8f * scale, scale, 0), scale * 0.09f, "RoyalBronzeGold", parent);
            }
            Box("Ritual central nose ridge", p + new Vector3(0, -scale * 0.15f, 0), new Vector3(scale * 0.13f, scale * 0.9f, 0.18f), "RoyalBronzeGold", parent, false);
            foreach (float side in new[] { -1f, 1f }) Box("Ritual jaw scroll", p + new Vector3(side * scale * 0.33f, -scale * 0.48f, 0), new Vector3(scale * 0.47f, scale * 0.12f, 0.1f), "RoyalBronzeGold", parent, false);
        }

    }
}
