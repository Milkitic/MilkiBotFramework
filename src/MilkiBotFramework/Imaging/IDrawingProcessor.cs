using SixLabors.ImageSharp;

namespace MilkiBotFramework.Imaging;

public interface IDrawingProcessor<in TViewModel> where TViewModel : class
{
    string Process(Image source, TViewModel model);
    string Process(TViewModel model);
    Task<string> ProcessAsync(Image source, TViewModel model);
    Task<string> ProcessAsync(TViewModel model);
    string ProcessGif(Image source, TViewModel model, TimeSpan interval, bool repeat);
    string ProcessGif(TViewModel model, TimeSpan interval, bool repeat);
    Task<string> ProcessGifAsync(Image source, TViewModel model, TimeSpan interval, bool repeat);
    Task<string> ProcessGifAsync(TViewModel model, TimeSpan interval, bool repeat);
}