# 実機（Docker の Pleasanter）での表示確認手順

開発者向け。公式の Pleasanter イメージ（`implem/pleasanter:1.5.8.1`）と PostgreSQL を Docker で起動し、生成した拡張スタイルが標準カレンダーと FullCalendar で効くかを、DOM と `getComputedStyle` とスクリーンショットで確認する。結果は [検証記録](検証記録.md) に残す。実運用の Pleasanter には向けない。

## 前提

- Docker、Node.js 24 以上、ローカルの Chrome（Playwright は同梱ブラウザではなくローカルの Chrome を使う）。
- `npm ci --ignore-scripts` で playwright-core 1.64.1（Apache-2.0、依存なし）を入れる。配布物には含まれない開発専用の依存。
- イメージ `implem/pleasanter:1.5.8.1` と `implem/pleasanter:codedefiner`、`postgres:18-alpine`。
- `compose.yaml` は専用のプロジェクト名（`calendarstyle-test`）・コンテナ名・ボリューム・ポート（本体 58801、DB 58802、いずれも 127.0.0.1）を使う。他のコンテナには触れない。
- 検証用の資格情報（管理者 Administrator、DB のパスワード）は使い捨て。初回ログインで既定のパスワードを変更する。

## 手順

作業用の状態ファイルは `temp/e2e/`（リポジトリの管理外）に置く。以降は PowerShell で実行する（Git Bash は `/output` などの引数をパス変換で壊す）。

