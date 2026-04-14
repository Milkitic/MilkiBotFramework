# DRY 违规分析报告

> 分析日期：2026-03-02  
> 项目：MilkiBotFramework

---

## 严重级 (Critical)

### 1. QApi.cs — `SendPrivateMessageAsync` 与 `SendChannelMessageAsync` 大规模内部重复

- **文件：** `src/Platforms/MilkiBotFramework.Platforms.QQ/Connecting/QApi.cs`
- **影响行数：** ~100 行
- **描述：** 两个方法的消息构造、图片上传、请求拼装逻辑几乎逐行相同，仅 3 处参数差异。

| 差异点 | `SendPrivateMessageAsync` | `SendChannelMessageAsync` |
|---|---|---|
| URL 路径 | `v2/users/{userId}/messages` | `v2/groups/{channelId}/messages` |
| 上传 URL | `v2/users/{userId}/files` | `v2/groups/{channelId}/files` |
| event_id | `"C2C_MSG_RECEIVE"` | `"GROUP_MSG_RECEIVE"` |

- **建议：** 提取 `SendMessageCoreAsync(string entityType, string entityId, string eventId, ...)` 私有方法，通过参数区分 `users`/`groups`。

---

### 2. QApiConnector 与 QApiWsConnector — 大量共享成员完全重复

- **文件：**
  - `src/Platforms/MilkiBotFramework.Platforms.QQ/Connecting/QApiConnector.cs`
  - `src/Platforms/MilkiBotFramework.Platforms.QQ/Connecting/QApiWsConnector.cs`
- **影响行数：** ~50 行
- **描述：** 以下成员在两个类中完全重复定义：

| 重复成员 | 行数 |
|---|---|
| `RequestAccessTokenAsync` 方法 | ~16 行逐字相同 |
| `ValidateResult` 静态方法 | ~8 行逐字相同 |
| `ProductHost` / `SandboxHost` 常量 | 2 行 |
| `_tokenExpireTime` / `_accessToken` / `_lastSequence` 字段 | 3 行 |
| `Host` 属性（沙盒/正式环境判断逻辑） | ~6 行逐字相同 |
| `MessageSequence` 属性 | 1 行 |

- **建议：** 提取 `QApiTokenHelper` 共享类或引入公共基类型，集中管理 Token 获取与验证逻辑。

---

### 3. GoCqParameterConverter 与 QParameterConverter — 100% 复制

- **文件：**
  - `src/Platforms/MilkiBotFramework.Platforms.GoCqHttp/GoCqParameterConverter.cs`
  - `src/Platforms/MilkiBotFramework.Platforms.QQ/QParameterConverter.cs`
- **影响行数：** 22 行（整个文件）
- **描述：** 两个文件除类名和命名空间外完全一致。更关键的是，`QParameterConverter` 在 QQ 平台的 `BotBuilderExtensions.cs` 中根本未被使用（实际注册的是 `DefaultParameterConverter`），**属于死代码**。
- **建议：** 删除 `QParameterConverter`；若两个平台确实都需要，则移入核心库并统一命名为 `LinkImageParameterConverter`。

---

### 4. PluginManager.cs — `SendAndCheckResponse` 同文件内重复两次

- **文件：** `src/MilkiBotFramework/Plugining/PluginManager.cs`
- **位置：**
  - `HandleNoticeMessage` 方法内部（~L92）
  - `HandleTextMessage` 方法内部（~L289）
- **影响行数：** ~30 行 × 2
- **描述：** 两处定义为 local function，包含完全相同的 `BeforeSend`/`AfterSend`/`Dispose` 框架逻辑，差异仅 3 行（`AsyncMessage` 处理）。

