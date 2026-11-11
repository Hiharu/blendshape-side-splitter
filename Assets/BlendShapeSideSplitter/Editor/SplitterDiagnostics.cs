using UnityEditor;
using UnityEngine;
namespace Hiharu.BlendShapeTools
{
    public static class SplitterDiagnostics
    {
        public static string Build(SkinnedMeshRenderer target, int shape, int axis, float plane, string negativeName, string positiveName, bool assign)
        {
            var b = SupportPanel.Header("BlendShape Side Splitter 1.1.0");
            b.AppendLine("[参考] Unity 2022.3.22f1で検証。SDK・MA不要。Humanoid/Generic共通。");
            var mesh = target ? target.sharedMesh : null;
            if (!mesh) { b.AppendLine("[要対応] Meshを持つSkinnedMeshRendererを指定してください。"); return SupportPanel.Finish(b); }
            b.AppendLine("頂点数: " + mesh.vertexCount + " / BlendShape数: " + mesh.blendShapeCount);
            b.AppendLine(mesh.isReadable ? "[OK] MeshのRead/Write有効。" : "[要対応] Import SettingsでRead/Writeを有効にしてください。");
            b.AppendLine("保存後に適用: " + assign);
            if (assign && (EditorUtility.IsPersistent(target) || !target.gameObject.scene.IsValid())) b.AppendLine("[要対応] 直接適用はシーン内のRendererを指定してください。");
            if (shape < 0 || shape >= mesh.blendShapeCount) b.AppendLine("[要対応] 有効な分割対象キーを指定してください。");
            else b.AppendLine("選択キーのフレーム数: " + mesh.GetBlendShapeFrameCount(shape));
            if (axis < 0 || axis > 2 || float.IsNaN(plane) || float.IsInfinity(plane)) b.AppendLine("[要対応] 分割軸と有限の位置を指定してください。");
            else
            {
                b.AppendLine("分割軸: " + "XYZ"[axis] + " / 位置: " + plane.ToString(System.Globalization.CultureInfo.InvariantCulture));
                if (mesh.isReadable)
                {
                    int negative = 0, positive = 0, center = 0;
                    foreach (var v in mesh.vertices) { if (v[axis] < plane) negative++; else if (v[axis] > plane) positive++; else center++; }
                    b.AppendLine("頂点の位置分布: 負=" + negative + " / 境界=" + center + " / 正=" + positive);
                    if (negative == 0 || positive == 0) b.AppendLine("[注意] 分割面の片側に頂点がありません。分割位置を確認してください。");
                }
            }
            bool validNames = !string.IsNullOrWhiteSpace(negativeName) && !string.IsNullOrWhiteSpace(positiveName) &&
                negativeName == negativeName.Trim() && positiveName == positiveName.Trim() && negativeName != positiveName &&
                mesh.GetBlendShapeIndex(negativeName) < 0 && mesh.GetBlendShapeIndex(positiveName) < 0;
            b.AppendLine(validNames ? "[OK] 追加名の空白・重複なし。" : "[要対応] 追加名の空白・前後の空白・既存名や相互の重複を解消してください。");
            b.AppendLine("[未確認] 選択キーの両側の変形量。頂点分布だけでは判断できません。保存時の分割処理が確認します。");
            b.AppendLine("次の操作: 警告解消 → 新規Mesh保存 → コピーを適用 → 追加キーを個別に動かして確認。元キーとの同時適用は二重変形に注意。");
            return SupportPanel.Finish(b);
        }
    }
}
