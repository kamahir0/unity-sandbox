using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MasterMemory;
using MasterMemoryTest;

namespace MasterMemoryTest.Editor
{
    public static class MasterMemoryBuilder
    {
        [MenuItem("MasterMemoryTest/Create Mock Asset and Build")]
        public static void CreateAssetAndBuild()
        {
            string assetPath = "Assets/MasterMemoryTest/MockMasterData.asset";
            var so = ScriptableObject.CreateInstance<MockMasterScriptableObject>();

            so.Users.Add(new MockUserSOData { id = 1, name = "Alice", level = 10 });
            so.Users.Add(new MockUserSOData { id = 2, name = "Bob", level = 25 });
            so.Users.Add(new MockUserSOData { id = 3, name = "Charlie", level = 50 });

            so.Items.Add(new MockItemSOData { id = 101, name = "Bronze Sword", price = 100 });
            so.Items.Add(new MockItemSOData { id = 102, name = "Iron Shield", price = 250 });
            so.Items.Add(new MockItemSOData { id = 103, name = "Health Potion", price = 50 });

            string dir = Path.GetDirectoryName(assetPath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            AssetDatabase.CreateAsset(so, assetPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"MockMasterData asset created at {assetPath}");

            Build();
        }

        [MenuItem("MasterMemoryTest/Build Master Data")]
        public static void Build()
        {
            // Find the ScriptableObject asset
            string[] guids = AssetDatabase.FindAssets("t:MockMasterScriptableObject");
            if (guids.Length == 0)
            {
                Debug.LogError("No MockMasterScriptableObject asset found in the project. Please create one first.");
                return;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var so = AssetDatabase.LoadAssetAtPath<MockMasterScriptableObject>(path);
            if (so == null)
            {
                Debug.LogError($"Failed to load MockMasterScriptableObject at path: {path}");
                return;
            }

            // Create MasterMemory database builder
            var builder = new DatabaseBuilder();

            // 1. Populate Users
            var users = new List<MockUser>();
            foreach (var u in so.Users)
            {
                users.Add(new MockUser
                {
                    Id = u.id,
                    Name = u.name,
                    Level = u.level
                });
            }
            builder.Append(users);

            // 2. Populate Items
            var items = new List<MockItem>();
            foreach (var item in so.Items)
            {
                items.Add(new MockItem
                {
                    Id = item.id,
                    Name = item.name,
                    Price = item.price
                });
            }
            builder.Append(items);

            // 3. Serialize to bytes
            byte[] binary = builder.Build();

            // 4. Save to Resources folder
            string resourcesPath = "Assets/MasterMemoryTest/Resources";
            if (!Directory.Exists(resourcesPath))
            {
                Directory.CreateDirectory(resourcesPath);
                AssetDatabase.ImportAsset(resourcesPath);
            }

            string filePath = Path.Combine(resourcesPath, "mock_master_data.bytes");
            File.WriteAllBytes(filePath, binary);

            AssetDatabase.ImportAsset(filePath);
            Debug.Log($"MasterMemory master data built successfully and saved to: {filePath}");
        }

        [MenuItem("MasterMemoryTest/Create Test Scene")]
        public static void CreateTestScene()
        {
            // Create scene
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects, 
                UnityEditor.SceneManagement.NewSceneMode.Single
            );

            // Create Controller
            var controllerGo = new GameObject("MasterMemoryTestController");
            var controller = controllerGo.AddComponent<MasterMemoryTestSceneController>();

            // Create Canvas
            var canvasGo = new GameObject("Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();

            // Create TextMeshPro
            var textGo = new GameObject("DisplayText");
            textGo.transform.SetParent(canvasGo.transform, false);
            var textComponent = textGo.AddComponent<TextMeshProUGUI>();

            var rectTransform = textComponent.rectTransform;
            rectTransform.anchorMin = new Vector2(0.1f, 0.1f);
            rectTransform.anchorMax = new Vector2(0.9f, 0.9f);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            textComponent.fontSize = 24;
            textComponent.color = Color.white;
            textComponent.alignment = TextAlignmentOptions.TopLeft;
            textComponent.text = "Loading MasterMemory data...";

            // Link TextMeshPro to Controller via SerializedObject
            var so = new SerializedObject(controller);
            so.FindProperty("displayText").objectReferenceValue = textComponent;
            so.ApplyModifiedProperties();

            // Save scene
            string scenePath = "Assets/MasterMemoryTest/MasterMemoryTestScene.unity";
            string dir = Path.GetDirectoryName(scenePath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.ImportAsset(scenePath);

            Debug.Log($"Test scene created successfully at: {scenePath}");
        }
    }
}
