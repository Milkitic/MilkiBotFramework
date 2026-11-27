using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MilkiBotFramework;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.ContactsManaging;
using MilkiBotFramework.ContactsManaging.Models;
using MilkiBotFramework.ContactsManaging.Results;
using MilkiBotFramework.Dispatching;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Platforms;
using MilkiBotFramework.Platforms.QQ.Connecting;
using MilkiBotFramework.Platforms.QQ.Connecting.Models;
using MilkiBotFramework.Platforms.QQ.ContactsManaging;
using MilkiBotFramework.Platforms.QQ.Dispatching;
using MilkiBotFramework.Platforms.QQ.Messaging;
using MilkiBotFramework.Tasking;
using Xunit;
using MemberInfo = MilkiBotFramework.ContactsManaging.Models.MemberInfo;

namespace UnitTests;

public class QQContactsAndRoleTests
{
    [Fact]
    public void QDispatcher_ParsesGroupAtAndGroupMessageCreateWithRoleAndNames()
    {
        using var services = CreateServiceProvider();
        var dispatcher = CreateDispatcher(services);

        var atPayload = """
                        {
                          "t": "GROUP_AT_MESSAGE_CREATE",
                          "d": {
                            "id": "msg-1",
                            "group_openid": "group-1",
                            "content": "  hello bot ",
                            "timestamp": "2026-11-27T10:00:00+08:00",
                            "author": {
                              "member_openid": "user-1",
                              "member_role": "owner",
                              "username": "Alice",
                              "member_nickname": "GroupOwnerAlice"
                            }
                          }
                        }
                        """;

        var atContext = PopulateContext(services, dispatcher, atPayload);
        Assert.Equal("GROUP_AT_MESSAGE_CREATE", atContext.EventType);
        Assert.Equal(MemberRole.Owner, atContext.AuthorMemberRole);
        Assert.Equal("Alice", atContext.AuthorUsername);
        Assert.Equal("GroupOwnerAlice", atContext.AuthorMemberNickname);
        Assert.Equal(MessageType.Channel, atContext.MessageIdentity?.MessageType);
        Assert.Equal("group-1", atContext.MessageIdentity?.Id);
        Assert.Equal("user-1", atContext.MessageUserIdentity?.UserId);
        Assert.Equal("hello bot", atContext.TextMessage);

        var allMsgPayload = """
                            {
                              "t": "GROUP_MESSAGE_CREATE",
                              "d": {
                                "id": "msg-2",
                                "group_openid": "group-1",
                                "content": "ping",
                                "timestamp": "2026-11-27T10:01:00+08:00",
                                "author": {
                                  "member_openid": "user-2",
                                  "member_role": "admin"
                                }
                              }
                            }
                            """;

        var allContext = PopulateContext(services, dispatcher, allMsgPayload);
        Assert.Equal("GROUP_MESSAGE_CREATE", allContext.EventType);
        Assert.Equal(MemberRole.Admin, allContext.AuthorMemberRole);
        Assert.Equal("group-1", allContext.MessageIdentity?.Id);
        Assert.Equal("user-2", allContext.MessageUserIdentity?.UserId);

        var missingRolePayload = """
                                 {
                                   "t": "GROUP_AT_MESSAGE_CREATE",
                                   "d": {
                                     "id": "msg-3",
                                     "group_openid": "group-1",
                                     "content": "hi",
                                     "timestamp": "2026-11-27T10:02:00+08:00",
                                     "author": {
                                       "member_openid": "user-3"
                                     }
                                   }
                                 }
                                 """;

        var missingRoleContext = PopulateContext(services, dispatcher, missingRolePayload);
        Assert.Null(missingRoleContext.AuthorMemberRole);
        Assert.True(dispatcher.CanDispatch(InboundMessage.FromRawText(allMsgPayload)));
    }

