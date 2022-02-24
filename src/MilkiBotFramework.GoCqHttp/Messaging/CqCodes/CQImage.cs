using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using MilkiBotFramework.GoCqHttp.Utils;
using MilkiBotFramework.Imaging;
using MilkiBotFramework.Messaging.RichMessages;
using SixLabors.ImageSharp;

namespace MilkiBotFramework.GoCqHttp.Messaging.CqCodes
{
    // ReSharper disable once InconsistentNaming
    public class CQImage : IRichMessage
    {
        private string? _downloadUri;

        /// <summary>
        /// go-cqhttp\data\images\{uuid}.image
        /// </summary>
        public string LocalGoFilename { get; private set; }

        public string? DownloadUri
        {
            get => _downloadUri;
            set
            {
                if (_downloadUri == value) return;
                _downloadUri = value;
                ImageFileBytes = null;
                ImageType = null;
            }
        }

        public CQImageType? SubType { get; private set; }
        public byte[]? ImageFileBytes { get; private set; }
        public ImageType? ImageType { get; private set; }

        public async Task<Image> GetImageAsync()
        {
            await EnsureImageBytesAndCaches();
            var ms = new MemoryStream(ImageFileBytes!);
            return await Image.LoadAsync(ms);
        }

        public async Task<string> GetBase64Async()
        {
            await EnsureImageBytesAndCaches();
            await using var ms = new MemoryStream(ImageFileBytes!);
            return EncodingHelper.EncodeFileToBase64(ms);
        }

        public async Task<string> EnsureImageBytesAndCaches()
        {
            throw new NotImplementedException();
            //bool writeCache = ImageFileBytes == null;
            //if (ImageFileBytes == null)
            //{
            //    var di = new DirectoryInfo(AppSettings.Directories.CacheImageDir);
            //    var file = di
            //        .EnumerateFiles(Path.GetFileNameWithoutExtension(LocalGoFilename) + ".*")
            //        .AsParallel()
            //        .FirstOrDefault();
            //    if (file != null)
            //    {
            //        ImageFileBytes = await File.ReadAllBytesAsync(file.FullName);
            //        ImageType = ImageHelper.GetKnownImageType(ImageFileBytes);
            //        return file.FullName;
            //    }

            //    if (LocalGoFilename.StartsWith("base64://"))
            //    {
            //        var base64 = LocalGoFilename[9..];
            //        var bytes = EncodingUtil.EncodeBase64ToBytes(base64);
            //        var format = ImageHelper.GetKnownImageType(bytes);

            //        ImageType = format;
            //        ImageFileBytes = bytes;
            //        LocalGoFilename = Path.GetRandomFileName();
            //    }
            //    else
            //    {
            //        if (DownloadUri == null) throw new NotImplementedException();
            //        var (imageBytes, imageType) = await HttpClient.GetImageBytesFromUrlAsync(DownloadUri);
            //        ImageFileBytes = imageBytes;
            //        ImageType = imageType;
            //    }
            //}

            //string ext = ImageType switch
            //{
            //    Imaging.ImageType.Jpeg => ".jpg",
            //    Imaging.ImageType.Png => ".png",
            //    Imaging.ImageType.Gif => ".gif",
            //    Imaging.ImageType.Bmp => ".bmp",
            //    _ => ".unk"
            //};

            //var cachePath = Path.Combine(AppSettings.Directories.CacheImageDir,
            //    Path.GetFileNameWithoutExtension(LocalGoFilename) + ext);
            //if (writeCache)
            //{
            //    try
            //    {
            //        await File.WriteAllBytesAsync(cachePath, ImageFileBytes);
            //    }
            //    catch (Exception e)
            //    {
            //    }
            //}
            //return cachePath;
        }

        public override string ToString() => "[图片]";

        public string Encode()
        {
            for (int i = 0; i < 2; i++)
            {
                if (ImageFileBytes != null)
                {
                    using var ms = new MemoryStream(ImageFileBytes);
                    return $"[CQ:image,file=base64://{EncodingHelper.EncodeFileToBase64(ms)}]";
                }

                if (DownloadUri != null && (DownloadUri.StartsWith("http://") || DownloadUri.StartsWith("https://")))
                    return $"[CQ:image,file={DownloadUri}]";

                if (i == 0)
                    EnsureImageBytesAndCaches().Wait();
            }

            throw new Exception("There is no image to encode.");
        }

        public static CQImage Parse(ReadOnlyMemory<char> content)
        {
            const int flagLen = 5;
            var s = content.Slice(5 + flagLen, content.Length - 6 - flagLen).ToString();
            var dictionary = CQCodeHelper.GetParameters(s);

            if (!dictionary.TryGetValue("file", out var file))
                throw new InvalidOperationException(nameof(CQImage) + "至少需要file参数");

            var cqImage = new CQImage(file);

            if (dictionary.TryGetValue("url", out var val))
                cqImage.DownloadUri = val;

            if (dictionary.TryGetValue("subType", out var subType))
                cqImage.SubType = (CQImageType)int.Parse(subType);

            return cqImage;
        }

        public static CQImage FromBytes(byte[] bytes)
        {
            return new CQImage(bytes);
        }

        public static CQImage FromFile(string path)
        {
            return new CQImage(File.ReadAllBytes(path));
        }

        public static CQImage FromUri(string uri)
        {
            return new CQImage(new Uri(uri));
        }

        private CQImage(Uri uri)
        {
            DownloadUri = uri.ToString();
            LocalGoFilename = Path.GetRandomFileName();
        }

        private CQImage(byte[] recordFileBytes)
        {
            ImageFileBytes = recordFileBytes;
            LocalGoFilename = Path.GetRandomFileName();
        }
        private CQImage(string localGoFilename)
        {
            LocalGoFilename = localGoFilename;
        }
    }
    // ReSharper disable once InconsistentNaming
}