using MilkiBotFramework.Platforms.OneBot.Connecting.ResponseModel;

namespace MilkiBotFramework.Platforms.OneBot.Connecting;

public interface IOneBotConnector
{
    Task<OneBotApiResponse<object>> SendMessageAsync(string action, IDictionary<string, object>? @params);
    Task<OneBotApiResponse<T>> SendMessageAsync<T>(string action, IDictionary<string, object>? @params);
}