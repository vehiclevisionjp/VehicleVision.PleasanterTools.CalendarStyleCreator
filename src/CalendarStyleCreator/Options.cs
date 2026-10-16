using System.Globalization;

namespace VehicleVision.PleasanterTools.CalendarStyleCreator;

public sealed record Options(string Action, IReadOnlyList<string> CsvPaths, IReadOnlyList<string> CalendarIds, IReadOnlyList<IcalEndpoint> IcalUrls, int? FromYear, int? ToYear, string Prefix, bool Prune, IReadOnlyList<long> SiteIds, int? TenantId, string? Output, string? PleasanterUrl, CalendarTheme Theme)
{
    // 拡張スタイルの名前の接頭辞。この接頭辞で始まるものだけをこのツールの管理対象とする（共通部は {prefix}core、年ごとは {prefix}{yyyy}）。
    public const string DefaultPrefix = "vvcsc_";
    // 認証付き iCal の既定の環境変数（認証の参照を持たない /ical-url 全体に適用）。Bearer と Basic はどちらか一方。
    // 資格情報はコマンドラインにも設定ファイルにも置かず環境変数から読む（履歴・プロセス一覧・ファイルへの露出を避ける）。
    public const string IcalBearerTokenVariable = "CALENDARSTYLE_ICAL_BEARER_TOKEN";
    public const string IcalBasicUserVariable = "CALENDARSTYLE_ICAL_BASIC_USER";
    public const string IcalBasicPasswordVariable = "CALENDARSTYLE_ICAL_BASIC_PASSWORD";
    public const string PleasanterApiKeyVariable = "CALENDARSTYLE_PLEASANTER_API_KEY";

    private static readonly string[] ValueOptions = ["config", "p", "csv", "calendar-id", "ical-url", "from", "to", "prefix", "site-id", "tenant-id", "output", "url", "holiday-bg", "saturday-bg", "holiday-text", "saturday-text"];
    private static readonly string[] FlagOptions = ["prune"];
    private static readonly string[] PathOptions = ["config", "p", "csv", "output"];

    /// <summary>優先順位は コマンドライン &gt; 設定ファイル（/config）&gt; 既定値。</summary>
    public static Options Parse(string[] args)
    {
        if (args.Length == 0 || args[0] is "help" or "/?") return new("help", [], [], [], null, null, DefaultPrefix, false, [], null, null, null, new());
        var action = args[0];
        if (action is not ("generate" or "register")) throw new UserError("Unknown action. Use generate or register.");
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var flags = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 1; i < args.Length; i++)
        {
            if (args[i].Length < 2 || args[i][0] != '/') throw new UserError("Options start with /. Use help for usage.");
            var name = args[i][1..];
            if (FlagOptions.Contains(name)) { if (!flags.Add(name)) throw new UserError("Duplicate option."); continue; }
            if (!ValueOptions.Contains(name)) throw new UserError("Unknown option. Use help for usage.");
            // 値は次の引数。パス（config / csv / output）は / で始まる値（Linux の絶対パス）も許す。
            if (++i >= args.Length || (!PathOptions.Contains(name) && args[i].StartsWith('/')) || !values.TryAdd(name, args[i])) throw new UserError("Missing or duplicate option value.");
        }
        string? V(string key) => values.GetValueOrDefault(key);
        var config = V("config") is { } configPath ? ConfigFile.Load(configPath) : new ConfigFile();
        int? Year(string key, int? fromConfig)
        {
            if (V(key) == null) return fromConfig;
            if (!int.TryParse(V(key), NumberStyles.None, CultureInfo.InvariantCulture, out var year) || year is < 1900 or > 2100) throw new UserError("Year option out of range.");
            return year;
        }
        List<string> List(string? text) => (text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal).ToList();

