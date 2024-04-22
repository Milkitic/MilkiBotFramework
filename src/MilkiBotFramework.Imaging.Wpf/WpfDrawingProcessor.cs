using System.IO;
using System.Windows;
using MilkiBotFramework.Imaging.Wpf.Internal;
using SixLabors.ImageSharp.Formats.Png;
using Image = SixLabors.ImageSharp.Image;

namespace MilkiBotFramework.Imaging.Wpf;

// How to: Encode and Decode a GIF Image:
// https://docs.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/how-to-encode-and-decode-a-gif-image?view=netframeworkdesktop-4.8&viewFallbackFrom=netdesktop-6.0
public class WpfDrawingProcessor<TViewModel, TProcessControl> : IDrawingProcessor<TViewModel>
    where TViewModel : class
    where TProcessControl : WpfDrawingControl
{
    private readonly bool _enableWindowRendering;
    private readonly Func<TViewModel, Image?, WpfDrawingControl>? _templateControlCreation;
    private readonly Type? _type;

    public WpfDrawingProcessor(bool enableWindowRendering = false)
    {
        _type = typeof(TProcessControl);
        _enableWindowRendering = enableWindowRendering;
    }

    public WpfDrawingProcessor(Func<TViewModel, Image?, WpfDrawingControl> templateControlCreation,
        bool enableWindowRendering = false)
    {
        _templateControlCreation = templateControlCreation;
        _enableWindowRendering = enableWindowRendering;
    }

    public async Task<Image> ProcessAsync(TViewModel viewModel, Image? sourceImage = null)
    {
        await UiThreadHelper.EnsureUiThreadAsync();
        MemoryStream? retStream = null;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var tcsOuter = new TaskCompletionSource(cts);
        await Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            var subProcessor = CreateControlInstance(sourceImage, viewModel);
            if (double.IsNaN(subProcessor.Width) || double.IsNaN(subProcessor.Height) || _enableWindowRendering)
            {
                var window = new HiddenWindow { Content = new DpiDecorator { Child = subProcessor } };
                window.Show();
                await subProcessor.GetDrawingTask();
                await window.WaitForShown();
                retStream = await subProcessor.ProcessOnceAsync();
                window.Close();
            }
            else
            {
                var size = new Size(subProcessor.Width, subProcessor.Height);
                subProcessor.Measure(size);
                subProcessor.Arrange(new Rect(size));
                subProcessor.UpdateLayout();
                await subProcessor.GetDrawingTask();
                retStream = await subProcessor.ProcessOnceAsync();
            }

            tcsOuter.SetResult();
        });

        await tcsOuter.Task;
        if (retStream == null)
        {
            throw new ArgumentException("The DrawingProcessControl returns empty results.");
        }

        try
        {
            return await PngDecoder.Instance.DecodeAsync(new PngDecoderOptions(), retStream);
        }
        finally
        {
            await retStream.DisposeAsync();
        }
    }

    public async Task<Image> ProcessGifAsync(TViewModel viewModel, TimeSpan interval, Image? sourceImage = null,
        bool repeat = true)
    {
        await UiThreadHelper.EnsureUiThreadAsync();
        var retStreams = new List<MemoryStream>();
        var size = SixLabors.ImageSharp.Size.Empty;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var tcsOuter = new TaskCompletionSource(cts);
        await Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            var subProcessor = CreateControlInstance(sourceImage, viewModel);
            if (!_enableWindowRendering)
            {
                // Not supported
            }

            var window = new HiddenWindow { Content = new DpiDecorator { Child = subProcessor } };

            var tcs = new TaskCompletionSource();
            subProcessor.RenderFinished += async (_, _) =>
            {
                await window.WaitForShown();
                //await Task.Delay(_delayTime); // Todo: Needs to delay?

                try
                {
                    _ = subProcessor.GetDrawingVisual(out var sizeD);
                    size = new SixLabors.ImageSharp.Size((int)sizeD.Width, (int)sizeD.Height);

                    await foreach (var retStream in subProcessor.ProcessMultiFramesAsync())
                    {
                        retStreams.Add(retStream);
                    }
                }
                catch
                {
                    foreach (var memoryStream in retStreams)
                    {
                        await memoryStream.DisposeAsync();
                    }

                    throw;
                }

                tcs.SetResult();
            };
            window.Show();

            await tcs.Task;
            window.Close();
            tcsOuter.SetResult();
        });

        await tcsOuter.Task;
        if (retStreams.Count == 0)
        {
            throw new ArgumentException("The DrawingProcessControl returns empty results.");
        }

        try
        {
            var images = retStreams.Select(Image.Load);
            var image = await ImageHelper.CreateGifByImagesAsync(images, interval, size);
            return image;
        }
        finally
        {
            foreach (var memoryStream in retStreams)
            {
                await memoryStream.DisposeAsync();
            }
        }
    }

    private WpfDrawingControl CreateControlInstance(Image? sourceImage, TViewModel model)
    {
        return _templateControlCreation == null
            ? (WpfDrawingControl)Activator.CreateInstance(_type!, new object?[] { model, sourceImage })!
            : _templateControlCreation(model, sourceImage);
    }
}