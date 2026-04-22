using MilkiBotFramework.Connecting;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Messaging.RichMessages;
using MilkiBotFramework.Platforms.OneBot.Connecting.RequestModel;
using MilkiBotFramework.Platforms.OneBot.Connecting.ResponseModel;
using MilkiBotFramework.Platforms.OneBot.Connecting.ResponseModel.Guild;
using MilkiBotFramework.Platforms.OneBot.Messaging;

namespace MilkiBotFramework.Platforms.OneBot.Connecting;

public class OneBotApi : IPlatformMessageApi
{
    private readonly IOneBotConnector _oneBotConnector;
    public string PlatformId => PlatformIds.OneBot;

    public OneBotApi(IOneBotConnector connector)
    {
        Connector = connector;
        _oneBotConnector = connector;
    }

    public IOneBotConnector Connector { get; }

    public bool Supports(MessageContext messageContext)
    {
        return messageContext is OneBotMessageContext;
    }

    private static class Actions
    {
        // ReSharper disable InconsistentNaming
        public const string GetLoginInfo = "get_login_info";
        public const string GetGuildServiceProfile = "get_guild_service_profile";

        public const string GetMsg = "get_msg";
        public const string DeleteMsg = "delete_msg";
        public const string SendPrivateMsg = "send_private_msg";
        public const string SendGroupMsg = "send_group_msg";
        public const string SendGuildChannelMsg = "send_guild_channel_msg";

        public const string SetFriendAddRequest = "set_friend_add_request";
        public const string SetGroupAddRequest = "set_group_add_request";
        public const string SetGroupBan = "set_group_ban";

        public const string GetGroupInfo = "get_group_info";
        public const string GetGroupList = "get_group_list";
        public const string GetGroupMemberInfo = "get_group_member_info";
        public const string GetGroupMemberList = "get_group_member_list";

        public const string GetGuildMetaByGuest = "get_guild_meta_by_guest";
        public const string GetGuildList = "get_guild_list";
        public const string GetGuildChannelList = "get_guild_channel_list";
        public const string GetGuildMembers = "get_guild_members";

        public const string GetStrangerInfo = "get_stranger_info";
        public const string GetFriendList = "get_friend_list";

        // ReSharper restore InconsistentNaming
    }

    #region Bot auth

    public async Task<LoginInfo> GetLoginInfo(string selfId)
    {
        return await RequestAsync<LoginInfo>(Actions.GetLoginInfo, null, selfId).ConfigureAwait(false);
    }

    public async Task<GuildServiceProfile> GetGuildServiceProfile(string selfId)
    {
        return await RequestAsync<GuildServiceProfile>(Actions.GetGuildServiceProfile, null, selfId).ConfigureAwait(false);
    }

    #endregion

    #region Messaging

    public async Task<GetMsgResponse> GetMessage(long messageId, string selfId)
    {
        var parameters = new Dictionary<string, object>
        {
            { "message_id", messageId },
        };
        return await RequestAsync<GetMsgResponse>(Actions.GetMsg, parameters, selfId).ConfigureAwait(false);
    }

    public async Task DeleteMessage(int messageId, string selfId)
    {
        var parameters = new Dictionary<string, object>
        {
            { "message_id", messageId }
        };
        await RequestAsync(Actions.DeleteMsg, parameters, selfId).ConfigureAwait(false);
    }

    public async Task<string> SendPrivateMessageAsync(long userId,
        string message,
        string selfId,
        long? groupId = null,
        bool autoEscape = false)
    {
        var parameters = new Dictionary<string, object>
        {
            { "user_id", userId },
            { "message", message },
            { "auto_escape", autoEscape }
        };
        if (groupId != null) parameters.Add("group_id", groupId);
        var response = await RequestAsync<MsgResponse>(Actions.SendPrivateMsg, parameters, selfId).ConfigureAwait(false);
        return response.MessageId;
    }

    public async Task<string> SendGroupMessageAsync(long messageId, string message, string selfId)
    {
        var parameters = new Dictionary<string, object>
        {
            { "group_id", messageId },
            { "message", message }
        };
        var response = await RequestAsync<MsgResponse>(Actions.SendGroupMsg, parameters, selfId).ConfigureAwait(false);
        return response.MessageId;
    }

    public async Task<string> SendGuildChannelMessageAsync(long guildId, long subChannelId, string message, string selfId)
    {
        var parameters = new Dictionary<string, object>
        {
            { "guild_id", guildId },
            { "channel_id", subChannelId },
            { "message", message },
        };
        return (await RequestAsync<MsgResponse>(Actions.SendGuildChannelMsg, parameters, selfId).ConfigureAwait(false)).MessageId;
    }

    #endregion

    #region Contacts info

    public async Task<StrangerInfo> GetStrangerInfo(long userId, string selfId, bool noCache = false)
    {
        var parameters = new Dictionary<string, object>
        {
            { "user_id", userId },
            { "no_cache", noCache }
        };
        return await RequestAsync<StrangerInfo>(Actions.GetStrangerInfo, parameters, selfId).ConfigureAwait(false);
    }

    public async Task<List<FriendInfo>> GetFriends(string selfId)
    {
        return await RequestAsync<List<FriendInfo>>(Actions.GetFriendList, null, selfId).ConfigureAwait(false);
    }

    public async Task<List<GroupInfo>> GetGroups(string selfId)
    {
        return await RequestAsync<List<GroupInfo>>(Actions.GetGroupList, null, selfId).ConfigureAwait(false);
    }

