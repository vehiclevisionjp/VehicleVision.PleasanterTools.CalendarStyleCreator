using VehicleVision.PleasanterTools.CalendarStyleCreator;

var passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    passed++;
}
void Reject(Action action, string name)
{
    try { action(); } catch (UserError) { passed++; return; }
    throw new Exception("FAIL: " + name);
}

// CSV
var csv = CsvHolidaySource.Parse("﻿date,name\r\n2026-01-01,元日\r\n# comment\r\n\r\n2026/2/11,\"建国記念の日, 祝\"\r\n");
Check(csv.Count == 2, "CSV skips header, comment and blank lines");
Check(csv[0] == new Holiday(new DateOnly(2026, 1, 1), "元日"), "CSV ISO date");
Check(csv[1] == new Holiday(new DateOnly(2026, 2, 11), "建国記念の日, 祝"), "CSV slash date and quoted comma");
Reject(() => CsvHolidaySource.Parse("2026-01-01,元日\nbad,x"), "CSV invalid date");
Reject(() => CsvHolidaySource.Parse("2026-01-01"), "CSV missing name");
Reject(() => CsvHolidaySource.Parse("2026-01-01,\"open"), "CSV unterminated quote");

// 内閣府 syukujitsu.csv 形式（Shift_JIS・ヘッダー・y/M/d）
System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
var cao = CsvHolidaySource.Parse(CsvHolidaySource.Decode(System.Text.Encoding.GetEncoding(932).GetBytes("国民の祝日・休日月日,国民の祝日・休日名称\r\n1955/1/1,元日\r\n2027/11/23,勤労感謝の日\r\n")));
Check(cao.Count == 2 && cao[1] == new Holiday(new DateOnly(2027, 11, 23), "勤労感謝の日"), "Cabinet Office CSV (Shift_JIS)");
Check(CsvHolidaySource.Decode(System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes("a,元日")).ToArray()).TrimStart('\uFEFF') == "a,元日", "UTF-8 with BOM");

// Google iCal
var ics = "BEGIN:VCALENDAR\r\nBEGIN:VEVENT\r\nDTSTART;VALUE=DATE:20260101\r\nSTATUS:CONFIRMED\r\nSUMMARY:元日\r\nEND:VEVENT\r\n"
    + "BEGIN:VEVENT\r\nDTSTART;VALUE=DATE:20260112\r\nSUMMARY:成人の\r\n 日\r\nEND:VEVENT\r\n"
    + "BEGIN:VEVENT\r\nDTSTART;VALUE=DATE:20260211\r\nSTATUS:CANCELLED\r\nSUMMARY:x\r\nEND:VEVENT\r\n"
    + "BEGIN:VEVENT\r\nDTSTART;VALUE=DATE:20300101\r\nSUMMARY:out\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n";
var google = IcalHolidaySource.Parse(ics, 2026, 2027);
Check(google.Count == 2 && google[0] == new Holiday(new DateOnly(2026, 1, 1), "元日"), "iCal date and summary");
Check(google[1].Name == "成人の日", "iCal folded line is joined");

