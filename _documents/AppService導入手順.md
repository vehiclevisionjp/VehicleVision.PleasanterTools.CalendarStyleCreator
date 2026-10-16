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

Linux の例です。

```bash
dotnet /home/site/wwwroot/CalendarStyleCreator/VehicleVision.PleasanterTools.CalendarStyleCreator.dll generate /output /home/site/wwwroot/Implem.Pleasanter/App_Data/Parameters/ExtendedStyles
```

パラメーターファイルの読み込み時期は本体の設定によります。反映されない場合はアプリを再起動してください。本体の再配置で `App_Data` が置き換わる運用では、再配置のたびに再実行するか、次の API 登録を使います。

## API 登録（register）

ファイルを置けない環境や、再配置で消える運用では、Extensions API で拡張スタイルをデータベースへ登録します。

```bat
set CALENDARSTYLE_PLEASANTER_API_KEY=（ユーザーの API キー）
dotnet "%HOME%\site\wwwroot\CalendarStyleCreator\VehicleVision.PleasanterTools.CalendarStyleCreator.dll" register /url https://pleasanter.example.com
```

- テナント管理で拡張機能 API（`AllowExtensionsApi`）を有効にします。
- API キーは、拡張機能を管理できるユーザー（テナント管理者など）のものを使います。App Service のアプリ設定に `CALENDARSTYLE_PLEASANTER_API_KEY` として登録し、コマンドラインには書かないでください。
- 同名の拡張スタイルがあれば更新し、なければ作成します。内容が同じなら何もしません。
- URL は https が必要です（localhost の検証用だけ http を許可）。

## 設定ファイル

固定の設定は JSON ファイルにして `/config` で渡せます。書式は [examples/calendarstyle.json](../examples/calendarstyle.json) を参照してください。コマンドラインのオプションが設定ファイルより優先されます。相対パスは設定ファイルのあるフォルダ基準です。

```bat
dotnet "%HOME%\site\wwwroot\CalendarStyleCreator\VehicleVision.PleasanterTools.CalendarStyleCreator.dll" generate /config "%HOME%\site\wwwroot\CalendarStyleCreator\calendarstyle.json"
```

認証が必要な iCal の資格情報は、設定ファイルに書かず、環境変数の名前で参照します（`bearerTokenEnv`、`basicUserEnv`、`basicPasswordEnv`）。

## 環境変数

| 名前 | 用途 |
| --- | --- |
| `CALENDARSTYLE_PLEASANTER_API_KEY` | `register` で使う Pleasanter の API キー |
| `CALENDARSTYLE_ICAL_BEARER_TOKEN` | 認証付き iCal の Bearer トークン（設定ファイルで名前を指定しない URL に使う） |
| `CALENDARSTYLE_ICAL_BASIC_USER` / `CALENDARSTYLE_ICAL_BASIC_PASSWORD` | 同じく Basic 認証 |

Google カレンダー（公開）への通信が必要です。ファイアウォールで外向きの通信を制限している場合は `calendar.google.com` への https を許可してください。CSV だけを使う場合（`/calendar-id none`）は外部通信は不要です。
