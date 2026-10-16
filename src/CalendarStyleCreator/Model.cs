namespace VehicleVision.PleasanterTools.CalendarStyleCreator;

/// <summary>利用者の入力・設定が原因のエラー。メッセージはそのままコンソールへ出す（秘密情報を含めない）。</summary>
public sealed class UserError(string message) : Exception(message);

/// <summary>休日 1 日分。日付は暦日（時刻とタイムゾーンを持たない）。</summary>
public sealed record Holiday(DateOnly Date, string Name);

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
