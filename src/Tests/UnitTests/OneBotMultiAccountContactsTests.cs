using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MilkiBotFramework.ContactsManaging.Models;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Platforms;
using MilkiBotFramework.Platforms.OneBot.Connecting;
using MilkiBotFramework.Platforms.OneBot.Connecting.ResponseModel;
using MilkiBotFramework.Platforms.OneBot.Connecting.ResponseModel.Guild;
using MilkiBotFramework.Platforms.OneBot.ContactsManaging;
using MilkiBotFramework.Platforms.OneBot.Messaging;
using MilkiBotFramework.Tasking;
using Xunit;

namespace UnitTests;

public class OneBotMultiAccountContactsTests
{
    [Fact]
    public async Task TryGetOrAddPrivateInfo_KeepsSeparateCachesPerSelfId()
    {
        await using var harness = CreateHarness((selfId, action, parameters) => action switch
        {
            "get_stranger_info" => new StrangerInfo
            {
                Nickname = selfId == "bot-a" ? "Alice" : "Bob"
            },
            _ => throw new InvalidOperationException($"Unexpected action: {action}")
        });

        var contextA = CreateContext("bot-a");
        var contextB = CreateContext("bot-b");

        var resultA = await harness.Manager.TryGetOrAddPrivateInfo(contextA, "10001");
        var resultB = await harness.Manager.TryGetOrAddPrivateInfo(contextB, "10001");

        Assert.True(resultA.IsSuccess);
        Assert.True(resultB.IsSuccess);
        Assert.Equal("Alice", resultA.PrivateInfo!.Nickname);
        Assert.Equal("Bob", resultB.PrivateInfo!.Nickname);
        Assert.Equal(2, harness.Manager.GetAllPrivates().Count(info => info.UserId == "10001"));
    }

    [Fact]
    public async Task TryGetOrAddGuildEntities_UsesSelfIdScopedLookup()
    {
        await using var harness = CreateHarness((selfId, action, parameters) => action switch
        {
            "get_guild_meta_by_guest" => new GuildInfo
            {
                GuildName = "Guild Alpha"
            },
            "get_guild_channel_list" => new List<SubChannelInfo>
            {
                new()
                {
                    ChannelId = 6001,
                    ChannelName = "general",
                    ChannelType = ChannelType.Text
                }
            },
            "get_guild_members" => new GetGuildMembersResponse
            {
                Members =
                [
                    new GuildMember
                    {
                        TinyId = 7001,
                        Nickname = "Guild User",
                        Title = "member"
                    }
                ]
            },
            _ => throw new InvalidOperationException($"Unexpected action: {action}")
        });

        var context = CreateContext("bot-a");

        var channelResult = await harness.Manager.TryGetOrAddChannelInfo(context, "5001", "6001");
        var memberResult = await harness.Manager.TryGetOrAddMemberInfo(context, "5001", "7001", "6001");

        Assert.True(channelResult.IsSuccess);
        Assert.True(memberResult.IsSuccess);
        Assert.Equal("general", channelResult.ChannelInfo!.Name);
        Assert.Equal("Guild User", memberResult.MemberInfo!.Nickname);
        Assert.Equal("member", memberResult.MemberInfo.Card);
    }

    [Fact]
    public async Task HandleMessageAsync_AppliesNoticeUpdatesPerAccount()
    {
        await using var harness = CreateHarness((selfId, action, parameters) => action switch
        {
            "get_group_info" => new GroupInfo
            {
                GroupId = Convert.ToInt64(parameters!["group_id"]).ToString(),
                GroupName = selfId == "bot-a" ? "Group A" : "Group B"
            },
            "get_group_member_info" => new GroupMember
            {
                GroupId = Convert.ToInt64(parameters!["group_id"]).ToString(),
                UserId = Convert.ToInt64(parameters["user_id"]).ToString(),
                Nickname = selfId == "bot-a" ? "Alpha" : "Beta",
                Card = selfId == "bot-a" ? "CardA" : "CardB",
                Role = "member"
            },
            _ => throw new InvalidOperationException($"Unexpected action: {action}")
        });

        var contextA = CreateContext("bot-a");
        var contextB = CreateContext("bot-b");
        await harness.Manager.TryGetOrAddChannelInfo(contextA, "1001");
        await harness.Manager.TryGetOrAddChannelInfo(contextB, "1001");
        await harness.Manager.TryGetOrAddMemberInfo(contextA, "1001", "2001");
        await harness.Manager.TryGetOrAddMemberInfo(contextB, "1001", "2001");

        var adminNotice = CreateNoticeContext("bot-a",
            """
            {"post_type":"notice","notice_type":"group_admin","sub_type":"set","group_id":1001,"user_id":2001,"self_id":"bot-a","time":1710000000}
            """);
        await harness.Manager.HandleMessageAsync(adminNotice);

        var memberA = await harness.Manager.TryGetOrAddMemberInfo(contextA, "1001", "2001");
        var memberB = await harness.Manager.TryGetOrAddMemberInfo(contextB, "1001", "2001");
        Assert.Equal(MemberRole.Admin, memberA.MemberInfo!.MemberRole);
        Assert.Equal(MemberRole.Member, memberB.MemberInfo!.MemberRole);

        Assert.Equal(2, harness.Manager.GetAllChannels().Count(channel => channel.ChannelId == "1001"));

        var kickMeNotice = CreateNoticeContext("bot-a",
            """
            {"post_type":"notice","notice_type":"group_decrease","sub_type":"kick_me","group_id":1001,"user_id":"bot-a","self_id":"bot-a","time":1710000001}
            """);
        await harness.Manager.HandleMessageAsync(kickMeNotice);

        Assert.Equal(1, harness.Manager.GetAllChannels().Count(channel => channel.ChannelId == "1001"));
    }

