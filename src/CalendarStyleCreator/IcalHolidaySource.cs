using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;

namespace VehicleVision.PleasanterTools.CalendarStyleCreator;

/// <summary>iCal 取得時の認証。Bearer トークン、または Basic（ユーザー名とパスワード）。どちらも環境変数から渡し、ログや例外に出さない。</summary>
public sealed record IcalAuth(string? BearerToken = null, string? BasicUser = null, string? BasicPassword = null)
{
    public bool IsEmpty => BearerToken == null && BasicUser == null;

    public static IcalAuth FromEnvironment(Func<string, string?> get)
    {
        var token = get(Options.IcalBearerTokenVariable);
        var user = get(Options.IcalBasicUserVariable);
        if (!string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(user)) throw new UserError($"Set either {Options.IcalBearerTokenVariable} or {Options.IcalBasicUserVariable}, not both.");
        if (!string.IsNullOrEmpty(user)) return new(BasicUser: user, BasicPassword: get(Options.IcalBasicPasswordVariable) ?? "");
        return new(BearerToken: string.IsNullOrEmpty(token) ? null : token);
    }

    public AuthenticationHeaderValue? ToHeader() =>
        BearerToken != null ? new("Bearer", BearerToken)
        : BasicUser != null ? new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{BasicUser}:{BasicPassword}")))
        : null;
}

/// <summary>
/// URL で指定した iCal（.ics）から休日を取得する。終日予定の DTSTART を日付として使い、RFC 5545 の折り返し行は連結する。
/// https のみ許可（資格情報の平文送信を避ける）。認証を付ける場合はリダイレクトを追従せず、別ホストへ資格情報を送らない。
/// URL 自体が秘密（Google の非公開 iCal アドレスなど）の場合があるため、URL はメッセージに出さない。
/// </summary>
public sealed class IcalHolidaySource(string url, IcalAuth? auth, int fromYear, int toYear, HttpClient? client = null) : IHolidaySource
{
    public static void ValidateUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) throw new UserError("iCal URL must be an absolute https URL.");
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
            if ((int)response.StatusCode is >= 300 and < 400 && header != null) throw new UserError("The iCal URL redirected; credentials are not sent to redirects. Use the final URL.");
            if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden) throw new UserError($"The iCal URL returned HTTP {(int)response.StatusCode}. Check the credentials ({Options.IcalBearerTokenVariable} or {Options.IcalBasicUserVariable}).");
            if (!response.IsSuccessStatusCode) throw new UserError($"The iCal URL returned HTTP {(int)response.StatusCode}.");
            return Parse(await response.Content.ReadAsStringAsync(cancel), fromYear, toYear);
        }
    }

    public static IReadOnlyList<Holiday> Parse(string ics, int fromYear, int toYear)
    {
        var unfolded = Regex.Replace(ics, @"\r?\n[ \t]", "");
        var holidays = new List<Holiday>();
        foreach (Match block in Regex.Matches(unfolded, @"BEGIN:VEVENT\r?\n(.*?)END:VEVENT", RegexOptions.Singleline))
        {
            var body = block.Groups[1].Value;
            if (Property(body, "STATUS").Equals("CANCELLED", StringComparison.OrdinalIgnoreCase)) continue;
            var start = Regex.Match(Property(body, "DTSTART"), @"^(\d{4})(\d{2})(\d{2})");
            if (!start.Success) continue;
            var date = new DateOnly(int.Parse(start.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(start.Groups[2].Value, CultureInfo.InvariantCulture), int.Parse(start.Groups[3].Value, CultureInfo.InvariantCulture));
            if (date.Year < fromYear || date.Year > toYear) continue;
            holidays.Add(new Holiday(date, Unescape(Property(body, "SUMMARY"))));
        }
        return holidays;
    }

    private static string Property(string block, string name)
    {
        var match = Regex.Match(block, "^" + name + @"(?:;[^:\r\n]*)?:(.*?)\r?$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : "";
    }

    private static string Unescape(string value) => Regex.Replace(value, @"\\([nN,;\\])", m => m.Groups[1].Value is "n" or "N" ? " " : m.Groups[1].Value);
}
