using System;
using System.Collections.Generic;
using UnityEngine;

namespace ThreeBodyProblem.Editor
{
    public static partial class TravellerSceneBuilder
    {
        // Novel-inspired proportions, not dimensions specified by the novel.
        // The 120 x 156 m stone mass encloses a deliberately much smaller hall.
        private static void BuildRoyalCourt(Transform parent)
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
                StonePrism("Thick entrance tunnel wall", new[] { new Vector2(-48,4), new Vector2(5,4), new Vector2(5,7.4f), new Vector2(-44.6f,7.4f) }, side < 0 ? -4.3f : 2.3f, side < 0 ? -2.3f : 4.3f, false, "PyramidInterior", a);
                Box("Audience chamber front wall", new Vector3(side * 8.15f,8.2f,4), new Vector3(11.7f,8.4f,2), "PyramidInterior", a);
                Box("Audience chamber side wall", new Vector3(side * 15.5f,8.2f,24), new Vector3(3,8.4f,40), "PyramidInterior", a);
                foreach (float z in new[] { 12f, 37f })
                {
                    Box("Monolithic hall buttress", new Vector3(side * 12.7f,8.2f,z), new Vector3(2.2f,8.4f,2.6f), "PyramidStone", a);
                    Box("Buttress dressed stone capital", new Vector3(side * 12.7f,11.85f,z), new Vector3(2.8f,1.1f,3.2f), "PyramidStone", a,false);
                }
                // Small variations and real stone courses break up the close-range interior.
                for (int row=0;row<5;row++) for(int block=0;block<8;block++)
                    Box("Weathered inner stone course", new Vector3(side*13.96f,4.84f+row*1.5f,6.8f+block*4.65f+(row%2)*0.32f), new Vector3(0.11f,1.42f,4.52f), "PyramidInterior",a,false);
                Box("Throne backdrop stone slab", new Vector3(side*5.7f,8.5f,42.2f), new Vector3(4.6f,6.2f,0.5f), "PyramidStone",a,false);
                RitualRelief(new Vector3(side*5.7f,8.5f,41.86f),1.1f,a);
                BronzeDing(new Vector3(side*9.4f,4,30),1.7f,a);
            }
            StonePrism("Tunnel roof several metres thick", new[] {new Vector2(-44.6f,7.4f),new Vector2(5,7.4f),new Vector2(5,10.6f),new Vector2(-41.4f,10.6f)},-4.3f,4.3f,false,"PyramidInterior",a);
            Box("Audience chamber rear wall", new Vector3(0,8.2f,45), new Vector3(34,8.4f,4), "PyramidInterior", a);
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
            // Cut out the tunnel's east wall at the junction; individual leaves retain collision.
            foreach(var t in a.GetComponentsInChildren<Transform>())
                if(t.name=="Thick entrance tunnel wall"&&t.GetComponent<Collider>()!=null&&t.GetComponent<Collider>().bounds.center.x>0)
                { UnityEngine.Object.DestroyImmediate(t.gameObject); break; }
            StonePrism("Tunnel east wall south of junction",new[]{new Vector2(-48,4),new Vector2(-11,4),new Vector2(-11,7.4f),new Vector2(-44.6f,7.4f)},2.3f,4.3f,false,"PyramidInterior",a);
            Box("Tunnel east wall north of junction",new Vector3(3.3f,5.7f,-1),new Vector3(2,3.4f,12),"PyramidInterior",a);
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
            CombineCapitalBuilding(structure,"NovelPyramidPalaceV1",24f);
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
        }

        private static void BuildPyramidSkin(Transform parent)
        {
            for(int face=0;face<4;face++)
            {
                float half=face<2?60:78;
                Vector3 normal=(face==0?new Vector3(0,1,-1):face==1?new Vector3(0,1,1):face==2?new Vector3(1,60f/78,0):new Vector3(-1,60f/78,0)).normalized;
                Func<Vector2,Vector3> map=p=>face==0?new Vector3(p.x,4+p.y,-48+p.y):face==1?new Vector3(-p.x,4+p.y,108-p.y):face==2?new Vector3(60*(1-p.y/78),4+p.y,30+p.x):new Vector3(-60*(1-p.y/78),4+p.y,30-p.x);
                var cuts=new SortedSet<float>{0,3.4f,69.5f,73.5f,78};for(float y=2.6f;y<78;y+=2.6f)cuts.Add(y);
                var heights=new List<float>(cuts);var random=new System.Random(1803+face);
                var vertices=new List<Vector3>();var triangles=new List<int>();var uv=new List<Vector2>();
                for(int row=1;row<heights.Count;row++)
                {
                    float bottom=heights[row-1],top=heights[row];
                    var edges=new SortedSet<float>{-half,half};
                    for(float x=-half+(row%2)*2.75f;x<half;x+=5.5f)edges.Add(x);
                    if(face==0&&bottom<3.4f){edges.Add(-2.3f);edges.Add(2.3f);}
                    if(face==0&&bottom>=69.5f&&top<=73.5f){edges.Add(-2);edges.Add(2);}
                    if(face==2&&bottom<3.4f){edges.Add(-41);edges.Add(-37);}
                    var spans=new List<float>(edges);
                    for(int col=1;col<spans.Count;col++)
                    {
                        float left=spans[col-1],right=spans[col],middle=(left+right)*0.5f;
                        if(face==0&&bottom<3.4f&&middle>-2.3f&&middle<2.3f)continue;
                        if(face==0&&bottom>=69.5f&&top<=73.5f&&middle>-2&&middle<2)continue;
                        if(face==2&&bottom<3.4f&&middle>-41&&middle<-37)continue;
                        var poly=new List<Vector2>{new Vector2(-half,0),new Vector2(half,0),new Vector2(0,78)};
                        poly=ClipStonePolygon(poly,0,left,true);poly=ClipStonePolygon(poly,0,right,false);
                        poly=ClipStonePolygon(poly,1,bottom,true);poly=ClipStonePolygon(poly,1,top,false);
                        if(poly.Count<3)continue;
                        float area=0;for(int i=0;i<poly.Count;i++){var b=poly[(i+1)%poly.Count];area+=poly[i].x*b.y-b.x*poly[i].y;}
                        if(Mathf.Abs(area)<0.002f)continue;
                        Vector2 center=Vector2.zero;foreach(var p in poly)center+=p;center/=poly.Count;
                        var outer=new Vector3[poly.Count];var inner=new Vector3[poly.Count];var backing=new Vector3[poly.Count];
                        float relief=0.045f+(float)random.NextDouble()*0.045f;
                        for(int i=0;i<poly.Count;i++)
                        {
                            backing[i]=map(poly[i])-normal*0.035f;
                            Vector2 direction=(center-poly[i]).normalized;
                            outer[i]=map(poly[i]+direction*0.018f);
                            inner[i]=map(poly[i]+direction*0.065f)+normal*relief;
                        }
                        AddStonePolygon(vertices,triangles,uv,backing,normal);
                        AddStonePolygon(vertices,triangles,uv,inner,normal);
                        for(int i=0;i<poly.Count;i++){int j=(i+1)%poly.Count;AddStonePolygon(vertices,triangles,uv,new[]{outer[i],outer[j],inner[j],inner[i]},normal);}
                    }
                }
                MeshObject(new[]{"South pyramid face with low entrance and sky aperture","North pyramid stone face","East pyramid face with passage exit","West pyramid stone face"}[face],SaveMesh("NovelPyramidFaceV1_"+face,vertices,triangles,uv),Vector3.zero,"PyramidStone",parent,true);
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
