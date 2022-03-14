using System.Diagnostics;
using System.Windows;
using MilkiBotFramework.Utils;

namespace MilkiBotFramework.Imaging.Wpf.Internal;

internal static class UiThreadHelper
{
    private static Thread? _uiThread;
    internal static Application? Application;
    private static readonly AsyncLock _asyncLock = new();
    private static readonly TaskCompletionSource<bool> WaitComplete = new();

    internal static async Task EnsureUiThreadAsync()
    {
        using (await _asyncLock.LockAsync())
        {
            if (_uiThread is { IsAlive: true })
            {
                return;
            }

            _uiThread = new Thread(() =>
            {
                Application = new Application
                {
                    ShutdownMode = ShutdownMode.OnExplicitShutdown
                };

                Application.Startup += (_, _) => WaitComplete.SetResult(true);
                Application.DispatcherUnhandledException += (_, e) =>
                {
                    Debug.Fail("UI线程异常", e.Exception.ToString());
                    e.Handled = true;
                };

                for (int i = 0; i < 10; i++)
                {
                    try
                    {
                        Application.Run();
                    }
                    catch
                    {
                        Application.Shutdown();
                    }
                }

                throw new Exception("UI thread failing for too much times.");
            })
            {
                IsBackground = true
            };
            _uiThread.SetApartmentState(ApartmentState.STA);
            _uiThread.Start();
            await WaitComplete.Task;
        }
    }
}