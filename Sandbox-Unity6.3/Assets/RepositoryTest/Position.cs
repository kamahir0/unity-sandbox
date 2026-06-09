using Lilja.Repository;

namespace RepositoryTest
{
    /// <summary>
    /// 座標を表すValueObject。
    /// </summary>
    public struct Position
    {
        /// <summary>
        /// X座標。
        /// </summary>
        public int X { get; }

        /// <summary>
        /// Y座標。
        /// </summary>
        public int Y { get; }

        /// <summary>
        /// プリミティブからPositionを復元する。
        /// </summary>
        [FromPrimitive]
        public Position(int x, int y)
        {
            X = x;
            Y = y;
        }

        /// <summary>
        /// Positionをプリミティブに変換する。
        /// </summary>
        [ToPrimitive]
        public (int x, int y) ToPrimitive() => (X, Y);

        /// <inheritdoc />
        public override string ToString() => $"({X}, {Y})";
    }
}
