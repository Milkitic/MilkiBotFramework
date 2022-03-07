namespace MilkiBotFramework.Connecting;

public sealed class WebSocketMessageSession
{
    public TaskCompletionSource TaskCompletionSource { get; set; }
    public string? Response { get; set; }

    public WebSocketMessageSession(TaskCompletionSource taskCompletionSource)
    {
        TaskCompletionSource = taskCompletionSource;
    }
}