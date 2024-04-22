using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Headless;
using MilkiBotFramework.Utils;

namespace MilkiBotFramework.Imaging.Avalonia.Internal;

public static class UiThreadHelper
{
    private static Thread? _uiThread;
    internal static AvaApp? Application;
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
                Application = GetApplicationFunc == null
                    ? new AvaApp(WaitComplete)
                    : GetApplicationFunc(WaitComplete);
                for (int i = 0; i < 10; i++)
                {
                    try
                    {
                        AppBuilder.Configure(() => Application)
                            .UseSkia() // enable Skia renderer
                            .UseHeadless(new AvaloniaHeadlessPlatformOptions
                            {
                                UseHeadlessDrawing = false // disable headless drawing
                            })
                            .StartWithClassicDesktopLifetime(Array.Empty<string>());
                        return;
                    }
                    catch (Exception ex)
                    {
                        Debug.Fail("UI线程异常且不可恢复", ex.ToString());
                    }
                }

                throw new Exception("UI thread failing for too much times.");
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