1. CSS を生成する。祝日が土曜に重なる日を確認するため、本物の祝日（既定の Google カレンダー）に検証用の CSV（日曜祝日 2026-10-25、土曜祝日 2026-10-31）を足す。

   ```powershell
   dotnet build -c Release
   New-Item -ItemType Directory -Force temp/e2e | Out-Null
   "date,name,type`n2026-10-31,テスト土曜祝日`n2026-10-25,テスト日曜祝日`n2026-10-17,振替出勤,平日`n2026-10-18,日曜の平日扱い,平日`n2026-10-14,社内土曜扱い,土`n2026-10-12,競合する平日,平日`n2026-10-13,祝日名,日祝" | Set-Content temp/e2e/extra.csv -Encoding utf8
   dotnet src/CalendarStyleCreator/bin/Release/net10.0/VehicleVision.PleasanterTools.CalendarStyleCreator.dll generate /output temp/e2e/out /csv temp/e2e/extra.csv
   ```

2. 本体を起動し、拡張スタイルと週の開始曜日を反映する。`prepare.ps1` が `General.json`（`FirstDayOfWeek`）と `Security.json`（Administrator を `PrivilegedUsers` に）の差し替えファイルを作り、生成した `.css` を `temp/e2e/ExtendedStyles` に置き、`docker compose up -d --wait` と本体の再起動までを行う。本体はパラメーターと拡張スタイルを起動時に読むため、変更のたびに再起動が必要。

   ```powershell
   pwsh _development/e2e/prepare.ps1 -StyleCss temp/e2e/out -FirstDayOfWeek 1
   ```

3. 作業用フォルダで、期限付きテーブルとレコードを作る（空の DB で一度だけ）。ログイン状態と SiteId は `e2e-state.json` と `e2e-ids.json` に書かれる。

   ```powershell
   cd temp/e2e
   node ../../_development/e2e/seed.mjs
   ```

4. 標準カレンダーと FullCalendar を確認する。`verify.mjs` は期限付きテーブルのカレンダー種別を切り替え（標準 `1`、FullCalendar `2`）、各表示で全セルの色・祝日名を期待値と比べる。NG があれば終了コード 1。スクリーンショットは第 3 引数のフォルダに出る。

   ```powershell
   node ../../_development/e2e/verify.mjs standard out shots mon
   node ../../_development/e2e/verify.mjs full     out shots mon
   ```

5. 週の開始曜日を日曜に変えて再確認する（ラベルに `sun` を含める）。標準カレンダーの土日は列の位置で指定するため、生成側も `/first-day-of-week 0` にして `out0` へ出力し、そちらを配置・検証する。

   ```powershell
   cd ../..
   dotnet src/CalendarStyleCreator/bin/Release/net10.0/VehicleVision.PleasanterTools.CalendarStyleCreator.dll generate /output temp/e2e/out0 /csv temp/e2e/extra.csv /first-day-of-week 0
   pwsh _development/e2e/prepare.ps1 -StyleCss temp/e2e/out0 -FirstDayOfWeek 0
   cd temp/e2e
   node ../../_development/e2e/verify.mjs standard out0 shots sun
   node ../../_development/e2e/verify.mjs full     out0 shots sun
   ```

6. Extensions API 方式を確認する。`extensions-api.mjs` がテナントの `AllowExtensionsApi` を有効にし、Administrator の API キーを作って `/api/extensions/` の create・get・update・delete を呼ぶ（動作確認用の拡張は消す）。API キーは `temp/e2e/e2e-apikey.txt` に書かれる（管理外）。その後 `register` で登録し、ファイルの `.css` を外して再起動してから手順 4 を繰り返す。

   ```powershell
   node ../../_development/e2e/extensions-api.mjs
   $env:CALENDARSTYLE_PLEASANTER_API_KEY = (Get-Content e2e-apikey.txt -Raw).Trim()
   cd ../..
   dotnet src/CalendarStyleCreator/bin/Release/net10.0/VehicleVision.PleasanterTools.CalendarStyleCreator.dll register /url http://127.0.0.1:58801 /csv temp/e2e/extra.csv
   Remove-Item Env:CALENDARSTYLE_PLEASANTER_API_KEY
   pwsh _development/e2e/prepare.ps1 -FirstDayOfWeek 1   # -StyleCss を付けないのでファイルの .css は空になる
   ```

7. 終了後にコンテナとボリュームを破棄する。

   ```powershell
   docker compose down -v
   ```

## 期待する結果

- 標準カレンダーの月・週・年表示、FullCalendar の月・週・日・リスト表示で、`verify.mjs` の NG が 0 件。
- 日曜・祝日は同じ背景色と文字色、土曜は別の背景色と文字色。土曜の祝日（検証用 2026-10-31）は祝日の色。`extra.csv` の種類の指定（土曜の平日扱い 10/17、日曜の平日扱い 10/18、水曜の土扱い 10/14、Google の祝日 10/12 と競合する平日指定は祝日が勝つ）が反映される。`verify.mjs` は作業用フォルダの `extra.csv` から種類を読む。
- 前月・翌月のはみ出し日（標準は `.other-month`、FullCalendar は `.fc-day-other`）に色が付かない。標準カレンダーの年表示のセル（`data-id` が月初日）に色と祝日名が付かない。
- 標準カレンダーの月表示（`.day::after`）・週表示（セル先頭の `::before`）と、FullCalendar の月・週・日表示・リストに祝日名が出る。標準カレンダーの週表示は月をまたぐ日にも色が付く。
- `FirstDayOfWeek` を 0 にしても標準カレンダーの土日色が日付どおりに付く（FullCalendar は常に月曜始まり）。

## 本体の画面が変わったとき

`lib.mjs` は本体の要素 ID（`#NewMenuContainer`、`#OpenSiteTitleDialog`、`#CreateByTemplate`、`#CalendarType`、
`#CreateCommand` など）に、`verify.mjs` は `#CalendarBody`・`#CalendarTimePeriod`・`$p.moveCalendar`・
`.fc-*-button` に依存する。本体の実機版を変えて失敗した場合は、該当箇所を本体の画面に合わせて直す。
ソースを調べる前は `bash scripts/sync-reference.sh` で同期し、完全な SHA・固定リンク・参照日を記録する。
取得先の最新 `main` と、実機のイメージの版は区別する。
`seed.mjs` の日付は 2026-10 を前提にしている（今日が 2026-10-16 の実行を想定）。

## 検証の「今日」

画面の確認は、実際の日付に依存しないよう「今日」を 2026-10-16 に固定している（`lib.mjs` の `ANCHOR_ISO`・`ANCHOR_DATE`）。

- ブラウザの `Date` は Playwright の `page.clock.setFixedTime` で固定する（タイマーは止めない）。FullCalendar の today ボタンと、初期表示のプロット位置がこの日付になる。
- 標準カレンダーの「今日」はサーバーの日付（`#CalendarToday`）で、ブラウザからは変えられない。そのため `verify.mjs` は、`$p.moveCalendar('Today')` の代わりに日付欄（`#CalendarDate`）へ `2026/10/16` を直接入れて移動する。
- レコードと祝日の日付は固定（`seed.mjs`、`extra.csv`）。今日の日付が変わっても、同じ画面・同じ期待値で確認できる。
