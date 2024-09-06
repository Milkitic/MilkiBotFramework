using MilkiBotFramework.Connecting;
using MilkiBotFramework.Messaging.RichMessages;
using MilkiBotFramework.Platforms.GoCqHttp.Connecting.RequestModel;
using MilkiBotFramework.Platforms.GoCqHttp.Connecting.ResponseModel;
using MilkiBotFramework.Platforms.GoCqHttp.Connecting.ResponseModel.Guild;

namespace MilkiBotFramework.Platforms.GoCqHttp.Connecting;

public class GoCqApi : IMessageApi
{
    private readonly IGoCqConnector _goCqConnector;

    public GoCqApi(IConnector connector)
    {
        if (connector is IGoCqConnector goCqWsConnector)
        {
            Connector = connector;
            _goCqConnector = goCqWsConnector;
        }
        else
        {
            throw new Exception("Except for IGoCqConnector, but actual is " + connector.GetType());
        }
    }

    public IConnector Connector { get; }

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

    public async Task<LoginInfo> GetLoginInfo()
    {
        return await RequestAsync<LoginInfo>(Actions.GetLoginInfo, null).ConfigureAwait(false);
    }

    public async Task<GuildServiceProfile> GetGuildServiceProfile()
    {
        return await RequestAsync<GuildServiceProfile>(Actions.GetGuildServiceProfile, null).ConfigureAwait(false);
    }

    #endregion

    #region Messaging

    /// <summary>
    /// 获取消息
    /// </summary>
    /// <param name="messageId">gocq消息Id</param>
    /// <returns>MessageId</returns>
    public async Task<GetMsgResponse> GetMessage(long messageId)
    {
        var parameters = new Dictionary<string, object>
        {
            { "message_id", messageId },
        };
        var response = await RequestAsync<GetMsgResponse>(Actions.GetMsg, parameters).ConfigureAwait(false);
        return response;
    }

    /// <summary>
    /// 撤回消息
    /// </summary>
    /// <param name="messageId"></param>
    public async Task DeleteMessage(int messageId)
    {
        var parameters = new Dictionary<string, object>
        {
            { "message_id", messageId }
        };
        await RequestAsync(Actions.DeleteMsg, parameters).ConfigureAwait(false);
    }

    /// <summary>
    /// 发送私聊消息
    /// </summary>
    /// <param name="userId">对方 QQ 号</param>
    /// <param name="message">要发送的内容</param>
    /// <param name="groupId">主动发起临时会话群号(机器人本身必须是管理员/群主)</param>
    /// <param name="autoEscape">消息内容是否作为纯文本发送 ( 即不解析 CQ 码 ) , 只在 message 字段是字符串时有效</param>
    /// <returns>MessageId</returns>
    public async Task<string> SendPrivateMessageAsync(long userId,
        string message,
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
        var response = await RequestAsync<MsgResponse>(Actions.SendPrivateMsg, parameters).ConfigureAwait(false);
        return response.MessageId;
    }

    /// <summary>
    /// 发送群聊消息
    /// </summary>
    /// <param name="messageId">群号</param>
    /// <param name="message">要发送的内容</param>
    /// <returns>MessageId</returns>
    public async Task<string> SendGroupMessageAsync(long messageId, string message)
    {
        var parameters = new Dictionary<string, object>
        {
            { "group_id", messageId },
            { "message", message }
        };
        var response = await RequestAsync<MsgResponse>(Actions.SendGroupMsg, parameters).ConfigureAwait(false);
        return response.MessageId;
    }

    public async Task<string> SendGuildChannelMessageAsync(long guildId, long subChannelId, string message)
    {
        var parameters = new Dictionary<string, object>
        {
            { "guild_id", guildId },
            { "channel_id", subChannelId },
            { "message", message },
        };
        return (await RequestAsync<MsgResponse>(Actions.SendGuildChannelMsg, parameters).ConfigureAwait(false)).MessageId;
    }

    #endregion

    #region Contacts info

    public async Task<StrangerInfo> GetStrangerInfo(long userId, bool noCache = false)
    {
        var parameters = new Dictionary<string, object>
        {
            { "user_id", userId },
            { "no_cache", noCache }
        };
        return await RequestAsync<StrangerInfo>(Actions.GetStrangerInfo, parameters).ConfigureAwait(false);
    }

    public async Task<List<FriendInfo>> GetFriends()
    {
        return await RequestAsync<List<FriendInfo>>(Actions.GetFriendList, null).ConfigureAwait(false);
    }

    public async Task<List<GroupInfo>> GetGroups()
    {
        return await RequestAsync<List<GroupInfo>>(Actions.GetGroupList, null).ConfigureAwait(false);
    }

