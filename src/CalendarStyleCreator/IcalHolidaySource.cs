using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;

namespace VehicleVision.PleasanterTools.CalendarStyleCreator;

/// <summary>iCal 取得時の認証。Bearer トークン、または Basic（ユーザー名とパスワード）。どちらも環境変数から渡し、ログや例外に出さない。</summary>
public sealed record IcalAuth(string? BearerToken = null, string? BasicUser = null, string? BasicPassword = null)
{
    public bool IsEmpty => BearerToken == null && BasicUser == null;

    /// <summary>
    /// 取得先の認証を環境変数から組み立てる。環境変数名を明示した取得先（設定ファイル）は、未設定ならエラーにする。
    /// 名前を省略した取得先は共通の既定の環境変数を見て、未設定なら認証なし。
    /// </summary>
    public static IcalAuth FromEnvironment(Func<string, string?> get, IcalEndpoint? endpoint = null)
    {
        var explicitNames = endpoint?.HasAuthReference == true;
        var tokenName = endpoint?.BearerTokenEnv ?? (explicitNames ? null : Options.IcalBearerTokenVariable);
        var userName = endpoint?.BasicUserEnv ?? (explicitNames ? null : Options.IcalBasicUserVariable);
        var passwordName = endpoint?.BasicPasswordEnv ?? (explicitNames ? null : Options.IcalBasicPasswordVariable);
        string? Read(string? name, bool required)
        {
            if (name == null)
            {
                return null;
            }

            var value = get(name);
            if (string.IsNullOrEmpty(value) && required)
            {
                throw new UserError($"Environment variable {name} is not set.");
            }

            return string.IsNullOrEmpty(value) ? null : value;
        }
        var token = Read(tokenName, explicitNames);
        var user = Read(userName, explicitNames);
        if (token != null && user != null)
        {
            throw new UserError($"Set either {tokenName} or {userName}, not both.");
        }

        if (user != null)
        {
            return new(BasicUser: user, BasicPassword: Read(passwordName, false) ?? "");
        }

        return new(BearerToken: token);
    }

    public AuthenticationHeaderValue? ToHeader() =>
        BearerToken != null ? new("Bearer", BearerToken)
        : BasicUser != null ? new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{BasicUser}:{BasicPassword}")))
        : null;
}

