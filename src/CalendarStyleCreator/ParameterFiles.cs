using System.Text.Json;

namespace VehicleVision.PleasanterTools.CalendarStyleCreator;

/// <summary>iCal の取得先 1 件。認証は環境変数の「名前」で参照する（パラメーターに秘密を書かせない）。名前を省略すると共通の既定の環境変数を使う。</summary>
public sealed record IcalEndpoint(string Url, string? BearerTokenEnv = null, string? BasicUserEnv = null, string? BasicPasswordEnv = null)
{
    public bool HasAuthReference => BearerTokenEnv != null || BasicUserEnv != null;
}

/// <summary>
/// 固定の設定を置くパラメーターファイル。Pleasanter 本体の App_Data/Parameters に倣い、関心ごとにファイルを分け、
/// キーは PascalCase、値は null なら未指定（既定）、配列やオブジェクトで階層を持つ。このツールの App_Data/Parameters（既定）に置く。
///
///   Holidays.json   取得元: GoogleCalendarIds・IcalUrls・CsvFiles・FromYear・ToYear
///   Style.json      見た目と名前: Prefix・FirstDayOfWeek・Colors
///   Placement.json  配置: OutputPath・PleasanterPath・Url・TenantId・SiteIdList・Prune
///
/// ファイルが無ければ既定値。未知のキーはタイプミスを見逃さないようエラーにする。資格情報は書けない（環境変数の名前だけを参照する）。
/// コマンドラインのオプションが優先。相対パスはパラメーターのフォルダ基準。
/// </summary>
public sealed class ParameterFiles
{
    public const string DefaultFolderName = "App_Data/Parameters";

    public IReadOnlyList<string>? Calendars { get; private set; }
    public IReadOnlyList<IcalEndpoint>? IcalUrls { get; private set; }
    public IReadOnlyList<string>? Csv { get; private set; }
    public int? From { get; private set; }
    public int? To { get; private set; }
    public string? Prefix { get; private set; }
    public int? FirstDayOfWeek { get; private set; }
    public CalendarTheme? Theme { get; private set; }
    public string? Output { get; private set; }
    public string? PleasanterPath { get; private set; }
    public string? PleasanterUrl { get; private set; }
    public int? TenantId { get; private set; }
    public IReadOnlyList<long>? SiteIds { get; private set; }
    public bool? Prune { get; private set; }

    private static readonly string[] HolidaysKeys = ["GoogleCalendarIds", "IcalUrls", "CsvFiles", "FromYear", "ToYear"];
    private static readonly string[] StyleKeys = ["Prefix", "FirstDayOfWeek", "Colors"];
    private static readonly string[] PlacementKeys = ["OutputPath", "PleasanterPath", "Url", "TenantId", "SiteIdList", "Prune"];
    private static readonly string[] ColorKeys = ["HolidayBackground", "SaturdayBackground", "HolidayText", "SaturdayText", "WeekdayBackground", "WeekdayText"];
    private static readonly string[] IcalKeys = ["Url", "BearerTokenEnv", "BasicUserEnv", "BasicPasswordEnv"];

    /// <summary>既定のフォルダ（このツールの App_Data/Parameters）。無ければ null。</summary>
    public static string? DefaultFolder(string? toolFolder = null)
    {
        var folder = Path.Combine(toolFolder ?? AppContext.BaseDirectory, DefaultFolderName);
        return Directory.Exists(folder) ? folder : null;
    }

    public static ParameterFiles Load(string? folder)
    {
        var parameters = new ParameterFiles();
        if (folder == null)
        {
            return parameters;
        }

        if (!Directory.Exists(folder))
        {
            throw new UserError("Parameters folder was not found.");
        }

        folder = Path.GetFullPath(folder);
        string Resolve(string value) => Path.IsPathRooted(value) ? value : Path.GetFullPath(Path.Combine(folder, value));
        foreach (var (name, keys, apply) in new (string, string[], Action<JsonProperty>)[]
        {
            ("Holidays.json", HolidaysKeys, p => parameters.ReadHolidays(p, Resolve)),
            ("Style.json", StyleKeys, parameters.ReadStyle),
            ("Placement.json", PlacementKeys, p => parameters.ReadPlacement(p, Resolve)),
        })
        {
            var path = Path.Combine(folder, name);
            if (!File.Exists(path))
            {
                continue;
            }

            JsonDocument document;
            try { document = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }); }
            catch (JsonException) { throw new UserError($"{name} is not valid JSON."); }
            using (document)
            {
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    throw new UserError($"{name} root must be an object.");
                }

