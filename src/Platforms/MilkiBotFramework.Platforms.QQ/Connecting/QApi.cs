using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Messaging.RichMessages;
using MilkiBotFramework.Platforms.QQ.Messaging;
using MilkiBotFramework.Platforms.QQ.Messaging.RichMessages;
using MilkiBotFramework.Platforms.QQ.ObjectStore;

namespace MilkiBotFramework.Platforms.QQ.Connecting;

/// <summary>调用 QQ 机器人官方 OpenAPI，并将框架消息发送到 QQ 单聊和群聊。</summary>
public class QApi : IPlatformMessageApi
{
    private readonly LightHttpClient _lightHttpClient;
    private readonly IObjectStorageProvider _objectStorageProvider;
    private readonly ConditionalWeakTable<MessageContext, ReplySequence> _replySequences = new();

    /// <summary>创建使用指定机器人凭证和对象存储的 QQ 接口客户端。</summary>
    /// <param name="lightHttpClient">底层 HTTP 客户端。</param>
    /// <param name="objectStorageProvider">将本地和内存图片转换为可供 QQ 访问的 URL 的存储服务。</param>
    /// <param name="connector">提供当前机器人凭证和 API 环境的连接器。</param>
    public QApi(LightHttpClient lightHttpClient, IObjectStorageProvider objectStorageProvider, QApiConnector connector)
    {
        _lightHttpClient = lightHttpClient;
        _objectStorageProvider = objectStorageProvider;
        Connector = connector;
    }

    /// <summary>框架中 QQ 官方平台的标识。</summary>
    public string PlatformId => PlatformIds.Qq;

    /// <summary>当前客户端使用的 QQ 连接器。</summary>
    public QApiConnector Connector { get; }

    /// <summary>判断框架消息上下文能否由此 QQ 客户端处理。</summary>
    /// <param name="messageContext">待检查的消息上下文。</param>
    /// <returns>上下文是 <see cref="QMessageContext"/> 时返回 <see langword="true"/>。</returns>
    public bool Supports(MessageContext messageContext)
    {
        return messageContext is QMessageContext;
    }

    /// <summary>向 QQ 用户发送框架消息。目标是上下文中的消息发送者时，按被动回复发送。</summary>
    /// <param name="userId">该机器人的目标用户 OpenID。</param>
    /// <param name="message">未提供富消息时使用的文本。</param>
    /// <param name="richMessage">可选的框架富消息。</param>
    /// <param name="messageContext">用于关联被动回复的入站消息上下文。</param>
    /// <returns>平台响应。发送图片时，每行是一次上传或发送响应。</returns>
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

    /// <summary>向 QQ 群发送框架消息。目标是入站消息所在群时，按被动回复发送。</summary>
    /// <param name="channelId">该机器人的目标群 OpenID。</param>
    /// <param name="message">未提供富消息时使用的文本。</param>
    /// <param name="richMessage">可选的框架富消息。</param>
    /// <param name="messageContext">用于关联被动回复的入站消息上下文。</param>
    /// <param name="subChannelId">框架通用子频道参数。QQ 群聊请传空值。</param>
    /// <returns>平台响应。发送图片时，每行是一次上传或发送响应。</returns>
    public Task<string> SendChannelMessageAsync(string channelId, string message, IRichMessage? richMessage, MessageContext messageContext, string? subChannelId)
    {
        var isReply = messageContext.MessageIdentity is { MessageType: MessageType.Channel } identity && identity.Id == channelId;
        return SendMessageCoreAsync("groups", channelId, message, richMessage, isReply ? messageContext : null);
    }

    /// <summary>
    /// 向已授权接收主动消息的 QQ 群发送文本。使用该机器人的群 OpenID。
    /// </summary>
    /// <param name="groupOpenId">目标群 OpenID。</param>
    /// <param name="content">非空的文本内容。</param>
    /// <param name="cancellationToken">取消发送的令牌。</param>
    /// <returns>发送接口的 JSON 响应。</returns>
    public Task<string> SendGroupMessageAsync(string groupOpenId, string content, CancellationToken cancellationToken = default)
    {
        return SendProactiveTextAsync("groups", groupOpenId, content, cancellationToken);
    }

