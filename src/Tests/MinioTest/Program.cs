using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace MinioTest;

internal class Program
{
    // ReSharper disable once UnusedParameter.Local
    static async Task Main(string[] args)
    {
        var lines = File.ReadAllLines("config.txt");

        var endpoint = lines[0];
        var accessKey = lines[1];
        var secretKey = lines[2];
        var filePath = lines[3];
        try
        {
            var minio = new MinioClient()
                .WithEndpoint(endpoint)
                .WithCredentials(accessKey, secretKey)
                //.WithSSL()
                .Build();
            await Run(minio, filePath);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }

        Console.ReadLine();
    }

    // File uploader task.
    private static async Task Run(IMinioClient minio, string filePath)
    {
        var bucketName = "test";
        //var location = "us-east-1";
        var objectName = Path.GetFileName(filePath);
        var contentType = "image/png";

        try
        {
            // Make a bucket on the server, if not already present.
            var beArgs = new BucketExistsArgs()
                .WithBucket(bucketName);
            bool found = await minio.BucketExistsAsync(beArgs).ConfigureAwait(false);
            if (!found)
            {
                var mbArgs = new MakeBucketArgs()
                    .WithBucket(bucketName);
                await minio.MakeBucketAsync(mbArgs).ConfigureAwait(false);
            }

            // Upload a file to bucket.
            var putObjectArgs = new PutObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName)
                .WithFileName(filePath)
                .WithContentType(contentType);
            await minio.PutObjectAsync(putObjectArgs).ConfigureAwait(false);
            Console.WriteLine("Successfully uploaded " + objectName);

            var reqParams = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "response-content-type", "image/png" }
            };
            var totalSeconds = (int)TimeSpan.FromMinutes(5).TotalSeconds;
            var getArgs = new PresignedGetObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName)
                .WithExpiry(totalSeconds)
                .WithHeaders(reqParams);
            var presignedUrl = await minio.PresignedGetObjectAsync(getArgs).ConfigureAwait(false);
            Console.WriteLine(presignedUrl);
            var args = new StatObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName);

            var stat = await minio.StatObjectAsync(args).ConfigureAwait(false);
            Console.WriteLine(stat);
        }
        catch (MinioException e)
        {
            Console.WriteLine("File Upload Error: {0}", e.Message);
        }
    }
}