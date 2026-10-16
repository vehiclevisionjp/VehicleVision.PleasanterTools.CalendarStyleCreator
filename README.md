# VehicleVision.PleasanterTools.CalendarStyleCreator

Pleasanter のカレンダー表示で休日を色分け・名称表示するための CSS を生成し、拡張スタイルとして配置する .NET 10 コンソールアプリです。休日は Google カレンダーのカレンダー ID と CSV から取得します。コンソール出力は英語固定です。

> 開発初期です。現時点で動作するのは CSV からのファイル出力のみです。Google カレンダー取得と Extensions API 登録は骨格だけで、未実装です。

## 配置方式

同じ生成結果を 2 つの方法で配置できます。

| 方式 | コマンド | 内容 |
| --- | --- | --- |
| ファイル配置 | `generate` | `App_Data/Parameters/ExtendedStyles/{名前}.json` を書き出す |
| API 登録 | `register` | Extensions API で拡張スタイルを登録・更新する（未実装） |

## 使う

```powershell
dotnet VehicleVision.PleasanterTools.CalendarStyleCreator.dll generate /csv holidays.csv /output "C:\web\pleasanter\Implem.Pleasanter\App_Data\Parameters\ExtendedStyles"
```

CSV は `日付,名前` の 1 行 1 休日です（例: [examples/holidays.csv](examples/holidays.csv)）。日付は `yyyy-MM-dd` または `yyyy/M/d` です。

`/site-id 1,2` で適用サイトを限定できます（省略時は全サイト）。`help` で全オプションを表示します。

## 開発者向け

[資料の案内](_documents/README.md) を参照してください。
