using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Messaging.RichMessages;

namespace MilkiBotFramework.Plugining;

public sealed class PluginResponseDispatcher
{
    private readonly ILogger<PluginResponseDispatcher> _logger;
    private readonly IMessageApi _messageApi;
    private readonly IRichMessageConverter _richMessageConverter;
    private readonly BotOptions _botOptions;

    public PluginResponseDispatcher(ILogger<PluginResponseDispatcher> logger,
        IMessageApi messageApi,
        IRichMessageConverter richMessageConverter,
        BotOptions botOptions)
    {
        _logger = logger;
        _messageApi = messageApi;
        _richMessageConverter = richMessageConverter;
        _botOptions = botOptions;
    }

    public async Task DispatchAsync(MessageContext messageContext, IResponse response)
    {
        var outgoingMessage = PrepareOutgoingMessage(messageContext, response);
        var plainMessage = await _richMessageConverter.EncodeAsync(outgoingMessage);

        if (response.Id == null)
        {
            var identity = messageContext.MessageIdentity;
            if (identity != null &&
                identity != MessageIdentity.MetaMessage &&
                identity != MessageIdentity.NoticeMessage)
            {
                await SendAsync(identity.Id!, identity.MessageType, plainMessage, response.Message, messageContext, identity.SubId);
            }
            else
            {
                _logger.LogWarning("Fail to reply: destination undefined.");
            }

            return;
        }

        if (response.MessageType == MessageType.Private)
        {
            await _messageApi.SendPrivateMessageAsync(response.Id, plainMessage, response.Message, messageContext);
        }
        else if (response.MessageType == MessageType.Channel)
        {
            await _messageApi.SendChannelMessageAsync(response.Id, plainMessage, response.Message, messageContext, response.SubId);
        }
        else
        {
            _logger.LogWarning("Send failed: destination undefined.");
        }
    }

    private async Task SendAsync(string id,
        MessageType messageType,
        string plainMessage,
        IRichMessage? richMessage,
        MessageContext messageContext,
        string? subId)
    {
        if (messageType == MessageType.Private)
        {
            await _messageApi.SendPrivateMessageAsync(id, plainMessage, richMessage, messageContext);
            return;
        }

        await _messageApi.SendChannelMessageAsync(id, plainMessage, richMessage, messageContext, subId);
    }

    private IRichMessage PrepareOutgoingMessage(MessageContext messageContext, IResponse response)
    {
        ReplaceContentIfPossible(response);

        var outgoingMessage = response.Message ?? new Text("");
        if (response.Id == null)
        {
            var identity = messageContext.MessageIdentity;
            if (identity?.MessageType == MessageType.Channel &&
                response.TryReply == true &&
                outgoingMessage is not RichMessage { FirstIsReply: true } &&
                outgoingMessage is not Reply)
            {
                outgoingMessage = new RichMessage(new Reply(messageContext.MessageId!), outgoingMessage);
                response.Message = outgoingMessage;
            }
        }
        else if (response is { MessageType: MessageType.Channel, TryAt: not null } &&
                 (outgoingMessage is not RichMessage richMessage || !richMessage.FirstIsAt(response.TryAt)) &&
                 (outgoingMessage is not At at || at.UserId != response.TryAt))
        {
            outgoingMessage = new RichMessage(new At(response.TryAt), outgoingMessage);
            response.Message = outgoingMessage;
        }

        return outgoingMessage;
    }

    private void ReplaceContentIfPossible(IResponse response)
    {
        if (_botOptions.Variables.Count <= 0) return;
        switch (response.Message)
        {
            case Text text:
                ReplaceContent(text);
                break;
            case RichMessage richMessage:
                foreach (var message in richMessage)
                {
                    if (message is not Text textMessage) continue;
                    ReplaceContent(textMessage);
                }

                break;
        }
    }

    private void ReplaceContent(Text text)
    {
        if (text.Content == null!) return;

        var index = text.Content.IndexOf("${", StringComparison.Ordinal);
        if (index < 0 || text.Content.IndexOf('}', index) < 0) return;

        foreach (var (key, value) in _botOptions.Variables)
        {
            text.Content = text.Content.Replace($"${{{key}}}", value);
        }
    }
}