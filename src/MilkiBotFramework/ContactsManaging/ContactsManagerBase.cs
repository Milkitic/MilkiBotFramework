using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.ContactsManaging.Models;
using MilkiBotFramework.ContactsManaging.Results;
using MilkiBotFramework.Dispatching;
using MilkiBotFramework.Event;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Tasking;

namespace MilkiBotFramework.ContactsManaging;

/// <summary>
/// 表示一个类，用以自动管理联系簿信息。
/// <para>在MilkiBotFramework中，联系簿支持3种联系人类型，其中包括私聊、主频道与子频道。</para>
/// </summary>
public abstract class ContactsManagerBase : IContactsManager
{
    private readonly BotTaskScheduler _botTaskScheduler;
    private readonly ILogger _logger;
    private readonly EventBus _eventBus;
    private bool _initialized;

    protected SelfInfo? SelfInfo;

    protected readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ChannelInfo>>
        // ReSharper disable once CollectionNeverUpdated.Global
        SubChannelMapping = new();

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
        if (_initialized) return;
        _initialized = true;
        _botTaskScheduler.AddTask("RefreshContactsTask", builder => builder
            .ByInterval(TimeSpan.FromMinutes(5))
            .AtStartup()
            .Do(RefreshContacts));
    }

    public virtual Task<SelfInfoResult> TryGetOrUpdateSelfInfo()
    {
        if (SelfInfo == null) return Task.FromResult(SelfInfoResult.Fail);
        return Task.FromResult(new SelfInfoResult { IsSuccess = true, SelfInfo = SelfInfo });
    }

    public virtual Task<MemberInfoResult> TryGetOrAddMemberInfo(string channelId, string userId,
        string? subChannelId = null)
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

    protected abstract bool GetContactsUpdateInfo(MessageContext messageContext, out ContactsUpdateInfo? updateInfo);

    protected abstract void GetContactsCore(
        out Dictionary<string, ChannelInfo> channels,
        out Dictionary<string, ChannelInfo> subChannels,
        out Dictionary<string, PrivateInfo> privates);

    private async Task OnEventReceived(DispatchMessageEvent e)
    {
        if (e.MessageType != MessageType.Notice) return;

        var messageContext = e.MessageContext;
        var success = GetContactsUpdateInfo(messageContext, out var contactsUpdateInfo);
        if (!success) return;

        switch (contactsUpdateInfo!.ContactsUpdateRole)
        {
            case ContactsUpdateRole.Channel:
                await TryUpdateChannel(contactsUpdateInfo);
                break;
            case ContactsUpdateRole.SubChannel:
                await TryUpdateSubChannel(contactsUpdateInfo);
                break;
            case ContactsUpdateRole.Member:
                await TryUpdateMember(contactsUpdateInfo);
                break;
            case ContactsUpdateRole.Private:
                await TryUpdatePrivate(contactsUpdateInfo);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private async Task TryUpdateMember(ContactsUpdateInfo updateInfo)
    {
        var userId = updateInfo.UserId;
        if (userId == null) return;

        ConcurrentDictionary<string, MemberInfo> members;
        if (updateInfo.SubId == null)
        {
            if (!ChannelMapping.TryGetValue(updateInfo.Id, out var channelInfo))
                return;
            members = channelInfo.Members;
        }
        else
        {
            if (!SubChannelMapping.TryGetValue(updateInfo.Id, out var dict) ||
                !dict.TryGetValue(updateInfo.Id, out var subChannelInfo))
                return;
            members = subChannelInfo.Members;
        }

        MemberInfo? memberInfo;
        if (updateInfo.ContactsUpdateType is ContactsUpdateType.Added or ContactsUpdateType.Changed)
        {
            members.AddOrUpdate(userId, new MemberInfo(updateInfo.Id, userId, updateInfo.SubId)
            {
                Nickname = updateInfo.Name,
                Card = updateInfo.Remark
            }, (_, v) =>
            {
                if (updateInfo.Name != null) v.Nickname = updateInfo.Name;
                if (updateInfo.Remark != null) v.Card = updateInfo.Remark;
                if (updateInfo.MemberRole != null) v.MemberRole = updateInfo.MemberRole.Value;
                return v;
            });

            memberInfo = members[userId];
        }
        else
        {
            members.TryRemove(userId, out memberInfo);
        }

        await _eventBus.PublishAsync((ContactsUpdateEvent)new ContactsUpdateSingleEvent
        {
            MemberInfo = memberInfo,
            UpdateType = updateInfo.ContactsUpdateType,
            UpdateRole = updateInfo.ContactsUpdateRole
        });

        _logger.LogInformation("Member " + updateInfo.ContactsUpdateType + ": " + updateInfo.Id);
    }

    private async Task TryUpdateChannel(ContactsUpdateInfo updateInfo)
    {
        ChannelInfo? channelInfo;
        if (updateInfo.ContactsUpdateType is ContactsUpdateType.Added or ContactsUpdateType.Changed)
        {
            ChannelMapping.AddOrUpdate(updateInfo.Id, new ChannelInfo(updateInfo.Id,
                updateInfo.Members)
            {
                Name = updateInfo.Name,
            }, (_, v) =>
            {
                if (updateInfo.Name != null) v.Name = updateInfo.Name;
                return v;
            });

            channelInfo = ChannelMapping[updateInfo.Id];
        }
        else
        {
            ChannelMapping.TryRemove(updateInfo.Id, out channelInfo);
        }

        await _eventBus.PublishAsync((ContactsUpdateEvent)new ContactsUpdateSingleEvent
        {
            ChannelInfo = channelInfo,
            UpdateType = updateInfo.ContactsUpdateType,
            UpdateRole = updateInfo.ContactsUpdateRole
        });

        _logger.LogInformation("Channel " + updateInfo.ContactsUpdateType + ": " + updateInfo.Id);
    }

    private async Task TryUpdateSubChannel(ContactsUpdateInfo updateInfo)
    {
        if (!SubChannelMapping.TryGetValue(updateInfo.Id, out var dict))
            return;
        if (updateInfo.SubId == null)
            return;

        ChannelInfo? channelInfo;
        if (updateInfo.ContactsUpdateType is ContactsUpdateType.Added or ContactsUpdateType.Changed)
        {
            dict.AddOrUpdate(updateInfo.SubId, new ChannelInfo(updateInfo.Id,
                updateInfo.Members)
            {
                SubChannelId = updateInfo.SubId,
                Name = updateInfo.Name,
            }, (_, v) =>
            {
                if (updateInfo.Name != null) v.Name = updateInfo.Name;
                return v;
            });

            channelInfo = dict[updateInfo.SubId];
        }
        else
        {
            dict.TryRemove(updateInfo.SubId, out channelInfo);
        }

        await _eventBus.PublishAsync((ContactsUpdateEvent)new ContactsUpdateSingleEvent
        {
            SubChannelInfo = channelInfo,
            UpdateType = updateInfo.ContactsUpdateType,
            UpdateRole = updateInfo.ContactsUpdateRole
        });

        _logger.LogInformation("SubChannel " + updateInfo.ContactsUpdateType + ": " + updateInfo.Id + "." +
                               updateInfo.SubId);
    }

    private async Task TryUpdatePrivate(ContactsUpdateInfo updateInfo)
    {
        PrivateInfo? privateInfo;
        if (updateInfo.ContactsUpdateType is ContactsUpdateType.Added or ContactsUpdateType.Changed)
        {
            PrivateMapping.AddOrUpdate(updateInfo.Id, new PrivateInfo(updateInfo.Id)
            {
                Nickname = updateInfo.Name,
                Remark = updateInfo.Remark
            }, (_, v) =>
            {
                if (updateInfo.Name != null) v.Nickname = updateInfo.Name;
                if (updateInfo.Remark != null) v.Remark = updateInfo.Remark;
                return v;
            });

            privateInfo = PrivateMapping[updateInfo.Id];
        }
        else
        {
            PrivateMapping.TryRemove(updateInfo.Id, out privateInfo);
        }

        await _eventBus.PublishAsync((ContactsUpdateEvent)new ContactsUpdateSingleEvent
        {
            PrivateInfo = privateInfo,
            UpdateType = updateInfo.ContactsUpdateType,
            UpdateRole = updateInfo.ContactsUpdateRole
        });

        _logger.LogInformation("Private " + updateInfo.ContactsUpdateType + ": " + updateInfo.Id);
    }

    private bool GetChannelOrSubChannel(string channelId, string? subChannelId,
        [NotNullWhen(true)] out ChannelInfo? channelInfo)
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

    private void RefreshContacts(TaskContext context, CancellationToken token)
    {
        GetContactsCore(out var channels,
            // ReSharper disable once UnusedVariable
            out var subChannels,
            out var privates);

        var list = RefreshChannels(channels, context.Logger);
        var list2 = RefreshPrivates(privates, context.Logger);
        // todo subchannels

        list.AddRange(list2);

        if (list.Count > 0)
            _eventBus.StartPublishTask(new ContactsUpdateEvent { Events = list });
    }

    private List<ContactsUpdateSingleEvent> RefreshPrivates(Dictionary<string, PrivateInfo> privates, ILogger logger)
    {
        var newPrivates = privates.Keys.ToHashSet();
        var oldPrivates = PrivateMapping.Keys.ToHashSet();

        var adds = newPrivates.Where(k => !oldPrivates.Contains(k));
        var exists = newPrivates.Where(k => oldPrivates.Contains(k)).ToArray();
        var removes = oldPrivates.Except(exists);

        var list = new List<ContactsUpdateSingleEvent>();

        foreach (var add in adds)
        {
            PrivateMapping.TryAdd(add, privates[add]);
            logger.LogInformation("Added private: " + add);
            list.Add(new ContactsUpdateSingleEvent { PrivateInfo = privates[add], UpdateRole = ContactsUpdateRole.Private, UpdateType = ContactsUpdateType.Added });
        }

        foreach (var remove in removes)
        {
            PrivateMapping.TryRemove(remove, out var removed);
            logger.LogInformation("Removed private: " + remove);
            list.Add(new ContactsUpdateSingleEvent { PrivateInfo = removed, UpdateRole = ContactsUpdateRole.Private, UpdateType = ContactsUpdateType.Removed });
        }

        foreach (var exist in exists)
        {
            var oldInfo = PrivateMapping[exist];
            var newInfo = privates[exist];
            if (oldInfo.Nickname != newInfo.Nickname)
            {
                logger.LogInformation($"Changed private {exist} nickname from: " + oldInfo.Nickname + " to " +
                                      newInfo.Nickname);
                oldInfo.Nickname = newInfo.Nickname;
                list.Add(new ContactsUpdateSingleEvent { ChangedPath = "Nickname", PrivateInfo = oldInfo, UpdateRole = ContactsUpdateRole.Private, UpdateType = ContactsUpdateType.Changed });
            }

            if (oldInfo.Remark != newInfo.Remark)
            {
                logger.LogInformation($"Changed private {exist} remark from: " + oldInfo.Remark + " to " +
                                      newInfo.Remark);
                oldInfo.Remark = newInfo.Remark;
                list.Add(new ContactsUpdateSingleEvent { ChangedPath = "Remark", PrivateInfo = oldInfo, UpdateRole = ContactsUpdateRole.Private, UpdateType = ContactsUpdateType.Changed });
            }
        }

        return list;
    }

    private List<ContactsUpdateSingleEvent> RefreshChannels(Dictionary<string, ChannelInfo> channels, ILogger logger)
    {
        var newChannels = channels.Keys.ToHashSet();
        var oldChannels = ChannelMapping.Keys.ToHashSet();

        var adds = newChannels.Where(k => !oldChannels.Contains(k));
        var exists = newChannels.Where(k => oldChannels.Contains(k)).ToArray();
        var removes = oldChannels.Except(exists);

        var list = new List<ContactsUpdateSingleEvent>();

        foreach (var add in adds)
        {
            ChannelMapping.TryAdd(add, channels[add]);
            logger.LogInformation("Add channel and members: " + add);
            list.Add(new ContactsUpdateSingleEvent { ChannelInfo = channels[add], UpdateRole = ContactsUpdateRole.Channel, UpdateType = ContactsUpdateType.Added });
        }

        foreach (var remove in removes)
        {
            ChannelMapping.TryRemove(remove, out var removed);
            logger.LogInformation("Remove channel and members: " + remove);
            list.Add(new ContactsUpdateSingleEvent { ChannelInfo = removed, UpdateRole = ContactsUpdateRole.Channel, UpdateType = ContactsUpdateType.Removed });
        }

        foreach (var exist in exists)
        {
            var oldInfo = ChannelMapping[exist];
            var newInfo = channels[exist];
            if (oldInfo.Name != newInfo.Name)
            {
                logger.LogInformation($"Changed channel {exist} name from: " + oldInfo.Name + " to " + newInfo.Name);
                oldInfo.Name = newInfo.Name;
                list.Add(new ContactsUpdateSingleEvent { ChangedPath = "Name", ChannelInfo = oldInfo, UpdateRole = ContactsUpdateRole.Channel, UpdateType = ContactsUpdateType.Changed });
            }

            var events = RefreshMembers(newInfo, oldInfo.Members, newInfo.Members, logger);
            list.AddRange(events);
        }

        return list;
    }

    private List<ContactsUpdateSingleEvent> RefreshMembers(ChannelInfo channel,
        ConcurrentDictionary<string, MemberInfo> oldMemberDict,
        ConcurrentDictionary<string, MemberInfo> newMemberDict,
        ILogger logger)
    {
        var newMembers = newMemberDict.Keys.ToHashSet();
        var oldMembers = oldMemberDict.Keys.ToHashSet();

        var adds = newMembers.Where(k => !oldMembers.Contains(k));
        var exists = newMembers.Where(k => oldMembers.Contains(k)).ToArray();
        var removes = oldMembers.Except(exists);

        var list = new List<ContactsUpdateSingleEvent>();

        var channelId = channel.ChannelId;
        foreach (var add in adds)
        {
            channel.Members.TryAdd(add, newMemberDict[add]);
            logger.LogInformation($"Add channel {channelId} member: " + add);
            list.Add(new ContactsUpdateSingleEvent { MemberInfo = newMemberDict[add], UpdateRole = ContactsUpdateRole.Member, UpdateType = ContactsUpdateType.Added });
        }

        foreach (var remove in removes)
        {
            channel.Members.TryRemove(remove, out var removed);
            logger.LogInformation($"Remove channel {channelId} member: " + remove);
            list.Add(new ContactsUpdateSingleEvent { MemberInfo = removed, UpdateRole = ContactsUpdateRole.Member, UpdateType = ContactsUpdateType.Removed });
        }

        foreach (var exist in exists)
        {
            var oldInfo = oldMemberDict[exist];
            var newInfo = newMemberDict[exist];
            if (oldInfo.Nickname != newInfo.Nickname)
            {
                logger.LogInformation($"Changed channel {channelId} member {exist} nickname from: " +
                                      oldInfo.Nickname + " to " + newInfo.Nickname);
                oldInfo.Nickname = newInfo.Nickname;
                list.Add(new ContactsUpdateSingleEvent { ChangedPath = "Nickname", MemberInfo = oldInfo, UpdateRole = ContactsUpdateRole.Member, UpdateType = ContactsUpdateType.Changed });
            }

            if (oldInfo.Card != newInfo.Card)
            {
                logger.LogInformation($"Changed channel {channelId} member {exist} card from: " +
                                      oldInfo.Card + " to " + newInfo.Card);
                oldInfo.Card = newInfo.Card;
                list.Add(new ContactsUpdateSingleEvent { ChangedPath = "Card", MemberInfo = oldInfo, UpdateRole = ContactsUpdateRole.Member, UpdateType = ContactsUpdateType.Changed });
            }

            if (oldInfo.MemberRole != newInfo.MemberRole)
            {
                logger.LogInformation($"Changed channel {channelId} member {exist} role from: " +
                                      oldInfo.MemberRole + " to " + newInfo.MemberRole);
                oldInfo.MemberRole = newInfo.MemberRole;
                list.Add(new ContactsUpdateSingleEvent { ChangedPath = "MemberRole", MemberInfo = oldInfo, UpdateRole = ContactsUpdateRole.Member, UpdateType = ContactsUpdateType.Changed });
            }
        }

        return list;
    }
}