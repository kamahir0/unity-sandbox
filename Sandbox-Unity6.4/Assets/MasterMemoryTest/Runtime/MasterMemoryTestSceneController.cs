using UnityEngine;
using TMPro;
using System.Text;

namespace MasterMemoryTest
{
    public class MasterMemoryTestSceneController : MonoBehaviour
    {
        [SerializeField] private TMP_Text displayText;

        private void Start()
        {
            LoadAndDisplayMasterData();
        }

        private void LoadAndDisplayMasterData()
        {
            // 1. Load the binary file from Resources
            var textAsset = Resources.Load<TextAsset>("mock_master_data");
            if (textAsset == null)
            {
                string errMsg = "Failed to load 'mock_master_data' from Resources.\nPlease build master data via the Editor menu: 'MasterMemoryTest -> Build Master Data' first.";
                Debug.LogError(errMsg);
                if (displayText != null) displayText.text = errMsg;
                return;
            }

            // 2. Initialize MemoryDatabase
            MasterMemory.MemoryDatabase db;
            try
            {
                db = new MasterMemory.MemoryDatabase(textAsset.bytes);
            }
            catch (System.Exception ex)
            {
                string errMsg = $"Failed to initialize MemoryDatabase: {ex.Message}";
                Debug.LogError(errMsg);
                if (displayText != null) displayText.text = errMsg;
                return;
            }

            // 3. Fetch and display all data
            var sb = new StringBuilder();
            sb.AppendLine("=== MasterMemory Loaded Master Data ===");
            sb.AppendLine();

            sb.AppendLine("[MockUser Table]");
            var users = db.MockUserTable.All;
            foreach (var u in users)
            {
                sb.AppendLine($"- ID: {u.Id}, Name: {u.Name}, Level: {u.Level}");
            }
            sb.AppendLine();

            sb.AppendLine("[MockItem Table]");
            var items = db.MockItemTable.All;
            foreach (var item in items)
            {
                sb.AppendLine($"- ID: {item.Id}, Name: {item.Name}, Price: {item.Price} G");
            }

            string resultText = sb.ToString();
            Debug.Log(resultText);

            if (displayText != null)
            {
                displayText.text = resultText;
            }
        }
    }
}
