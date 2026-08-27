namespace ClassicGamePlugin.Features.Game2048.Domain;

/// <summary>
/// 按经典规则均匀选择一个空格，并以 90%/10% 的概率生成 2/4。
/// 该类型无棋盘状态；随机源仅用于选择结果，不承担规则校验。
/// </summary>
internal sealed class RandomTileSpawnStrategy : ITileSpawnStrategy
{
    private readonly Random _random;

    /// <summary>使用进程共享随机源创建生产策略。</summary>
    public RandomTileSpawnStrategy()
        : this(Random.Shared)
    {
    }

    /// <summary>允许测试注入可控随机源，准确覆盖概率分界而不依赖统计采样。</summary>
    internal RandomTileSpawnStrategy(Random random)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
    }

    /// <inheritdoc />
    public Game2048TileSpawn CreateSpawn(IReadOnlyList<Game2048Position> emptyPositions)
    {
        ArgumentNullException.ThrowIfNull(emptyPositions);
        if (emptyPositions.Count == 0)
        {
            throw new ArgumentException("至少需要一个空格才能生成新方块。", nameof(emptyPositions));
        }

        var position = emptyPositions[_random.Next(emptyPositions.Count)];
        var value = _random.Next(10) == 0 ? 4 : 2;
        return new Game2048TileSpawn(position, value);
    }
}
