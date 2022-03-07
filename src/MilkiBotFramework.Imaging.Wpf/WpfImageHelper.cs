using System.IO;
using System.Windows.Media.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;

namespace MilkiBotFramework.Imaging.Wpf;

internal static class WpfImageHelper
{
    public static BitmapSource GetBitmapImageFromImageSharp(Image image)
    {
        using var ms = new MemoryStream(); // todo: using?
        image.Save(ms, new PngEncoder());

        var bitmapSource = new BitmapImage();
        bitmapSource.BeginInit();
        bitmapSource.StreamSource = ms;
        bitmapSource.EndInit();

        //freeze bitmapSource and clear memory to avoid memory leaks
        bitmapSource.Freeze();
        return bitmapSource;
    }
}