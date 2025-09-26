using Microsoft.Extensions.Logging;
using MilkiBotFramework.Imaging;
using Qiniu.Storage;
using Qiniu.Util;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace MilkiBotFramework.Platforms.QQ.ObjectStore;

public class QiniuProvider : IObjectStorageProvider
{
    private static readonly Dictionary<ImageType, string> ImageTypeToExtension = new()
    {
        { ImageType.Unknown, ".bin" },
        { ImageType.Jpeg, ".jpg" },
        { ImageType.Bmp, ".bmp" },
        { ImageType.Gif, ".gif" },
        { ImageType.Png, ".png" },
        { ImageType.Webp, ".webp" }
    };

    private static readonly Dictionary<ImageType, string> ImageTypeToMimeType = new()
    {
        { ImageType.Unknown, "application/octet-stream" },
        { ImageType.Jpeg, "image/jpg" },
        { ImageType.Bmp, "image/bmp" },
        { ImageType.Gif, "image/gif" },
        { ImageType.Png, "image/png" },
        { ImageType.Webp, "image/webp" }
    };

    private static readonly Dictionary<ImageType, Func<ImageEncodingOptions, ImageEncoder>> ImageTypeToImageEncoder =
        new()
        {
            { ImageType.Unknown, _ => new BmpEncoder() },
            { ImageType.Bmp, _ => new BmpEncoder() },
            { ImageType.Gif, _ => new GifEncoder() },
            {
                ImageType.Jpeg, options => new JpegEncoder
                {
                    Quality = options.LossyQuality
                }
            },
            {
                ImageType.Png, options => new PngEncoder
                {
                    CompressionLevel = (PngCompressionLevel)(options.LossyQuality / 10),
                    ColorType = options.PreferPalette ? PngColorType.Palette : PngColorType.RgbWithAlpha,
                    Quantizer = options.PreferPalette ? new WuQuantizer() : null,
                }
            },
            {
                ImageType.Webp, options => new WebpEncoder
                {
                    FileFormat = options.PreferLossless ? WebpFileFormatType.Lossless : WebpFileFormatType.Lossy,
                    NearLossless = !options.PreferLossless,
                    Quality = options.PreferLossless ? 100 : options.LossyQuality,
                    TransparentColorMode = WebpTransparentColorMode.Preserve
                }
            }
        };

    private readonly ILogger<QiniuProvider> _logger;
    private readonly QQBotOptions _botOptions;
    private readonly OssOptions _options;
    private readonly Mac _mac;

    public QiniuProvider(ILogger<QiniuProvider> logger, BotOptions botOptions)
    {
        _logger = logger;
        _botOptions = (QQBotOptions)botOptions;
        _options = _botOptions.OssOptions;
        _mac = new Mac(_options.AccessKey, _options.SecretKey);
    }

    public async Task<string> UploadImage(string path)
    {
        var objectName = Path.GetFileName(path);
        var data = await File.ReadAllBytesAsync(path);
        var imageType = ImageHelper.GetKnownImageType(data);
        using var memoryStream = new MemoryStream(data);

        var contentType = ImageTypeToMimeType[imageType];
        return await UploadImage(objectName, contentType, memoryStream);
    }

    public async Task<string> UploadImage(Image image, ImageEncodingOptions encodingOptions)
    {
        var imageType = encodingOptions.ImageType;
        using var memoryStream = new MemoryStream();
        var imageEncoder = ImageTypeToImageEncoder[imageType](encodingOptions);
        await image.SaveAsync(memoryStream, imageEncoder);
        memoryStream.Position = 0;

        var objectName = $"{Path.GetRandomFileName()}{ImageTypeToExtension[imageType]}";
        var contentType = ImageTypeToMimeType[imageType];
        return await UploadImage(objectName, contentType, memoryStream);
    }

    private async Task<string> UploadImage(string objectName, string contentType, MemoryStream memoryStream)
    {
        var folder = _botOptions.Connection.IsDevelopment ? "development" : "production";
        objectName = $"{folder}/{objectName}"; // Add folder prefix to the object name

        // https://developer.qiniu.com/kodo/1237/csharp#server-upload
        var bucketName = _options.BucketName;

        // 设置上传策略
        var putPolicy = new PutPolicy
        {
            Scope = bucketName, // 设置要上传的目标空间
            DeleteAfterDays = 1, // 文件上传完毕后，在多少天后自动被删除
        };
        putPolicy.SetExpires(3600);  // 上传策略的过期时间(单位:秒)
        var putExtra = new PutExtra
        {
            MimeType = contentType
        };

        var token = Auth.CreateUploadToken(_mac, putPolicy.ToJsonString()); // 生成上传token
        var config = new Config
        {
            Zone = Zone.ZONE_AS_Singapore, // 设置上传区域，非大陆无需备案
            UseHttps = false, // 设置 http 或者 https 上传
            UseCdnDomains = false,
            ChunkSize = ChunkUnit.U512K
        };

        var target = new FormUploader(config); // 表单上传
        var result = await Task.Run(() => target.UploadStream(memoryStream, objectName, token, putExtra));
        if (result.Code < 400)
        {
            _logger.LogDebug("Successfully uploaded {ObjectName} to {BucketName}", objectName, bucketName);
        }
        else
        {
            _logger.LogWarning("Upload failed");
            throw new Exception("图片上传失败！")
            {
                Data = { ["HTTP Response"] = result.ToString() }
            };
        }

        var protocol = _options.CustomUseSSL ? "https" : "http";
        string domain = $"{protocol}://{_options.CustomEndpoint}";
        string key = $"{objectName}";
        string privateUrl = DownloadManager.CreatePrivateUrl(_mac, domain, key, 3600);
        _logger.LogDebug($"Successfully got temporary download link: {privateUrl}");
        return privateUrl;
    }
}