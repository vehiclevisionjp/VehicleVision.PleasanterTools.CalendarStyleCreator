using System.Globalization;

namespace VehicleVision.PleasanterTools.CalendarStyleCreator;

/// <summary>
/// 「日付,名前」の CSV から休日を読む。内閣府「国民の祝日」CSV（syukujitsu.csv）の形式をそのまま読める
/// （https://www8.cao.go.jp/chosei/shukujitsu/syukujitsu.csv、Shift_JIS・ヘッダー 1 行・y/M/d、1955 年〜、確認 2026-10-16）。
/// 文字コードは UTF-8（BOM 有無）を優先し、UTF-8 として不正なら Shift_JIS（CP932）として読む。
/// 日付は yyyy-MM-dd / yyyy/M/d。ヘッダー行・空行・# 始まりの行は読み飛ばす。名前に , を含める場合は "..." で囲む（"" は " のエスケープ）。
/// </summary>
public sealed class CsvHolidaySource(string path) : IHolidaySource
{
    private static readonly string[] DateFormats = ["yyyy-MM-dd", "yyyy-M-d", "yyyy/MM/dd", "yyyy/M/d"];

    public async Task<IReadOnlyList<Holiday>> GetHolidaysAsync(CancellationToken cancel)
    {
        if (!File.Exists(path)) throw new UserError("CSV file was not found.");
        return Parse(Decode(await File.ReadAllBytesAsync(path, cancel)));
    }

    public static string Decode(byte[] bytes)
    {
        try { return new System.Text.UTF8Encoding(false, true).GetString(bytes); }
        catch (System.Text.DecoderFallbackException)
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            return System.Text.Encoding.GetEncoding(932).GetString(bytes);
        }
    }

    public static IReadOnlyList<Holiday> Parse(string text)
    {
        var holidays = new List<Holiday>();
        var lineNumber = 0;
        foreach (var line in text.Split('\n'))
        {
            lineNumber++;
            var row = line.TrimEnd('\r');
            if (lineNumber == 1) row = row.TrimStart('﻿');
            if (string.IsNullOrWhiteSpace(row) || row.TrimStart().StartsWith('#')) continue;
            var fields = SplitFields(row, lineNumber);
            if (fields.Count < 2) throw new UserError($"CSV line {lineNumber}: expected 'date,name'.");
            if (!DateOnly.TryParseExact(fields[0].Trim(), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                if (holidays.Count == 0 && lineNumber == 1) continue; // ヘッダー行
                throw new UserError($"CSV line {lineNumber}: invalid date.");
            }
            holidays.Add(new Holiday(date, fields[1].Trim()));
        }
        return holidays;
    }

    private static List<string> SplitFields(string row, int lineNumber)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        var quoted = false;
        for (var i = 0; i < row.Length; i++)
        {
            var c = row[i];
            if (quoted)
            {
                if (c != '"') current.Append(c);
                else if (i + 1 < row.Length && row[i + 1] == '"') { current.Append('"'); i++; }
                else quoted = false;
            }
            else if (c == '"' && current.Length == 0) quoted = true;
            else if (c == ',') { fields.Add(current.ToString()); current.Clear(); }
            else current.Append(c);
        }
        if (quoted) throw new UserError($"CSV line {lineNumber}: unterminated quote.");
        fields.Add(current.ToString());
        return fields;
    }
}
