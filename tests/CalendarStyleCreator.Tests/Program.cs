using VehicleVision.PleasanterTools.CalendarStyleCreator;

var passed = 0;
void Check(bool condition, string name)
{
    if (!condition)
    {
        throw new Exception("FAIL: " + name);
    }

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

// iCal: 複数日の終日予定（DTEND は含まない）・年跨ぎ・DURATION・時間指定・上限
string Ev(string lines) => "BEGIN:VCALENDAR\r\nBEGIN:VEVENT\r\n" + lines.Replace("\n", "\r\n") + "\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n";
var multi = IcalHolidaySource.Parse(Ev("DTSTART;VALUE=DATE:20261229\nDTEND;VALUE=DATE:20270104\nSUMMARY:年末年始休業"), 2026, 2027);
Check(multi.Count == 6 && multi[0].Date == new DateOnly(2026, 12, 29) && multi[5].Date == new DateOnly(2027, 1, 3) && multi.All(h => h.Name == "年末年始休業"), "iCal multi-day all-day event covers DTSTART to the day before DTEND");
var crossing = IcalHolidaySource.Parse(Ev("DTSTART;VALUE=DATE:20261229\nDTEND;VALUE=DATE:20270104\nSUMMARY:x"), 2027, 2027);
Check(crossing.Count == 3 && crossing[0].Date == new DateOnly(2027, 1, 1), "iCal multi-day event is filtered per day by year");
Check(IcalHolidaySource.Parse(Ev("DTSTART;VALUE=DATE:20260101\nSUMMARY:x"), 2026, 2026).Count == 1, "iCal all-day event without DTEND is one day");
Check(IcalHolidaySource.Parse(Ev("DTSTART;VALUE=DATE:20260101\nDTEND;VALUE=DATE:20260101\nSUMMARY:x"), 2026, 2026).Count == 1, "iCal DTEND not after DTSTART is one day");
Check(IcalHolidaySource.Parse(Ev("DTSTART;VALUE=DATE:20260101\nDURATION:P3D\nSUMMARY:x"), 2026, 2026).Count == 3, "iCal DURATION in days");
Check(IcalHolidaySource.Parse(Ev("DTSTART;VALUE=DATE:20260101\nDURATION:P1W\nSUMMARY:x"), 2026, 2026).Count == 7, "iCal DURATION in weeks");
Check(IcalHolidaySource.Parse(Ev("DTSTART:20260101T090000Z\nDTEND:20260105T090000Z\nSUMMARY:x"), 2026, 2026).Count == 1, "iCal timed event is one day");
Check(IcalHolidaySource.Parse(Ev("DTSTART;VALUE=DATE:20260101\nDTEND;VALUE=DATE:20300101\nSUMMARY:x"), 2026, 2030).Count == IcalHolidaySource.MaxSpanDays, "iCal span is capped");

// iCal: 繰り返し（RRULE・EXDATE・RECURRENCE-ID）
var yearly = IcalHolidaySource.Parse(Ev("UID:a\nDTSTART;VALUE=DATE:20250401\nRRULE:FREQ=YEARLY\nSUMMARY:創立記念日"), 2026, 2027);
Check(yearly.Select(h => h.Date).SequenceEqual([new DateOnly(2026, 4, 1), new DateOnly(2027, 4, 1)]), "RRULE yearly within the year range");
var weekly = IcalHolidaySource.Parse(Ev("UID:b\nDTSTART;VALUE=DATE:20260105\nRRULE:FREQ=WEEKLY;BYDAY=MO,WE;COUNT=5\nSUMMARY:定休"), 2026, 2026);
Check(weekly.Select(h => h.Date).SequenceEqual([new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 7), new DateOnly(2026, 1, 12), new DateOnly(2026, 1, 14), new DateOnly(2026, 1, 19)]), "RRULE weekly BYDAY with COUNT");
var monthly = IcalHolidaySource.Parse(Ev("UID:c\nDTSTART;VALUE=DATE:20260131\nRRULE:FREQ=MONTHLY;BYMONTHDAY=-1;UNTIL=20260430\nSUMMARY:月末"), 2026, 2026);
Check(monthly.Select(h => h.Date).SequenceEqual([new DateOnly(2026, 1, 31), new DateOnly(2026, 2, 28), new DateOnly(2026, 3, 31), new DateOnly(2026, 4, 30)]), "RRULE monthly last day with UNTIL");
var every2 = IcalHolidaySource.Parse(Ev("UID:d\nDTSTART;VALUE=DATE:20260101\nRRULE:FREQ=DAILY;INTERVAL=10;COUNT=3\nSUMMARY:x"), 2026, 2026);
Check(every2.Select(h => h.Date.Day).SequenceEqual([1, 11, 21]), "RRULE daily INTERVAL and COUNT");
var exd = IcalHolidaySource.Parse(Ev("UID:e\nDTSTART;VALUE=DATE:20260401\nRRULE:FREQ=YEARLY\nEXDATE;VALUE=DATE:20270401\nSUMMARY:x"), 2026, 2028);
Check(exd.Select(h => h.Date.Year).SequenceEqual([2026, 2028]), "EXDATE removes an occurrence");
var multiRec = IcalHolidaySource.Parse(Ev("UID:f\nDTSTART;VALUE=DATE:20261229\nDTEND;VALUE=DATE:20270102\nRRULE:FREQ=YEARLY\nSUMMARY:x"), 2027, 2027);
Check(multiRec.Count == 1 + 3 && multiRec[0].Date == new DateOnly(2027, 1, 1), "Recurring multi-day event overlaps into the next year");
var recurrenceOverride = IcalHolidaySource.Parse(
    "BEGIN:VCALENDAR\r\nBEGIN:VEVENT\r\nUID:g\r\nDTSTART;VALUE=DATE:20260401\r\nRRULE:FREQ=YEARLY\r\nSUMMARY:x\r\nEND:VEVENT\r\n"
    + "BEGIN:VEVENT\r\nUID:g\r\nRECURRENCE-ID;VALUE=DATE:20270401\r\nDTSTART;VALUE=DATE:20270402\r\nSUMMARY:moved\r\nEND:VEVENT\r\n"
    + "BEGIN:VEVENT\r\nUID:g\r\nRECURRENCE-ID;VALUE=DATE:20280401\r\nDTSTART;VALUE=DATE:20280401\r\nSTATUS:CANCELLED\r\nSUMMARY:x\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n", 2026, 2028);
