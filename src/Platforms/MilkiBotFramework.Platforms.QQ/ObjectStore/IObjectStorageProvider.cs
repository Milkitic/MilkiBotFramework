using MilkiBotFramework.Imaging;
using SixLabors.ImageSharp;

namespace MilkiBotFramework.Platforms.QQ.ObjectStore;

/// <summary>将本地或内存图片上传到对象存储，供 QQ 平台按 URL 转存。</summary>
public interface IObjectStorageProvider
{
    /// <summary>上传本地图片文件。</summary>
    /// <param name="path">待读取的图片路径。</param>
    /// <returns>可用于 QQ 文件上传接口的临时下载 URL。</returns>
    Task<string> UploadImage(string path);
    /// <summary>编码并上传内存图片。</summary>
    /// <param name="path">待上传的内存图片。</param>
    /// <param name="encodingOptions">图片编码选项，具体支持取决于存储服务实现。</param>
    /// <returns>可用于 QQ 文件上传接口的临时下载 URL。</returns>
    Task<string> UploadImage(Image path, ImageEncodingOptions encodingOptions);
}
