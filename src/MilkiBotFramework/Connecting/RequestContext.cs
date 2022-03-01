namespace MilkiBotFramework.Connecting;

internal class RequestContext
{
    public string RequestUri { get; set; }

    public RequestContext(string requestUri)
    {
        RequestUri = requestUri;
    }
}