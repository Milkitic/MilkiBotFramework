# QQ 官方 OpenAPI 接入

调用 `UseQQ()` 后可注入 `QApi`。客户端使用当前机器人的 AppID、AppSecret 获取并缓存访问凭证。开发环境沿用沙箱地址，正式环境使用 `api.bot.qq.com`。群和单聊目标使用当前 AppID 获得的 OpenID。 [获取访问凭证](https://bot.qq.com/wiki/develop/api-v2/dev-prepare/access-token.html) · [API 调用指南](https://bot.qq.com/wiki/develop/api-v2/dev-prepare/api-call-guide.html)

| 用途 | 群聊 | 单聊 |
| --- | --- | --- |
| 主动发送文本或图片 | `SendGroupMessageAsync` | 无需消息上下文的 `SendPrivateMessageAsync` 重载 |
| 发送官方格式的消息 | `SendGroupPayloadAsync` | `SendPrivatePayloadAsync` |
| 发送流式消息分片 |  | `SendPrivateStreamChunkAsync` |
| 上传、转存 URL 或合并分片 | `UploadGroupMediaAsync` | `UploadPrivateMediaAsync` |
| 获取分片上传任务和预签名地址 | `PrepareGroupMediaUploadAsync` | `PreparePrivateMediaUploadAsync` |
| 通知 QQ 一个分片已上传 | `FinishGroupMediaUploadPartAsync` | `FinishPrivateMediaUploadPartAsync` |
| 撤回消息 | `RecallGroupMessageAsync` | `RecallPrivateMessageAsync` |

官方格式的发送方法接受 `JsonObject`，可传入 Markdown、键盘、引用和卡片等接口字段。从消息回复时传入事件体中的 `d.id` 作为 `msg_id`。响应支持回复的事件时，将外层 `id` 作为 `event_id`。两种被动回复字段互斥。单聊流式消息按 `index` 从 0 递增发送，续片携带上一分片返回的 `id` 作为 `stream_msg_id`，完成时使用 `input_state=10`。 [群聊发送](https://bot.qq.com/wiki/develop/api-v2/autogen/api/v2_groups_group_openid_messages.post.html) · [单聊发送](https://bot.qq.com/wiki/develop/api-v2/autogen/api/v2_users_user_openid_messages.post.html) · [单聊流式发送](https://bot.qq.com/wiki/develop/api-v2/autogen/api/v2_users_user_openid_stream_messages.post.html)

分片上传先调用准备接口，再按平台给定的分片大小将文件逐片 PUT 到对应的预签名 URL。PUT 使用普通 HTTP 客户端，预签名地址自带上传授权。每片完成后调用对应的分片完成方法，最后通过富媒体上传接口携带 `upload_id` 合并。发送富媒体时使用合并返回的 `file_info`，并留意其有效期。 [富媒体上传流程](https://bot.qq.com/wiki/develop/api-v2/server-inter/message/rich-media.html)

## 调用其他官方端点

频道、群管理、交互回执等端点可通过 `CallOpenApiAsync` 调用。路径从 `/` 开始，查询参数按需编码。请求体可以是 JSON 或 `HttpContent`，例如频道发送本地图片时所需的 `MultipartFormDataContent`。该入口使用机器人身份，接口权限由 QQ 平台校验。 [API 调用指南](https://bot.qq.com/wiki/develop/api-v2/dev-prepare/api-call-guide.html) · [频道消息格式](https://bot.qq.com/wiki/develop/api-v2/server-inter/channel/message/send.html)

```csharp
using System.Net.Http;

var botInfo = await qqApi.CallOpenApiAsync(HttpMethod.Get, "/users/@me",
    cancellationToken: cancellationToken);
```

成功且无响应体时返回空值。平台拒绝请求时，`QApiException` 提供 `Code`、`StatusCode` 和 `TraceId`。发送接口返回 HTTP 201 或 202 及审核码 `304023` 或 `304024` 时，表示请求已受理，消息仍在等待审核。发送请求只提交一次。 [API 响应和错误码](https://bot.qq.com/wiki/develop/api-v2/dev-prepare/api-call-guide.html)

## 接收事件

在 `Connection.CallbackPath` 配置与 QQ 开放平台一致的回调路径，也可填写完整 HTTPS URL。适配器完成地址验证，验签后发送回执，再将事件交给框架。群内 @、全量群消息和单聊消息进入普通消息处理流程。其他事件可以在插件的 `OnNoticeReceived` 中通过 `QMessageContext.EventType`、`EventId` 和 `RawJsonDocument` 处理。原始 JSON 文档只在当前消息作用域内有效，异步任务需要的字段应在离开作用域前取出。 [事件订阅与通知](https://bot.qq.com/wiki/develop/api-v2/dev-prepare/interface-framework/event-emit.html)