```csharp
// 两处重复的核心逻辑（~30行）:
async Task SendAndCheckResponse(PluginInfo pluginInfo, IResponse? response)
{
    if (response == null) return;
    if (response is MessageResponse mr) mr.MessageContext = messageContext;
    try
    {
        foreach (var serviceExecutionInfo in serviceExecutionInfos)
        {
            var servicePlugin = (ServicePlugin)serviceExecutionInfo.PluginInstance;
            var result = await servicePlugin.BeforeSend(pluginInfo, response);
            if (!result) { response.IsHandled = true; handled = response.IsHandled; return; }
        }
        handled = response.IsHandled;
        if (response.Message == null) return;
        await AutoReply(messageContext, response);
        foreach (var serviceExecutionInfo in serviceExecutionInfos)
        {
            var servicePlugin = (ServicePlugin)serviceExecutionInfo.PluginInstance;
            await servicePlugin.AfterSend(pluginInfo, response);
        }
        // ↓ 仅 HandleTextMessage 版本有这 3 行
        // if (!handled && response.AsyncMessage is AsyncMessage asyncMessage)
        //     _asyncMessageDict.AddOrUpdate(...);
    }
    finally { /* dispose response.Message */ }
}
```

- **建议：** 提取为私有实例方法，增加 `bool handleAsyncMessage` 参数控制差异分支。

---

## 中等级 (Medium)

### 5. Avalonia vs WPF `ProcessGifAsync` — 跨项目结构重复

- **文件：**
  - `src/MilkiBotFramework.Imaging.Avalonia/AvaRenderingProcessor.cs` (L147-199)
  - `src/MilkiBotFramework.Imaging.Wpf/WpfDrawingProcessor.cs` (L76-131)
- **影响行数：** ~40 行
- **描述：** 处理流程逐行对应：`EnsureUiThreadAsync` → CTS/TCS → UI Dispatcher 调度 → 窗口创建 → 渲染回调 → GIF 合成 → 资源清理。仅 Dispatcher 调用方式（`Dispatcher.UIThread.InvokeAsync` vs `Application.Current.Dispatcher.InvokeAsync`）和窗口类型（`DrawingWindow` vs `HiddenWindow`）不同。
- **建议：** 引入 `DrawingProcessorBase<TControl>` 抽象基类，用模板方法模式注入框架特定差异：
  - `abstract Task InvokeOnUiThread(Func<Task> action)`
  - `abstract IWindow CreateWindow(Control child)`

---

### 6. GoCqClient / GoCqServer / GoCqKestrelConnector — IGoCqConnector 委托转发重复

- **文件：**
  - `src/Platforms/MilkiBotFramework.Platforms.GoCqHttp/Connecting/GoCqClient.cs`
  - `src/Platforms/MilkiBotFramework.Platforms.GoCqHttp/Connecting/GoCqServer.cs`
  - `src/Platforms/MilkiBotFramework.Platforms.GoCqHttp/Connecting/GoCqKestrelConnector.cs`
- **描述：** 三个类的 `SendMessageAsync` 和 `TryGetStateByMessage` 均为转发到 `GoCqWebSocketHelper` 的相同代码。受限于 C# 无多继承，无法直接共享实现。
- **建议：** 用组合模式，包含一个 `GoCqConnectorCore` 成员来消除转发样板。

---

### 7. Avalonia / WPF `UiThreadHelper.cs` — 骨架结构重复

- **文件：**
  - `src/MilkiBotFramework.Imaging.Avalonia/Internal/UiThreadHelper.cs`
  - `src/MilkiBotFramework.Imaging.Wpf/Internal/UiThreadHelper.cs`
- **影响行数：** ~20 行骨架
- **描述：** 字段声明（`_uiThread`, `AsyncLock`, `WaitComplete`）、`EnsureUiThreadAsync` 方法骨架（lock → alive check → 创建 STA 线程 → 启动 → await）完全相同，差异仅在线程内部的应用初始化代码（Avalonia 用 `AppBuilder`，WPF 用 `new Application()`）。
- **建议：** 提取共享抽象基类，将 `InitializeApplication()` 作为抽象方法。

---

### 8. PluginManager.cs `AutoReply` — Private/Channel 分发逻辑重复

- **文件：** `src/MilkiBotFramework/Plugining/PluginManager.cs` (L340-393)
- **描述：** `response.Id == null` 和有值两个分支内都执行同样的 `MessageType.Private` → `SendPrivateMessageAsync` / `SendChannelMessageAsync` 分派逻辑。
- **建议：** 提取 `SendByMessageType(identity, message, ...)` 辅助方法。

---

### 9. GoCqMessageContext 与 QMessageContext — 高度相似

