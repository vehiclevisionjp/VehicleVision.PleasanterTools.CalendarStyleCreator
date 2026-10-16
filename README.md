# VehicleVision.PleasanterTools.CalendarStyleCreator

Pleasanter のカレンダー表示で休日を色分け・名称表示するための CSS を生成し、拡張スタイルとして配置する .NET 10 コンソールアプリです。休日は Google カレンダーのカレンダー ID と CSV から取得します。コンソール出力は英語固定です。

> 開発初期です。動作するのは、Google カレンダー（公開）と CSV からの取得、およびファイル配置（`generate`）です。Extensions API 登録（`register`）は骨格だけで未実装です。

標準カレンダーと FullCalendar の両方に、次を表示します。

- 日曜・祝日の背景色と日付の文字色（同じ色）
- 土曜日の背景色と日付の文字色
- 祝日名（標準カレンダーの月表示、FullCalendar の月・日・週・リスト表示）

## 配置方式

同じ生成結果を 2 つの方法で配置できます。

| 方式 | コマンド | 内容 |
| --- | --- | --- |
| ファイル配置 | `generate` | `App_Data/Parameters/ExtendedStyles/{名前}.json` を書き出す |
| API 登録 | `register` | Extensions API で拡張スタイルを登録・更新する（未実装） |

## 使う

既定では、Google カレンダーの日本の祝日（`ja.japanese.official#holiday@group.v.calendar.google.com`）を使います。公開カレンダーなので API キーは不要です。

```powershell
dotnet VehicleVision.PleasanterTools.CalendarStyleCreator.dll generate /output "C:\web\pleasanter\Implem.Pleasanter\App_Data\Parameters\ExtendedStyles"
```

| オプション | 内容 |
| --- | --- |
| `/calendar-id a,b` | Google カレンダー ID をカンマ区切りで複数指定。`none` で Google を使わない |
| `/csv <file>` | CSV からも読む。同じ日付は CSV が優先 |
| `/from 2026 /to 2027` | 出力する年の範囲（既定: 今年と来年）。範囲外の休日と土日は出力しない |
| `/holiday-bg` `/saturday-bg` `/holiday-text` `/saturday-text` | 色（`#rrggbb` または色名） |
| `/site-id 1,2` | 適用サイトを限定（省略時は全サイト） |
| `/name` | 拡張スタイル名（既定: `CalendarHolidays`） |

CSV は [内閣府の「国民の祝日」CSV](https://www8.cao.go.jp/chosei/shukujitsu/syukujitsu.csv) をそのまま読めます（Shift_JIS・UTF-8 どちらも可）。自作する場合は `日付,名前` の 1 行 1 休日です（例: [examples/holidays.csv](examples/holidays.csv)）。

標準カレンダーの土日は日付ごとに指定するため、出力範囲の年を過ぎると色が付かなくなります。年を跨ぐ前に再生成してください。

## 開発者向け

[資料の案内](_documents/README.md) を参照してください。
