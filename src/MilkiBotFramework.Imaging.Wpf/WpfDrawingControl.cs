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

    public WpfDrawingControl(object viewModel, Image? sourceImage = null)
    {
        SourceImage = sourceImage;
        DataContext = ViewModel = viewModel;
        if (sourceImage != null)
            SourceBitmapImage = WpfImageHelper.GetBitmapImageFromImageSharp(sourceImage);
    }

    public virtual Task<MemoryStream> ProcessOnceAsync()
    {
        var dpi = GetDpi();
        var visual = GetDrawingVisual(out var size);

        var bmp = new RenderTargetBitmap(
            (int)(size.Width * dpi.X / 96), (int)(size.Height * dpi.Y / 96),
            dpi.X, dpi.Y, PixelFormats.Pbgra32
        );

        bmp.Render(visual);

        var stream = new MemoryStream();
        BitmapEncoder encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bmp));
        encoder.Save(stream);
        return Task.FromResult(stream);
    }

    public virtual async IAsyncEnumerable<MemoryStream> ProcessMultiFramesAsync()
    {
        yield break;
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

        double dpiX = 0, dpiY = 0;
        if (source is { CompositionTarget: { } })
        {
            dpiX = 96.0 * source.CompositionTarget.TransformToDevice.M11;
            dpiY = 96.0 * source.CompositionTarget.TransformToDevice.M22;
        }

        return new Point(dpiX, dpiY);
    }
}