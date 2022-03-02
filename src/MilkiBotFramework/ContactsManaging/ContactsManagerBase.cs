using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.ContactsManaging.Models;
using MilkiBotFramework.ContactsManaging.Results;
using MilkiBotFramework.Dispatching;
using MilkiBotFramework.Event;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Tasking;

namespace MilkiBotFramework.ContactsManaging;

public abstract class ContactsManagerBase : IContactsManager
{
    private readonly BotTaskScheduler _botTaskScheduler;
    private readonly ILogger _logger;
    private readonly EventBus _eventBus;
    private IDispatcher? _dispatcher;

    protected SelfInfo? SelfInfo;

    protected readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ChannelInfo>> SubChannelMapping = new();
    protected readonly ConcurrentDictionary<string, ChannelInfo> ChannelMapping = new();
    protected readonly ConcurrentDictionary<string, PrivateInfo> PrivateMapping = new();

    protected readonly ConcurrentDictionary<string, Avatar> UserAvatarMapping = new();
    protected readonly ConcurrentDictionary<string, Avatar> ChannelAvatarMapping = new();

    public ContactsManagerBase(BotTaskScheduler botTaskScheduler, ILogger logger, EventBus eventBus)
    {
        _botTaskScheduler = botTaskScheduler;
        _logger = logger;
        _eventBus = eventBus;
        _eventBus.Subscribe<DispatchMessageEvent>(OnEventReceived);
    }

    public void Initialize()
    {
        _botTaskScheduler.AddTask("RefreshContractsTask", builder => builder
            .ByInterval(TimeSpan.FromMinutes(5))
            .AtStartup()
            .Do(RefreshContracts));
    }

    private void RefreshContracts(TaskContext context, CancellationToken token)
    {
        _logger.LogInformation("Refreshed!");
    }

    public virtual Task<SelfInfoResult> TryGetOrUpdateSelfInfo()
    {
        if (SelfInfo == null) return Task.FromResult(SelfInfoResult.Fail);
        return Task.FromResult(new SelfInfoResult { IsSuccess = true, SelfInfo = SelfInfo });
    }

    public virtual Task<MemberInfoResult> TryGetOrAddMemberInfo(string channelId, string userId, string? subChannelId = null)
    {
        if (subChannelId == null)
        {
            if (ChannelMapping.TryGetValue(channelId, out var channelInfo) &&
                channelInfo.Members.TryGetValue(userId, out var memberInfo))
            {
                return Task.FromResult(new MemberInfoResult
                {
                    IsSuccess = true,
                    MemberInfo = memberInfo
                });
            }
        }
        else
        {
            if (SubChannelMapping.TryGetValue(channelId, out var subChannels) &&
                subChannels.TryGetValue(channelId, out var channelInfo) &&
                channelInfo.Members.TryGetValue(userId, out var memberInfo))
            {
                return Task.FromResult(new MemberInfoResult
                {
                    IsSuccess = true,
                    MemberInfo = memberInfo
                });
            }
        }

        return Task.FromResult(MemberInfoResult.Fail);
    }

    public virtual Task<ChannelInfoResult> TryGetOrAddChannelInfo(string channelId, string? subChannelId = null)
    {
        return GetChannelOrSubChannel(channelId, subChannelId, out var channelInfo)
            ? Task.FromResult(new ChannelInfoResult
            {
                IsSuccess = true,
                ChannelInfo = channelInfo
            })
            : Task.FromResult(ChannelInfoResult.Fail);
    }

    public virtual Task<PrivateInfoResult> TryGetOrAddPrivateInfo(string userId)
    {
        if (PrivateMapping.TryGetValue(userId, out var privateInfo))
        {
            return Task.FromResult(new PrivateInfoResult
            {
                IsSuccess = true,
                PrivateInfo = privateInfo
            });
        }

        return Task.FromResult(PrivateInfoResult.Fail);
    }

    protected virtual Task<ContractUpdateResult> UpdateMemberIfPossible(MessageContext messageContext)
    {
        return Task.FromResult(ContractUpdateResult.Fail);
    }

    protected virtual Task<ContractUpdateResult> UpdateChannelsIfPossible(MessageContext messageContext)
    {
        return Task.FromResult(ContractUpdateResult.Fail);
    }

    protected virtual Task<ContractUpdateResult> UpdatePrivatesIfPossible(MessageContext messageContext)
    {
        return Task.FromResult(ContractUpdateResult.Fail);
    }

    private async Task OnEventReceived(DispatchMessageEvent e)
    {
        if (e.MessageType != MessageType.Notice) return;

        var messageContext = e.MessageContext;
        var updateResult = await UpdateMemberIfPossible(messageContext);
        if (updateResult.IsSuccess)
        {
            _logger.LogInformation("Member " + updateResult.ContractUpdateType + ": " + updateResult.Id);
            return;
        }

        updateResult = await UpdateChannelsIfPossible(messageContext);
        if (updateResult.IsSuccess)
        {
            _logger.LogInformation("Channel " + updateResult.ContractUpdateType + ": " + updateResult.Id);
            return;
        }

        updateResult = await UpdatePrivatesIfPossible(messageContext);
        if (updateResult.IsSuccess)
        {
            _logger.LogInformation("Private " + updateResult.ContractUpdateType + ": " + updateResult.Id);
        }
    }

    public IEnumerable<ChannelInfo> GetAllChannels()
    {
        return ChannelMapping.Values;
    }

    public IEnumerable<MemberInfo> GetAllMembers(string channelId, string? subChannelId = null)
    {
        return GetChannelOrSubChannel(channelId, subChannelId, out var channelInfo)
            ? channelInfo.Members.Values
            : Array.Empty<MemberInfo>();
    }

    public IEnumerable<PrivateInfo> GetAllPrivates()
    {
        return PrivateMapping.Values;
    }

    private bool GetChannelOrSubChannel(string channelId, string? subChannelId, [NotNullWhen(true)] out ChannelInfo? channelInfo)
    {
        if (subChannelId == null)
        {
            if (ChannelMapping.TryGetValue(channelId, out channelInfo))
            {
                return true;
            }
        }
        else
        {
            if (SubChannelMapping.TryGetValue(channelId, out var dict) &&
                dict.TryGetValue(subChannelId, out channelInfo))
            {
                return true;
            }
        }

        channelInfo = null;
        return false;
    }
}