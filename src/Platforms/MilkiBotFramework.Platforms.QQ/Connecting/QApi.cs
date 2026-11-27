using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Messaging.RichMessages;
using MilkiBotFramework.Platforms.QQ.Connecting.Models;
using MilkiBotFramework.Platforms.QQ.Messaging;
using MilkiBotFramework.Platforms.QQ.Messaging.RichMessages;
using MilkiBotFramework.Platforms.QQ.ObjectStore;

namespace MilkiBotFramework.Platforms.QQ.Connecting;

public class QApi : IPlatformMessageApi, IQGroupMemberApi
{
    private readonly LightHttpClient _lightHttpClient;
    private readonly IObjectStorageProvider _objectStorageProvider;
    private readonly ConditionalWeakTable<MessageContext, ReplySequence> _replySequences = new();

    public QApi(LightHttpClient lightHttpClient, IObjectStorageProvider objectStorageProvider, QApiConnector connector)
    {
        _lightHttpClient = lightHttpClient;
        _objectStorageProvider = objectStorageProvider;
        Connector = connector;
    }

    public string PlatformId => PlatformIds.Qq;

    public QApiConnector Connector { get; }

    public bool Supports(MessageContext messageContext)
    {
        return messageContext is QMessageContext;
    }

    public Task<string> SendPrivateMessageAsync(string userId, string message, IRichMessage? richMessage, MessageContext messageContext)
    {
        var isReply = messageContext.MessageIdentity switch
        {
            { MessageType: MessageType.Private } identity => identity.Id == userId,
            { MessageType: MessageType.Channel } => messageContext.MessageUserIdentity?.UserId == userId,
            _ => false
        };
        return SendMessageCoreAsync("users", userId, message, richMessage, isReply ? messageContext : null);
    }

    public Task<string> SendChannelMessageAsync(string channelId, string message, IRichMessage? richMessage, MessageContext messageContext, string? subChannelId)
    {
        var isReply = messageContext.MessageIdentity is { MessageType: MessageType.Channel } identity && identity.Id == channelId;
        return SendMessageCoreAsync("groups", channelId, message, richMessage, isReply ? messageContext : null);
    }

    /// <summary>
    /// 向已授权接收主动消息的 QQ 群发送文本（通过统一频道接口调用）。
    /// </summary>
    public Task<string> SendChannelMessageAsync(string channelId, string content, CancellationToken cancellationToken = default)
    {
        return SendGroupMessageAsync(channelId, content, cancellationToken);
    }

    /// <summary>
    /// 向已授权接收主动消息的 QQ 群发送文本及图片（通过统一频道接口调用）。
    /// </summary>
    public Task<string> SendChannelMessageAsync(string channelId, IRichMessage message, CancellationToken cancellationToken = default)
    {
        return SendGroupMessageAsync(channelId, message, cancellationToken);
    }

    /// <summary>
    /// 向已授权接收主动消息的 QQ 群发送文本。使用该机器人的群 OpenID。
    /// </summary>
    public Task<string> SendGroupMessageAsync(string groupOpenId, string content, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        return SendMessageCoreAsync("groups", groupOpenId, content, null, null, cancellationToken);
    }

    /// <summary>
    /// 向已授权接收主动消息的 QQ 群发送文本及图片。每张图片对应一次消息发送。
    /// </summary>
    public Task<string> SendGroupMessageAsync(string groupOpenId, IRichMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        return SendMessageCoreAsync("groups", groupOpenId, null, message, null, cancellationToken);
    }

    /// <summary>
    /// 获取指定 QQ 群成员的信息（包括 <c>member_role</c>）。该接口通常需要开放平台白名单权限。
    /// </summary>
    public async Task<QGroupMember> GetGroupMemberAsync(string groupOpenId, string memberOpenId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupOpenId);
        ArgumentException.ThrowIfNullOrWhiteSpace(memberOpenId);

        var url = $"https://{Connector.Host}/v2/groups/{Uri.EscapeDataString(groupOpenId)}/members/{Uri.EscapeDataString(memberOpenId)}";
        var result = await GetAsync(url, null, cancellationToken);
        var member = QMemberRoleParser.ParseGroupMember(result);
        if (member == null)
        {
            throw new QApiException("QQ 群成员响应格式无效", null);
        }

