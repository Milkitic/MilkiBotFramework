using System.Collections.Generic;
using System.Threading.Tasks;
using MilkiBotFramework.GoCqHttp.Connecting.ResponseModel;

namespace MilkiBotFramework.GoCqHttp.Connecting;

public interface IGoCqConnector
{
    Task<GoCqWsResponse<object>> SendMessageAsync(string action, IDictionary<string, object>? @params);
    Task<GoCqWsResponse<T>> SendMessageAsync<T>(string action, IDictionary<string, object>? @params);
}