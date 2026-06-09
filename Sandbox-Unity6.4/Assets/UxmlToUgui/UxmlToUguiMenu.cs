// UxmlToUguiMenu.cs
// Editorフォルダ配下に配置すること
// UxmlToUguiConverter.cs と同じフォルダに置く

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Unity Editor メニューから UXML → UGUI Prefab 変換を起動するエントリポイント。
/// Tools > UXML to UGUI > Convert...
/// </summary>
public static class UxmlToUguiMenu
{
    [MenuItem("Tools/UXML to UGUI/Convert...")]
    private static void ConvertFromMenu()
    {
        // ── 1. OS ファイル選択ダイアログ ────────────────────────
        string absPath = EditorUtility.OpenFilePanel(
            title: "変換する UXML ファイルを選択",
            directory: Application.dataPath,
            extension: "uxml");

        if (string.IsNullOrEmpty(absPath)) return; // キャンセル

        // ── 2. 絶対パス → プロジェクト相対パスへ変換 ────────────
        if (!absPath.StartsWith(Application.dataPath))
        {
            Debug.LogError(
                $"[UxmlToUguiMenu] 選択されたファイルがプロジェクト外にあります:\n{absPath}");
            return;
        }

        string uxmlPath = "Assets" + absPath.Substring(Application.dataPath.Length);

        // ── 3. 出力パスの決定（同ディレクトリ、同名、.prefab）────
        //   例: Assets/UI/LoginScreen.uxml → Assets/UI/LoginScreen.prefab
        string outputPath = Path.ChangeExtension(uxmlPath, ".prefab");

        // 既存 Prefab がある場合は上書き確認
        if (File.Exists(Path.GetFullPath(outputPath)))
        {
            bool overwrite = EditorUtility.DisplayDialog(
                title: "上書き確認",
                message: $"以下のファイルが既に存在します。上書きしますか？\n\n{outputPath}",
                ok: "上書き",
                cancel: "キャンセル");

            if (!overwrite) return;
        }

        // ── 4. 変換実行 ──────────────────────────────────────────
        UxmlToUguiConverter.Convert(uxmlPath, outputPath);
    }
}
#endif