        if (string.IsNullOrWhiteSpace(member.MemberOpenId))
        {
            return new QGroupMember
            {
                MemberOpenId = memberOpenId,
                Username = member.Username,
                MemberNickname = member.MemberNickname,
                MemberRoleRaw = member.MemberRoleRaw,
                Role = member.Role,
                IsBot = member.IsBot,
                JoinedAt = member.JoinedAt,
                UnionOpenId = member.UnionOpenId
            };
        }

        return member;
    }

    /// <summary>
    /// 分页获取指定 QQ 群的成员列表（每页最多 30 条）。该接口通常需要开放平台白名单权限。
    /// </summary>
    public async Task<QGroupMembersPage> GetGroupMembersPageAsync(string groupOpenId, string? cursor = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupOpenId);

        var url = $"https://{Connector.Host}/v2/groups/{Uri.EscapeDataString(groupOpenId)}/members";
        var queries = string.IsNullOrEmpty(cursor)
            ? null
            : new Dictionary<string, string> { ["cursor"] = cursor };
        var result = await GetAsync(url, queries, cancellationToken);

        var members = new List<QGroupMember>();
        if (result["members"] is JsonArray membersArray)
        {
            foreach (var item in membersArray)
            {
                var parsed = QMemberRoleParser.ParseGroupMember(item);
                if (parsed != null && !string.IsNullOrWhiteSpace(parsed.MemberOpenId))
                {
                    members.Add(parsed);
                }
            }
        }

        var nextCursor = QMemberRoleParser.GetOptionalString(result["next_cursor"]);
        return new QGroupMembersPage
        {
            Members = members,
            NextCursor = string.IsNullOrEmpty(nextCursor) ? null : nextCursor
        };
    }

    /// <summary>
    /// 拉取指定 QQ 群的全部成员列表（自动分页）。该接口通常需要开放平台白名单权限。
    /// </summary>
    public async Task<IReadOnlyList<QGroupMember>> GetGroupMembersAsync(string groupOpenId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupOpenId);

        var membersById = new Dictionary<string, QGroupMember>(StringComparer.Ordinal);
        var seenCursors = new HashSet<string>(StringComparer.Ordinal);
        string? cursor = null;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = await GetGroupMembersPageAsync(groupOpenId, cursor, cancellationToken);
            foreach (var member in page.Members)
            {
                membersById[member.MemberOpenId] = member;
            }

            if (string.IsNullOrEmpty(page.NextCursor) || !seenCursors.Add(page.NextCursor))
            {
                break;
            }

            cursor = page.NextCursor;
        }

        return membersById.Values.ToList();
    }

    private async Task<string> SendMessageCoreAsync(string entityType, string entityId, string? message,
        IRichMessage? richMessage, MessageContext? replyContext, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);
        cancellationToken.ThrowIfCancellationRequested();

        var messageId = replyContext?.MessageId;
        if (replyContext != null && string.IsNullOrWhiteSpace(messageId))
        {
            throw new ArgumentException("QQ 被动回复需要入站消息 ID。", nameof(replyContext));
        }

        var (imageMessages, text) = richMessage == null ? (new List<IRichMessage>(), message) : await PrepareMessagesAsync(richMessage);
        var targetUrl = $"https://{Connector.Host}/v2/{entityType}/{Uri.EscapeDataString(entityId)}";
        var messageUrl = $"{targetUrl}/messages";

        if (imageMessages.Count == 0)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";

            var request = new JsonObject { ["msg_type"] = 0, ["content"] = text };
            AddReplyFields(request, replyContext, messageId);
            return (await PostAsync(messageUrl, request, cancellationToken)).ToJsonString();
        }

        var sb = new StringBuilder();
        for (var i = 0; i < imageMessages.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var imageMessage = imageMessages[i];
            var imageUrl = imageMessage switch
            {
                MemoryImage memoryImage => await _objectStorageProvider.UploadImage(memoryImage.ImageSource, memoryImage.ImageEncodingOptions),
                LinkImage linkImage => linkImage.Uri,
                FileImage fileImage => await _objectStorageProvider.UploadImage(fileImage.Path),
                _ => throw new ArgumentOutOfRangeException(nameof(imageMessage), imageMessage.GetType(), null)
            };

            var uploadRequest = new
            {
                file_type = 1,
                url = imageUrl,
                srv_send_msg = false
            };
            var uploadResult = await PostAsync($"{targetUrl}/files", uploadRequest, cancellationToken);
            var fileInfo = uploadResult["file_info"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(fileInfo))
            {
                throw new QApiException("QQ 富媒体上传响应缺少 file_info", null);
            }

            var request = new JsonObject
            {
                ["msg_type"] = 7,
                ["media"] = new JsonObject { ["file_info"] = fileInfo }
            };
            if (i == imageMessages.Count - 1 && text != null)
            {
                request["content"] = text;
            }

            AddReplyFields(request, replyContext, messageId);
            var sendResult = await PostAsync(messageUrl, request, cancellationToken);
            sb.AppendLine(uploadResult.ToJsonString());
            sb.AppendLine(sendResult.ToJsonString());
        }

        return sb.ToString();
    }

    private void AddReplyFields(JsonObject request, MessageContext? replyContext, string? messageId)
    {
        if (replyContext == null) return;

        var sequence = _replySequences.GetValue(replyContext, static _ => new ReplySequence());
        request["msg_id"] = messageId;
        request["msg_seq"] = Interlocked.Increment(ref sequence.Value);
    }

    private async Task<JsonObject> GetAsync(string url, IReadOnlyDictionary<string, string>? queries, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var headers = new Dictionary<string, string> { ["Authorization"] = await Connector.GetAuthorizationAsync(cancellationToken) };
        var (statusCode, body) = await _lightHttpClient.HttpGetOnceAsync(url, queries, headers, cancellationToken);
        return ParseResponse(statusCode, body);
    }

    private async Task<JsonObject> PostAsync(string url, object request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var headers = new Dictionary<string, string> { ["Authorization"] = await Connector.GetAuthorizationAsync(cancellationToken) };
        var (statusCode, body) = await _lightHttpClient.HttpPostOnceAsync(url, request, headers, cancellationToken);
        return ParseResponse(statusCode, body);
    }

    private static JsonObject ParseResponse(HttpStatusCode statusCode, string body)
    {
        var success = (int)statusCode is >= 200 and < 300;

        JsonObject? result;
        try
        {
            result = JsonNode.Parse(body) as JsonObject;
        }
        catch (JsonException) when (!success)
        {
            result = null;
        }

        var code = result?["code"]?.ToString();
        if (!success || code is not (null or "0"))
        {
            throw new QApiException(code ?? $"HTTP {(int)statusCode}", result?["message"]?.ToString() ?? body, statusCode);
        }

        return result ?? throw new QApiException("QQ API 响应格式无效", null, statusCode);
    }

    private static async Task<(List<IRichMessage> Images, string? Text)> PrepareMessagesAsync(IRichMessage richMessage)
    {
        if (richMessage is At) return ([], null);

        var message = richMessage as RichMessage ?? new RichMessage(richMessage);
        var parts = message.Where(part => part is not (RichMessage or Reply)).ToList();
        var images = parts.Where(IsImage).ToList();
        if (images.Count == 0) return (images, await new RichMessage(parts).EncodeAsync());
        if (images.Count == parts.Count) return (images, null);

        var firstImage = parts.FindIndex(IsImage);
        var lastImage = parts.FindLastIndex(IsImage);
        if (lastImage - firstImage + 1 == images.Count)
        {
            if (firstImage == 0)
            {
                return (images, "(见上图)" + await new RichMessage(parts.Skip(lastImage + 1)).EncodeAsync());
            }

            if (lastImage == parts.Count - 1)
            {
                return (images, await new RichMessage(parts.Take(firstImage)).EncodeAsync() + "(见上图)");
            }
        }

        var imageIndex = 0;
        var withPlaceholders = parts.Select(part => IsImage(part) ? new ImagePlaceholder(++imageIndex) : part);
        return (images, await new RichMessage(withPlaceholders).EncodeAsync());
    }

    private static bool IsImage(IRichMessage message)
    {
        return message is MemoryImage or FileImage or LinkImage;
    }

    private sealed class ReplySequence
    {
        public int Value;
    }
}
