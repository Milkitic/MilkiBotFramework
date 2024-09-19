namespace MilkiBotFramework.Utils;

public static class WaitHandleExtensions
{
    //https://stackoverflow.com/questions/18756354/wrapping-manualresetevent-as-awaitable-task
    public static Task WaitOneAsync(this WaitHandle waitHandle, CancellationToken cancellationToken, int timeoutMilliseconds = Timeout.Infinite)
    {
        if (waitHandle == null)
            throw new ArgumentNullException(nameof(waitHandle));

        var tcs = new TaskCompletionSource<bool>();
        var tokenRegistration = cancellationToken.Register(() => tcs.TrySetCanceled());
        var timeout = timeoutMilliseconds > Timeout.Infinite
            ? TimeSpan.FromMilliseconds(timeoutMilliseconds)
            : Timeout.InfiniteTimeSpan;

        var registeredWaitHandle = ThreadPool.RegisterWaitForSingleObject(waitHandle,
            (_, timedOut) =>
            {
                if (timedOut)
                {
                    tcs.TrySetCanceled();
                }
                else
                {
                    tcs.TrySetResult(true);
                }
            },
            null, timeout, true);

        _ = tcs.Task.ContinueWith(_ =>
        {
            registeredWaitHandle.Unregister(null);
            return tokenRegistration.Unregister();
        }, CancellationToken.None);

        return tcs.Task;
    }
}