using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using Avalonia.Vulkan;
using MilkiBotFramework.Imaging.Avalonia;
using MilkiBotFramework.Imaging.Avalonia.Internal;
using SixLabors.ImageSharp.Formats.Png;

namespace AvaHeadlessTest;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
internal class Program
{
    public static async Task Main(string[] args)
    {
        //BuildAvaloniaApp()
        //    .StartWithClassicDesktopLifetime(args);
        try
        {
            AvaloniaOptions.GetApplicationFunc = taskCompletionSource => new App(taskCompletionSource);
            AvaloniaOptions.CustomConfigureFunc = k => k           .With(new Win32PlatformOptions
                {
                    RenderingMode = new []
                    {
                        Win32RenderingMode.Vulkan
                    }
                })
                .With(new X11PlatformOptions(){RenderingMode =new[] { X11RenderingMode.Vulkan } })
                .With(new VulkanOptions()
                {
                    VulkanInstanceCreationOptions = new VulkanInstanceCreationOptions()
                    {
                        UseDebug = true
                    }
                }).WithInterFont();
            var vm = new AvaTestViewModel();
            var processor = new AvaRenderingProcessor<AvaTestControl>(true);
            var sb = await processor.ProcessAsync(vm);
            Console.WriteLine(sb.Bounds);
            Directory.CreateDirectory("output");
            await using var fs = File.Create("output/file.png");
            //await using var fs = new MemoryStream();
            //for (int i = 0; i < 100000; i++)
            {
                await sb.SaveAsync(fs, new PngEncoder());
                //fs.Position = 0;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }

        //return;
        //await TestRaw();
    }

    private static async Task TestRaw()
    {
        await UiThreadHelper.EnsureUiThreadAsync();
        Console.WriteLine("Load finished");
        Dispatcher.UIThread.Invoke(() =>
        {
            var button = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Content = "Hello world!"
            };

            var window = new Window
            {
                Width = 100,
                Height = 100,
                Content = button
            };

            window.Loaded += Window_Loaded;
            window.Show();
        });
        Console.ReadLine();
    }

    private static void Window_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var window = (Window)sender!;

        var frame = window.CaptureRenderedFrame();
        frame?.Save("file.png");
        window.Close();
    }

    // For avalonia preview
    public static AppBuilder BuildAvaloniaApp()
    {
        var waitComplete = new TaskCompletionSource();
        return AppBuilder.Configure(() => new App(waitComplete))
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
    }
}