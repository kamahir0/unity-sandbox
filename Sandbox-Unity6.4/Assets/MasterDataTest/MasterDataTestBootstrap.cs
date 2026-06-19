using System;
using UnityEngine;

namespace MasterDataTest
{
    public sealed class MasterDataTestBootstrap : MonoBehaviour
    {
        public static bool Succeeded { get; private set; }
        public static string LastResult { get; private set; } = string.Empty;

        private void Start()
        {
            Succeeded = false;
            LastResult = string.Empty;

            try
            {
                var asset = Resources.Load<TextAsset>("master-data");
                if (asset == null)
                {
                    throw new InvalidOperationException("Resources/master-data.bytes was not found.");
                }

                var databaseType = Type.GetType("MasterDataTest.Data.MemoryDatabase, Assembly-CSharp", throwOnError: true);
                var itemType = Type.GetType("MasterDataTest.Data.ItemType, Assembly-CSharp", throwOnError: true);
                var database = Activator.CreateInstance(databaseType, asset.bytes, true, null, 1);
                var itemTable = databaseType.GetProperty("ItemMasterTable")!.GetValue(database);
                var questTable = databaseType.GetProperty("QuestMasterTable")!.GetValue(database);
                var rewardTable = databaseType.GetProperty("RewardMasterTable")!.GetValue(database);

                var item = itemTable!.GetType().GetMethod("FindById")!.Invoke(itemTable, new object[] { 1001 });
                var quest = questTable!.GetType().GetMethod("FindByChapterAndNumber")!.Invoke(questTable, new object[] { ValueTuple.Create(1, 1) });
                var requiredItem = quest!.GetType().GetMethod("GetRequiredItem")!.Invoke(quest, new[] { itemTable });
                var mainReward = quest.GetType().GetMethod("GetMainReward")!.Invoke(quest, new[] { rewardTable });
                var rewards = quest.GetType().GetMethod("GetRewards")!.Invoke(quest, new[] { rewardTable });
                var consumable = Enum.Parse(itemType, "Consumable");
                var consumables = itemTable.GetType().GetMethod("FindByType")!.Invoke(itemTable, new[] { consumable });

                AssertEqual("potion", Get<string>(item!, "Code"), "item.Code");
                AssertEqual("First Delivery", Get<string>(quest, "Title"), "quest.Title");
                AssertEqual(Get<int>(item!, "Id"), Get<int>(requiredItem!, "Id"), nameof(requiredItem));
                AssertEqual(1, Get<int>(mainReward!, "Id"), nameof(mainReward));
                AssertEqual(2, Get<int>(rewards!, "Length"), nameof(rewards));
                AssertEqual(1, Count(consumables!), nameof(consumables));

                Succeeded = true;
                LastResult = $"Loaded quest {Get<int>(quest, "Id")}: {Get<string>(quest, "Title")}, rewards={Get<int>(rewards!, "Length")}";
                Debug.Log($"MasterDataTest succeeded. {LastResult}");
            }
            catch (Exception ex)
            {
                LastResult = ex.ToString();
                Debug.LogException(ex);
            }
        }

        private static int Count(object view)
        {
            var count = 0;
            foreach (var _ in (System.Collections.IEnumerable)view)
            {
                count++;
            }

            return count;
        }

        private static T Get<T>(object instance, string propertyName)
        {
            return (T)instance.GetType().GetProperty(propertyName)!.GetValue(instance);
        }

        private static void AssertEqual<T>(T expected, T actual, string label)
        {
            if (!Equals(expected, actual))
            {
                throw new InvalidOperationException($"{label}: expected {expected}, actual {actual}");
            }
        }
    }
}
