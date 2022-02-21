using System.Threading.Tasks;

namespace MilkiBotFramework.Connecting;

public sealed class WebsocketRequestSession
{
    public TaskCompletionSource TaskCompletionSource { get; set; }
    public string? Response { get; set; }

    public WebsocketRequestSession(TaskCompletionSource taskCompletionSource)
    {
        TaskCompletionSource = taskCompletionSource;
    }
}