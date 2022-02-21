using System;
using System.Collections.Generic;
using System.Text;

namespace MilkiBotFramework.Utils;

internal static class HttpHelperExtensions
{
    public static string ToUrlParamString(this IDictionary<string, string>? args)
    {
        if (args == null || args.Count < 1)
            return "";

        var sb = new StringBuilder("?");
        foreach (var item in args)
            sb.Append(item.Key + "=" + item.Value + "&");
        sb.Remove(sb.Length - 1, 1);

        return sb.ToString();
    }

    public static string GetContentType(this HttpHelper.HttpContentType type)
    {
        return type switch
        {
            HttpHelper.HttpContentType.Json => "application/json",
            HttpHelper.HttpContentType.Form => "application/x-www-form-urlencoded",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }
}