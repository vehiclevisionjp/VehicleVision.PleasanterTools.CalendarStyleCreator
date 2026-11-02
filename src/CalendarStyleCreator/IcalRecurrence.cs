using System.Globalization;

namespace VehicleVision.PleasanterTools.CalendarStyleCreator;

/// <summary>
/// iCal の繰り返し規則（RRULE。RFC 5545 3.3.10）の最小の展開。外部パッケージは使わない。
/// 対応: <c>FREQ=YEARLY|MONTHLY|WEEKLY|DAILY</c>、<c>INTERVAL</c>、<c>COUNT</c>、<c>UNTIL</c>、<c>BYMONTH</c>、
/// <c>BYMONTHDAY</c>（負の値も可）、<c>BYDAY</c>（序数なし）、<c>WKST</c>（無視。週の開始は月曜）。
/// それ以外の規則は <see cref="TryParse"/> が <c>false</c> を返す（呼び出し側が警告して最初の 1 回だけ使う）。
/// </summary>
public sealed class IcalRecurrence
{
    /// <summary>1 つの予定が展開できる回数の上限（暴走を避ける）。</summary>
    public const int MaxOccurrences = 1000;

    private static readonly Dictionary<string, DayOfWeek> Days = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MO"] = DayOfWeek.Monday, ["TU"] = DayOfWeek.Tuesday, ["WE"] = DayOfWeek.Wednesday, ["TH"] = DayOfWeek.Thursday,
        ["FR"] = DayOfWeek.Friday, ["SA"] = DayOfWeek.Saturday, ["SU"] = DayOfWeek.Sunday,
    };

    private string _freq = "";
    private int _interval = 1;
    private int? _count;
    private DateOnly? _until;
    private HashSet<int> _byMonth = [];
    private List<int> _byMonthDay = [];
    private HashSet<DayOfWeek> _byDay = [];

    /// <summary>規則を読む。対応外・不正なら <c>false</c>（<paramref name="reason"/> に英語の理由）。</summary>
    public static bool TryParse(string rule, out IcalRecurrence? result, out string reason)
    {
        result = null;
        reason = "";
        var r = new IcalRecurrence();
        foreach (var part in rule.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = part.IndexOf('=');
            if (eq <= 0)
            {
                reason = "malformed RRULE";
                return false;
            }

            var key = part[..eq].ToUpperInvariant();
            var value = part[(eq + 1)..];
            switch (key)
            {
                case "FREQ":
                    r._freq = value.ToUpperInvariant();
                    if (r._freq is not ("YEARLY" or "MONTHLY" or "WEEKLY" or "DAILY"))
                    {
                        reason = $"FREQ={value} is not supported";
                        return false;
                    }

                    break;
                case "INTERVAL":
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out r._interval) || r._interval < 1)
                    {
                        reason = "invalid INTERVAL";
                        return false;
                    }

                    break;
                case "COUNT":
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var count) || count < 1)
                    {
                        reason = "invalid COUNT";
                        return false;
                    }

                    r._count = count;
                    break;
                case "UNTIL":
                    if (value.Length < 8 || !DateOnly.TryParseExact(value[..8], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var until))
                    {
                        reason = "invalid UNTIL";
                        return false;
                    }

                    r._until = until;
                    break;
                case "BYMONTH":
                    foreach (var v in value.Split(','))
                    {
                        if (!int.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out var m) || m is < 1 or > 12)
                        {
                            reason = "invalid BYMONTH";
                            return false;
                        }

                        r._byMonth.Add(m);
                    }

                    break;
                case "BYMONTHDAY":
                    foreach (var v in value.Split(','))
                    {
                        if (!int.TryParse(v, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var d) || d == 0 || Math.Abs(d) > 31)
                        {
                            reason = "invalid BYMONTHDAY";
                            return false;
                        }

                        r._byMonthDay.Add(d);
                    }

                    break;
                case "BYDAY":
                    foreach (var v in value.Split(','))
                    {
                        if (!Days.TryGetValue(v, out var day))
                        {
                            reason = "BYDAY with an ordinal is not supported";
                            return false;
                        }

                        r._byDay.Add(day);
                    }

                    break;
                case "WKST":
                    break;
                default:
                    reason = $"{key} is not supported";
                    return false;
            }
        }

        if (r._freq.Length == 0)
        {
            reason = "FREQ is missing";
            return false;
        }

        result = r;
        return true;
    }

    /// <summary>
    /// 開始日 <paramref name="first"/>（最初の 1 回。規則に合わなくても含める）から <paramref name="last"/> までの、
    /// 各回の開始日を返す。<paramref name="truncated"/> は回数の上限で切ったか。
    /// </summary>
    public IReadOnlyList<DateOnly> Expand(DateOnly first, DateOnly last, out bool truncated)
    {
        truncated = false;
        var end = _until is { } until && until < last ? until : last;
        var result = new List<DateOnly> { first };
        if (_count == 1)
        {
            return result;
        }

        for (var day = first.AddDays(1); day <= end; day = day.AddDays(1))
        {
            if (!Matches(first, day))
            {
                continue;
            }

            if (result.Count >= MaxOccurrences)
            {
                truncated = true;
                break;
            }

            result.Add(day);
            if (_count is { } count && result.Count >= count)
            {
                break;
            }
        }

        return result;
    }

    private bool Matches(DateOnly first, DateOnly day)
    {
        switch (_freq)
        {
            case "DAILY":
                if ((day.DayNumber - first.DayNumber) % _interval != 0)
                {
                    return false;
                }

                return MonthOk(day) && MonthDayOk(day, null) && DayOk(day, null);
            case "WEEKLY":
                if (WeekIndex(day) - WeekIndex(first) is var weeks && weeks % _interval != 0)
                {
                    return false;
                }

                return MonthOk(day) && MonthDayOk(day, null) && DayOk(day, first.DayOfWeek);
            case "MONTHLY":
                if (((day.Year - first.Year) * 12 + day.Month - first.Month) % _interval != 0)
                {
                    return false;
                }

                return MonthOk(day) && MonthDayOk(day, _byDay.Count == 0 ? first.Day : null) && DayOk(day, null);
            default: // YEARLY
                if ((day.Year - first.Year) % _interval != 0)
                {
                    return false;
                }

                var hasDayRule = _byMonthDay.Count > 0 || _byDay.Count > 0;
                var monthOk = _byMonth.Count > 0 ? _byMonth.Contains(day.Month) : (hasDayRule || day.Month == first.Month);
                var defaultDay = hasDayRule ? (int?)null : first.Day;
                return monthOk && MonthDayOk(day, _byDay.Count == 0 ? defaultDay : null) && DayOk(day, null);
        }
    }

    private bool MonthOk(DateOnly day) => _byMonth.Count == 0 || _byMonth.Contains(day.Month);

    /// <summary>BYMONTHDAY に合うか。指定が無ければ <paramref name="defaultDay"/>（無ければ制限なし）。</summary>
    private bool MonthDayOk(DateOnly day, int? defaultDay)
    {
        if (_byMonthDay.Count == 0)
        {
            return defaultDay is null || day.Day == defaultDay;
        }

        var length = DateTime.DaysInMonth(day.Year, day.Month);
        return _byMonthDay.Any(d => (d > 0 ? d : length + d + 1) == day.Day);
    }

    /// <summary>BYDAY に合うか。指定が無ければ <paramref name="defaultWeekday"/>（無ければ制限なし）。</summary>
    private bool DayOk(DateOnly day, DayOfWeek? defaultWeekday) =>
        _byDay.Count == 0 ? defaultWeekday is null || day.DayOfWeek == defaultWeekday : _byDay.Contains(day.DayOfWeek);

    /// <summary>月曜始まりの週の通し番号。</summary>
    private static int WeekIndex(DateOnly day) => (day.DayNumber - (((int)day.DayOfWeek + 6) % 7)) / 7;
}
