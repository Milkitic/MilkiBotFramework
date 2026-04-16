namespace MilkiBotFramework.Connecting;

public interface IPlatformMessageApi : IMessageApi
{
    string PlatformId { get; }
}