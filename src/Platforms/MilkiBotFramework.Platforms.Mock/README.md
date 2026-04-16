# Mock Platform 配置文档

这是 Mock Platform 的配置参考。将这个文件放在应用运行目录下，命名为 `appsettings.yaml` 以覆盖默认配置。

## 可配置项

### Config（Mock 平台配置）

```yaml
Config:
  # 虚拟群组ID（用于标识群聊）
  GroupId: "mock_group_001"
  
  # 虚拟群组名称（显示名称）
  GroupName: "Mock Test Group"
  
  # 虚拟用户ID（测试用户的ID）
  UserId: "mock_user_001"
  
  # 虚拟用户名（测试用户的显示名称）
  UserName: "Mock User"
  
  # Bot 自身的ID
  BotUserId: "mock_bot_001"
  
  # Bot 的显示名称
  BotUserName: "Mock Bot"
```

### RootAccounts（根管理员账户）

```yaml
RootAccounts:
  - "mock_user_001"  # 将虚拟用户设为根管理员
```

## 使用示例

### 场景1：单用户群聊测试

```yaml
Config:
  GroupId: test_group_1
  GroupName: Test Group
  UserId: test_user
  UserName: Test User
  BotUserId: test_bot
  BotUserName: Test Bot

RootAccounts:
  - test_user
```

### 场景2：多角色模拟

虽然当前默认只支持单用户，但可以通过以下方式扩展：

1. 修改 `MockContactsManager` 添加更多虚拟成员
2. 修改 `MockConnector.SimulateReceiveMessageAsync()` 支持不同的发送者ID
3. 在 UI 中添加用户选择器，允许模拟不同用户发送消息

## 配置文件位置

应将配置文件放在以下位置之一：

- 应用运行目录：`./appsettings.yaml`
- 框架默认搜索路径

如果未找到配置文件，将使用 `MockBotOptions` 中的默认值。

## 高级配置

### 自定义消息转换器

如需自定义富文本消息处理，编辑 `Messaging/MockMessageConverter.cs`。

### 自定义分发逻辑

如需自定义消息分发规则，编辑 `Dispatching/MockDispatcher.cs` 的 `TryPopulateMessageContext()` 方法。

### 扩展联系人管理

如需支持多用户或动态成员，修改 `ContactsManaging/MockContactsManager.cs` 的 `InitializeMockData()` 方法。
