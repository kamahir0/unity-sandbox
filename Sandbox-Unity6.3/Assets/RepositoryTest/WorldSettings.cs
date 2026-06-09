using Lilja.Repository;

namespace RepositoryTest
{
    /// <summary>
    /// ワールド全体の難易度。
    /// </summary>
    public enum WorldDifficulty
    {
        Relaxed,
        Normal,
        Hard,
        Nightmare,
    }

    /// <summary>
    /// シングルトンで扱うワールド設定 Entity。
    /// enum / bool / float / ValueObject を含む検証用モデル。
    /// </summary>
    [Entity]
    public partial class WorldSettings
    {
        [Persist(0)]
        private string _regionName;

        [Persist(1)]
        private WorldDifficulty _difficulty;

        [Persist(2)]
        private bool _nightMode;

        [Persist(3)]
        private float _spawnRate;

        [Persist(4)]
        private Position _startPosition;

        public WorldSettings(string regionName, WorldDifficulty difficulty, bool nightMode, float spawnRate, Position startPosition)
        {
            _regionName = regionName;
            _difficulty = difficulty;
            _nightMode = nightMode;
            _spawnRate = spawnRate;
            _startPosition = startPosition;
        }

        public string RegionName => _regionName;

        public WorldDifficulty Difficulty => _difficulty;

        public bool NightMode => _nightMode;

        public float SpawnRate => _spawnRate;

        public Position StartPosition => _startPosition;
    }
}
