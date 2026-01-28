# MilkiBotFramework

[![NuGet Version](https://img.shields.io/nuget/v/MilkiBotFramework.svg?style=flat-square)](https://www.nuget.org/packages/MilkiBotFramework/)
[![Build Status](https://img.shields.io/github/actions/workflow/status/Milkitic/MilkiBotFramework/pr-build-check.yml?branch=main&style=flat-square)](https://github.com/Milkitic/MilkiBotFramework/actions)
[![License: GPLv3](https://img.shields.io/badge/License-GPLv3-blue.svg?style=flat-square)](https://opensource.org/license/gpl-3-0)

基于 .NET 10 的高性能机器人框架，内置 OneBot、官方机器人接口实现。

本项目提供了一个现代化、高效率的 .NET 机器人开发解决方案，以构建功能丰富的聊天机器人或进行快速原型开发。

**更多 Wiki 文档请前往：[https://deepwiki.com/Milkitic/MilkiBotFramework](https://deepwiki.com/Milkitic/MilkiBotFramework)**

> 本项目源自旧项目 [daylily v2](https://github.com/Milkitic/daylily/tree/archived/v2) 的彻底改造。其基本的系统逻辑不变，但提升了代码可维护度、工程合理度、拓展性、性能、稳定性，以及最重要的，将框架本身独立，并与 Bot 逻辑不相干。
> 现旧项目已基于本框架重构，可作为示例仓库，更多请见：[daylily 项目主页](https://github.com/Milkitic/daylily)

## ✨ 核心特性

*   **🔌 插件化架构：** 开发者只需关注插件逻辑，支持消息、事件、服务处理，同时也兼容传统的 `EventHandler` 方式。
*   **🚀 开箱即用：** 最少三行代码即可完成基础 Bot 的运行与部署。
*   **🌐 跨平台API设计：** 编写的通用插件可轻松迁移至不同平台 (例如：OneBot -> Discord)。
*   **🛠️ 统一管理：** 框架层面提供数据库 (EF Core, Migration目前仅支持ExecutingAssembly)、资源及配置的统一管理。
*   **💬 强大命令系统：** 自动解析用户输入，智能映射到插件方法的参数。
*   **🔧 高度可扩展：** 支持插件间与框架功能的依赖注入；核心中间件可继承修改，满足定制需求。
*   **🎨 内置图形绘制：** 集成基于 XAML 的 UI 框架级无头图形绘制功能，保持高效开发的同时，高效渲染并生成图片 **（无需原始绘图操作、无需Chromium）**。
*   **🌐 Web API集成：** 插件支持单点 ASP.NET Core Web API，可在插件中创建 Controller 并注入框架模块。
*   **🔔 完善的内部事件：** 关键模型支持属性更新通知，便于开发 GUI 插件或进行精细化控制。

## 🚀 快速开始

**前置条件：**
*   安装 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
*   准备一个 OneBot 实现（如 [NapCatQQ](https://github.com/NapNeko/NapCatQQ)），并配置为 WebSocket 连接，例如监听 `ws://127.0.0.1:6700`。

**步骤：**

1. 新建 .NET 控制台项目：
    ```pwsh
    dotnet new console --name MyBotApp
    cd MyBotApp
    ```

2. 添加 MilkiBotFramework NuGet 包：
    ```pwsh
    dotnet add package MilkiBotFramework
    ```
    
3. 在 `Program.cs` 文件中编写如下代码：

    ```cs
    using MilkiBotFramework;
    using MilkiBotFramework.Messaging;
    using MilkiBotFramework.Platforms.GoCqHttp;
    using MilkiBotFramework.Plugining;
    using MilkiBotFramework.Plugining.Attributes;

    // 启动 Bot
    return await new BotBuilder()
          .UseGoCqHttp(GoCqConnection.WebSocket("ws://127.0.0.1:6700")) // 配置连接 OneApi
          .Build()
          .RunAsync();

    // 定义一个简单的插件
    [PluginIdentifier(guid: "e4c18c40-afe0-447b-b7eb-b84f842520b4", name: "HelloWorld Demo")]
    public class HelloWorld : BasicPlugin
    {
        [CommandHandler] // 响应 /echo 命令
        public IResponse Echo([Argument] string content) => Reply(content); // 回复接收到的内容
    }
    ```

4. 运行项目
    ```pwsh
    dotnet run
    ```

5.  验证：
    当控制台输出类似以下日志时，表示 Bot 已成功初始化：
    ```
    11:06:13.92+08 info: MilkiBotFramework.Platforms.GoCqHttp.Connecting.GoCqClient[0]
          Connected to websocket server.
    ......
    11:06:14.26+08 info: MilkiBotFramework.Plugining.PluginManager[0]
          Add plugin "HelloWorld Demo" (Scoped BasicPlugin)
    11:06:14.28+08 info: MilkiBotFramework.Plugining.PluginManager[0]
          Plugin initialization done in 0.360s!
    ```
    此时，向 Bot 私聊发送 `/echo helloworld`，若 Bot 回复 `helloworld`，则表示 Hello World 功能已成功实现。

## 🗺️ 路线图 (Roadmap)

框架本身设计文档待完善

**TODO:**
- [ ] 新增命令选择器的重写支持，为将来采用LLM function calling作尽早支持。
- [ ] 支持单实例多Platform，以进行多平台互联。
 
**WIP:**
- [ ] 内置QQ官方API

**FINISHED:**
- [x] 基本框架功能
- [x] 命令解析，自动识别负数，自动合并多个argument
- [x] 简易session实现 (Reply并out一个IAsyncMessage)
- [x] 事件插件
- [x] 在尽可能保持轻量的前提下，实现基于asp.net core+kestrel的统一单点webserver，支持自定义添加对应的Contoller。
- [x] 将现有static HttpHelper纳入生命周期管理内
- [x] 为了支持插件的管理，需要在context内注入相关信息
- [x] 插件应实现对于即将发送的消息过滤与控制（允许强制发送）
- [x] 联系人相关功能完善（包括go-cq实现）
- [x] 插件命令的权限(考虑其他插件要正确读取声明的权限)
- [x] 插件的配置设置、数据库读写、资源分配的管理
- [x] 完善内部事件
- [x] 支持reverse-ws
- [x] 部分绘制图像共享功能 (包含Maui/WPF绘制框架)
- [x] 将WPF相关功能迁移至Avalonia

详细开发计划和讨论，请关注项目的 [GitHub Issues](https://github.com/Milkitic/MilkiBotFramework/issues) 和 [Wiki](https://deepwiki.com/Milkitic/MilkiBotFramework)。

## 🤝 贡献

本项目欢迎社区的贡献，尤其在各种平台适配方面，社区的参与将极大地提升开发效率。

在提交 Pull Request 前，请注意以下基本要求：
1.  **编译通过：** 提交的代码必须能够成功编译。
2.  **代码清晰度：** 请确保代码本身足够清晰，遵循项目现有的代码样式（如变量命名原则、大括号换行等），并使用代码格式化工具进行整理。
3.  **Git提交记录：** 尽可能使 Git 提交记录清晰、简洁，避免包含无关内容的大面积更改，以便于代码审查。
4.  **重大更新的讨论：** 如果计划进行较大的更新或重构，请事先通过 [Discussions](https://github.com/Milkitic/MilkiBotFramework/discussions) 或 [Issue](https://github.com/Milkitic/MilkiBotFramework/issues) 进行讨论，以避免不必要的重复工作。

## 📦 第三方库

本项目依赖以下优秀的开源库：

### 社区
* [dnlib](https://github.com/0xd4d/dnlib) (MIT) - .NET 程序集分析
* [Avalonia](https://github.com/AvaloniaUI/Avalonia) (MIT) - 强大的跨平台UI库
* [Fleck](https://github.com/statianzo/Fleck) (MIT) - WebSocket服务器
* [SixLabors.ImageSharp](https://github.com/SixLabors/ImageSharp) (Apache-2.0) - 托管的2D绘图库
* [Websocket.Client](https://github.com/Marfusios/websocket-client) (MIT) - WebSocket客户端
* [YamlDotNet](https://github.com/aaubry/YamlDotNet) (MIT) - YAML库
* 
### 官方
* [Microsoft.EntityFrameworkCore](https://github.com/dotnet/efcore) (MIT) 现代化ORM
* [Microsoft.AspNetCore](https://github.com/dotnet/aspnetcore) (MIT) 现代化跨平台Web框架
* Microsoft.Extensions.DependencyInjection (MIT) DI框架及实现
* Microsoft.Extensions.Logging (MIT) 日志框架
* wpf (MIT) Windows窗体UI框架


## 📄 许可证 (License)
本项目采用 [GPL-3.0 License](https://opensource.org/license/gpl-3-0) 开源。