    public async Task<GroupInfo> GetGroupInfo(long groupId, string selfId)
    {
        var parameters = new Dictionary<string, object>
        {
            { "group_id", groupId }
        };
        return await RequestAsync<GroupInfo>(Actions.GetGroupInfo, parameters, selfId).ConfigureAwait(false);
    }

    public async Task<List<GroupMember>> GetFuzzyGroupMembers(long groupId, string selfId)
    {
        var parameters = new Dictionary<string, object>
        {
            { "group_id", groupId }
        };
        return await RequestAsync<List<GroupMember>>(Actions.GetGroupMemberList, parameters, selfId).ConfigureAwait(false);
    }

    public async Task<GroupMember> GetGroupMemberDetail(long groupId, long userId, string selfId, bool noCache = false)
    {
        var parameters = new Dictionary<string, object>
        {
            { "group_id", groupId },
            { "user_id", userId },
            { "no_cache", noCache }
        };

        return await RequestAsync<GroupMember>(Actions.GetGroupMemberInfo, parameters, selfId).ConfigureAwait(false);
    }

    public async Task<List<GuildBrief>> GetGuilds(string selfId)
    {
        return await RequestAsync<List<GuildBrief>>(Actions.GetGuildList, null, selfId).ConfigureAwait(false);
    }

    public async Task<GuildInfo> GetGuildMetaByGuest(long guildId, string selfId)
    {
        var parameters = new Dictionary<string, object>
        {
            { "guild_id", guildId.ToString() }
        };
        return await RequestAsync<GuildInfo>(Actions.GetGuildMetaByGuest, parameters, selfId).ConfigureAwait(false);
    }

    public async Task<List<SubChannelInfo>> GetGuildChannelList(long guildId, string selfId)
    {
        var parameters = new Dictionary<string, object>
        {
            { "guild_id", guildId.ToString() }
        };
        return await RequestAsync<List<SubChannelInfo>>(Actions.GetGuildChannelList, parameters, selfId).ConfigureAwait(false);
    }

    public async Task<GetGuildMembersResponse> GetGuildMembers(long guildId, string selfId)
    {
        var parameters = new Dictionary<string, object>
        {
            { "guild_id", guildId.ToString() }
        };
        return await RequestAsync<GetGuildMembersResponse>(Actions.GetGuildMembers, parameters, selfId).ConfigureAwait(false);
    }

    #endregion

    #region Operations

    public async Task SetGroupBan(long groupId, long userId, TimeSpan duration, string selfId)
    {
        var parameters = new Dictionary<string, object>
        {
            {"group_id", groupId},
            {"user_id", userId},
            {"duration", (int)duration.TotalSeconds}
        };
        await RequestAsync(Actions.SetGroupBan, parameters, selfId).ConfigureAwait(false);
    }

    public async Task SetFriendAddRequest(FriendAddRequest request, string selfId)
    {
        var parameters = new Dictionary<string, object>
        {
            {"flag", request.Flag},
            {"approve", request.Approve},
            {"remark", request.Remark}
        };

        await RequestAsync(Actions.SetFriendAddRequest, parameters, selfId).ConfigureAwait(false);
    }

    public async Task SetGroupAddRequest(GroupAddRequest request, string selfId)
    {
        var parameters = new Dictionary<string, object>
        {
            {"flag", request.Flag},
            {"sub_type", request.SubType},
            {"type", request.Type},
            {"approve", request.Approve},
            {"reason", request.Reason}
        };

        await RequestAsync(Actions.SetGroupAddRequest, parameters, selfId).ConfigureAwait(false);
    }

    #endregion

    private async Task RequestAsync(string url, IDictionary<string, object>? parameters, string selfId)
    {
        var response = await _oneBotConnector.SendMessageAsync(url, parameters, selfId).ConfigureAwait(false);
        if (response == null)
            throw new Exception("未知错误，请检查连接是否正常");

        if (string.Equals(response.Status, "failed", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(response.Wording))
                throw new OneBotApiException(response.Msg, response.Wording);
            throw new Exception("未知错误");
        }
    }

    private async Task<T> RequestAsync<T>(string url, IDictionary<string, object>? parameters, string selfId)
    {
        var response = await _oneBotConnector.SendMessageAsync<T>(url, parameters, selfId).ConfigureAwait(false);
        if (response == null)
            throw new Exception("未知错误，请检查连接是否正常");

        if (string.Equals(response.Status, "failed", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(response.Wording))
                throw new OneBotApiException(response.Msg, response.Wording);
            throw new Exception("未知错误");
        }

        return response.Data;
    }

    private static string ResolveRequiredSelfId(MessageContext messageContext)
    {
        return messageContext.SelfId
               ?? throw new InvalidOperationException("OneBot message context requires self_id in multi-account mode.");
    }

    Task<string> IMessageApi.SendPrivateMessageAsync(string userId, string message, IRichMessage? richMessage, MessageContext messageContext)
    {
        return SendPrivateMessageAsync(long.Parse(userId), message, ResolveRequiredSelfId(messageContext));
    }

    Task<string> IMessageApi.SendChannelMessageAsync(string channelId, string message, IRichMessage? richMessage, MessageContext messageContext,
        string? subChannelId)
    {
        var selfId = ResolveRequiredSelfId(messageContext);
        if (subChannelId == null) return SendGroupMessageAsync(long.Parse(channelId), message, selfId);
        return SendGuildChannelMessageAsync(long.Parse(channelId), long.Parse(subChannelId), message, selfId);
    }
}