    [Fact]
    public void QDispatcher_ParsesC2CAndNoticeEventsWithUnixTimestamps()
    {
        using var services = CreateServiceProvider();
        var dispatcher = CreateDispatcher(services);

        var c2cPayload = """
                         {
                           "t": "C2C_MESSAGE_CREATE",
                           "d": {
                             "id": "c2c-1",
                             "content": "hi private",
                             "timestamp": "2026-11-27T10:00:00+08:00",
                             "author": {
                               "user_openid": "private-1",
                               "username": "Bob"
                             }
                           }
                         }
                         """;

        var c2cContext = PopulateContext(services, dispatcher, c2cPayload);
        Assert.Equal(MessageType.Private, c2cContext.MessageIdentity?.MessageType);
        Assert.Equal("private-1", c2cContext.MessageUserIdentity?.UserId);
        Assert.Equal("Bob", c2cContext.AuthorUsername);

        var noticePayloads = new[]
        {
            """{"id":"evt-1","t":"GROUP_ADD_ROBOT","d":{"group_openid":"g-1","op_member_openid":"u-1","timestamp":1700000000}}""",
            """{"id":"evt-2","t":"GROUP_DEL_ROBOT","d":{"group_openid":"g-1","op_member_openid":"u-1","timestamp":1700000001}}""",
            """{"id":"evt-3","t":"FRIEND_ADD","d":{"openid":"u-2","timestamp":1700000002}}""",
            """{"id":"evt-4","t":"FRIEND_DEL","d":{"openid":"u-2","timestamp":1700000003}}"""
        };

        foreach (var payload in noticePayloads)
        {
            Assert.True(dispatcher.CanDispatch(InboundMessage.FromRawText(payload)));
            var ctx = PopulateContext(services, dispatcher, payload);
            Assert.Equal(MessageIdentity.NoticeMessage, ctx.MessageIdentity);
            Assert.Equal(string.Empty, ctx.TextMessage);
            Assert.True(ctx.ReceivedTime.ToUnixTimeSeconds() >= 1700000000);
        }
    }

    [Theory]
    [InlineData("owner", MemberRole.Owner, MessageAuthority.Admin)]
    [InlineData("admin", MemberRole.Admin, MessageAuthority.Admin)]
    [InlineData("member", MemberRole.Member, MessageAuthority.Public)]
    public async Task MessageContextEnricher_MapsQQRoleToAuthority(string rawRole, MemberRole expectedRole, MessageAuthority expectedAuthority)
    {
        await using var harness = CreateContactsHarness(new FakeGroupMemberApi());
        var enricher = new MessageContextEnricher(
            harness.Manager,
            new BotOptions(),
            harness.Services.GetRequiredService<ILogger<MessageContextEnricher>>());

        var ctx = CreateGroupMessageContext("group-1", "user-1", rawRole);
        await enricher.EnrichAsync(ctx);

        Assert.NotNull(ctx.MemberInfo);
        Assert.Equal(expectedRole, ctx.MemberInfo!.MemberRole);
        Assert.Equal(expectedAuthority, ctx.Authority);
    }

    [Fact]
    public async Task MessageContextEnricher_RootAccountTakesPrecedenceOverMemberRole()
    {
        await using var harness = CreateContactsHarness(new FakeGroupMemberApi());
        var options = new BotOptions { RootAccounts = ["root-user"] };
        var enricher = new MessageContextEnricher(
            harness.Manager,
            options,
            harness.Services.GetRequiredService<ILogger<MessageContextEnricher>>());

        var ctx = CreateGroupMessageContext("group-1", "root-user", "member");
        await enricher.EnrichAsync(ctx);

        Assert.Equal(MemberRole.Member, ctx.MemberInfo!.MemberRole);
        Assert.Equal(MessageAuthority.Root, ctx.Authority);
    }

