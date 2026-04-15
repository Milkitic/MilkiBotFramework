namespace MilkiBotFramework.Platforms.OneBot.Connecting;

public class OneBotApiException : Exception
{
    public OneBotApiException(string error, string message) : base(error + ": " + message)
    {
    }
}