using MilkiBotFramework.ContactsManaging.Models;
using MilkiBotFramework.ContactsManaging.Results;

namespace MilkiBotFramework.ContactsManaging;

public class AggregateContactsManager : IContactsManager
{
    private readonly IEnumerable<IContactsManager> _managers;

    public AggregateContactsManager(IEnumerable<IContactsManager> managers)
    {
        _managers = managers;
    }

    public void InitializeTasks()
    {
        foreach (var m in _managers)
            if (m != this)
                m.InitializeTasks();
    }

    public async Task<SelfInfoResult> TryGetOrUpdateSelfInfo()
    {
        foreach (var m in _managers)
        {
            if (m == this) continue;
            try
            {
                var result = await m.TryGetOrUpdateSelfInfo();
                if (result.IsSuccess) return result;
            }
            catch
            {
            }
        }

        return new SelfInfoResult { Message = "No manager succeeded" };
    }

    public async Task<MemberInfoResult> TryGetOrAddMemberInfo(string channelId, string userId,
        string? subChannelId = null)
    {
        foreach (var m in _managers)
        {
            if (m == this) continue;
            try
            {
                var result = await m.TryGetOrAddMemberInfo(channelId, userId, subChannelId);
                if (result.IsSuccess) return result;
            }
            catch
            {
            }
        }

        return new MemberInfoResult { Message = "No manager succeeded" };
    }

    public async Task<ChannelInfoResult> TryGetOrAddChannelInfo(string channelId, string? subChannelId = null)
    {
        foreach (var m in _managers)
        {
            if (m == this) continue;
            try
            {
                var result = await m.TryGetOrAddChannelInfo(channelId, subChannelId);
                if (result.IsSuccess) return result;
            }
            catch
            {
            }
        }

        return new ChannelInfoResult { Message = "No manager succeeded" };
    }

    public async Task<PrivateInfoResult> TryGetOrAddPrivateInfo(string userId)
    {
        foreach (var m in _managers)
        {
            if (m == this) continue;
            try
            {
                var result = await m.TryGetOrAddPrivateInfo(userId);
                if (result.IsSuccess) return result;
            }
            catch
            {
            }
        }

        return new PrivateInfoResult { Message = "No manager succeeded" };
    }

    public IEnumerable<ChannelInfo> GetAllChannels()
    {
        return _managers.Where(m => m != this).SelectMany(m => m.GetAllChannels());
    }

    public IEnumerable<MemberInfo> GetAllMembers(string channelId, string? subChannelId = null)
    {
        return _managers.Where(m => m != this).SelectMany(m => m.GetAllMembers(channelId, subChannelId));
    }

    public IEnumerable<PrivateInfo> GetAllPrivates()
    {
        return _managers.Where(m => m != this).SelectMany(m => m.GetAllPrivates());
    }
}