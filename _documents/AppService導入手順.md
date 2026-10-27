# App Service への配置

本書は既存の Pleasanter を Azure App Service で運用している管理者向けです。CalendarStyleCreator は常駐する Web アプリではなく、必要なときに実行するコンソールツールです。休日は年ごとに変わるため、年に 1 回など定期的に再実行します。

## 配置とパス

**配布 ZIP（`VehicleVision.PleasanterTools.CalendarStyleCreator-<バージョン>-portable.zip`）は x86 / x64 の `dotnet` ホストのどちらでも使えます。** 配置前に `dotnet --info` の Host Architecture を確認し、ホストと同じアーキテクチャの .NET 10 ランタイムであることを確認してください。

本体が `wwwroot/Implem.Pleasanter` にある場合は、配布フォルダを `wwwroot/CalendarStyleCreator` に置きます。

```text
wwwroot/
├── Implem.Pleasanter/
│   └── App_Data/Parameters/ExtendedStyles/   ← ファイル配置の出力先
└── CalendarStyleCreator/
    └── VehicleVision.PleasanterTools.CalendarStyleCreator.dll
```

## ファイル配置（generate）

管理用コンソール（Kudu）で、本体の `ExtendedStyles` フォルダへ書き出します。

```bat
dotnet "%HOME%\site\wwwroot\CalendarStyleCreator\VehicleVision.PleasanterTools.CalendarStyleCreator.dll" generate /output "%HOME%\site\wwwroot\Implem.Pleasanter\App_Data\Parameters\ExtendedStyles"
```

`Implem.Pleasanter` が `CalendarStyleCreator` の隣にある配置では、`/output` も省略できます（本体フォルダは `/p` でも指定できます。CodeDefiner と同じ指定です）。

Linux の例です。

```bash
dotnet /home/site/wwwroot/CalendarStyleCreator/VehicleVision.PleasanterTools.CalendarStyleCreator.dll generate /output /home/site/wwwroot/Implem.Pleasanter/App_Data/Parameters/ExtendedStyles
```

拡張スタイルは本体の起動時に読み込まれるため、配置後にアプリを再起動してください。`vvcsc_core.css` と `vvcsc_{年}.css` が出力されます。年が変わったら再実行し、過ぎた年のファイルを消すときは `/prune` を付けます（接頭辞で始まるファイルだけが対象で、他のファイルには触れません）。ファイルの `.css` は全サイトに適用されます（サイトを限定するときは次の API 登録）。本体の再配置で `App_Data` が置き換わる運用では、再配置のたびに再実行するか、次の API 登録を使います。

## API 登録（register）

ファイルを置けない環境や、再配置で消える運用では、Extensions API で拡張スタイルをデータベースへ登録します。

```bat
set CALENDARSTYLE_PLEASANTER_API_KEY=（ユーザーの API キー）
dotnet "%HOME%\site\wwwroot\CalendarStyleCreator\VehicleVision.PleasanterTools.CalendarStyleCreator.dll" register /url https://pleasanter.example.com
```

- テナント管理で拡張機能 API（`AllowExtensionsApi`）を有効にします。
- API キーは、拡張機能を管理できるユーザー（テナント管理者など）のものを使います。App Service のアプリ設定に `CALENDARSTYLE_PLEASANTER_API_KEY` として登録し、コマンドラインには書かないでください。
- `/site-id` で適用サイトを限定できます。登録後はアプリの再起動が必要です。
- 同名の拡張スタイルがあれば更新し、なければ作成します。内容が同じなら何もしません。
- URL は http も使えます（Pleasanter の API に https の制限はありません）。ただし API キーが平文で流れるので、http のときは警告を出します。
- 拡張は API キーの持ち主のテナントに登録されます。別のテナントにするときは `/tenant-id` を指定してください。登録した拡張スタイルは、テナントに関係なく Pleasanter 全体に適用されます。
- スタイルは `vvcsc_core` と `vvcsc_{年}` の組で登録されます（`/prefix` で変更可）。今回の範囲から外れた年（過ぎた年など）を消すときは `/prune` を付けます。接頭辞で始まるものだけが対象です。

## パラメーター

固定の設定は、配布フォルダの `App_Data/Parameters`（`Holidays.json`・`Style.json`・`Placement.json`）に置きます。Pleasanter 本体の `App_Data/Parameters` と同じ構成で、既定値のファイルが ZIP に入っています。項目は [README](../README.md) を参照してください。コマンドラインのオプションがパラメーターより優先されます。

```text
wwwroot/CalendarStyleCreator/
└── App_Data/Parameters/
    ├── Holidays.json    取得元（Google カレンダー・iCal・CSV・年の範囲）
    ├── Style.json       接頭辞・週の開始曜日・色
    └── Placement.json   出力先・本体のパス・URL・テナント・サイト・Prune
```

認証が必要な iCal の資格情報は、パラメーターに書かず、環境変数の名前で参照します（`BearerTokenEnv`、`BasicUserEnv`、`BasicPasswordEnv`）。ZIP を展開し直すとパラメーターは既定に戻るため、更新時は編集したファイルを残してください。

標準カレンダーの土日は列の位置で指定します。Pleasanter 側の週の開始曜日を変えたら、`Style.json` の `FirstDayOfWeek` も合わせて再実行してください。

## 環境変数

| 名前 | 用途 |
| --- | --- |
| `CALENDARSTYLE_PLEASANTER_API_KEY` | `register` で使う Pleasanter の API キー |
| `CALENDARSTYLE_ICAL_BEARER_TOKEN` | 認証付き iCal の Bearer トークン（パラメーターで環境変数名を指定しない URL に使う） |
| `CALENDARSTYLE_ICAL_BASIC_USER` / `CALENDARSTYLE_ICAL_BASIC_PASSWORD` | 同じく Basic 認証 |

Google カレンダー（公開）への通信が必要です。ファイアウォールで外向きの通信を制限している場合は `calendar.google.com` への https を許可してください。CSV だけを使う場合（`/calendar-id none`）は外部通信は不要です。
