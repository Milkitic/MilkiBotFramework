namespace MilkiBotFramework.Tasking
{
    /// <summary>
    /// 任务设置
    /// </summary>
    public sealed class TaskOption
    {
        /// <summary>
        /// 任务Id
        /// </summary>
        public Guid Id { get; init; }

        /// <summary>
        /// 任务名称
        /// </summary>
        public string Name { get; init; }

        /// <summary>
        /// 是否在初始化时执行任务
        /// </summary>
        public bool TriggerOnStartup { get; set; }

        /// <summary>
        /// 触发器列表
        /// </summary>
        public List<Trigger> Triggers { get; set; } = new();

        /// <summary>
        /// 执行操作回调
        /// </summary>
        public TaskExecutionHandler? Handler { get; set; }
    }
}