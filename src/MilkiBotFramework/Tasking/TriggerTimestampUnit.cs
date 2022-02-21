namespace MilkiBotFramework.Tasking
{
    /// <summary>
    /// 表示<see cref="Trigger"/>的时间单位
    /// </summary>
    public enum TriggerTimestampUnit
    {
        /// <summary>
        /// 表示以年为单位
        /// <para>Start from 1, timestamp as month of year</para>
        /// </summary>
        Year,

        /// <summary>
        /// 表示以月为单位
        /// <para>Start from 1, timestamp as day of month</para>
        /// </summary>
        Month,

        /// <summary>
        /// 表示以周为单位
        /// <para>Start from 1, timestamp as day of week</para>
        /// </summary>
        Week,

        /// <summary>
        /// 表示以天为单位
        /// <para>Start from 1, timestamp as hour</para>
        /// </summary>
        Day,

        /// <summary>
        /// 表示以小时为单位
        /// <para>Start from 0, timestamp as minute</para>
        /// </summary>
        Hour,

        /// <summary>
        /// 表示以分为单位
        /// <para>Start from 0, timestamp as second</para>
        /// </summary>
        Minute,

        /// <summary>
        /// 表示绝对时间
        /// <para>Directly convert from unix timestamp</para>
        /// </summary>
        Absolute,

        /// <summary>
        /// 表示固定间隔
        /// <para>Directly convert from unix timestamp</para>
        /// </summary>
        Interval
    }
}