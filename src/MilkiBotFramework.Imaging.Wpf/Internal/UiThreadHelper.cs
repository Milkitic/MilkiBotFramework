using System.Diagnostics;
using System.Windows;

namespace MilkiBotFramework.Imaging.Wpf.Internal;

internal static class UiThreadHelper
{
    private static Thread? _uiThread;
    internal static Application? Application;
    private static readonly ReaderWriterLockSlim UiThreadCheckLock = new();
    private static readonly TaskCompletionSource<bool> WaitComplete = new();

    internal static async Task EnsureUiThreadAsync()
    {
        try
        {
            UiThreadCheckLock.EnterReadLock();
            if (_uiThread is { IsAlive: true })
            {
                return;
            }
        }
        finally
        {
            UiThreadCheckLock.ExitReadLock();
        }

        UiThreadCheckLock.EnterWriteLock();
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
        UiThreadCheckLock.ExitWriteLock();
    }
}