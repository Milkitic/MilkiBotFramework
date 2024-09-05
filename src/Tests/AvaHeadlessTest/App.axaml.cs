using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace AvaHeadlessTest;

// ReSharper disable once PartialTypeWithSinglePart
public partial class App : Application
{
    private readonly TaskCompletionSource _setupFinished;

    public App()
    {
        _setupFinished = new TaskCompletionSource();
    }

    public App(TaskCompletionSource setupFinished)
    {
        _setupFinished = setupFinished;
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.Startup += Desktop_Startup;
            Dispatcher.UIThread.UnhandledException += UIThread_UnhandledException;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void Desktop_Startup(object? sender, ControlledApplicationLifetimeStartupEventArgs e)
    {
        _setupFinished.SetResult();
    }

    private void UIThread_UnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Debug.Fail("Exception on UI thread!", e.Exception.ToString());
        e.Handled = true;
    }
}