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
        public Position(int x, int y)
        {
            X = x;
            Y = y;
        }

        [FromPrimitive]
        public Position(string value)
        {
            var parts = (value ?? string.Empty).Split(',');
            X = parts.Length > 0 && int.TryParse(parts[0], out var x) ? x : 0;
            Y = parts.Length > 1 && int.TryParse(parts[1], out var y) ? y : 0;
        }

        /// <summary>
        /// Positionをプリミティブに変換する。
        /// </summary>
        [ToPrimitive]
        public string ToPrimitive() => $"{X},{Y}";

        /// <inheritdoc />
        public override string ToString() => $"({X}, {Y})";
    }
}
