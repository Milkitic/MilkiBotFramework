using MilkiBotFramework.ContactsManaging;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Plugining;

namespace MilkiBotFramework.Dispatching;

public sealed class MessageDispatchCoordinator
{
    private readonly IContactsManager _contactsManager;
    private readonly IPlatformContactsManagerRouter? _contactsManagerRouter;
    private readonly MessageDispatchNotifier _messageDispatchNotifier;
    private readonly PluginRuntime _pluginRuntime;

    public MessageDispatchCoordinator(IContactsManager contactsManager,
        MessageDispatchNotifier messageDispatchNotifier,
        PluginRuntime pluginRuntime,
        IPlatformContactsManagerRouter? contactsManagerRouter = null)
    {
        _contactsManager = contactsManager;
        _contactsManagerRouter = contactsManagerRouter;
        _pluginRuntime = pluginRuntime;
        _messageDispatchNotifier = messageDispatchNotifier;
    }

    public async Task DispatchAsync(MessageContext messageContext)
    {
        var contactsManager = _contactsManagerRouter?.ResolveRequired(messageContext) ?? _contactsManager;
        await contactsManager.HandleMessageAsync(messageContext);
        await _pluginRuntime.HandleMessageAsync(messageContext);
        await _messageDispatchNotifier.NotifyAsync(messageContext);
    }
}