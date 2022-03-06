using MilkiBotFramework.Imaging;

namespace MilkiBotFramework.Utils
{
    public static class FormatHelper
    {
        private static readonly Dictionary<ImageType, Memory<byte>> KnownFileHeaders = new()
        {
            { ImageType.Jpeg, new byte[] { 0xFF, 0xD8 } }, // JPEG
            { ImageType.Bmp, new byte[] { 0x42, 0x4D } }, // BMP
            { ImageType.Gif, new byte[] { 0x47, 0x49, 0x46 } }, // GIF
            { ImageType.Png, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } }, // PNG
            //{ ImageType.Pdf, new byte[]{ 0x25, 0x50, 0x44, 0x46 }} // PDF
        };

        public static ImageType GetKnownImageType(ReadOnlySpan<byte> data)
        {
            foreach (var check in KnownFileHeaders)
            {
                if (data.Length >= check.Value.Length)
                {
                    var slice = data.Slice(0, check.Value.Length);
                    if (slice.SequenceEqual(check.Value.Span))
                    {
                        return check.Key;
                    }
                }
            }

            return ImageType.Unknown;
        }
    }
}
