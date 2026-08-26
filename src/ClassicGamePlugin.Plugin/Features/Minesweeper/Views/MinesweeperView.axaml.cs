using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ClassicGamePlugin.Features.Minesweeper.ViewModels;

namespace ClassicGamePlugin.Features.Minesweeper.Views;

/// <summary>
/// 扫雷的 Avalonia View。它从创建开始只接收 <see cref="MinesweeperViewModel"/>，
/// 负责布局和转发左右键输入，不感知 Plugin SDK Document。
/// </summary>
public partial class MinesweeperView : UserControl
{
    private MinesweeperCellViewModel? _chordPreviewCenter;
    private MinesweeperCellViewModel? _suppressedPrimaryClickCell;

    /// <summary>创建视图并加载声明式布局。</summary>
    public MinesweeperView() => InitializeComponent();

    private void OnCellClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button { DataContext: MinesweeperCellViewModel cell })
        {
            HandlePrimaryCellAction(cell);
            eventArgs.Handled = true;
        }
    }

    private void OnCellPointerPressed(object? sender, PointerPressedEventArgs eventArgs)
    {
        if (sender is not Button { DataContext: MinesweeperCellViewModel cell } button ||
            DataContext is not MinesweeperViewModel)
        {
            return;
        }

        var properties = eventArgs.GetCurrentPoint(button).Properties;
        if (properties.IsRightButtonPressed)
        {
            HandlePointerButtons(
                cell,
                properties.IsLeftButtonPressed,
                properties.IsRightButtonPressed);
            eventArgs.Handled = true;
        }
    }

    private void OnCellPointerReleased(object? sender, PointerReleasedEventArgs eventArgs)
    {
        if (sender is not Button { DataContext: MinesweeperCellViewModel cell } button)
        {
            return;
        }

        var properties = eventArgs.GetCurrentPoint(button).Properties;
        if (HandlePointerButtonsReleased(
                cell,
                properties.IsLeftButtonPressed,
                properties.IsRightButtonPressed))
        {
            eventArgs.Handled = true;
        }
    }

    private void OnCellPointerCaptureLost(object? sender, PointerCaptureLostEventArgs eventArgs) =>
        CancelChordPreview();

    /// <summary>
    /// 转发 Button 已确认的主要操作。左键必须使用 <see cref="Button.Click"/>，不能依赖
    /// PointerPressed：Button 会先处理左键按下事件，普通 XAML 路由处理器可能因此收不到事件。
    /// </summary>
    internal void HandlePrimaryCellAction(MinesweeperCellViewModel cell)
    {
        if (DataContext is not MinesweeperViewModel viewModel)
        {
            return;
        }

        if (ReferenceEquals(_suppressedPrimaryClickCell, cell))
        {
            _suppressedPrimaryClickCell = null;
            return;
        }

        if (ReferenceEquals(_chordPreviewCenter, cell))
        {
            _chordPreviewCenter = null;
            viewModel.CompleteChordPreview(cell);
        }
        else
        {
            viewModel.RevealCell(cell);
        }
    }

    /// <summary>
    /// 把仍需区分物理按键的输入转换成游戏意图。单独右键切换旗帜；在已翻开的数字格上
    /// 同时按住左右键时执行经典“快速展开”。快速展开的旗帜校验和踩雷规则仍由领域层负责。
    /// </summary>
    internal void HandlePointerButtons(
        MinesweeperCellViewModel cell,
        bool isLeftButtonPressed,
        bool isRightButtonPressed)
    {
        if (DataContext is not MinesweeperViewModel viewModel || !isRightButtonPressed)
        {
            return;
        }

        if (isLeftButtonPressed && cell.IsRevealed)
        {
            if (viewModel.BeginChordPreview(cell))
            {
                _chordPreviewCenter = cell;
            }

            return;
        }

        // 组合键落在覆盖格上时不应意外插旗；只有纯右键才执行旗帜操作。
        if (!isLeftButtonPressed)
        {
            viewModel.ToggleFlag(cell);
        }
    }

    /// <summary>
    /// 在组合键中的任一按键松开时提交快速展开。若右键先松开而左键仍按住，Button 随后还会产生
    /// 一次 Click，因此记录抑制标记，避免同一手势被提交两次。
    /// </summary>
    internal bool HandlePointerButtonsReleased(
        MinesweeperCellViewModel cell,
        bool isLeftButtonPressed,
        bool isRightButtonPressed)
    {
        if (DataContext is not MinesweeperViewModel viewModel ||
            !ReferenceEquals(_chordPreviewCenter, cell) ||
            (isLeftButtonPressed && isRightButtonPressed))
        {
            return false;
        }

        _chordPreviewCenter = null;
        var completed = viewModel.CompleteChordPreview(cell);
        if (completed && isLeftButtonPressed)
        {
            _suppressedPrimaryClickCell = cell;
        }

        return completed;
    }

    /// <summary>取消 View 与 ViewModel 中的临时预览，确保捕获丢失后没有格子残留为下压状态。</summary>
    internal void CancelChordPreview()
    {
        _chordPreviewCenter = null;
        _suppressedPrimaryClickCell = null;
        if (DataContext is MinesweeperViewModel viewModel)
        {
            viewModel.CancelChordPreview();
        }
    }
}
