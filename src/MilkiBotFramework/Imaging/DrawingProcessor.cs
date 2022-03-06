using System.Diagnostics;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace MilkiBotFramework.Imaging
{
    // wpf gif: https://docs.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/how-to-encode-and-decode-a-gif-image?view=netframeworkdesktop-4.8
    public class DrawingProcessor<TViewModel> : IDrawingProcessor<TViewModel> where TViewModel : class
    {
        public string Process(Image source, TViewModel model)
        {
            throw new NotImplementedException();
        }

        public string Process(TViewModel model)
        {
            throw new NotImplementedException();
        }

        public Task<string> ProcessAsync(Image source, TViewModel model)
        {
            throw new NotImplementedException();
        }

        public Task<string> ProcessAsync(TViewModel model)
        {
            throw new NotImplementedException();
        }

        public string ProcessGif(Image source, TViewModel model, TimeSpan interval, bool repeat)
        {
            throw new NotImplementedException();
        }

        public string ProcessGif(TViewModel model, TimeSpan interval, bool repeat)
        {
            throw new NotImplementedException();
        }

        public Task<string> ProcessGifAsync(Image source, TViewModel model, TimeSpan interval, bool repeat)
        {
            throw new NotImplementedException();
        }

        public Task<string> ProcessGifAsync(TViewModel model, TimeSpan interval, bool repeat)
        {
            throw new NotImplementedException();
        }
    }
}