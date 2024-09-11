namespace MilkiBotFramework.Connecting;

public class WebSocketAsyncMessage
{
    public WebSocketAsyncMessage(string message)
    {
        Message = message;
    }

    public string Message { get; set; }
    public bool IsHandled { get; set; }
}