// iCal URL
Reject(() => IcalHolidaySource.ValidateUrl("http://example.com/a.ics"), "iCal URL must be https");
Reject(() => Options.Parse(["generate", "/output", "o", "/ical-url", "file:///x.ics"]), "iCal URL scheme");
var byUrl = Options.Parse(["generate", "/output", "o", "/ical-url", "https://a.example/x.ics, https://b.example/y.ics"]);
Check(byUrl.IcalUrls.Count == 2 && byUrl.CalendarIds.Count == 0, "Multiple iCal URLs replace the default calendar");
Check(Options.Parse(["generate", "/output", "o", "/ical-url", "https://a.example/x.ics", "/calendar-id", "a@x"]).CalendarIds.Count == 1, "Calendar IDs and iCal URLs can be combined");
Check(IcalAuth.FromEnvironment(_ => null).IsEmpty, "No auth by default");
Check(IcalAuth.FromEnvironment(k => k == Options.IcalBearerTokenVariable ? "tok" : null).ToHeader()!.ToString() == "Bearer tok", "Bearer auth");
Check(IcalAuth.FromEnvironment(k => k == Options.IcalBasicUserVariable ? "u" : k == Options.IcalBasicPasswordVariable ? "p" : null).ToHeader()!.Parameter == Convert.ToBase64String("u:p"u8.ToArray()), "Basic auth");
Reject(() => IcalAuth.FromEnvironment(_ => "x"), "Bearer and Basic are exclusive");
var named = new IcalEndpoint("https://a.example/x.ics", BearerTokenEnv: "MY_TOKEN");
Check(IcalAuth.FromEnvironment(k => k == "MY_TOKEN" ? "t1" : null, named).BearerToken == "t1", "Per-URL Bearer from a named variable");
Reject(() => IcalAuth.FromEnvironment(_ => null, named), "Named variable must be set");
Reject(() => IcalAuth.FromEnvironment(k => k == Options.IcalBearerTokenVariable ? "shared" : null, named), "Named auth does not fall back to the shared variables");
var fake = new FakeIcalHandler();
using (var fakeClient = new HttpClient(fake))
{
    const string FakeUrl = "https://ical.test/private/basic.ics";
    var withAuth = new IcalHolidaySource(FakeUrl, new IcalAuth(BearerToken: "secret"), 2026, 2026, fakeClient);
    Check((await withAuth.GetHolidaysAsync(default)).Count == 1 && fake.LastAuthorization == "Bearer secret", "iCal fetch sends the Authorization header");
    try { await new IcalHolidaySource(FakeUrl, null, 2026, 2026, fakeClient).GetHolidaysAsync(default); throw new Exception("FAIL: 401 expected"); }
    catch (UserError e) { Check(e.Message.Contains("401") && !e.Message.Contains("secret") && !e.Message.Contains(FakeUrl), "401 reports status without leaking URL or token"); }
}

