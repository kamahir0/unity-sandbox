using Lilja.Repository;

namespace RepositoryTest
{
    /// <summary>
    /// モンスターEntity。
    /// Source Generatorによってリポジトリ・DTOが自動生成される。
    /// </summary>
    [Entity]
    public partial class Monster
    {
        /// <summary>
        /// モンスターID（主キー）。
        /// </summary>
        [Key]
        [Persist(0)]
        private int _id;

        /// <summary>
        /// モンスター名。
        /// </summary>
        [Persist(1)]
        private string _name;

        /// <summary>
        /// レベル。
        /// </summary>
        [Persist(2)]
        private int _level;

        /// <summary>
        /// 座標（ValueObject）。
        /// </summary>
        [Persist(3)]
        private Position _position;

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        public Monster(int id, string name, int level, Position position)
        {
            _id = id;
            _name = name;
            _level = level;
            _position = position;
        }

        /// <summary>
        /// モンスターID。
        /// </summary>
        public int Id => _id;

        /// <summary>
        /// モンスター名。
        /// </summary>
        public string Name => _name;

        /// <summary>
        /// レベル。
        /// </summary>
        public int Level => _level;

        /// <summary>
        /// 座標。
        /// </summary>
        public Position Position => _position;
    }
}
