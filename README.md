# VehicleVision.PleasanterTools.CalendarStyleCreator

Pleasanter のカレンダー表示で休日を色分け・名称表示するための CSS を生成し、拡張スタイルとして配置する .NET 10 コンソールアプリです。休日は Google カレンダーのカレンダー ID と CSV から取得します。コンソール出力は英語固定です。

> 開発初期です。Google カレンダー・iCal URL・CSV からの取得、ファイル配置（`generate`）、Extensions API 登録（`register`）を実装済みです。Pleasanter 実機での確認結果は [検証記録](_development/検証記録.md) を参照してください。

標準カレンダーと FullCalendar の両方に、次を表示します。

- 日曜・祝日の背景色と日付の文字色（同じ色）
- 土曜日の背景色と日付の文字色
- 祝日名（標準カレンダーの月・週表示、FullCalendar の月・週・日・リスト表示）
- FullCalendar のリスト表示も、土日・祝日の日付行に色を付ける

## 配置方式

同じ生成結果を 2 つの方法で配置できます。

| 方式 | コマンド | 内容 |
| --- | --- | --- |
| ファイル配置 | `generate` | `App_Data/Parameters/ExtendedStyles/` に `.css` を書き出す（全サイトに適用） |
| API 登録 | `register` | Extensions API で拡張スタイルを登録・更新する（同名は更新） |

## 使う

既定では、Google カレンダーの日本の祝日（`ja.japanese.official#holiday@group.v.calendar.google.com`）を使います。公開カレンダーなので API キーは不要です。

```powershell
dotnet VehicleVision.PleasanterTools.CalendarStyleCreator.dll generate /p "C:\web\pleasanter\Implem.Pleasanter"
```

| オプション | 内容 |
| --- | --- |
| `/calendar-id a,b` | Google カレンダー ID をカンマ区切りで複数指定（`GoogleCalendarIds` を上書き）。`none` で Google を使わない |
| `/ical-url <url,...>` | iCal（.ics）の https URL をカンマ区切りで複数指定。`IcalUrls` を上書き。既定の日本の祝日にも足される |
| `/csv <file>` | CSV からも読む。同じ日付は CSV が優先 |
| `/from 2026 /to 2027` | 出力する年の範囲（既定: 今年と来年）。範囲外の休日と土日は出力しない |
| `/holiday-bg` `/saturday-bg` `/weekday-bg` `/holiday-text` `/saturday-text` `/weekday-text` | 色（`#rrggbb` または色名）。平日の既定は背景なし（`transparent`）・文字色は継承（`inherit`） |
| `/p <フォルダ>` | `generate` の出力先を、本体フォルダ（`App_Data` のある `Implem.Pleasanter` またはその親）から決める。CodeDefiner と同じ指定。省略時は、このツールの兄弟フォルダ `Implem.Pleasanter` を使う |
| `/output <フォルダ>` | `generate` の出力先を直接指定（`/p` より優先） |
| `/site-id 1,2` | 適用サイトを限定（`register` のみ。省略時は全サイト） |
| `/first-day-of-week <0-6>` | 標準カレンダーの週の開始曜日（0 日曜〜6 土曜、既定 1）。Pleasanter 側の設定と合わせる |
| `/prefix <文字列>` | スタイル名の接頭辞（既定: `vvcsc_`）。英数字・`_`・`-` で 32 文字まで |
| `/prune` | この接頭辞で始まり、今回は生成しなくなったスタイル（過ぎた年など）を削除する |
| `/tenant-id <n>` | `register` のみ。拡張のテナント（省略時は API キーの持ち主のテナント） |

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

## CSV と取得元の併用

