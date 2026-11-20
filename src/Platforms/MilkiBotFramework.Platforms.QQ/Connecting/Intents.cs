namespace MilkiBotFramework.Platforms.QQ.Connecting;

/// <summary>在登录 QQ WebSocket 网关时订阅的事件类型位掩码。</summary>
/// <seealso href="https://bot.qq.com/wiki/develop/api-v2/dev-prepare/interface-framework/event-emit.html"/>
[Flags]
public enum Intents
{
    /// <summary>频道及子频道的创建、更新和删除。</summary>
    Guilds = 1 << 0,
    /// <summary>频道成员变更。</summary>
    GuildMembers = 1 << 1,
    /// <summary>私域频道的消息事件。</summary>
    GuildMessages = 1 << 9,
    /// <summary>频道消息的表情表态变更。</summary>
    GuildMessageReactions = 1 << 10,
    /// <summary>频道私信事件。</summary>
    DirectMessage = 1 << 12,
    /// <summary>QQ 群聊、单聊及其关系变更事件。</summary>
    GroupAndC2CEvent = 1 << 25,
    /// <summary>按钮等消息组件触发的交互事件。</summary>
    Interaction = 1 << 26,
    /// <summary>消息审核结果通知。</summary>
    MessageAudit = 1 << 27,
    /// <summary>论坛的主题、帖子和回复事件。</summary>
    ForumsEvent = 1 << 28,
    /// <summary>音频播放状态事件。</summary>
    AudioAction = 1 << 29,
    /// <summary>公域频道的提及和消息撤回事件。</summary>
    PublicGuildMessages = 1 << 30,
}