    [Fact]
    public async Task QContactsManager_UpdatesRoleAcrossMessagesAndDoesNotDowngradeWhenOmitted()
    {
        var events = new List<ContactsUpdateEvent>();
        await using var harness = CreateContactsHarness(new FakeGroupMemberApi());
        harness.Manager.ContactsUpdated += evt =>
        {
            events.Add(evt);
            return Task.CompletedTask;
        };

        var enricher = new MessageContextEnricher(
            harness.Manager,
            new BotOptions(),
            harness.Services.GetRequiredService<ILogger<MessageContextEnricher>>());

        // member -> admin -> owner -> member
        var ctx1 = CreateGroupMessageContext("group-1", "user-1", "member", "User1", "Card1");
        await enricher.EnrichAsync(ctx1);
        Assert.Equal(MessageAuthority.Public, ctx1.Authority);

        var ctx2 = CreateGroupMessageContext("group-1", "user-1", "admin");
        await enricher.EnrichAsync(ctx2);
        Assert.Equal(MemberRole.Admin, ctx2.MemberInfo!.MemberRole);
        Assert.Equal(MessageAuthority.Admin, ctx2.Authority);

        var ctx3 = CreateGroupMessageContext("group-1", "user-1", "owner");
        await enricher.EnrichAsync(ctx3);
        Assert.Equal(MemberRole.Owner, ctx3.MemberInfo!.MemberRole);
        Assert.Equal(MessageAuthority.Admin, ctx3.Authority);

        // 省略 member_role 时不覆盖已有 Owner 角色
        var ctxOmitted = CreateGroupMessageContext("group-1", "user-1", null);
        await enricher.EnrichAsync(ctxOmitted);
        Assert.Equal(MemberRole.Owner, ctxOmitted.MemberInfo!.MemberRole);
        Assert.Equal(MessageAuthority.Admin, ctxOmitted.Authority);

        var ctx4 = CreateGroupMessageContext("group-1", "user-1", "member");
        await enricher.EnrichAsync(ctx4);
        Assert.Equal(MemberRole.Member, ctx4.MemberInfo!.MemberRole);
        Assert.Equal(MessageAuthority.Public, ctx4.Authority);

        var roleChanges = events
            .SelectMany(e => e.Events)
            .Where(s => s.UpdateRole == ContactsUpdateRole.Member &&
                        s.UpdateType == ContactsUpdateType.Changed &&
                        s.ChangedPath == nameof(MemberInfo.MemberRole))
            .ToList();
        Assert.Equal(3, roleChanges.Count);
    }

    [Fact]
    public async Task QContactsManager_FallbacksToMemberApiAndTripsCircuitBreakerOn11253()
    {
        var api = new FakeGroupMemberApi
        {
            GetMemberHandler = (_, memberOpenId) => Task.FromResult(new QGroupMember
            {
                MemberOpenId = memberOpenId,
                Username = "ApiUser",
                MemberNickname = "ApiCard",
                Role = MemberRole.Admin
            })
        };

        await using var harness = CreateContactsHarness(api);
        var enricher = new MessageContextEnricher(
            harness.Manager,
            new BotOptions(),
            harness.Services.GetRequiredService<ILogger<MessageContextEnricher>>());

        // 首次出现且消息省略 member_role -> 调用 API 补全为 Admin
        var ctx1 = CreateGroupMessageContext("group-1", "user-1", null);
        await enricher.EnrichAsync(ctx1);
        Assert.Equal(1, api.GetMemberCalls);
        Assert.Equal(MemberRole.Admin, ctx1.MemberInfo!.MemberRole);
        Assert.Equal("ApiUser", ctx1.MemberInfo.Nickname);
        Assert.Equal("ApiCard", ctx1.MemberInfo.Card);
        Assert.Equal(MessageAuthority.Admin, ctx1.Authority);

        // API 返回 11253 时熔断并降级为 Member，且后续不再调用 API
        var deniedApi = new FakeGroupMemberApi
        {
            GetMemberHandler = (_, _) => throw new QApiException("11253", "应用无接口访问权限", HttpStatusCode.Forbidden)
        };
        await using var deniedHarness = CreateContactsHarness(deniedApi);
        var deniedEnricher = new MessageContextEnricher(
            deniedHarness.Manager,
            new BotOptions(),
            deniedHarness.Services.GetRequiredService<ILogger<MessageContextEnricher>>());

        var d1 = CreateGroupMessageContext("group-1", "user-1", null);
        await deniedEnricher.EnrichAsync(d1);
        var d2 = CreateGroupMessageContext("group-1", "user-2", null);
        await deniedEnricher.EnrichAsync(d2);

        Assert.Equal(1, deniedApi.GetMemberCalls);
        Assert.Equal(MemberRole.Member, d1.MemberInfo!.MemberRole);
        Assert.Equal(MemberRole.Member, d2.MemberInfo!.MemberRole);
    }

