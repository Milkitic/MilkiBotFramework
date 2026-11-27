using MilkiBotFramework.Platforms.QQ.Connecting.Models;

namespace MilkiBotFramework.Platforms.QQ.Connecting;

internal interface IQGroupMemberApi
{
    Task<QGroupMember> GetGroupMemberAsync(string groupOpenId, string memberOpenId, CancellationToken cancellationToken = default);
    Task<QGroupMembersPage> GetGroupMembersPageAsync(string groupOpenId, string? cursor = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QGroupMember>> GetGroupMembersAsync(string groupOpenId, CancellationToken cancellationToken = default);
}
