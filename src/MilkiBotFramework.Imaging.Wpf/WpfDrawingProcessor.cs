using System.IO;
using System.Windows;
using MilkiBotFramework.Imaging.Wpf.Internal;
using Image = SixLabors.ImageSharp.Image;

namespace MilkiBotFramework.Imaging.Wpf;

// How to: Encode and Decode a GIF Image:
// https://docs.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/how-to-encode-and-decode-a-gif-image?view=netframeworkdesktop-4.8&viewFallbackFrom=netdesktop-6.0
public class WpfDrawingProcessor<TViewModel, TProcessControl> : IDrawingProcessor<TViewModel>
    where TViewModel : class
    where TProcessControl : WpfDrawingControl
{
    private readonly Func<Image?, TViewModel, WpfDrawingControl>? _templateControlCreation;
    private readonly Type? _type;
    //private readonly TimeSpan _delayTime = TimeSpan.FromMilliseconds(500);

    public WpfDrawingProcessor()
    {
        _type = typeof(TProcessControl);
    }

    public WpfDrawingProcessor(Func<Image?, TViewModel, WpfDrawingControl> templateControlCreation)
    {
        _templateControlCreation = templateControlCreation;
    }

    public async Task<Image> ProcessAsync(TViewModel viewModel, Image? sourceImage = null)
    {
        await UiThreadHelper.EnsureUiThreadAsync();
        MemoryStream? retStream = null;

        await Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            var subProcessor = CreateControlInstance(sourceImage, viewModel);
            var window = new HiddenWindow { Content = new DpiDecorator { Child = subProcessor } };

            var tcs = new TaskCompletionSource();
            subProcessor.RenderFinished += async (_, _) =>
            {
                await window.WaitForShown();
                //await Task.Delay(_delayTime); // Todo: Needs to delay?

                retStream = await subProcessor.ProcessOnceAsync();
                tcs.SetResult();
            };
            window.Show();

            await tcs.Task;
            window.Close();
        });

        if (retStream == null)
        {
            throw new ArgumentException("The DrawingProcessControl returns empty results.");
        }

        try
        {
            return await Image.LoadAsync(retStream);
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

        await Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            var subProcessor = CreateControlInstance(sourceImage, viewModel);
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
        });

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
            ? (WpfDrawingControl)Activator.CreateInstance(_type!, sourceImage, model)!
            : _templateControlCreation(sourceImage, model);
    }
}