CSV は [内閣府の「国民の祝日」CSV](https://www8.cao.go.jp/chosei/shukujitsu/syukujitsu.csv) をそのまま読めます（Shift_JIS・UTF-8 どちらも可）。自作する場合は `日付,名前[,種類]` の 1 行 1 日です（例: [examples/holidays.csv](examples/holidays.csv)）。

Google カレンダー（`GoogleCalendarIds`）・iCal・CSV は併用でき、すべての取得元の日付を 1 つにまとめます。日付ごとの色は 3 種類で、CSV の第 3 列（種類）で指定します。Google カレンダーと iCal の予定は日祝です。

| 種類（CSV の第 3 列） | 色 |
| --- | --- |
| `日祝`（既定。`祝日`・`holiday` も可） | 日曜・祝日の色 |
| `土`（`土曜`・`sat` も可） | 土曜の色 |
| `平日`（`weekday` も可） | 土日の色を打ち消して通常の色にする（土曜の振替出勤日など） |

同じ日付が複数あるときの優先順位は **日祝 > 土 > 平日** です。優先順位の高い種類だけが残り、低い種類は名称ごと捨てます（例: Google の祝日に `平日` を指定しても祝日のまま）。同じ種類の名称は「 / 」でつなぎます。

平日の指定は、標準カレンダーの月・週表示と FullCalendar の月・週・日表示の日のセルが対象です。FullCalendar の曜日見出しとリスト表示の日付行は、平日にしても本体の既定のままです（土日の色は付きません）。

標準カレンダーの土日は日付ごとに指定するため、出力範囲の年を過ぎると色が付かなくなります。年を跨ぐ前に再生成してください。

ファイル配置・API 登録のどちらも、反映には Pleasanter の再起動が必要です（拡張スタイルは起動時に読み込まれます）。サイトを限定したい場合は API 登録を使います（ファイルの `.css` は全サイトに適用されます）。

## 生成されるスタイル

接頭辞付きで、共通部と年ごとに分けて配置します。

| 名前（既定の接頭辞） | 内容 |
| --- | --- |
| `vvcsc_core` | FullCalendar の土日の色（年に依存しない） |
| `vvcsc_2026`、`vvcsc_2027` … | その年の祝日・祝日名と、標準カレンダーの土日 |

年が変わったら、新しい年のスタイルが増えるだけで、共通部と過去の年は変わりません。接頭辞で始まるものだけをこのツールの管理対象とし、他のスタイルには触れません。今回の範囲に入らなくなった管理対象（過ぎた年など）は、既定では残して警告を出し、`/prune` を付けたときだけ削除します。

## API 登録

```powershell
$env:CALENDARSTYLE_PLEASANTER_API_KEY = "..."
dotnet VehicleVision.PleasanterTools.CalendarStyleCreator.dll register /url https://pleasanter.example.com
```

テナントの拡張機能 API（`AllowExtensionsApi`）を有効にし、拡張機能を管理できるユーザーの API キーを使います。同名の拡張スタイルがあれば更新し、内容が同じなら何もしません。

- **テナント**: 拡張は API キーの持ち主のテナントに登録されます。別のテナントにする場合は `/tenant-id` を指定します。登録した拡張スタイルは、テナントに関係なく Pleasanter 全体に適用されます（本体は全テナントの拡張を起動時に読み込みます）。
- **http**: Pleasanter の API に https の制限はないため http の URL も使えますが、API キーが平文で流れるため警告を出します。

## パラメーター（固定の設定）

固定にしておく設定は、Pleasanter 本体の `App_Data/Parameters` に倣った JSON で、このツールの `App_Data/Parameters` に置きます。配布物に既定値のファイルが入っているので、変えたい項目だけ編集してください（キーは PascalCase、値が `null` なら未指定）。

| ファイル | 内容 |
| --- | --- |
| `Holidays.json` | 取得元: `GoogleCalendarIds`（配列）、`IcalUrls`（配列。文字列、または `Url` と認証用の環境変数名を持つオブジェクト）、`CsvFiles`（配列）、`FromYear`、`ToYear` |
| `Style.json` | 見た目と名前: `Prefix`、`FirstDayOfWeek`（0 日曜〜6 土曜）、`Colors`（`HolidayBackground`・`SaturdayBackground`・`WeekdayBackground`・`HolidayText`・`SaturdayText`・`WeekdayText`） |
| `Placement.json` | 配置: `OutputPath`、`PleasanterPath`、`Url`、`TenantId`、`SiteIdList`、`Prune` |

```json
{
    "GoogleCalendarIds": [ "ja.japanese.official#holiday@group.v.calendar.google.com" ],
    "IcalUrls": [
        "https://example.com/company-holidays.ics",
        { "Url": "https://example.com/private/team.ics", "BearerTokenEnv": "TEAM_ICAL_TOKEN" },
        { "Url": "https://example.com/other.ics", "BasicUserEnv": "OTHER_ICAL_USER", "BasicPasswordEnv": "OTHER_ICAL_PASSWORD" }
    ],
    "CsvFiles": [ "holidays.csv" ]
}
```

- コマンドラインのオプションがパラメーターより優先されます（同じ項目だけを上書き）。`/parameters <フォルダ>` で別のフォルダも使えます。
- 相対パスはパラメーターのフォルダ基準です。未知のキーはエラーになります。
- 資格情報はパラメーターに書けません。認証付きの iCal は、URL ごとに環境変数の名前（`BearerTokenEnv`・`BasicUserEnv`・`BasicPasswordEnv`）を指定します。
- ファイルが無ければ既定値（日本の祝日・`vvcsc_`・月曜始まり）で動きます。

標準カレンダーの土日は、列の位置（`FirstDayOfWeek` からの並び）で指定するため、Pleasanter 側の週の開始曜日と同じ値にしてください。既定は月曜（1）で、Pleasanter の既定と同じです。

App Service への配置は [App Service への配置](_documents/AppService導入手順.md) を参照してください。

## 貢献・ライセンス

**デュアルライセンス**で提供します。詳細は [ライセンスの説明](LICENSING.md) を参照してください。

| | ライセンス | 想定する利用 |
| --- | --- | --- |
| A | [GNU AGPL v3 以降](LICENSE) | オープンソースとして利用・改変・再配布する場合 |
| B | [株式会社ピー・エム・シー ITソリューション事業部の商用ライセンス](LICENSE-COMMERCIAL.md) | 法人が AGPL の義務を負わずに利用したい場合（個別契約） |

公開済みの版（v0.1.0）は、受け取った時点の AGPL-3.0-or-later のままです。

貢献を取り込む前に [CLA](CLA.md) への署名が必要です。手順は [貢献のしかた](CONTRIBUTING.md) を参照してください。

## 開発者向け

[資料の案内](_documents/README.md) を参照してください。
