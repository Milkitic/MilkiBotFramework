using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MilkiBotFramework.Imaging.Wpf.Internal;

internal class DpiDecorator : Decorator
{
    public DpiDecorator()
    {
        Loaded += OnLoaded;
    }

    private void OnLoaded(object o, RoutedEventArgs routedEventArgs)
    {
        var presentationSource = PresentationSource.FromVisual(this);
        if (presentationSource?.CompositionTarget == null) return;

        var matrix = presentationSource.CompositionTarget.TransformToDevice;
        var dpiTransform = new ScaleTransform(1 / matrix.M11, 1 / matrix.M22);

        if (dpiTransform.CanFreeze) dpiTransform.Freeze();

        LayoutTransform = dpiTransform;
    }
}