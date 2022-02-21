using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Dispatching;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Tasking;

namespace MilkiBotFramework.ContractsManaging;

public interface IContractsManager
{
    bool TryGetChannelInfo(MessageIdentity messageIdentity, out ChannelInfo channelInfo);
}

public class ChannelInfo
{
    public bool IsRootChannel => SubChannelId == null;
    public string ChannelId { get; internal set; }
    public string? SubChannelId { get; internal set; }
    public string Name { get; internal set; }
    public List<Member> Members { get; internal set; }
}

public class Member
{
    public string UserId { get; internal set; }
    public string Card { get; internal set; }
    public MemberRole MemberRole { get; internal set; }
}

public enum MemberRole
{
    Role, Admin, Member
}

public abstract class ContractsManagerBase : IContractsManager
{
    private readonly BotTaskScheduler _botTaskScheduler;
    private readonly ILogger _logger;
    private IDispatcher? _dispatcher;

    public ContractsManagerBase(BotTaskScheduler botTaskScheduler, ILogger logger)
    {
        _botTaskScheduler = botTaskScheduler;
        _logger = logger;
        _botTaskScheduler.AddTask("RefreshContractsTask", k => k
            .ByInterval(TimeSpan.FromMinutes(15))
            .AtStartup()
            .Do(RefreshContracts));
    }

    private void RefreshContracts(TaskContext context, CancellationToken token)
    {
        _logger.LogInformation("Refreshed!");
    }

    public IDispatcher? Dispatcher
    {
        get => _dispatcher;
        internal set
        {
            if (_dispatcher != null) _dispatcher.SystemMessageReceived -= Dispatcher_SystemMessageReceived;
            _dispatcher = value;
            if (_dispatcher != null) _dispatcher.SystemMessageReceived += Dispatcher_SystemMessageReceived;
        }
    }

    private System.Threading.Tasks.Task Dispatcher_SystemMessageReceived(MessageContext arg)
    {
        throw new System.NotImplementedException();
    }

    public bool TryGetChannelInfo(MessageIdentity messageIdentity, out ChannelInfo channelInfo)
    {
        throw new System.NotImplementedException();
    }
}