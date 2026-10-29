using System.Collections.Generic;
using UnityEngine;

namespace ThreeBodyProblem.Editor
{
    // Detailed rigid equipment follows the humanoid bones; character bodies remain skinned meshes.
    public static class TravellerCrowdEquipment
    {
        private sealed class Parts
        {
            public readonly List<Vector3> vertices = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int> triangles = new List<int>();
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int n = vertices.Count; vertices.AddRange(new[] { a, b, c, d });
                uv.AddRange(new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up });
                triangles.AddRange(new[] { n, n + 1, n + 2, n, n + 2, n + 3 });
            }
            public void Box(Vector3 p, Vector3 size)
            {
                Vector3 x = Vector3.right * size.x * .5f, y = Vector3.up * size.y * .5f, z = Vector3.forward * size.z * .5f;
                Quad(p-x-y+z,p+x-y+z,p+x+y+z,p-x+y+z); Quad(p+x-y-z,p-x-y-z,p-x+y-z,p+x+y-z);
                Quad(p-x-y-z,p-x-y+z,p-x+y+z,p-x+y-z); Quad(p+x-y+z,p+x-y-z,p+x+y-z,p+x+y+z);
                Quad(p-x+y+z,p+x+y+z,p+x+y-z,p-x+y-z); Quad(p-x-y-z,p+x-y-z,p+x-y+z,p-x-y+z);
            }
            public void Dome(Vector3 center, Vector3 radii, float endAngle = Mathf.PI, int rings = 14, int slices = 32)
            {
                int first = vertices.Count;
                for (int r = 0; r <= rings; r++)
                for (int s = 0; s <= slices; s++)
                {
                    float theta = endAngle * r / rings, phi = 2 * Mathf.PI * s / slices;
                    vertices.Add(center + Vector3.Scale(radii, new Vector3(Mathf.Sin(theta)*Mathf.Cos(phi),Mathf.Cos(theta),Mathf.Sin(theta)*Mathf.Sin(phi))));
                    uv.Add(new Vector2((float)s/slices, (float)r/rings));
                    if (r == rings || s == slices) continue;
                    int a = first + r*(slices+1)+s, b = a+slices+1;
                    triangles.AddRange(new[] { a, a+1, b+1, a, b+1, b });
                }
            }
            public void Shaft(Vector3 center, float radius, float length, int sides = 24)
            {
                for (int i = 0; i < sides; i++)
                {
                    float a = i * Mathf.PI * 2 / sides, b = (i+1)*Mathf.PI*2/sides;
                    Vector3 va = new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius), vb = new Vector3(Mathf.Cos(b)*radius,0,Mathf.Sin(b)*radius);
                    Quad(center+va-Vector3.up*length*.5f,center+vb-Vector3.up*length*.5f,center+vb+Vector3.up*length*.5f,center+va+Vector3.up*length*.5f);
                }
            }
            public Mesh Mesh(string name)
            {
                var mesh = new Mesh { name = name }; mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.RecalculateTangents(); return mesh;
            }
        }

        public static void AddShangArmour(GameObject root, Animator animator, Material bronze, Material leather)
        {
            var chest = new Parts();
            for (int side = -1; side <= 1; side += 2)
            for (int row = 0; row < 7; row++)
            for (int column = 0; column < 9; column++)
            {
                float x = (column-4)*.048f;
                float depth = side*(.22f + .06f*Mathf.Sqrt(Mathf.Max(0,1-x*x/.052f)));
                chest.Box(new Vector3(x,1.09f+row*.058f,depth),new Vector3(.044f,.065f,.012f));
            }
            Attach(root, animator.GetBoneTransform(HumanBodyBones.Chest), chest.Mesh("Shang lamellar chest and back"), bronze);
            var apron = new Parts();
            for (int row = 0; row < 5; row++)
            for (int column = 0; column < 10; column++)
            {
                float x=(column-4.5f)*.046f;
                apron.Box(new Vector3(x,.76f+row*.046f,.27f+.045f*Mathf.Sqrt(Mathf.Max(0,1-x*x/.052f))),new Vector3(.042f,.054f,.012f));
            }
            Attach(root,animator.GetBoneTransform(HumanBodyBones.Hips),apron.Mesh("Shang lamellar waist panels"),bronze);
            foreach (int side in new[] { -1, 1 })
            {
                var shoulder = new Parts();
                var shoulderPoint=root.transform.InverseTransformPoint(animator.GetBoneTransform(side<0?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm).position);
                shoulder.Dome(shoulderPoint+new Vector3(side*.02f,.035f,0),new Vector3(.115f,.065f,.14f),Mathf.PI*.6f,8,20);
                Attach(root,animator.GetBoneTransform(side<0?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm),shoulder.Mesh("Shang shoulder "+side),bronze);
            }
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            var center = root.transform.InverseTransformPoint(head.position) + Vector3.up*.2f;
            var helmet = new Parts(); helmet.Dome(center,new Vector3(.16f,.15f,.17f),Mathf.PI*.51f);
            for (int i = 0; i < 16; i++)
            {
                float a = i*Mathf.PI*2/16;
                helmet.Dome(center+new Vector3(Mathf.Cos(a)*.158f,0,Mathf.Sin(a)*.168f),Vector3.one*.006f,Mathf.PI,4,8);
            }
            helmet.Box(center+new Vector3(0,.143f,-.01f),new Vector3(.025f,.045f,.16f));
            Attach(root,head,helmet.Mesh("Shang bronze helmet with rivets"),bronze);
            AddWeapons(root,animator,bronze,leather);
        }

        public static void AddWeapons(GameObject root, Animator animator, Material metal, Material wood)
        {
            var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Vector3 grip = root.transform.InverseTransformPoint(hand.position) + Vector3.forward*.025f;
            var shaft = new Parts(); shaft.Shaft(grip+Vector3.up*.38f,.018f,2.05f);
            Attach(root,hand,shaft.Mesh("Guard hardwood spear shaft"),wood);
            var tip = new Parts();
            Vector3 basePoint = grip+Vector3.up*1.405f;
            tip.Quad(basePoint-Vector3.right*.075f,basePoint+Vector3.forward*.018f+Vector3.up*.15f,basePoint+Vector3.up*.36f,basePoint-Vector3.forward*.018f+Vector3.up*.15f);
            tip.Quad(basePoint+Vector3.right*.075f,basePoint-Vector3.forward*.018f+Vector3.up*.15f,basePoint+Vector3.up*.36f,basePoint+Vector3.forward*.018f+Vector3.up*.15f);
            tip.Quad(basePoint-Vector3.right*.075f,basePoint-Vector3.forward*.018f+Vector3.up*.15f,basePoint+Vector3.right*.075f,basePoint+Vector3.forward*.018f+Vector3.up*.15f);
            Attach(root,hand,tip.Mesh("Guard leaf spear head"),metal);
            var left = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            var shieldCenter = root.transform.InverseTransformPoint(left.position) + new Vector3(-.09f,-.18f,.13f);
            var board = new Parts(); var rim = new Parts();
            for (int i=0;i<40;i++)
            {
                float a=i*Mathf.PI*2/40,b=(i+1)*Mathf.PI*2/40;
                Vector3 va=new Vector3(Mathf.Cos(a)*.24f,Mathf.Sin(a)*.34f,0),vb=new Vector3(Mathf.Cos(b)*.24f,Mathf.Sin(b)*.34f,0);
                board.Quad(shieldCenter,shieldCenter+va,shieldCenter+vb,shieldCenter);
                board.Quad(shieldCenter-Vector3.forward*.04f,shieldCenter+vb-Vector3.forward*.04f,shieldCenter+va-Vector3.forward*.04f,shieldCenter-Vector3.forward*.04f);
                rim.Quad(shieldCenter+va*1.04f,shieldCenter+vb*1.04f,shieldCenter+vb*.95f,shieldCenter+va*.95f);
                rim.Quad(shieldCenter+va-Vector3.forward*.04f,shieldCenter+vb-Vector3.forward*.04f,shieldCenter+vb,shieldCenter+va);
            }
            rim.Dome(shieldCenter+Vector3.forward*.015f,new Vector3(.09f,.09f,.06f),Mathf.PI,10,20);
            Attach(root,left,board.Mesh("Guard curved wooden shield"),wood);
            Attach(root,left,rim.Mesh("Guard shield bronze rim and boss"),metal);
        }

        private static void Attach(GameObject root, Transform bone, Mesh mesh, Material material)
        {
            mesh.name=root.name+"_"+mesh.name;
            var saved = TravellerCrowdLibrary.SaveMesh(mesh);
            var part = new GameObject(saved.name,typeof(MeshFilter),typeof(MeshRenderer));
            part.transform.SetParent(root.transform,false);
            part.GetComponent<MeshFilter>().sharedMesh=saved;part.GetComponent<MeshRenderer>().sharedMaterial=material;
            part.transform.SetParent(bone,true);
        }
    }
}
