using System;
using SixLabors.ImageSharp;

namespace MilkiBotFramework.Imaging;

public class FrameInfo
{
    public FrameInfo(Image image, TimeSpan delay)
    {
        Image = image;
        Delay = delay;
    }

    public Image Image { get; set; }
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