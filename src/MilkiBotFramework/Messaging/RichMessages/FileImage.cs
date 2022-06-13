using MilkiBotFramework.Imaging;
using SixLabors.ImageSharp;

namespace MilkiBotFramework.Messaging.RichMessages;

public class FileImage : IRichMessage
{
    public FileImage(string path) => Path = path;
    public string Path { get; set; }

    public async Task<MemoryImage> ToMemoryImageAsync()
    {
        var bytes = await File.ReadAllBytesAsync(Path);
        var imageType = ImageHelper.GetKnownImageType(bytes);

        var stream = new MemoryStream(bytes);
        var bitmap = await Image.LoadAsync(stream);

        return new MemoryImage(bitmap, imageType);
    }

    public virtual ValueTask<string> EncodeAsync() => ValueTask.FromResult("[FileImage]");
}