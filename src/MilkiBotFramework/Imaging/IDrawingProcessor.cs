using SixLabors.ImageSharp;

namespace MilkiBotFramework.Imaging;

public interface IDrawingProcessor<in TViewModel> where TViewModel : class
{
    Task<Image> ProcessAsync(TViewModel viewModel, string locale = "en-US", Image? sourceImage = null);
    Task<Image> ProcessGifAsync(TViewModel viewModel, TimeSpan interval, string locale = "en-US", Image? sourceImage = null, bool repeat = true);
}