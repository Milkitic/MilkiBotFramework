using System.Collections.Concurrent;

namespace MilkiBotFramework.Event
{
    /// <summary>
    /// EventBus implement
    /// </summary>
    public sealed class EventBus
    {
        // payloadtype - list of action<T>s, where T is payload type
        private readonly ConcurrentDictionary<Type, Dictionary<Guid, Func<IEventBusEvent, Task>>> _subscriptions = new();

        /// <summary>
        /// awaitable event.Invoke()
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="payload"></param>
        /// <param name="options"></param>
        /// <param name="smt">是否同步多线程执行，否则依次执行</param>
        /// <returns></returns>
        public async Task PublishAsync<T>(T payload, PublishOptions options = default, bool smt = true)
            where T : class, IEventBusEvent
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            var type = payload.GetType();
            if (_subscriptions.TryGetValue(type, out var subscription))
            {
                if (smt) // 多线程执行
                {
                    // ParallelQuery，可以自动管理线程
                    var query = subscription
                        .AsParallel()
                        .Select(async (func, _) =>
                        {
                            await func.Value(payload);
                        });

                    await Task.WhenAll(query);
                }
                else // 依次执行，模拟原生event.Invoke()
                {
                    foreach (var func in subscription.Values)
                    {
                        await func(payload);
                    }
                    //for (var i = 0; i < funcList.Values.Count; i++)
                    //{
                    //    await funcList[i](payload);
                    //}
                }
            }
        }

        public async void StartPublishTask<T>(T payload, PublishOptions options = default, bool smt = true)
            where T : class, IEventBusEvent
        {
            await PublishAsync(payload, options, smt);
        }

        /// <summary>
        /// event+=...
        /// <remarks>不要重复订阅！
        /// 订阅到相同类型的 action 会被同时触发</remarks>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="action"></param>
        /// <param name="guid"></param>
        public void Subscribe<T>(Action<T> action, Guid guid) where T : IEventBusEvent
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            if (guid == default)
                guid = Guid.NewGuid();

            var type = typeof(T);
            var list = _subscriptions.GetOrAdd(type, _ => new());

            list.Add(guid, e =>
            {
                action((T)e);
                return Task.CompletedTask;
            });
        }

        public void Unsubscribe<T>(Guid guid) where T : IEventBusEvent
        {
            var type = typeof(T);
            var list = _subscriptions.GetOrAdd(type, _ => new());

            list.Remove(guid, out _);
        }

        /// <summary>
        /// event+=...（安全的async anonymous method）
        /// <remarks>不要重复订阅！
        /// 订阅到相同类型的 action 会被同时触发</remarks>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="func"></param>
        /// <param name="guid"></param>
        public void Subscribe<T>(Func<T, Task> func, Guid guid = default) where T : IEventBusEvent
        {
            if (func == null)
                throw new ArgumentNullException(nameof(func));
            if (guid == default)
                guid = Guid.NewGuid();

            var type = typeof(T);
            var list = _subscriptions.GetOrAdd(type, _ => new());

            list.Add(guid, async e => await func((T)e).ConfigureAwait(false));
        }

        public struct PublishOptions
        {
            public PublishOptions(string token, bool isPrivate = false)
            {
                Token = token;
                IsPrivate = isPrivate;
            }

            /// <summary>
            /// 事件引发所有者，null为服务器
            /// </summary>
            public string Token { get; set; }

            /// <summary>
            /// 是否为私有通知
            /// </summary>
            public bool IsPrivate { get; set; }
        }
    }
}
