using Lilja.Repository;

namespace RepositoryTest
{
    /// <summary>
    /// レリックのレアリティ。
    /// </summary>
    public enum RelicRarity
    {
        Common,
        Rare,
        Epic,
        Legendary,
    }

    /// <summary>
    /// レリックの戦闘補正値を表す ValueObject。
    /// </summary>
    public struct RelicStats
    {
        public int Attack { get; }

        public int Defense { get; }

        public float CriticalRate { get; }

        [FromPrimitive]
        public RelicStats(int attack, int defense, float criticalRate)
        {
            Attack = attack;
            Defense = defense;
            CriticalRate = criticalRate;
        }

        [ToPrimitive]
        public (int attack, int defense, float criticalRate) ToPrimitive()
        {
            return (Attack, Defense, CriticalRate);
        }

        public override string ToString()
        {
            return $"(Atk:{Attack}, Def:{Defense}, Crit:{CriticalRate:0.00})";
        }
    }

    /// <summary>
    /// 装備アイテム Entity。
    /// enum / bool / long / ValueObject を含む検証用モデル。
    /// </summary>
    [Entity]
    public partial class Relic
    {
        [Key]
        [Persist(0)]
        private int _id;

        [Persist(1)]
        private string _name;

        [Persist(2)]
        private RelicRarity _rarity;

        [Persist(3)]
        private bool _isEquipped;

        [Persist(4)]
        private long _price;

        [Persist(5)]
        private RelicStats _stats;

        public Relic(int id, string name, RelicRarity rarity, bool isEquipped, long price, RelicStats stats)
        {
            _id = id;
            _name = name;
            _rarity = rarity;
            _isEquipped = isEquipped;
            _price = price;
            _stats = stats;
        }

        public int Id => _id;

        public string Name => _name;

        public RelicRarity Rarity => _rarity;

        public bool IsEquipped => _isEquipped;

        public long Price => _price;

        public RelicStats Stats => _stats;
    }
}
