namespace MilkiBotFramework.Messaging;

public enum MessageAuthority
{
    Unspecified, Public, SubAdmin, Admin, Root
}

public static class MessageAuthorityExtensions
{
    public static string ToFriendlyString(this MessageAuthority messageAuthority)
    {
        return messageAuthority switch
        {
            MessageAuthority.Unspecified => "未指定",
            MessageAuthority.Public => "公共",
            MessageAuthority.SubAdmin => "子频道管理员",
            MessageAuthority.Admin => "群内管理员",
            MessageAuthority.Root => "开发组",
            _ => throw new ArgumentOutOfRangeException(nameof(messageAuthority), messageAuthority, null)
        };
    }
}