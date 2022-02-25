using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.ContractsManaging.Models;
using MilkiBotFramework.ContractsManaging.Results;
using MilkiBotFramework.Dispatching;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Tasking;

namespace MilkiBotFramework.ContractsManaging;

public abstract class ContractsManagerBase : IContractsManager
{
    private readonly BotTaskScheduler _botTaskScheduler;
    private readonly ILogger _logger;
    private IDispatcher? _dispatcher;

    protected readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ChannelInfo>> SubChannelMapping = new();
    protected readonly ConcurrentDictionary<string, ChannelInfo> ChannelMapping = new();
    protected readonly ConcurrentDictionary<string, PrivateInfo> PrivateMapping = new();

    protected readonly ConcurrentDictionary<string, Avatar> UserAvatarMapping = new();
    protected readonly ConcurrentDictionary<string, Avatar> ChannelAvatarMapping = new();

    public ContractsManagerBase(BotTaskScheduler botTaskScheduler, ILogger logger)
    {
        _botTaskScheduler = botTaskScheduler;
        _logger = logger;
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

    public IDispatcher? Dispatcher
    {
        get => _dispatcher;
        internal set
        {
            if (_dispatcher != null) _dispatcher.SystemMessageReceived -= Dispatcher_NoticeMessageReceived;
            _dispatcher = value;
            if (_dispatcher != null) _dispatcher.SystemMessageReceived += Dispatcher_NoticeMessageReceived;
        }
    }

    public abstract Task<SelfInfoResult> TryGetSelfInfo();

    // todo: not a good design for public
    public bool TryGetMemberInfo(string channelId, string userId, [NotNullWhen(true)] out MemberInfo? memberInfo, string? subChannelId = null)
    {
        if (subChannelId == null)
        {
            if (ChannelMapping.TryGetValue(channelId, out var channelInfo) &&
                channelInfo.Members.TryGetValue(userId, out memberInfo))
            {
                return true;
            }
        }
        else
        {
            if (SubChannelMapping.TryGetValue(channelId, out var subChannels) &&
                subChannels.TryGetValue(channelId, out var channelInfo) &&
                channelInfo.Members.TryGetValue(userId, out memberInfo))
            {
                return true;
            }
        }

        memberInfo = null;
        return false;
    }

    // todo: not a good design for public
    public bool TryGetChannelInfo(string channelId,
        [NotNullWhen(true)] out ChannelInfo? channelInfo,
        string? subChannelId = null)
    {
        if (subChannelId == null)
            return ChannelMapping.TryGetValue(channelId, out channelInfo);

        channelInfo = null;
        return SubChannelMapping.TryGetValue(channelId, out var dict) &&
               dict.TryGetValue(subChannelId, out channelInfo);
    }

    // todo: not a good design for public
    public bool TryGetPrivateInfo(string userId, [NotNullWhen(true)] out PrivateInfo? privateInfo)
    {
        return PrivateMapping.TryGetValue(userId, out privateInfo);
    }

    public void AddMember(string channelId, MemberInfo member)
    {
        if (ChannelMapping.TryGetValue(channelId, out var channelInfo))
        {
            channelInfo.Members.AddOrUpdate(member.UserId, member, (id, instance) => member);
        }
    }

    public void RemoveMember(string channelId, string userId)
    {
    }

    public void AddChannel(ChannelInfo channelInfo)
    {
        ChannelMapping.AddOrUpdate(channelInfo.ChannelId, channelInfo, (id, instance) => channelInfo);
    }

    public void RemoveChannel(string channelId)
    {
    }

    public void AddSubChannel(string channelId, ChannelInfo subChannelInfo)
    {
    }

    public void RemoveSubChannel(string channelId, string subChannelId)
    {
    }

    public void AddPrivate(PrivateInfo privateInfo)
    {
        PrivateMapping.AddOrUpdate(privateInfo.UserId, privateInfo, (id, instance) => privateInfo);
    }

    public void RemovePrivate(string userId)
    {
    }

    protected virtual Task<ContractUpdateResult> UpdateMemberIfPossible(MessageContext messageContext)
    {
        return Task.FromResult(new ContractUpdateResult(false, null, ContractUpdateType.Unspecified));
    }

    protected virtual Task<ContractUpdateResult> UpdateChannelsIfPossible(MessageContext messageContext)
    {
        return Task.FromResult(new ContractUpdateResult(false, null, ContractUpdateType.Unspecified));
    }

    protected virtual Task<ContractUpdateResult> UpdatePrivatesIfPossible(MessageContext messageContext)
    {
        return Task.FromResult(new ContractUpdateResult(false, null, ContractUpdateType.Unspecified));
    }

    private async Task Dispatcher_NoticeMessageReceived(MessageContext messageContext)
    {
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

    public abstract Task<ChannelInfoResult> TryGetChannelInfoByMessageContext(MessageIdentity messageIdentity, string userId);
    public abstract Task<PrivateInfoResult> TryGetPrivateInfoByMessageContext(MessageIdentity messageIdentity);
    public IEnumerable<ChannelInfo> GetAllMainChannels()
    {
        return ChannelMapping.Values;
    }

    public IEnumerable<PrivateInfo> GetAllPrivates()
    {
        return PrivateMapping.Values;
    }
}