    [Fact]
    public async Task QContactsManager_HandlesGroupAndFriendNoticeEvents()
    {
        var events = new List<ContactsUpdateEvent>();
        await using var harness = CreateContactsHarness(new FakeGroupMemberApi());
        harness.Manager.ContactsUpdated += evt =>
        {
            events.Add(evt);
            return Task.CompletedTask;
        };

        await harness.Manager.HandleMessageAsync(CreateNoticeContext("""{"t":"GROUP_ADD_ROBOT","d":{"group_openid":"g-1","timestamp":1700000000}}"""));
        Assert.Contains(harness.Manager.GetAllChannels(), c => c.ChannelId == "g-1");

        await harness.Manager.HandleMessageAsync(CreateNoticeContext("""{"t":"GROUP_DEL_ROBOT","d":{"group_openid":"g-1","timestamp":1700000001}}"""));
        Assert.DoesNotContain(harness.Manager.GetAllChannels(), c => c.ChannelId == "g-1");

        await harness.Manager.HandleMessageAsync(CreateNoticeContext("""{"t":"FRIEND_ADD","d":{"openid":"u-1","timestamp":1700000002}}"""));
        Assert.Contains(harness.Manager.GetAllPrivates(), p => p.UserId == "u-1");

        await harness.Manager.HandleMessageAsync(CreateNoticeContext("""{"t":"FRIEND_DEL","d":{"openid":"u-1","timestamp":1700000003}}"""));
        Assert.DoesNotContain(harness.Manager.GetAllPrivates(), p => p.UserId == "u-1");

        var singles = events.SelectMany(e => e.Events).ToList();
        Assert.Equal(4, singles.Count);
        Assert.Equal(ContactsUpdateType.Added, singles[0].UpdateType);
        Assert.Equal(ContactsUpdateType.Removed, singles[1].UpdateType);
        Assert.Equal(ContactsUpdateType.Added, singles[2].UpdateType);
        Assert.Equal(ContactsUpdateType.Removed, singles[3].UpdateType);
    }

