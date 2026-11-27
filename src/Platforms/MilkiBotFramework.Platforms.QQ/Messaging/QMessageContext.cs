using System.Text.Json;
using MilkiBotFramework.ContactsManaging.Models;
using MilkiBotFramework.Messaging;

namespace MilkiBotFramework.Platforms.QQ.Messaging;

public class QMessageContext : MessageContext
{
    public JsonDocument RawJsonDocument { get; internal set; } = null!;
    public string RawMessage { get; internal set; } = null!;

    /// <summary>
    /// QQ 网关事件类型（如 <c>GROUP_AT_MESSAGE_CREATE</c>、<c>GROUP_MESSAGE_CREATE</c>、<c>GROUP_ADD_ROBOT</c> 等）。
    /// </summary>
    public string? EventType { get; internal set; }

    /// <summary>
    /// 消息事件中发送者在群内的角色；未携带或无法识别时为 <c>null</c>。
    /// </summary>
    public MemberRole? AuthorMemberRole { get; internal set; }

    /// <summary>
    /// 消息事件中发送者的用户昵称。
    /// </summary>
    public string? AuthorUsername { get; internal set; }

    /// <summary>
    /// 消息事件中发送者的群内昵称（群名片）。
    /// </summary>
    public string? AuthorMemberNickname { get; internal set; }

    public QMessageContext(IRichMessageConverter richMessageConverter) : base(richMessageConverter)
    {
    }
}
