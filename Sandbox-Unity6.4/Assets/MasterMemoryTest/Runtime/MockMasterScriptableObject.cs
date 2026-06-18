using System.Collections.Generic;
using UnityEngine;

namespace MasterMemoryTest
{
    [CreateAssetMenu(fileName = "MockMasterData", menuName = "MasterMemoryTest/MockMasterData")]
    public class MockMasterScriptableObject : ScriptableObject
    {
        [SerializeField] private List<MockUserSOData> users = new List<MockUserSOData>();
        [SerializeField] private List<MockItemSOData> items = new List<MockItemSOData>();

        public List<MockUserSOData> Users => users;
        public List<MockItemSOData> Items => items;
    }

    [System.Serializable]
    public class MockUserSOData
    {
        public int id;
        public string name;
        public int level;
    }

    [System.Serializable]
    public class MockItemSOData
    {
        public int id;
        public string name;
        public int price;
    }
}
