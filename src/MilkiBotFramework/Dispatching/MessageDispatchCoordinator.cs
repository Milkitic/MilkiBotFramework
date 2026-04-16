using MilkiBotFramework.ContactsManaging;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Plugining;

namespace MilkiBotFramework.Dispatching;

public sealed class MessageDispatchCoordinator
{
    private readonly IContactsManager _contactsManager;
    private readonly MessageDispatchNotifier _messageDispatchNotifier;
    private readonly PluginRuntime _pluginRuntime;

    public MessageDispatchCoordinator(IContactsManager contactsManager,
        MessageDispatchNotifier messageDispatchNotifier,
        PluginRuntime pluginRuntime)
    {
        _contactsManager = contactsManager;
        _pluginRuntime = pluginRuntime;
        _messageDispatchNotifier = messageDispatchNotifier;
    }

    public async Task DispatchAsync(MessageContext messageContext)
    {
        await _contactsManager.HandleMessageAsync(messageContext);
        await _pluginRuntime.HandleMessageAsync(messageContext);
        await _messageDispatchNotifier.NotifyAsync(messageContext);
    }
}