/// <summary>
/// URL で指定した iCal（.ics）から休日を取得する。終日予定の DTSTART を日付として使い、RFC 5545 の折り返し行は連結する。
/// https のみ許可（資格情報の平文送信を避ける。ループバックの検証用だけ http 可）。認証を付ける場合はリダイレクトを追従せず、別ホストへ資格情報を送らない。
/// URL 自体が秘密（Google の非公開 iCal アドレスなど）の場合があるため、URL はメッセージに出さない。
/// </summary>
public sealed class IcalHolidaySource(string url, IcalAuth? auth, int fromYear, int toYear, HttpClient? client = null) : IHolidaySource
{
    public static void ValidateUrl(string url)
    {
        // https のみ。ループバック（検証用の手元のサーバー）だけ http を許可する。
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !(uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)))
        {
            throw new UserError("iCal URL must be an absolute https URL (http is allowed only for localhost).");
        }
    }

    public async Task<IReadOnlyList<Holiday>> GetHolidaysAsync(CancellationToken cancel)
    {
        ValidateUrl(url);
        var header = auth?.ToHeader();
        using var owned = client == null ? new HttpClient(new HttpClientHandler { AllowAutoRedirect = header == null }) { Timeout = TimeSpan.FromSeconds(30) } : null;
        using var request = new HttpRequestMessage(HttpMethod.Get, url) { Headers = { Authorization = header } };
        HttpResponseMessage response;
        try { response = await (client ?? owned!).SendAsync(request, cancel); }
        catch (HttpRequestException) { throw new UserError("Could not connect to the iCal URL."); }
        using (response)
        {
            if ((int)response.StatusCode is >= 300 and < 400 && header != null)
            {
                throw new UserError("The iCal URL redirected; credentials are not sent to redirects. Use the final URL.");
            }

            if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            {
                throw new UserError($"The iCal URL returned HTTP {(int)response.StatusCode}. Check the credentials for this URL (environment variables, see help).");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new UserError($"The iCal URL returned HTTP {(int)response.StatusCode}.");
            }

            return Parse(await response.Content.ReadAsStringAsync(cancel), fromYear, toYear);
        }
    }

    private sealed record IcalEvent(string Uid, DateOnly First, bool Timed, string Name, int Days, string RRule, List<DateOnly> ExDates, DateOnly? RecurrenceId, bool Cancelled);

    public static IReadOnlyList<Holiday> Parse(string ics, int fromYear, int toYear)
    {
        var unfolded = Regex.Replace(ics, @"\r?\n[ \t]", "");
        var events = new List<IcalEvent>();
        foreach (Match block in Regex.Matches(unfolded, @"BEGIN:VEVENT\r?\n(.*?)END:VEVENT", RegexOptions.Singleline))
        {
            var body = block.Groups[1].Value;
            var start = Regex.Match(Property(body, "DTSTART"), @"^(\d{4})(\d{2})(\d{2})(T.*)?$");
            if (!start.Success)
            {
                continue;
            }

            var first = ToDate(start);
            var name = Unescape(Property(body, "SUMMARY"));

            // 終日予定（日付だけの DTSTART）は、DTEND（その日を含まない）または DURATION までの各日を休日にする。
            // 時間指定の予定は開始日の 1 日だけ
            var days = start.Groups[4].Success ? 1 : SpanDays(Property(body, "DTEND"), Property(body, "DURATION"), first);
            if (days > MaxSpanDays)
            {
                RuntimeLog.WriteLine($"WARNING: An iCal event \"{name}\" spans {days} days; only the first {MaxSpanDays} days are used.");
                days = MaxSpanDays;
            }

            var exDates = new List<DateOnly>();
            foreach (Match ex in Regex.Matches(body, @"^EXDATE(?:;[^:\r\n]*)?:(.*?)\r?$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
            {
                foreach (var value in ex.Groups[1].Value.Split(','))
                {
                    var d = Regex.Match(value.Trim(), @"^(\d{4})(\d{2})(\d{2})");
                    if (d.Success)
                    {
                        exDates.Add(ToDate(d));
                    }
                }
            }

            var recurrenceId = Regex.Match(Property(body, "RECURRENCE-ID"), @"^(\d{4})(\d{2})(\d{2})");
            events.Add(new IcalEvent(
                Property(body, "UID"), first, start.Groups[4].Success, name, days, Property(body, "RRULE"), exDates,
                recurrenceId.Success ? ToDate(recurrenceId) : null,
                Property(body, "STATUS").Equals("CANCELLED", StringComparison.OrdinalIgnoreCase)));
        }

        // RECURRENCE-ID は、同じ UID の元の回を置き換える（取り消しなら除く）。置き換えた回は自分の DTSTART で出る
        var replaced = events.Where(e => e.RecurrenceId is not null && e.Uid.Length > 0)
            .Select(e => (e.Uid, e.RecurrenceId!.Value)).ToHashSet();

        var holidays = new List<Holiday>();
        var windowEnd = new DateOnly(toYear, 12, 31);
        foreach (var e in events)
        {
            if (e.Cancelled)
            {
                continue;
            }

            IReadOnlyList<DateOnly> starts = [e.First];
            if (e.RRule.Length > 0 && e.RecurrenceId is null)
            {
                if (IcalRecurrence.TryParse(e.RRule, out var rule, out var reason))
                {
                    starts = rule!.Expand(e.First, windowEnd, out var truncated);
                    if (truncated)
                    {
                        RuntimeLog.WriteLine($"WARNING: An iCal recurring event \"{e.Name}\" has more than {IcalRecurrence.MaxOccurrences} occurrences; the rest are ignored.");
                    }
                }
                else
                {
                    RuntimeLog.WriteLine($"WARNING: An iCal recurring event \"{e.Name}\" is used only once ({reason}).");
                }
            }

            foreach (var s in starts)
            {
                if (e.ExDates.Contains(s) || (e.RecurrenceId is null && replaced.Contains((e.Uid, s))))
                {
                    continue;
                }

                for (var i = 0; i < e.Days; i++)
                {
                    var date = s.AddDays(i);
                    if (date.Year >= fromYear && date.Year <= toYear)
                    {
                        holidays.Add(new Holiday(date, e.Name));
                    }
                }
            }
        }

        return holidays;
    }

    private static DateOnly ToDate(Match m) =>
        new(int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture), int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture));

    /// <summary>1 つの終日予定が展開できる日数の上限（暴走を避ける）。</summary>
    public const int MaxSpanDays = 366;

    /// <summary>終日予定の日数。DTEND（排他的）か DURATION（日・週）で決まる。無い・不正・開始以前なら 1 日。</summary>
    private static int SpanDays(string dtEnd, string duration, DateOnly first)
    {
        var end = Regex.Match(dtEnd, @"^(\d{4})(\d{2})(\d{2})$");
        if (end.Success)
        {
            var last = new DateOnly(int.Parse(end.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(end.Groups[2].Value, CultureInfo.InvariantCulture), int.Parse(end.Groups[3].Value, CultureInfo.InvariantCulture));
            return Math.Max(1, last.DayNumber - first.DayNumber);
        }

        var span = Regex.Match(duration, @"^P(?:(\d+)W)?(?:(\d+)D)?$");
        if (span.Success && (span.Groups[1].Success || span.Groups[2].Success))
        {
            var weeks = span.Groups[1].Success ? long.Parse(span.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
            var extra = span.Groups[2].Success ? long.Parse(span.Groups[2].Value, CultureInfo.InvariantCulture) : 0;
            return (int)Math.Clamp(weeks * 7 + extra, 1, int.MaxValue);
        }

        return 1;
    }

    private static string Property(string block, string name)
    {
        var match = Regex.Match(block, "^" + name + @"(?:;[^:\r\n]*)?:(.*?)\r?$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : "";
    }

    private static string Unescape(string value) => Regex.Replace(value, @"\\([nN,;\\])", m => m.Groups[1].Value is "n" or "N" ? " " : m.Groups[1].Value);
}