                CheckKeys(document.RootElement, keys, name);
                // null は「未指定」（Pleasanter のパラメーターと同じく、既定の値を null で書いてよい）。
                foreach (var property in document.RootElement.EnumerateObject().Where(p => p.Value.ValueKind != JsonValueKind.Null))
                {
                    apply(property);
                }
            }
        }
        return parameters;
    }

    private void ReadHolidays(JsonProperty property, Func<string, string> resolve)
    {
        var value = property.Value;
        switch (property.Name)
        {
            case "GoogleCalendarIds": Calendars = Strings(value, property.Name); break;
            case "IcalUrls": IcalUrls = value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().Select(Endpoint).ToList() : throw new UserError("IcalUrls must be an array."); break;
            case "CsvFiles": Csv = Strings(value, property.Name).Select(resolve).ToList(); break;
            case "FromYear": From = Year(value, property.Name); break;
            case "ToYear": To = Year(value, property.Name); break;
        }
    }

    private void ReadStyle(JsonProperty property)
    {
        var value = property.Value;
        switch (property.Name)
        {
            case "Prefix": Prefix = Str(value, property.Name); break;
            case "FirstDayOfWeek": FirstDayOfWeek = value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var day) && day is >= 0 and <= 6 ? day : throw new UserError("FirstDayOfWeek must be 0 (Sunday) to 6 (Saturday)."); break;
            case "Colors": Theme = ReadTheme(value); break;
        }
    }

    private void ReadPlacement(JsonProperty property, Func<string, string> resolve)
    {
        var value = property.Value;
        switch (property.Name)
        {
            case "OutputPath": Output = resolve(Str(value, property.Name)); break;
            case "PleasanterPath": PleasanterPath = resolve(Str(value, property.Name)); break;
            case "Url": PleasanterUrl = Str(value, property.Name); break;
            case "TenantId": TenantId = value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var tenant) && tenant >= 1 ? tenant : throw new UserError("TenantId must be a positive number."); break;
            case "SiteIdList": SiteIds = value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().Select(e => e.ValueKind == JsonValueKind.Number && e.TryGetInt64(out var id) && id > 0 ? id : throw new UserError("SiteIdList must be positive numbers.")).Distinct().ToList() : throw new UserError("SiteIdList must be an array."); break;
            case "Prune": Prune = value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : throw new UserError("Prune must be true or false."); break;
        }
    }

    private static void CheckKeys(JsonElement element, string[] allowed, string where)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (!allowed.Contains(property.Name))
            {
                throw new UserError($"{where} has an unknown key. Allowed: {string.Join(", ", allowed)}.");
            }
        }
    }

    private static string Str(JsonElement value, string key) =>
        value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString()!.Trim() : throw new UserError($"{key} must be a non-empty string.");

    private static List<string> Strings(JsonElement value, string key) =>
        value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().Select(e => Str(e, key)).Distinct(StringComparer.Ordinal).ToList() : throw new UserError($"{key} must be an array of strings.");

    private static int Year(JsonElement value, string key) =>
        value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var year) && year is >= 1900 and <= 2100 ? year : throw new UserError($"{key} must be a year (1900-2100).");

    private static IcalEndpoint Endpoint(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            return Checked(new IcalEndpoint(Str(value, "IcalUrls")));
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new UserError("IcalUrls items must be a URL string or an object.");
        }

        CheckKeys(value, IcalKeys, "IcalUrls item");
        string? Optional(string key) => value.TryGetProperty(key, out var v) && v.ValueKind != JsonValueKind.Null ? Str(v, key) : null;
        var endpoint = new IcalEndpoint(Optional("Url") ?? throw new UserError("IcalUrls item requires Url."), Optional("BearerTokenEnv"), Optional("BasicUserEnv"), Optional("BasicPasswordEnv"));
        if (endpoint.BearerTokenEnv != null && endpoint.BasicUserEnv != null)
        {
            throw new UserError("An iCal item can use BearerTokenEnv or BasicUserEnv, not both.");
        }

        if (endpoint.BasicPasswordEnv != null && endpoint.BasicUserEnv == null)
        {
            throw new UserError("BasicPasswordEnv requires BasicUserEnv.");
        }

        return Checked(endpoint);
    }

    private static IcalEndpoint Checked(IcalEndpoint endpoint) { IcalHolidaySource.ValidateUrl(endpoint.Url); return endpoint; }

    private static CalendarTheme ReadTheme(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new UserError("Colors must be an object.");
        }

        CheckKeys(value, ColorKeys, "Colors");
        var defaults = new CalendarTheme();
        string Pick(string key, string fallback) => value.TryGetProperty(key, out var v) && v.ValueKind != JsonValueKind.Null ? Str(v, key) : fallback;
        return new CalendarTheme(Pick("HolidayBackground", defaults.HolidayBackground), Pick("SaturdayBackground", defaults.SaturdayBackground), Pick("HolidayText", defaults.HolidayText), Pick("SaturdayText", defaults.SaturdayText), Pick("WeekdayBackground", defaults.WeekdayBackground), Pick("WeekdayText", defaults.WeekdayText)).Validate();
    }
}
