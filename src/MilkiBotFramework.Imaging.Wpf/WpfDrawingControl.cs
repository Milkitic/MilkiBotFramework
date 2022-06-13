using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MilkiBotFramework.Imaging.Wpf.Internal;
using Image = SixLabors.ImageSharp.Image;

namespace MilkiBotFramework.Imaging.Wpf;

public abstract class WpfDrawingControl : UserControl
{
    internal event RenderFinishDelegate? RenderFinished;

    protected readonly Image? SourceImage;
    protected readonly BitmapSource? SourceBitmapImage;
    protected readonly object ViewModel;
    private readonly TaskCompletionSource _tcs;

    public WpfDrawingControl(object viewModel, Image? sourceImage = null)
    {
        SourceImage = sourceImage;
        DataContext = ViewModel = viewModel;
        if (sourceImage != null)
            SourceBitmapImage = WpfImageHelper.GetBitmapImageFromImageSharp(sourceImage);
        _tcs = new TaskCompletionSource();
        RenderFinished += (_, _) =>
        {
            _tcs.SetResult();
            return Task.CompletedTask;
        };
    }

    public virtual Task<MemoryStream> ProcessOnceAsync()
    {
        var dpi = GetDpi();
        var visual = GetDrawingVisual(out var size);

        if (dpi.X == 0 || dpi.Y == 0)
            throw new Exception("The DPI cannot be zero.");
        if (double.IsNaN(dpi.X) || double.IsNaN(dpi.Y))
            throw new Exception("The DPI cannot be NaN.");
        if (size.Width == 0 || size.Height == 0)
            throw new Exception("The size cannot be zero.");
        if (double.IsNaN(size.Width) || double.IsNaN(size.Height))
            throw new Exception("The size cannot be NaN.");

        var bmp = new RenderTargetBitmap(
            (int)(size.Width * dpi.X / 96), (int)(size.Height * dpi.Y / 96),
            dpi.X, dpi.Y, PixelFormats.Pbgra32
        );

        bmp.Render(visual);

        var stream = new MemoryStream();
        BitmapEncoder encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bmp));
        encoder.Save(stream);
        stream.Position = 0;
        return Task.FromResult(stream);
    }

#pragma warning disable CS1998
    public virtual async IAsyncEnumerable<MemoryStream> ProcessMultiFramesAsync()
#pragma warning restore CS1998
    {
        yield break;
    }

    public Task GetDrawingTask()
    {
        return _tcs.Task;
    }

    protected internal virtual Visual GetDrawingVisual(out Size size)
    {
        size = new Size(ActualWidth, ActualHeight);
        return this;
    }

    protected async Task FinishDrawing()
    {
        if (RenderFinished != null)
            await RenderFinished.Invoke(this, EventArgs.Empty);
    }

    private Point GetDpi()
    {
        var source = PresentationSource.FromVisual(this);

        double dpiX = 96.0, dpiY = 96.0;
        if (source is { CompositionTarget: { } })
        {
            dpiX = 96.0 * source.CompositionTarget.TransformToDevice.M11;
            dpiY = 96.0 * source.CompositionTarget.TransformToDevice.M22;
        }

        return new Point(dpiX, dpiY);
    }
}