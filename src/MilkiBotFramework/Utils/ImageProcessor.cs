using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using MilkiBotFramework.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace MilkiBotFramework.Utils
{
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

            ImageHelper.SaveGifToFileAsync(tempPath, source, palette).Wait();
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
            var images = await ImageHelper.CloneImagesFromFramesAsync(imageFrames);
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
    }
}