Check(recurrenceOverride.Select(h => h.Date).SequenceEqual([new DateOnly(2026, 4, 1), new DateOnly(2027, 4, 2)]), "RECURRENCE-ID replaces and cancels occurrences");
var unsupported = IcalHolidaySource.Parse(Ev("UID:h\nDTSTART;VALUE=DATE:20260112\nRRULE:FREQ=MONTHLY;BYDAY=2MO\nSUMMARY:x"), 2026, 2027);
Check(unsupported.Count == 1, "Unsupported RRULE (ordinal BYDAY) uses only the first occurrence");
Check(IcalHolidaySource.Parse(Ev("UID:i\nDTSTART;VALUE=DATE:20260101\nRRULE:FREQ=DAILY\nSUMMARY:x"), 2026, 2040).Count == IcalRecurrence.MaxOccurrences, "RRULE expansion is capped");

// iCal URL
Reject(() => IcalHolidaySource.ValidateUrl("http://example.com/a.ics"), "iCal URL must be https");
Reject(() => IcalHolidaySource.ValidateUrl("http://example.com/a.ics", false), "iCal URL non-loopback http is rejected when explicitly not allowed");
IcalHolidaySource.ValidateUrl("http://localhost:8080/a.ics", false);
passed++;
foreach (var insecureIcal in new[] { "http://example.com/a.ics", "http://192.168.0.10:8080/a.ics", "https://example.com/a.ics" })
{
    IcalHolidaySource.ValidateUrl(insecureIcal, true);
    passed++;
}
foreach (var badIcal in new[] { "file:///x.ics", "ftp://example.com/a.ics", "example.com/a.ics", "" })
{
    Reject(() => IcalHolidaySource.ValidateUrl(badIcal, true), "iCal URL is rejected even when insecure http is allowed: " + badIcal);
}
Reject(() => Options.Parse(["generate", "/output", "o", "/ical-url", "file:///x.ics"]), "iCal URL scheme");
var byUrl = Options.Parse(["generate", "/output", "o", "/ical-url", "https://a.example/x.ics, https://b.example/y.ics"]);
Check(byUrl.IcalUrls.Count == 2 && byUrl.CalendarIds.SequenceEqual([GoogleCalendarHolidaySource.JapaneseHolidays]), "Multiple iCal URLs are added to the configured calendars");
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

Reject(() => Options.Parse(["generate", "/csv", "h.csv", "/output", "o", "/site-id", "5"]), "generate rejects /site-id (file placement is all sites)");
Check(Options.Parse(["register", "/csv", "h.csv", "/url", "https://pl.example.com", "/site-id", "5"]).SiteIds.SequenceEqual([5]), "register accepts /site-id");

