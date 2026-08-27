using ClassicGamePlugin.Features.Game2048.Domain;

namespace ClassicGamePlugin.Tests;

/// <summary>按预设队列返回方块，用于精确制造位置、数值和非法策略结果。</summary>
internal sealed class QueuedTileSpawnStrategy(params Game2048TileSpawn[] spawns) : ITileSpawnStrategy
{
    private readonly Queue<Game2048TileSpawn> _spawns = new(spawns);

    internal int CallCount { get; private set; }

    public Game2048TileSpawn CreateSpawn(IReadOnlyList<Game2048Position> emptyPositions)
    {
        CallCount++;
        return _spawns.Count > 0
            ? _spawns.Dequeue()
            : new Game2048TileSpawn(emptyPositions[0], 2);
    }
}

/// <summary>每次选择当前第一个空格，并按队列提供数值；队列用尽后固定生成 2。</summary>
internal sealed class FirstEmptyTileSpawnStrategy(params int[] values) : ITileSpawnStrategy
{
    private readonly Queue<int> _values = new(values);

    internal int CallCount { get; private set; }

    public Game2048TileSpawn CreateSpawn(IReadOnlyList<Game2048Position> emptyPositions)
    {
        CallCount++;
        return new Game2048TileSpawn(
            emptyPositions[0],
            _values.Count > 0 ? _values.Dequeue() : 2);
    }
}

/// <summary>让 RandomTileSpawnStrategy 的两个 Next 调用返回预设值，避免概率测试依赖统计结果。</summary>
internal sealed class SequenceRandom(params int[] values) : Random
{
    private readonly Queue<int> _values = new(values);

    public override int Next(int maxValue)
    {
        var value = _values.Dequeue();
        if (value < 0 || value >= maxValue)
        {
            throw new InvalidOperationException("预设随机值超出了本次 Next 调用的范围。");
        }

        return value;
    }
}
