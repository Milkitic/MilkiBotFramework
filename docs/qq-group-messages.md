# QQ 群消息发送

QQ 群聊消息分为**主动消息**与**被动回复**两种模式，底层均调用 [`POST /v2/groups/{group_openid}/messages`](https://bot.qq.com/wiki/develop/api-v2/autogen/api/v2_groups_group_openid_messages.post.html) 接口，区别在于是否携带入站上下文 ID。

## 主动消息

适用于定时推送、后台通知等无入站消息的场景。发送时省略 `msg_id`、`event_id` 与 `msg_seq`。

- **前置条件**：目标群已添加机器人，且群管理员已在机器人资料页[开启主动消息通知](https://bot.qq.com/wiki/develop/api-v2/autogen/event/group_msg_receive.html)。
- **获取群 OpenID**：可从 `GROUP_AT_MESSAGE_CREATE` 或 [`GROUP_ADD_ROBOT`](https://bot.qq.com/wiki/develop/api-v2/autogen/event/group_add_robot.html) 事件的 `d.group_openid` 字段中提取并保存。

## 被动回复

用于响应群内消息或事件（详见 QQ [消息收发概述](https://bot.qq.com/wiki/develop/api-v2/server-inter/message/overview.html)）：

- **消息回复（`msg_id`）**：取入站消息的 `d.id`，有效期 **60 分钟**。单条消息默认最多回复 **5 次**；若机器人管理员同时是该群群主或管理员，上限为 **50 次**。框架会对同一入站上下文的多条回复自动递增 `msg_seq`。
- **事件回复（`event_id`）**：取事件外层的 `id`。
- **互斥约束**：`msg_id` 与 `event_id` 互斥，不可同时传入。

## 在插件或后台服务中调用

`UseQQ()` 会自动注册 `MilkiBotFramework.Platforms.QQ.Connecting.QApi`。在构造函数中注入 `QApi` 后，即可通过已保存的 `groupOpenId` 发送文本或图片：

```csharp
using MilkiBotFramework.Messaging.RichMessages;

await qqApi.SendGroupMessageAsync(groupOpenId, "今天的活动即将开始。", cancellationToken);
await qqApi.SendGroupMessageAsync(groupOpenId, new LinkImage(imageUrl), cancellationToken);
```

发送图片时的注意事项：

- **外链图片**：`LinkImage` 传入的地址须能被 QQ 服务端访问。
- **本地 / 内存图片**：沿用机器人的对象存储配置。
- **上传与配额**：适配器会先以 `srv_send_msg=false` [上传富媒体](https://bot.qq.com/wiki/develop/api-v2/autogen/api/v2_groups_group_openid_files.post.html)，再使用返回的 `file_info` 发送。包含多张图片时会逐张下发，**每张各自消耗一次主动消息配额**。

## 频控与错误处理

### 主动消息频控

| 维度 | 限制 |
| --- | --- |
| 企业认证 / 个人认证机器人 | 60 条 / 分钟 |
| 未认证机器人 | 30 条 / 分钟 |
| 单个目标群 | 20 条 / 分钟，1000 条 / 天 |

### 异常与重试行为

- **异常信息**：请求被 QQ 拒绝时抛出 `QApiException`，`Code` 为 QQ 业务错误码，`StatusCode` 为 HTTP 状态码。
- **被动回复失败**：适配器不吞掉异常，直接交由调用方处理。
- **无自动重试**：主动发送遇到超时或连接中断时**不会自动重试**（消息此时可能已被 QQ 服务端接收）。
- **多图部分送达**：发送多图若中途失败，先前成功的图片可能已送达群聊。

### 常见错误码

| QQ 错误码 | 说明与排查方向 |
| --- | --- |
| `40034100` | 主动发送触发频控，等待配额恢复 |
| `40034105` | 主动发送无权限，检查机器人权限及目标群通知开关 |
| `40034101`、`40054003` | 机器人不在目标群内 |
| `304103`、`40034005`、`40034128` | 被动回复已过期或超出回复次数上限 |