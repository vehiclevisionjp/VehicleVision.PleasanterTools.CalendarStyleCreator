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
var fake = new FakeIcalHandler();
using (var fakeClient = new HttpClient(fake))
{
    const string FakeUrl = "https://ical.test/private/basic.ics";
    var withAuth = new IcalHolidaySource(FakeUrl, new IcalAuth(BearerToken: "secret"), 2026, 2026, fakeClient);
    Check((await withAuth.GetHolidaysAsync(default)).Count == 1 && fake.LastAuthorization == "Bearer secret", "iCal fetch sends the Authorization header");
    try { await new IcalHolidaySource(FakeUrl, null, 2026, 2026, fakeClient).GetHolidaysAsync(default); throw new Exception("FAIL: 401 expected"); }
    catch (UserError e) { Check(e.Message.Contains("401") && !e.Message.Contains("secret") && !e.Message.Contains(FakeUrl), "401 reports status without leaking URL or token"); }
}

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
Check(parsed.Action == "generate" && parsed.CsvPath == "/tmp/h.csv" && parsed.SiteIds.SequenceEqual([1, 2]), "Options parse");
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
