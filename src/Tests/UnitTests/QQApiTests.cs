using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using MilkiBotFramework;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Platforms.QQ.Connecting;
using MilkiBotFramework.Platforms.QQ.Dispatching;
using MilkiBotFramework.Platforms.QQ.Messaging;
using NSec.Cryptography;
using Xunit;

namespace UnitTests;

public class QQApiTests
{
    private const string TestAppId = "12345";
    private const string TestSecret = "qq-unit-test-placeholder";
    private const string CallbackPath = "/qq/callback";
    private const string Timestamp = "1784592000";

    [Theory]
    [InlineData(HttpStatusCode.OK, "40034100", "err_code")]
    [InlineData(HttpStatusCode.BadRequest, "40034105", "code")]
    public async Task ReturnsQqErrorCodeAndTraceId(HttpStatusCode statusCode, string code, string field)
    {
        using var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent($$"""{"{{field}}":{{code}},"message":"QQ 拒绝请求","trace_id":"body-trace"}""")
        };
        var exception = await Assert.ThrowsAsync<QApiException>(() =>
            QOpenApiResponse.ReadAsync(response, CancellationToken.None));

        Assert.Equal(int.Parse(code), exception.Code);
        Assert.Equal(statusCode, exception.StatusCode);
        Assert.Equal("body-trace", exception.TraceId);
    }

    [Fact]
    public async Task AcceptsAuditResponseAndEmptyRecallResponse()
    {
        using var audit = new HttpResponseMessage(HttpStatusCode.Accepted)
        {
            Content = new StringContent("""{"err_code":304023,"data":{"message_audit":{"audit_id":"audit-id"}}}""")
        };
        var pending = await QOpenApiResponse.ReadAsync(audit, CancellationToken.None);
        Assert.Equal("audit-id", pending?["data"]?["message_audit"]?["audit_id"]?.GetValue<string>());

        using var empty = new HttpResponseMessage(HttpStatusCode.NoContent);
        Assert.Null(await QOpenApiResponse.ReadAsync(empty, CancellationToken.None));
    }

    [Fact]
    public async Task VerifiesUnsignedHandshakeWithoutDispatchingEvent()
    {
        await using var app = WebApplication.CreateBuilder().Build();
        var (connector, middleware) = CreateMiddleware(app);
        var dispatchCount = 0;
        connector.MessageReceived += _ =>
        {
            dispatchCount++;
            return Task.CompletedTask;
        };

        var context = CreateRequest("""{"op":13,"d":{"plain_token":"platform-challenge","event_ts":"1784592000"}}""");
        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        var response = ReadPayload(context);
        Assert.Equal("platform-challenge", response?["plain_token"]?.GetValue<string>());
        Assert.Equal(Sign("1784592000platform-challenge"), response?["signature"]?.GetValue<string>());
        Assert.Equal(0, dispatchCount);
    }

    [Fact]
    public async Task LeavesOtherRoutesAndRejectsUnsignedEvents()
    {
        await using var app = WebApplication.CreateBuilder().Build();
        var (_, middleware) = CreateMiddleware(app);
        const string EventJson = """{"op":0,"t":"GROUP_ADD_ROBOT","d":{"group_openid":"group"}}""";

        var otherRoute = CreateRequest("not JSON");
        otherRoute.Request.Path = "/other";
        await middleware.InvokeAsync(otherRoute);
        Assert.Equal(StatusCodes.Status404NotFound, otherRoute.Response.StatusCode);
        Assert.Equal(0, otherRoute.Request.Body.Position);

        var unsignedEvent = CreateRequest(EventJson);
        await middleware.InvokeAsync(unsignedEvent);
        Assert.Equal(StatusCodes.Status401Unauthorized, unsignedEvent.Response.StatusCode);
    }

    [Fact]
    public async Task AcknowledgesSignedWebhookAndDeduplicatesDelivery()
    {
        await using var app = WebApplication.CreateBuilder().Build();
        var (connector, middleware) = CreateMiddleware(app);
        var dispatchCount = 0;
        connector.MessageReceived += _ =>
        {
            dispatchCount++;
            return Task.CompletedTask;
        };

        // 验签使用原始请求字节，包括 JSON 内的空白和 UTF-8 字符。
        const string EventJson = """{ "op":0,"id":"event-id","s":3,"t":"GROUP_ADD_ROBOT","d":{"group_openid":"测试"} }""";
        for (var i = 0; i < 2; i++)
        {
            var context = CreateRequest(EventJson);
            context.Request.Headers["X-Signature-Ed25519"] = Sign(Timestamp + EventJson);
            context.Request.Headers["X-Signature-Timestamp"] = Timestamp;
            await middleware.InvokeAsync(context);

            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
            var ack = ReadPayload(context);
            Assert.Equal(12, ack?["op"]?.GetValue<int>());
            Assert.Equal(0, ack?["d"]?.GetValue<int>());
        }

        Assert.Equal(1, dispatchCount);
        Assert.Equal(3, connector.MessageSequence);
    }

    [Theory]
    [InlineData("GROUP_AT_MESSAGE_CREATE", "group_id")]
    [InlineData("GROUP_MESSAGE_CREATE", "group_openid")]
    public void DispatcherSeparatesMessageAndEventIds(string eventType, string groupIdField)
    {
        var dispatcher = new TestDispatcher();
        using var context = new QMessageContext(new QMessageConverter());
        var inbound = InboundMessage.FromRawText($$"""
            {"op":0,"id":"outer-id","t":"{{eventType}}","d":{"id":"message-id","{{groupIdField}}":"group",
            "author":{"member_openid":"user"},"content":" test ","timestamp":"2026-07-21T10:00:00+08:00"} }
            """, "qq-http");

        Assert.True(dispatcher.Populate(context, inbound));
        Assert.Equal(MessageType.Channel, context.MessageIdentity?.MessageType);
        Assert.Equal("group", context.MessageIdentity?.Id);
        Assert.Equal("user", context.MessageUserIdentity?.UserId);
        Assert.Equal("message-id", context.MessageId);
        Assert.Equal("outer-id", context.EventId);
        Assert.Equal(eventType, context.EventType);
        Assert.Equal("test", context.TextMessage);
    }

    [Fact]
    public void DispatcherPreservesNoticePayload()
    {
        var dispatcher = new TestDispatcher();
        using var context = new QMessageContext(new QMessageConverter());
        var inbound = InboundMessage.FromRawText("""
            {"op":0,"id":"outer-id","t":"GROUP_MSG_RECEIVE","d":{"group_openid":"group"}}
            """, "qq-http");

        Assert.True(dispatcher.Populate(context, inbound));
        Assert.Equal(MessageType.Notice, context.MessageIdentity?.MessageType);
        Assert.Equal("outer-id", context.EventId);
        Assert.Equal("group", context.RawJsonDocument.RootElement.GetProperty("d").GetProperty("group_openid").GetString());
    }

    private static (QApiConnector Connector, QApiHttpMiddleware Middleware) CreateMiddleware(WebApplication app)
    {
        var options = new BotOptions();
        var connector = new QApiConnector(new LightHttpClient(NullLogger<LightHttpClient>.Instance, options),
            NullLogger<QApiConnector>.Instance, app)
        {
            Connection = new() { AppId = TestAppId, ClientSecret = TestSecret, CallbackPath = "https://example.test" + CallbackPath }
        };

        return (connector, new QApiHttpMiddleware(context =>
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        }, options, connector, NullLogger<QApiHttpMiddleware>.Instance));
    }

    private static DefaultHttpContext CreateRequest(string body)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = CallbackPath;
        context.Request.Headers["X-Bot-Appid"] = TestAppId;
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static JsonNode? ReadPayload(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return JsonNode.Parse(context.Response.Body);
    }

    private static string Sign(string text)
    {
        var seed = Encoding.UTF8.GetBytes(string.Concat(TestSecret, TestSecret)[..32]);
        using var key = Key.Import(SignatureAlgorithm.Ed25519, seed, KeyBlobFormat.RawPrivateKey);
        return Convert.ToHexStringLower(SignatureAlgorithm.Ed25519.Sign(key, Encoding.UTF8.GetBytes(text)));
    }

    private sealed class TestDispatcher()
        : QDispatcher(null!, null!, NullLogger<QDispatcher>.Instance, null!)
    {
        internal bool Populate(QMessageContext context, InboundMessage inbound) =>
            TryPopulateMessageContext(context, inbound, out _);
    }
}
