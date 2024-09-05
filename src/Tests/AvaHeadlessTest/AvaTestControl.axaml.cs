using FluentAvalonia.UI.Controls;
using MilkiBotFramework.Data;
using MilkiBotFramework.Imaging.Avalonia;

namespace AvaHeadlessTest;

public class AvaTestViewModel : ViewModelBase
{
    public string Text { get; } = "Hello MilkiBotFramework!";
}

public partial class AvaTestControl : AvaRenderingControl<AvaTestViewModel>
{
    public AvaTestControl()
    {
        InitializeComponent();
        NavigationView.SelectedItem = NavigationView.MenuItems[0];
        Loaded += AvaTestControl_Loaded;
    }

    private async void AvaTestControl_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await FinishRender();
    }
}