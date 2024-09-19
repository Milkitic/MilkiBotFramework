using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using MilkiBotFramework.Imaging.Avalonia.Internal;
using Image = SixLabors.ImageSharp.Image;

namespace MilkiBotFramework.Imaging.Avalonia;

public abstract class AvaRenderingControl<TViewModel> : AvaRenderingControl
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
        }
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

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == LocaleProperty)
        {
            Resources["DefaultFonts"] = LocalFontManager.Instance.GetFontFamily(e.NewValue as string);
        }
    }

    protected internal virtual Visual GetDrawingVisual(out Size size)
    {
        size = new Size(Bounds.Width, Bounds.Height);
        return this;
    }

    protected async Task FinishRender()
    {
        await _timer.DisposeAsync();
        if (RenderFinished != null)
            await RenderFinished.Invoke(this, EventArgs.Empty);
    }

    private void AvaRenderingControl_Loaded(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        Resources["DefaultFonts"] = LocalFontManager.Instance.GetFontFamily(Locale as string);
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