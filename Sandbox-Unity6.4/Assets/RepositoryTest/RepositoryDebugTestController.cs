using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using RepositoryTest.Repositories;
using UnityEngine;

namespace RepositoryTest
{
    /// <summary>
    /// 新生 Lilja.Repository の Load/Save 境界を確認する最小テストコントローラ。
    /// </summary>
    public sealed class RepositoryDebugTestController : MonoBehaviour
    {
        [SerializeField]
        private bool _runOnStart = true;

        private async void Start()
        {
            if (!_runOnStart)
            {
                return;
            }

            await RunAllAsync(destroyCancellationToken);
        }

        [ContextMenu("Run Repository Tests")]
        private void RunFromContextMenu()
        {
            RunAllAsync(destroyCancellationToken).Forget();
        }

        private async UniTask RunAllAsync(CancellationToken ct)
        {
            Debug.Log("[RepositoryTest] start");

            await RunMonsterJsonAsync(ct);
            await RunHeroJsonAsync(ct);
            await RunRelicInMemorySnapshotAsync(ct);
            await RunWorldSettingsJsonAsync(ct);

            Debug.Log("[RepositoryTest] complete");
        }

        private static async UniTask RunMonsterJsonAsync(CancellationToken ct)
        {
            IMonsterRepository repository = MonsterRepository.Json.Create();
            var monster = new Monster(101, "Slime", 3, new Position(4, 5));

            await repository.LoadAsync(ct);
            repository.Delete(monster.Id);
            await repository.SaveAsync(ct);
            Debug.Assert(!repository.Exists(monster.Id), "Monster should not exist after delete.");

            Debug.Assert(!repository.TryGet(monster.Id, out _), "Missing keyed try-get should return false.");

            repository.Update(monster);
            await repository.SaveAsync(ct);
            Debug.Assert(repository.Exists(monster.Id), "Monster should exist after save.");

            await repository.LoadAsync(ct);
            var loaded = repository.Get(monster.Id);
            Debug.Assert(loaded.Id == monster.Id && loaded.Name == monster.Name && loaded.Position.X == 4, "Monster roundtrip failed.");

            var all = repository.All();
            Debug.Assert(all.Any(item => item.Id == monster.Id), "All should include saved monster.");
        }

        private static async UniTask RunHeroJsonAsync(CancellationToken ct)
        {
            IHeroRepository repository = HeroRepository.Json.Create();
            var hero = new Hero("Lilja", 12, new Position(10, 20));

            await repository.LoadAsync(ct);
            repository.Delete();
            await repository.SaveAsync(ct);
            Debug.Assert(!repository.Exists(), "Hero should not exist after delete.");

            repository.Update(hero);
            await repository.SaveAsync(ct);
            Debug.Assert(repository.Exists(), "Hero should exist after save.");

            await repository.LoadAsync(ct);
            var loaded = repository.Get();
            Debug.Assert(loaded.Name == hero.Name && loaded.Level == hero.Level && loaded.Position.Y == 20, "Hero roundtrip failed.");
        }

        private static async UniTask RunRelicInMemorySnapshotAsync(CancellationToken ct)
        {
            var original = new Relic(1, "Old Crown", RelicRarity.Rare, false, 500, new RelicStats(3, 4, 0.15f));
            IRelicRepository repository = RelicRepository.InMemory.Create(new List<Relic> { original });

            await repository.LoadAsync(ct);
            var loaded = repository.Get(original.Id);
            var mutatedOutsideRepository = new Relic(original.Id, "Mutated", RelicRarity.Legendary, true, 9999, new RelicStats(99, 99, 1f));

            Debug.Assert(loaded.Name == original.Name, "InMemory should load from DTO snapshot.");
            Debug.Assert(mutatedOutsideRepository.Name != loaded.Name, "Sanity check for snapshot test.");

            repository.Update(mutatedOutsideRepository);
            await repository.SaveAsync(ct);
            var saved = repository.Get(original.Id);
            Debug.Assert(saved.Name == mutatedOutsideRepository.Name && saved.Stats.Attack == 99, "InMemory save/load failed.");
        }

        private static async UniTask RunWorldSettingsJsonAsync(CancellationToken ct)
        {
            IWorldSettingsRepository repository = WorldSettingsRepository.Json.Create();
            var settings = new WorldSettings("North", WorldDifficulty.Hard, true, 1.25f, new Position(7, 8));

            await repository.LoadAsync(ct);
            repository.Update(settings);
            await repository.SaveAsync(ct);
            await repository.LoadAsync(ct);
            var loaded = repository.Get();

            Debug.Assert(loaded.RegionName == settings.RegionName, "WorldSettings region roundtrip failed.");
            Debug.Assert(loaded.Difficulty == settings.Difficulty, "WorldSettings enum roundtrip failed.");
            Debug.Assert(loaded.StartPosition.X == settings.StartPosition.X, "WorldSettings value object roundtrip failed.");
        }
    }
}