    /// <summary>
    /// 向已授权接收主动消息的 QQ 群发送文本及图片。每张图片对应一次消息发送。
    /// </summary>
    /// <param name="groupOpenId">目标群 OpenID。</param>
    /// <param name="message">待发送的框架富消息。</param>
    /// <param name="cancellationToken">取消后续请求的令牌。</param>
    /// <returns>平台响应。发送图片时，每行是一次上传或发送响应。</returns>
    public Task<string> SendGroupMessageAsync(string groupOpenId, IRichMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        return SendMessageCoreAsync("groups", groupOpenId, null, message, null, cancellationToken);
    }

    /// <summary>向已授权接收主动消息的 QQ 用户发送文本。</summary>
    /// <param name="userOpenId">目标用户 OpenID。</param>
    /// <param name="content">非空的文本内容。</param>
    /// <param name="cancellationToken">取消发送的令牌。</param>
    /// <returns>发送接口的 JSON 响应。</returns>
    public Task<string> SendPrivateMessageAsync(string userOpenId, string content, CancellationToken cancellationToken = default)
    {
        return SendProactiveTextAsync("users", userOpenId, content, cancellationToken);
    }

    /// <summary>向已授权接收主动消息的 QQ 用户发送框架文本或图片。</summary>
    /// <param name="userOpenId">目标用户 OpenID。</param>
    /// <param name="message">待发送的框架富消息。</param>
    /// <param name="cancellationToken">取消后续请求的令牌。</param>
    /// <returns>平台响应。发送图片时，每行是一次上传或发送响应。</returns>
    public Task<string> SendPrivateMessageAsync(string userOpenId, IRichMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        return SendMessageCoreAsync("users", userOpenId, null, message, null, cancellationToken);
    }

    /// <summary>按官方请求结构发送群消息，支持 Markdown、键盘、引用和事件回复等字段。</summary>
    /// <param name="groupOpenId">目标群 OpenID。</param>
    /// <param name="payload">官方接口请求体。使用 <c>msg_id</c> 和 <c>event_id</c> 之一进行被动回复。</param>
    /// <param name="cancellationToken">取消发送的令牌。</param>
    /// <returns>QQ 返回的消息或审核信息。</returns>
    public Task<JsonObject> SendGroupPayloadAsync(string groupOpenId, JsonObject payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return PostAsync(TargetPath("groups", groupOpenId) + "/messages", payload, cancellationToken);
    }

    /// <summary>按官方请求结构发送单聊消息，支持 Markdown、键盘、事件回复和互动召回等字段。</summary>
    /// <param name="userOpenId">目标用户 OpenID。</param>
    /// <param name="payload">官方接口请求体。<c>msg_id</c>、<c>event_id</c> 和 <c>is_wakeup</c> 的发送模式互斥。</param>
    /// <param name="cancellationToken">取消发送的令牌。</param>
    /// <returns>QQ 返回的消息或审核信息。</returns>
    public Task<JsonObject> SendPrivatePayloadAsync(string userOpenId, JsonObject payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return PostAsync(TargetPath("users", userOpenId) + "/messages", payload, cancellationToken);
    }

    /// <summary>发送一个单聊流式消息分片。</summary>
    /// <param name="userOpenId">目标用户 OpenID。</param>
    /// <param name="payload">官方流式请求体。分片序号从零递增，后续分片携带前一次返回的流式消息 ID。</param>
    /// <param name="cancellationToken">取消当前分片请求的令牌。</param>
    /// <returns>当前分片的响应，其中 <c>id</c> 用于标识这一条流式消息。</returns>
    public Task<JsonObject> SendPrivateStreamChunkAsync(string userOpenId, JsonObject payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return PostAsync(TargetPath("users", userOpenId) + "/stream_messages", payload, cancellationToken);
    }

    /// <summary>上传群聊富媒体。仅上传时返回可用于该群发消息的 <c>file_info</c>。</summary>
    /// <param name="groupOpenId">目标群 OpenID。</param>
    /// <param name="payload">官方上传请求体。可使用 URL、二进制内容或已完成分片上传的 <c>upload_id</c>。</param>
    /// <param name="cancellationToken">取消上传的令牌。</param>
    /// <returns>腾讯返回的文件信息。直接发送时可能包含消息 ID。</returns>
    public Task<JsonObject> UploadGroupMediaAsync(string groupOpenId, JsonObject payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return PostAsync(TargetPath("groups", groupOpenId) + "/files", payload, cancellationToken);
    }

