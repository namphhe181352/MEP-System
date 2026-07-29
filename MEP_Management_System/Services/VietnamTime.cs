using System;
public static class VietnamTime {
    private static readonly TimeZoneInfo _tz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _tz);
    public static DateTime TodayStart => Now.Date;
    public static DateTime TodayEnd => TodayStart.AddDays(1);
    public static DateTime DaysAgo(int days) => TodayStart.AddDays(-days);
}
