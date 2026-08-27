namespace ClassicGamePlugin.Features.Xiangqi.Domain;

/// <summary>
/// 纯中国象棋规则入口。这里集中处理伪合法走法、将帅安全、终局和休闲重复裁定；它不保存真实棋局，
/// 也不关心玩家模式、计时、异步任务或界面文案。
/// </summary>
internal static class XiangqiRules
{
    internal const int RowCount = 10;
    internal const int ColumnCount = 9;
    internal const int CellCount = RowCount * ColumnCount;
    internal const int NoCaptureDrawPlyCount = 120;
    private static ulong _zobristSeed = 0x5849414E475149UL;
    private static readonly ulong[,] ZobristPieces = CreateZobristPieces();
    private static readonly ulong ZobristBlackToMove = NextSplitMix64(ref _zobristSeed);

    internal static XiangqiGameSnapshot CreateInitialSnapshot()
    {
        var board = new XiangqiPiece?[CellCount];
        PlaceBackRank(board, XiangqiSide.Black, row: 0);
        Set(board, new XiangqiPosition(2, 1), new XiangqiPiece(XiangqiSide.Black, XiangqiPieceType.Cannon));
        Set(board, new XiangqiPosition(2, 7), new XiangqiPiece(XiangqiSide.Black, XiangqiPieceType.Cannon));
        for (var column = 0; column < ColumnCount; column += 2)
        {
            Set(board, new XiangqiPosition(3, column), new XiangqiPiece(XiangqiSide.Black, XiangqiPieceType.Soldier));
        }

        PlaceBackRank(board, XiangqiSide.Red, row: 9);
        Set(board, new XiangqiPosition(7, 1), new XiangqiPiece(XiangqiSide.Red, XiangqiPieceType.Cannon));
        Set(board, new XiangqiPosition(7, 7), new XiangqiPiece(XiangqiSide.Red, XiangqiPieceType.Cannon));
        for (var column = 0; column < ColumnCount; column += 2)
        {
            Set(board, new XiangqiPosition(6, column), new XiangqiPiece(XiangqiSide.Red, XiangqiPieceType.Soldier));
        }

        var key = ComputePositionKey(board, XiangqiSide.Red);
        var signature = CreatePositionSignature(board, XiangqiSide.Red);
        return new XiangqiGameSnapshot(
            board,
            XiangqiSide.Red,
            XiangqiGameState.Ready,
            0,
            null,
            null,
            null,
            0,
            [new XiangqiPositionRecord(key, signature, XiangqiSide.Red, null, false)]);
    }

    internal static bool IsInside(XiangqiPosition position) =>
        position.Row is >= 0 and < RowCount && position.Column is >= 0 and < ColumnCount;

    internal static XiangqiSide OpponentOf(XiangqiSide side) =>
        side == XiangqiSide.Red ? XiangqiSide.Black : XiangqiSide.Red;

