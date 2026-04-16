using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.ContactsManaging.Models;
using MilkiBotFramework.ContactsManaging.Results;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Tasking;

namespace MilkiBotFramework.ContactsManaging;

/// <summary>
/// 表示一个类，用以自动管理联系簿信息。
/// <para>在MilkiBotFramework中，联系簿支持3种联系人类型，其中包括私聊、主频道与子频道。</para>
/// </summary>
public abstract class ContactsManagerBase : IPlatformContactsManager
{
    private readonly BotTaskScheduler _botTaskScheduler;
    private readonly ILogger _logger;
    private bool _initialized;

    public event Func<ContactsUpdateEvent, Task>? ContactsUpdated;

    public virtual string PlatformId => string.Empty;

    protected SelfInfo? SelfInfo;

    protected readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ChannelInfo>>
        // ReSharper disable once CollectionNeverUpdated.Global
        SubChannelMapping = new();

    protected readonly ConcurrentDictionary<string, ChannelInfo> ChannelMapping = new();
    protected readonly ConcurrentDictionary<string, PrivateInfo> PrivateMapping = new();

    protected readonly ConcurrentDictionary<string, Avatar> UserAvatarMapping = new();
    protected readonly ConcurrentDictionary<string, Avatar> ChannelAvatarMapping = new();

    public ContactsManagerBase(BotTaskScheduler botTaskScheduler, ILogger logger)
    {
        _botTaskScheduler = botTaskScheduler;
        _logger = logger;
    }

