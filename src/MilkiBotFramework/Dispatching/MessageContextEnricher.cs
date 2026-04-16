using Microsoft.Extensions.Logging;
using MilkiBotFramework.ContactsManaging;
using MilkiBotFramework.ContactsManaging.Models;
using MilkiBotFramework.Messaging;

namespace MilkiBotFramework.Dispatching;

public sealed class MessageContextEnricher : IMessageContextEnricher
{
    private readonly IContactsManager _contactsManager;
    private readonly IPlatformContactsManagerRouter? _contactsManagerRouter;
    private readonly BotOptions _botOptions;
    private readonly ILogger<MessageContextEnricher> _logger;

    public MessageContextEnricher(IContactsManager contactsManager,
        BotOptions botOptions,
        ILogger<MessageContextEnricher> logger,
        IPlatformContactsManagerRouter? contactsManagerRouter = null)
    {
        _contactsManager = contactsManager;
        _contactsManagerRouter = contactsManagerRouter;
        _botOptions = botOptions;
        _logger = logger;
    }

    public async Task EnrichAsync(MessageContext messageContext)
    {
        var messageIdentity = messageContext.MessageIdentity
                              ?? throw new ArgumentException("Message identity is required.", nameof(messageContext));

        switch (messageIdentity.MessageType)
        {
            case MessageType.Private:
                await FillPrivateContext(messageContext, messageIdentity);
                break;
            case MessageType.Channel:
                await FillChannelContext(messageContext, messageIdentity);
                break;
            case MessageType.Notice:
            case MessageType.Meta:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private async Task FillPrivateContext(MessageContext messageContext, MessageIdentity messageIdentity)
    {
        if (messageIdentity.Id == null)
            throw new ArgumentNullException(nameof(messageIdentity.Id));

        var contactsManager = ResolveContactsManager(messageContext);
        var privateResult = await contactsManager.TryGetOrAddPrivateInfo(messageIdentity.Id);
        if (privateResult.IsSuccess)
        {
            messageContext.Authority = _botOptions.RootAccounts.Contains(messageIdentity.Id)
                ? MessageAuthority.Root
                : MessageAuthority.Public;
            messageContext.PrivateInfo = privateResult.PrivateInfo;
            return;
        }

        _logger.LogWarning("Failed to fill PrivateInfo automatically. This may leads to further plugin errors.");
    }

    private async Task FillChannelContext(MessageContext messageContext, MessageIdentity messageIdentity)
    {
        if (messageIdentity.Id == null)
            throw new ArgumentNullException(nameof(messageIdentity.Id));

        var userId = messageContext.MessageUserIdentity?.UserId;
        if (userId == null)
            throw new ArgumentNullException(nameof(MessageUserIdentity.UserId));

        var contactsManager = ResolveContactsManager(messageContext);
        var channelResult = await contactsManager.TryGetOrAddChannelInfo(messageIdentity.Id, messageIdentity.SubId);
        var memberResult = await contactsManager.TryGetOrAddMemberInfo(messageIdentity.Id, userId, messageIdentity.SubId);

        if (channelResult.IsSuccess)
            messageContext.ChannelInfo = channelResult.ChannelInfo;
        else
            _logger.LogWarning("Failed to ChannelInfo automatically. This may leads to further plugin errors.");

        if (!memberResult.IsSuccess)
        {
            _logger.LogWarning("Failed to MemberInfo automatically. This may leads to further plugin errors.");
            return;
        }

        messageContext.MemberInfo = memberResult.MemberInfo;
        messageContext.Authority = ResolveAuthority(userId, memberResult.MemberInfo!);
    }

    private MessageAuthority ResolveAuthority(string userId, MemberInfo memberInfo)
    {
        if (_botOptions.RootAccounts.Contains(userId))
            return MessageAuthority.Root;

        return memberInfo.MemberRole switch
        {
            MemberRole.Admin => MessageAuthority.Admin,
            MemberRole.SubAdmin => MessageAuthority.SubAdmin,
            _ => MessageAuthority.Public
        };
    }

    private IContactsManager ResolveContactsManager(MessageContext messageContext)
    {
        return _contactsManagerRouter?.ResolveRequired(messageContext) ?? _contactsManager;
    }
}