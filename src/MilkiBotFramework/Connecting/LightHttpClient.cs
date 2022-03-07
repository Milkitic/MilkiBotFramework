using System.Buffers;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MilkiBotFramework.Imaging;
using MilkiBotFramework.Utils;
using SixLabors.ImageSharp;

namespace MilkiBotFramework.Connecting;

public class LightHttpClient
{
    private enum RequestMethod
    {
        Get, Post, Put, Delete
    }

    static LightHttpClient()
    {
        ServicePointManager.ServerCertificateValidationCallback = (_, _, _, _) => true;
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            _httpClient?.Dispose();
        };
        _httpClient = null!;
    }

    private static HttpClient _httpClient;
    private readonly LightHttpClientCreationOptions _clientCreationOptions;

    public LightHttpClient(LightHttpClientCreationOptions clientCreationOptions)
    {
        _clientCreationOptions = clientCreationOptions;
        if (_httpClient != null!) return;
        HttpMessageHandler handler;
        if (clientCreationOptions.ProxyUrl != null)
        {
            handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip
            };
        }
        else
        {
            handler = new SocketsHttpHandler
            {
                Proxy = new WebProxy(clientCreationOptions.ProxyUrl),
                AutomaticDecompression = DecompressionMethods.GZip
            };
        }

        _httpClient = new HttpClient(handler) { Timeout = clientCreationOptions.Timeout };
    }


    public async Task<T> HttpGet<T>(
        string url,
        IReadOnlyDictionary<string, string>? queries = null,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        return await SendAsync<T>(url, queries, null, headers, RequestMethod.Get);
    }

    /// <summary>
    /// DELETE with value-pairs.
    /// </summary>
    /// <param name="url">Http uri.</param>
    /// <param name="queries">Parameter dictionary.</param>
    /// <param name="headers">Header dictionary.</param>
    /// <returns></returns>
    public async Task<T> HttpDelete<T>(
        string url,
        IReadOnlyDictionary<string, string>? queries = null,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        return await SendAsync<T>(url, queries, null, headers, RequestMethod.Delete);
    }

    /// <summary>
    /// POST with Json.
    /// </summary>
    /// <param name="url">Http uri.</param>
    /// <param name="obj">object</param>
    /// <param name="headers">Header dictionary.</param>
    /// <returns></returns>
    public async Task<T> HttpPost<T>(string url, object obj,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        HttpContent content = new StringContent(JsonSerializer.Serialize(obj));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return await SendAsync<T>(url, null, content, headers, RequestMethod.Post);
    }

    /// <summary>
    /// POST with Json.
    /// </summary>
    /// <param name="url">Http uri.</param>
    /// <param name="obj">object</param>
    /// <param name="headers">Header dictionary.</param>
    /// <returns></returns>
    public async Task<T> HttpPut<T>(string url, object obj,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        HttpContent content = new StringContent(JsonSerializer.Serialize(obj));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return await SendAsync<T>(url, null, content, headers, RequestMethod.Put);
    }

    public async Task<(byte[], ImageType)> GetImageBytesFromUrlAsync(string uri)
    {
        byte[] urlContents = await _httpClient.GetByteArrayAsync(uri);
        var type = ImageHelper.GetKnownImageType(urlContents);
        return (urlContents, type);
    }

    public async Task<(Image, ImageType)> GetImageFromUrlAsync(string uri)
    {
        byte[] urlContents = await _httpClient.GetByteArrayAsync(uri);
        var type = ImageHelper.GetKnownImageType(urlContents);
        var ms = new MemoryStream(urlContents);
        return (await Image.LoadAsync(ms), type);
    }

    public async Task<string> SaveImageFromUrlAsync(string uri, string saveDir, string filename)
    {
        byte[] urlContents = await _httpClient.GetByteArrayAsync(uri);
        var type = ImageHelper.GetKnownImageType(urlContents);
        string ext = type switch
        {
            ImageType.Jpeg => ".jpg",
            ImageType.Png => ".png",
            ImageType.Gif => ".gif",
            ImageType.Bmp => ".bmp",
            _ => ""
        };

        string fullname = Path.Combine(saveDir, filename + ext);
        await File.WriteAllBytesAsync(fullname, urlContents);

        return new FileInfo(fullname).FullName;
    }

    private async Task<T> SendAsync<T>(
        string url,
        IReadOnlyDictionary<string, string>? args,
        HttpContent? content,
        IReadOnlyDictionary<string, string>? argsHeader,
        RequestMethod requestMethod)
    {
        var context = new RequestContext(url + BuildQueries(args));
        return await RunWithRetry(context, async () =>
        {
            var uri = context.RequestUri;
            var request = requestMethod switch
            {
                RequestMethod.Get => new HttpRequestMessage(HttpMethod.Get, uri),
                RequestMethod.Delete => new HttpRequestMessage(HttpMethod.Delete, uri),
                RequestMethod.Post => new HttpRequestMessage(HttpMethod.Post, uri),
                RequestMethod.Put => new HttpRequestMessage(HttpMethod.Put, uri),
                _ => throw new ArgumentOutOfRangeException(nameof(requestMethod), requestMethod, null)
            };

            if (content != null)
            {
                request.Content = content;
            }

            if (argsHeader != null)
            {
                foreach (var (key, value) in argsHeader)
                {
                    request.Headers.TryAddWithoutValidation(key, value);
                }
            }

            HttpResponseMessage response;
            using (var cts = new CancellationTokenSource(_clientCreationOptions.Timeout))
            {
                response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
                    cts.Token);
            }

            try
            {
                if (response.RequestMessage is { RequestUri: { } })
                    context.RequestUri = response.RequestMessage.RequestUri.ToString();
                response.EnsureSuccessStatusCode();

                await using var responseStream = await response.Content.ReadAsStreamAsync();
                return (await JsonSerializer.DeserializeAsync<T>(responseStream))!;
            }
            finally
            {
                response.Dispose();
            }
        });
    }

    private async Task<T> RunWithRetry<T>(RequestContext context, Func<Task<T>> func)
    {
        for (int i = 0; i < _clientCreationOptions.RetryCount; i++)
        {
            var uri = context.RequestUri;
            try
            {
                return await func();
            }
            catch (Exception ex)
            {
                if (context.RequestUri != uri)
                {
                    i--;
                }
                else
                {
                    Debug.WriteLine(string.Format("Tried {0} time{1}. (>{2}ms): {3}",
                        i + 1,
                        i + 1 > 1 ? "s" : "",
                        _clientCreationOptions.Timeout,
                        context.RequestUri)
                    );
                }

                if (ex is HttpRequestException httpRequestException)
                {
                    if (httpRequestException.StackTrace?.Contains("EnsureSuccessStatusCode") == true)
                    {
                        throw;
                    }
                }

                if (i == _clientCreationOptions.RetryCount - 1)
                    throw;
            }
        }

        throw new Exception("HttpRequest not success");
    }

    public static string? BuildQueries(IReadOnlyDictionary<string, string>? args)
    {
        if (args == null || args.Count < 1)
            return null;

        var sb = new StringBuilder("?");
        int i = 0;
        foreach (var (key, value) in args)
        {
            if (i > 0) sb.Append('&');

            if (key.Length < 65520)
                sb.Append(Uri.EscapeDataString(key));
            else
                WriteEncoded(sb, key);
            sb.Append('=');
            if (value.Length < 65520)
                sb.Append(Uri.EscapeDataString(value));
            else
                WriteEncoded(sb, value);
            i++;
        }

        return sb.ToString();
    }

    private static void WriteEncoded(StringBuilder sb, string content)
    {
        var maxCharLength = content.Length * 12;
        char[]? bytesRent = null;
        Span<char> chars = maxCharLength <= FrameworkConstants.MaxStackArrayLength
            ? stackalloc char[maxCharLength]
            : bytesRent = ArrayPool<char>.Shared.Rent(maxCharLength);
        try
        {
            if (bytesRent != null) chars = bytesRent.AsSpan(0, maxCharLength);
            var count = HttpEncoder.UrlEncode(content, chars);
            sb.Append(chars[..count]);
        }
        finally
        {
            if (bytesRent != null) ArrayPool<char>.Shared.Return(bytesRent);
        }
    }
}