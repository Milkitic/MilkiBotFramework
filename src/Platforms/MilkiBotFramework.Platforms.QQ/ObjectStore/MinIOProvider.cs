using Microsoft.Extensions.Logging;
using MilkiBotFramework.Imaging;
using Minio;
using Minio.DataModel.Args;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;

namespace MilkiBotFramework.Platforms.QQ.ObjectStore;

/// <summary>将待发送的图片上传到 MinIO 对象存储。</summary>
// ReSharper disable once InconsistentNaming
public class MinIOProvider : IObjectStorageProvider
{
    private static readonly PngEncoder ImageEncoder = new PngEncoder();

    private readonly ILogger<MinIOProvider> _logger;
    private readonly OssOptions _options;
    private readonly IMinioClient _minio;

    /// <summary>从 QQ 图片存储配置创建 MinIO 客户端。</summary>
    /// <param name="logger">存储操作日志。</param>
    /// <param name="botOptions">包含 MinIO 配置的 QQ 机器人配置。</param>
    public MinIOProvider(ILogger<MinIOProvider> logger, BotOptions botOptions)
    {
        _logger = logger;
        _options = ((QQBotOptions)botOptions).OssOptions;
        var minioClient = new MinioClient()
            .WithEndpoint(_options.CustomEndpoint)
            .WithCredentials(_options.AccessKey, _options.SecretKey);

        if (_options.CustomUseSSL)
        {
            minioClient = minioClient.WithSSL();
        }

        _minio = minioClient.Build();
    }

    /// <summary>上传本地图片文件并签发临时下载地址。</summary>
    /// <param name="path">待读取的图片路径。</param>
    /// <returns>MinIO 临时下载 URL。</returns>
    public async Task<string> UploadImage(string path)
    {
        var bucketName = _options.BucketName;
        var objectName = Path.GetFileName(path);
        var data = await File.ReadAllBytesAsync(path);
        var imageType = ImageHelper.GetKnownImageType(data);
        var contentType = imageType switch
        {
            ImageType.Unknown => "application/octet-stream",
            ImageType.Jpeg => "image/jpg",
            ImageType.Bmp => "image/bmp",
            ImageType.Gif => "image/gif",
            ImageType.Png => "image/png",
            _ => throw new ArgumentOutOfRangeException()
        };

        var beArgs = new BucketExistsArgs()
            .WithBucket(bucketName);
        bool found = await _minio.BucketExistsAsync(beArgs).ConfigureAwait(false);
        if (!found)
        {
            var mbArgs = new MakeBucketArgs()
                .WithBucket(bucketName);
            await _minio.MakeBucketAsync(mbArgs).ConfigureAwait(false);
            _logger.LogInformation("Successfully create bucket: " + bucketName);
        }

        // Upload a file to bucket.

        using var ms = new MemoryStream(data);
        var putObjectArgs = new PutObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithStreamData(ms)
            .WithObjectSize(ms.Length)
            .WithContentType(contentType);
        await _minio.PutObjectAsync(putObjectArgs).ConfigureAwait(false);
        _logger.LogDebug($"Successfully uploaded {objectName} to {bucketName}");

        var reqParams = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "response-content-type", contentType }
        };
        var totalSeconds = (int)TimeSpan.FromMinutes(5).TotalSeconds;
        var getArgs = new PresignedGetObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithExpiry(totalSeconds)
            .WithHeaders(reqParams);
        var presignedUrl = await _minio.PresignedGetObjectAsync(getArgs).ConfigureAwait(false);
        _logger.LogDebug($"Successfully got temporary download link: {presignedUrl}");
        return presignedUrl;
    }

    /// <summary>将内存图片编码为 PNG 后上传。</summary>
    /// <param name="image">待上传的内存图片。</param>
    /// <param name="encodingOptions">框架通用图片编码参数。此实现使用固定的 PNG 编码。</param>
    /// <returns>MinIO 临时下载 URL。</returns>
    public async Task<string> UploadImage(Image image, ImageEncodingOptions encodingOptions)
    {
        var bucketName = _options.BucketName;
        var objectName = $"{Path.GetRandomFileName()}.png";
        var contentType = "image/png";

        var beArgs = new BucketExistsArgs()
            .WithBucket(bucketName);
        bool found = await _minio.BucketExistsAsync(beArgs).ConfigureAwait(false);
        if (!found)
        {
            var mbArgs = new MakeBucketArgs()
                .WithBucket(bucketName);
            await _minio.MakeBucketAsync(mbArgs).ConfigureAwait(false);
            _logger.LogInformation("Successfully create bucket: " + bucketName);
        }

        // Upload a file to bucket.

        using var ms = new MemoryStream();
        await image.SaveAsync(ms, ImageEncoder);
        ms.Position = 0;

        var putObjectArgs = new PutObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithStreamData(ms)
            .WithObjectSize(ms.Length)
            .WithContentType(contentType);
        var response = await _minio.PutObjectAsync(putObjectArgs).ConfigureAwait(false);
        if ((int)response.ResponseStatusCode >= 400)
        {
            throw new Exception(response.ResponseStatusCode.ToString());
        }

        _logger.LogDebug($"Successfully uploaded {objectName} to {bucketName}");

        var reqParams = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "response-content-type", contentType }
        };
        var totalSeconds = (int)TimeSpan.FromMinutes(5).TotalSeconds;
        var getArgs = new PresignedGetObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithExpiry(totalSeconds)
            .WithHeaders(reqParams);
        var presignedUrl = await _minio.PresignedGetObjectAsync(getArgs).ConfigureAwait(false);
        _logger.LogDebug($"Successfully got temporary download link: {presignedUrl}");
        return presignedUrl;
    }
}
