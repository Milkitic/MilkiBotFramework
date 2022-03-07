using System.Diagnostics;
using System.Text;
using MilkiBotFramework.ContactsManaging.Models;
using MilkiBotFramework.ContactsManaging.Results;

namespace MilkiBotFramework.ContactsManaging;

[DebuggerDisplay("{DebuggerDisplay}")]
public sealed class ContactsUpdateSingleEvent
{
    public string? ChangedPath { get; init; }
    public MemberInfo? MemberInfo { get; init; }
    public PrivateInfo? PrivateInfo { get; init; }
    public ChannelInfo? ChannelInfo { get; init; }
    public ChannelInfo? SubChannelInfo { get; init; }
    public ContactsUpdateRole UpdateRole { get; init; }
    public ContactsUpdateType UpdateType { get; init; }

    private string DebuggerDisplay
    {
        get
        {
            var sb = new StringBuilder($"Role={UpdateRole};Type={UpdateType};");
            switch (UpdateRole)
            {
                case ContactsUpdateRole.Channel:
                    sb.Append($"Id={ChannelInfo!.ChannelId}");
                    break;
                case ContactsUpdateRole.SubChannel:
                    sb.Append($"Id={ChannelInfo!.ChannelId}.{ChannelInfo.SubChannelId}");
                    break;
                case ContactsUpdateRole.Member:
                    sb.Append($"Id={MemberInfo!.ChannelId}.{MemberInfo.UserId}");
                    break;
                case ContactsUpdateRole.Private:
                    sb.Append($"Id={PrivateInfo!.UserId}");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return sb.ToString();
        }
    }
}