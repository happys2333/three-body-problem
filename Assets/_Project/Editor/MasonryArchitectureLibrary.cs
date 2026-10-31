using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ThreeBodyProblem.Editor
{
    // Authored metre-scale masonry. Bricks are baked into small spatial chunks,
    // while collision remains independent of the visible LOD.
    public static class MasonryArchitectureLibrary
    {
        public const string Root = "Assets/_Project/Art/MonumentalMasonry";
        public const string Docs = "Documentation/Architecture/MonumentalMasonry";
        public static Material Stone, Interior;

        public static void EnsureMaterials()
        {
            foreach (string path in new[] { Root + "/Meshes", Root + "/Materials", Root + "/Prefabs", Docs }) Directory.CreateDirectory(path);
            Stone = Surface("Dressed sandstone blocks", new Color(.91f, .88f, .80f));
            Interior = Surface("Interior sandstone blocks", new Color(.62f, .60f, .55f));
        }
        private static Material Surface(string name, Color tint)
        {
            var source = AssetDatabase.LoadAssetAtPath<Material>(EnvironmentProduction.Root + "/Materials/Rock.mat");
            if (source == null) throw new FileNotFoundException("The photographic rock surface is required.");
            string path = Root + "/Materials/" + name + ".mat";
            var value = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (value == null) { value = new Material(source); AssetDatabase.CreateAsset(value, path); }
            else value.CopyPropertiesFromMaterial(source);
            value.name=name; value.color = tint; value.SetFloat("_DetailMetres", 2.2f); value.enableInstancing = true; EditorUtility.SetDirty(value); return value;
        }

        public sealed class Geometry
        {
            public readonly List<Vector3> vertices = new List<Vector3>(), normals = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int> indices = new List<int>();
            public void Face(Vector3[] points, Vector3 outward)
            {
                if (points.Length < 3) return;
                var normal = Vector3.Cross(points[1] - points[0], points[2] - points[0]);
                for(int i=3;normal.sqrMagnitude<1e-12f&&i<points.Length;i++)normal=Vector3.Cross(points[1]-points[0],points[i]-points[0]);
                if (normal.sqrMagnitude < 1e-12f) return;
                bool flip = Vector3.Dot(normal, outward) < 0; normal = (flip ? -normal : normal).normalized;
                int start = vertices.Count;
                foreach (var p in points)
                {
                    vertices.Add(p); normals.Add(normal);
                    uv.Add(Mathf.Abs(normal.y) > .7f ? new Vector2(p.x, p.z) : Mathf.Abs(normal.x) > .7f ? new Vector2(p.z, p.y) : new Vector2(p.x, p.y));
                }
                for (int i = 1; i < points.Length - 1; i++) if(Vector3.Cross(points[i]-points[0],points[i+1]-points[0]).sqrMagnitude>1e-12f)
                    indices.AddRange(flip ? new[] { start, start + i + 1, start + i } : new[] { start, start + i, start + i + 1 });
            }
            public void Box(Vector3 centre, Vector3 size, Matrix4x4 matrix, float bevel)
            {
                var half = size * .5f; bevel = Mathf.Min(bevel, Mathf.Min(half.x, Mathf.Min(half.y, half.z)) * .35f);
                var inner = half - Vector3.one * bevel;
                Action<Vector3[], Vector3> face = (p, n) => Face(p.Select(v => matrix.MultiplyPoint3x4(v + centre)).ToArray(), matrix.MultiplyVector(n));
                for (int axis = 0; axis < 3; axis++) foreach (float sign in new[] { -1f, 1f })
                {
                    int b = (axis + 1) % 3, c = (axis + 2) % 3; var points = new List<Vector3>();
                    foreach (var corner in new[] { new Vector2(-1,-1), new Vector2(1,-1), new Vector2(1,1), new Vector2(-1,1) })
                    { var p = Vector3.zero; p[axis] = sign * half[axis]; p[b] = corner.x * inner[b]; p[c] = corner.y * inner[c]; points.Add(p); }
                    var n = Vector3.zero; n[axis] = sign; face(points.ToArray(), n);
                }
                if (bevel <= 0) return;
                for (int axis = 0; axis < 3; axis++) foreach (float sb in new[] { -1f, 1f }) foreach (float sc in new[] { -1f, 1f })
                {
                    int b = (axis + 1) % 3, c = (axis + 2) % 3; var p = new Vector3[4];
                    for (int i = 0; i < 4; i++) { p[i][axis] = (i == 0 || i == 3 ? -1 : 1) * inner[axis]; p[i][b] = sb * (i < 2 ? half[b] : inner[b]); p[i][c] = sc * (i < 2 ? inner[c] : half[c]); }
                    var n = Vector3.zero; n[b] = sb; n[c] = sc; face(p, n.normalized);
                }
                foreach (float sx in new[] { -1f, 1f }) foreach (float sy in new[] { -1f, 1f }) foreach (float sz in new[] { -1f, 1f })
                    face(new[] { new Vector3(sx*half.x,sy*inner.y,sz*inner.z), new Vector3(sx*inner.x,sy*half.y,sz*inner.z), new Vector3(sx*inner.x,sy*inner.y,sz*half.z) }, new Vector3(sx,sy,sz).normalized);
            }
            public void Prism(Vector3[] polygon, Vector3 normal, float depth, float gap, float bevel, float relief)
            {
                var centre = polygon.Aggregate(Vector3.zero, (s,p) => s+p) / polygon.Length;
                var rim = polygon.Select(p => p + (centre-p).normalized * gap).ToArray();
                var front = rim.Select(p => p + (centre-p).normalized * bevel + normal * relief).ToArray();
                var back = rim.Select(p => p-normal*depth).ToArray();
                Face(front, normal); Face(back, -normal);
                for (int i=0;i<polygon.Length;i++)
                {
                    int j=(i+1)%polygon.Length; var side=(rim[i]+rim[j])*.5f-centre;
                    if (bevel>0 || relief>0) Face(new[]{rim[i],rim[j],front[j],front[i]},side+normal);
                    Face(new[]{back[i],back[j],rim[j],rim[i]},side);
                }
            }
        }

        public sealed class Chunk
        {
            public readonly Geometry[] lod = { new Geometry(), new Geometry(), new Geometry() };
            public readonly Geometry collision = new Geometry();
            public int blocks;
            public void SlopingStone(Vector3[] polygon, Vector3 normal, float depth)
            {
                float relief=.10f+.10f*((blocks*73)%17)/16f;
                lod[0].Prism(polygon,normal,depth,.025f,.08f,relief);
                lod[0].Face(polygon.Select(p=>p-normal*.12f).ToArray(),normal);
                lod[1].Prism(polygon,normal,depth,.012f,0,.06f);
                lod[1].Face(polygon.Select(p=>p-normal*.12f).ToArray(),normal);
                lod[2].Face(polygon.Select(p=>p+normal*.02f).ToArray(),normal);
                collision.Prism(polygon,normal,depth,0,0,.20f); blocks++;
            }
            public GameObject Save(string key, Transform parent, Material material, Bounds? solidBox=null)
            {
                var root = new GameObject(key); root.transform.SetParent(parent,false);
                var levels = new LOD[3];
                for(int level=0;level<3;level++)
                {
                    var mesh=SaveMesh(key+"_LOD"+level,lod[level]);
                    var child=new GameObject("LOD "+level,typeof(MeshFilter),typeof(MeshRenderer));child.transform.SetParent(root.transform,false);
                    child.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=child.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
                    renderer.shadowCastingMode=level==2?ShadowCastingMode.Off:ShadowCastingMode.On;
                    levels[level]=new LOD(level==0?.22f:level==1?.055f:.001f,new Renderer[]{renderer});
                    GameObjectUtility.SetStaticEditorFlags(child,StaticEditorFlags.OccluderStatic|StaticEditorFlags.OccludeeStatic);
                }
                var group=root.AddComponent<LODGroup>();group.SetLODs(levels);group.RecalculateBounds();
                var solid=new GameObject("Independent collision");solid.transform.SetParent(root.transform,false);
                if(solidBox.HasValue){var box=solid.AddComponent<BoxCollider>();box.center=solidBox.Value.center;box.size=solidBox.Value.size;}
                else {var collider=solid.AddComponent<MeshCollider>();collider.sharedMesh=SaveMesh(key+"_Collision",collision);}
                root.isStatic=true;solid.isStatic=true;return root;
            }
        }

        public static Mesh SaveMesh(string key, Geometry data)
        {
            string safe=key.Replace(' ','_').Replace('/','_');string path=Root+"/Meshes/"+safe+".asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null){mesh=new Mesh{name=safe};AssetDatabase.CreateAsset(mesh,path);}else mesh.Clear();
            mesh.indexFormat=IndexFormat.UInt32;mesh.SetVertices(data.vertices);mesh.SetNormals(data.normals);mesh.SetUVs(0,data.uv);mesh.SetTriangles(data.indices,0);
            mesh.RecalculateBounds();mesh.RecalculateTangents();
            var tangents=mesh.tangents;
            for(int i=0;i<tangents.Length;i++)if(new Vector3(tangents[i].x,tangents[i].y,tangents[i].z).sqrMagnitude<.1f)
            {var n=data.normals[i];var t=Vector3.Cross(Mathf.Abs(n.y)>.95f?Vector3.right:Vector3.up,n).normalized;tangents[i]=new Vector4(t.x,t.y,t.z,1);}
            mesh.tangents=tangents;mesh.UploadMeshData(false);EditorUtility.SetDirty(mesh);return mesh;
        }

        public static GameObject Wall(string key, Vector3 centre, Vector3 size, Material material, Transform parent)
        {
            var root=new GameObject(key);root.transform.SetParent(parent,false);
            string geometryKey=key+"_P"+centre.ToString("F3",System.Globalization.CultureInfo.InvariantCulture).Replace(" ","")+"_D"+size.ToString("F3",System.Globalization.CultureInfo.InvariantCulture).Replace(" ","");
            bool alongZ=size.z>size.x;float length=alongZ?size.z:size.x,depth=alongZ?size.x:size.z;
            var matrix=Matrix4x4.TRS(centre,alongZ?Quaternion.Euler(0,90,0):Quaternion.identity,Vector3.one);
            int xi=0;
            for(float x=-length*.5f;x<length*.5f-.001f;x+=8.4f,xi++)
            {
                float width=Mathf.Min(8.4f,length*.5f-x);int yi=0;
                for(float y=-size.y*.5f;y<size.y*.5f-.001f;y+=4.2f,yi++)
                {
                    float height=Mathf.Min(4.2f,size.y*.5f-y);var chunk=new Chunk();
                    var localCentre=new Vector3(x+width*.5f,y+height*.5f,0);var dimensions=new Vector3(width,height,depth);
                    // A mortar core seals the tiny visual joints. The blocks remain actual closed volumes.
                    chunk.lod[0].Box(localCentre,new Vector3(width,height,Mathf.Max(.02f,depth-.20f)),matrix,0);
                    chunk.lod[1].Box(localCentre,new Vector3(width,height,Mathf.Max(.02f,depth-.10f)),matrix,0);
                    chunk.lod[2].Box(localCentre,dimensions,matrix,0);
                    int rowIndex=0;
                    for(float row=y;row<y+height-.001f;row+=1.4f,rowIndex++)
                    {
                        float h=Mathf.Min(1.4f,y+height-row);float start=x-(rowIndex%2)*1.4f;
                        for(float brick=start;brick<x+width-.001f;brick+=2.8f)
                        {
                            float left=Mathf.Max(x,brick),right=Mathf.Min(x+width,brick+2.8f);if(right-left<.04f)continue;
                            var p=new Vector3((left+right)*.5f,row+h*.5f,0);var s=new Vector3(right-left-.025f,h-.025f,depth);
                            chunk.lod[0].Box(p,s,matrix,.055f);chunk.lod[1].Box(p,s,matrix,0);chunk.blocks++;
                        }
                    }
                    var worldSize=alongZ?new Vector3(depth,height,width):dimensions;
                    chunk.Save(geometryKey+"_"+xi+"_"+yi,root.transform,material,new Bounds(matrix.MultiplyPoint3x4(localCentre),worldSize));
                }
            }
            return root;
        }

        [MenuItem("Tools/Three Body Traveller/Assets/Build Monumental Masonry Kit")]
        public static void BuildKit()
        {
            EnsureMaterials();var scene=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            try
            {
                foreach(string id in new[]{"GiantStone_2p8x1p4x1p8","ThickWall_8p4x4p2x4p2","ThickCorner_8p4","Doorway_5p6_Clear","Buttress_8p4_High","Lintel_8p4","StoneFloor_8p4"})
                {
                    var root=new GameObject(id);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
                    if(id.StartsWith("GiantStone"))
                    {
                        var c=new Chunk();var centre=Vector3.up*.7f;var dimensions=new Vector3(2.8f,1.4f,1.8f);
                        c.lod[0].Box(centre,dimensions,Matrix4x4.identity,.055f);c.lod[1].Box(centre,dimensions,Matrix4x4.identity,0);c.lod[2].Box(centre,dimensions,Matrix4x4.identity,0);
                        c.Save(id+"_Block",root.transform,Stone,new Bounds(centre,dimensions));
                    }
                    else if(id.StartsWith("ThickWall"))Wall(id+"_Panel",Vector3.up*2.1f,new Vector3(8.4f,4.2f,4.2f),Stone,root.transform);
                    else if(id.StartsWith("ThickCorner"))
                    {Wall(id+"_A",new Vector3(0,2.1f,-2.1f),new Vector3(8.4f,4.2f,4.2f),Stone,root.transform);Wall(id+"_B",new Vector3(2.1f,2.1f,2.1f),new Vector3(4.2f,4.2f,4.2f),Stone,root.transform);}
                    else if(id.StartsWith("Doorway"))
                    {foreach(float side in new[]{-1f,1f})Wall(id+"_Pier"+side,new Vector3(side*4.2f,2.1f,0),new Vector3(2.8f,4.2f,4.2f),Stone,root.transform);Wall(id+"_Head",new Vector3(0,5.25f,0),new Vector3(11.2f,2.1f,4.2f),Stone,root.transform);}
                    else if(id.StartsWith("Buttress"))Wall(id+"_Panel",Vector3.up*4.2f,new Vector3(4.2f,8.4f,4.2f),Stone,root.transform);
                    else if(id.StartsWith("Lintel"))Wall(id+"_Panel",Vector3.up*1.05f,new Vector3(8.4f,2.1f,4.2f),Stone,root.transform);
                    else Wall(id+"_Panel",Vector3.up*.7f,new Vector3(8.4f,1.4f,8.4f),Stone,root.transform);
                    PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/"+id+".prefab");Object.DestroyImmediate(root);
                }
            }
            finally{UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);}
            AssetDatabase.SaveAssets();
        }
    }
}
