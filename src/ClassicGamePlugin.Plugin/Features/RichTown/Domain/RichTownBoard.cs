using System.Collections.Immutable;

namespace ClassicGamePlugin.Features.RichTown.Domain;

/// <summary>小镇首版唯一的规则数值来源。固定地图不接受外部可变集合，规则升级时必须同时提升版本。</summary>
internal static class RichTownBoard
{
    public const int RulesVersion = 1;
    public const int CellCount = 24;
    public const int PlayerCount = 3;
    public const int InitialCash = 1500;
    public const int StartReward = 200;
    public const int Tax = 100;
    public const int MaxRounds = 30;
    public const int MaxLevel = 2;
    public const int ComputerReserve = 300;

    /// <summary>机会卡按固定编号排列：正数由银行支付，负数向银行缴费；卡牌数量也决定等概率抽样范围。</summary>
    public static ImmutableArray<int> ChanceAmounts { get; } = [100, 200, -100, -200];

    public static ImmutableArray<RichTownCell> Cells { get; } = Enumerable.Range(0, CellCount)
        .Select(index => new RichTownCell(index, index switch
        {
            0 => RichTownCellKind.Start,
            6 or 18 => RichTownCellKind.Chance,
            12 => RichTownCellKind.Tax,
            3 or 15 => RichTownCellKind.Rest,
            _ => RichTownCellKind.Property
        }, index < 8 ? 100 : index < 16 ? 150 : 200))
        .Select(cell => cell.Kind == RichTownCellKind.Property ? cell : cell with { Price = 0 })
        .ToImmutableArray();

    /// <summary>租金只与购买价、等级有关；输入单位为整数货币，不使用浮点数。</summary>
    public static int Rent(RichTownProperty property) => property.Level switch
    {
        0 => Cells[property.Index].Price / 5,
        1 => Cells[property.Index].Price / 2,
        2 => Cells[property.Index].Price,
        _ => throw new ArgumentOutOfRangeException(nameof(property), "建筑等级必须在 0–2 之间。")
    };

    public static int BookValue(RichTownProperty property) => Cells[property.Index].Price * (property.Level + 1);
    public static int SaleValue(RichTownProperty property) => BookValue(property) / 2;
}

/// <summary>地块类型是小镇自己的规则概念，与渲染模型及其他游戏没有关联。</summary>
internal enum RichTownCellKind { Start, Property, Chance, Tax, Rest }
internal sealed record RichTownCell(int Index, RichTownCellKind Kind, int Price);
