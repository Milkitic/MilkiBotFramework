using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.ContactsManaging;
using MilkiBotFramework.Dispatching;
using MilkiBotFramework.Event;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Platforms.QQ.Messaging;

namespace MilkiBotFramework.Platforms.QQ.Dispatching;

public class QDispatcher : DispatcherBase<QMessageContext>
{
    public QDispatcher(IConnector connector,
        IContactsManager contactsManager,
        ILogger<QDispatcher> logger,
        IServiceProvider serviceProvider,
        BotOptions botOptions,
        EventBus eventBus)
        : base(connector, contactsManager, logger, serviceProvider, botOptions, eventBus)
    {
    }

    protected override bool TrySetTextMessage(QMessageContext messageContext)
    {
        if (messageContext.MessageIdentity == MessageIdentity.MetaMessage ||
            messageContext.MessageIdentity == MessageIdentity.NoticeMessage) return false;
        //messageContext.TextMessage = messageContext.RawMessage.Message;
        throw new NotImplementedException();
        return true;
    }

    protected override bool TryGetIdentityByRawMessage(QMessageContext messageContext,
        [NotNullWhen(true)] out MessageIdentity? messageIdentity,
        out string? strIdentity)
    {
        var rawJson = messageContext.RawTextMessage;
        strIdentity = null;

        var jDoc = JsonDocument.Parse(rawJson);
        var hasProperty = jDoc.RootElement.TryGetProperty("post_type", out var postTypeElement);
        if (!hasProperty)
        {
            messageIdentity = null;
            return false;
        }

        throw new NotImplementedException();
    }
}