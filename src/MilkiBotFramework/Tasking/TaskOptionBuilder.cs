namespace MilkiBotFramework.Tasking
{
    /// <summary>
    /// 为<see cref="TaskOption"/>的包装，以采用Fluent API配置
    /// </summary>
    public sealed class TaskOptionBuilder
    {
        public TaskOptionBuilder()
        {
            Option = new TaskOption();
        }

        public TaskOptionBuilder(TaskOption option)
        {
            Option = option;
        }

        public TaskOption Option { get; }

        /// <summary>
        /// 采用以年为单位，具体在每年的某个时间执行。
        /// </summary>
        /// <param name="dateTime">执行时间</param>
        /// <returns></returns>
        public TaskOptionBuilder EachYearAt(DateTime dateTime)
        {
            Option.Triggers.Add(new Trigger
            {
                TimestampUnit = TriggerTimestampUnit.Year,
                TimestampOffset = dateTime.ToYearOffset()
            });
            return this;
        }

        /// <summary>
        /// 采用以月为单位，具体在每个月的某个时间执行。
        /// </summary>
        /// <param name="dateTime">执行时间</param>
        /// <returns></returns>
        public TaskOptionBuilder EachMonthAt(DateTime dateTime)
        {
            Option.Triggers.Add(new Trigger
            {
                TimestampUnit = TriggerTimestampUnit.Month,
                TimestampOffset = dateTime.ToMonthOffset()
            });
            return this;
        }

        /// <summary>
        /// 采用以周为单位，具体在每周的某个时间执行。
        /// </summary>
        /// <param name="timeSpan">执行时间，表示方法为 0.00:00:00 - DayOfWeek.Hour:Minute:Second。
        /// 其中 DayOfWeek 0 = Sunday</param>
        /// <returns></returns>
        public TaskOptionBuilder EachWeekAt(TimeSpan timeSpan)
        {
            Option.Triggers.Add(new Trigger
            {
                TimestampUnit = TriggerTimestampUnit.Week,
                TimestampOffset = timeSpan.ToOffset()
            });
            return this;
        }

        /// <summary>
        /// 采用以日为单位，具体在每天的某个时间执行。
        /// </summary>
        /// <param name="dateTime">执行时间</param>
        /// <returns></returns>
        public TaskOptionBuilder EachDayAt(DateTime dateTime)
        {
            Option.Triggers.Add(new Trigger
            {
                TimestampUnit = TriggerTimestampUnit.Day,
                TimestampOffset = dateTime.ToDayOffset()
            });
            return this;
        }

        /// <summary>
        /// 采用以小时为单位，具体在每小时的某个时间执行。
        /// </summary>
        /// <param name="dateTime">执行时间</param>
        /// <returns></returns>
        public TaskOptionBuilder EachHourAt(DateTime dateTime)
        {
            Option.Triggers.Add(new Trigger
            {
                TimestampUnit = TriggerTimestampUnit.Hour,
                TimestampOffset = dateTime.ToHourOffset()
            });
            return this;
        }

        /// <summary>
        /// 采用以分钟为单位，具体在每分钟的某个时间执行。
        /// </summary>
        /// <param name="dateTime">执行时间</param>
        /// <returns></returns>
        public TaskOptionBuilder EachMinuteAt(DateTime dateTime)
        {
            Option.Triggers.Add(new Trigger
            {
                TimestampUnit = TriggerTimestampUnit.Minute,
                TimestampOffset = dateTime.ToMinuteOffset()
            });
            return this;
        }

        /// <summary>
        /// 采用固定间隔执行任务
        /// </summary>
        /// <param name="interval">间隔时间</param>
        /// <returns></returns>
        public TaskOptionBuilder ByInterval(TimeSpan interval)
        {
            Option.Triggers.Add(new Trigger
            {
                TimestampUnit = TriggerTimestampUnit.Interval,
                TimestampOffset = interval.ToOffset()
            });
            return this;
        }

        /// <summary>
        /// 在初始化时执行任务
        /// </summary>
        /// <returns></returns>
        public TaskOptionBuilder AtStartup()
        {
            Option.TriggerOnStartup = true;
            return this;
        }

        public TaskOptionBuilder WithoutLogging()
        {
            Option.UseLogging = false;
            return this;
        }

        /// <summary>
        /// 执行操作
        /// </summary>
        /// <param name="handler">执行操作回调</param>
        /// <returns></returns>
        public TaskOptionBuilder Do(TaskExecutionHandler handler)
        {
            Option.Handler = handler;
            return this;
        }
    }
}
