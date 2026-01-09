using MilkiBotFramework.ContactsManaging;
using MilkiBotFramework.ContactsManaging.Models;
using MilkiBotFramework.ContactsManaging.Results;

namespace MilkiBotFramework.Platforms.Discord.ContactsManaging;

public class DiscordContactsManager : ContactsManagerBase
{
    public override Task<ChannelInfoResult> GetChannelInfoAsync(string channelId, string? subChannelId = null)
    {
        // 简单实现，返回未知
        return Task.FromResult(new ChannelInfoResult() { IsSuccess = false });
    }

    public override Task<MemberInfoResult> GetMemberInfoAsync(string userId, string channelId, string? subChannelId = null)
    {
        return Task.FromResult(new MemberInfoResult() { IsSuccess = false });
    }

    public override Task<SelfInfoResult> GetSelfInfoAsync()
    {
        return Task.FromResult(new SelfInfoResult() 
        { 
            IsSuccess = true, 
            SelfInfo = new SelfInfo("DiscordBot", "Bot") 
        });
    }

    public override Task<PrivateInfoResult> TryGetOrAddPrivateInfo(string userId)
    {
         return Task.FromResult(new PrivateInfoResult() { IsSuccess = true, PrivateInfo = new PrivateInfo(userId, userId) });
    }
}