- **文件：**
  - `src/Platforms/MilkiBotFramework.Platforms.GoCqHttp/Messaging/GoCqMessageContext.cs`
  - `src/Platforms/MilkiBotFramework.Platforms.QQ/Messaging/QMessageContext.cs`
- **描述：** 两者都持有 `JsonDocument RawJsonDocument` + `RawMessage` 属性，都仅有构造函数传 `IRichMessageConverter`。
- **建议：** 提取 `JsonBasedMessageContext` 中间基类。

---

## 低级 (Low)

### 10. 三平台 BotBuilderExtensions 注册模式重复

- **文件：**
  - `src/Platforms/MilkiBotFramework.Platforms.Discord/BotBuilderExtensions.cs`
  - `src/Platforms/MilkiBotFramework.Platforms.GoCqHttp/BotBuilderExtensions.cs`
  - `src/Platforms/MilkiBotFramework.Platforms.QQ/BotBuilderExtensions.cs`
- **影响行数：** ~10 行 × 3
- **描述：** `UseXxx` 调用链结构完全相同（`ConfigureServices` → `UseCommandLineAnalyzer` → `UseContactsManager` → `UseDispatcher` → `UseMessageApi` → `UseOptions` → `UseRichMessageConverter`）。
- **建议：** 可考虑提供 `UsePlatform<TContext, TContacts, TDispatcher, TApi, TOptions, TConverter>(...)` 泛型方法。

### 11. 三平台 Dispatcher 构造函数完全相同

- **描述：** 三个平台的 Dispatcher 构造函数参数签名和 `: base(...)` 调用逐字一致。这是继承模式下的正常样板。
- **建议：** 可考虑 `[ActivatorUtilitiesConstructor]` 或默认参数减轻。

### 12. Avalonia / WPF `Delegates.cs` — 1 行委托定义逐字相同

- **文件：**
  - `src/MilkiBotFramework.Imaging.Avalonia/Internal/Delegates.cs`
  - `src/MilkiBotFramework.Imaging.Wpf/Internal/Delegates.cs`
- **建议：** 零成本移入共享 Imaging 项目。

### 13. Avalonia `DrawingWindow` / WPF `HiddenWindow` 的 `WaitForShown()` 逐字相同

- **影响行数：** 6 行
- **描述：** TCS 模式等待窗口首次渲染完成的逻辑完全相同。

### 14. GoCqApiException 与 QApiException — 结构相同的异常类

- **文件：**
  - `src/Platforms/MilkiBotFramework.Platforms.GoCqHttp/Connecting/GoCqApiException.cs`
  - `src/Platforms/MilkiBotFramework.Platforms.QQ/Connecting/QApiException.cs`
- **建议：** 在核心库提供 `PlatformApiException` 基类。

### 15. RichMessage.cs — `Dispose`/`DisposeAsync` 遍历逻辑内部重复

- **文件：** `src/MilkiBotFramework/Messaging/RichMessages/RichMessage.cs` (L66-82)
- **描述：** 同步与异步 dispose 方法遍历逻辑近乎相同。这是 C# Dispose 模式的常见现象。

---

## 附：复制粘贴 Bug

- **文件：** `src/Platforms/MilkiBotFramework.Platforms.QQ/Connecting/QApi.cs` (L30)
- **问题：** 错误消息写的是 `"Except for IGoCqConnector"`，这是从 `GoCqApi.cs` 复制时遗留的，应改为 QQ 平台对应的类型名。

---

## 影响统计

| 严重度 | 问题数 | 预估可消除重复行数 |
|---|---|---|
| Critical | 4 | ~250 行 |
| Medium | 5 | ~150 行 |
| Low | 6 | ~50 行 |
| **合计** | **15** | **~450 行** |

## 推荐修复优先级

1. **#1 QApi 内部重复** — 同文件重构，风险最低，收益最大
2. **#4 PluginManager 内部重复** — 同文件重构，风险低
3. **#3 删除死代码 QParameterConverter** — 零风险
4. **#2 QApiConnector Token 逻辑提取** — 同平台内重构
5. **#5 Imaging Processor 模板方法** — 跨项目重构，需仔细设计接口
