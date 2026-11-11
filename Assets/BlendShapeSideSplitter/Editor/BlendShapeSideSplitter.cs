using System;
using UnityEditor;
using UnityEngine;

namespace Hiharu.BlendShapeTools
{
    /// <summary>Copies a mesh and splits one blend shape using its undeformed local coordinates.</summary>
    public static class SideSplitter
    {
        public static Mesh Split(Mesh source, int shape, int axis, float plane,
            string negativeName, string positiveName)
        {
            if (!source || !source.isReadable) throw new ArgumentException("読み取り可能なメッシュが必要です。");
            if (shape < 0 || shape >= source.blendShapeCount) throw new ArgumentOutOfRangeException(nameof(shape));
            if (axis < 0 || axis > 2 || float.IsNaN(plane) || float.IsInfinity(plane))
                throw new ArgumentException("分割軸・位置が不正です。");
            ValidateName(source, negativeName);
            ValidateName(source, positiveName);
            if (negativeName == positiveName) throw new ArgumentException("追加する名前は別々にしてください。");
            int frames = source.GetBlendShapeFrameCount(shape);
            if (frames == 0) throw new ArgumentException("分割対象にフレームがありません。");
            var vertices = source.vertices;
            int count = vertices.Length;
            var dv = new Vector3[count];
            var dn = new Vector3[count];
            var dt = new Vector3[count];
            var outputV = new Vector3[count];
            var outputN = new Vector3[count];
            var outputT = new Vector3[count];
            bool negativeAffected = false, positiveAffected = false;
            // Derived from the independent-pupil split used in ShinanoLocalExtras.EyeHighlight.
            // No avatar assets or fixed bone/shape names are included.
            Mesh result = UnityEngine.Object.Instantiate(source);
            result.name = source.name + "_SideSplit";
            try
            {
                for (int side = 0; side < 2; side++)
                    for (int frame = 0; frame < frames; frame++)
                    {
                        source.GetBlendShapeFrameVertices(shape, frame, dv, dn, dt);
                        for (int i = 0; i < count; i++)
                        {
                            float coordinate = vertices[i][axis];
                            // Divide vertices exactly on the plane evenly so both outputs sum to the original.
                            float factor = coordinate == plane ? .5f : ((coordinate < plane) == (side == 0) ? 1f : 0f);
                            outputV[i] = dv[i] * factor;
                            outputN[i] = dn[i] * factor;
                            outputT[i] = dt[i] * factor;
                            if (factor > 0 && (dv[i] != Vector3.zero || dn[i] != Vector3.zero || dt[i] != Vector3.zero))
                            {
                                if (side == 0) negativeAffected = true; else positiveAffected = true;
                            }
                        }
                        result.AddBlendShapeFrame(side == 0 ? negativeName : positiveName,
                            source.GetBlendShapeFrameWeight(shape, frame), outputV, outputN, outputT);
                    }
                if (!negativeAffected || !positiveAffected)
                    throw new ArgumentException("変形が片側にしかありません。分割位置と対象を確認してください。");
                return result;
            }
            catch { UnityEngine.Object.DestroyImmediate(result); throw; }
        }

