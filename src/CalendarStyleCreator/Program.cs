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
                    Usage: CalendarStyleCreator <action> (/csv <file> | /calendar-id <id>) [options]
                      generate                    Write an extended style file (ExtendedStyles/<name>.json); requires /output
                      register                    Register the extended style through the Extensions API; requires /url
                    Options (same style as Implem.CodeDefiner: start with /, a value follows):
                      /csv <file>                 Holidays from a CSV file ("date,name" per line)
                      /calendar-id <id>           Holidays from a Google Calendar (API key: env {Options.GoogleApiKeyVariable})
                      /from <year> /to <year>     Year range for the Google Calendar (default: this year and next)
                      /name <name>                Extended style name (default: {Options.DefaultName})
                      /site-id <id,...>           Apply to these sites only (default: all sites)
                      /output <folder>            generate: ExtendedStyles folder
                      /url <base url>             register: Pleasanter base URL (API key: env {Options.PleasanterApiKeyVariable})
                    """);
                return 0;
            }
            var holidays = await LoadHolidays(options, cancel.Token);
            var style = new StyleDefinition(options.Name, "Holiday highlight for the calendar view (generated)", options.SiteIds, CssGenerator.Generate(holidays));
            IStyleSink sink = options.Action == "generate"
                ? new FileStyleSink(options.Output!)
                : new ApiStyleSink(options.PleasanterUrl!, Environment.GetEnvironmentVariable(Options.PleasanterApiKeyVariable) ?? throw new UserError($"Set {Options.PleasanterApiKeyVariable}."));
            await sink.WriteAsync(style, cancel.Token);
            RuntimeLog.WriteLine($"Done. {holidays.Count} holiday(s).");
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

    private static async Task<IReadOnlyList<Holiday>> LoadHolidays(Options options, CancellationToken cancel)
    {
        var sources = new List<IHolidaySource>();
        if (options.CalendarId != null)
        {
            var thisYear = DateTime.Now.Year;
            sources.Add(new GoogleCalendarHolidaySource(
                options.CalendarId,
                Environment.GetEnvironmentVariable(Options.GoogleApiKeyVariable) ?? throw new UserError($"Set {Options.GoogleApiKeyVariable}."),
                options.FromYear ?? thisYear,
                options.ToYear ?? thisYear + 1));
        }
        // 同じ日付が複数の取得元にある場合は後勝ち（CSV で Google の名称や追加休日を上書きできる）。
        if (options.CsvPath != null) sources.Add(new CsvHolidaySource(options.CsvPath));
        var holidays = new List<Holiday>();
        foreach (var source in sources) holidays.AddRange(await source.GetHolidaysAsync(cancel));
        return holidays;
    }
}
