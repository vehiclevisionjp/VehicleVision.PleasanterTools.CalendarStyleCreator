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

// CSS
var css = CssGenerator.Generate([new(new DateOnly(2026, 2, 11), "B"), new(new DateOnly(2026, 1, 1), "A"), new(new DateOnly(2026, 1, 1), "A2")]);
Check(css.IndexOf("2026/1/1", StringComparison.Ordinal) < css.IndexOf("2026/2/11", StringComparison.Ordinal), "CSS sorted by date");
Check(css.Contains("td.container[data-id=\"2026/1/1\"] .day::after { content: \" A2\""), "CSS last duplicate wins and date is not zero padded");
Check(!css.Contains("\" A\";"), "CSS drops overridden duplicate");
Check(css == CssGenerator.Generate([new(new DateOnly(2026, 1, 1), "A2"), new(new DateOnly(2026, 2, 11), "B")]), "CSS is deterministic");
Check(CssGenerator.EscapeString("a\"b\\c</style>\n") == "a\\\"b\\\\c\\3c /style\\3e  ", "CSS string escape");
Check(!CssGenerator.Generate([new(new DateOnly(2026, 1, 1), "")]).Contains("::after"), "CSS omits label for empty name");

// Options
var parsed = Options.Parse(["generate", "/csv", "/tmp/h.csv", "/output", "out", "/site-id", "1, 2,2"]);
Check(parsed.Action == "generate" && parsed.CsvPath == "/tmp/h.csv" && parsed.SiteIds.SequenceEqual([1, 2]), "Options parse");
Check(Options.Parse([]).Action == "help", "Options help");
Reject(() => Options.Parse(["generate", "/csv", "h.csv"]), "generate requires /output");
Reject(() => Options.Parse(["register", "/csv", "h.csv"]), "register requires /url");
Reject(() => Options.Parse(["generate", "/output", "out"]), "source required");
Reject(() => Options.Parse(["generate", "/csv", "h.csv", "/output", "o", "/from", "2027", "/to", "2026"]), "year range");
Reject(() => Options.Parse(["generate", "/bogus", "x"]), "unknown option");

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
