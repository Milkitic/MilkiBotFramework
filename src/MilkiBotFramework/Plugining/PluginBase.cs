using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Messaging.RichMessages;

namespace MilkiBotFramework.Plugining;

public abstract class PluginBase
{
    protected internal PluginMetadata Metadata { get; internal set; }
    public bool IsInitialized { get; internal set; }
    public abstract PluginType PluginType { get; }

    protected static IResponse Handled() => new MessageResponse(true);
    protected static IResponse Reply(string message, out IAsyncMessage nextMessage, bool reply = true) => Reply(new Text(message), out nextMessage, reply);
    protected static IResponse Reply(IRichMessage message, out IAsyncMessage nextMessage, bool reply = true)
    {
        var nextResponse = new NextMessage();
        nextMessage = nextResponse;
        return new MessageResponse(message, reply) { NextMessage = nextResponse };
    }

    protected static IResponse Reply(string message, bool reply = true) => Reply(new Text(message), reply);
    protected static IResponse Reply(IRichMessage message, bool reply = true) => new MessageResponse(message, reply);
    protected static IResponse ToPrivate(string userId, string message) => ToPrivate(userId, new Text(message));
    protected static IResponse ToPrivate(string userId, IRichMessage message) => new MessageResponse(userId, null, message, MessageType.Private);
    protected static IResponse ToChannel(string channelId, string message, string? subChannelId = null, string? atId = null) => ToChannel(channelId, new Text(message), subChannelId, atId);
    protected static IResponse ToChannel(string channelId, IRichMessage message, string? subChannelId = null, string? atId = null) => ((IResponse)new MessageResponse(channelId, subChannelId, message, MessageType.Channel)).At(atId);

    protected internal virtual Task OnInitialized() => Task.CompletedTask;
    protected internal virtual Task OnUninitialized() => Task.CompletedTask;
    protected internal virtual Task OnExecuting() => Task.CompletedTask;
    protected internal virtual Task OnExecuted() => Task.CompletedTask;

    protected Task<T> ReadValueAsync<T>(string key)
    {
        throw new NotImplementedException();
    }

    protected Task<T[]> ReadArrayAsync<T>(string key)
    {
        throw new NotImplementedException();
    }

    protected Task WriteValueAsync(string key, object value)
    {
        throw new NotImplementedException();
    }

    protected Task WriteArrayAsync<T>(string key, IEnumerable<T> value)
    {
        throw new NotImplementedException();
    }
}