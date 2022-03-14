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
                try
                {
                    Application.Run();
                }
                catch (Exception ex)
                {
                    Debug.Fail("UI线程异常", ex.ToString());
                    Application.Shutdown();
                }
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