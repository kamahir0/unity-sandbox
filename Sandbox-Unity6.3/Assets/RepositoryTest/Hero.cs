using Lilja.Repository;

namespace RepositoryTest
{
    /// <summary>
    /// プレイヤーEntity。
    /// Source Generatorによってリポジトリ・DTOが自動生成される。
    /// </summary>
    [Entity]
    public partial class Hero
    {
        /// <summary>
        /// プレイヤー名。
        /// </summary>
        [Persist(0)]
        private string _name;

        /// <summary>
        /// レベル。
        /// </summary>
        [Persist(1)]
        private int _level;

        /// <summary>
        /// 座標（ValueObject）。
        /// </summary>
        [Persist(2)]
        private Position _position;

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        public Hero(string name, int level, Position position)
        {
            _name = name;
            _level = level;
            _position = position;
        }

        /// <summary>
        /// プレイヤー名。
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
