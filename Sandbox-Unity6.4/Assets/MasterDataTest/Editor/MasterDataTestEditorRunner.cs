using System;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MasterDataTest.Editor
{
    public static class MasterDataTestEditorRunner
    {
        private const string PlaybackPendingKey = "MasterDataTest.PlaybackPending";
        private const string PlaybackStartedAtKey = "MasterDataTest.PlaybackStartedAt";

        [InitializeOnLoadMethod]
        private static void ResumePendingScenePlayback()
        {
            if (!EditorPrefs.GetBool(PlaybackPendingKey, false))
            {
                return;
            }

            EditorApplication.update -= WatchScenePlayback;
            EditorApplication.update += WatchScenePlayback;
        }

        public static void ImportNuGetPackages()
        {
            AssetDatabase.ImportAsset("Assets/Packages", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            Label("Assets/Packages/MasterMemory.3.0.4/lib/netstandard2.0/MasterMemory.dll", "NuGetForUnity");
            Label("Assets/Packages/MasterMemory.Annotations.3.0.4/lib/netstandard2.0/MasterMemory.Annotations.dll", "NuGetForUnity");
            Label("Assets/Packages/System.Memory.4.5.5/lib/netstandard2.0/System.Memory.dll", "NuGetForUnity");
            Label(
                "Assets/Packages/MasterMemory.3.0.4/analyzers/dotnet/cs/MasterMemory.SourceGenerator.dll",
                "NuGetForUnity",
                "RoslynAnalyzer");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        }

        public static void CreateScene()
        {
            AssetDatabase.ImportAsset("Assets/MasterDataTest", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            Directory.CreateDirectory("Assets/MasterDataTest");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var go = new GameObject("MasterDataTest");
            go.AddComponent<MasterDataTestBootstrap>();

            EditorSceneManager.SaveScene(scene, "Assets/MasterDataTest/MasterDataTest.unity");
            AssetDatabase.ImportAsset("Assets/MasterDataTest/MasterDataTest.unity", ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
        }

        public static void VerifyRuntime()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/MasterDataTest/Resources/master-data.bytes");
            if (asset == null)
            {
                throw new InvalidOperationException("master-data.bytes was not imported.");
            }

            var databaseType = Type.GetType("MasterDataTest.Data.MemoryDatabase, Assembly-CSharp", throwOnError: true);
            var database = Activator.CreateInstance(databaseType, asset.bytes, true, null, 1);
            var itemTable = databaseType.GetProperty("ItemMasterTable")!.GetValue(database);
            var questTable = databaseType.GetProperty("QuestMasterTable")!.GetValue(database);

            var item = itemTable!.GetType().GetMethod("FindById")!.Invoke(itemTable, new object[] { 1001 });
            var quest = questTable!.GetType().GetMethod("FindByChapterAndNumber")!.Invoke(questTable, new object[] { ValueTuple.Create(1, 1) });
            var code = item!.GetType().GetProperty("Code")!.GetValue(item);
            var title = quest!.GetType().GetProperty("Title")!.GetValue(quest);

            if (!Equals("potion", code) || !Equals("First Delivery", title))
            {
                throw new InvalidOperationException($"Unexpected master data. item={code}, quest={title}");
            }

            Debug.Log($"MasterDataTest editor verification succeeded. item={code}, quest={title}");
        }

        public static void RunScenePlayback()
        {
            try
            {
                EditorSceneManager.OpenScene("Assets/MasterDataTest/MasterDataTest.unity");
                EditorPrefs.SetBool(PlaybackPendingKey, true);
                EditorPrefs.SetString(
                    PlaybackStartedAtKey,
                    EditorApplication.timeSinceStartup.ToString("R", CultureInfo.InvariantCulture));
                EditorApplication.update += WatchScenePlayback;
                EditorApplication.EnterPlaymode();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorApplication.Exit(1);
            }
        }

        private static void WatchScenePlayback()
        {
            if (MasterDataTestBootstrap.Succeeded)
            {
                Debug.Log($"MasterDataTest scene playback succeeded. {MasterDataTestBootstrap.LastResult}");
                FinishScenePlayback(0);
                return;
            }

            var elapsed = EditorApplication.timeSinceStartup - GetPlaybackStartedAt();
            if (EditorApplication.isPlaying &&
                elapsed > 1.0 &&
                !string.IsNullOrEmpty(MasterDataTestBootstrap.LastResult))
            {
                Debug.LogError($"MasterDataTest scene playback failed. {MasterDataTestBootstrap.LastResult}");
                FinishScenePlayback(1);
                return;
            }

            if (elapsed < 20.0)
            {
                return;
            }

            Debug.LogError($"MasterDataTest scene playback timed out. {MasterDataTestBootstrap.LastResult}");
            FinishScenePlayback(1);
        }

        private static void FinishScenePlayback(int exitCode)
        {
            EditorPrefs.DeleteKey(PlaybackPendingKey);
            EditorPrefs.DeleteKey(PlaybackStartedAtKey);
            EditorApplication.update -= WatchScenePlayback;
            if (EditorApplication.isPlaying)
            {
                EditorApplication.ExitPlaymode();
            }

            EditorApplication.Exit(exitCode);
        }

        private static double GetPlaybackStartedAt()
        {
            var raw = EditorPrefs.GetString(PlaybackStartedAtKey, "0");
            return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? value
                : 0.0;
        }

        private static void Label(string path, params string[] labels)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (asset == null)
            {
                throw new FileNotFoundException(path);
            }

            AssetDatabase.SetLabels(asset, labels);
        }
    }
}
