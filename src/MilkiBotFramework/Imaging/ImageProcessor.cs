using System.Diagnostics;
using System.Numerics;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace MilkiBotFramework.Imaging;

public class ImageProcessor
{
    private readonly BotOptions _options;

    public ImageProcessor(BotOptions options)
    {
        _options = options;
    }

    public string CompressToFile(Image source, Color[]? palette = null)
    {
        var tempPath = Path.Combine(_options.CacheImageDir, Path.GetRandomFileName() + ".gif");

        ImageProcessor.SaveGifToFileAsync(tempPath, source, palette).Wait();
        var newPath = CompressToFile(tempPath);
        return newPath;
    }

    public string CompressToFile(string sourcePath)
    {
        var targetDir = Path.GetDirectoryName(sourcePath);
        if (targetDir == null)
        {
            throw new DirectoryNotFoundException($"\"{targetDir}\" was not found");
        }

        var fileName = Path.GetFileNameWithoutExtension(sourcePath);
        var extension = Path.GetExtension(sourcePath);

        var targetName = $"{fileName}-opt{extension}";

        try
        {
            var proc = Process.Start(
                new ProcessStartInfo(_options.GifSiclePath,
                    $"-i \"{fileName}{extension}\" --loopcount=infinite --careful --optimize=2 -o \"{targetName}\"")
                {
                    WorkingDirectory = targetDir,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });
            proc?.WaitForExit();
            if (proc?.ExitCode != 0) throw new Exception("gifsicle exit with code: " + proc?.ExitCode);
            proc?.Dispose();
        }
        catch (Exception e)
        {
            return sourcePath;
        }

        return Path.Combine(targetDir, targetName);
    }

    public async Task<string[]> SaveFramesToFileAsync(ImageFrameCollection imageFrames)
    {
        var images = await ImageProcessor.CloneImagesFromFramesAsync(imageFrames);
        var guid = Path.GetRandomFileName();
        var j = 0;
        var pathList = new List<string>();
        foreach (var keyValuePair in images)
        {
            string outputFilePath = Path.Combine(_options.CacheImageDir, $"{guid}-{j:00}.png");
            await keyValuePair.Image.SaveAsPngAsync(outputFilePath);
            pathList.Add(outputFilePath);
            j++;
        }

        return pathList.ToArray();
    }

    public async Task<Color[]> ComputePalette(ImageFrameCollection imageFrames)
    {
        var path = await SaveFramesToFileAsync(imageFrames);
        var sourceFolder = Path.GetDirectoryName(path[0]);
        var split = Path.GetFileName(path[0]).Split('-');
        var standardName = split[0];
        var ext = Path.GetExtension(split[1]);

        var ffmpeg = _options.FfMpegPath;
        RunAndWait(ffmpeg,
            $"-hide_banner " +
            //$"-f image2 " +
            $"-hwaccel auto " +
            $"-i {standardName}-%02d{ext} " +
            $"-vf scale=iw:ih:sws_dither=ed,palettegen " +
            $"{standardName}-palette.png",
            sourceFolder);


        var targetPath = Path.Combine(sourceFolder, $"{standardName}-palette.png");

        var colors = GetPaletteFromFile(targetPath);
        return colors;
    }

    private static Color[] GetPaletteFromFile(string targetPath)
    {
        bool x = false;
        bool y = true;

        var result = x | y;

        using var image = (Image<Rgba32>)Image.Load(targetPath);
        var arr = new Color[256];
        int k = 0;
        image.ProcessPixelRows(pixelAccessor =>
        {
            for (int y = 0; y < pixelAccessor.Height; y++)
            {
                Span<Rgba32> row = pixelAccessor.GetRowSpan(y);

                // Using row.Length helps JIT to eliminate bounds checks when accessing row[x].
                foreach (var color in row)
                {
                    arr[k++] = color;
                }
            }
        });

        return arr;
    }

    private static void RunAndWait(string fileName, string arguments, string workingDirectory)
    {
        var stringBuilder = new StringBuilder();

        void OnProcDataReceived(object s, DataReceivedEventArgs e)
        {
            stringBuilder.AppendLine(e.Data);
        }

        var psi = new ProcessStartInfo(fileName, arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        var proc = new Process { StartInfo = psi };
        proc.Start();

        proc.OutputDataReceived += OnProcDataReceived;
        proc.ErrorDataReceived += OnProcDataReceived;
        proc.BeginErrorReadLine();
        proc.BeginOutputReadLine();

        proc?.WaitForExit();

        proc.OutputDataReceived -= OnProcDataReceived;
        proc.ErrorDataReceived -= OnProcDataReceived;


        if (proc?.ExitCode != 0)
        {
            throw new Exception("ffmpeg exit with code: " + proc?.ExitCode,
                new Exception(stringBuilder.ToString()));
        }

        proc?.Dispose();
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