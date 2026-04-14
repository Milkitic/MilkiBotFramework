using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.ContactsManaging;
using MilkiBotFramework.ContactsManaging.Models;
using MilkiBotFramework.ContactsManaging.Results;
using MilkiBotFramework.Event;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Tasking;

namespace MilkiBotFramework.Platforms.Discord.ContactsManaging;

public class DiscordContactsManager : ContactsManagerBase
{
    public DiscordContactsManager(BotTaskScheduler botTaskScheduler,
        ILogger<DiscordContactsManager> logger,
        EventBus eventBus) : base(botTaskScheduler, logger, eventBus)
    {
    }

    public override Task<PrivateInfoResult> TryGetOrAddPrivateInfo(string userId)
    {
        return Task.FromResult(new PrivateInfoResult
        {
            IsSuccess = true,
            PrivateInfo = new PrivateInfo(userId) { Nickname = userId }
        });
    }

    protected override bool GetContactsUpdateInfo(MessageContext messageContext, out ContactsUpdateInfo? updateInfo)
    {
        updateInfo = null;
        return false;
    }

    protected override bool GetContactsCore(
        [NotNullWhen(true)] out Dictionary<string, ChannelInfo>? channels,
        [NotNullWhen(true)] out Dictionary<string, ChannelInfo>? subChannels,
        [NotNullWhen(true)] out Dictionary<string, PrivateInfo>? privates)
    {
        channels = null;
        subChannels = null;
        privates = null;
        return false;
    }
}