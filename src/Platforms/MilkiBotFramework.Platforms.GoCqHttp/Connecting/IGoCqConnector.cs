using MilkiBotFramework.Platforms.GoCqHttp.Connecting.ResponseModel;

namespace MilkiBotFramework.Platforms.GoCqHttp.Connecting;

public interface IGoCqConnector
{
    Task<GoCqApiResponse<object>> SendMessageAsync(string action, IDictionary<string, object>? @params);
    Task<GoCqApiResponse<T>> SendMessageAsync<T>(string action, IDictionary<string, object>? @params);
}