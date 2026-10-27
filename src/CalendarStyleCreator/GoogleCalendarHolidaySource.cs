namespace VehicleVision.PleasanterTools.CalendarStyleCreator;

/// <summary>
/// Google カレンダーの公開 iCal（https://calendar.google.com/calendar/ical/{ID}/public/basic.ics）から休日を取得する。
/// 公開カレンダーのみ対象で、API キー・OAuth は不要。非公開カレンダーは「非公開 iCal 形式のアドレス」を /ical-url で指定する。
/// 日本の祝日: ja.japanese.official#holiday@group.v.calendar.google.com（実取得で確認、2026-10-16）。
/// </summary>
public sealed class GoogleCalendarHolidaySource(string calendarId, int fromYear, int toYear, HttpClient? client = null) : IHolidaySource
{
    public const string JapaneseHolidays = "ja.japanese.official#holiday@group.v.calendar.google.com";

    public static string Url(string calendarId) => $"https://calendar.google.com/calendar/ical/{Uri.EscapeDataString(calendarId)}/public/basic.ics";

    public async Task<IReadOnlyList<Holiday>> GetHolidaysAsync(CancellationToken cancel)
    {
        try { return await new IcalHolidaySource(Url(calendarId), null, fromYear, toYear, client).GetHolidaysAsync(cancel); }
        catch (UserError e) when (e.Message.Contains("HTTP 404", StringComparison.Ordinal)) { throw new UserError("Google Calendar returned HTTP 404. The calendar must be public."); }
    }
}
