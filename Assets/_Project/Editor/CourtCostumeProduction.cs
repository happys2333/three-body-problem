using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ThreeBodyProblem.Editor
{
    public static class CourtCostumeProduction
    {
        public static void Add(TravellerActor actor,string id,Color color)
        {
            TravellerStoryCharacters.WithSample(actor.animator,"MX_Idle",.25f,()=>{
                var bones=new[]{B(actor,HumanBodyBones.Hips),B(actor,HumanBodyBones.Chest),B(actor,HumanBodyBones.LeftUpperLeg),B(actor,HumanBodyBones.RightUpperLeg),
                    B(actor,HumanBodyBones.LeftUpperArm),B(actor,HumanBodyBones.LeftLowerArm),B(actor,HumanBodyBones.RightUpperArm),B(actor,HumanBodyBones.RightLowerArm),B(actor,HumanBodyBones.Head)};
                var build=new Tailor(actor.transform,bones);
                build.Robe();build.Sleeve(4,5,B(actor,HumanBodyBones.LeftHand));build.Sleeve(6,7,B(actor,HumanBodyBones.RightHand));build.Crown();
                var mesh=build.Save(id+"_CeremonialRobe");
                var child=new GameObject("Skinned long robe, sleeves and court crown");child.transform.SetParent(actor.transform,false);
                var skin=child.AddComponent<SkinnedMeshRenderer>();skin.sharedMesh=mesh;skin.bones=bones;skin.rootBone=bones[0];skin.localBounds=new Bounds(new Vector3(0,1.0f,0),new Vector3(2.2f,2.5f,1.6f));
                skin.sharedMaterials=new[]{Cloth(id,color),AssetDatabase.LoadAssetAtPath<Material>(PalaceInteriorProduction.Root+"/Materials/PatinatedBronze.mat"),Cloth("CrownBlack",new Color(.035f,.027f,.023f))};
            });
        }
        private static Transform B(TravellerActor actor,HumanBodyBones bone)
        {var result=actor.animator.GetBoneTransform(bone);return result!=null?result:actor.animator.GetBoneTransform(HumanBodyBones.Spine);}
        private static Material Cloth(string id,Color color)
        {
            string path=CourtRetinueProduction.Root+"/Materials/"+id+"_Robe.mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Traveller/Palace Craft Surface"));AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_Color",color);m.SetColor("_Secondary",color*.7f);m.SetFloat("_Metallic",0);m.SetFloat("_Smoothness",.12f);m.SetFloat("_Hide",0);m.SetFloat("_PatternScale",65);EditorUtility.SetDirty(m);return m;
        }
        private sealed class Tailor
        {
            private readonly Transform root;
            private readonly Transform[] bones;
            private readonly List<Vector3> v=new List<Vector3>();private readonly List<Vector2> uv=new List<Vector2>();private readonly List<BoneWeight> w=new List<BoneWeight>();
            private readonly List<int>[] triangles={new List<int>(),new List<int>(),new List<int>()};
            public Tailor(Transform root,Transform[] bones){this.root=root;this.bones=bones;}
            private Vector3 P(int i)=>root.InverseTransformPoint(bones[i].position);
            private static BoneWeight Weight(int a,int b=0,float blend=0)=>new BoneWeight{boneIndex0=a,weight0=1-blend,boneIndex1=b,weight1=blend};
            private int Vertex(Vector3 p,BoneWeight weight,Vector2 tex){int n=v.Count;v.Add(p);w.Add(weight);uv.Add(tex);return n;}
            private void Quad(int a,int b,int c,int d,int slot){triangles[slot].AddRange(new[]{a,b,c,a,c,d});}
            public void Robe()
            {
                const int segments=64;const int rings=9;
                float hip=P(0).y;int start=v.Count;
                for(int j=0;j<rings;j++)
                {
                    float t=(float)j/(rings-1),y=Mathf.Lerp(.14f,hip+.08f,t);
                    for(int i=0;i<=segments;i++)
                    {
                        float a=i*Mathf.PI*2/segments;float fold=.014f*Mathf.Sin(a*12)*Mathf.Lerp(1,.25f,t);
                        float x=Mathf.Sin(a)*(Mathf.Lerp(.43f,.29f,t)+fold),z=Mathf.Cos(a)*(Mathf.Lerp(.30f,.20f,t)+fold);
                        int leg=x<0?2:3;float influence=Mathf.Lerp(.7f,0,Mathf.Clamp01(t*1.6f));
                        Vertex(new Vector3(x,y,z),Weight(0,leg,influence),new Vector2((float)i/segments,t));
                        if(j>0&&i<segments&&!(j<4&&(i==0||i==segments-1)))
                        {int n=start+j*(segments+1)+i;Quad(n,n-segments-1,n-segments,n+1,0);}
                    }
                }
                // A fitted outer torso covers the travelling coat under the formal robe.
                int torso=v.Count;
                for(int j=0;j<4;j++)for(int i=0;i<=segments;i++)
                {
                    float t=j/3f,a=i*Mathf.PI*2/segments;
                    Vertex(new Vector3(Mathf.Sin(a)*Mathf.Lerp(.30f,.245f,t),Mathf.Lerp(hip-.01f,P(1).y+.10f,t),Mathf.Cos(a)*Mathf.Lerp(.205f,.155f,t)),Weight(0,1,t),new Vector2((float)i/segments,t));
                    if(j>0&&i<segments){int n=torso+j*(segments+1)+i;Quad(n,n-segments-1,n-segments,n+1,0);}
                }
                // Bronze-edged belt, independently weighted to the hips.
                Band(new Vector3(0,hip+.02f,0),.31f,.213f,.045f,0,1);
            }
            public void Sleeve(int upper,int lower,Transform hand)
            {
                var shoulder=P(upper);var elbow=P(lower);var wrist=root.InverseTransformPoint(hand.position);
                const int rings=9,segments=24;int start=v.Count;
                for(int j=0;j<rings;j++)
                {
                    float t=j/(float)(rings-1);Vector3 centre=t<.5f?Vector3.Lerp(shoulder,elbow,t*2):Vector3.Lerp(elbow,wrist,(t-.5f)*1.85f);
                    var axis=(t<.5f?elbow-shoulder:wrist-elbow).normalized;var across=Vector3.Cross(axis,Vector3.forward).normalized;if(across.sqrMagnitude<.1f)across=Vector3.right;
                    var depth=Vector3.Cross(axis,across).normalized;float r=t<.5f?Mathf.Lerp(.112f,.17f,t*2):Mathf.Lerp(.17f,.115f,(t-.5f)*2);
                    for(int i=0;i<=segments;i++)
                    {
                        float a=i*Mathf.PI*2/segments;float fold=1+.045f*Mathf.Sin(a*6+j*.3f);
                        Vertex(centre+(across*Mathf.Cos(a)+depth*Mathf.Sin(a))*r*fold,Weight(upper,lower,Mathf.SmoothStep(0,1,t)),new Vector2((float)i/segments,t));
                        if(j>0&&i<segments){int n=start+j*(segments+1)+i;Quad(n-segments-1,n-segments,n+1,n,t>.88f?1:0);}
                    }
                }
            }
            private void Band(Vector3 centre,float x,float z,float height,int bone,int slot)
            {
                int start=v.Count;const int n=48;
                for(int j=0;j<2;j++)for(int i=0;i<=n;i++)
                {float a=i*Mathf.PI*2/n;Vertex(centre+new Vector3(Mathf.Sin(a)*x,(j-.5f)*height,Mathf.Cos(a)*z),Weight(bone),new Vector2((float)i/n,j));if(j>0&&i<n){int k=start+n+1+i;Quad(k,k-n-1,k-n,k+1,slot);}}
            }
            public void Crown()
            {
                var centre=P(8)+Vector3.up*.12f;const int n=32;int start=v.Count;
                for(int j=0;j<3;j++)for(int i=0;i<=n;i++)
                {
                    float a=i*Mathf.PI*2/n;float t=j*.5f;
                    float x=Mathf.Sign(Mathf.Sin(a))*Mathf.Pow(Mathf.Abs(Mathf.Sin(a)),.45f)*Mathf.Lerp(.135f,.115f,t);
                    float z=Mathf.Sign(Mathf.Cos(a))*Mathf.Pow(Mathf.Abs(Mathf.Cos(a)),.45f)*Mathf.Lerp(.105f,.08f,t);
                    Vertex(centre+new Vector3(x,t*.205f,z),Weight(8),new Vector2((float)i/n,t));
                    if(j>0&&i<n){int k=start+j*(n+1)+i;Quad(k,k-n-1,k-n,k+1,2);}
                }
                int top=Vertex(centre+Vector3.up*.205f,Weight(8),Vector2.one*.5f);
                for(int i=0;i<n;i++)triangles[2].AddRange(new[]{top,start+2*(n+1)+i,start+2*(n+1)+i+1});
                Band(centre+Vector3.up*.025f,.139f,.109f,.018f,8,1);
            }
            public Mesh Save(string id)
            {
                var mesh=new Mesh{name=id,indexFormat=IndexFormat.UInt32,vertices=v.ToArray(),uv=uv.ToArray(),boneWeights=w.ToArray(),subMeshCount=3};
                for(int i=0;i<3;i++)mesh.SetTriangles(triangles[i],i);
                var bind=new Matrix4x4[bones.Length];for(int i=0;i<bones.Length;i++)bind[i]=bones[i].worldToLocalMatrix*root.localToWorldMatrix;mesh.bindposes=bind;
                mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
                string path=CourtRetinueProduction.Root+"/Meshes/"+id+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(saved==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}EditorUtility.CopySerialized(mesh,saved);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(saved);return saved;
            }
        }
    }
}
