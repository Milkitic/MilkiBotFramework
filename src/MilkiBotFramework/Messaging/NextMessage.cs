using System;
using System.Threading;
using System.Threading.Tasks;

namespace MilkiBotFramework.Messaging;

internal class NextMessage : IAsyncMessage
{
    private TaskCompletionSource? _taskCompleteSource;
    public string? RawMessage { get; set; }

    public void SetMessage(string message)
    {
        RawMessage = message;
        _taskCompleteSource?.TrySetResult();
    }

    public async Task<string?> TryGetNextMessage(TimeSpan dueTime)
    {
        if (RawMessage != null) return RawMessage;
        if (_taskCompleteSource == null)
        {
            using var cts = new CancellationTokenSource(dueTime);
            _taskCompleteSource = new TaskCompletionSource();
            cts.Token.Register(() => _taskCompleteSource.TrySetCanceled());
            await _taskCompleteSource.Task;
        }
        else
        {
            await _taskCompleteSource.Task;
        }

        return RawMessage;
    }
}