using System.Numerics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace MilkiBotFramework.Imaging;

public static class ImageHelper
{

    private static readonly Dictionary<ImageType, Memory<byte>> KnownFileHeaders = new()
    {
        { ImageType.Jpeg, new byte[] { 0xFF, 0xD8 } }, // JPEG
        { ImageType.Bmp, new byte[] { 0x42, 0x4D } }, // BMP
        { ImageType.Gif, new byte[] { 0x47, 0x49, 0x46 } }, // GIF
        { ImageType.Png, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } }, // PNG
        //{ ImageType.Pdf, new byte[]{ 0x25, 0x50, 0x44, 0x46 }} // PDF
    };

    public static ImageType GetKnownImageType(ReadOnlySpan<byte> data)
    {
        foreach (var check in KnownFileHeaders)
        {
            if (data.Length >= check.Value.Length)
            {
                var slice = data.Slice(0, check.Value.Length);
                if (slice.SequenceEqual(check.Value.Span))
                {
                    return check.Key;
                }
            }
        }

        return ImageType.Unknown;
    }

    public static Image GetResizedImage(Image source, float uniformScaleRate)
    {
        var width = source.Width * uniformScaleRate;
        var height = source.Height * uniformScaleRate;
        var returnBitmap = source
            .Clone(k => k
                .AutoOrient()
                .Resize((int)width, (int)height, KnownResamplers.Bicubic)
            );

        return returnBitmap;
    }

    public static Image GetRotatedImage(Image source, float angle, bool crop = false, Size resize = default)
    {
        if (resize == Size.Empty) resize = source.Size();

        var returnBitmap = source
            .Clone(k => k
                .AutoOrient()
                .Transform(new Rectangle(0, 0, source.Width, source.Height),
                    Matrix3x2.CreateRotation((float)(angle / 180d * MathF.PI),
                        new Vector2(source.Width / 2f, source.Height / 2f)),
                    new Size(source.Width, source.Height), KnownResamplers.Bicubic)
                .BackgroundColor(Color.White));

        if (returnBitmap.Width != resize.Width || returnBitmap.Height != resize.Height)
        {
            returnBitmap.Mutate(k => k.Resize(resize.Width, resize.Height));
        }

        if (crop)
        {
            var actualWidth = resize.Width / 1.4142135623731F;
            var actualHeight = resize.Height / 1.4142135623731F;

            var min = MathF.Min(actualHeight, actualWidth);
            var cropRectangle = new Rectangle((int)(resize.Width / 2f - min / 2),
                (int)(resize.Height / 2f - min / 2),
                (int)min, (int)min);
            returnBitmap.Mutate(k => k.Crop(cropRectangle));
        }

        return returnBitmap;
    }

    public static Image GetTranslatedBitmap(Image source, int x, int y, Size resize = default)
    {
        if (resize == Size.Empty) resize = source.Size();
        var returnBitmap = source
            .Clone(k => k
                .AutoOrient()
                .Transform(new Rectangle(0, 0, source.Width, source.Height),
                    Matrix3x2.CreateTranslation(x, y),
                    new Size(source.Width, source.Height), KnownResamplers.Bicubic)
                .BackgroundColor(Color.White));

        if (returnBitmap.Width != resize.Width || returnBitmap.Height != resize.Height)
        {
            returnBitmap.Mutate(k => k.Resize(resize.Width, resize.Height));
        }

        return returnBitmap;
    }

    public static async Task SaveGifToFileAsync(string path, Image gif, Color[]? palettes = null)
    {
        gif.Mutate(k => k.Quantize());
        var encoder = new GifEncoder
        {
            ColorTableMode = GifColorTableMode.Global,
            GlobalPixelSamplingStrategy = new ExtensivePixelSamplingStrategy(),
            Quantizer = new OctreeQuantizer(new QuantizerOptions
            {
                DitherScale = QuantizerConstants.MinDitherScale,
            })
        };

        if (palettes != null)
        {
            encoder.ColorTableMode = GifColorTableMode.Local;
            encoder.Quantizer = new PaletteQuantizer(new ReadOnlyMemory<Color>(palettes),
                new QuantizerOptions
                {
                    DitherScale = QuantizerConstants.MinDitherScale
                }
            );
        }

        await gif.SaveAsGifAsync(path, encoder);
    }

    public static async Task<List<FrameInfo>> CloneImagesFromFramesAsync(
        ImageFrameCollection imageFrames)
    {
        var t = Task.Run(() => imageFrames
            .AsParallel()
            .Select((f, i) =>
            {
                var image = imageFrames.CloneFrame(i);
                return new FrameInfo(image, TimeSpan.FromMilliseconds(f.Metadata.GetGifMetadata().FrameDelay * 10d));
            }
            ));

        var results = (await t).ToList();
        return results;
    }

    public static Task<Image> CreateGifByImagesAsync(IReadOnlyCollection<Image> images, TimeSpan delay, Size size,
        bool clone = false)
    {
        return CreateGifByImagesAsync(images.Select(k => new FrameInfo(k, delay)).ToArray(), size, clone);
    }

    public static async Task<Image> CreateGifByImagesAsync(ICollection<FrameInfo> frameInfos, Size size,
        bool clone = false)
    {
        // Iterate in parallel over the images and wait until all images are processed
        var t = Task.Run(() => frameInfos
            .AsParallel()
            .Select((kvp, index) =>
            {
                var (image, delay) = kvp;

                // Resize the image
                var (width, height) = size;
                if (clone)
                    image = image.Clone(_ => { });
                if (image.Width != size.Width || image.Height != size.Height)
                    image.Mutate(ctx => ctx.Resize(width, height));
                return (image, delay);
            })
        );

        var results = (await t).ToArray();
        try
        {
            var gif = new Image<Rgba32>(results.Max(k => k.image.Width),
                results.Max(k => k.image.Height));
            // Iterate over the images to create the gif
            var index = 0;
            foreach (var tuple in results)
            {
                var (image, delay) = tuple;
                // Set the duration of the image
                image.Frames.RootFrame.Metadata.GetGifMetadata().FrameDelay = (int)(delay.TotalMilliseconds / 10);

                // Add the image to the gif
                gif.Frames.InsertFrame(index, image.Frames.RootFrame);
                index++;
            }

            gif.Frames.RemoveFrame(gif.Frames.Count - 1);
            return gif;
        }
        finally
        {
            if (clone)
                foreach (var valueTuple in results)
                {
                    valueTuple.image.Dispose();
                }
        }
    }

    [Obsolete("todo: dangerous: memory leak", true)]
    public static List<FrameInfo> CompressSerial(IReadOnlyList<FrameInfo> sourceSerial, bool autoDisposeSource = true)
    {
        if (sourceSerial.Count <= 1) return sourceSerial.ToList();
        var newList = new List<FrameInfo>
            {
                new(((Image<Rgba32>)sourceSerial[0].Image).Clone(), sourceSerial[0].Delay)
            };
        for (var i = 0; i < sourceSerial.Count - 1; i++)
        {
            var bitmap = (Image<Rgba32>)sourceSerial[i].Image;
            var nextBitmap = (Image<Rgba32>)sourceSerial[i + 1].Image;
            var difference = GetDifference(bitmap, nextBitmap);
            newList.Add(new FrameInfo(difference, sourceSerial[i + 1].Delay));
        }

        if (autoDisposeSource)
        {
            foreach (var image in sourceSerial)
            {
                image.Image.Dispose();
            }
        }

        return newList;
    }

    [Obsolete("todo: dangerous: memory leak", true)]
    public static List<Image> CompressSerial(IReadOnlyList<Image> sourceSerial, bool autoDisposeSource = true)
    {
        if (sourceSerial.Count <= 1) return sourceSerial.ToList();
        var newList = new List<Image> { sourceSerial[0] };
        for (var i = 0; i < sourceSerial.Count - 1; i++)
        {
            var bitmap = (Image<Rgba32>)sourceSerial[i];
            var nextBitmap = (Image<Rgba32>)sourceSerial[i + 1];
            var difference = GetDifference(bitmap, nextBitmap);
            newList.Add(difference);
        }

        if (autoDisposeSource)
        {
            foreach (var image in sourceSerial)
            {
                image.Dispose();
            }
        }

        return newList;
    }

    [Obsolete("todo: dangerous: memory leak", true)]
    public static Image GetDifference(Image<Rgba32> oldFrame, Image<Rgba32> newFrame)
    {
        if (oldFrame.Height != newFrame.Height || oldFrame.Width != newFrame.Width)
        {
            throw new Exception("Bitmaps are not of equal dimensions.");
        }

        var newImage = new Image<Rgba32>(oldFrame.Width, oldFrame.Height);

        newImage.ProcessPixelRows(oldFrame, newFrame, (newImageAccessor, oldAccessor, newAccessor) =>
        {
            for (int y = 0; y < oldAccessor.Height; y++)
            {
                Span<Rgba32> pixelRowSpan1 = oldAccessor.GetRowSpan(y);
                Span<Rgba32> pixelRowSpan2 = newAccessor.GetRowSpan(y);
                Span<Rgba32> pixelRowSpanSource = newImageAccessor.GetRowSpan(y);

                // Using row.Length helps JIT to eliminate bounds checks when accessing row[x].
                for (int x = 0; x < pixelRowSpan1.Length; x++)
                {
                    var color1 = pixelRowSpan1[x];
                    var color2 = pixelRowSpan2[x];

                    if (color1.Rgba != color2.Rgba)
                    {
                        pixelRowSpanSource[x] = new Rgba32(color2.Rgba);
                    }
                }
            }
        });

        return newImage;
    }
}