using System;
using System.IO;
using Hiharu.BlendShapeTools;
using UnityEditor;
using UnityEngine;

public static class SplitterVerification
{
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    public static void Run()
    {
        var source = new Mesh { name = "SyntheticFixture" };
        source.vertices = new[] { new Vector3(-1,-1,-1), Vector3.zero, new Vector3(1,1,1) };
        source.triangles = new[] { 0,1,2 };
        source.uv = new[] { Vector2.zero, Vector2.one, Vector2.right };
        source.bindposes = new[] { Matrix4x4.identity };
        source.boneWeights = new[] { Weight(), Weight(), Weight() };
        var a = new[] { Vector3.up, Vector3.right, Vector3.forward };
        var b = new[] { Vector3.up * 2, Vector3.right * 2, Vector3.forward * 2 };
        source.AddBlendShapeFrame("Original", 50, a, a, a);
        source.AddBlendShapeFrame("Original", 100, b, b, b);
        source.AddBlendShapeFrame("Other", 100, a, a, a);
        for (int axis=0; axis<3; axis++)
        {
            var copy = SideSplitter.Split(source, 0, axis, 0, "Negative", "Positive");
            Check(copy.blendShapeCount == 4 && source.blendShapeCount == 2, "Original mutation/count");
            Check(copy.bindposes.Length == 1 && copy.boneWeights.Length == 3 && copy.uv[1] == Vector2.one && copy.triangles.Length == 3, "Mesh attributes lost");
            for (int f=0; f<2; f++)
            {
                var n=new Vector3[3]; var nn=new Vector3[3]; var nt=new Vector3[3];
                var p=new Vector3[3]; var pn=new Vector3[3]; var pt=new Vector3[3];
                copy.GetBlendShapeFrameVertices(2,f,n,nn,nt);
                copy.GetBlendShapeFrameVertices(3,f,p,pn,pt);
                Check(copy.GetBlendShapeFrameWeight(2,f)==(f==0?50:100), "Frame weight lost");
                for(int i=0;i<3;i++)
                {
                    var expected=f==0?a[i]:b[i];
                    Check((n[i]+p[i]-expected).sqrMagnitude<1e-10f && (nn[i]+pn[i]-expected).sqrMagnitude<1e-10f && (nt[i]+pt[i]-expected).sqrMagnitude<1e-10f,"Split conservation");
                }
                Check(p[0]==Vector3.zero && n[2]==Vector3.zero && n[1]==p[1],"Wrong side or boundary");
            }
            UnityEngine.Object.DestroyImmediate(copy);
        }
        Reject(() => SideSplitter.Split(source,0,0,0,"Original","New"));
        Reject(() => SideSplitter.Split(source,0,0,0,"Same","Same"));
        Reject(() => SideSplitter.Split(source,0,0,5,"N","P"));
        Reject(() => SideSplitter.Split(source,0,0,float.NaN,"N","P"));
        var persistent=SideSplitter.Split(source,0,0,0,"N","P");
        AssetDatabase.CreateAsset(persistent,AssetDatabase.GenerateUniqueAssetPath("Assets/VerifiedMesh.asset"));
        AssetDatabase.SaveAssets();
        Check(persistent.GetBlendShapeIndex("P")==3,"Persistence failure");
        UnityEngine.Object.DestroyImmediate(source);
        File.WriteAllText(Path.Combine(Application.dataPath,"../verification.txt"),"PASS: X/Y/Z; multiframe; boundary conservation; vertex/normal/tangent deltas; mesh attributes; source preservation; invalid/duplicate/one-sided rejection; asset save. Unity " + Application.unityVersion);
    }
    static BoneWeight Weight() => new BoneWeight {boneIndex0=0,weight0=1};
    static void Reject(Func<Mesh> action) { try { var m=action(); UnityEngine.Object.DestroyImmediate(m); } catch(ArgumentException) {return;} throw new Exception("Invalid input accepted"); }
}
