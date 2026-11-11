using System;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
namespace Hiharu.BlendShapeTools
{
    internal static class SupportPanel
    {
        internal static StringBuilder Header(string product, params string[] packages)
        {
            var b = new StringBuilder();
            b.AppendLine(product + " 診断 / UTC " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
            b.AppendLine("Unity: " + Application.unityVersion);
            b.AppendLine("Build Target: " + EditorUserBuildSettings.activeBuildTarget);
            b.AppendLine("Play Mode: " + EditorApplication.isPlayingOrWillChangePlaymode);
            if (EditorApplication.isPlayingOrWillChangePlaymode) b.AppendLine("[要対応] 作成・適用の前にPlay Modeを終了してください。");
            var list = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
            foreach (var id in packages)
            {
                var p = list.FirstOrDefault(x => x.name == id);
                b.AppendLine(id + ": " + (p == null ? "登録なし / Assets形式の導入は判別できません" : p.version));
            }
            return b;
        }
        internal static string Finish(StringBuilder b)
        {
            b.AppendLine("[未確認] Consoleログ本文・実モデルの見た目・他ギミック競合・VRChat実機。");
            b.AppendLine("名前・個人のパス・Blueprint ID・ログ本文を含めず、自動送信しません。");
            b.AppendLine("相談時に追記: 操作した段階 / 期待した動作 / 実際の動作。");
            return b.ToString();
        }
        internal static void Draw(Func<string> build, string guide, ref string report, ref Vector2 scroll)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("環境チェック")) report = build();
                if (GUILayout.Button("診断結果をコピー")) { report = build(); EditorGUIUtility.systemCopyBuffer = report; }
                if (GUILayout.Button("導入ガイド")) Application.OpenURL(guide);
            }
            EditorGUILayout.LabelField("名前・パス・ログ本文は含めません。コピー時に再チェックします。", EditorStyles.wordWrappedMiniLabel);
            if (!string.IsNullOrEmpty(report))
            {
                scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(145));
                EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
                EditorGUILayout.LabelField("チェック時点の結果です。変更後は再チェックしてください。", EditorStyles.miniLabel);
            }
        }
    }
}