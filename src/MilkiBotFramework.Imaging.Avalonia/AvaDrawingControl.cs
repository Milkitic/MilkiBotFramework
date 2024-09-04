using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using MilkiBotFramework.Imaging.Avalonia.Internal;
using Image = SixLabors.ImageSharp.Image;

namespace MilkiBotFramework.Imaging.Avalonia;

public abstract class AvaDrawingControl : UserControl
{
    internal event RenderFinishDelegate? RenderFinished;

    protected readonly Image? SourceImage;
    protected readonly Bitmap? SourceBitmapImage;
    protected readonly object ViewModel;
    private readonly TaskCompletionSource _tcs;

    public AvaDrawingControl(object viewModel, Image? sourceImage = null)
    {
        SourceImage = sourceImage;
        DataContext = ViewModel = viewModel;
        if (sourceImage != null)
            SourceBitmapImage = AvaImageHelper.GetBitmapImageFromImageSharp(sourceImage);
        _tcs = new TaskCompletionSource();
        RenderFinished += (_, _) =>
        {
            _tcs.SetResult();
            return Task.CompletedTask;
        };
    }

    public virtual Task<MemoryStream> ProcessOnceAsync()
    {
        var scaling = GetScaling();
        var visual = GetDrawingVisual(out var size);

        if (scaling == 0)
            throw new Exception("The DPI cannot be zero.");
        if (double.IsNaN(scaling))
            throw new Exception("The DPI cannot be NaN.");
        if (size.Width == 0 || size.Height == 0)
            throw new Exception("The size cannot be zero.");
        if (double.IsNaN(size.Width) || double.IsNaN(size.Height))
            throw new Exception("The size cannot be NaN.");

        var pixelSize = new PixelSize((int)(size.Width * scaling), (int)(size.Height * scaling));
        var dpi = new Vector(96 * scaling, 96 * scaling);

        var stream = new MemoryStream();
        using (var renderBitmap = new RenderTargetBitmap(pixelSize, dpi))
        {
            renderBitmap.Render(visual);
            renderBitmap.Save(stream);
        }

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
        size = new Size(Bounds.Width, Bounds.Height);
        return this;
    }

    protected async Task FinishDrawing()
    {
        if (RenderFinished != null)
            await RenderFinished.Invoke(this, EventArgs.Empty);
    }

    private double GetScaling()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is WindowBase window)
        {
            var screenFromVisual = window.Screens.ScreenFromVisual(this);
            if (screenFromVisual != null)
            {
                return screenFromVisual.Scaling;
            }
        }

        return 1;
    }
}