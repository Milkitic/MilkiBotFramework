using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Messaging.RichMessages;
using MilkiBotFramework.Platforms.Mock;
using MilkiBotFramework.Platforms.Mock.Connecting;
using MilkiBotFramework.Platforms.Mock.Messaging;
using Xunit;

namespace UnitTests;

public class ProactiveMessageApiTests
{
    [Fact]
    public async Task MockMessageApi_SendChannelAndGroupAndPrivateAsync_RecordsMessagesAndFiresEvent()
    {
        MockMessageApi.ClearSentMessages();
        var received = new List<MockMessage>();
        void Handler(MockMessage msg) => received.Add(msg);
        MockMessageApi.MessageSent += Handler;

        try
        {
            var options = new MockBotOptions();
            var connector = new MockConnector(options);
            var api = new MockMessageApi(options, connector);

            var id1 = await ((IMessageApi)api).SendChannelMessageAsync("group-1", "hello channel");
            var id2 = await api.SendGroupMessageAsync("group-2", new Text("hello rich group"));
            var id3 = await api.SendPrivateMessageAsync("user-1", "hello private");

            Assert.False(string.IsNullOrWhiteSpace(id1));
            Assert.False(string.IsNullOrWhiteSpace(id2));
            Assert.False(string.IsNullOrWhiteSpace(id3));

            var sent = MockMessageApi.GetSentMessages();
            Assert.Equal(3, sent.Count);
            Assert.Equal(3, received.Count);

            Assert.Equal("group-1", sent[0].GroupId);
            Assert.Equal("hello channel", sent[0].Content);
            Assert.Equal("group-2", sent[1].GroupId);
            Assert.Equal("hello rich group", sent[1].Content);
            Assert.Null(sent[2].GroupId);
            Assert.Equal("hello private", sent[2].Content);
        }
        finally
        {
            MockMessageApi.MessageSent -= Handler;
            MockMessageApi.ClearSentMessages();
        }
    }

    [Fact]
    public async Task PlatformMessageApiRouter_ProactiveChannelSend_RoutesSinglePlatformAndRejectsMultiPlatform()
    {
        MockMessageApi.ClearSentMessages();
        try
        {
            var singleRouter = new PlatformMessageApiRouter([new StubPlatformApi("p1")]);
            var result = await singleRouter.SendChannelMessageAsync("ch-1", "hi");
            Assert.Equal("p1:ch-1:hi", result);

            var multiRouter = new PlatformMessageApiRouter([new StubPlatformApi("p1"), new StubPlatformApi("p2")]);
            await Assert.ThrowsAsync<NotSupportedException>(() => multiRouter.SendChannelMessageAsync("ch-1", "hi"));
        }
        finally
        {
            MockMessageApi.ClearSentMessages();
        }
    }

    [Fact]
    public async Task DefaultInterfaceMethod_ThrowsNotSupportedException()
    {
        IMessageApi api = new MinimalMessageApi();
        await Assert.ThrowsAsync<NotSupportedException>(() => api.SendChannelMessageAsync("ch-1", "hi"));
        await Assert.ThrowsAsync<NotSupportedException>(() => api.SendChannelMessageAsync("ch-1", new Text("hi")));
    }

    private sealed class MinimalMessageApi : IMessageApi
    {
        public bool Supports(MessageContext messageContext) => false;
        public Task<string> SendPrivateMessageAsync(string userId, string message, IRichMessage? richMessage, MessageContext messageContext) => Task.FromResult("");
        public Task<string> SendChannelMessageAsync(string channelId, string message, IRichMessage? richMessage, MessageContext messageContext, string? subChannelId) => Task.FromResult("");
    }

    private sealed class StubPlatformApi(string platformId) : IPlatformMessageApi
    {
        public string PlatformId => platformId;
        public bool Supports(MessageContext messageContext) => false;
        public Task<string> SendPrivateMessageAsync(string userId, string message, IRichMessage? richMessage, MessageContext messageContext) => Task.FromResult("");
        public Task<string> SendChannelMessageAsync(string channelId, string message, IRichMessage? richMessage, MessageContext messageContext, string? subChannelId) => Task.FromResult("");
        public Task<string> SendChannelMessageAsync(string channelId, string content, CancellationToken cancellationToken = default)
            => Task.FromResult($"{PlatformId}:{channelId}:{content}");
    }
}
