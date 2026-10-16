using System.Text.Json;

namespace VehicleVision.PleasanterTools.CalendarStyleCreator;

/// <summary>iCal の取得先 1 件。認証は環境変数の「名前」で参照する（設定ファイルに秘密を書かせない）。名前を省略すると共通の既定の環境変数を使う。</summary>
public sealed record IcalEndpoint(string Url, string? BearerTokenEnv = null, string? BasicUserEnv = null, string? BasicPasswordEnv = null)
{
    public bool HasAuthReference => BearerTokenEnv != null || BasicUserEnv != null;
}

/// <summary>
/// 設定ファイル（JSON）。固定の設定を置き、コマンドラインのオプションで上書きする。相対パスは設定ファイルのあるフォルダ基準。
/// 未知のキーはタイプミスを見逃さないようエラーにする。キー名はコメント・末尾カンマ可（camelCase）。
/// </summary>
public sealed class ConfigFile
{
    public IReadOnlyList<string>? Calendars { get; private set; }
    public IReadOnlyList<IcalEndpoint>? IcalUrls { get; private set; }
    public IReadOnlyList<string>? Csv { get; private set; }
    public int? From { get; private set; }
    public int? To { get; private set; }
    public string? Name { get; private set; }
    public IReadOnlyList<long>? SiteIds { get; private set; }
    public string? Output { get; private set; }
    public string? PleasanterPath { get; private set; }
    public string? PleasanterUrl { get; private set; }
    public CalendarTheme? Theme { get; private set; }

    private static readonly string[] Keys = ["calendars", "icalUrls", "csv", "from", "to", "name", "siteIds", "output", "pleasanterPath", "pleasanterUrl", "colors"];
    private static readonly string[] ColorKeys = ["holidayBackground", "saturdayBackground", "holidayText", "saturdayText"];
    private static readonly string[] IcalKeys = ["url", "bearerTokenEnv", "basicUserEnv", "basicPasswordEnv"];

    public static ConfigFile Load(string path)
    {
        if (!File.Exists(path)) throw new UserError("Config file was not found.");
        JsonDocument document;
        try { document = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }); }
        catch (JsonException) { throw new UserError("Config file is not valid JSON."); }
        using (document) return Read(document.RootElement, Path.GetDirectoryName(Path.GetFullPath(path))!);
    }

    public static ConfigFile Read(JsonElement root, string baseDirectory)
    {
        if (root.ValueKind != JsonValueKind.Object) throw new UserError("Config root must be an object.");
        CheckKeys(root, Keys, "Config");
        var config = new ConfigFile();
        string Resolve(string value) => Path.IsPathRooted(value) ? value : Path.GetFullPath(Path.Combine(baseDirectory, value));
        foreach (var property in root.EnumerateObject())
        {
            var value = property.Value;
            switch (property.Name)
            {
                case "calendars": config.Calendars = Strings(value, "calendars"); break;
                case "icalUrls": config.IcalUrls = value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().Select(Endpoint).ToList() : throw new UserError("icalUrls must be an array."); break;
                case "csv": config.Csv = Strings(value, "csv").Select(Resolve).ToList(); break;
                case "from": config.From = Year(value, "from"); break;
                case "to": config.To = Year(value, "to"); break;
                case "name": config.Name = Str(value, "name"); break;
                case "siteIds": config.SiteIds = value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().Select(e => e.ValueKind == JsonValueKind.Number && e.TryGetInt64(out var id) && id > 0 ? id : throw new UserError("siteIds must be positive numbers.")).Distinct().ToList() : throw new UserError("siteIds must be an array."); break;
                case "output": config.Output = Resolve(Str(value, "output")); break;
                case "pleasanterPath": config.PleasanterPath = Resolve(Str(value, "pleasanterPath")); break;
                case "pleasanterUrl": config.PleasanterUrl = Str(value, "pleasanterUrl"); break;
                case "colors": config.Theme = ReadTheme(value); break;
            }
        }
        return config;
    }

    private static void CheckKeys(JsonElement element, string[] allowed, string where)
    {
        foreach (var property in element.EnumerateObject())
            if (!allowed.Contains(property.Name)) throw new UserError($"{where} has an unknown key. Allowed: {string.Join(", ", allowed)}.");
    }

    private static string Str(JsonElement value, string key) =>
        value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString()!.Trim() : throw new UserError($"{key} must be a non-empty string.");

    private static List<string> Strings(JsonElement value, string key) =>
        value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().Select(e => Str(e, key)).Distinct(StringComparer.Ordinal).ToList() : throw new UserError($"{key} must be an array of strings.");

    private static int Year(JsonElement value, string key) =>
        value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var year) && year is >= 1900 and <= 2100 ? year : throw new UserError($"{key} must be a year (1900-2100).");

    private static IcalEndpoint Endpoint(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String) return Checked(new IcalEndpoint(Str(value, "icalUrls")));
        if (value.ValueKind != JsonValueKind.Object) throw new UserError("icalUrls items must be a URL string or an object.");
        CheckKeys(value, IcalKeys, "icalUrls item");
        string? Optional(string key) => value.TryGetProperty(key, out var v) ? Str(v, key) : null;
        var endpoint = new IcalEndpoint(Optional("url") ?? throw new UserError("icalUrls item requires url."), Optional("bearerTokenEnv"), Optional("basicUserEnv"), Optional("basicPasswordEnv"));
        if (endpoint.BearerTokenEnv != null && endpoint.BasicUserEnv != null) throw new UserError("An iCal item can use bearerTokenEnv or basicUserEnv, not both.");
        if (endpoint.BasicPasswordEnv != null && endpoint.BasicUserEnv == null) throw new UserError("basicPasswordEnv requires basicUserEnv.");
        return Checked(endpoint);
    }

    private static IcalEndpoint Checked(IcalEndpoint endpoint) { IcalHolidaySource.ValidateUrl(endpoint.Url); return endpoint; }

    private static CalendarTheme ReadTheme(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object) throw new UserError("colors must be an object.");
        CheckKeys(value, ColorKeys, "colors");
        var defaults = new CalendarTheme();
        string Pick(string key, string fallback) => value.TryGetProperty(key, out var v) ? Str(v, key) : fallback;
        return new CalendarTheme(Pick("holidayBackground", defaults.HolidayBackground), Pick("saturdayBackground", defaults.SaturdayBackground), Pick("holidayText", defaults.HolidayText), Pick("saturdayText", defaults.SaturdayText)).Validate();
    }
}
