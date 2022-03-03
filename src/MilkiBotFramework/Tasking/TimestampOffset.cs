namespace MilkiBotFramework.Tasking
{
    /// <summary>
    /// 时间戳偏移
    /// <para>
    /// 由于.NET内置的<see cref="DateTime"/>以及<see cref="TimeSpan"/>皆不能达成实现目标，特此设计此结构。
    /// 其中<see cref="DateTime"/>的年份，月份等不能为 0，
    /// 其次<see cref="TimeSpan"/>不能精确表示年、月（考虑每月天数、闰年等）。
    /// </para>
    /// <example>
    /// 单位从大至小寻找最后为 0 的单位，以其作为偏移量单位。
    /// 例：
    /// <code>new TimestampOffset(0,0,4,5,16,0)</code>
    /// 则表示基于月份的偏移，表示每月 4 号当天的 5点16分
    /// </example>
    /// </summary>
    public readonly struct TimestampOffset
    {
        /// <summary>
        /// 创建一个基于周的时间偏移量
        /// </summary>
        /// <param name="day">星期数，0表示周日</param>
        /// <param name="hour">小时</param>
        /// <param name="minute">分钟</param>
        /// <param name="second">秒</param>
        public TimestampOffset(int day,
            int hour, int minute, int second)
        {
            IsWeek = true;
            Year = 0;
            Month = 0;
            Day = day;
            Hour = hour;
            Minute = minute;
            Second = second;
        }

        /// <summary>
        /// 创建一个基于日期的时间偏移量
        /// </summary>
        /// <param name="year">年份</param>
        /// <param name="month">月份</param>
        /// <param name="day">日期</param>
        /// <param name="hour">小时</param>
        /// <param name="minute">分钟</param>
        /// <param name="second">秒</param>
        public TimestampOffset(int year, int month, int day,
            int hour, int minute, int second)
        {
            IsWeek = false;
            Year = year;
            Month = month;
            Day = day;
            Hour = hour;
            Minute = minute;
            Second = second;
        }

        /// <summary>
        /// 是否表示为周偏移量
        /// </summary>
        public readonly bool IsWeek { get; }

        /// <summary>
        /// 年份
        /// </summary>
        public readonly int Year { get; }

        /// <summary>
        /// 月份
        /// </summary>
        public readonly int Month { get; }

        /// <summary>
        /// 天。可表示星期数或者日期
        /// </summary>
        public readonly int Day { get; }

        /// <summary>
        /// 小时
        /// </summary>
        public readonly int Hour { get; }

        /// <summary>
        /// 分钟
        /// </summary>
        public readonly int Minute { get; }

        /// <summary>
        /// 秒
        /// </summary>
        public readonly int Second { get; }

        /// <summary>
        /// 转化为等价<see cref="TimeSpan"/>
        /// </summary>
        /// <exception cref="ArgumentException">当月份或者年份不为0时，无法转化。
        /// 由于<see cref="TimeSpan"/>仅有数量概念，且无法定义闰年等，因此无法等价转化。</exception>
        /// <returns></returns>
        public TimeSpan ToTimeSpan()
        {
            if (Year != 0 || Month != 0)
                throw new ArgumentException("Can not to TimeSpan accurately if year or month exists.");
            return new TimeSpan(Day, Hour, Minute, Second);
        }

        /// <summary>
        /// 创建以分钟为单位的时间偏移
        /// </summary>
        public static TimestampOffset CreateAsMinuteOffset(int second)
        {
            return new TimestampOffset(0, 0, 0, 0, 0, second);
        }

        /// <summary>
        /// 创建以小时为单位的时间偏移
        /// </summary>
        public static TimestampOffset CreateAsHourOffset(int minute, int second = 0)
        {
            return new TimestampOffset(0, 0, 0, 0, minute, second);
        }

        /// <summary>
        /// 创建以日为单位的时间偏移
        /// </summary>
        public static TimestampOffset CreateAsDayOffset(int hour, int minute = 0, int second = 0)
        {
            return new TimestampOffset(0, 0, 0, hour, minute, second);
        }

        /// <summary>
        /// 创建以星期为单位的时间偏移
        /// </summary>
        public static TimestampOffset CreateAsWeekOffset(int day, int hour = 0, int minute = 0, int second = 0)
        {
            return new TimestampOffset(day, hour, minute, second);
        }

        /// <summary>
        /// 创建以月为单位的时间偏移
        /// </summary>
        public static TimestampOffset CreateAsMonthOffset(int day, int hour = 0, int minute = 0, int second = 0)
        {
            return new TimestampOffset(0, 0, day, hour, minute, second);
        }

        /// <summary>
        /// 创建以年为单位的时间偏移
        /// </summary>
        public static TimestampOffset CreateAsYearOffset(int month, int day = 0, int hour = 0, int minute = 0, int second = 0)
        {
            return new TimestampOffset(0, month, day, hour, minute, second);
        }
    }
}