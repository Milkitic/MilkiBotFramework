using SixLabors.ImageSharp;

namespace MilkiBotFramework.Imaging;

public sealed class GifProcessOptions
{
    public readonly TimeSpan Interval;
    public readonly bool Repeat;
    public readonly Image? Image;

    public GifProcessOptions(TimeSpan interval, Image? image = null, bool repeat = true)
    {
        Interval = interval;
        Image = image;
        Repeat = repeat;
    }
}