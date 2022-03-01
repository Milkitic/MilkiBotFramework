using System.IO;
using System.Threading.Tasks;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.Utils;
using SixLabors.ImageSharp;

namespace MilkiBotFramework.Messaging.RichMessages;

public class LinkImage : IRichMessage
{
    public LinkImage(string uri) => Uri = uri;
    public string Uri { get; set; }
    public async Task<MemoryImage> ToMemoryImageAsync(LightHttpClient client)
    {
        var (bytes, imageType) = await client.GetImageBytesFromUrlAsync(Uri);

        var stream = new MemoryStream(bytes);
        var bitmap = await Image.LoadAsync(stream);

        return new MemoryImage(bitmap, imageType);
    }

    public virtual string Encode() => $"[Image: {Uri}]";
}