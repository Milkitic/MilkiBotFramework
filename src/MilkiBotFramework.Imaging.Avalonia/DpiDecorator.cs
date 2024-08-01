using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace MilkiBotFramework.Imaging.Avalonia;

public class DpiDecorator : LayoutTransformControl
{
    public DpiDecorator()
    {
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? o, RoutedEventArgs routedEventArgs)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is not WindowBase window) return;

        var screenFromVisual = window.Screens.ScreenFromVisual(this);
        if (screenFromVisual == null) return;

        var scaling = screenFromVisual.Scaling;
        var dpiTransform = new ScaleTransform(1 / scaling, 1 / scaling);
        LayoutTransform = dpiTransform;
    }
}