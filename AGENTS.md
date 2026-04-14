# MilkiBotFramework — Agent 指南

> 本文件面向 AI 编程助手，旨在帮助快速理解项目结构、开发流程与代码规范。项目主要使用中文进行注释和文档编写。

---

## 项目概览

**MilkiBotFramework** 是一个基于 **.NET 10** 开发的高性能、插件化聊天机器人框架。它支持通过统一抽象层对接多种即时通讯平台（如 OneBot/Go-CQHttp、QQ 官方 Bot、Discord），并内置 ASP.NET Core Web API 集成、无头图像渲染（Avalonia / WPF）、任务调度、EF Core 数据库管理等能力。

- **许可证**：GPL-3.0
- **主要语言**：C#（注释和文档以中文为主）
- **目标框架**：`net10.0`（WPF 成像模块为 `net10.0-windows`）
- **解决方案文件**：`src/MilkiBotFramework.sln`

---

## 技术栈与依赖

| 类别 | 关键技术 / 包 |
|------|--------------|
| 运行时 | .NET 10 |
| Web 框架 | ASP.NET Core (Kestrel) |
| ORM | Entity Framework Core (SQLite) |
| 日志 | Microsoft.Extensions.Logging.Console |
| DI | Microsoft.Extensions.DependencyInjection |
| 图像 | SixLabors.ImageSharp |
| UI 渲染 | Avalonia 11.3.11 / WPF |
| WebSocket | Fleck (服务端) / Websocket.Client (客户端) |
| 序列化 | System.Text.Json、YamlDotNet、Newtonsoft.Json (QQ 平台) |
| 测试 | xUnit、Microsoft.NET.Test.Sdk、coverlet.collector |
| 版本管理 | MinVer 7.0.0（基于 Git Tag 自动版本化） |
| 代码质量 | JetBrains ReSharper GlobalTools + NVika |

---

## 仓库结构

```
src/
├── MilkiBotFramework/                  # 核心框架（平台无关）
│   ├── Connecting/                     # 连接器抽象（WebSocket / HTTP / StdIO）
│   ├── ContactsManaging/               # 联系人管理
│   ├── Dispatching/                    # 消息分发器
│   ├── Event/                          # 内部事件总线
│   ├── Imaging/                        # 图像相关抽象
│   ├── Messaging/                      # 消息模型、富文本消息
│   ├── Plugining/                      # 插件系统（加载、生命周期、命令解析、配置、数据库）
│   ├── Services/                       # 框架级服务
│   ├── Tasking/                        # 定时任务调度
│   └── Utils/                          # 通用工具类
├── MilkiBotFramework.Aspnetcore/       # ASP.NET Core 集成（Bot + WebHost 合一）
├── MilkiBotFramework.Imaging.Avalonia/ # 基于 Avalonia 的无头渲染
├── MilkiBotFramework.Imaging.Wpf/      # 基于 WPF 的无头渲染（仅 Windows）
├── Platforms/                          # 各平台适配器
│   ├── MilkiBotFramework.Platforms.GoCqHttp/  # OneBot / Go-CQHttp
│   ├── MilkiBotFramework.Platforms.QQ/        # QQ 官方 Bot API
│   └── MilkiBotFramework.Platforms.Discord/   # Discord
├── Samples/                            # 示例项目
│   ├── DemoBot/                        # 可运行的示例 Bot
│   └── DemoPlugin/                     # 示例插件
├── Tests/                              # 测试项目
│   ├── UnitTests/                      # xUnit 单元测试
│   ├── AvaHeadlessTest/                # Avalonia 无头渲染测试
│   └── MinioTest/                      # MinIO 对象存储测试
└── Benchmarks/                         # BenchmarkDotNet 基准测试
```

---

## 构建与运行

所有构建命令均应在 `src/` 目录或解决方案根目录下执行：

```bash
# 还原依赖
dotnet restore ./src

# 构建全部项目
dotnet build ./src

# 运行单元测试
dotnet test ./src/Tests/UnitTests

# 运行示例 Bot
dotnet run --project ./src/Samples/DemoBot

# 还原本地工具（ReSharper CLI、NVika 等）
dotnet tool restore
```

---

## 代码风格规范

1. **语言版本**：C# 14，`Nullable` 与 `ImplicitUsings` 均启用。修改代码时请保持 `nullable` 上下文一致，避免产生新的可空警告。
2. **命名风格**：采用 PascalCase 用于类/方法/属性，camelCase 用于局部变量/参数，私有字段可使用 `_` 前缀。
3. **大括号**：遵循 K&R 风格（左大括号不换行），与现有代码保持一致。
4. **注释与文档**：新增公共 API 建议添加中文 XML 文档注释；内部逻辑使用中文行内注释。
5. **ReSharper**：项目使用 ReSharper 进行静态分析。CI 会执行 `jb inspectcode` 并将警告视为错误（`--treatwarningsaserrors`）。
   - 如需要局部禁用 ReSharper 检查，请使用 `// ReSharper disable All` 或对应的 suppression 注释。
   - 同理，禁用编译器警告请使用 `#pragma warning disable CSxxxx`。
6. **不要引入无关变更**：提交 PR 前避免包含大面积格式化或无意义的文件移动，以减轻 Code Review 负担。

---

## 测试策略

- **单元测试**：位于 `src/Tests/UnitTests/`，使用 **xUnit**。现有测试覆盖命令行解析、URL 编码等核心逻辑。
- **集成 / 专项测试**：
  - `AvaHeadlessTest`：验证 Avalonia 无头渲染流程。
  - `MinioTest`：验证 MinIO 文件上传相关功能。
- **运行要求**：
  - 单元测试无需外部依赖即可运行。
  - Avalonia / MinIO 测试可能需要特定运行时环境或外部服务。