    internal static XiangqiMoveValidation ValidateMove(XiangqiGameSnapshot snapshot, XiangqiMove move)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.State == XiangqiGameState.Finished)
        {
            return new XiangqiMoveValidation(XiangqiMoveError.GameFinished);
        }

        var structural = ValidateStructuralMove(snapshot.CopyBoard(), snapshot.CurrentSide, move);
        if (!structural.IsLegal)
        {
            return structural;
        }

        var preview = ApplyBoardMove(snapshot.CopyBoard(), move);
        if (GeneralsFace(preview))
        {
            return new XiangqiMoveValidation(XiangqiMoveError.GeneralsFace);
        }

        if (IsInCheck(preview, snapshot.CurrentSide))
        {
            return new XiangqiMoveValidation(XiangqiMoveError.ExposesGeneral);
        }

        var movingPiece = snapshot.GetPiece(move.From)!.Value;
        var opponent = OpponentOf(movingPiece.Side);
        var gaveCheck = IsInCheck(preview, opponent);
        var nextKey = ComputePositionKey(preview, opponent);
        var nextSignature = CreatePositionSignature(preview, opponent);
        if (WouldBeForbiddenPerpetualCheck(
            snapshot.PositionHistory,
            new XiangqiPositionRecord(nextKey, nextSignature, opponent, movingPiece.Side, gaveCheck)))
        {
            return new XiangqiMoveValidation(XiangqiMoveError.PerpetualCheck);
        }

        return XiangqiMoveValidation.Legal;
    }

    /// <summary>
    /// 在全部规则通过后生成新快照。先判将死和困毙，再判三次重复与 120 半回合自然限着，
    /// 保证能够立即结束棋局的着法不会被较低优先级的和棋条件覆盖。
    /// </summary>
    internal static XiangqiMoveResult? TryApplyMove(XiangqiGameSnapshot snapshot, XiangqiMove move)
    {
        var validation = ValidateMove(snapshot, move);
        if (!validation.IsLegal)
        {
            return null;
        }

        var board = snapshot.CopyBoard();
        var movingPiece = board[IndexOf(move.From)]!.Value;
        var captured = board[IndexOf(move.To)];
        board[IndexOf(move.To)] = movingPiece;
        board[IndexOf(move.From)] = null;
        var nextSide = OpponentOf(movingPiece.Side);
        var gaveCheck = IsInCheck(board, nextSide);
        var nextRecord = new XiangqiPositionRecord(
            ComputePositionKey(board, nextSide),
            CreatePositionSignature(board, nextSide),
            nextSide,
            movingPiece.Side,
            gaveCheck);
        var history = snapshot.CopyPositionHistory().Append(nextRecord).ToArray();
        var noCapture = captured is null ? snapshot.NoCapturePlyCount + 1 : 0;
        var provisional = new XiangqiGameSnapshot(
            board,
            nextSide,
            XiangqiGameState.Running,
            snapshot.MoveCount + 1,
            move,
            null,
            null,
            noCapture,
            history);

        XiangqiSide? winner = null;
        XiangqiTerminationReason? reason = null;
        if (!HasAnyLegalMove(provisional))
        {
            winner = movingPiece.Side;
            reason = gaveCheck ? XiangqiTerminationReason.Checkmate : XiangqiTerminationReason.Stalemate;
        }
        else if (CountOccurrences(history, nextRecord) >= 3)
        {
            reason = XiangqiTerminationReason.ThreefoldRepetition;
        }
        else if (noCapture >= NoCaptureDrawPlyCount)
        {
            reason = XiangqiTerminationReason.NoCaptureLimit;
        }

        var after = new XiangqiGameSnapshot(
            board,
            nextSide,
            reason is null ? XiangqiGameState.Running : XiangqiGameState.Finished,
            snapshot.MoveCount + 1,
            move,
            winner,
            reason,
            noCapture,
            history);
        return new XiangqiMoveResult(
            snapshot,
            after,
            move,
            movingPiece,
            captured,
            gaveCheck,
            XiangqiNotation.Format(snapshot, move));
    }

    internal static IReadOnlyList<XiangqiMove> GetLegalMoves(XiangqiGameSnapshot snapshot) =>
        GetLegalMoves(snapshot, snapshot.CurrentSide);

    internal static IReadOnlyList<XiangqiMove> GetLegalMoves(
        XiangqiGameSnapshot snapshot,
        XiangqiSide side)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.State == XiangqiGameState.Finished)
        {
            return [];
        }

        var view = side == snapshot.CurrentSide ? snapshot : WithCurrentSide(snapshot, side);
        var moves = new List<XiangqiMove>(64);
        for (var row = 0; row < RowCount; row++)
        {
            for (var column = 0; column < ColumnCount; column++)
            {
                var from = new XiangqiPosition(row, column);
                if (view.GetPiece(from) is not { Side: var pieceSide } || pieceSide != side)
                {
                    continue;
                }

                foreach (var to in GeneratePseudoTargets(view.CopyBoard(), from))
                {
                    var move = new XiangqiMove(from, to);
                    if (ValidateMove(view, move).IsLegal)
                    {
                        moves.Add(move);
                    }
                }
            }
        }

        return moves;
    }

    internal static bool IsInCheck(XiangqiGameSnapshot snapshot, XiangqiSide side) =>
        IsInCheck(snapshot.CopyBoard(), side);

    internal static bool IsSquareAttacked(
        XiangqiGameSnapshot snapshot,
        XiangqiPosition target,
        XiangqiSide bySide) =>
        IsSquareAttacked(snapshot.CopyBoard(), target, bySide);

    internal static bool WouldGiveCheck(XiangqiGameSnapshot snapshot, XiangqiMove move)
    {
        var piece = snapshot.GetPiece(move.From);
        if (piece is null)
        {
            return false;
        }

        var board = ApplyBoardMove(snapshot.CopyBoard(), move);
        return IsInCheck(board, OpponentOf(piece.Value.Side));
    }

    internal static bool IsInCheck(XiangqiPiece?[] board, XiangqiSide side)
    {
        var general = FindGeneral(board, side);
        return general is null || IsSquareAttacked(board, general.Value, OpponentOf(side));
    }

    internal static int CountPieces(XiangqiGameSnapshot snapshot, XiangqiSide side) =>
        snapshot.CopyBoard().Count(piece => piece?.Side == side);

    internal static ulong ComputePositionKey(IEnumerable<XiangqiPiece?> board, XiangqiSide currentSide)
    {
        var cells = board.ToArray();
        if (cells.Length != CellCount)
        {
            throw new ArgumentException("棋盘长度必须为 90。", nameof(board));
        }

        ulong key = currentSide == XiangqiSide.Black ? ZobristBlackToMove : 0;
        for (var index = 0; index < cells.Length; index++)
        {
            if (cells[index] is { } piece)
            {
                key ^= ZobristPieces[index, PieceIndex(piece)];
            }
        }

        return key;
    }

    internal static string CreatePositionSignature(IEnumerable<XiangqiPiece?> board, XiangqiSide side)
    {
        var cells = board.ToArray();
        var chars = new char[CellCount + 1];
        for (var index = 0; index < CellCount; index++)
        {
            chars[index] = cells[index] is { } piece
                ? (char)('A' + PieceIndex(piece))
                : '.';
        }

        chars[^1] = side == XiangqiSide.Red ? 'R' : 'B';
        return new string(chars);
    }

    private static XiangqiMoveValidation ValidateStructuralMove(
        XiangqiPiece?[] board,
        XiangqiSide side,
        XiangqiMove move)
    {
        if (!IsInside(move.From) || !IsInside(move.To))
        {
            return new XiangqiMoveValidation(XiangqiMoveError.OutOfBounds);
        }

        if (board[IndexOf(move.From)] is not { } piece)
        {
            return new XiangqiMoveValidation(XiangqiMoveError.EmptyOrigin);
        }

        if (piece.Side != side)
        {
            return new XiangqiMoveValidation(XiangqiMoveError.WrongSide);
        }

        if (board[IndexOf(move.To)] is { } target)
        {
            if (target.Side == side)
            {
                return new XiangqiMoveValidation(XiangqiMoveError.FriendlyDestination);
            }

            if (target.Type == XiangqiPieceType.General)
            {
                return new XiangqiMoveValidation(XiangqiMoveError.GeneralCaptureNotAllowed);
            }
        }

        var rowDelta = move.To.Row - move.From.Row;
        var columnDelta = move.To.Column - move.From.Column;
        var absoluteRow = Math.Abs(rowDelta);
        var absoluteColumn = Math.Abs(columnDelta);
        return piece.Type switch
        {
            XiangqiPieceType.General => ValidateGeneral(piece.Side, move.To, absoluteRow, absoluteColumn),
            XiangqiPieceType.Advisor => ValidateAdvisor(piece.Side, move.To, absoluteRow, absoluteColumn),
            XiangqiPieceType.Elephant => ValidateElephant(board, piece.Side, move, absoluteRow, absoluteColumn),
            XiangqiPieceType.Horse => ValidateHorse(board, move, absoluteRow, absoluteColumn),
            XiangqiPieceType.Chariot => ValidateChariot(board, move, absoluteRow, absoluteColumn),
            XiangqiPieceType.Cannon => ValidateCannon(board, move, absoluteRow, absoluteColumn),
            XiangqiPieceType.Soldier => ValidateSoldier(piece.Side, move.From, rowDelta, columnDelta),
            _ => new XiangqiMoveValidation(XiangqiMoveError.PieceMovement),
        };
    }

    private static XiangqiMoveValidation ValidateGeneral(
        XiangqiSide side,
        XiangqiPosition to,
        int row,
        int column) =>
        !IsInPalace(side, to)
            ? new XiangqiMoveValidation(XiangqiMoveError.PalaceRestricted)
            : row + column == 1
                ? XiangqiMoveValidation.Legal
                : new XiangqiMoveValidation(XiangqiMoveError.PieceMovement);

    private static XiangqiMoveValidation ValidateAdvisor(
        XiangqiSide side,
        XiangqiPosition to,
        int row,
        int column) =>
        !IsInPalace(side, to)
            ? new XiangqiMoveValidation(XiangqiMoveError.PalaceRestricted)
            : row == 1 && column == 1
                ? XiangqiMoveValidation.Legal
                : new XiangqiMoveValidation(XiangqiMoveError.PieceMovement);

    private static XiangqiMoveValidation ValidateElephant(
        XiangqiPiece?[] board,
        XiangqiSide side,
        XiangqiMove move,
        int row,
        int column)
    {
        if (row != 2 || column != 2)
        {
            return new XiangqiMoveValidation(XiangqiMoveError.PieceMovement);
        }

        if (side == XiangqiSide.Red ? move.To.Row < 5 : move.To.Row > 4)
        {
            return new XiangqiMoveValidation(XiangqiMoveError.ElephantCrossesRiver);
        }

        var eye = new XiangqiPosition(
            (move.From.Row + move.To.Row) / 2,
            (move.From.Column + move.To.Column) / 2);
        return board[IndexOf(eye)] is null
            ? XiangqiMoveValidation.Legal
            : new XiangqiMoveValidation(XiangqiMoveError.ElephantEyeBlocked);
    }

    private static XiangqiMoveValidation ValidateHorse(
        XiangqiPiece?[] board,
        XiangqiMove move,
        int row,
        int column)
    {
        if (!((row == 2 && column == 1) || (row == 1 && column == 2)))
        {
            return new XiangqiMoveValidation(XiangqiMoveError.PieceMovement);
        }

        var leg = row == 2
            ? new XiangqiPosition(move.From.Row + Math.Sign(move.To.Row - move.From.Row), move.From.Column)
            : new XiangqiPosition(move.From.Row, move.From.Column + Math.Sign(move.To.Column - move.From.Column));
        return board[IndexOf(leg)] is null
            ? XiangqiMoveValidation.Legal
            : new XiangqiMoveValidation(XiangqiMoveError.HorseLegBlocked);
    }

    private static XiangqiMoveValidation ValidateChariot(
        XiangqiPiece?[] board,
        XiangqiMove move,
        int row,
        int column)
    {
        if ((row == 0) == (column == 0))
        {
            return new XiangqiMoveValidation(XiangqiMoveError.PieceMovement);
        }

        return CountBetween(board, move) == 0
            ? XiangqiMoveValidation.Legal
            : new XiangqiMoveValidation(XiangqiMoveError.PathBlocked);
    }

    private static XiangqiMoveValidation ValidateCannon(
        XiangqiPiece?[] board,
        XiangqiMove move,
        int row,
        int column)
    {
        if ((row == 0) == (column == 0))
        {
            return new XiangqiMoveValidation(XiangqiMoveError.PieceMovement);
        }

        var screens = CountBetween(board, move);
        var isCapture = board[IndexOf(move.To)] is not null;
        return (!isCapture && screens == 0) || (isCapture && screens == 1)
            ? XiangqiMoveValidation.Legal
            : new XiangqiMoveValidation(XiangqiMoveError.CannonScreen);
    }

    private static XiangqiMoveValidation ValidateSoldier(
        XiangqiSide side,
        XiangqiPosition from,
        int rowDelta,
        int columnDelta)
    {
        var forward = side == XiangqiSide.Red ? -1 : 1;
        var crossedRiver = side == XiangqiSide.Red ? from.Row <= 4 : from.Row >= 5;
        var legal = rowDelta == forward && columnDelta == 0 ||
            crossedRiver && rowDelta == 0 && Math.Abs(columnDelta) == 1;
        return legal
            ? XiangqiMoveValidation.Legal
            : new XiangqiMoveValidation(XiangqiMoveError.SoldierDirection);
    }

    private static bool IsSquareAttacked(XiangqiPiece?[] board, XiangqiPosition target, XiangqiSide bySide)
    {
        for (var row = 0; row < RowCount; row++)
        {
            for (var column = 0; column < ColumnCount; column++)
            {
                var from = new XiangqiPosition(row, column);
                if (board[IndexOf(from)] is not { } piece || piece.Side != bySide)
                {
                    continue;
                }

                if (Attacks(board, piece, from, target))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool Attacks(
        XiangqiPiece?[] board,
        XiangqiPiece piece,
        XiangqiPosition from,
        XiangqiPosition target)
    {
        var row = Math.Abs(target.Row - from.Row);
        var column = Math.Abs(target.Column - from.Column);
        return piece.Type switch
        {
            XiangqiPieceType.General => row + column == 1 && IsInPalace(piece.Side, target) ||
                from.Column == target.Column && CountBetween(board, new XiangqiMove(from, target)) == 0 &&
                board[IndexOf(target)] is { Type: XiangqiPieceType.General },
            XiangqiPieceType.Advisor => row == 1 && column == 1 && IsInPalace(piece.Side, target),
            XiangqiPieceType.Elephant => row == 2 && column == 2 &&
                (piece.Side == XiangqiSide.Red ? target.Row >= 5 : target.Row <= 4) &&
                board[IndexOf(new XiangqiPosition((from.Row + target.Row) / 2, (from.Column + target.Column) / 2))] is null,
            XiangqiPieceType.Horse => ((row == 2 && column == 1) || (row == 1 && column == 2)) &&
                board[IndexOf(row == 2
                    ? new XiangqiPosition(from.Row + Math.Sign(target.Row - from.Row), from.Column)
                    : new XiangqiPosition(from.Row, from.Column + Math.Sign(target.Column - from.Column)))] is null,
            XiangqiPieceType.Chariot => (row == 0) != (column == 0) &&
                CountBetween(board, new XiangqiMove(from, target)) == 0,
            XiangqiPieceType.Cannon => (row == 0) != (column == 0) &&
                CountBetween(board, new XiangqiMove(from, target)) == 1,
            XiangqiPieceType.Soldier => SoldierAttacks(piece.Side, from, target),
            _ => false,
        };
    }

    private static bool SoldierAttacks(XiangqiSide side, XiangqiPosition from, XiangqiPosition target)
    {
        var forward = side == XiangqiSide.Red ? -1 : 1;
        if (target.Row - from.Row == forward && target.Column == from.Column)
        {
            return true;
        }

        var crossed = side == XiangqiSide.Red ? from.Row <= 4 : from.Row >= 5;
        return crossed && target.Row == from.Row && Math.Abs(target.Column - from.Column) == 1;
    }

    private static IEnumerable<XiangqiPosition> GeneratePseudoTargets(
        XiangqiPiece?[] board,
        XiangqiPosition from)
    {
        var piece = board[IndexOf(from)]!.Value;
        var offsets = piece.Type switch
        {
            XiangqiPieceType.General => new[] { (-1, 0), (1, 0), (0, -1), (0, 1) },
            XiangqiPieceType.Advisor => new[] { (-1, -1), (-1, 1), (1, -1), (1, 1) },
            XiangqiPieceType.Elephant => new[] { (-2, -2), (-2, 2), (2, -2), (2, 2) },
            XiangqiPieceType.Horse => new[]
            {
                (-2, -1), (-2, 1), (-1, -2), (-1, 2),
                (1, -2), (1, 2), (2, -1), (2, 1),
            },
            XiangqiPieceType.Soldier => piece.Side == XiangqiSide.Red
                ? new[] { (-1, 0), (0, -1), (0, 1) }
                : new[] { (1, 0), (0, -1), (0, 1) },
            _ => [],
        };
        if (piece.Type is not (XiangqiPieceType.Chariot or XiangqiPieceType.Cannon))
        {
            foreach (var (row, column) in offsets)
            {
                var target = new XiangqiPosition(from.Row + row, from.Column + column);
                if (IsInside(target))
                {
                    yield return target;
                }
            }

            yield break;
        }

        foreach (var (rowDirection, columnDirection) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
        {
            for (var step = 1; step < RowCount; step++)
            {
                var target = new XiangqiPosition(
                    from.Row + (rowDirection * step),
                    from.Column + (columnDirection * step));
                if (!IsInside(target))
                {
                    break;
                }

                yield return target;
            }
        }
    }

    private static bool HasAnyLegalMove(XiangqiGameSnapshot snapshot)
    {
        for (var row = 0; row < RowCount; row++)
        {
            for (var column = 0; column < ColumnCount; column++)
            {
                var from = new XiangqiPosition(row, column);
                if (snapshot.GetPiece(from) is not { Side: var side } || side != snapshot.CurrentSide)
                {
                    continue;
                }

                foreach (var to in GeneratePseudoTargets(snapshot.CopyBoard(), from))
                {
                    if (ValidateMove(snapshot, new XiangqiMove(from, to)).IsLegal)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool WouldBeForbiddenPerpetualCheck(
        IReadOnlyList<XiangqiPositionRecord> history,
        XiangqiPositionRecord candidate)
    {
        var matches = history
            .Select((record, index) => (record, index))
            .Where(item => item.record.Key == candidate.Key && item.record.Signature == candidate.Signature)
            .Select(item => item.index)
            .ToList();
        if (matches.Count < 2 || candidate.Mover is not { } candidateMover)
        {
            return false;
        }

        var cycle = history.Skip(matches[^2] + 1).Append(candidate).ToArray();
        var opponent = OpponentOf(candidateMover);
        var candidateAlwaysChecks = MovesBy(candidateMover).All(record => record.GaveCheck);
        var opponentMoves = MovesBy(opponent).ToArray();
        var opponentAlwaysChecks = opponentMoves.Length > 0 && opponentMoves.All(record => record.GaveCheck);
        return candidateAlwaysChecks && !opponentAlwaysChecks;

        IEnumerable<XiangqiPositionRecord> MovesBy(XiangqiSide side) =>
            cycle.Where(record => record.Mover == side);
    }

    private static int CountOccurrences(
        IEnumerable<XiangqiPositionRecord> history,
        XiangqiPositionRecord target) =>
        history.Count(record => record.Key == target.Key && record.Signature == target.Signature);

    private static XiangqiGameSnapshot WithCurrentSide(XiangqiGameSnapshot snapshot, XiangqiSide side)
    {
        var board = snapshot.CopyBoard();
        var history = snapshot.CopyPositionHistory();
        var key = ComputePositionKey(board, side);
        var signature = CreatePositionSignature(board, side);
        history[^1] = history[^1] with { Key = key, Signature = signature, SideToMove = side };
        return new XiangqiGameSnapshot(
            board,
            side,
            snapshot.State,
            snapshot.MoveCount,
            snapshot.LastMove,
            snapshot.Winner,
            snapshot.TerminationReason,
            snapshot.NoCapturePlyCount,
            history);
    }

    private static XiangqiPiece?[] ApplyBoardMove(XiangqiPiece?[] board, XiangqiMove move)
    {
        board[IndexOf(move.To)] = board[IndexOf(move.From)];
        board[IndexOf(move.From)] = null;
        return board;
    }

    private static bool GeneralsFace(XiangqiPiece?[] board)
    {
        var red = FindGeneral(board, XiangqiSide.Red);
        var black = FindGeneral(board, XiangqiSide.Black);
        return red is { } redPosition && black is { } blackPosition &&
            redPosition.Column == blackPosition.Column &&
            CountBetween(board, new XiangqiMove(redPosition, blackPosition)) == 0;
    }

    private static XiangqiPosition? FindGeneral(XiangqiPiece?[] board, XiangqiSide side)
    {
        for (var index = 0; index < board.Length; index++)
        {
            if (board[index] is { Side: var pieceSide, Type: XiangqiPieceType.General } && pieceSide == side)
            {
                return new XiangqiPosition(index / ColumnCount, index % ColumnCount);
            }
        }

        return null;
    }

    private static int CountBetween(XiangqiPiece?[] board, XiangqiMove move)
    {
        var rowDirection = Math.Sign(move.To.Row - move.From.Row);
        var columnDirection = Math.Sign(move.To.Column - move.From.Column);
        var current = new XiangqiPosition(move.From.Row + rowDirection, move.From.Column + columnDirection);
        var count = 0;
        while (current != move.To)
        {
            if (board[IndexOf(current)] is not null)
            {
                count++;
            }

            current = new XiangqiPosition(current.Row + rowDirection, current.Column + columnDirection);
        }

        return count;
    }

    private static bool IsInPalace(XiangqiSide side, XiangqiPosition position) =>
        position.Column is >= 3 and <= 5 &&
        (side == XiangqiSide.Red ? position.Row is >= 7 and <= 9 : position.Row is >= 0 and <= 2);

    private static void PlaceBackRank(XiangqiPiece?[] board, XiangqiSide side, int row)
    {
        XiangqiPieceType[] types =
        [
            XiangqiPieceType.Chariot,
            XiangqiPieceType.Horse,
            XiangqiPieceType.Elephant,
            XiangqiPieceType.Advisor,
            XiangqiPieceType.General,
            XiangqiPieceType.Advisor,
            XiangqiPieceType.Elephant,
            XiangqiPieceType.Horse,
            XiangqiPieceType.Chariot,
        ];
        for (var column = 0; column < types.Length; column++)
        {
            Set(board, new XiangqiPosition(row, column), new XiangqiPiece(side, types[column]));
        }
    }

    private static void Set(XiangqiPiece?[] board, XiangqiPosition position, XiangqiPiece piece) =>
        board[IndexOf(position)] = piece;

    private static int IndexOf(XiangqiPosition position) =>
        (position.Row * ColumnCount) + position.Column;

    private static int PieceIndex(XiangqiPiece piece) =>
        ((int)piece.Side * 7) + (int)piece.Type;

    private static ulong[,] CreateZobristPieces()
    {
        var values = new ulong[CellCount, 14];
        var seed = _zobristSeed;
        for (var cell = 0; cell < CellCount; cell++)
        {
            for (var piece = 0; piece < 14; piece++)
            {
                values[cell, piece] = NextSplitMix64(ref seed);
            }
        }

        _zobristSeed = seed;
        return values;
    }

    private static ulong NextSplitMix64(ref ulong state)
    {
        state += 0x9E3779B97F4A7C15UL;
        var value = state;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }
}
