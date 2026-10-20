namespace CinemaShowtimes.Api.Application;

internal static class CinemaTimeZone
{
    internal static readonly TimeZoneInfo Value = Resolve();

    private static TimeZoneInfo Resolve()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Kyiv");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("FLE Standard Time");
        }
    }
}
