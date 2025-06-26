using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using MilkiBotFramework.Imaging.Avalonia.Internal;
using Image = SixLabors.ImageSharp.Image;

namespace MilkiBotFramework.Imaging.Avalonia;

public abstract class AvaRenderingControl<TViewModel> : AvaRenderingControl where TViewModel : class
{
    public static readonly StyledProperty<TViewModel?> ViewModelProperty =
        AvaloniaProperty.Register<AvaRenderingControl, TViewModel?>(nameof(ViewModel));

    public TViewModel? ViewModel
    {
        get => GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == DataContextProperty)
        {
            if (e.NewValue is TViewModel viewModel)
            {
                ViewModel = viewModel;
            }
            else
            {
                ViewModel = default;
            }

            OnViewModelChanged(e.OldValue as TViewModel, e.NewValue as TViewModel);
        }
    }

    protected virtual void OnViewModelChanged(TViewModel? oldValue, TViewModel? newValue)
    {
    }
}

public abstract class AvaRenderingControl : UserControl
{
    internal event RenderFinishDelegate? RenderFinished;

    public static readonly StyledProperty<Bitmap?> SourceBitmapProperty =
        AvaloniaProperty.Register<AvaRenderingControl, Bitmap?>(nameof(SourceBitmap));

    public Bitmap? SourceBitmap
    {
        get => GetValue(SourceBitmapProperty);
        set => SetValue(SourceBitmapProperty, value);
    }

    public static readonly StyledProperty<string?> LocaleProperty =
        AvaloniaProperty.Register<AvaRenderingControl, string?>(nameof(Locale), "zh-CN");

    public string? Locale
    {
        get => GetValue(LocaleProperty);
        set => SetValue(LocaleProperty, value);
    }

    private readonly TaskCompletionSource _tcs;
    private readonly Image? _sourceImage;
    private readonly Timer _timer;

    public AvaRenderingControl(/*object viewModel, Image? sourceImage = null*/)
    {
        _tcs = new TaskCompletionSource();
        // ReSharper disable once AsyncVoidLambda
        _timer = new Timer(async _ =>
        {
            await FinishRender();
            _timer?.Dispose();
        }, null, TimeSpan.FromSeconds(10), Timeout.InfiniteTimeSpan);

        Loaded += AvaRenderingControl_Loaded;
        RenderFinished += (_, _) =>
        {
            _tcs.TrySetResult();
            return Task.CompletedTask;
        };
        // SubpixelAntialias needs opaque background, and only on windows
        // https://github.com/AvaloniaUI/Avalonia/issues/2464
        RenderOptions.SetTextRenderingMode(this, TextRenderingMode.Antialias);
    }

    public Image? SourceImage
    {
        protected get => _sourceImage;
        init
        {
            _sourceImage = value;
            if (value != null)
                SourceBitmap = AvaImageHelper.GetBitmapImageFromImageSharp(value);
        }
    }

    public Task DrawingTask => _tcs.Task;

    public virtual Task<RenderResult> ProcessOnceAsync()
    {
        var scaling = GetScaling();
        var visual = GetDrawingVisual(out var size);

        ValidateParameters(scaling, size);

        // 计算渲染参数
        var (pixelSize, dpi) = CalculateRenderParameters(scaling, size);

        // 执行渲染
        using var renderBitmap = RenderVisual(visual, pixelSize, dpi);

        // 获取像素数据
        var buffer = GetPixelBuffer(renderBitmap, out var length);

        return Task.FromResult(new RenderResult(
            buffer,
            length,
            pixelSize,
            renderBitmap.Format ?? throw new InvalidOperationException("Invalid pixel format")));
    }

#pragma warning disable CS1998
    public virtual async IAsyncEnumerable<MemoryStream> ProcessMultiFramesAsync()
#pragma warning restore CS1998
    {
        yield break;
    }

    public async Task FinishRender()
    {
        await _timer.DisposeAsync();
        if (RenderFinished != null)
            await RenderFinished.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == LocaleProperty)
        {
            var fontFamily = LocalFontManager.Instance.GetFontFamily(e.NewValue as string);
            Resources["DefaultFonts"] = fontFamily;
        }
    }

    protected internal virtual Visual GetDrawingVisual(out Size size)
    {
        size = new Size(Bounds.Width, Bounds.Height);
        return this;
    }

    private void AvaRenderingControl_Loaded(object? sender, RoutedEventArgs e)
    {
        var fontFamily = LocalFontManager.Instance.GetFontFamily(Locale);
        Resources["DefaultFonts"] = fontFamily;
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

    private static void ValidateParameters(double scaling, Size size)
    {
        if (scaling is <= 0 or double.NaN)
        {
            throw new ArgumentOutOfRangeException(nameof(scaling),
                $"Invalid scaling value: {scaling}. Must be positive number.");
        }

        if (size.Width <= 0 || size.Height <= 0 || double.IsNaN(size.Width) || double.IsNaN(size.Height))
        {
            throw new ArgumentException(
                $"Invalid size: {size.Width}x{size.Height}. Must be positive values.");
        }
    }

    private static (PixelSize pixelSize, Vector dpi) CalculateRenderParameters(double scaling, Size size)
    {
        var width = (int)Math.Round(size.Width * scaling);
        var height = (int)Math.Round(size.Height * scaling);

        return (new PixelSize(width, height), new Vector(96 * scaling, 96 * scaling));
    }

    private static RenderTargetBitmap RenderVisual(Visual visual, PixelSize pixelSize, Vector dpi)
    {
        var renderBitmap = new RenderTargetBitmap(pixelSize, dpi);
        renderBitmap.Render(visual);
        return renderBitmap;
    }

    private static byte[] GetPixelBuffer(RenderTargetBitmap bitmap, out int length)
    {
        var format = bitmap.Format ?? PixelFormats.Bgra8888;
        int bytesPerPixel = (format.BitsPerPixel + 7) / 8; // 处理非8整除的情况
        int stride = bitmap.PixelSize.Width * bytesPerPixel;
        length = stride * bitmap.PixelSize.Height;

        var buffer = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            CopyPixelsUnsafe(bitmap, buffer, bytesPerPixel);
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(buffer);
            throw;
        }

        return buffer;
    }

    private static unsafe void CopyPixelsUnsafe(RenderTargetBitmap bitmap, byte[] buffer, int bytesPerPixel)
    {
        fixed (byte* ptr = buffer)
        {
            var sourceRect = new PixelRect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height);
            bitmap.CopyPixels(sourceRect, (IntPtr)ptr, buffer.Length, bitmap.PixelSize.Width * bytesPerPixel);
        }
    }
}