    [Fact]
    public async Task InitializeTasks_RefreshesKnownAccountSnapshots()
    {
        await using var harness = CreateHarness((selfId, action, parameters) => action switch
        {
            "get_login_info" => new LoginInfo
            {
                UserId = 9001,
                Nickname = "RefreshBot"
            },
            "get_friend_list" => new List<FriendInfo>
            {
                new()
                {
                    UserId = "3001",
                    Nickname = "Friend",
                    Remark = "BestFriend"
                }
            },
            "get_group_list" => new List<GroupInfo>
            {
                new()
                {
                    GroupId = "4001",
                    GroupName = "Refresh Group"
                }
            },
            "get_group_member_list" => new List<GroupMember>
            {
                new()
                {
                    GroupId = "4001",
                    UserId = "5001",
                    Nickname = "Member",
                    Card = "M",
                    Role = "member"
                }
            },
            "get_guild_list" => new List<GuildBrief>
            {
                new()
                {
                    GuildId = 6001,
                    GuildName = "Guild Refresh"
                }
            },
            "get_guild_channel_list" => new List<SubChannelInfo>
            {
                new()
                {
                    ChannelId = 7001,
                    ChannelName = "guild-chat",
                    ChannelType = ChannelType.Text
                }
            },
            "get_guild_members" => new GetGuildMembersResponse
            {
                Members =
                [
                    new GuildMember
                    {
                        TinyId = 8001,
                        Nickname = "GuildMember",
                        Title = "GM"
                    }
                ]
            },
            _ => throw new InvalidOperationException($"Unexpected action: {action}")
        });

        var context = CreateContext("9001");
        await harness.Manager.TryGetOrUpdateSelfInfo(context);
        harness.Manager.InitializeTasks();

        await WaitUntilAsync(() =>
            harness.Manager.GetAllPrivates().Any(info => info.UserId == "3001") &&
            harness.Manager.GetAllChannels().Any(info => info.ChannelId == "4001") &&
            harness.Manager.GetAllMembers("4001").Any(member => member.UserId == "5001"));

        Assert.Contains(harness.Manager.GetAllPrivates(), info => info.UserId == "3001");
        Assert.Contains(harness.Manager.GetAllChannels(), info => info.ChannelId == "4001");
        Assert.Contains(harness.Manager.GetAllChannels(), info => info.ChannelId == "6001");
    }

    private static MessageContext CreateContext(string selfId)
    {
        return new MessageContext(new DefaultRichMessageConverter())
        {
            SelfId = selfId,
            PlatformId = PlatformIds.OneBot
        };
    }

    private static OneBotMessageContext CreateNoticeContext(string selfId, string rawJson)
    {
        var context = new OneBotMessageContext(new DefaultRichMessageConverter())
        {
            SelfId = selfId,
            PlatformId = PlatformIds.OneBot,
            MessageIdentity = MessageIdentity.NoticeMessage
        };

        typeof(OneBotMessageContext)
            .GetProperty(nameof(OneBotMessageContext.RawJsonDocument), BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!
            .SetValue(context, JsonDocument.Parse(rawJson));

        return context;
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, int timeoutMs = 3000)
    {
        var started = DateTime.UtcNow;
        while (DateTime.UtcNow - started < TimeSpan.FromMilliseconds(timeoutMs))
        {
            if (predicate())
            {
                return;
            }

            await Task.Delay(50);
        }

        Assert.True(predicate(), "Condition was not satisfied before timeout.");
    }

    private static TestHarness CreateHarness(Func<string, string, IDictionary<string, object>?, object> handler)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();
        var schedulerLogger = services.GetRequiredService<Microsoft.Extensions.Logging.ILogger<BotTaskScheduler>>();
        var scheduler = new BotTaskScheduler(schedulerLogger, services);
        var api = new OneBotApi(new FakeOneBotConnector(handler));
        var managerLogger = services.GetRequiredService<Microsoft.Extensions.Logging.ILogger<OneBotContactsManager>>();
        var manager = new OneBotContactsManager(api, scheduler, managerLogger);
        return new TestHarness(services, scheduler, manager);
    }

    private sealed class FakeOneBotConnector(Func<string, string, IDictionary<string, object>?, object> handler) : IOneBotConnector
    {
        public Task<OneBotApiResponse<object>> SendMessageAsync(string action, IDictionary<string, object>? @params, string selfId)
        {
            return SendMessageAsync<object>(action, @params, selfId);
        }

        public Task<OneBotApiResponse<T>> SendMessageAsync<T>(string action, IDictionary<string, object>? @params, string selfId)
        {
            var data = handler(selfId, action, @params);
            return Task.FromResult(new OneBotApiResponse<T>
            {
                Status = "ok",
                Data = (T)data,
                State = "test"
            });
        }
    }

    private sealed class TestHarness(ServiceProvider serviceProvider, BotTaskScheduler scheduler, OneBotContactsManager manager)
        : IAsyncDisposable
    {
        public OneBotContactsManager Manager { get; } = manager;

        public async ValueTask DisposeAsync()
        {
            await scheduler.DisposeAsync();
            await serviceProvider.DisposeAsync();
        }
    }
}
