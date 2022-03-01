using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MilkiBotFramework.Tasking
{
    /// <summary>
    /// 计划任务管理器
    /// </summary>
    public sealed class BotTaskScheduler : IAsyncDisposable
    {
        private readonly ILogger<BotTaskScheduler> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<Guid, TaskInstance> _tasks = new();

        public BotTaskScheduler(ILogger<BotTaskScheduler> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// 新建一个任务
        /// </summary>
        /// <param name="name">任务名称</param>
        /// <param name="options">任务设置</param>
        public void AddTask(string name, Action<TaskOptionBuilder> options)
        {
            var builder = new TaskOptionBuilder(new TaskOption
            {
                Id = Guid.NewGuid(),
                Name = name
            });
            options?.Invoke(builder);
            var option = builder.Option;

            if (option.Triggers.Count == 0)
            {
                _logger.LogWarning("No triggers for task {0}", name);
            }

            var taskInstance = new TaskInstance(option);
            taskInstance.Task = Task.Factory.StartNew(() => TaskLoop(taskInstance));
            _tasks.Add(option.Id, taskInstance);
        }

        /// <summary>
        /// 停止全部任务
        /// </summary>
        /// <returns></returns>
        public async Task CancelAllAsync()
        {
            foreach (var (id, taskInstance) in _tasks.ToList())
            {
                taskInstance.CancellationTokenSource.Cancel();
                await taskInstance.Task;
                taskInstance.Task.Dispose();
                taskInstance.CancellationTokenSource.Dispose();
                _tasks.Remove(id);
            }
        }

        private void TaskLoop(TaskInstance taskInstance)
        {
            // loop implementation
            var taskOption = taskInstance.Option;
            var loggerFactory = _serviceProvider.GetService<ILoggerFactory>();
            var logger = loggerFactory!.CreateLogger("EamTaskScheduler." + taskOption.Name);
            var cts = taskInstance.CancellationTokenSource;
            if (taskOption.TriggerOnStartup) Execute(null, DateTime.Now);
            while (!cts.IsCancellationRequested)
            {
                var now = DateTime.Now;
                var list = taskOption.Triggers
                    .Select(k => (k, k.GetNextExecutionTime(now)))
                    .OrderBy(k => k.Item2)
#if DEBUG
                    .ToList() // DEBUG: 获取下次执行的时间列表
#endif
                    ;

                var (trigger, nextTime) = list
                   .First(); // 获取最近的下次执行时间

                logger.LogDebug("Next time for task {0}: {1}", taskOption.Name, nextTime);
                if (!Sleep(nextTime - now, cts)) break;

                Task.Run(() => Execute(trigger, nextTime), cts.Token);
            }

            void Execute(Trigger t, DateTime triggerTime)
            {
                try
                {
                    logger.LogDebug("Executing task {0} at {1}", taskOption.Name, triggerTime);
                    taskOption.Handler?.Invoke(new TaskContext
                    {
                        TaskId = taskOption.Id,
                        TaskName = taskOption.Name,
                        Trigger = t,
                        TriggerTime = triggerTime,
                        IsStartupTrigger = t == null,
                        Triggers = new ReadOnlyCollection<Trigger>(taskOption.Triggers),
                        LastTriggerTimes = taskOption.Triggers.Select(k => k.GetLastExecutionTime(triggerTime)).ToArray(),
                        NextTriggerTimes = taskOption.Triggers.Select(k => k.GetNextExecutionTime(triggerTime)).ToArray()
                    }, cts.Token);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Error occurs while executing task {0}", taskOption.Name);
                }
            }
        }

        private static bool Sleep(TimeSpan timeSpan, CancellationTokenSource cts)
        {
            try
            {
                if (timeSpan <= TimeSpan.Zero)
                    return !cts.IsCancellationRequested;

                Task.Delay(timeSpan, cts.Token).Wait();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await CancelAllAsync();
        }
    }
}