    public void InitializeTasks()
    {
        if (_initialized) return;
        _initialized = true;
        InitializeTasksCore();
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
                subChannels.TryGetValue(subChannelId, out var channelInfo) &&
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

    public virtual bool Supports(MessageContext messageContext)
    {
        return string.Equals(messageContext.PlatformId, PlatformId, StringComparison.OrdinalIgnoreCase);
    }

    protected abstract bool GetContactsUpdateInfo(MessageContext messageContext, out ContactsUpdateInfo? updateInfo);

    protected abstract bool GetContactsCore(
        [NotNullWhen(true)] out Dictionary<string, ChannelInfo>? channels,
        [NotNullWhen(true)] out Dictionary<string, ChannelInfo>? subChannels,
        [NotNullWhen(true)] out Dictionary<string, PrivateInfo>? privates);

    protected virtual string RefreshContactsTaskName => string.IsNullOrWhiteSpace(PlatformId)
        ? "RefreshContactsTask"
        : $"RefreshContactsTask[{PlatformId}]";

    protected virtual void InitializeTasksCore()
    {
        _botTaskScheduler.AddTask(RefreshContactsTaskName, builder => builder
            .ByInterval(TimeSpan.FromMinutes(5))
            .AtStartup()
            .Do(RefreshContacts));
    }

    public async Task HandleMessageAsync(MessageContext messageContext)
    {
        if (messageContext.MessageIdentity?.MessageType != MessageType.Notice) return;

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
                updateInfo.SubId == null ||
                !dict.TryGetValue(updateInfo.SubId, out var subChannelInfo))
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

        await NotifyContactsUpdatedAsync((ContactsUpdateEvent)new ContactsUpdateSingleEvent
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

        await NotifyContactsUpdatedAsync((ContactsUpdateEvent)new ContactsUpdateSingleEvent
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

        await NotifyContactsUpdatedAsync((ContactsUpdateEvent)new ContactsUpdateSingleEvent
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

        await NotifyContactsUpdatedAsync((ContactsUpdateEvent)new ContactsUpdateSingleEvent
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
        // ReSharper disable once UnusedVariable
        if (!GetContactsCore(out var channels, out var subChannels, out var privates)) return;

        var list = RefreshChannels(channels, context.Logger);
        var list2 = RefreshPrivates(privates, context.Logger);
        // todo subchannels

        list.AddRange(list2);

        if (list.Count > 0)
            _ = NotifyContactsUpdatedAsync(new ContactsUpdateEvent { Events = list });

    }

    private Task NotifyContactsUpdatedAsync(ContactsUpdateEvent updateEvent)
    {
        var handlers = ContactsUpdated;
        if (handlers == null)
            return Task.CompletedTask;

        return Task.WhenAll(handlers
            .GetInvocationList()
            .Cast<Func<ContactsUpdateEvent, Task>>()
            .Select(handler => handler(updateEvent)));
    }

    private List<ContactsUpdateSingleEvent> RefreshPrivates(Dictionary<string, PrivateInfo> privates, ILogger logger)
    {
        GetCollections(privates, PrivateMapping.Keys,
            out var toAdd, out var toUpdate, out var toRemove);

        // 处理添加的联系人
        var addedPrivates = toAdd
            .Select(userId => privates[userId])
            .Where(privateInfo => PrivateMapping.TryAdd(privateInfo.UserId, privateInfo))
            .ToList();

        var events = new List<ContactsUpdateSingleEvent>(addedPrivates.Count);
        events.AddRange(addedPrivates.Select(ContactsUpdateSingleEvent.Add));

        if (addedPrivates.Count > 0)
        {
            var logMessage = string.Join(", ", addedPrivates.Select(p => $"\"{p.Nickname} ({p.UserId})\""));
            logger.LogInformation($"Add private: [{logMessage}]");
        }

        // 处理删除的联系人
        foreach (var userId in toRemove)
        {
            if (!PrivateMapping.TryRemove(userId, out var privateInfo)) continue;
            logger.LogInformation("Removed private: " + userId);
            events.Add(ContactsUpdateSingleEvent.Remove(privateInfo));
        }

        // 处理更新的联系人
        foreach (var userId in toUpdate)
        {
            var oldInfo = PrivateMapping[userId];
            var newInfo = privates[userId];
            if (oldInfo.Nickname != newInfo.Nickname)
            {
                logger.LogInformation($"Changed private {userId} nickname from: " + oldInfo.Nickname + " to " +
                                      newInfo.Nickname);
                oldInfo.Nickname = newInfo.Nickname;
                events.Add(ContactsUpdateSingleEvent.Update(oldInfo, nameof(newInfo.Nickname)));
            }

            if (oldInfo.Remark != newInfo.Remark)
            {
                logger.LogInformation($"Changed private {userId} remark from: " + oldInfo.Remark + " to " +
                                      newInfo.Remark);
                oldInfo.Remark = newInfo.Remark;
                events.Add(ContactsUpdateSingleEvent.Update(oldInfo, nameof(newInfo.Remark)));
            }
        }

        return events;
    }

    private List<ContactsUpdateSingleEvent> RefreshChannels(Dictionary<string, ChannelInfo> channels, ILogger logger)
    {
        var newChannels = channels.Keys.ToHashSet();
        var oldChannels = ChannelMapping.Keys.ToHashSet();

        var adds = newChannels.Where(k => !oldChannels.Contains(k));
        var exists = newChannels.Where(k => oldChannels.Contains(k)).ToArray();
        var removes = oldChannels.Except(exists);

        var list = new List<ContactsUpdateSingleEvent>();

        var sb = new StringBuilder();
        foreach (var add in adds)
        {
            var channelInfo = channels[add];
            ChannelMapping.TryAdd(add, channelInfo);
            sb.Append($"\"{channelInfo.Name} ({channelInfo.ChannelId})\", ");
            //logger.LogInformation("Add channel and members: " + add);
            list.Add(new ContactsUpdateSingleEvent { ChannelInfo = channelInfo, UpdateRole = ContactsUpdateRole.Channel, UpdateType = ContactsUpdateType.Added });
        }

        if (sb.Length > 0)
        {
            sb.Remove(sb.Length - 2, 2);
            logger.LogInformation($"Add channel and members: [{sb}]");
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

    private static void GetCollections(Dictionary<string, PrivateInfo> privates, ICollection<string> oldPrivates,
        out List<string> toAdd, out List<string> toUpdate, out List<string> toRemove)
    {
        var oldPrivateKeys = oldPrivates.ToHashSet();

        toAdd = new List<string>();
        toUpdate = new List<string>();

        // 单次遍历确定添加和更新的项目
        foreach (var key in privates.Keys)
        {
            if (oldPrivateKeys.Contains(key))
            {
                toUpdate.Add(key);
            }
            else
            {
                toAdd.Add(key);
            }
        }

        toRemove = oldPrivateKeys.Except(privates.Keys).ToList();
    }
}