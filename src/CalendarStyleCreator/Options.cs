using System.Globalization;

namespace VehicleVision.PleasanterTools.CalendarStyleCreator;

public sealed record Options(string Action, string? CsvPath, IReadOnlyList<string> CalendarIds, IReadOnlyList<string> IcalUrls, int? FromYear, int? ToYear, string Name, IReadOnlyList<long> SiteIds, string? Output, string? PleasanterUrl, CalendarTheme Theme)
{
    public const string DefaultName = "CalendarHolidays";
    // Pleasanter の API キーはコマンドラインへ置かず環境変数から読む（履歴・プロセス一覧への露出を避ける）。
    // 認証付き iCal（/ical-url 全体に適用）。Bearer と Basic はどちらか一方。
    public const string IcalBearerTokenVariable = "CALENDARSTYLE_ICAL_BEARER_TOKEN";
    public const string IcalBasicUserVariable = "CALENDARSTYLE_ICAL_BASIC_USER";
    public const string IcalBasicPasswordVariable = "CALENDARSTYLE_ICAL_BASIC_PASSWORD";
    public const string PleasanterApiKeyVariable = "CALENDARSTYLE_PLEASANTER_API_KEY";

    private static readonly string[] ValueOptions = ["csv", "calendar-id", "ical-url", "from", "to", "name", "site-id", "output", "url", "holiday-bg", "saturday-bg", "holiday-text", "saturday-text"];

    public static Options Parse(string[] args)
    {
        if (args.Length == 0 || args[0] is "help" or "/?") return new("help", null, [], [], null, null, DefaultName, [], null, null, new());
        var action = args[0];
        if (action is not ("generate" or "register")) throw new UserError("Unknown action. Use generate or register.");
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 1; i < args.Length; i++)
        {
            if (args[i].Length < 2 || args[i][0] != '/') throw new UserError("Options start with /. Use help for usage.");
            var name = args[i][1..];
            if (!ValueOptions.Contains(name)) throw new UserError("Unknown option. Use help for usage.");
            // 値は次の引数。パス（csv / output）は / で始まる値（Linux の絶対パス）も許す。
            if (++i >= args.Length || (name is not ("csv" or "output") && args[i].StartsWith('/')) || !values.TryAdd(name, args[i])) throw new UserError("Missing or duplicate option value.");
        }
        string? V(string key) => values.GetValueOrDefault(key);
        int? Year(string key)
        {
            if (V(key) == null) return null;
            if (!int.TryParse(V(key), NumberStyles.None, CultureInfo.InvariantCulture, out var year) || year is < 1900 or > 2100) throw new UserError("Year option out of range.");
            return year;
        }
        var sites = (V("site-id") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => long.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0 ? id : throw new UserError("Site ID must be a positive number."))
            .Distinct().ToList();
        // 既定は日本の祝日カレンダー。複数はカンマ区切り。none で Google カレンダーを使わない（CSV のみ）。
        var calendars = V("calendar-id") switch
        {
            null => [GoogleCalendarHolidaySource.JapaneseHolidays],
            "none" => [],
            var list => list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal).ToList(),
        };
        var icalUrls = (V("ical-url") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal).ToList();
        icalUrls.ForEach(IcalHolidaySource.ValidateUrl);
        // /ical-url だけを指定したときは、既定の日本の祝日は足さない（指定した取得元だけを使う）。
        if (V("calendar-id") == null && icalUrls.Count > 0) calendars = [];
        var defaults = new CalendarTheme();
        var options = new Options(action, V("csv"), calendars, icalUrls, Year("from"), Year("to"), V("name") ?? DefaultName, sites, V("output"), V("url"),
            new CalendarTheme(V("holiday-bg") ?? defaults.HolidayBackground, V("saturday-bg") ?? defaults.SaturdayBackground, V("holiday-text") ?? defaults.HolidayText, V("saturday-text") ?? defaults.SaturdayText).Validate());
        if (options.CsvPath == null && options.CalendarIds.Count == 0 && options.IcalUrls.Count == 0) throw new UserError("Specify /calendar-id, /ical-url or /csv.");
        if (options.FromYear > options.ToYear) throw new UserError("/from must not be later than /to.");
        if (action == "generate" && options.Output == null) throw new UserError("generate requires /output.");
        if (action == "register" && options.PleasanterUrl == null) throw new UserError("register requires /url.");
        return options;
    }
}
