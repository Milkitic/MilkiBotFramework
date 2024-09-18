using MilkiBotFramework.Messaging.RichMessages;

namespace MilkiBotFramework.Platforms.QQ.Messaging.RichMessages;

internal class ImagePlaceholder(int i) : IRichMessage
{
    public int Index { get; } = i;

    public ValueTask<string> EncodeAsync()
    {
        return ValueTask.FromResult($"(见图{Index})");
    }
}