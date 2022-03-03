using Microsoft.Extensions.Logging;

namespace MilkiBotFramework.Tasking
{
    /// <summary>
    /// 计划任务上下文
    /// </summary>
    public sealed class TaskContext
    {
        /// <summary>
        /// 任务Id
        /// </summary>
        public Guid TaskId { get; init; }

        /// <summary>
        /// 任务名称
        /// </summary>
        public string TaskName { get; init; }

        /// <summary>
        /// 本次任务执行的触发器
        /// <para>当Startup触发时为null。</para>
        /// </summary>
        public Trigger? Trigger { get; init; }

        /// <summary>
        /// 是否是Startup触发
        /// </summary>
        public bool IsStartupTrigger { get; init; }

        /// <summary>
        /// 任务触发时间
        /// <para>请以此时间为准，传到委托后调用DateTime.Now会有一定偏差。</para>
        /// </summary>
        public DateTime TriggerTime { get; init; }

        /// <summary>
        /// 任务触发器列表
        /// </summary>
        public IReadOnlyList<Trigger> Triggers { get; init; }

        /// <summary>
        /// 以触发器为单位，上次执行的时间列表
        /// </summary>
        public IReadOnlyList<DateTime> LastTriggerTimes { get; init; }

        /// <summary>
        /// 以触发器为单位，下次执行的时间列表
        /// </summary>
        public IReadOnlyList<DateTime> NextTriggerTimes { get; init; }

        public ILogger Logger { get; set; }
    }
}