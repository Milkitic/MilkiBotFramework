using MilkiBotFramework.ContactsManaging;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Plugining;

namespace MilkiBotFramework.Dispatching;

public sealed class MessageDispatchCoordinator
{
    private readonly IContactsManager _contactsManager;
    private readonly PluginManager _pluginManager;
    private readonly MessageDispatchNotifier _messageDispatchNotifier;

    public MessageDispatchCoordinator(IContactsManager contactsManager,
        PluginManager pluginManager,
        MessageDispatchNotifier messageDispatchNotifier)
    {
        _contactsManager = contactsManager;
        _pluginManager = pluginManager;
        _messageDispatchNotifier = messageDispatchNotifier;
    }

    public async Task DispatchAsync(MessageContext messageContext)
    {
        await _contactsManager.HandleMessageAsync(messageContext);
        await _pluginManager.HandleMessageAsync(messageContext);
        await _messageDispatchNotifier.NotifyAsync(messageContext);
    }
}