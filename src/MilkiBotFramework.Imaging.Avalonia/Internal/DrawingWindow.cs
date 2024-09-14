using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace MilkiBotFramework.Imaging.Avalonia.Internal;

internal class DrawingWindow : Window
{
    public DrawingWindow()
    {
        SizeToContent = SizeToContent.WidthAndHeight;
        //Width = 0;
        //Height = 0;
        //AllowsTransparency = true;
        //WindowStyle = WindowStyle.None;
        ShowInTaskbar = false;
        ShowActivated = false;
        //Opacity = 0;
    }

    public bool IsShown { get; private set; }

    /// <summary>
    /// 窗体显示事件
    /// </summary>
    public static readonly RoutedEvent<RoutedEventArgs> ShownEvent =
        RoutedEvent.Register<DrawingWindow, RoutedEventArgs>(
            nameof(Shown), RoutingStrategies.Tunnel | RoutingStrategies.Bubble);

    /// <summary>
    /// 当窗体显示时发生。
    /// </summary>
    public event EventHandler<RoutedEventArgs>? Shown
    {
        add => AddHandler(ShownEvent, value);
        remove => RemoveHandler(ShownEvent, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (IsShown)
            return;

        IsShown = true;

        var args = new RoutedEventArgs(ShownEvent, this);
        RaiseEvent(args);
    }

    public async Task WaitForShown()
    {
        if (IsShown) return;
        var tcs = new TaskCompletionSource();
        Shown += (_, _) => tcs.SetResult();
        if (IsShown) return;
        await tcs.Task;
    }
}