// Config file
var configDir = Path.Combine(Path.GetTempPath(), "csc-cfg-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(configDir);
try
{
    var configPath = Path.Combine(configDir, "calendarstyle.json");
    File.WriteAllText(configPath, """
        {
          // comments and trailing commas are allowed
          "calendars": ["a@group.calendar.google.com", "b@group.calendar.google.com"],
          "icalUrls": ["https://x.example/a.ics", { "url": "https://y.example/b.ics", "basicUserEnv": "U", "basicPasswordEnv": "P" },],
          "csv": ["holidays.csv"], "from": 2026, "to": 2027, "name": "FromConfig", "siteIds": [3, 4],
          "output": "out", "colors": { "saturdayBackground": "#abcdef" }
        }
        """);
    var fromConfig = Options.Parse(["generate", "/config", configPath]);
    Check(fromConfig.CalendarIds.Count == 2 && fromConfig.IcalUrls.Count == 2 && fromConfig.IcalUrls[1].BasicUserEnv == "U", "Config arrays and per-URL auth names");
    Check(fromConfig.CsvPaths.SequenceEqual([Path.Combine(configDir, "holidays.csv")]) && fromConfig.Output == Path.Combine(configDir, "out"), "Config relative paths resolve from the config folder");
    Check(fromConfig.Name == "FromConfig" && fromConfig.SiteIds.SequenceEqual([3, 4]) && fromConfig.FromYear == 2026 && fromConfig.ToYear == 2027, "Config scalar values");
    Check(fromConfig.Theme.SaturdayBackground == "#abcdef" && fromConfig.Theme.HolidayBackground == new CalendarTheme().HolidayBackground, "Config colors override only given keys");
    var overridden = Options.Parse(["generate", "/config", configPath, "/name", "Cli", "/calendar-id", "none", "/site-id", "9"]);
    Check(overridden.Name == "Cli" && overridden.CalendarIds.Count == 0 && overridden.SiteIds.SequenceEqual([9]), "Command line overrides the config file");
    File.WriteAllText(configPath, "{ \"calendars\": [] , \"csv\": [\"h.csv\"], \"output\": \"o\" }");
    Check(Options.Parse(["generate", "/config", configPath]).CalendarIds.Count == 0, "Empty calendars array disables Google calendars");
    File.WriteAllText(configPath, "{ \"csv\": [\"h.csv\"], \"output\": \"o\" }");
    Check(Options.Parse(["generate", "/config", configPath]).CalendarIds.SequenceEqual([GoogleCalendarHolidaySource.JapaneseHolidays]), "Default calendar when the config names no calendar");
    foreach (var bad in new[] { "{ \"bearerToken\": \"secret\" }", "{ \"icalUrls\": [{ \"url\": \"https://x/a.ics\", \"bearerToken\": \"secret\" }] }", "{ \"icalUrls\": [\"http://x/a.ics\"] }", "{ \"from\": \"2026\" }", "[1]", "{ broken" })
    {
        File.WriteAllText(configPath, bad);
        try { Options.Parse(["generate", "/config", configPath, "/output", "o"]); throw new Exception("FAIL: bad config accepted: " + bad); }
        catch (UserError e) { Check(!e.Message.Contains("secret"), "Bad config is rejected without echoing values"); }
    }
}
finally { Directory.Delete(configDir, true); }

// CSS
var theme = new CalendarTheme();
var css = CssGenerator.Generate([new(new DateOnly(2026, 2, 11), "B"), new(new DateOnly(2026, 1, 1), "A"), new(new DateOnly(2026, 1, 1), "A2")], theme, 2026, 2026);
Check(css.Contains("td.container:has(> div > .day)[data-id=\"2026/1/1\"]:not(.other-month)"), "Standard monthly cell, no zero padding, excludes other month and yearly view");
Check(css.Contains("td.container.is-weekly[data-id=\"2026/2/11\"]"), "Standard weekly cell");
Check(css.Contains("td.fc-daygrid-day[data-date=\"2026-01-01\"]:not(.fc-day-other) .fc-daygrid-day-top::after"), "FullCalendar day label");
Check(css.Contains("content: \"A2\"") && !css.Contains("content: \"A\""), "Last duplicate wins");
Check(css.Contains("[data-id=\"2026/1/3\"]") && css.Contains("[data-id=\"2026/1/4\"]"), "Saturday 2026-01-03 and Sunday 2026-01-04 are styled");
Check(!css.Contains("[data-id=\"2026/1/5\"]"), "Weekday is not styled");
Check(css.Contains(".fc-day-sat:not(.fc-day-other) {") && css.Contains(".fc-day-sun:not(.fc-day-other) {"), "FullCalendar weekday classes");
Check(css.IndexOf("saturday", StringComparison.Ordinal) < 0 && css.IndexOf(theme.SaturdayBackground, StringComparison.Ordinal) < css.IndexOf(theme.HolidayBackground, StringComparison.Ordinal), "Holiday colors come after Saturday so they win");
Check(css == CssGenerator.Generate([new(new DateOnly(2026, 1, 1), "A2"), new(new DateOnly(2026, 2, 11), "B")], theme, 2026, 2026), "CSS is deterministic");
Check(CssGenerator.EscapeString("a\"b\\c</style>\n") == "a\\\"b\\\\c\\3c /style\\3e  ", "CSS string escape");
Check(!CssGenerator.Generate([new(new DateOnly(2026, 1, 1), "")], theme, 2026, 2026).Contains("::after"), "CSS omits label for empty name");
Reject(() => new CalendarTheme(HolidayBackground: "red; } body { display:none").Validate(), "Color injection is rejected");

// Options
var parsed = Options.Parse(["generate", "/csv", "/tmp/h.csv", "/output", "out", "/site-id", "1, 2,2"]);
Check(parsed.Action == "generate" && parsed.CsvPaths.SequenceEqual(["/tmp/h.csv"]) && parsed.SiteIds.SequenceEqual([1, 2]), "Options parse");
Check(Options.Parse([]).Action == "help", "Options help");
Reject(() => Options.Parse(["generate", "/csv", "h.csv"]), "generate requires /output");
Reject(() => Options.Parse(["register", "/csv", "h.csv"]), "register requires /url");
Check(Options.Parse(["generate", "/output", "out"]).CalendarIds.SequenceEqual([GoogleCalendarHolidaySource.JapaneseHolidays]), "Default calendar is the Japanese holidays");
Check(Options.Parse(["generate", "/output", "out", "/calendar-id", "a@x, b@x,a@x"]).CalendarIds.SequenceEqual(["a@x", "b@x"]), "Multiple calendar IDs");
Check(Options.Parse(["generate", "/csv", "h.csv", "/output", "out", "/calendar-id", "none"]).CalendarIds.Count == 0, "Calendar can be disabled");
Reject(() => Options.Parse(["generate", "/calendar-id", "none", "/output", "out"]), "source required when calendar is disabled");
Reject(() => Options.Parse(["generate", "/csv", "h.csv", "/output", "o", "/from", "2027", "/to", "2026"]), "year range");
Reject(() => Options.Parse(["generate", "/bogus", "x"]), "unknown option");
Reject(() => Options.Parse(["generate", "/csv", "h.csv", "/output", "o", "/holiday-bg", "red;x"]), "invalid color option");

// Extensions API
Reject(() => ApiStyleSink.ValidateBaseUrl("http://pleasanter.example.com"), "Pleasanter URL must be https");
ApiStyleSink.ValidateBaseUrl("http://localhost:58800");
ApiStyleSink.ValidateBaseUrl("https://pleasanter.example.com/");
Reject(() => Options.Parse(["register", "/csv", "h.csv", "/url", "http://pleasanter.example.com"]), "register rejects plain http");
{
    var pl = new FakePleasanter();
    using var http = new HttpClient(pl);
    var sink = new ApiStyleSink("https://pl.test/", "KEY-123", http);
    var style = new StyleDefinition("CalendarHolidays", "d", [], "/* css */");
    await sink.WriteAsync(style, default);
    Check(pl.Created == 1 && pl.Updated == 0 && pl.LastCreate!["ExtensionType"]!.ToString() == "Style" && pl.LastCreate["Body"]!.ToString() == "/* css */" && pl.LastCreate["ApiKey"]!.ToString() == "KEY-123", "Creates a Style extension when none exists");
    Check(pl.LastCreate!["ExtensionSettings"]!["Name"]!.ToString() == "CalendarHolidays" && pl.LastCreate["ExtensionSettings"]!["SiteIdList"] == null, "Settings carry the name and omit SiteIdList for all sites");
    await sink.WriteAsync(style, default);
    Check(pl.Created == 1 && pl.Updated == 0, "Unchanged content is not written again");
    await sink.WriteAsync(style with { Css = "/* new */", SiteIds = [5] }, default);
    Check(pl.Created == 1 && pl.Updated == 1 && pl.LastUpdatePath == "/api/extensions/77/update" && pl.LastUpdate!["Body"]!.ToString() == "/* new */" && pl.LastUpdate["ExtensionSettings"]!["SiteIdList"]![0]!.GetValue<long>() == 5, "Updates the existing extension instead of creating a duplicate");
    pl.Mode = "forbidden";
    try { await sink.WriteAsync(style, default); throw new Exception("FAIL: 403 expected"); }
    catch (UserError e) { Check(e.Message.Contains("AllowExtensionsApi") && !e.Message.Contains("KEY-123"), "403 explains AllowExtensionsApi without leaking the API key"); }
    pl.Mode = "duplicate";
    try { await sink.WriteAsync(style, default); throw new Exception("FAIL: duplicate expected"); }
    catch (UserError e) { Check(e.Message.Contains("Multiple"), "Duplicate names are reported"); }
}

// File sink
var temp = Path.Combine(Path.GetTempPath(), "csc-test-" + Guid.NewGuid().ToString("N"));
try
{
    await new FileStyleSink(temp).WriteAsync(new("Holidays", "d", [], "/* css */"), CancellationToken.None);
    var json = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(temp, "Holidays.json")));
    Check(json.RootElement.GetProperty("Style").GetString() == "/* css */" && !json.RootElement.TryGetProperty("SiteIdList", out _), "File sink writes Style and omits empty SiteIdList");
    await new FileStyleSink(temp).WriteAsync(new("Holidays", "d", [5], "x"), CancellationToken.None);
    json = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(temp, "Holidays.json")));
    Check(json.RootElement.GetProperty("SiteIdList")[0].GetInt64() == 5, "File sink overwrites same name");
    Reject(() => new FileStyleSink(temp).WriteAsync(new("../x", "d", [], "x"), CancellationToken.None).GetAwaiter().GetResult(), "File sink rejects path in name");
}
finally { if (Directory.Exists(temp)) Directory.Delete(temp, true); }

