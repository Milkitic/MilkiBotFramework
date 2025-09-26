using MilkiBotFramework.Imaging;
using SixLabors.ImageSharp;

namespace MilkiBotFramework.Platforms.QQ.ObjectStore;

public interface IObjectStorageProvider
{
    Task<string> UploadImage(string path);
    Task<string> UploadImage(Image path, ImageEncodingOptions encodingOptions);
}