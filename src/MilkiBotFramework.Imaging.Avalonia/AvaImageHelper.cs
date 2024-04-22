using System;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace MilkiBotFramework.Imaging.Avalonia;

internal static class AvaImageHelper
{
    public static Bitmap GetBitmapImageFromImageSharp(Image image)
    {
        //var dpi = new Vector(image.Metadata.HorizontalResolution, image.Metadata.VerticalResolution);
        var bitmap = FillFrame(image, image.PixelType.BitsPerPixel);
        return bitmap;
    }

    private static unsafe Bitmap FillFrame(Image imageFrame, int bitsPerPixel)
    {
        var pixelSize = new PixelSize(imageFrame.Width, imageFrame.Height);
        //var pixels = pixelSize.Width * pixelSize.Height;
        int pixelLength = bitsPerPixel / 8;
        //var bytes = pixels * pixelLength;

        PixelFormat pixelFormat;
        void* pointer;

        if (imageFrame is Image<Rgba64> rgba64)
        {
            pixelFormat = PixelFormats.Rgba64;
            pointer = GetFrameAddress(rgba64);
        }
        else if (imageFrame is Image<Rgba32> rgba32)
        {
            pixelFormat = PixelFormats.Rgba8888;
            pointer = GetFrameAddress(rgba32);
        }
        else if (imageFrame is Image<Bgra32> bgra32)
        {
            pixelFormat = PixelFormats.Bgra8888;
            pointer = GetFrameAddress(bgra32);
        }
        else if (imageFrame is Image<Bgr24> bgr24)
        {
            pixelFormat = PixelFormats.Bgr24;
            pointer = GetFrameAddress(bgr24);
        }
        else if (imageFrame is Image<Rgb24> rgb24)
        {
            pixelFormat = PixelFormats.Rgb24;
            pointer = GetFrameAddress(rgb24);
        }
        else
        {
            throw new NotSupportedException();
        }

        var bitmap = new Bitmap(pixelFormat, AlphaFormat.Premul,
            new nint(pointer), pixelSize, new Vector(96, 96), pixelLength * imageFrame.Width);
        return bitmap;
    }

    private static unsafe void* GetFrameAddress<TPixel>(Image<TPixel> frame) where TPixel : unmanaged, IPixel<TPixel>
    {
        if (frame.DangerousTryGetSinglePixelMemory(out var buffer))
        {
            fixed (void* p = &buffer.Span.GetPinnableReference())
            {
                return p;
            }
        }

        throw new NotImplementedException();
    }
}