Console.WriteLine($"{passed} checks passed.");

// https が必須のため、実サーバーの代わりにメッセージハンドラーで応答する。
sealed class FakeIcalHandler : HttpMessageHandler
{
    public string? LastAuthorization { get; private set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel)
    {
        LastAuthorization = request.Headers.Authorization?.ToString();
        if (LastAuthorization == null) return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized));
        return Task.FromResult(new HttpResponseMessage { Content = new StringContent("BEGIN:VEVENT\r\nDTSTART;VALUE=DATE:20260101\r\nSUMMARY:x\r\nEND:VEVENT\r\n") });
    }
}

// Extensions API の最小の模擬。Style 種別の拡張を 1 件だけ保持する。
sealed class FakePleasanter : HttpMessageHandler
{
    private System.Text.Json.Nodes.JsonObject? stored;
    public int Created, Updated;
    public string Mode = "";
    public System.Text.Json.Nodes.JsonObject? LastCreate, LastUpdate;
    public string? LastUpdatePath;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel)
    {
        var path = request.RequestUri!.AbsolutePath;
        var body = (System.Text.Json.Nodes.JsonObject)System.Text.Json.Nodes.JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancel))!;
        static HttpResponseMessage Json(string json) => new() { Content = new StringContent(json) };
        if (Mode == "forbidden") return Json("""{"StatusCode":403,"Message":"Forbidden"}""");
        if (path.EndsWith("/get"))
        {
            var items = new System.Text.Json.Nodes.JsonArray();
            if (Mode == "duplicate") { items.Add(Item(1)); items.Add(Item(2)); }
            else if (stored != null) items.Add(Item(77));
            return Json(new System.Text.Json.Nodes.JsonObject { ["StatusCode"] = 200, ["Response"] = new System.Text.Json.Nodes.JsonObject { ["Data"] = items } }.ToJsonString());
        }
        stored = body;
        if (path.EndsWith("/create")) { Created++; LastCreate = body; }
        else { Updated++; LastUpdate = body; LastUpdatePath = path; }
        return Json("""{"Id":77,"StatusCode":200,"Message":"ok"}""");
    }
    private System.Text.Json.Nodes.JsonObject Item(int id) => new()
    {
        ["ExtensionId"] = id,
        ["ExtensionType"] = "Style",
        ["ExtensionName"] = stored?["ExtensionName"]?.ToString() ?? "CalendarHolidays",
        ["Body"] = stored?["Body"]?.ToString(),
        // 本体は ExtensionSettings を JSON 文字列として返すことがあるため、文字列で返して比較の両対応を確認する。
        ["ExtensionSettings"] = stored?["ExtensionSettings"]?.ToJsonString(),
    };
}
