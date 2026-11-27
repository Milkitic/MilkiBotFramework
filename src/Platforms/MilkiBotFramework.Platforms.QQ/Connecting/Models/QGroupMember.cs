using System.Globalization;
using System.Text.Json.Nodes;
using MilkiBotFramework.ContactsManaging.Models;

namespace MilkiBotFramework.Platforms.QQ.Connecting.Models;

/// <summary>
/// QQ 群成员信息模型。
/// </summary>
public sealed class QGroupMember
{
    /// <summary>
    /// 群成员 OpenID。
    /// </summary>
    public string MemberOpenId { get; init; } = string.Empty;

    /// <summary>
    /// 用户昵称。
    /// </summary>
    public string? Username { get; init; }

    /// <summary>
    /// 群内昵称（群名片）。
    /// </summary>
    public string? MemberNickname { get; init; }

    /// <summary>
    /// 原始群成员角色字符串（如 owner、admin、member）。
    /// </summary>
    public string? MemberRoleRaw { get; init; }

    /// <summary>
    /// 解析后的框架成员角色；若字段缺失或未知则为 <c>null</c>。
    /// </summary>
    public MemberRole? Role { get; init; }

    /// <summary>
    /// 是否为机器人账号。
    /// </summary>
    public bool IsBot { get; init; }

    /// <summary>
    /// 入群时间。
    /// </summary>
    public DateTimeOffset? JoinedAt { get; init; }

    /// <summary>
    /// 跨应用关联用户 OpenID。
    /// </summary>
    public string? UnionOpenId { get; init; }
}

/// <summary>
/// QQ 群成员分页查询结果。
/// </summary>
public sealed class QGroupMembersPage
{
    /// <summary>
    /// 当前页群成员列表。
    /// </summary>
    public IReadOnlyList<QGroupMember> Members { get; init; } = Array.Empty<QGroupMember>();

    /// <summary>
    /// 下一页游标；为空表示已到达末页。
    /// </summary>
    public string? NextCursor { get; init; }
}

internal static class QMemberRoleParser
{
    public static bool TryParse(string? rawRole, out MemberRole role)
    {
        if (string.IsNullOrWhiteSpace(rawRole))
        {
            role = default;
            return false;
        }

        switch (rawRole.Trim().ToLowerInvariant())
        {
            case "owner":
                role = MemberRole.Owner;
                return true;
            case "admin":
                role = MemberRole.Admin;
                return true;
            case "member":
                role = MemberRole.Member;
                return true;
            default:
                role = default;
                return false;
        }
    }

    public static QGroupMember? ParseGroupMember(JsonNode? node)
    {
        if (node is not JsonObject obj)
        {
            return null;
        }

        var memberOpenId = GetOptionalString(obj["member_openid"]);
        var username = GetOptionalString(obj["username"]);
        var memberNickname = GetOptionalString(obj["member_nickname"]);
        var memberRoleRaw = GetOptionalString(obj["member_role"]);
        var unionOpenId = GetOptionalString(obj["union_openid"]);

        MemberRole? role = TryParse(memberRoleRaw, out var parsedRole) ? parsedRole : null;

        var isBot = false;
        if (obj["bot"] is JsonValue botVal && botVal.TryGetValue<bool>(out var parsedBot))
        {
            isBot = parsedBot;
        }

        DateTimeOffset? joinedAt = null;
        var joinedAtRaw = GetOptionalString(obj["joined_at"]);
        if (!string.IsNullOrWhiteSpace(joinedAtRaw) &&
            DateTimeOffset.TryParse(joinedAtRaw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsedJoinedAt))
        {
            joinedAt = parsedJoinedAt;
        }

        return new QGroupMember
        {
            MemberOpenId = memberOpenId ?? string.Empty,
            Username = username,
            MemberNickname = memberNickname,
            MemberRoleRaw = memberRoleRaw,
            Role = role,
            IsBot = isBot,
            JoinedAt = joinedAt,
            UnionOpenId = unionOpenId
        };
    }

    internal static string? GetOptionalString(JsonNode? node)
    {
        if (node is not JsonValue value)
        {
            return null;
        }

        return value.TryGetValue<string>(out var str) ? str : null;
    }
}