- **CI 检查**：GitHub Actions 目前执行 `dotnet build` 和 ReSharper 代码检查，**不会自动运行测试**。请在本地确认测试通过后再提交。

---

## 架构核心概念

### 1. BotBuilder 模式
框架入口为 `BotBuilder`（或 `AspnetcoreBotBuilder`），采用 Fluent API 配置：

```csharp
var bot = new BotBuilder()
    .UseGoCqHttp(GoCqConnection.WebSocket("ws://127.0.0.1:6700"))
    .Build();
await bot.RunAsync();
```

`BotBuilderBase` 负责注册核心服务（`PluginManager`、`EventBus`、`BotTaskScheduler`、`ICommandLineAnalyzer` 等），并支持聚合多个 `IConnector`、`IDispatcher`、`IContactsManager`。

### 2. 插件系统
插件继承自 `PluginBase`，常用子类包括：
- `BasicPlugin` / `BasicPlugin<TContext>`：处理普通消息，支持命令自动绑定（`[CommandHandler]`）。
- `ServicePlugin`：提供全局服务逻辑（如消息发送前/后拦截）。

关键特性：
- 命令参数自动解析与模型绑定（支持 `[Argument]`、`[Option]`）。
- 插件可通过构造函数注入框架服务（`ILogger<T>`、`IRichMessageConverter`、`PluginManager` 等）。
- 插件支持异步会话（`Reply(..., out var nextMessage)` + `await nextMessage.GetNextMessageAsync(...)`）。
- 使用 `[PluginIdentifier]` 标识插件，使用 `[PluginLifetime]` 控制生命周期（`Singleton` / `Scoped` / `Transient`）。

### 3. 平台适配器
每个平台项目提供：
- `BotBuilderExtensions`：扩展方法（如 `UseGoCqHttp()`、`UseQQ()`）。
- 平台专属的 `IConnector`（WebSocket / HTTP / Reverse-WebSocket）。
- 平台专属的 `IDispatcher`、`IMessageApi`、`IContactsManager`。
- 平台专属的 `MessageContext` 子类。

### 4. 图像渲染
框架支持通过 XAML（Avalonia 或 WPF）进行无头渲染生成图片，无需 Chromium：
- `MilkiBotFramework.Imaging.Avalonia`：跨平台，推荐首选。
- `MilkiBotFramework.Imaging.Wpf`：仅 Windows，历史兼容。

### 5. ASP.NET Core 集成
`AspnetcoreBotBuilder` 将 Bot 生命周期与 ASP.NET Core `WebApplication` 合并，支持：
- 在插件中编写 MVC Controller。
- 通过 HTTP Middleware 或 Reverse WebSocket 接收平台回调。

---

## 开发注意事项

1. **DRY 违规已知问题**：项目根目录下存在 `DRY-VIOLATIONS.md`，记录了当前代码中较严重的重复逻辑（如 QQ 平台 `QApi` 内部重复、`PluginManager` 内局部函数重复等）。在进行相关模块重构前建议先阅读该文件，避免新增重复。
2. **死代码**：`src/Platforms/MilkiBotFramework.Platforms.QQ/QParameterConverter.cs` 目前未被使用，可直接删除（详见 DRY 报告）。
3. **MinVer 版本化**：框架类库（`MilkiBotFramework*` 前缀的项目）通过 `Directory.Build.targets` 自动引入 MinVer。发布 NuGet 包时版本由 Git Tag（前缀 `v`）决定。
4. **依赖升级**：仓库已启用 Dependabot（`.github/dependabot.yml`），每日检查 NuGet 依赖更新。

---

## CI / CD

- **工作流文件**：`.github/workflows/pr-build-check.yml`
- **触发条件**：向 `master` 分支发起 Pull Request
- **运行环境**：`windows-latest`
- **执行步骤**：
  1. 安装 .NET SDK（3.1.x / 6.0.x / 8.0.x，用于兼容工具链）
  2. `dotnet tool restore`
  3. `dotnet restore ./src`
  4. `dotnet build ./src --no-restore`
  5. `dotnet jb inspectcode ./src/MilkiBotFramework.sln --no-build ...`
  6. `dotnet nvika parsereport ... --treatwarningsaserrors`

> 任何导致 ReSharper 警告的代码都可能阻塞 PR 合并。

---

## 安全与合规

- 框架采用 **GPL-3.0** 许可证，引入新依赖时请确认许可证兼容性。
- `SixLabors.ImageSharp` 3.1.12 在 `MilkiBotFramework.csproj` 中被显式标记了 `<!-- ReSharper disable once VulnerablePackage -->`。升级该包时请评估安全公告。
- 处理用户输入的命令解析、消息反序列化逻辑时，请保持防御式编程，避免异常崩溃影响 Bot 稳定性。

---

## 快速参考：常用文件路径

| 用途 | 路径 |
|------|------|
| 解决方案 | `src/MilkiBotFramework.sln` |
| 全局构建属性 | `src/Directory.Build.props` |
| 全局构建目标 | `src/Directory.Build.targets` |
| 核心 Bot 构建器 | `src/MilkiBotFramework/BotBuilderBase.cs` |
| 插件基类 | `src/MilkiBotFramework/Plugining/PluginBase.cs` |
| 命令解析器 | `src/MilkiBotFramework/Plugining/CommandLine/` |
| ASP.NET 集成入口 | `src/MilkiBotFramework.Aspnetcore/AspnetcoreBotBuilder.cs` |
| CI 工作流 | `.github/workflows/pr-build-check.yml` |
| 本地开发工具 | `.config/dotnet-tools.json` |
| DRY 问题追踪 | `DRY-VIOLATIONS.md` |
