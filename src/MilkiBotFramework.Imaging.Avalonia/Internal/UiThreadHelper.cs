using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Headless;
using MilkiBotFramework.Utils;

namespace MilkiBotFramework.Imaging.Avalonia.Internal;

internal static class UiThreadHelper
{
    private static Thread? _uiThread;
    private static readonly AsyncLock AsyncLock = new();
    private static readonly TaskCompletionSource WaitComplete = new();

    // ReSharper disable once InconsistentNaming
    public static Func<TaskCompletionSource, AvaApp>? GetApplicationFunc;

    public static async Task EnsureUiThreadAsync()
    {
        using (await AsyncLock.LockAsync())
        {
            if (_uiThread is { IsAlive: true })
            {
                return;
            }

            _uiThread = new Thread(() =>
            {
                var appBuilder = AppBuilder.Configure(() => GetApplicationFunc == null
                       ? new AvaApp(WaitComplete)
                       : GetApplicationFunc(WaitComplete))
                   .UseSkia() // enable Skia renderer
                   .UseHeadless(new AvaloniaHeadlessPlatformOptions
                   {
                       UseHeadlessDrawing = false // disable headless drawing
                   });

                try
                {
                    appBuilder.StartWithClassicDesktopLifetime(Array.Empty<string>());
                }
                catch (Exception ex)
                {
                    Debug.Fail("UI线程异常且不可恢复", ex.ToString());
                    throw new Exception("UI thread shutdown.", ex);
                }
            })
            {
                IsBackground = true
            };
            if (OperatingSystem.IsWindows()) _uiThread.SetApartmentState(ApartmentState.STA);
            _uiThread.Start();
            await WaitComplete.Task;
        }
    }
}