using Avalonia;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using ClassicGamePlugin.Features.Reversi.Domain;

namespace ClassicGamePlugin.Features.Reversi.ViewModels;

/// <summary>把一个领域格子投影为棋子、合法点、提示和最近落子的可绑定状态。</summary>
public sealed class ReversiCellViewModel : ObservableObject
{
    private static readonly IBrush BlackDiscBrush = new SolidColorBrush(Color.Parse("#FF171A1F"));
    private static readonly IBrush WhiteDiscBrush = new SolidColorBrush(Color.Parse("#FFF8FAFC"));
    private static readonly IBrush NormalBorderBrush = new SolidColorBrush(Color.Parse("#FF17623A"));
    private static readonly IBrush LastMoveBorderBrush = new SolidColorBrush(Color.Parse("#FF4CC9F0"));
    private static readonly IBrush HintBorderBrush = new SolidColorBrush(Color.Parse("#FFFFD166"));

    private ReversiDiscColor? _disc;
    private bool _isLegalMove;
    private bool _isHint;
    private bool _isLastMove;
    private bool _canInteract;

    internal ReversiCellViewModel(ReversiPosition position)
    {
        Position = position;
    }

    internal ReversiPosition Position { get; }
    public int Row => Position.Row;
    public int Column => Position.Column;
    public string Coordinate => Position.DisplayName;
    public bool HasDisc => _disc.HasValue;
    public bool IsLegalMove => _isLegalMove;
    public bool IsHint => _isHint;
    public bool IsLastMove => _isLastMove;
    public bool IsPlayable => _canInteract && _isLegalMove;
    public IBrush? DiscBrush => _disc switch
    {
        ReversiDiscColor.Black => BlackDiscBrush,
        ReversiDiscColor.White => WhiteDiscBrush,
        _ => null,
    };
    public IBrush CellBorderBrush => _isHint
        ? HintBorderBrush
        : _isLastMove ? LastMoveBorderBrush : NormalBorderBrush;
    public Thickness CellBorderThickness => new(_isHint || _isLastMove ? 3 : 1);
    public string AccessibleText => _disc switch
    {
        ReversiDiscColor.Black => $"{Coordinate}，黑棋",
        ReversiDiscColor.White => $"{Coordinate}，白棋",
        _ when _isHint => $"{Coordinate}，建议落子",
        _ when _isLegalMove => $"{Coordinate}，合法落子",
        _ => $"{Coordinate}，空格",
    };

    /// <summary>从最新领域快照一次刷新所有相关展示属性，避免 View 自行推导规则。</summary>
    internal void Refresh(
        ReversiDiscColor? disc,
        bool isLegalMove,
        bool isHint,
        bool isLastMove,
        bool canInteract)
    {
        _disc = disc;
        _isLegalMove = isLegalMove;
        _isHint = isHint;
        _isLastMove = isLastMove;
        _canInteract = canInteract;
        OnPropertyChanged(string.Empty);
    }
}