        IReadOnlyList<long> sites = V("site-id") != null
            ? List(V("site-id")).Select(s => long.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0 ? id : throw new UserError("Site ID must be a positive number.")).Distinct().ToList()
            : config.SiteIds ?? [];
        // 取得元。指定がなければ日本の祝日カレンダー。none（設定ファイルでは空配列）で Google カレンダーを使わない。
        // iCal URL だけを指定したときは、既定の日本の祝日は足さない（指定した取得元だけを使う）。
        IReadOnlyList<string>? calendars = V("calendar-id") switch { null => config.Calendars, "none" => [], var list => List(list) };
        IReadOnlyList<IcalEndpoint> icalUrls = V("ical-url") != null ? List(V("ical-url")).Select(url => { IcalHolidaySource.ValidateUrl(url); return new IcalEndpoint(url); }).ToList() : config.IcalUrls ?? [];
        calendars ??= icalUrls.Count > 0 ? [] : [GoogleCalendarHolidaySource.JapaneseHolidays];
        IReadOnlyList<string> csv = V("csv") != null ? [V("csv")!] : config.Csv ?? [];

        var defaults = config.Theme ?? new CalendarTheme();
        var theme = new CalendarTheme(V("holiday-bg") ?? defaults.HolidayBackground, V("saturday-bg") ?? defaults.SaturdayBackground, V("holiday-text") ?? defaults.HolidayText, V("saturday-text") ?? defaults.SaturdayText).Validate();
        int? tenant = null;
        if (V("tenant-id") != null)
        {
            if (!int.TryParse(V("tenant-id"), NumberStyles.None, CultureInfo.InvariantCulture, out var t) || t < 1) throw new UserError("Tenant ID must be a positive number.");
            tenant = t;
        }
        tenant ??= config.TenantId;
        var output = V("output") ?? config.Output ?? (action == "generate" ? FindExtendedStylesFolder(V("p") ?? config.PleasanterPath) : null);
        var options = new Options(action, csv, calendars, icalUrls, Year("from", config.From), Year("to", config.To), ValidatePrefix(V("prefix") ?? config.Prefix ?? DefaultPrefix), flags.Contains("prune") || config.Prune == true, sites, tenant, output, V("url") ?? config.PleasanterUrl, theme);
        if (options.CsvPaths.Count == 0 && options.CalendarIds.Count == 0 && options.IcalUrls.Count == 0) throw new UserError("Specify /calendar-id, /ical-url, /csv or /config.");
        if (options.FromYear > options.ToYear) throw new UserError("/from must not be later than /to.");
        if (action == "generate" && options.Output == null) throw new UserError("generate requires /output, /p, or an Implem.Pleasanter folder beside this tool.");
        if (action == "register" && options.PleasanterUrl == null) throw new UserError("register requires /url (or pleasanterUrl in the config file).");
        if (action == "generate" && options.SiteIds.Count > 0) throw new UserError("generate (file placement) applies to all sites. Use register to limit the style to specific sites.");
        if (action == "generate" && options.TenantId != null) throw new UserError("/tenant-id applies to register only.");
        if (action == "register") ApiStyleSink.ValidateBaseUrl(options.PleasanterUrl!);
        return options;
    }

    /// <summary>
    /// 出力先の既定。/p は CodeDefiner と同じく本体フォルダ（App_Data を持つ Implem.Pleasanter、またはその親）。
    /// 省略時は、このツールの兄弟フォルダ Implem.Pleasanter を探す（配布 ZIP の既定の配置）。見つからなければ null。
    /// </summary>
    public static string? FindExtendedStylesFolder(string? pleasanterPath, string? toolFolder = null)
    {
        static string? Styles(string folder) => Directory.Exists(Path.Combine(folder, "App_Data")) ? Path.Combine(folder, "App_Data", "Parameters", "ExtendedStyles") : null;
        if (pleasanterPath != null)
            return Styles(pleasanterPath) ?? Styles(Path.Combine(pleasanterPath, "Implem.Pleasanter")) ?? throw new UserError("/p must be the Pleasanter folder that contains App_Data.");
        return Styles(Path.GetFullPath(Path.Combine(toolFolder ?? AppContext.BaseDirectory, "..", "Implem.Pleasanter")));
    }

    /// <summary>接頭辞は管理対象の判定（Prune の範囲）に使うため、空や記号を許さない。ファイル名にも使える文字だけ。</summary>
    public static string ValidatePrefix(string prefix) =>
        System.Text.RegularExpressions.Regex.IsMatch(prefix, "^[A-Za-z0-9][A-Za-z0-9_-]{0,31}$") ? prefix : throw new UserError("Prefix must be 1-32 characters of letters, digits, _ or -, starting with a letter or digit.");
}