    [Fact]
    public async Task QContactsManager_TryRefreshGroupMembersAsync_ReconcilesDiffAndHandles11253()
    {
        var currentRemote = new List<QGroupMember>
        {
            new() { MemberOpenId = "u-1", Username = "UserOne", MemberNickname = "CardOne", Role = MemberRole.Owner },
            new() { MemberOpenId = "u-2", Username = "UserTwo", MemberNickname = "CardTwo", Role = MemberRole.Member }
        };

        var api = new FakeGroupMemberApi
        {
            GetMembersHandler = _ => Task.FromResult<IReadOnlyList<QGroupMember>>(currentRemote)
        };

        var events = new List<ContactsUpdateEvent>();
        await using var harness = CreateContactsHarness(api);
        harness.Manager.ContactsUpdated += evt =>
        {
            events.Add(evt);
            return Task.CompletedTask;
        };

        Assert.True(await harness.Manager.TryRefreshGroupMembersAsync("g-1"));
        Assert.Equal(2, harness.Manager.GetAllMembers("g-1").Count());

        // u-1 改名片，u-2 升为 admin，u-3 加入，随后移除 u-2
        currentRemote =
        [
            new() { MemberOpenId = "u-1", Username = "UserOne", MemberNickname = "CardOneNew", Role = MemberRole.Owner },
            new() { MemberOpenId = "u-3", Username = "UserThree", MemberNickname = "CardThree", Role = MemberRole.Admin }
        ];

        events.Clear();
        Assert.True(await harness.Manager.TryRefreshGroupMembersAsync("g-1"));

        var members = harness.Manager.GetAllMembers("g-1").ToDictionary(m => m.UserId);
        Assert.Equal(2, members.Count);
        Assert.Equal("CardOneNew", members["u-1"].Card);
        Assert.Equal(MemberRole.Admin, members["u-3"].MemberRole);
        Assert.False(members.ContainsKey("u-2"));

        var diffEvents = events.SelectMany(e => e.Events).ToList();
        Assert.Contains(diffEvents, e => e.UpdateType == ContactsUpdateType.Changed && e.ChangedPath == nameof(MemberInfo.Card) && e.MemberInfo?.UserId == "u-1");
        Assert.Contains(diffEvents, e => e.UpdateType == ContactsUpdateType.Added && e.MemberInfo?.UserId == "u-3");
        Assert.Contains(diffEvents, e => e.UpdateType == ContactsUpdateType.Removed && e.MemberInfo?.UserId == "u-2");

        // 验证 11253 熔断
        api.GetMembersHandler = _ => throw new QApiException("11253", "no permission", HttpStatusCode.Forbidden);
        Assert.False(await harness.Manager.TryRefreshGroupMembersAsync("g-1"));
        Assert.False(await harness.Manager.TryRefreshGroupMembersAsync("g-1"));
        Assert.Equal(3, api.GetMembersCalls);
    }