    public async Task<GroupInfo> GetGroupInfo(long groupId)
    {
        var parameters = new Dictionary<string, object>
        {
            { "group_id", groupId }
        };
        return await RequestAsync<GroupInfo>(Actions.GetGroupInfo, parameters).ConfigureAwait(false);
    }

    public async Task<List<GroupMember>> GetFuzzyGroupMembers(long groupId)
    {
        var parameters = new Dictionary<string, object>
        {
            { "group_id", groupId }
        };
        return await RequestAsync<List<GroupMember>>(Actions.GetGroupMemberList, parameters).ConfigureAwait(false);
    }

    public async Task<GroupMember> GetGroupMemberDetail(long groupId, long userId, bool noCache = false)
    {
        var parameters = new Dictionary<string, object>
        {
            { "group_id", groupId },
            { "user_id", userId },
            { "no_cache", noCache }
        };

        return await RequestAsync<GroupMember>(Actions.GetGroupMemberInfo, parameters).ConfigureAwait(false);
    }

    public async Task<List<GuildBrief>> GetGuilds()
    {
        return await RequestAsync<List<GuildBrief>>(Actions.GetGuildList, null).ConfigureAwait(false);
    }

    public async Task<GuildInfo> GetGuildMetaByGuest(long guildId)
    {
        var parameters = new Dictionary<string, object>
        {
            { "guild_id", guildId.ToString() } // temporary str
        };
        return await RequestAsync<GuildInfo>(Actions.GetGuildMetaByGuest, parameters).ConfigureAwait(false);
    }

    public async Task<List<SubChannelInfo>> GetGuildChannelList(long guildId)
    {
        var parameters = new Dictionary<string, object>
        {
            { "guild_id", guildId.ToString() } // temporary str
        };
        return await RequestAsync<List<SubChannelInfo>>(Actions.GetGuildChannelList, parameters).ConfigureAwait(false);
    }

    public async Task<GetGuildMembersResponse> GetGuildMembers(long guildId)
    {
        var parameters = new Dictionary<string, object>
        {
            { "guild_id", guildId.ToString() } // temporary str
        };
        return await RequestAsync<GetGuildMembersResponse>(Actions.GetGuildMembers, parameters).ConfigureAwait(false);
    }

    #endregion

    #region Operations

    public async Task SetGroupBan(long groupId, long userId, TimeSpan duration)
    {
        var parameters = new Dictionary<string, object>
        {
            {"group_id", groupId},
            {"user_id", userId},
            {"duration", (int)duration.TotalSeconds}
        };
        await RequestAsync(Actions.SetGroupBan, parameters).ConfigureAwait(false);
    }

    public async Task SetFriendAddRequest(FriendAddRequest request)
    {
        var parameters = new Dictionary<string, object>
        {
            {"flag", request.Flag},
            {"approve", request.Approve},
            {"remark", request.Remark}
        };

        await RequestAsync(Actions.SetFriendAddRequest, parameters).ConfigureAwait(false);
    }

    public async Task SetGroupAddRequest(GroupAddRequest request)
    {
        var parameters = new Dictionary<string, object>
        {
            {"flag", request.Flag},
            {"sub_type", request.SubType},
            {"type", request.Type},
            {"approve", request.Approve},
            {"reason", request.Reason}
        };

        await RequestAsync(Actions.SetGroupAddRequest, parameters).ConfigureAwait(false);
    }

    #endregion

    private async Task RequestAsync(string url, IDictionary<string, object>? parameters)
    {
        var response = await _goCqConnector.SendMessageAsync(url, parameters).ConfigureAwait(false);
        if (response == null)
            throw new Exception("未知错误，请检查连接是否正常");

        if (string.Equals(response.Status, "failed", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(response.Wording))
                throw new GoCqApiException(response.Msg, response.Wording);
            throw new Exception("未知错误");
        }
    }

    private async Task<T> RequestAsync<T>(string url, IDictionary<string, object>? parameters)
    {
        var response = await _goCqConnector.SendMessageAsync<T>(url, parameters).ConfigureAwait(false);
        if (response == null)
            throw new Exception("未知错误，请检查连接是否正常");

        if (string.Equals(response.Status, "failed", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(response.Wording))
                throw new GoCqApiException(response.Msg, response.Wording);
            throw new Exception("未知错误");
        }

        return response.Data;
    }

    Task<string> IMessageApi.SendPrivateMessageAsync(string userId, string message, IRichMessage? richMessage)
    {
        return SendPrivateMessageAsync(long.Parse(userId), message);
    }

    Task<string> IMessageApi.SendChannelMessageAsync(string channelId, string message, IRichMessage? richMessage, string? subChannelId)
    {
        if (subChannelId == null) return SendGroupMessageAsync(long.Parse(channelId), message);
        return SendGuildChannelMessageAsync(long.Parse(channelId), long.Parse(subChannelId), message);
    }
}