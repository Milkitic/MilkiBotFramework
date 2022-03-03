namespace MilkiBotFramework.Messaging;

public class MessageTimeoutException : Exception
{
    public MessageTimeoutException(string message) : base(message)
    {
    }
}