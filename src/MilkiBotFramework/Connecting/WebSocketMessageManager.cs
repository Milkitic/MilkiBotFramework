using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace MilkiBotFramework.Connecting;

public class WebSocketMessageManager
{
    public delegate bool TryGetStateByMessageDelegate(string msg, [NotNullWhen(true)] out string? state);

    private readonly ILogger _logger;

    private readonly Func<string, Task> _sendAction;
    private readonly Func<string, Task>? _rawReceivedFunc;
    private readonly TryGetStateByMessageDelegate _tryGetStateDelegate;

    private readonly ConcurrentDictionary<string, WebsocketRequestSession> _sessions = new();
    private readonly Func<TimeSpan> _getMessageTimeout;

    public WebSocketMessageManager(ILogger logger,
        Func<TimeSpan> getMessageTimeout,
        Func<string, Task> sendAction,
        Func<string, Task>? rawReceivedFunc,
        TryGetStateByMessageDelegate tryGetStateDelegate)
    {
        _getMessageTimeout = getMessageTimeout;
        _logger = logger;
        _sendAction = sendAction;
        _rawReceivedFunc = rawReceivedFunc;
        _tryGetStateDelegate = tryGetStateDelegate;
    }

    public async Task<string> SendMessageAsync(string message, string state)
    {
        var tcs = new TaskCompletionSource();
        var messageTimeout = _getMessageTimeout();
        using var cts = new CancellationTokenSource(messageTimeout);
        cts.Token.Register(() =>
        {
            try
            {
                tcs.SetCanceled();
                _logger.LogWarning($"Message is forced to time out after {messageTimeout.Seconds} seconds.");
            }
            catch
            {
                // ignored
            }
        });
        var sessionObj = new WebsocketRequestSession(tcs);
        _sessions.TryAdd(state, sessionObj);
        await _sendAction(message);
        try
        {
            await tcs.Task.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new Exception("Timed out for receiving response from websocket server.", ex);
        }
        finally
        {
            _sessions.TryRemove(state, out _);
        }

        if (sessionObj.Response == null) throw new NullReferenceException();
        return sessionObj.Response;
    }

    public Task InvokeMessageReceive(string msg)
    {
        var hasState = _tryGetStateDelegate(msg, out var state);
        if (!hasState || string.IsNullOrEmpty(state))
        {
            _rawReceivedFunc?.Invoke(msg);
            return Task.CompletedTask;
        }

        if (_sessions.TryGetValue(state, out var sessionObj))
        {
            sessionObj.Response = msg;
            try
            {
                sessionObj.TaskCompletionSource.SetResult();
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning($"Rollback to raw message: response received but the task has been canceled due to the timeout setting.");
            }
        }
        else
        {
            _logger.LogWarning($"Rollback to raw message due to unknown response state: {state}.");
            _rawReceivedFunc?.Invoke(msg);
        }

        return Task.CompletedTask;
    }
}