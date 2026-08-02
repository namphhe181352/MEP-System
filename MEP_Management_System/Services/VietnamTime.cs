using System;
public static class VietnamTime {
    public static readonly TimeZoneInfo Tz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Tz);
    public static DateTime TodayStart => Now.Date;
    public static DateTime TodayEnd => TodayStart.AddDays(1);
    public static DateTime DaysAgo(int days) => TodayStart.AddDays(-days);
}
