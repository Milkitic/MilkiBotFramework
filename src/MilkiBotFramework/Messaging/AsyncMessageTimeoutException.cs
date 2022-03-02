namespace MilkiBotFramework.Messaging;

public class AsyncMessageTimeoutException : MessageTimeoutException
{
    public AsyncMessageTimeoutException(string message) : base(message)
    {
    }
}