using System.Text;
using System.Web;

namespace MilkiBotFramework.Utils;

public static class HttpHelperExtensions
{
    [Obsolete]
    public static string ToUrlParamString(this IDictionary<string, string>? args)
    {
        if (args == null || args.Count < 1)
            return "";

        var sb = new StringBuilder("?");
        foreach (var item in args)
            sb.Append(HttpUtility.UrlEncode(item.Key) + "=" + HttpUtility.UrlEncode(item.Value) + "&");
        sb.Remove(sb.Length - 1, 1);

        return sb.ToString();
    }
}