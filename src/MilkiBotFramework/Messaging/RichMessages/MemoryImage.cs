using MilkiBotFramework.Imaging;
using SixLabors.ImageSharp;

namespace MilkiBotFramework.Messaging.RichMessages;

public class MemoryImage : IRichMessage, IDisposable
{
    public MemoryImage(Image imageSource, ImageType imageType)
    {
        ImageSource = imageSource;
        ImageEncodingOptions.ImageType = imageType;
    }

    public Image ImageSource { get; }
    public ImageEncodingOptions ImageEncodingOptions { get; } = new();

    public void Dispose() => ImageSource.Dispose();
    public virtual ValueTask<string> EncodeAsync() => ValueTask.FromResult("[Image]");

    public override string ToString()
    {
        return "[合成图片]";
    }
}