namespace MilkiBotFramework.Dispatching
{
    public interface IDispatcher
    {
        Task InvokeRawMessageReceived(string rawMessage);
    }
}
