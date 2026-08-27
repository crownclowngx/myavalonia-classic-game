namespace ClassicGamePlugin.Features.Xiangqi.Domain;

/// <summary>
/// 把内部固定坐标转换为双方视角的中文纵线记谱。记谱只描述已经通过规则验证的走法，不参与合法性判断；
/// 同一路同类棋子通过前、中、后或顺序编号消歧，确保局内记录始终能唯一定位原棋子。
/// </summary>
internal static class XiangqiNotation
{
    private static readonly string[] ChineseNumbers = ["零", "一", "二", "三", "四", "五", "六", "七", "八", "九"];

    internal static string Format(XiangqiGameSnapshot before, XiangqiMove move)
    {
        ArgumentNullException.ThrowIfNull(before);
        var piece = before.GetPiece(move.From) ?? throw new ArgumentException("记谱起点必须存在棋子。", nameof(move));
        var prefix = CreatePiecePrefix(before, move.From, piece);
        var rowDelta = move.To.Row - move.From.Row;
        var action = rowDelta == 0
            ? "平"
            : IsForward(piece.Side, rowDelta) ? "进" : "退";
        var destination = rowDelta == 0 || piece.Type is
            XiangqiPieceType.Horse or XiangqiPieceType.Advisor or XiangqiPieceType.Elephant
            ? FormatFile(piece.Side, move.To.Column)
            : FormatNumber(piece.Side, Math.Abs(rowDelta));
        return prefix + action + destination;
    }

    private static string CreatePiecePrefix(
        XiangqiGameSnapshot snapshot,
        XiangqiPosition from,
        XiangqiPiece piece)
    {
        var sameFile = Enumerable.Range(0, XiangqiRules.RowCount)
            .Select(row => new XiangqiPosition(row, from.Column))
            .Where(position => snapshot.GetPiece(position) == piece)
            .OrderBy(position => piece.Side == XiangqiSide.Red ? position.Row : -position.Row)
            .ToArray();
        if (sameFile.Length <= 1)
        {
            return piece.DisplayName + FormatFile(piece.Side, from.Column);
        }

        var index = Array.IndexOf(sameFile, from);
        var discriminator = sameFile.Length switch
        {
            2 => index == 0 ? "前" : "后",
            3 => index switch { 0 => "前", 1 => "中", _ => "后" },
            _ => index switch
            {
                0 => "前",
                var last when last == sameFile.Length - 1 => "后",
                _ => FormatNumber(piece.Side, index + 1),
            },
        };
        return discriminator + piece.DisplayName;
    }

    private static bool IsForward(XiangqiSide side, int rowDelta) =>
        side == XiangqiSide.Red ? rowDelta < 0 : rowDelta > 0;

    private static string FormatFile(XiangqiSide side, int column)
    {
        var number = side == XiangqiSide.Red ? column + 1 : XiangqiRules.ColumnCount - column;
        return FormatNumber(side, number);
    }

    private static string FormatNumber(XiangqiSide side, int number) =>
        side == XiangqiSide.Red
            ? ChineseNumbers[number]
            : number.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
