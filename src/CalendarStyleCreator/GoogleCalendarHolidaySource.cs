using System.Globalization;
using System.Text.RegularExpressions;

namespace VehicleVision.PleasanterTools.CalendarStyleCreator;

/// <summary>
/// Google カレンダーの公開 iCal（https://calendar.google.com/calendar/ical/{ID}/public/basic.ics）から休日を取得する。
/// 公開カレンダーのみ対象で、API キー・OAuth は不要。終日予定の DTSTART を日付として使う（RFC 5545 の折り返し行は連結する）。
/// 日本の祝日: ja.japanese.official#holiday@group.v.calendar.google.com（実取得で確認、2026-10-16）。
/// </summary>
public sealed class GoogleCalendarHolidaySource(string calendarId, int fromYear, int toYear, HttpClient? client = null) : IHolidaySource
{
    public const string JapaneseHolidays = "ja.japanese.official#holiday@group.v.calendar.google.com";

    public async Task<IReadOnlyList<Holiday>> GetHolidaysAsync(CancellationToken cancel)
    {
        var url = $"https://calendar.google.com/calendar/ical/{Uri.EscapeDataString(calendarId)}/public/basic.ics";
        using var owned = client == null ? new HttpClient { Timeout = TimeSpan.FromSeconds(30) } : null;
        using var response = await (client ?? owned!).GetAsync(url, cancel);
        if (!response.IsSuccessStatusCode) throw new UserError($"Google Calendar returned HTTP {(int)response.StatusCode}. The calendar must be public.");
        return Parse(await response.Content.ReadAsStringAsync(cancel), fromYear, toYear);
    }

    public static IReadOnlyList<Holiday> Parse(string ics, int fromYear, int toYear)
    {
        var unfolded = Regex.Replace(ics, @"\r?\n[ \t]", "");
        var holidays = new List<Holiday>();
        foreach (Match block in Regex.Matches(unfolded, @"BEGIN:VEVENT\r?\n(.*?)END:VEVENT", RegexOptions.Singleline))
        {
            var body = block.Groups[1].Value;
            if (Property(body, "STATUS").Equals("CANCELLED", StringComparison.OrdinalIgnoreCase)) continue;
            var start = Regex.Match(Property(body, "DTSTART"), @"^(\d{4})(\d{2})(\d{2})");
            if (!start.Success) continue;
            var date = new DateOnly(int.Parse(start.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(start.Groups[2].Value, CultureInfo.InvariantCulture), int.Parse(start.Groups[3].Value, CultureInfo.InvariantCulture));
            if (date.Year < fromYear || date.Year > toYear) continue;
            holidays.Add(new Holiday(date, Unescape(Property(body, "SUMMARY"))));
        }
        return holidays;
    }

    private static string Property(string block, string name)
    {
        var match = Regex.Match(block, "^" + name + @"(?:;[^:\r\n]*)?:(.*?)\r?$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : "";
    }

    private static string Unescape(string value) => Regex.Replace(value, @"\\([nN,;\\])", m => m.Groups[1].Value is "n" or "N" ? " " : m.Groups[1].Value);
}
