using System;

namespace MilkiBotFramework.GoCqHttp.Connecting;

public class GoCqApiException : Exception
{
    public GoCqApiException(string error, string message) : base(error + ": " + message)
    {
    }
}