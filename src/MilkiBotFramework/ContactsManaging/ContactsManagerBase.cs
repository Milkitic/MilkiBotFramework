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

    public void InitializeTasks()
    {
        _botTaskScheduler.AddTask("RefreshContractsTask", builder => builder
            .ByInterval(TimeSpan.FromMinutes(5))
            .AtStartup()
            .Do(RefreshContracts));
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

    protected abstract bool GetContractUpdateInfo(MessageContext messageContext, out ContractUpdateInfo? updateInfo);

    protected abstract void GetContractsCore(
        out Dictionary<ChannelInfo, List<MemberInfo>> channels,
        out Dictionary<ChannelInfo, List<MemberInfo>> subChannels,
        out List<PrivateInfo> privates);

    private void OnEventReceived(DispatchMessageEvent e)
    {
        if (e.MessageType != MessageType.Notice) return;

        var messageContext = e.MessageContext;
        var success = GetContractUpdateInfo(messageContext, out var contractUpdateInfo);
        if (!success) return;

        switch (contractUpdateInfo!.ContractUpdateRole)
        {
            case ContractUpdateRole.Channel:
                TryUpdateChannel(contractUpdateInfo);
                break;
            case ContractUpdateRole.SubChannel:
                TryUpdateSubChannel(contractUpdateInfo);
                break;
            case ContractUpdateRole.Member:
                TryUpdateMember(contractUpdateInfo);
                break;
            case ContractUpdateRole.Private:
                TryUpdatePrivate(contractUpdateInfo);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void TryUpdateMember(ContractUpdateInfo updateInfo)
    {
        // todo
        _logger.LogInformation("Member " + updateInfo.ContractUpdateType + ": " + updateInfo.Id);
    }

    private void TryUpdateChannel(ContractUpdateInfo updateInfo)
    {
        // todo
        _logger.LogInformation("Channel " + updateInfo.ContractUpdateType + ": " + updateInfo.Id);
    }

    private void TryUpdateSubChannel(ContractUpdateInfo updateInfo)
    {
        // todo
        _logger.LogInformation("SubChannel " + updateInfo.ContractUpdateType + ": " + updateInfo.Id);
    }

    private void TryUpdatePrivate(ContractUpdateInfo updateInfo)
    {
        // todo
        _logger.LogInformation("Private " + updateInfo.ContractUpdateType + ": " + updateInfo.Id);
    }

    private void RefreshContracts(TaskContext context, CancellationToken token)
    {
        GetContractsCore(out var channels, out var subChannels, out var privates);
        _logger.LogInformation("Refreshed!");
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