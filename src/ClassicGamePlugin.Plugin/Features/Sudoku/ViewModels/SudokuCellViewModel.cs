using CommunityToolkit.Mvvm.ComponentModel;
using ClassicGamePlugin.Features.Sudoku.Domain;

namespace ClassicGamePlugin.Features.Sudoku.ViewModels;

/// <summary>
/// 单格只读视觉投影。它不修改对局；BoardControl 可以统一读取数字、候选、冲突和选择状态进行零图片绘制。
/// </summary>
public sealed class SudokuCellViewModel : ObservableObject
{
    private int _value;
    private int _notesMask;
    private bool _isGiven;
    private bool _isHint;
    private bool _isConflict;
    private bool _isSelected;
    private bool _isRelated;
    private bool _hasSameValue;

    internal SudokuCellViewModel(int row, int column)
    {
        Row = row;
        Column = column;
    }

    public int Row { get; }
    public int Column { get; }
    public int Value => _value;
    public int NotesMask => _notesMask;
    public bool IsGiven => _isGiven;
    public bool IsHint => _isHint;
    public bool IsConflict => _isConflict;
    public bool IsSelected => _isSelected;
    public bool IsRelated => _isRelated;
    public bool HasSameValue => _hasSameValue;
    public bool HasValue => _value != 0;
    public string DisplayText => _value == 0 ? string.Empty : _value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>供悬停提示与辅助技术说明当前格的完整中文状态。</summary>
    public string AccessibleText
    {
        get
        {
            var coordinate = $"第 {Row + 1} 行，第 {Column + 1} 列";
            if (_value != 0)
            {
                var kind = _isGiven ? "题目给定" : _isHint ? "提示填入" : "玩家填入";
                return $"{coordinate}，{kind}数字 {_value}{(_isConflict ? "，存在冲突" : string.Empty)}";
            }

            var notes = Enumerable.Range(1, 9)
                .Where(number => HasNote(number))
                .Select(number => number.ToString(System.Globalization.CultureInfo.InvariantCulture));
            var noteText = string.Join("、", notes);
            return string.IsNullOrEmpty(noteText)
                ? $"{coordinate}，空格"
                : $"{coordinate}，候选数字 {noteText}";
        }
    }

    public bool HasNote(int number) => (_notesMask & (1 << number)) != 0;

    internal void Refresh(
        int value,
        int notesMask,
        bool isGiven,
        bool isHint,
        bool isConflict,
        bool isSelected,
        bool isRelated,
        bool hasSameValue)
    {
        var accessibleChanged = _value != value || _notesMask != notesMask ||
                                _isGiven != isGiven || _isHint != isHint || _isConflict != isConflict;
        SetProperty(ref _value, value, nameof(Value));
        SetProperty(ref _notesMask, notesMask, nameof(NotesMask));
        SetProperty(ref _isGiven, isGiven, nameof(IsGiven));
        SetProperty(ref _isHint, isHint, nameof(IsHint));
        SetProperty(ref _isConflict, isConflict, nameof(IsConflict));
        SetProperty(ref _isSelected, isSelected, nameof(IsSelected));
        SetProperty(ref _isRelated, isRelated, nameof(IsRelated));
        SetProperty(ref _hasSameValue, hasSameValue, nameof(HasSameValue));
        OnPropertyChanged(nameof(HasValue));
        OnPropertyChanged(nameof(DisplayText));
        if (accessibleChanged)
        {
            OnPropertyChanged(nameof(AccessibleText));
        }
    }
}
