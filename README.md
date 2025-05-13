# MilkiBotFramework
易于使用与快速开发，基于.net 8的高性能机器人框架（内置OneBot实现）

更多wiki请前往：https://deepwiki.com/Milkitic/MilkiBotFramework

本项目源自旧项目 https://github.com/Milkitic/daylily/tree/archived/v2 的彻底改造。

其基本的系统逻辑不变，但是提升了代码可维护度、工程合理度、拓展性、性能、稳定性，以及最重要的，将框架本身独立，并与Bot逻辑不相干。
> 现旧项目已基于本框架重构，可作为示例仓库，更多请见[项目主页](https://github.com/Milkitic/daylily)

## 特色
1. 插件式开发。对于Bot编写者，只需完成插件类的开发。包含消息、事件、服务处理。（当然，也支持比较原始的`EventHandler`方式）
2. 开箱即用。在不进行自定义设置的情况下，支持三行代码完成运行部署。
3. 支持多平台统一API。这代表着若你编写插件是通用的（例如：不包括特定的CQ码如戳一戳），你可以将Bot功能快速转移至其他平台（例如：OneBot -> Discord）
3. 框架层面的数据库管理(目前Migration仅支持ExecutingAssembly)、资源管理以及配置管理，插件之间统一。
4. 完善的命令支持。将用户输入自动转换为对应插件方法的参数。
5. 编写插件时，支持插件之间与框架本身功能的依赖注入，插件开发有着很高自由度。
6. 若内置的一些逻辑无法满足你的需求，你可以自己进行部分中间件的继承与修改。
7. 内置UI框架级的图形绘制功能。无需用户进行原始的绘图操作，支持Xaml设计器。
8. 插件支持使用单点的ASP.NET Core Web框架（默认支持WebApi）。这意味着在你的程序集内，新增对应的ControllerBase类即可。同时支持在你的Controller内依赖注入MilkiBotFramework的各种单点模块（不支持插件的注入，因为两者的Scope不一致）。
9. 内部事件完善，且关键模型本身支持属性更新通知。这代表着你将更容易制作GUI相关插件。

## 示例

> 前置条件：
> 将go-cqhttp配置为websocket连接，地址设置为`ws://127.0.0.1:6700`，运行并确认正常工作。

新建dotnet项目

`dotnet new console --name MyBotApp`

在Program.cs内编写:

```cs
using MilkiBotFramework;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Platforms.GoCqHttp;
using MilkiBotFramework.Plugining;
using MilkiBotFramework.Plugining.Attributes;

return await new BotBuilder()
      .UseGoCqHttp(GoCqConnection.WebSocket("ws://127.0.0.1:6700"))
      .Build()
      .RunAsync();

[PluginIdentifier(guid: "e4c18c40-afe0-447b-b7eb-b84f842520b4", name: "HelloWorld Demo")]
public class HelloWorld : BasicPlugin
{
    [CommandHandler]
    public IResponse Echo([Argument] string content) => Reply(content);
}
```

运行项目
`dotnet run`

控制台相关输出
```
11:06:13.92+08 info: MilkiBotFramework.Platforms.GoCqHttp.Connecting.GoCqClient[0]
      Connected to websocket server.
......
11:06:14.26+08 info: MilkiBotFramework.Plugining.PluginManager[0]
      Add plugin "HelloWorld Demo" (Scoped BasicPlugin)
11:06:14.28+08 info: MilkiBotFramework.Plugining.PluginManager[0]
      Plugin initialization done in 0.360s!
```
以上输出代表着bot已初始化完成。
下面私聊bot账号，输入`/echo helloworld`，若bot成功回复了`helloworld`，代表着你已成功完成了你的Hello World功能！

## 贡献

本项目十分欢迎contribute~因为对于各种平台适配需要大家的一起帮助才能更加的高效。

但是有以下几个很基本的点，在提交PR前请注意：
1. 编译通过
2. 代码本身足够的清晰，请使用现有的代码样式（变量命名基础原则、括号换行等），使用代码格式化工具完成过清理
3. 尽可能的使git的提交记录足够清晰，避免无法区分更新内容的大面积更改
4. 如果是较大的更新，请事先进行discussion，避免低效地重做等

框架本身设计文档待完善

## TODO: 
- [ ] 支持多前端输入，并将结果返回到对应前端中。
- [ ] 将WPF相关功能迁移至Avalonia
- [ ] 内置QQ官方API
## FINISHED: 
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
## NOT DETERMINED: 
- [ ] 支持基于Github issue的更新
## TBD:
**是否支持同时多OneBot实例亦或是其他的客户端，以进行多平台互联？**
目前该框架设计没有考虑这一点，考虑到其实用性，大概率会以大版本号更新实现。

**类似nonebot内置自然语言处理器？**
我认为没有必要内置，因为此类功能放入通用框架并不是足够的成熟稳定（存在各种概率因素）。可以继承ICommandLineAnalyzer，编写自己的命令分析器，将自然语言映射成对应命令。

## 第三方库
### 社区
* [dnlib](https://github.com/0xd4d/dnlib) (MIT) .NET 程序集分析
* [Fleck](https://github.com/statianzo/Fleck) (MIT) WebSocket服务器
* [SixLabors.ImageSharp](https://github.com/SixLabors/ImageSharp) (Apache-2.0) 托管的2D绘图库
* [Websocket.Client](https://github.com/Marfusios/websocket-client) (MIT) WebSocket客户端
* [YamlDotNet](https://github.com/aaubry/YamlDotNet) (MIT) YAML库
### 官方
* [Microsoft.EntityFrameworkCore](https://github.com/dotnet/efcore) (MIT) 现代化ORM
* [Microsoft.AspNetCore](https://github.com/dotnet/aspnetcore) (MIT) 现代化跨平台Web框架
* Microsoft.Extensions.DependencyInjection (MIT) DI框架及实现
* Microsoft.Extensions.Logging (MIT) 日志框架
* wpf (MIT) Windows窗体UI框架
