namespace ClassicGamePlugin.Features.Tetris.Domain;

/// <summary>
/// 俄罗斯方块领域唯一需要替换的随机边界。游戏只要求按次取得方块，不知道来源使用随机袋还是测试序列。
/// </summary>
internal interface ITetrominoSource
{
    TetrominoType Next();
}

/// <summary>
/// 按现代 7-bag 规则生成方块：每一袋恰好包含七种方块各一次，袋内使用 Fisher-Yates 洗牌。
/// 跨袋不附加人为限制，避免悄悄改变标准随机分布。
/// </summary>
internal sealed class SevenBagTetrominoSource : ITetrominoSource
{
    private readonly Random _random;
    private readonly Queue<TetrominoType> _bag = [];

    internal SevenBagTetrominoSource()
        : this(Random.Shared)
    {
    }

    internal SevenBagTetrominoSource(Random random) =>
        _random = random ?? throw new ArgumentNullException(nameof(random));

    public TetrominoType Next()
    {
        if (_bag.Count == 0)
        {
            Refill();
        }

        return _bag.Dequeue();
    }

    private void Refill()
    {
        var values = Enum.GetValues<TetrominoType>();
        for (var index = values.Length - 1; index > 0; index--)
        {
            var swapIndex = _random.Next(index + 1);
            (values[index], values[swapIndex]) = (values[swapIndex], values[index]);
        }

        foreach (var value in values)
        {
            _bag.Enqueue(value);
        }
    }
}

