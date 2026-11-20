using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.ContactsManaging;
using MilkiBotFramework.ContactsManaging.Models;
using MilkiBotFramework.ContactsManaging.Results;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Tasking;

namespace MilkiBotFramework.Platforms.QQ.ContactsManaging;

/// <summary>维护 QQ 官方适配器从事件和消息上下文得到的联系人缓存。</summary>
public sealed class QContactsManager : ContactsManagerBase
{
    /// <summary>框架中 QQ 官方平台的标识。</summary>
    public override string PlatformId => PlatformIds.Qq;

    /// <summary>创建 QQ 联系人管理器。</summary>
    /// <param name="botTaskScheduler">框架任务调度器。</param>
    /// <param name="logger">联系人管理日志。</param>
    public QContactsManager(
        BotTaskScheduler botTaskScheduler,
        ILogger<QContactsManager> logger)
        : base(botTaskScheduler, logger)
    {
    }

    /// <summary>用网关返回的机器人资料更新本机缓存。</summary>
    /// <param name="selfInfo">当前机器人资料。</param>
    public void UpdateSelfInfo(SelfInfo selfInfo)
    {
        SelfInfo = selfInfo;
    }

    /// <summary>从缓存读取机器人自身资料。</summary>
    /// <returns>已有资料时返回成功结果。</returns>
    public override Task<SelfInfoResult> TryGetOrUpdateSelfInfo() => base.TryGetOrUpdateSelfInfo();

    /// <summary>查找或创建目标 QQ 群的本地资料。</summary>
    /// <param name="channelId">群 OpenID。</param>
    /// <param name="subChannelId">框架通用子频道参数。群聊请传空值。</param>
    /// <returns>本地群联系人记录。</returns>
    public override async Task<ChannelInfoResult> TryGetOrAddChannelInfo(string channelId, string? subChannelId = null)
    {
        var baseResult = await base.TryGetOrAddChannelInfo(channelId, subChannelId);
        if (baseResult.IsSuccess) return baseResult;

        var channelInfo = ChannelMapping.GetOrAdd(channelId, static id => new ChannelInfo(id));
        return new ChannelInfoResult { IsSuccess = true, ChannelInfo = channelInfo };
    }

    /// <summary>查找或创建 QQ 群成员的本地资料。</summary>
    /// <param name="channelId">群 OpenID。</param>
    /// <param name="userId">群成员 OpenID。</param>
    /// <param name="subChannelId">框架通用子频道参数。群聊请传空值。</param>
    /// <returns>本地群成员记录。</returns>
    public override async Task<MemberInfoResult> TryGetOrAddMemberInfo(string channelId, string userId, string? subChannelId = null)
    {
        var baseResult = await base.TryGetOrAddMemberInfo(channelId, userId, subChannelId);
        if (baseResult.IsSuccess) return baseResult;

        var success = ChannelMapping.TryGetValue(channelId, out var channelInfo);
        if (!success)
        {
            var channelResult = await TryGetOrAddChannelInfo(channelId, subChannelId);
            success = channelResult.IsSuccess;
            channelInfo = channelResult.ChannelInfo;
        }

        if (success && channelInfo != null)
        {
            var memberInfo = channelInfo.Members.GetOrAdd(userId, id => new MemberInfo(channelId, id, subChannelId)
            {
                MemberRole = MemberRole.Member
            });
            return new MemberInfoResult { IsSuccess = true, MemberInfo = memberInfo };
        }

        return MemberInfoResult.Fail;
    }

    /// <summary>查找或创建 QQ 单聊用户的本地资料。</summary>
    /// <param name="userId">单聊用户 OpenID。</param>
    /// <returns>本地单聊用户记录。</returns>
    public override async Task<PrivateInfoResult> TryGetOrAddPrivateInfo(string userId)
    {
        var baseResult = await base.TryGetOrAddPrivateInfo(userId);
        if (baseResult.IsSuccess) return baseResult;

        var privateInfo = PrivateMapping.GetOrAdd(userId, static id => new PrivateInfo(id));
        return new PrivateInfoResult { IsSuccess = true, PrivateInfo = privateInfo };
    }

    /// <summary>检查通知是否携带框架可以直接应用的联系人变更。</summary>
    /// <param name="messageContext">当前通知的消息上下文。</param>
    /// <param name="updateInfo">适配器未映射联系人变更时为空。</param>
    /// <returns>当前实现始终返回 <see langword="false"/>。</returns>
    protected override bool GetContactsUpdateInfo(MessageContext messageContext, [NotNullWhen(true)] out ContactsUpdateInfo? updateInfo)
    {
        // todo
        updateInfo = null;
        return false;
    }

    /// <summary>说明此适配器的联系人按事件按需建立缓存，无全量同步。</summary>
    /// <param name="channels">未同步的群列表。</param>
    /// <param name="subChannels">未同步的子频道列表。</param>
    /// <param name="privates">未同步的单聊用户列表。</param>
    /// <returns>当前实现始终返回 <see langword="false"/>。</returns>
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

    /// <summary>保留缓存模式，无需注册联系人全量刷新任务。</summary>
    protected override void InitializeTasksCore()
    {
    }
}
