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
        Loaded += AvaTestControl_Loaded;
    }

    private async void AvaTestControl_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await FinishRender();
    }
}