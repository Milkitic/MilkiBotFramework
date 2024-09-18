using Microsoft.Extensions.Logging;
using MilkiBotFramework.Imaging;
using Minio;
using Minio.DataModel.Args;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;

namespace MilkiBotFramework.Platforms.QQ.Messaging;

public class MinIOController
{
    private static readonly PngEncoder ImageEncoder = new PngEncoder();

    private readonly ILogger<MinIOController> _logger;
    private readonly MinIOOptions _options;
    private readonly IMinioClient _minio;

    public MinIOController(ILogger<MinIOController> logger, BotOptions botOptions)
    {
        _logger = logger;
        _options = ((QQBotOptions)botOptions).MinIOOptions;
        var minioClient = new MinioClient()
            .WithEndpoint(_options.Endpoint)
            .WithCredentials(_options.AccessKey, _options.SecretKey);

        if (_options.UseSSL)
        {
            minioClient = minioClient.WithSSL();
        }

        _minio = minioClient.Build();
    }

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

    public async Task<string> UploadImage(Image image)
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
}