    /// <summary>上传单聊富媒体。仅上传时返回可用于向该用户发消息的 <c>file_info</c>。</summary>
    /// <param name="userOpenId">目标用户 OpenID。</param>
    /// <param name="payload">官方上传请求体。可使用 URL、二进制内容或已完成分片上传的 <c>upload_id</c>。</param>
    /// <param name="cancellationToken">取消上传的令牌。</param>
    /// <returns>腾讯返回的文件信息。直接发送时可能包含消息 ID。</returns>
    public Task<JsonObject> UploadPrivateMediaAsync(string userOpenId, JsonObject payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return PostAsync(TargetPath("users", userOpenId) + "/files", payload, cancellationToken);
    }

    /// <summary>创建群聊富媒体分片上传任务。</summary>
    /// <param name="groupOpenId">目标群 OpenID。</param>
    /// <param name="payload">包含文件类型、字节大小、文件名和校验值的官方请求体。</param>
    /// <param name="cancellationToken">取消请求的令牌。</param>
    /// <returns>上传任务 ID、分片大小和预签名上传地址。</returns>
    public Task<JsonObject> PrepareGroupMediaUploadAsync(string groupOpenId, JsonObject payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return PostAsync(TargetPath("groups", groupOpenId) + "/upload_prepare", payload, cancellationToken);
    }

    /// <summary>创建单聊富媒体分片上传任务。</summary>
    /// <param name="userOpenId">目标用户 OpenID。</param>
    /// <param name="payload">包含文件类型、字节大小、文件名和校验值的官方请求体。</param>
    /// <param name="cancellationToken">取消请求的令牌。</param>
    /// <returns>上传任务 ID、分片大小和预签名上传地址。</returns>
    public Task<JsonObject> PreparePrivateMediaUploadAsync(string userOpenId, JsonObject payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return PostAsync(TargetPath("users", userOpenId) + "/upload_prepare", payload, cancellationToken);
    }

    /// <summary>将已通过预签名地址上传的群聊分片标记为完成。</summary>
    /// <param name="groupOpenId">创建上传任务时使用的群 OpenID。</param>
    /// <param name="payload">包含上传任务 ID、分片序号、实际大小和分片校验值的官方请求体。</param>
    /// <param name="cancellationToken">取消请求的令牌。</param>
    public Task FinishGroupMediaUploadPartAsync(string groupOpenId, JsonObject payload, CancellationToken cancellationToken = default)
    {
        return FinishMediaUploadPartAsync("groups", groupOpenId, payload, cancellationToken);
    }

    /// <summary>将已通过预签名地址上传的单聊分片标记为完成。</summary>
    /// <param name="userOpenId">创建上传任务时使用的用户 OpenID。</param>
    /// <param name="payload">包含上传任务 ID、分片序号、实际大小和分片校验值的官方请求体。</param>
    /// <param name="cancellationToken">取消请求的令牌。</param>
    public Task FinishPrivateMediaUploadPartAsync(string userOpenId, JsonObject payload, CancellationToken cancellationToken = default)
    {
        return FinishMediaUploadPartAsync("users", userOpenId, payload, cancellationToken);
    }

    /// <summary>撤回指定群中的消息。撤回权限和两分钟时限由 QQ 检查。</summary>
    /// <param name="groupOpenId">目标群 OpenID。</param>
    /// <param name="messageId">要撤回的消息 ID。</param>
    /// <param name="cancellationToken">取消撤回请求的令牌。</param>
    public Task RecallGroupMessageAsync(string groupOpenId, string messageId, CancellationToken cancellationToken = default)
    {
        return RecallAsync("groups", groupOpenId, messageId, cancellationToken);
    }

    /// <summary>撤回机器人发送给指定用户的单聊消息。QQ 要求在发送后两分钟内操作。</summary>
    /// <param name="userOpenId">目标用户 OpenID。</param>
    /// <param name="messageId">机器人发消息时返回的消息 ID。</param>
    /// <param name="cancellationToken">取消撤回请求的令牌。</param>
    public Task RecallPrivateMessageAsync(string userOpenId, string messageId, CancellationToken cancellationToken = default)
    {
        return RecallAsync("users", userOpenId, messageId, cancellationToken);
    }

