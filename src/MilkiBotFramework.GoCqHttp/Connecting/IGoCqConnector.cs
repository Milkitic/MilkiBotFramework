using System.Collections.Generic;
using System.Threading.Tasks;
using MilkiBotFramework.Platforms.GoCqHttp.Connecting.ResponseModel;

namespace MilkiBotFramework.Platforms.GoCqHttp.Connecting;

public interface IGoCqConnector
{
    Task<GoCqWsResponse<object>> SendMessageAsync(string action, IDictionary<string, object>? @params);
    Task<GoCqWsResponse<T>> SendMessageAsync<T>(string action, IDictionary<string, object>? @params);
}