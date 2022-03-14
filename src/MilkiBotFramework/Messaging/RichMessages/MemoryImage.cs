using MilkiBotFramework.Imaging;
using SixLabors.ImageSharp;

namespace MilkiBotFramework.Messaging.RichMessages;

public class MemoryImage : IRichMessage, IDisposable
{
    public MemoryImage(Image imageSource, ImageType imageType)
    {
        ImageSource = imageSource;
        ImageType = imageType;
    }

    public Image ImageSource { get; set; }
    public ImageType ImageType { get; }
    public void Dispose() => ImageSource?.Dispose();
    public virtual async ValueTask<string> EncodeAsync() => "[Image]";
    public override string ToString()
    {
        return "[合成图片]";
    }
}