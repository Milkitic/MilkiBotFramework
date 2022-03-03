namespace MilkiBotFramework.Tasking
{
    /// <summary>
    /// 计划任务触发器。
    /// <para>当前仅时间实现。</para>
    /// </summary>
    public sealed class Trigger
    {
        /// <summary>
        /// 触发器的时间单位类型
        /// </summary>
        public TriggerTimestampUnit TimestampUnit { get; init; }

        /// <summary>
        /// 触发器的偏移量
        /// </summary>
        public TimestampOffset TimestampOffset { get; init; }

        /// <summary>
        /// 获取下次执行的时间
        /// </summary>
        /// <param name="dateTime">计算的相对时间</param>
        /// <returns></returns>
        public DateTime GetNextExecutionTime(DateTime dateTime)
        {
            return dateTime.ComputeExecutionTime(this, true);
        }

        /// <summary>
        /// 获取上次执行的时间
        /// </summary>
        /// <param name="dateTime">计算的相对时间</param>
        /// <returns></returns>
        public DateTime GetLastExecutionTime(DateTime dateTime)
        {
            return dateTime.ComputeExecutionTime(this, false);
        }
    }
}