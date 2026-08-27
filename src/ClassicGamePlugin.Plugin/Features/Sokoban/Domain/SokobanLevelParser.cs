namespace ClassicGamePlugin.Features.Sokoban.Domain;

/// <summary>
/// 把便于人工评审的经典 ASCII 地图转换为强类型关卡。解析器只负责结构合法性，不尝试在生产代码中求解地图；
/// 内置地图的可解性由测试回放已知答案保证，使运行时保持小而确定。
/// </summary>
internal static class SokobanLevelParser
{
    internal static SokobanLevelDefinition Parse(
        string id,
        string name,
        SokobanDifficulty difficulty,
        params string[] rows)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Length < 3)
        {
            throw new ArgumentException("推箱子地图至少需要三行。", nameof(rows));
        }

        var width = rows[0]?.Length ?? 0;
        if (width < 3)
        {
            throw new ArgumentException("推箱子地图每行至少需要三个字符。", nameof(rows));
        }

        if (rows.Any(row => row is null || row.Length != width))
        {
            throw new ArgumentException("推箱子地图必须是行宽一致的矩形。", nameof(rows));
        }

        var terrain = new SokobanTerrain[width * rows.Length];
        var boxes = new List<SokobanPosition>();
        SokobanPosition? player = null;
        var goalCount = 0;

        for (var row = 0; row < rows.Length; row++)
        {
            for (var column = 0; column < width; column++)
            {
                var symbol = rows[row][column];
                if ((row == 0 || row == rows.Length - 1 || column == 0 || column == width - 1) && symbol != '#')
                {
                    throw new ArgumentException("推箱子地图的矩形边界必须全部由墙封闭。", nameof(rows));
                }

                var position = new SokobanPosition(row, column);
                var index = (row * width) + column;
                switch (symbol)
                {
                    case '#':
                        terrain[index] = SokobanTerrain.Wall;
                        break;
                    case ' ':
                        terrain[index] = SokobanTerrain.Floor;
                        break;
                    case '.':
                        terrain[index] = SokobanTerrain.Goal;
                        goalCount++;
                        break;
                    case '$':
                        terrain[index] = SokobanTerrain.Floor;
                        boxes.Add(position);
                        break;
                    case '*':
                        terrain[index] = SokobanTerrain.Goal;
                        boxes.Add(position);
                        goalCount++;
                        break;
                    case '@':
                    case '+':
                        if (player.HasValue)
                        {
                            throw new ArgumentException("推箱子地图必须且只能包含一个玩家。", nameof(rows));
                        }

                        terrain[index] = symbol == '+' ? SokobanTerrain.Goal : SokobanTerrain.Floor;
                        if (symbol == '+')
                        {
                            goalCount++;
                        }

                        player = position;
                        break;
                    default:
                        throw new ArgumentException($"推箱子地图包含不支持的字符“{symbol}”。", nameof(rows));
                }
            }
        }

        if (!player.HasValue)
        {
            throw new ArgumentException("推箱子地图必须包含一个玩家。", nameof(rows));
        }

        if (boxes.Count == 0)
        {
            throw new ArgumentException("推箱子地图至少需要一个箱子。", nameof(rows));
        }

        if (boxes.Count != goalCount)
        {
            throw new ArgumentException($"推箱子地图的箱子数 {boxes.Count} 必须等于目标数 {goalCount}。", nameof(rows));
        }

        return new SokobanLevelDefinition(
            id,
            name,
            difficulty,
            width,
            rows.Length,
            terrain,
            player.Value,
            boxes);
    }
}
