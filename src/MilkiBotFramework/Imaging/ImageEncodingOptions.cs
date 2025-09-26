namespace MilkiBotFramework.Imaging;

public class ImageEncodingOptions
{
    public ImageType ImageType { get; set; } = ImageType.Webp;
    public int LossyQuality { get; set; } = 90;
    public bool PreferLossless { get; set; } = true;
    public bool PreferPalette { get; set; } = false;
}