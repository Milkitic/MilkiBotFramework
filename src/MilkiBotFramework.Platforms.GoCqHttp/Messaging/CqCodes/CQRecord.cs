using MilkiBotFramework.Messaging.RichMessages;
using MilkiBotFramework.Platforms.GoCqHttp.Utils;

namespace MilkiBotFramework.Platforms.GoCqHttp.Messaging.CqCodes;

// ReSharper disable once InconsistentNaming
public class CQRecord : IRichMessage
{
    private string? _downloadUri;

    public string File { get; private set; }

    public string? DownloadUri
    {
        get => _downloadUri;
        set
        {
            if (_downloadUri == value) return;
            _downloadUri = value;
            RecordFileBytes = null;
        }
    }

    public byte[]? RecordFileBytes { get; private set; }

    public async Task<string> GetBase64Async()
    {
        await EnsureRecordBytesAndCaches();
        await using var ms = new MemoryStream(RecordFileBytes!);
        return EncodingHelper.EncodeFileToBase64(ms);
    }

    public Task<string> EnsureRecordBytesAndCaches()
    {
        throw new NotImplementedException();
        //bool writeCache = RecordFileBytes == null;
        //if (RecordFileBytes == null)
        //{
        //    var di = new DirectoryInfo(AppSettings.Directories.CacheVoiceDir);
        //    var file = di
        //        .EnumerateFiles(Path.GetFileNameWithoutExtension(File) + ".*")
        //        .AsParallel()
        //        .FirstOrDefault();
        //    if (file != null)
        //    {
        //        RecordFileBytes = await System.IO.File.ReadAllBytesAsync(file.FullName);
        //        return file.FullName;
        //    }

        //    if (File.StartsWith("base64://"))
        //    {
        //        var base64 = File[9..];
        //        var bytes = EncodingHelper.DecodeBase64ToBytes(base64);

        //        RecordFileBytes = bytes;
        //        File = Path.GetRandomFileName();
        //    }
        //    else
        //    {
        //        if (DownloadUri == null) throw new NotImplementedException();
        //        var (imageBytes, imageType) = await HttpClient.GetImageBytesFromUrlAsync(DownloadUri);
        //        RecordFileBytes = imageBytes;
        //    }
        //}

        //var cachePath = Path.Combine(AppSettings.Directories.CacheVoiceDir,
        //    Path.GetFileNameWithoutExtension(File) + ".amr");
        //if (writeCache)
        //{
        //    try
        //    {
        //        await System.IO.File.WriteAllBytesAsync(cachePath, RecordFileBytes);
        //    }
        //    catch (Exception e)
        //    {
        //    }
        //}

        //return cachePath;
    }

    public override string ToString() => "[语音]";

    public async ValueTask<string> EncodeAsync()
    {
        for (int i = 0; i < 2; i++)
        {
            if (RecordFileBytes != null)
            {
                await using var ms = new MemoryStream(RecordFileBytes);
                return $"[CQ:record,file=base64://{EncodingHelper.EncodeFileToBase64(ms)}]";
            }

            if (DownloadUri != null && (DownloadUri.StartsWith("http://") || DownloadUri.StartsWith("https://")))
                return $"[CQ:record,file={DownloadUri}]";

            if (i == 0)
                await EnsureRecordBytesAndCaches();
        }

        throw new Exception("There is no record to encode.");
    }

    public static CQRecord FromBytes(byte[] bytes)
    {
        return new CQRecord(bytes);
    }

    public static CQRecord FromFile(string path)
    {
        return new CQRecord(System.IO.File.ReadAllBytes(path));
    }

    public static CQRecord FromUri(string uri)
    {
        return new CQRecord(new Uri(uri));
    }

    private CQRecord(Uri uri)
    {
        DownloadUri = uri.ToString();
        File = Path.GetRandomFileName();
    }

    private CQRecord(byte[] recordFileBytes)
    {
        RecordFileBytes = recordFileBytes;
        File = Path.GetRandomFileName();
    }

    //internal new static CQRecord Parse(string content)
    //{
    //    const int flagLen = 5;
    //    var dictionary = GetParameters(content.Substring(5 + flagLen, content.Length - 6 - flagLen));

    //    if (!dictionary.TryGetValue("file", out var file))
    //        throw new InvalidOperationException(nameof(CQRecord) + "至少需要file参数");

    //    var cqImage = new CQRecord(file);

    //    if (dictionary.TryGetValue("url", out var val))
    //        cqImage.DownloadUri = val;

    //    if (dictionary.TryGetValue("subType", out var subType))
    //        cqImage.SubType = (CQImageType)int.Parse(subType);

    //    return cqImage;
    //}

    //private CQRecord(string localGoFilename)
    //{
    //    LocalGoFilename = localGoFilename;
    //}
}