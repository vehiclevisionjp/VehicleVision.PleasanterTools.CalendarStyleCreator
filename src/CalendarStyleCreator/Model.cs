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
    Task WriteAsync(StyleDefinition style, CancellationToken cancel);
}

/// <summary>拡張スタイル 1 件分。Name は配置先で同名を更新する際のキーになる。</summary>
public sealed record StyleDefinition(string Name, string Description, IReadOnlyList<long> SiteIds, string Css);
