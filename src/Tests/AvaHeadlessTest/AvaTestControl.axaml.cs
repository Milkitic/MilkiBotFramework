using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
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
        Button.Focus(NavigationMethod.Tab);
        Loaded += AvaTestControl_Loaded;
    }

    private async void AvaTestControl_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        //await Task.Delay(200);
        var topLevel = TopLevel.GetTopLevel(this);
        topLevel.MouseDown(new Point(251, 165), MouseButton.Left);
        //topLevel.MouseDown(new Point(251, 247), MouseButton.Left);
        //topLevel.MouseUp(new Point(251, 247), MouseButton.Left);
        //topLevel.MouseMove(new Point(271, 165));
        await Task.Delay(200);
        await FinishRender();
    }
}