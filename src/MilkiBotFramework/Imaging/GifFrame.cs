using SixLabors.ImageSharp;

namespace MilkiBotFramework.Imaging;

public sealed class GifFrame
{
    public readonly Image Image;

    public GifFrame(Image image, TimeSpan delay)
    {
        Image = image;
        Delay = delay;
    }

    public TimeSpan Delay { get; set; }

    public void Deconstruct(out Image image, out TimeSpan delay)
    {
        image = Image;
        delay = Delay;
    }

    public override string ToString()
    {
        return Delay.TotalMilliseconds / 10 + ": " + Image;
    }
}