// /p（本体フォルダ）と既定の兄弟フォルダ
var pRoot = Path.Combine(Path.GetTempPath(), "csc-p-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(Path.Combine(pRoot, "web", "Implem.Pleasanter", "App_Data"));
Directory.CreateDirectory(Path.Combine(pRoot, "web", "CalendarStyleCreator"));
try
{
    var expected = Path.Combine(pRoot, "web", "Implem.Pleasanter", "App_Data", "Parameters", "ExtendedStyles");
    Check(Options.FindExtendedStylesFolder(Path.Combine(pRoot, "web", "Implem.Pleasanter")) == expected, "/p accepts the Implem.Pleasanter folder");
    Check(Options.FindExtendedStylesFolder(Path.Combine(pRoot, "web")) == expected, "/p accepts the parent of Implem.Pleasanter");
    Check(Options.FindExtendedStylesFolder(null, Path.Combine(pRoot, "web", "CalendarStyleCreator")) == expected, "Default finds Implem.Pleasanter beside the tool");
    Check(Options.FindExtendedStylesFolder(null, Path.Combine(pRoot, "other")) == null, "No default when Implem.Pleasanter is not beside the tool");
    Reject(() => Options.FindExtendedStylesFolder(Path.Combine(pRoot, "nothing")), "/p without App_Data is rejected");
    Check(Options.Parse(["generate", "/csv", "h.csv", "/p", Path.Combine(pRoot, "web")]).Output == expected, "generate derives the output from /p");
    Check(Options.Parse(["generate", "/csv", "h.csv", "/p", Path.Combine(pRoot, "web"), "/output", "o"]).Output == "o", "/output overrides /p");
}
finally { Directory.Delete(pRoot, true); }

// Parameters（Pleasanter の App_Data/Parameters に倣う。Holidays.json・Style.json・Placement.json）
var shipped = ParameterFiles.DefaultFolder() ?? throw new Exception("FAIL: default parameters are not shipped beside the tool");
Check(Directory.GetFiles(shipped, "*.json").Select(Path.GetFileName).Order().SequenceEqual(["Holidays.json", "Placement.json", "Style.json"]), "Default parameter files are shipped with the tool");
var defaultParameters = ParameterFiles.Load(shipped);
Check(defaultParameters.Calendars!.SequenceEqual([GoogleCalendarHolidaySource.JapaneseHolidays]) && defaultParameters.IcalUrls!.Count == 0 && defaultParameters.Csv!.Count == 0 && defaultParameters.From == null && defaultParameters.To == null, "Shipped Holidays.json holds the defaults (Japanese holidays)");
Check(defaultParameters.Prefix == Options.DefaultPrefix && defaultParameters.FirstDayOfWeek == CssGenerator.DefaultFirstDayOfWeek && defaultParameters.Theme == new CalendarTheme(), "Shipped Style.json equals the built-in defaults");
Check(defaultParameters.Output == null && defaultParameters.PleasanterPath == null && defaultParameters.PleasanterUrl == null && defaultParameters.TenantId == null && defaultParameters.SiteIds!.Count == 0 && defaultParameters.Prune == false, "Shipped Placement.json holds the defaults");
Check(ParameterFiles.Load(null).Calendars == null, "No parameter files means unspecified values");
var configDir = Path.Combine(Path.GetTempPath(), "csc-cfg-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(configDir);
try
{
    void Write(string name, string json) => File.WriteAllText(Path.Combine(configDir, name), json);
    Write("Holidays.json", """
        {
          "GoogleCalendarIds": ["a@group.calendar.google.com", "b@group.calendar.google.com"],
          "IcalUrls": ["https://x.example/a.ics", { "Url": "https://y.example/b.ics", "BasicUserEnv": "U", "BasicPasswordEnv": "P" },],
          "CsvFiles": ["holidays.csv"], "FromYear": 2026, "ToYear": 2027
        }
        """);
    Write("Style.json", """{ "Prefix": "FromFile_", "FirstDayOfWeek": 0, "Colors": { "SaturdayBackground": "#abcdef", "HolidayText": null } }""");
    Write("Placement.json", """{ "OutputPath": "out", "Url": "https://pl.example.com", "TenantId": 4, "SiteIdList": [3, 4], "Prune": true }""");
    var fromFiles = Options.Parse(["register", "/parameters", configDir]);
    Check(fromFiles.CalendarIds.Count == 2 && fromFiles.IcalUrls.Count == 2 && fromFiles.IcalUrls[1].BasicUserEnv == "U", "Parameter arrays and per-URL auth names");
    Check(fromFiles.CsvPaths.SequenceEqual([Path.Combine(configDir, "holidays.csv")]) && fromFiles.Output == Path.Combine(configDir, "out"), "Relative paths resolve from the Parameters folder");
    Check(fromFiles.Prefix == "FromFile_" && fromFiles.Prune && fromFiles.TenantId == 4 && fromFiles.SiteIds.SequenceEqual([3, 4]) && fromFiles.FromYear == 2026 && fromFiles.ToYear == 2027 && fromFiles.FirstDayOfWeek == 0 && fromFiles.PleasanterUrl == "https://pl.example.com", "Parameter scalar values across the three files");
    Check(fromFiles.Theme.SaturdayBackground == "#abcdef" && fromFiles.Theme.HolidayBackground == new CalendarTheme().HolidayBackground && fromFiles.Theme.HolidayText == new CalendarTheme().HolidayText, "Colors override only given keys; null means default");
    var overridden = Options.Parse(["register", "/parameters", configDir, "/prefix", "Cli_", "/calendar-id", "none", "/site-id", "9", "/first-day-of-week", "2"]);
    Check(overridden.Prefix == "Cli_" && overridden.CalendarIds.Count == 0 && overridden.SiteIds.SequenceEqual([9]) && overridden.FirstDayOfWeek == 2, "Command line overrides the parameter files");
    Write("Holidays.json", """{ "GoogleCalendarIds": [], "CsvFiles": ["h.csv"] }""");
    File.Delete(Path.Combine(configDir, "Style.json")); File.Delete(Path.Combine(configDir, "Placement.json"));
    Check(Options.Parse(["generate", "/parameters", configDir, "/output", "o"]).CalendarIds.Count == 0, "Empty GoogleCalendarIds disables Google calendars");
    Write("Holidays.json", """{ "CsvFiles": ["h.csv"] }""");
    Check(Options.Parse(["generate", "/parameters", configDir, "/output", "o"]).CalendarIds.SequenceEqual([GoogleCalendarHolidaySource.JapaneseHolidays]), "Default calendar when the parameters name no calendar");
    Check(Options.Parse(["generate", "/output", "o", "/parameters", configDir]).Prefix == Options.DefaultPrefix, "Missing Style.json means defaults");
    Reject(() => Options.Parse(["generate", "/parameters", Path.Combine(configDir, "nothing"), "/output", "o"]), "Missing Parameters folder is rejected");
    foreach (var (file, bad) in new[]
    {
        ("Holidays.json", """{ "BearerToken": "secret" }"""),
        ("Holidays.json", """{ "IcalUrls": [{ "Url": "https://x/a.ics", "BearerToken": "secret" }] }"""),
        ("Holidays.json", """{ "IcalUrls": ["http://x/a.ics"] }"""),
        ("Holidays.json", """{ "FromYear": "2026" }"""),
        ("Style.json", """{ "Colors": { "HolidayBackground": "red; }" } }"""),
        ("Style.json", """{ "FirstDayOfWeek": 7 }"""),
        ("Placement.json", """{ "TenantId": 0 }"""),
        ("Placement.json", """[1]"""),
        ("Placement.json", """{ broken"""),
    })
    {
        Write(file, bad);
        try { Options.Parse(["generate", "/parameters", configDir, "/output", "o", "/csv", "h.csv"]); throw new Exception("FAIL: bad parameters accepted: " + bad); }
        catch (UserError e) { Check(!e.Message.Contains("secret"), "Bad parameters are rejected without echoing values"); }
        File.Delete(Path.Combine(configDir, file));
    }
}
finally { Directory.Delete(configDir, true); }

var theme = new CalendarTheme();
// 日付ごとの種類（日祝・土・平日）と、取得元の併用
var csvTyped = CsvHolidaySource.Parse("2026-10-10,振替出勤,平日\n2026-10-11,会社の土曜扱い,土\n2026-10-12,スポーツの日\n2026-10-13,x,holiday\n2026-10-14,y,SAT\n2026-10-15,z,");
Check(csvTyped.Select(h => h.Type).SequenceEqual([DayType.Weekday, DayType.Saturday, DayType.Holiday, DayType.Holiday, DayType.Saturday, DayType.Holiday]), "CSV type column: 平日 / 土 / default holiday / holiday / SAT / empty");
Reject(() => CsvHolidaySource.Parse("2026-10-10,x,unknown"), "CSV invalid type");
var d1 = new DateOnly(2026, 10, 10);
var merged = DayResolver.Resolve([new(d1, "G", DayType.Holiday), new(d1, "C", DayType.Weekday), new(d1, "G", DayType.Holiday), new(d1, "H2", DayType.Holiday), new(new DateOnly(2026, 10, 11), "S", DayType.Saturday), new(new DateOnly(2026, 10, 11), "W", DayType.Weekday), new(new DateOnly(2026, 10, 12), "W", DayType.Weekday)]);
Check(merged[d1].Type == DayType.Holiday && merged[d1].Name == "G / H2", "Priority holiday > weekday: the lower type and its name are dropped, same-type names are joined without duplicates");
Check(merged[new DateOnly(2026, 10, 11)] == new Holiday(new DateOnly(2026, 10, 11), "S", DayType.Saturday) && merged[new DateOnly(2026, 10, 12)].Type == DayType.Weekday, "Priority saturday > weekday; weekday alone stays");
var typed = CssGenerator.Generate([new(new DateOnly(2026, 10, 10), "振替出勤", DayType.Weekday), new(new DateOnly(2026, 10, 11), "土曜扱い", DayType.Saturday), new(new DateOnly(2026, 10, 12), "祝", DayType.Holiday), new(new DateOnly(2026, 10, 12), "平日にしたい", DayType.Weekday)], theme, 2026, 2026).Years[2026];
Check(typed.Contains("td.container:is(.is-weekly, :has(> div > .day):not(.other-month)):is([data-id=\"2026/10/10\"]) { background-color: transparent; }") && typed.Contains(".day { color: inherit; }"), "Weekday type resets the weekend colors for that date");
Check(typed.Contains("td.container:is(.is-weekly, :has(> div > .day):not(.other-month)):is([data-id=\"2026/10/11\"]) { background-color: " + theme.SaturdayBackground + "; }"), "Saturday type uses the Saturday colors on any date");
Check(typed.Contains("td.container:is(.is-weekly, :has(> div > .day):not(.other-month)):is([data-id=\"2026/10/12\"]) { background-color: " + theme.HolidayBackground + "; }") && !typed.Contains("平日にしたい"), "Holiday beats weekday on the same date (the weekday entry and its name are dropped)");
Check(typed.Contains("tr.fc-list-day:is(") && typed.Contains("[data-date=\"2026-01-03\"]") && typed.Contains(".fc-list-day-cushion { background-color: " + theme.SaturdayBackground + "; }") && typed.Contains(".fc-list-day-cushion { background-color: " + theme.HolidayBackground + "; }"), "FullCalendar list view: weekends are colored by date");
Check(!System.Text.RegularExpressions.Regex.IsMatch(typed, "(th|tr\\.fc-list-day)[^{}]*\\[data-date=\"2026-10-10\"\\][^{}]*\\{ (background-color|color):"), "Weekday type is not applied to FullCalendar headers or list rows (the defaults stay)");
Check(typed.Length < 30000, "Per-year size with the list view weekends stays small");

// CSS（共通部 + 年ごと）
var generated = CssGenerator.Generate([new(new DateOnly(2026, 2, 11), "B"), new(new DateOnly(2026, 1, 1), "A"), new(new DateOnly(2026, 1, 1), "A2"), new(new DateOnly(2027, 1, 1), "C")], theme, 2026, 2027);
var css = generated.Years[2026];
Check(generated.Years.Keys.SequenceEqual([2026, 2027]), "One style per year in the range");
Check(!generated.Years[2026].Contains("2027/1/1") && generated.Years[2027].Contains("[data-id=\"2027/1/1\"]") && !generated.Years[2027].Contains("2026/1/1"), "Each year file contains only its own dates");
Check(generated.Core.Contains(".fc-day-sat:where(:not(.fc-day-other)) {") && generated.Core.Contains(".fc-day-sun:where(:not(.fc-day-other)) {") && !generated.Core.Contains("data-id") && !generated.Core.Contains("data-date"), "Core holds only the FullCalendar weekday classes, no dates");
Check(!css.Contains(".fc-day-sat"), "Year files do not repeat the weekday classes");
Check(css.Contains("td.container:is(.is-weekly, :has(> div > .day):not(.other-month)):is([data-id=\"2026/1/1\"],[data-id=\"2026/2/11\"])"), "Standard monthly and weekly cells share one :is() rule, no zero padding, exclude other month (monthly) and the yearly view");
Check(css.Contains("td.container.is-weekly:is([data-id=\"2026/1/1\"],[data-id=\"2026/2/11\"])"), "Weekly cells are also grouped for the position rule");
Check(css.Contains("td.fc-daygrid-day[data-date=\"2026-01-01\"]:not(.fc-day-other) .fc-daygrid-day-top::after"), "FullCalendar day label");
Check(css.Contains("content: \"A / A2\""), "Same-type names on one date are joined with  / ");
Check(!css.Contains("[data-id=\"2026/1/3\"]") && !css.Contains("[data-id=\"2026/1/4\"]") && css.Length < 20000, "Year files hold only holidays (weekends are not listed by date), so they stay small");
Check(css.Contains(":is(#CalendarBody, [id^=\"CalendarBody\"]) td.container") && css.Contains(":is(#FullCalendar, [id^=\"FullCalendar\"]).calendar-container"), "Selectors outweigh the built-in #CalendarBody td / #FullCalendar td rules (verified on a real Pleasanter)");
Check(css.Contains("th.fc-col-header-cell:is([data-date=\"2026-01-01\"],[data-date=\"2026-02-11\"]) .fc-col-header-cell-cushion") && css.Contains(":is([data-id=\"2026/1/1\"],[data-id=\"2026/2/11\"]):not(.other-month) .day { width: auto"), "Holiday header text color and widened .day for the label");
Check(generated.Core.Contains("td.container:where(:has(> div > .day)):where(:nth-of-type(6)):where(:not(.other-month))") && generated.Core.Contains("td.container.is-weekly:where(:nth-of-type(7))"), "Core weekends use column positions (Monday start: Saturday is the 6th, Sunday the 7th td) with zero-specificity structure so holidays win");
Check(CssGenerator.Generate([], theme, 2026, 2026, 0).Core.Contains(":where(:nth-of-type(7))") && CssGenerator.Generate([], theme, 2026, 2026, 0).Core.Contains(":where(:nth-of-type(1))"), "Sunday start: Saturday is the 7th, Sunday the 1st td");
Check(CssGenerator.Generate([], theme, 2026, 2026, 6).Core.Contains(":where(:nth-of-type(1))") && CssGenerator.Generate([], theme, 2026, 2026, 6).Core.Contains(":where(:nth-of-type(2))"), "Saturday start: Saturday is the 1st, Sunday the 2nd td");
Reject(() => CssGenerator.Generate([], theme, 2026, 2026, 7), "First day of week out of range");
Check(Options.Parse(["generate", "/csv", "h.csv", "/output", "o", "/first-day-of-week", "0"]).FirstDayOfWeek == 0 && Options.Parse(["generate", "/csv", "h.csv", "/output", "o"]).FirstDayOfWeek == 1, "/first-day-of-week (default Monday)");
Reject(() => Options.Parse(["generate", "/csv", "h.csv", "/output", "o", "/first-day-of-week", "7"]), "/first-day-of-week out of range");
Check(css.Contains("position: absolute") && css.Contains("td.container.is-weekly[data-id=\"2026/1/1\"]::before"), "Weekly label is absolutely positioned so multi-day bars do not shift");
Check(css == CssGenerator.Generate([new(new DateOnly(2026, 1, 1), "A"), new(new DateOnly(2026, 1, 1), "A2"), new(new DateOnly(2026, 2, 11), "B")], theme, 2026, 2026).Years[2026], "CSS is deterministic and independent of other years");
Check(CssGenerator.EscapeString("a\"b\\c</style>\n") == "a\\\"b\\\\c\\3c /style\\3e  ", "CSS string escape");
Check(!CssGenerator.Generate([new(new DateOnly(2026, 1, 1), "")], theme, 2026, 2026).Years[2026].Contains("::after"), "CSS omits label for empty name");
Reject(() => new CalendarTheme(HolidayBackground: "red; } body { display:none").Validate(), "Color injection is rejected");

Reject(() => Options.Parse(["generate", "/csv", "h.csv", "/output", "o", "/holiday-bg", "red;x"]), "invalid color option");

// スタイル一式（接頭辞・共通部・年ごと・管理対象）
var set = StyleSet.Create("vvcsc_", generated, [], false);
Check(set.Styles.Select(s => s.Name).SequenceEqual(["vvcsc_core", "vvcsc_2026", "vvcsc_2027"]), "Style names: <prefix>core and <prefix><year>");
Check(set.Obsolete(["vvcsc_2025", "vvcsc_2026", "vvcsc_core", "other_2024", "vvcscx", "VVCSC_2020"]).SequenceEqual(["vvcsc_2025"]), "Only names starting with the prefix and not generated are obsolete");
Check(Options.Parse(["generate", "/csv", "h.csv", "/output", "o"]).Prefix == "vvcsc_" && !Options.Parse(["generate", "/csv", "h.csv", "/output", "o"]).Prune, "Default prefix, no prune");
Check(Options.Parse(["generate", "/csv", "h.csv", "/output", "o", "/prefix", "Co-1_", "/prune"]).Prune, "/prune flag");
foreach (var badPrefix in new[] { "", "_x", "a b", "a/b", "../x", new string('a', 33) })
{
    Reject(() => Options.ValidatePrefix(badPrefix), "Bad prefix is rejected: " + badPrefix);
}

Reject(() => Options.Parse(["generate", "/csv", "h.csv", "/output", "o", "/tenant-id", "1"]), "/tenant-id applies to register only");
Check(Options.Parse(["register", "/csv", "h.csv", "/url", "https://pleasanter.local", "/tenant-id", "3"]).TenantId == 3, "register accepts a tenant ID");

// Extensions API
foreach (var badUrl in new[] { "ftp://pleasanter.example.com", "http://pleasanter.local", "http://192.168.0.10", "https://user:pass@pleasanter.example.com", "https://pleasanter.example.com/?x=1", "https://pleasanter.example.com/#f", "pleasanter.example.com", "" })
{
    Reject(() => ApiStyleSink.ValidateBaseUrl(badUrl), "Pleasanter URL is rejected: " + badUrl);
    Reject(() => Options.Parse(["register", "/csv", "h.csv", "/url", badUrl]), "register rejects the URL early: " + badUrl);
}

foreach (var goodUrl in new[] { "https://pleasanter.example.com/", "https://pleasanter.example.com/pleasanter", "http://localhost:8080", "http://127.0.0.1", "http://[::1]:5000" })
{
    ApiStyleSink.ValidateBaseUrl(goodUrl);
    passed++;
}

// 平文 http の許可（PLEASANTER_ALLOW_INSECURE_HTTP / AllowInsecureHttp）。許可なしの挙動は上のとおり変わらない。
Check(!Options.ParseAllowInsecureHttp(null) && !Options.ParseAllowInsecureHttp("") && !Options.ParseAllowInsecureHttp("1") && !Options.ParseAllowInsecureHttp("false") && !Options.ParseAllowInsecureHttp("yes"), "Insecure http is off unless the value is true");
Check(Options.ParseAllowInsecureHttp("true") && Options.ParseAllowInsecureHttp("TRUE"), "Insecure http is on for true (case-insensitive)");
foreach (var insecureUrl in new[] { "http://pleasanter.local", "http://192.168.0.10:8080", "http://pleasanter:8080/pleasanter", "https://pleasanter.example.com/" })
{
    ApiStyleSink.ValidateBaseUrl(insecureUrl, true);
    passed++;
}
foreach (var alwaysBad in new[] { "ftp://pleasanter.example.com", "http://user:pass@pleasanter.local", "https://user:pass@pleasanter.example.com", "http://pleasanter.local/?x=1", "http://pleasanter.local/#f", "https://pleasanter.example.com/?x=1", "pleasanter.local", "" })
{
    Reject(() => ApiStyleSink.ValidateBaseUrl(alwaysBad, true), "Pleasanter URL is rejected even when insecure http is allowed: " + alwaysBad);
}

Reject(() => ApiStyleSink.ValidateBaseUrl("http://pleasanter.local", false), "Explicit false rejects non-loopback http");
{
    var saved = Environment.GetEnvironmentVariable(Options.AllowInsecureHttpVariable);
    try
    {
        Environment.SetEnvironmentVariable(Options.AllowInsecureHttpVariable, null);
        Check(!Options.Parse(["register", "/csv", "h.csv", "/url", "https://pleasanter.local"]).AllowInsecureHttp, "AllowInsecureHttp defaults to false");
        Reject(() => Options.Parse(["register", "/csv", "h.csv", "/url", "http://pleasanter.local"]), "register rejects non-loopback http by default");
        Reject(() => Options.Parse(["generate", "/output", "o", "/ical-url", "http://example.com/a.ics"]), "generate rejects a non-loopback http iCal URL by default");
        Environment.SetEnvironmentVariable(Options.AllowInsecureHttpVariable, "true");
        var allowed = Options.Parse(["register", "/csv", "h.csv", "/url", "http://pleasanter.local:8080"]);
        Check(allowed.AllowInsecureHttp && allowed.PleasanterUrl == "http://pleasanter.local:8080", "The environment variable allows register with a non-loopback http URL");
        Reject(() => Options.Parse(["register", "/csv", "h.csv", "/url", "http://u:p@pleasanter.local"]), "register still rejects credentials with the environment variable");
        Reject(() => Options.Parse(["register", "/csv", "h.csv", "/url", "ftp://pleasanter.local"]), "register still rejects ftp with the environment variable");
        Check((allowed with { AllowInsecureHttp = false }).AllowInsecureHttp == false, "AllowInsecureHttp can be set explicitly");
        Check(Options.Parse(["generate", "/output", "o", "/ical-url", "http://example.com/a.ics"]).IcalUrls[0].Url == "http://example.com/a.ics", "The environment variable allows a non-loopback http iCal URL");
        Reject(() => Options.Parse(["generate", "/output", "o", "/ical-url", "ftp://example.com/a.ics"]), "iCal URL still rejects ftp with the environment variable");
        using var http = new HttpClient(new FakePleasanter());
        Reject(() => new ApiStyleSink("http://pl.test/", "KEY-123", null, http, allowInsecureHttp: false).WriteAsync(StyleSet.Create("vvcsc_", generated, [], false), default).GetAwaiter().GetResult(), "The sink rejects non-loopback http when explicitly not allowed");
        var allowedFake = new FakePleasanter();
        using var allowedHttp = new HttpClient(allowedFake);
        await new ApiStyleSink("http://pl.test/", "KEY-123", null, allowedHttp).WriteAsync(StyleSet.Create("vvcsc_", generated, [], false), default);
        Check(allowedFake.Created == 3, "The sink sends to a non-loopback http URL when the environment variable allows it");
    }
    finally
    {
        Environment.SetEnvironmentVariable(Options.AllowInsecureHttpVariable, saved);
    }
}
{
    var pl = new FakePleasanter();
    using var http = new HttpClient(pl);
    var sink = new ApiStyleSink("https://pl.test/", "KEY-123", null, http);
    await sink.WriteAsync(set, default);
    Check(pl.Created == 3 && pl.Updated == 0 && pl.Items.Values.All(i => i["ExtensionType"]!.ToString() == "Style" && i["ApiKey"]!.ToString() == "KEY-123"), "Creates one Style extension per style when none exists");
    Check(pl.Items.Values.Select(i => i["ExtensionName"]!.ToString()).Order().SequenceEqual(["vvcsc_2026", "vvcsc_2027", "vvcsc_core"]) && pl.Items.Values.All(i => i["ExtensionSettings"]!["SiteIdList"] == null && i["TenantId"] == null), "Names carry the prefix; no SiteIdList and no TenantId by default");
    await sink.WriteAsync(set, default);
    Check(pl.Created == 3 && pl.Updated == 0, "Unchanged styles are not written again");
    var changed = StyleSet.Create("vvcsc_", generated with { Core = "/* new core */" }, [5], false);
    await sink.WriteAsync(changed, default);
    Check(pl.Created == 3 && pl.Updated == 3 && pl.Items.Values.All(i => i["ExtensionSettings"]!["SiteIdList"]![0]!.GetValue<long>() == 5) && pl.Items.Values.Any(i => i["Body"]!.ToString() == "/* new core */"), "Updates the existing extensions instead of creating duplicates");
    // 管理対象外（接頭辞が違う）の拡張と、管理対象で一式に含まれないものを仕込む。
    pl.AddForeign(100, "other_2024"); pl.AddForeign(101, "vvcsc_2025");
    var after = StyleSet.Create("vvcsc_", generated with { Core = "/* new core */" }, [5], false);
    await sink.WriteAsync(after, default);
    Check(pl.Items.ContainsKey(101) && pl.Items.ContainsKey(100) && pl.Deleted.Count == 0, "Without /prune nothing is deleted (a warning is shown)");
    await sink.WriteAsync(after with { Prune = true }, default);
    Check(pl.Deleted.SequenceEqual([101]) && pl.Items.ContainsKey(100) && pl.Items.Count == 4, "/prune deletes only managed styles that are no longer generated");
    var tenant = new FakePleasanter();
    using var tenantHttp = new HttpClient(tenant);
    await new ApiStyleSink("https://pl.test/", "KEY-123", 7, tenantHttp).WriteAsync(set, default);
    Check(tenant.Items.Values.All(i => i["TenantId"]!.GetValue<int>() == 7), "TenantId is sent when specified");
    pl.Mode = "forbidden";
    try { await sink.WriteAsync(set, default); throw new Exception("FAIL: 403 expected"); }
    catch (UserError e) { Check(e.Message.Contains("AllowExtensionsApi") && !e.Message.Contains("KEY-123"), "403 explains AllowExtensionsApi without leaking the API key"); }
    foreach (var (mode, expected) in new[] { ("unauthorized", "401"), ("down", "Could not connect"), ("redirect", "status 307"), ("server-error", "status 503"), ("body-429", "status 429") })
    {
        pl.Mode = mode;
        try { await sink.WriteAsync(set, default); throw new Exception("FAIL: failure expected for " + mode); }
        catch (UserError e) { Check(e.Message.Contains(expected) && !e.Message.Contains("KEY-123"), $"{mode}: reported as '{expected}' without leaking the API key"); }
    }

    pl.Mode = "";
    using var canceled = new CancellationTokenSource();
    canceled.Cancel();
    try { await sink.WriteAsync(set, canceled.Token); throw new Exception("FAIL: cancel expected"); }
    catch (OperationCanceledException) { passed++; }
    pl.Mode = "duplicate";
    try { await sink.WriteAsync(set, default); throw new Exception("FAIL: duplicate expected"); }
    catch (UserError e) { Check(e.Message.Contains("Multiple"), "Duplicate names are reported"); }
}

// 既定の HttpClient（PleasanterHttp）はリダイレクトを辿らない（307 は本文ごと再送されるため、API キーが転送先へ渡る）。ループバックの実サーバーで確かめる。
{
    static int FreePort()
    {
        var probe = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        probe.Start();
        var port = ((System.Net.IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    var (portA, portB) = (FreePort(), FreePort());
    using var a = new System.Net.HttpListener();
    using var b = new System.Net.HttpListener();
    a.Prefixes.Add($"http://127.0.0.1:{portA}/");
    b.Prefixes.Add($"http://127.0.0.1:{portB}/");
    a.Start();
    b.Start();
    var forwarded = 0;
    _ = Task.Run(async () =>
    {
        try
        {
            while (true)
            {
                var ctx = await a.GetContextAsync();
                ctx.Response.StatusCode = 307;
                ctx.Response.RedirectLocation = $"http://127.0.0.1:{portB}/api/extensions/get";
                ctx.Response.Close();
            }
        }
        catch (Exception) { }
    });
    _ = Task.Run(async () =>
    {
        try
        {
            while (true)
            {
                var ctx = await b.GetContextAsync();
                Interlocked.Increment(ref forwarded);
                ctx.Response.Close();
            }
        }
        catch (Exception) { }
    });
    try { await new ApiStyleSink($"http://127.0.0.1:{portA}", "KEY-123").WriteAsync(set, default); throw new Exception("FAIL: 307 expected"); }
    catch (UserError e) { Check(e.Message.Contains("status 307") && forwarded == 0, "The default client does not follow redirects (the API key is not forwarded)"); }
}

// File sink
var temp = Path.Combine(Path.GetTempPath(), "csc-test-" + Guid.NewGuid().ToString("N"));
try
{
    await new FileStyleSink(temp).WriteAsync(set, CancellationToken.None);
    Check(await File.ReadAllTextAsync(Path.Combine(temp, "vvcsc_2026.css")) == generated.Years[2026] && File.Exists(Path.Combine(temp, "vvcsc_core.css")) && Directory.GetFiles(temp, "*.json").Length == 0, "File sink writes one .css per style (the only form Pleasanter reads)");
    File.WriteAllText(Path.Combine(temp, "other.css"), "/* mine */");
    File.WriteAllText(Path.Combine(temp, "vvcsc_2025.css"), "/* old */");
    await new FileStyleSink(temp).WriteAsync(set, CancellationToken.None);
    Check(File.Exists(Path.Combine(temp, "vvcsc_2025.css")), "Obsolete managed file remains without /prune");
    await new FileStyleSink(temp).WriteAsync(set with { Prune = true }, CancellationToken.None);
    Check(!File.Exists(Path.Combine(temp, "vvcsc_2025.css")) && File.ReadAllText(Path.Combine(temp, "other.css")) == "/* mine */" && File.Exists(Path.Combine(temp, "vvcsc_2027.css")), "/prune deletes only managed files no longer generated and never touches other files");
    Reject(() => new FileStyleSink(temp).WriteAsync(StyleSet.Create("vvcsc_", generated, [5], false), CancellationToken.None).GetAwaiter().GetResult(), "File sink rejects a site limit");
    Reject(() => new FileStyleSink(temp).WriteAsync(new("p", [new("../x", "d", [], "x")], false), CancellationToken.None).GetAwaiter().GetResult(), "File sink rejects path in name");
}
finally { if (Directory.Exists(temp))
    {
        Directory.Delete(temp, true);
    }
}

Console.WriteLine($"{passed} checks passed.");

// https が必須のため、実サーバーの代わりにメッセージハンドラーで応答する。
sealed class FakeIcalHandler : HttpMessageHandler
{
    public string? LastAuthorization { get; private set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel)
    {
        LastAuthorization = request.Headers.Authorization?.ToString();
        if (LastAuthorization == null)
        {
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized));
        }

        return Task.FromResult(new HttpResponseMessage { Content = new StringContent("BEGIN:VEVENT\r\nDTSTART;VALUE=DATE:20260101\r\nSUMMARY:x\r\nEND:VEVENT\r\n") });
    }
}

// Extensions API の最小の模擬。Style 種別の拡張を複数保持し、get・create・update・delete に応える。
sealed class FakePleasanter : HttpMessageHandler
{
    private int nextId = 1;
    public Dictionary<int, System.Text.Json.Nodes.JsonObject> Items = [];
    public List<int> Deleted = [];
    public int Created, Updated;
    public string Mode = "";
    public void AddForeign(int id, string name) => Items[id] = new() { ["ExtensionType"] = "Style", ["ExtensionName"] = name, ["Body"] = "/* other */", ["ExtensionSettings"] = new System.Text.Json.Nodes.JsonObject { ["Name"] = name }.ToJsonString() };
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();
        var path = request.RequestUri!.AbsolutePath;
        var body = (System.Text.Json.Nodes.JsonObject)System.Text.Json.Nodes.JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancel))!;
        static HttpResponseMessage Json(string json) => new() { Content = new StringContent(json) };
        if (Mode == "unauthorized")
        {
            return Json("""{"StatusCode":401,"Message":"Unauthorized"}""");
        }

        if (Mode == "body-429")
        {
            return Json("""{"StatusCode":429,"Message":"Too many"}""");
        }

        if (Mode == "down")
        {
            throw new HttpRequestException("connection refused");
        }

        if (Mode == "redirect")
        {
            return new HttpResponseMessage(System.Net.HttpStatusCode.TemporaryRedirect);
        }

        if (Mode == "server-error")
        {
            return new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable) { Content = new StringContent("<html>") };
        }

        if (Mode == "forbidden")
        {
            return Json("""{"StatusCode":403,"Message":"Forbidden"}""");
        }

        if (path.EndsWith("/get"))
        {
            var items = new System.Text.Json.Nodes.JsonArray();
            foreach (var (id, item) in Items)
            {
                items.Add(Item(id, item));
            }

            if (Mode == "duplicate" && Items.Count > 0)
            {
                items.Add(Item(999, Items.Values.First()));
            }

            return Json(new System.Text.Json.Nodes.JsonObject { ["StatusCode"] = 200, ["Response"] = new System.Text.Json.Nodes.JsonObject { ["Data"] = items } }.ToJsonString());
        }
        if (path.EndsWith("/create")) { Created++; Items[nextId++] = body; return Json("""{"Id":1,"StatusCode":200,"Message":"ok"}"""); }
        var id2 = int.Parse(path.Split('/')[^2]);
        if (path.EndsWith("/update")) { Updated++; Items[id2] = body; }
        else if (path.EndsWith("/delete")) { Deleted.Add(id2); Items.Remove(id2); }
        return Json("""{"Id":1,"StatusCode":200,"Message":"ok"}""");
    }
    private static System.Text.Json.Nodes.JsonObject Item(int id, System.Text.Json.Nodes.JsonObject stored) => new()
    {
        ["ExtensionId"] = id,
        ["ExtensionType"] = "Style",
        ["ExtensionName"] = stored["ExtensionName"]!.ToString(),
        ["Body"] = stored["Body"]?.ToString(),
        // 本体は ExtensionSettings を JSON 文字列として返すことがあるため、文字列で返して比較の両対応を確認する。
        ["ExtensionSettings"] = stored["ExtensionSettings"] is System.Text.Json.Nodes.JsonValue v ? v.ToString() : stored["ExtensionSettings"]?.ToJsonString(),
    };
}
