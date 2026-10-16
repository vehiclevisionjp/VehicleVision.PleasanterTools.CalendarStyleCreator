using System.Globalization;

namespace VehicleVision.PleasanterTools.CalendarStyleCreator;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("en-US");
        using var cancel = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancel.Cancel(); };
        try
        {
            var options = Options.Parse(args);
            if (options.Action == "help")
            {
                RuntimeLog.WriteLine($"""
                    CalendarStyleCreator {typeof(Program).Assembly.GetName().Version!.ToString(3)} - Pleasanter calendar holiday style generator
                    Usage: CalendarStyleCreator <action> [options]
                      generate                    Write ExtendedStyles/<name>.css (all sites) to /output, or under /p, or beside this tool
                      register                    Register the extended style through the Extensions API; requires /url
                    Fixed settings: App_Data/Parameters/Holidays.json, Style.json, Placement.json beside this tool (see README). Command-line options override them.
                    /parameters <folder>        Use another Parameters folder
                    Options (same style as Implem.CodeDefiner: start with /, a value follows):
                      /calendar-id <id,...>       Google Calendar IDs, comma separated (public calendars; no API key)
                                                  Default: {GoogleCalendarHolidaySource.JapaneseHolidays}; "none" = CSV only
                      /ical-url <url,...>         iCal (.ics) https URLs, comma separated. Alone, it replaces the default calendar.
                                                  Auth for all URLs: env {Options.IcalBearerTokenVariable}, or {Options.IcalBasicUserVariable} + {Options.IcalBasicPasswordVariable}
                      /csv <file>                 Extra holidays from a CSV file ("date,name" per line); overrides Google on the same date
                      /from <year> /to <year>     Output year range for holidays and weekends (default: this year and next)
                      /holiday-bg /saturday-bg /holiday-text /saturday-text <color>   Colors (#rrggbb or name)
                      /first-day-of-week <0-6>    First day of the week of the standard calendar: 0=Sunday .. 6=Saturday (default: 1, as in General.json)
                      /prefix <text>              Name prefix of the generated styles (default: {Options.DefaultPrefix}). Styles: <prefix>core and <prefix><year>
                      /prune                      Delete managed styles (names starting with the prefix) that are no longer generated
                      /tenant-id <id>             register: tenant of the extensions (default: the tenant of the API key's user)
                      /site-id <id,...>           register: apply to these sites only (default: all sites)
                      /p <folder>                 generate: Pleasanter folder (Implem.Pleasanter); same as Implem.CodeDefiner
                                                  Default: Implem.Pleasanter beside this tool
                      /output <folder>            generate: ExtendedStyles folder (overrides /p)
                      /url <base url>             register: Pleasanter base URL (API key: env {Options.PleasanterApiKeyVariable})
                    """);
                return 0;
            }
            // 出力する年の範囲。取得元（Google・CSV）の休日と、標準カレンダー用の土日の指定範囲を同じにする。
            var thisYear = DateTime.Now.Year;
            var fromYear = options.FromYear ?? thisYear;
            var toYear = options.ToYear ?? thisYear + 1;
            var holidays = (await LoadHolidays(options, fromYear, toYear, cancel.Token)).Where(h => h.Date.Year >= fromYear && h.Date.Year <= toYear).ToList();
            var set = StyleSet.Create(options.Prefix, CssGenerator.Generate(holidays, options.Theme, fromYear, toYear, options.FirstDayOfWeek), options.SiteIds, options.Prune);
            IStyleSink sink = options.Action == "generate"
                ? new FileStyleSink(options.Output!)
                : new ApiStyleSink(options.PleasanterUrl!, Environment.GetEnvironmentVariable(Options.PleasanterApiKeyVariable) ?? throw new UserError($"Set {Options.PleasanterApiKeyVariable}."), options.TenantId);
            await sink.WriteAsync(set, cancel.Token);
            RuntimeLog.WriteLine($"Done. {holidays.Select(h => h.Date).Distinct().Count()} holiday(s).");
            return 0;
        }
        catch (UserError e)
        {
            RuntimeLog.WriteError("ERROR: " + e.Message);
            return 2;
        }
        catch (OperationCanceledException)
        {
            RuntimeLog.WriteError("Canceled.");
            return 130;
        }
    }

    private static async Task<IReadOnlyList<Holiday>> LoadHolidays(Options options, int fromYear, int toYear, CancellationToken cancel)
    {
        var sources = new List<IHolidaySource>();
        foreach (var calendarId in options.CalendarIds)
            sources.Add(new GoogleCalendarHolidaySource(calendarId, fromYear, toYear));
        foreach (var endpoint in options.IcalUrls)
        {
            var auth = IcalAuth.FromEnvironment(Environment.GetEnvironmentVariable, endpoint);
            sources.Add(new IcalHolidaySource(endpoint.Url, auth.IsEmpty ? null : auth, fromYear, toYear));
        }
        // 同じ日付が複数の取得元にある場合は後勝ち（CSV で Google の名称や追加休日を上書きできる）。
        foreach (var csv in options.CsvPaths) sources.Add(new CsvHolidaySource(csv));
        var holidays = new List<Holiday>();
        foreach (var source in sources) holidays.AddRange(await source.GetHolidaysAsync(cancel));
        return holidays;
    }
}