        private static void ValidateName(Mesh source, string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name != name.Trim() || source.GetBlendShapeIndex(name) >= 0)
                throw new ArgumentException("追加名は空白・前後の空白・既存名との重複を避けてください。");
        }
    }

    public sealed class SideSplitterWindow : EditorWindow
    {
        private SkinnedMeshRenderer target;
        private Mesh observedMesh;
        private int shape, axis;
        private float plane;
        private string negativeName = "", positiveName = "";
        private bool assign;
        private Vector2 scroll;
        private string diagnosticReport;
        private Vector2 diagnosticScroll;

        [MenuItem("Tools/Hiharu/BlendShape Side Splitter")]
        public static void Open() => GetWindow<SideSplitterWindow>("Shape Side Splitter");

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.HelpBox("シェイプキーを2方向に分け、新しいメッシュを保存します。元メッシュと既存のキーは保持します。", MessageType.Info);
            target = (SkinnedMeshRenderer)EditorGUILayout.ObjectField("対象 Renderer", target, typeof(SkinnedMeshRenderer), true);
            Mesh mesh = target ? target.sharedMesh : null;
            if (mesh != observedMesh) { observedMesh = mesh; shape = 0; ResetNames(); }
            if (mesh && mesh.blendShapeCount > 0)
            {
                var names = new string[mesh.blendShapeCount];
                for (int i = 0; i < names.Length; i++) names[i] = mesh.GetBlendShapeName(i);
                int next = EditorGUILayout.Popup("分割するキー", shape, names);
                if (next != shape) { shape = next; ResetNames(); }
                axis = EditorGUILayout.Popup("メッシュのローカル軸", axis, new[] { "X", "Y", "Z" });
                plane = EditorGUILayout.FloatField("分割位置（ローカル座標）", plane);
                negativeName = EditorGUILayout.TextField("負方向の追加名", negativeName);
                positiveName = EditorGUILayout.TextField("正方向の追加名", positiveName);
                assign = EditorGUILayout.Toggle("保存後に対象へ適用", assign);
                EditorGUILayout.HelpBox("正負は画面の左右ではなくメッシュの座標です。境界上の変形は半分ずつ配分します。元キーと追加キーを同時に動かすと変形が重なります。", MessageType.Warning);
                if (!mesh.isReadable) EditorGUILayout.HelpBox("Read/Writeが必要です。Import Settingsを確認してください（自動変更しません）。", MessageType.Error);
                if (assign && (EditorUtility.IsPersistent(target) || !target.gameObject.scene.IsValid()))
                    EditorGUILayout.HelpBox("直接適用はシーン内のRendererで行ってください。", MessageType.Warning);
                using (new EditorGUI.DisabledScope(!mesh.isReadable || EditorApplication.isPlayingOrWillChangePlaymode ||
                    (assign && (EditorUtility.IsPersistent(target) || !target.gameObject.scene.IsValid()))))
                    if (GUILayout.Button("分割メッシュを保存…")) Save(mesh);
            }
            else EditorGUILayout.HelpBox("BlendShapeを持つSkinnedMeshRendererを指定してください。", MessageType.Info);
            SupportPanel.Draw(() => SplitterDiagnostics.Build(target, shape, axis, plane, negativeName, positiveName, assign), "https://github.com/Hiharu/blendshape-side-splitter#readme", ref diagnosticReport, ref diagnosticScroll);
            EditorGUILayout.EndScrollView();
        }

        private void ResetNames()
        {
            string name = observedMesh && observedMesh.blendShapeCount > shape ? observedMesh.GetBlendShapeName(shape) : "Shape";
            negativeName = name + "_Negative"; positiveName = name + "_Positive";
        }

        private void Save(Mesh source)
        {
            Mesh copy = null;
            try
            {
                copy = SideSplitter.Split(source, shape, axis, plane, negativeName, positiveName);
                string path = EditorUtility.SaveFilePanelInProject("新しいメッシュの保存先", "SideSplitMesh", "asset", "Assets内を選択してください。");
                if (string.IsNullOrEmpty(path)) return;
                // Do not replace an existing asset, even when the file picker offered overwrite.
                path = AssetDatabase.GenerateUniqueAssetPath(path);
                AssetDatabase.CreateAsset(copy, path);
                AssetDatabase.SaveAssets();
                if (assign)
                {
                    var weights = new float[source.blendShapeCount];
                    for (int i = 0; i < weights.Length; i++) weights[i] = target.GetBlendShapeWeight(i);
                    Undo.RecordObject(target, "Apply split blend shape mesh");
                    target.sharedMesh = copy;
                    for (int i = 0; i < weights.Length; i++) target.SetBlendShapeWeight(i, weights[i]);
                    target.SetBlendShapeWeight(source.blendShapeCount, 0);
                    target.SetBlendShapeWeight(source.blendShapeCount + 1, 0);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                    EditorUtility.SetDirty(target);
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
                }
                Selection.activeObject = copy;
                EditorGUIUtility.PingObject(copy);
                ShowNotification(new GUIContent("分割メッシュを保存しました"));
            }
            catch (Exception ex) { Debug.LogException(ex); EditorUtility.DisplayDialog("分割できませんでした", ex.Message, "OK"); }
            finally { if (copy && !AssetDatabase.Contains(copy)) DestroyImmediate(copy); }
        }
    }
}
