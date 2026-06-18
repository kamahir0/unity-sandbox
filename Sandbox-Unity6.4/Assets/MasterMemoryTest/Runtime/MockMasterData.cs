using MasterMemory;
using MessagePack;
using MessagePack.Resolvers;
using UnityEngine;

namespace MasterMemoryTest
{
    [MemoryTable("mock_user"), MessagePackObject(true)]
    public class MockUser
    {
        [PrimaryKey]
        public int Id { get; set; }
        public string Name { get; set; }
        public int Level { get; set; }
    }

    [MemoryTable("mock_item"), MessagePackObject(true)]
    public class MockItem
    {
        [PrimaryKey]
        public int Id { get; set; }
        public string Name { get; set; }
        public int Price { get; set; }
    }

    public static class MasterMemoryInitializer
    {
        private static bool isInitialized = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            if (isInitialized) return;

            // Register generated resolvers
            var resolver = CompositeResolver.Create(
                MasterMemory.MasterMemoryResolver.Instance,
                MessagePack.GeneratedMessagePackResolver.Instance,
                StandardResolver.Instance
            );

            var options = MessagePackSerializerOptions.Standard.WithResolver(resolver);
            MessagePackSerializer.DefaultOptions = options;

            isInitialized = true;
            Debug.Log("MessagePack Resolver initialized for MasterMemoryTest.");
        }
    }
}
