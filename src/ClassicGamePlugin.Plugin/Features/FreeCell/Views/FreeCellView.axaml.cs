using Avalonia.Controls;
using Avalonia.Input;
using ClassicGamePlugin.Features.FreeCell.ViewModels;

namespace ClassicGamePlugin.Features.FreeCell.Views;

public partial class FreeCellView : UserControl
{
    public FreeCellView() => InitializeComponent();

    private void OnDealNumberKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key == Key.Enter && DataContext is FreeCellViewModel viewModel)
        {
            viewModel.LoadDealCommand.Execute(null);
            eventArgs.Handled = true;
        }
    }
}
