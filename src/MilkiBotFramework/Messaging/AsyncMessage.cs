namespace MilkiBotFramework.Messaging;

internal class AsyncMessage : IAsyncMessage
{
    private readonly object _lock = new();
    private TaskCompletionSource? _taskCompleteSource;
    internal IAsyncMessageResponse? Response { get; set; }

    public void SetMessage(IAsyncMessageResponse message)
    {
        lock (_lock)
        {
            Response = message;
        }

        _taskCompleteSource?.TrySetResult();
    }

    public Task<IAsyncMessageResponse> GetNextMessageAsync(int seconds = 10)
    {
        return GetNextMessageAsync(TimeSpan.FromSeconds(seconds));
    }

    public async Task<IAsyncMessageResponse> GetNextMessageAsync(TimeSpan dueTime)
    {
        lock (_lock)
        {
            if (Response != null)
            {
                return Response;
            }
        }

        try
        {
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
        }
        catch (TaskCanceledException)
        {
            throw new AsyncMessageTimeoutException("Async message timeout after " + dueTime.TotalSeconds + "s");
        }

        return Response!;
    }
}