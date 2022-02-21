using System.Threading;
using System.Threading.Tasks;

namespace MilkiBotFramework.Tasking
{
    /// <summary>
    /// 任务实例，当任务创建时进行创建
    /// </summary>
    public sealed class TaskInstance
    {
        public TaskInstance(TaskOption option)
        {
            Option = option;
        }

        /// <summary>
        /// 任务的设置信息
        /// </summary>
        public TaskOption Option { get; }

        /// <summary>
        /// 后台执行的实际任务
        /// </summary>
        public Task Task { get; set; }

        /// <summary>
        /// 任务的<see cref="System.Threading.CancellationTokenSource"/>信号
        /// </summary>
        public CancellationTokenSource CancellationTokenSource { get; } = new();
    }
}