    [Fact]
    public async Task ContactsManagerBase_TryUpdateMember_DefaultsNewMemberRoleToMember()
    {
        await using var harness = CreateContactsHarness(new FakeGroupMemberApi());
        await harness.Manager.TryGetOrAddChannelInfo("g-1");

        var method = typeof(ContactsManagerBase).GetMethod("TryUpdateMember", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var updateInfo = new ContactsUpdateInfo("g-1")
        {
            UserId = "u-new",
            Name = "NewUser",
            ContactsUpdateRole = ContactsUpdateRole.Member,
            ContactsUpdateType = ContactsUpdateType.Added
        };

        await (Task)method.Invoke(harness.Manager, [updateInfo])!;
        var result = await harness.Manager.TryGetOrAddMemberInfo("g-1", "u-new");

        Assert.True(result.IsSuccess);
        Assert.Equal(MemberRole.Member, result.MemberInfo!.MemberRole);
    }

    private static ServiceProvider CreateServiceProvider()
    {
        return new ServiceCollection()
            .AddLogging()
            .AddSingleton<IRichMessageConverter, DefaultRichMessageConverter>()
            .AddScoped<QMessageContext>()
            .BuildServiceProvider();
    }

    private static QDispatcher CreateDispatcher(ServiceProvider services)
    {
        var botOptions = new BotOptions();
        var loggerFactory = services.GetRequiredService<ILoggerFactory>();
        var dummyManager = new QContactsManager(
            (IQGroupMemberApi?)null,
            new BotTaskScheduler(loggerFactory.CreateLogger<BotTaskScheduler>(), services),
            loggerFactory.CreateLogger<QContactsManager>());
        var enricher = new MessageContextEnricher(dummyManager, botOptions, loggerFactory.CreateLogger<MessageContextEnricher>());
        var coordinator = (MessageDispatchCoordinator)RuntimeHelpers.GetUninitializedObject(typeof(MessageDispatchCoordinator));
        return new QDispatcher(enricher, coordinator, loggerFactory.CreateLogger<QDispatcher>(), services);
    }

    private static QMessageContext PopulateContext(ServiceProvider services, QDispatcher dispatcher, string rawJson)
    {
        using var scope = services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<QMessageContext>();
        ctx.PlatformId = PlatformIds.Qq;
        var inbound = InboundMessage.FromRawText(rawJson, "qq-http");

        var method = typeof(QDispatcher).GetMethod("TryPopulateMessageContext", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var args = new object?[] { ctx, inbound, null };
        var ok = (bool)method.Invoke(dispatcher, args)!;
        Assert.True(ok, $"TryPopulateMessageContext failed: {args[2]}");
        return ctx;
    }

    private static QMessageContext CreateGroupMessageContext(string groupOpenId, string memberOpenId, string? rawRole,
        string? username = null, string? memberNickname = null)
    {
        var ctx = new QMessageContext(new DefaultRichMessageConverter())
        {
            PlatformId = PlatformIds.Qq,
            EventType = "GROUP_AT_MESSAGE_CREATE",
            MessageIdentity = new MessageIdentity(groupOpenId, MessageType.Channel),
            AuthorUsername = username,
            AuthorMemberNickname = memberNickname
        };
        ctx.MessageUserIdentity = new MessageUserIdentity(ctx.MessageIdentity, memberOpenId);
        if (QMemberRoleParser.TryParse(rawRole, out var parsedRole))
        {
            ctx.AuthorMemberRole = parsedRole;
        }

        return ctx;
    }

    private static QMessageContext CreateNoticeContext(string rawJson)
    {
        return new QMessageContext(new DefaultRichMessageConverter())
        {
            PlatformId = PlatformIds.Qq,
            MessageIdentity = MessageIdentity.NoticeMessage,
            RawJsonDocument = JsonDocument.Parse(rawJson)
        };
    }

    private static ContactsHarness CreateContactsHarness(IQGroupMemberApi? groupMemberApi)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();
        var scheduler = new BotTaskScheduler(services.GetRequiredService<ILogger<BotTaskScheduler>>(), services);
        var manager = new QContactsManager(groupMemberApi, scheduler, services.GetRequiredService<ILogger<QContactsManager>>());
        return new ContactsHarness(services, scheduler, manager);
    }

    private sealed class FakeGroupMemberApi : IQGroupMemberApi
    {
        public int GetMemberCalls { get; private set; }
        public int GetMembersCalls { get; private set; }

        public Func<string, string, Task<QGroupMember>>? GetMemberHandler { get; set; }
        public Func<string, Task<IReadOnlyList<QGroupMember>>>? GetMembersHandler { get; set; }

        public Task<QGroupMember> GetGroupMemberAsync(string groupOpenId, string memberOpenId, CancellationToken cancellationToken = default)
        {
            GetMemberCalls++;
            if (GetMemberHandler != null)
            {
                return GetMemberHandler(groupOpenId, memberOpenId);
            }

            return Task.FromResult(new QGroupMember { MemberOpenId = memberOpenId, Role = MemberRole.Member });
        }

        public Task<QGroupMembersPage> GetGroupMembersPageAsync(string groupOpenId, string? cursor = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new QGroupMembersPage());
        }

        public Task<IReadOnlyList<QGroupMember>> GetGroupMembersAsync(string groupOpenId, CancellationToken cancellationToken = default)
        {
            GetMembersCalls++;
            if (GetMembersHandler != null)
            {
                return GetMembersHandler(groupOpenId);
            }

            return Task.FromResult<IReadOnlyList<QGroupMember>>(Array.Empty<QGroupMember>());
        }
    }

    private sealed class ContactsHarness(ServiceProvider services, BotTaskScheduler scheduler, QContactsManager manager)
        : IAsyncDisposable
    {
        public ServiceProvider Services { get; } = services;
        public QContactsManager Manager { get; } = manager;

        public async ValueTask DisposeAsync()
        {
            await scheduler.DisposeAsync();
            await Services.DisposeAsync();
        }
    }
}
