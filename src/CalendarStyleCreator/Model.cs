namespace VehicleVision.PleasanterTools.CalendarStyleCreator;

/// <summary>利用者の入力・設定が原因のエラー。メッセージはそのままコンソールへ出す（秘密情報を含めない）。</summary>
public sealed class UserError(string message) : Exception(message);

/// <summary>
/// 日付ごとの色の種類。値が大きいほど優先順位が高い（日祝 &gt; 土 &gt; 平日）。
/// 日祝は日曜・祝日と同じ色、土は土曜と同じ色、平日は土日の色を打ち消して通常の色にする（土曜の振替出勤日など）。
/// </summary>
public enum DayType { Weekday = 0, Saturday = 1, Holiday = 2 }

/// <summary>日付ごとの指定 1 件（休日・名称・色の種類）。日付は暦日（時刻とタイムゾーンを持たない）。取得元（Google・iCal）は日祝、CSV は種類の列で指定する。</summary>
public sealed record Holiday(DateOnly Date, string Name, DayType Type = DayType.Holiday);

public static class DayResolver
{
    /// <summary>
    /// 取得元をまたいで日付ごとに 1 件へまとめる。同じ日付に複数あるときは、最も優先順位の高い種類（日祝 &gt; 土 &gt; 平日）だけを残し、
    /// 低い種類は名称ごと捨てる。同じ種類の名称は、出てきた順に「 / 」でつなぐ（重複は 1 つにする）。
    /// </summary>
    public static Dictionary<DateOnly, Holiday> Resolve(IEnumerable<Holiday> entries)
    {
        var result = new Dictionary<DateOnly, Holiday>();
        foreach (var group in entries.GroupBy(e => e.Date))
        {
            var type = group.Max(e => e.Type);
            var names = group.Where(e => e.Type == type).Select(e => e.Name).Where(n => n.Length > 0).Distinct(StringComparer.Ordinal);
            result[group.Key] = new Holiday(group.Key, string.Join(" / ", names), type);
        }
        return result;
    }
}

/// <summary>休日の取得元。Google カレンダーと CSV を同じ形で扱う。</summary>
public interface IHolidaySource
{
    Task<IReadOnlyList<Holiday>> GetHolidaysAsync(CancellationToken cancel);
}

/// <summary>生成した CSS の配置先。ファイル出力と Extensions API 登録を同じ入力で切り替える。</summary>
public interface IStyleSink
{
    Task WriteAsync(StyleSet set, CancellationToken cancel);
}

/// <summary>拡張スタイル 1 件分。Name は配置先で同名を更新する際のキーになる。</summary>
public sealed record StyleDefinition(string Name, string Description, IReadOnlyList<long> SiteIds, string Css);

/// <summary>
/// 1 回の実行で配置するスタイルの一式（共通部 + 年ごと）。Prefix で始まる名前だけをこのツールの管理対象とし、
/// 一式に含まれなくなった管理対象（年を過ぎて生成しなくなったものなど）は、Prune のときだけ削除する（そうでなければ一覧を警告で出す）。
/// </summary>
public sealed record StyleSet(string Prefix, IReadOnlyList<StyleDefinition> Styles, bool Prune)
{
    public static StyleSet Create(string prefix, CssGenerator.GeneratedStyles generated, IReadOnlyList<long> siteIds, bool prune)
    {
        var styles = new List<StyleDefinition> { new(CoreName(prefix), "Calendar holiday styles: core (generated)", siteIds, generated.Core) };
        styles.AddRange(generated.Years.Select(y => new StyleDefinition(YearName(prefix, y.Key), $"Calendar holiday styles: {y.Key} (generated)", siteIds, y.Value)));
        return new(prefix, styles, prune);
    }

    public static string CoreName(string prefix) => prefix + "core";

    public static string YearName(string prefix, int year) => prefix + year.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>管理対象の名前（接頭辞で始まる）で、この一式に含まれないもの。</summary>
    public IReadOnlyList<string> Obsolete(IEnumerable<string> existingNames) =>
        existingNames.Where(n => n.StartsWith(Prefix, StringComparison.Ordinal) && Styles.All(s => s.Name != n)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
}
