using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using MilkiBotFramework.Imaging.Avalonia.Internal;
using SixLabors.ImageSharp.PixelFormats;
using Image = SixLabors.ImageSharp.Image;
using Size = Avalonia.Size;

namespace MilkiBotFramework.Imaging.Avalonia;

public enum RenderingMode
{
    InMemory, InMemoryWithWindow, Headless,
}

public class AvaRenderingProcessor<TProcessControl> : IDrawingProcessor
    where TProcessControl : AvaRenderingControl
{
    private readonly RenderingMode _renderingMode;
    private readonly Type? _type;
    private readonly Func<object, Image?, AvaRenderingControl>? _templateControlCreation;

    public AvaRenderingProcessor() : this(RenderingMode.InMemory)
    {
    }

    public AvaRenderingProcessor(RenderingMode renderingMode)
    {
        _type = typeof(TProcessControl);
        _renderingMode = renderingMode;
    }

    public AvaRenderingProcessor(Func<object, Image?, AvaRenderingControl> templateControlCreation,
        RenderingMode renderingMode = RenderingMode.InMemory)
    {
        _templateControlCreation = templateControlCreation;
        _renderingMode = renderingMode;
    }

    public async Task<Image> ProcessAsync(object viewModel, string locale = "en-US", Image? sourceImage = null)
    {
        await UiThreadHelper.EnsureUiThreadAsync();
        RenderResult? renderResult = default;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var tcsOuter = new TaskCompletionSource(cts);
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var subProcessor = CreateControlInstance(sourceImage, viewModel, locale);
            if (double.IsNaN(subProcessor.Width) || double.IsNaN(subProcessor.Height) || _renderingMode != RenderingMode.InMemory)
            {
                var window = new DrawingWindow { Content = new DpiDecorator { Child = subProcessor } };

                if (!double.IsNaN(subProcessor.Width)) window.Width = subProcessor.Width;
                if (!double.IsNaN(subProcessor.Height)) window.Height = subProcessor.Height;

                //if (subProcessor.Content is LayoutTransformControl
                //    {
                //        LayoutTransform: ScaleTransform scaleTransform,
                //        Child: { } child
                //    } && !double.IsNaN(child.Width) && !double.IsNaN(child.Height))
                //{
                //    child.Measure(new Size());
                //    child.Arrange(new Rect(0, 0, 0, 0));
                //    var bounds = child.Bounds;
                //    window.SizeToContent = SizeToContent.Manual;
                //    window.Width = bounds.Width * scaleTransform.ScaleX;
                //    window.Height = bounds.Height * scaleTransform.ScaleY;
                //}

                window.Show();
                await subProcessor.DrawingTask;
                await window.WaitForShown();

                if (_renderingMode != RenderingMode.Headless)
                {
                    renderResult = await subProcessor.ProcessOnceAsync();
                }
                else
                {
                    var PART_Content = subProcessor.Find<Control>("PART_Content");
                    if (PART_Content is not null)
                    {
                        window.SizeToContent = SizeToContent.Manual;
                        window.Width = PART_Content.Bounds.Width;
                        window.Height = PART_Content.Bounds.Height;
                        await Task.Delay(100);
                    }

                    var renderBitmap = window.CaptureRenderedFrame();
                    if (renderBitmap != null)
                    {
                        var pixelSize = renderBitmap.PixelSize;
                        var pixelFormat = renderBitmap.Format!.Value;
                        var bpp = pixelFormat.BitsPerPixel / 8;
                        var length = pixelSize.Width * pixelSize.Height * bpp;

                        var rentByte = ArrayPool<byte>.Shared.Rent(length);
                        GetRawBytes(rentByte, renderBitmap, bpp);
                        renderResult = new RenderResult(rentByte, length, pixelSize, pixelFormat);
                    }
                    else
                    {
                        Console.WriteLine("Warn: Window.CaptureRenderedFrame() failed.");
                        renderResult = await subProcessor.ProcessOnceAsync(); // fallback
                    }
                }


                window.Close();
            }
            else
            {
                var window = new DrawingWindow { Content = new DpiDecorator { Child = subProcessor } };

                var size = new Size(subProcessor.Width, subProcessor.Height);
                subProcessor.Measure(size);
                subProcessor.Arrange(new Rect(size));
                subProcessor.UpdateLayout();
                await Task.Delay(1);
                await subProcessor.FinishRender();
                await subProcessor.DrawingTask;
                renderResult = await subProcessor.ProcessOnceAsync();
                window.Content = null;
            }

            tcsOuter.SetResult();
        });

        await tcsOuter.Task;
        if (renderResult == null)
        {
            throw new ArgumentException("The DrawingProcessControl returns empty results.");
        }

        try
        {
            var result = renderResult.Value;
            if (result.PixelFormat == PixelFormat.Bgra8888)
            {
                return Image.LoadPixelData<Bgra32>(
                    result.Buffer.AsSpan(0, result.Length),
                    result.PixelSize.Width,
                    result.PixelSize.Height);
            }

            if (result.PixelFormat == PixelFormat.Rgba8888)
            {
                return Image.LoadPixelData<Rgba32>(
                    result.Buffer.AsSpan(0, result.Length),
                    result.PixelSize.Width,
                    result.PixelSize.Height);
            }

            else throw new NotSupportedException($"Unsupported pixel format: {result.PixelFormat}");
        }
        finally
        {
            renderResult?.Dispose();
        }
    }

    public async Task<Image> ProcessGifAsync(object viewModel, TimeSpan interval, string locale = "en-US", Image? sourceImage = null, bool repeat = true)
    {
        await UiThreadHelper.EnsureUiThreadAsync();
        var retStreams = new List<MemoryStream>();
        var size = SixLabors.ImageSharp.Size.Empty;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var tcsOuter = new TaskCompletionSource(cts);
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var subProcessor = CreateControlInstance(sourceImage, viewModel, locale);
            if (_renderingMode == RenderingMode.InMemory)
            {
                // Not supported
            }

            var window = new DrawingWindow { Content = new DpiDecorator { Child = subProcessor } };

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

    private AvaRenderingControl CreateControlInstance(Image? sourceImage, object model, string locale)
    {
        if (_templateControlCreation != null) return _templateControlCreation(model, sourceImage);

        var type = _type!;
        var avaDrawingControl = (AvaRenderingControl)Activator.CreateInstance(type)!;
        var propImg = type.GetProperty(nameof(AvaRenderingControl.SourceImage));
        var propCtx = type.GetProperty(nameof(AvaRenderingControl.DataContext));
        var propLocale = type.GetProperty(nameof(AvaRenderingControl.Locale));

        propImg?.SetValue(avaDrawingControl, sourceImage);
        propCtx?.SetValue(avaDrawingControl, model);
        propLocale?.SetValue(avaDrawingControl, locale);

        return avaDrawingControl;
    }

    private static unsafe void GetRawBytes(byte[] buffer, Bitmap bitmap, int bpp)
    {
        fixed (byte* pointer = buffer)
        {
            var sourceRect = new PixelRect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height);
            bitmap.CopyPixels(sourceRect, (IntPtr)pointer, buffer.Length, bitmap.PixelSize.Width * bpp);
        }
    }
}