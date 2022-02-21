using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Dispatching;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Tasking;

namespace MilkiBotFramework.ContractsManaging;

public abstract class ContractsManagerBase : IContractsManager
{
    private readonly BotTaskScheduler _botTaskScheduler;
    private readonly ILogger _logger;
    private IDispatcher? _dispatcher;

    protected ConcurrentDictionary<string, ConcurrentDictionary<string, ChannelInfo>> _subChannelMapping = new();
    protected ConcurrentDictionary<string, ChannelInfo> _channelMapping = new();
    protected ConcurrentDictionary<string, PrivateInfo> _privateMapping = new();

    protected ConcurrentDictionary<string, Avatar> _userAvatarMapping = new();
    protected ConcurrentDictionary<string, Avatar> _channelAvatarMapping = new();

    public ContractsManagerBase(BotTaskScheduler botTaskScheduler, ILogger logger)
    {
        _botTaskScheduler = botTaskScheduler;
        _logger = logger;
        _botTaskScheduler.AddTask("RefreshContractsTask", builder => builder
            .ByInterval(TimeSpan.FromSeconds(15))
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
            if (_dispatcher != null) _dispatcher.SystemMessageReceived -= Dispatcher_SystemMessageReceived;
            _dispatcher = value;
            if (_dispatcher != null) _dispatcher.SystemMessageReceived += Dispatcher_SystemMessageReceived;
        }
    }

    public bool TryGetMemberInfo(string channelId, string userId, [NotNullWhen(true)] out MemberInfo? memberInfo, string? subChannelId = null)
    {
        if (subChannelId == null)
        {
            if (_channelMapping.TryGetValue(channelId, out var channelInfo) &&
                channelInfo.Members.TryGetValue(userId, out memberInfo))
            {
                return true;
            }
        }
        else
        {
            if (_subChannelMapping.TryGetValue(channelId, out var subChannels) &&
                subChannels.TryGetValue(channelId, out var channelInfo) &&
                channelInfo.Members.TryGetValue(userId, out memberInfo))
            {
                return true;
            }
        }

        memberInfo = null;
        return false;
    }

    public bool TryGetChannelInfo(string channelId, out ChannelInfo channelInfo, string? subChannelId = null)
    {
        throw new NotImplementedException();
    }

    public bool TryGetPrivateInfo(string userId, out PrivateInfo privateInfo)
    {
        throw new NotImplementedException();
    }

    public void AddMember(string channelId, MemberInfo member)
    {
    }

    public void RemoveMember(string channelId, string userId)
    {
    }

    public void AddChannel(ChannelInfo channelInfo)
    {
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

    public void AddPrivate(PrivateInfo channelInfo)
    {
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

    private async Task Dispatcher_SystemMessageReceived(MessageContext messageContext)
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

    public abstract bool TryGetChannelInfoByMessageContext(MessageIdentity messageIdentity,
        out ChannelInfo channelInfo,
        out MemberInfo memberInfo);
    public abstract bool TryGetPrivateInfoByMessageContext(MessageIdentity messageIdentity,
        out PrivateInfo channelInfo);
}