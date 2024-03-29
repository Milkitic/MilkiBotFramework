﻿using System.Windows;
using System.Windows.Interop;

namespace MilkiBotFramework.Imaging.Wpf.Internal;

internal class HiddenWindow : Window
{
    public HiddenWindow()
    {
        SizeToContent = SizeToContent.WidthAndHeight;
        Width = 0;
        Height = 0;
        AllowsTransparency = true;
        WindowStyle = WindowStyle.None;
        ShowInTaskbar = false;
        ShowActivated = false;
        Opacity = 0;
    }

    public bool IsShown { get; private set; }

    /// <summary>
    /// 窗体显示事件
    /// </summary>
    public static readonly RoutedEvent ShownEvent = EventManager.RegisterRoutedEvent
        ("Shown", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(HiddenWindow));

    /// <summary>
    /// 当窗体显示时发生。
    /// </summary>
    public event RoutedEventHandler Shown
    {
        add => AddHandler(ShownEvent, value);
        remove => RemoveHandler(ShownEvent, value);
    }

    protected sealed override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);

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

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var source = PresentationSource.FromVisual(this) as HwndSource;
        source?.AddHook(WndProc);
    }

    // ReSharper disable once InconsistentNaming
    // ReSharper disable once IdentifierTypo
    private const int WM_DPICHANGED = 0x02E0;

    private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_DPICHANGED)
        {
            handled = true;
        }

        return IntPtr.Zero;
    }
}