    /// <summary>携带当前机器人的访问凭证调用同一 QQ OpenAPI 主机上的接口。请求只提交一次。</summary>
    /// <param name="method">接口要求的 HTTP 方法。</param>
    /// <param name="path">以斜杠开头的接口路径，可附带已编码的查询参数。</param>
    /// <param name="body">JSON 值或 <see cref="HttpContent"/>，例如频道上传所需的表单。空值省略请求体，传入的 HTTP 内容随请求释放。</param>
    /// <param name="cancellationToken">取消请求的令牌。</param>
    /// <returns>响应中的 JSON 值。成功且无响应体时为空。</returns>
    /// <exception cref="ArgumentException">路径未指向当前 QQ OpenAPI 主机。</exception>
    /// <exception cref="QApiException">QQ 拒绝请求，或返回的成功响应不是有效的 JSON。</exception>
    public async Task<JsonNode?> CallOpenApiAsync(HttpMethod method, string path, object? body = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(method);
        var host = new Uri($"https://{Connector.Host}/");
        if (string.IsNullOrEmpty(path) || path[0] != '/' || path.StartsWith("//", StringComparison.Ordinal) ||
            path.Contains('\\') || !Uri.TryCreate(host, path, out var uri) || !host.IsBaseOf(uri) || uri.Fragment.Length != 0)
        {
            throw new ArgumentException("请输入 QQ OpenAPI 的接口路径。", nameof(path));
        }

        var headers = new Dictionary<string, string> { ["Authorization"] = await Connector.GetAuthorizationAsync(cancellationToken) };
        using var response = await _lightHttpClient.SendOnceAsync(method, uri, body, headers, cancellationToken);
        return await QOpenApiResponse.ReadAsync(response, cancellationToken);
    }

    private Task<string> SendProactiveTextAsync(string entityType, string entityId, string content, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        return SendMessageCoreAsync(entityType, entityId, content, null, null, cancellationToken);
    }

    private async Task RecallAsync(string entityType, string entityId, string messageId, CancellationToken cancellationToken)
    {
        await CallOpenApiAsync(HttpMethod.Delete, $"{TargetPath(entityType, entityId)}/messages/{EncodePathId(messageId)}",
            cancellationToken: cancellationToken);
    }

    private async Task FinishMediaUploadPartAsync(string entityType, string entityId, JsonObject payload,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);
        await CallOpenApiAsync(HttpMethod.Post, TargetPath(entityType, entityId) + "/upload_part_finish", payload, cancellationToken);
    }

    private static string TargetPath(string entityType, string entityId) => $"/v2/{entityType}/{EncodePathId(entityId)}";

    private static string EncodePathId(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (id is "." or "..")
        {
            throw new ArgumentException("无效的 QQ 标识。", nameof(id));
        }

        return Uri.EscapeDataString(id);
    }

    private async Task<string> SendMessageCoreAsync(string entityType, string entityId, string? message,
        IRichMessage? richMessage, MessageContext? replyContext, CancellationToken cancellationToken = default)
    {
        var targetPath = TargetPath(entityType, entityId);
        cancellationToken.ThrowIfCancellationRequested();

        var messageId = replyContext?.MessageId;
        if (replyContext != null && string.IsNullOrWhiteSpace(messageId))
        {
            throw new ArgumentException("QQ 被动回复需要入站消息 ID。", nameof(replyContext));
        }

        var (imageMessages, text) = richMessage == null ? (new List<IRichMessage>(), message) : await PrepareMessagesAsync(richMessage);
        var messagePath = $"{targetPath}/messages";

        if (imageMessages.Count == 0)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";

            var request = new JsonObject { ["msg_type"] = 0, ["content"] = text };
            AddReplyFields(request, replyContext, messageId);
            return (await PostAsync(messagePath, request, cancellationToken)).ToJsonString();
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
            var uploadResult = await PostAsync($"{targetPath}/files", uploadRequest, cancellationToken);
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
            var sendResult = await PostAsync(messagePath, request, cancellationToken);
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

    private async Task<JsonObject> PostAsync(string path, object request, CancellationToken cancellationToken)
    {
        return await CallOpenApiAsync(HttpMethod.Post, path, request, cancellationToken) as JsonObject
               ?? throw new QApiException("QQ API 响应缺少 JSON 对象", null);
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
