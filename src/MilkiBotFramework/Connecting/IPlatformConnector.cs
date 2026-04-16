namespace MilkiBotFramework.Connecting;

public interface IPlatformConnector : IConnector
{
    string PlatformId { get; }
}