using System.Threading;

namespace MilkiBotFramework.Tasking
{
    /// <summary>
    /// 任务执行Delegate
    /// </summary>
    /// <param name="context">任务执行时的计划任务上下文</param>
    /// <param name="token">传入的<see cref="CancellationToken"/>。
    /// 用于外部调用<see cref="CancellationTokenSource"/>.Cancel()时，取消内部任务</param>
    public delegate void TaskExecutionHandler(TaskContext context, CancellationToken token);
}