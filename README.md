# MilkiBotFramework
易于使用与快速开发，基于.net 6的高性能机器人框架（内置OneBot协议）

TODO: 
- [x] 基本框架功能
- [ ] 命令解析，自动识别负数，自动合并多个argument
- [ ] 简易session实现 (Reply并out一个IAsyncMessage)
- [ ] 事件插件
- [ ] 插件对应AssemblyLoaderConext的Unload，并且支持基于Github issue的热更新
- [x] Partially, 在尽可能保持轻量的前提下，实现基于asp.net core+kestrel的统一单点webserver，支持自定义添加对应的Contoller。
- [x] 将现有static HttpHelper纳入生命周期管理内
- [ ] 插件的message identity管理，启用与禁用
- [ ] 插件命令的权限(考虑其他插件要正确读取声明的权限)
- [ ] 联系人相关功能完善（包括go-cq实现）
- [ ] 插件的配置设置、数据库读写、资源分配的管理
- [x] 插件应实现对于即将发送的消息过滤与控制（允许强制发送）
- [ ] 部分绘制图像共享功能 (包含Maui/WPF绘制框架)
