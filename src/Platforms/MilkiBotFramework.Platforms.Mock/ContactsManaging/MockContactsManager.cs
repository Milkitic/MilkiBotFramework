using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.ContactsManaging;
using MilkiBotFramework.ContactsManaging.Models;
using MilkiBotFramework.ContactsManaging.Results;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Tasking;

namespace MilkiBotFramework.Platforms.Mock.ContactsManaging;

/// <summary>
///     Mock 平台联系人管理器 - 管理虚拟的群组和私聊信息
/// </summary>
public class MockContactsManager : ContactsManagerBase
{
    private readonly MockBotOptions _options;

    public MockContactsManager(
        BotTaskScheduler botTaskScheduler,
        ILogger<MockContactsManager> logger,
        BotOptions botOptions)
        : base(botTaskScheduler, logger)
    {
        if (botOptions is not MockBotOptions mockOptions)
        {
            throw new ArgumentException("Options must be of type MockBotOptions", nameof(botOptions));
        }

        _options = mockOptions;

        // 初始化虚拟数据
        InitializeMockData();
    }

    protected override bool GetContactsUpdateInfo(
        MessageContext messageContext,
        out ContactsUpdateInfo? updateInfo)
    {
        // Mock 平台不处理联系人更新事件
        updateInfo = null;
        return false;
    }

    protected override bool GetContactsCore(
        out Dictionary<string, ChannelInfo> channels,
        out Dictionary<string, ChannelInfo> subChannels,
        out Dictionary<string, PrivateInfo> privates)
    {
        channels = ChannelMapping.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        subChannels = new();
        privates = PrivateMapping.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        return true;
    }

    private void InitializeMockData()
    {
        var config = _options.Config;

        // 创建虚拟 Self 信息
        SelfInfo = new SelfInfo
        {
            UserId = config.BotUserId,
            Nickname = config.BotUserName
        };

        // 创建虚拟群组信息
        var groupMembers = new ConcurrentDictionary<string, MemberInfo>();
        groupMembers.TryAdd(config.UserId, new MemberInfo(config.GroupId, config.UserId, null)
        {
            Nickname = config.UserName,
            MemberRole = MemberRole.Member
        });
        groupMembers.TryAdd(config.BotUserId, new MemberInfo(config.GroupId, config.BotUserId, null)
        {
            Nickname = config.BotUserName,
            MemberRole = MemberRole.Admin
        });

        var groupInfo = new ChannelInfo(config.GroupId, groupMembers.Values)
        {
            Name = config.GroupName
        };

        ChannelMapping.TryAdd(config.GroupId, groupInfo);

        // 创建虚拟私聊信息
        var privateInfo = new PrivateInfo(config.UserId)
        {
            Nickname = config.UserName
        };

        PrivateMapping.TryAdd(config.UserId, privateInfo);
    }
}