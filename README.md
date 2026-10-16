# VehicleVision.PleasanterTools.CalendarStyleCreator

Pleasanter のカレンダー表示で休日を色分け・名称表示するための CSS を生成し、拡張スタイルとして配置する .NET 10 コンソールアプリです。休日は Google カレンダーのカレンダー ID と CSV から取得します。コンソール出力は英語固定です。

> 開発初期です。Google カレンダー・iCal URL・CSV からの取得、ファイル配置（`generate`）、Extensions API 登録（`register`）を実装済みです。Pleasanter 実機での確認結果は [検証記録](_development/検証記録.md) を参照してください。

標準カレンダーと FullCalendar の両方に、次を表示します。

- 日曜・祝日の背景色と日付の文字色（同じ色）
- 土曜日の背景色と日付の文字色
- 祝日名（標準カレンダーの月表示、FullCalendar の月・日・週・リスト表示）

## 配置方式

同じ生成結果を 2 つの方法で配置できます。

| 方式 | コマンド | 内容 |
| --- | --- | --- |
| ファイル配置 | `generate` | `App_Data/Parameters/ExtendedStyles/{名前}.json` を書き出す |
| API 登録 | `register` | Extensions API で拡張スタイルを登録・更新する（同名は更新） |

## 使う

既定では、Google カレンダーの日本の祝日（`ja.japanese.official#holiday@group.v.calendar.google.com`）を使います。公開カレンダーなので API キーは不要です。

```powershell
dotnet VehicleVision.PleasanterTools.CalendarStyleCreator.dll generate /output "C:\web\pleasanter\Implem.Pleasanter\App_Data\Parameters\ExtendedStyles"
```

| オプション | 内容 |
| --- | --- |
| `/calendar-id a,b` | Google カレンダー ID をカンマ区切りで複数指定。`none` で Google を使わない |
| `/ical-url <url,...>` | iCal（.ics）の https URL をカンマ区切りで複数指定。単独で指定すると既定の日本の祝日は使わない |
| `/csv <file>` | CSV からも読む。同じ日付は CSV が優先 |
| `/from 2026 /to 2027` | 出力する年の範囲（既定: 今年と来年）。範囲外の休日と土日は出力しない |
| `/holiday-bg` `/saturday-bg` `/holiday-text` `/saturday-text` | 色（`#rrggbb` または色名） |
| `/site-id 1,2` | 適用サイトを限定（省略時は全サイト） |
| `/name` | 拡張スタイル名（既定: `CalendarHolidays`） |

### 認証付きの iCal

`/ical-url` で指定した URL に認証が必要な場合は、環境変数で渡します（コマンドラインに置くと履歴やプロセス一覧に残るため）。すべての `/ical-url` に同じ認証を使います。

| 環境変数 | 内容 |
| --- | --- |
| `CALENDARSTYLE_ICAL_BEARER_TOKEN` | Bearer トークン |
| `CALENDARSTYLE_ICAL_BASIC_USER` / `CALENDARSTYLE_ICAL_BASIC_PASSWORD` | Basic 認証（Bearer とは同時に指定できません） |

```powershell
$env:CALENDARSTYLE_ICAL_BEARER_TOKEN = "..."
dotnet VehicleVision.PleasanterTools.CalendarStyleCreator.dll generate /ical-url "https://example.com/holidays.ics" /output ...
```

https のみ使えます。認証付きの場合はリダイレクトを追従せず、資格情報を別のホストへ送りません。Google の「非公開 iCal 形式のアドレス」のように URL 自体が秘密の場合も `/ical-url` で指定できます（URL はメッセージに出しません）。

CSV は [内閣府の「国民の祝日」CSV](https://www8.cao.go.jp/chosei/shukujitsu/syukujitsu.csv) をそのまま読めます（Shift_JIS・UTF-8 どちらも可）。自作する場合は `日付,名前` の 1 行 1 休日です（例: [examples/holidays.csv](examples/holidays.csv)）。

標準カレンダーの土日は日付ごとに指定するため、出力範囲の年を過ぎると色が付かなくなります。年を跨ぐ前に再生成してください。

## API 登録

```powershell
$env:CALENDARSTYLE_PLEASANTER_API_KEY = "..."
dotnet VehicleVision.PleasanterTools.CalendarStyleCreator.dll register /url https://pleasanter.example.com
```

テナントの拡張機能 API（`AllowExtensionsApi`）を有効にし、拡張機能を管理できるユーザーの API キーを使います。同名の拡張スタイルがあれば更新し、内容が同じなら何もしません。

## 設定ファイル

固定の設定は JSON にして `/config` で渡せます（例: [examples/calendarstyle.json](examples/calendarstyle.json)）。コマンドラインのオプションが設定ファイルより優先され、相対パスは設定ファイルのあるフォルダ基準です。コメントと末尾カンマも書けます。

```powershell
dotnet VehicleVision.PleasanterTools.CalendarStyleCreator.dll generate /config calendarstyle.json
```

認証付きの iCal は、URL ごとに環境変数の名前を指定します（資格情報そのものは書けません）。

```json
{ "icalUrls": [ { "url": "https://example.com/team.ics", "bearerTokenEnv": "TEAM_ICAL_TOKEN" } ] }
```

App Service への配置は [App Service への配置](_documents/AppService導入手順.md) を参照してください。

## 開発者向け

[資料の案内](_documents/README.md) を参照してください。
