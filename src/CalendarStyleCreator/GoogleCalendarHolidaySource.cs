namespace VehicleVision.PleasanterTools.CalendarStyleCreator;

/// <summary>
/// Google カレンダー（公開カレンダー ID + API キー）から休日を取得する。
/// TODO: Calendar API v3 の events.list（終日予定の start.date、singleEvents、ページング）を一次情報で確認して実装する。
/// </summary>
public sealed class GoogleCalendarHolidaySource(string calendarId, string apiKey, int fromYear, int toYear) : IHolidaySource
{
    public Task<IReadOnlyList<Holiday>> GetHolidaysAsync(CancellationToken cancel) =>
        throw new UserError("Google Calendar source is not implemented yet. Use /csv.");

    public string CalendarId { get; } = calendarId;
    public string ApiKey { get; } = apiKey;
    public int FromYear { get; } = fromYear;
    public int ToYear { get; } = toYear;
}
