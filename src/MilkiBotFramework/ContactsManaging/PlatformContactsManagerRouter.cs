using MilkiBotFramework.ContactsManaging.Models;
using MilkiBotFramework.ContactsManaging.Results;
using MilkiBotFramework.Messaging;

namespace MilkiBotFramework.ContactsManaging;

public sealed class PlatformContactsManagerRouter : IContactsManager, IPlatformContactsManagerRouter, IContextualContactsManager
{
    private readonly IPlatformContactsManager[] _contactsManagers;

    public PlatformContactsManagerRouter(IEnumerable<IPlatformContactsManager> contactsManagers)
    {
        _contactsManagers = contactsManagers.ToArray();
    }

    public event Func<ContactsUpdateEvent, Task>? ContactsUpdated
    {
        add
        {
            foreach (var manager in _contactsManagers)
            {
                manager.ContactsUpdated += value;
            }
        }
        remove
        {
            foreach (var manager in _contactsManagers)
            {
                manager.ContactsUpdated -= value;
            }
        }
    }

    public void InitializeTasks()
    {
        foreach (var manager in _contactsManagers)
        {
            manager.InitializeTasks();
        }
    }

    public Task HandleMessageAsync(MessageContext messageContext)
    {
        return ResolveRequired(messageContext).HandleMessageAsync(messageContext);
    }

    public Task<SelfInfoResult> TryGetOrUpdateSelfInfo()
    {
        return ResolveSingle().TryGetOrUpdateSelfInfo();
    }

    public Task<SelfInfoResult> TryGetOrUpdateSelfInfo(MessageContext messageContext)
    {
        return ResolveContextual(messageContext).TryGetOrUpdateSelfInfo(messageContext);
    }

    public Task<MemberInfoResult> TryGetOrAddMemberInfo(string channelId, string userId, string? subChannelId = null)
    {
        return ResolveSingle().TryGetOrAddMemberInfo(channelId, userId, subChannelId);
    }

    public Task<MemberInfoResult> TryGetOrAddMemberInfo(MessageContext messageContext, string channelId, string userId,
        string? subChannelId = null)
    {
        return ResolveContextual(messageContext).TryGetOrAddMemberInfo(messageContext, channelId, userId, subChannelId);
    }

    public Task<ChannelInfoResult> TryGetOrAddChannelInfo(string channelId, string? subChannelId = null)
    {
        return ResolveSingle().TryGetOrAddChannelInfo(channelId, subChannelId);
    }

    public Task<ChannelInfoResult> TryGetOrAddChannelInfo(MessageContext messageContext, string channelId,
        string? subChannelId = null)
    {
        return ResolveContextual(messageContext).TryGetOrAddChannelInfo(messageContext, channelId, subChannelId);
    }

    public Task<PrivateInfoResult> TryGetOrAddPrivateInfo(string userId)
    {
        return ResolveSingle().TryGetOrAddPrivateInfo(userId);
    }

    public Task<PrivateInfoResult> TryGetOrAddPrivateInfo(MessageContext messageContext, string userId)
    {
        return ResolveContextual(messageContext).TryGetOrAddPrivateInfo(messageContext, userId);
    }

    public IEnumerable<ChannelInfo> GetAllChannels()
    {
        return _contactsManagers.SelectMany(manager => manager.GetAllChannels());
    }

    public IEnumerable<MemberInfo> GetAllMembers(string channelId, string? subChannelId = null)
    {
        return ResolveSingle().GetAllMembers(channelId, subChannelId);
    }

    public IEnumerable<PrivateInfo> GetAllPrivates()
    {
        return _contactsManagers.SelectMany(manager => manager.GetAllPrivates());
    }

    public IPlatformContactsManager ResolveRequired(MessageContext messageContext)
    {
        return _contactsManagers.FirstOrDefault(manager => manager.Supports(messageContext))
               ?? throw new InvalidOperationException($"No contacts manager registered for platform '{messageContext.PlatformId ?? "unknown"}'.");
    }

    private IPlatformContactsManager ResolveSingle()
    {
        if (_contactsManagers.Length == 1)
        {
            return _contactsManagers[0];
        }

        throw new InvalidOperationException("Multiple contacts managers are registered. Use the platform-aware API instead.");
    }

    private IContextualContactsManager ResolveContextual(MessageContext messageContext)
    {
        var contactsManager = ResolveRequired(messageContext);
        if (contactsManager is IContextualContactsManager contextualContactsManager)
        {
            return contextualContactsManager;
        }

        throw new InvalidOperationException($"Contacts manager for platform '{messageContext.PlatformId ?? "unknown"}' does not support message-context-aware access.");
    }
}