using System;

namespace MilkiBotFramework.Tasking
{
    /// <summary>
    /// 时间/日期相关扩展方法
    /// </summary>
    public static class DateTimeExtensions
    {
        /// <summary>
        /// 将指定时间转化为以年为单位的偏移
        /// </summary>
        /// <param name="time">指定的时间</param>
        /// <returns></returns>
        public static TimestampOffset ToYearOffset(this DateTime time)
        {
            return new TimestampOffset(0, time.Month, time.Day, time.Hour, time.Minute, time.Second);
        }

        /// <summary>
        /// 将指定时间转化为以月为单位的偏移
        /// </summary>
        /// <param name="time">指定的时间</param>
        /// <returns></returns>
        public static TimestampOffset ToMonthOffset(this DateTime time)
        {
            return new TimestampOffset(0, 0, time.Day, time.Hour, time.Minute, time.Second);
        }

        /// <summary>
        /// 将指定时间转化为以周为单位的偏移
        /// </summary>
        /// <param name="time">指定的时间</param>
        /// <returns></returns>
        public static TimestampOffset ToWeekOffset(this DateTime time)
        {
            return new TimestampOffset(time.Day, time.Hour, time.Minute, time.Second);
        }

        /// <summary>
        /// 将指定时间转化为以天为单位的偏移
        /// </summary>
        /// <param name="time">指定的时间</param>
        /// <returns></returns>
        public static TimestampOffset ToDayOffset(this DateTime time)
        {
            return new TimestampOffset(0, 0, 0, time.Hour, time.Minute, time.Second);
        }

        /// <summary>
        /// 将指定时间转化为以小时为单位的偏移
        /// </summary>
        /// <param name="time">指定的时间</param>
        /// <returns></returns>
        public static TimestampOffset ToHourOffset(this DateTime time)
        {
            return new TimestampOffset(0, 0, 0, 0, time.Minute, time.Second);
        }

        /// <summary>
        /// 将指定时间转化为以分钟为单位的偏移
        /// </summary>
        /// <param name="time">指定的时间</param>
        /// <returns></returns>
        public static TimestampOffset ToMinuteOffset(this DateTime time)
        {
            return new TimestampOffset(0, 0, 0, 0, 0, time.Second);
        }

        /// <summary>
        /// 将指定<see cref="TimeSpan"/>转化为等效偏移
        /// </summary>
        /// <param name="time">指定的时间</param>
        /// <returns></returns>
        public static TimestampOffset ToOffset(this TimeSpan time)
        {
            return new TimestampOffset(0, 0, time.Days, time.Hours, time.Minutes, time.Seconds);
        }

        /// <summary>
        /// 计算任务触发时间
        /// </summary>
        /// <param name="dateTime">指定的时间</param>
        /// <param name="trigger">指定的触发器</param>
        /// <param name="isNext">指定是否为下一次执行时间。若为false，则为上次执行时间。</param>
        /// <returns></returns>
        public static DateTime ComputeExecutionTime(this DateTime dateTime, Trigger trigger, bool isNext)
        {
            return ComputeExecutionTime(dateTime, trigger.TimestampOffset, trigger.TimestampUnit, isNext);
        }

        /// <summary>
        /// 计算任务触发时间
        /// </summary>
        /// <param name="dateTime">指定的时间</param>
        /// <param name="offset">指定的<see cref="TimestampOffset"/>偏移量</param>
        /// <param name="unit">指定的时间单位</param>
        /// <param name="isNext">指定是否为下一次执行时间。若为false，则为上次执行时间。</param>
        /// <returns></returns>
        public static DateTime ComputeExecutionTime(this DateTime dateTime,
            TimestampOffset offset,
            TriggerTimestampUnit unit,
            bool isNext)
        {
            if (unit == TriggerTimestampUnit.Week)
            {
                var targetTs = offset.ToTimeSpan();
                var currentTs = new TimeSpan((int)dateTime.DayOfWeek, dateTime.Hour, dateTime.Minute, dateTime.Second,
                    dateTime.Millisecond); // 利用TimeSpan计算时间差
                var addition = targetTs - currentTs;
                if (addition.Ticks <= 0) // 如果小于0，再加一周
                {
                    addition = addition.Add(TimeSpan.FromDays(7));
                }

                var nextExecutionTime = dateTime.Add(addition);
                return isNext ? nextExecutionTime : nextExecutionTime.AddDays(-7);
            }

            if (unit == TriggerTimestampUnit.Interval) // todo: 这样实现会有点小问题，用到再优化
            {
                var targetTs = offset.ToTimeSpan();
                var nextExecutionTime = dateTime.Add(targetTs);
                return isNext ? nextExecutionTime : dateTime;
            }

            DateTime nextTime;
            switch (unit)
            {
                case TriggerTimestampUnit.Year:
                    nextTime = new DateTime(dateTime.Year, offset.Month, offset.Day,
                        offset.Hour, offset.Minute, offset.Second); // 传入当前的年份，以及偏移量的其他值
                    if (nextTime <= dateTime) nextTime = nextTime.AddYears(1); // 如果小于0，再加一年
                    if (!isNext) nextTime = nextTime.AddYears(-1);
                    break;
                case TriggerTimestampUnit.Month:
                    nextTime = new DateTime(dateTime.Year, dateTime.Month, offset.Day,
                        offset.Hour, offset.Minute, offset.Second); // 传入当前的年份、月份，以及偏移量的其他值
                    if (nextTime <= dateTime) nextTime = nextTime.AddMonths(1); // 如果小于0，再加一月
                    if (!isNext) nextTime = nextTime.AddMonths(-1);
                    break;
                case TriggerTimestampUnit.Day:
                    nextTime = new DateTime(dateTime.Year, dateTime.Month, dateTime.Day,
                        offset.Hour, offset.Minute, offset.Second); // 传入当前的年份、月份、日期，以及偏移量的其他值
                    if (nextTime <= dateTime) nextTime = nextTime.AddDays(1); // 如果小于0，再加一天
                    if (!isNext) nextTime = nextTime.AddDays(-1);
                    break;
                case TriggerTimestampUnit.Hour:
                    nextTime = new DateTime(dateTime.Year, dateTime.Month, dateTime.Day,
                        dateTime.Hour, offset.Minute, offset.Second); // 传入当前的年份、月份、日期、小时，以及偏移量的其他值
                    if (nextTime <= dateTime) nextTime = nextTime.AddHours(1); // 如果小于0，再加一小时
                    if (!isNext) nextTime = nextTime.AddHours(-1);
                    break;
                case TriggerTimestampUnit.Minute:
                    nextTime = new DateTime(dateTime.Year, dateTime.Month, dateTime.Day,
                        dateTime.Hour, dateTime.Minute, offset.Second); // 传入当前的年份、月份、日期、小时、分钟，以及偏移量的秒数
                    if (nextTime <= dateTime) nextTime = nextTime.AddMinutes(1); // 如果小于0，再加一分钟
                    if (!isNext) nextTime = nextTime.AddMinutes(-1);
                    break;
                case TriggerTimestampUnit.Absolute:
                    nextTime = new DateTime(dateTime.Year, dateTime.Month, dateTime.Day,
                        dateTime.Hour, dateTime.Minute, dateTime.Second); // 直接转换
                    break;
                case TriggerTimestampUnit.Week:
                case TriggerTimestampUnit.Interval:
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return nextTime;
        }
    }
}