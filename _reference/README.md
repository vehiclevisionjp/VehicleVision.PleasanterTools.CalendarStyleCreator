# _reference（参考専用・AGPL）

Pleasanter 本体（GNU AGPL v3）は AI（Claude・Copilot・Codex）の事実確認専用ソースです。
必要なときに上流 `main` の先頭を取得し、本リポジトリでは追跡しません。
CI は取得・使用しません。この README だけを引き続き追跡します。

- 上流: <https://github.com/Implem/Implem.Pleasanter>
- 既定先: `_reference/Implem.Pleasanter/`（Git 管理外）
- 共有先: 環境変数 `PLEASANTER_REFERENCE_DIR`（その場所を直接読む。リンクは作らない）

## 厳守事項

- コードをコピー・流用・リンク・プロジェクト参照しない
- ビルド対象・配布物・ソリューションへ含めない
- 取得した作業ツリーを編集しない。本リポジトリのコミット対象にしない
- 実運用の設定・権限・API 応答・カレンダー画面の動作は実機で確認し、未検証は明記する

本アプリのデュアルライセンス（AGPL-3.0-or-later ＋ 商用。草案。[LICENSING.md](../LICENSING.md)）と CLA の方針は変更しません。
本体ソースの取り込みを許可する変更ではありません。取り込むと商用側を提供できなくなります。

## 取得と更新

事実確認を始める前に毎回実行します。Git と bash が必要です。Windows は Git Bash を使います。

```powershell
bash scripts/sync-reference.sh

# worktree 間で共有する場合（絶対パスを推奨）
$env:PLEASANTER_REFERENCE_DIR = 'D:\references\Implem.Pleasanter'
bash scripts/sync-reference.sh
```

未取得なら `main` を浅く clone し、既存なら fetch 後に `FETCH_HEAD` へ detach checkout します。
共有先は直接読み、既定先へのジャンクションやシンボリックリンクは作りません。
相対パスはリポジトリ直下が基準です。共有先の更新中に他の AI が読まないよう調整してください。

終了時に Path・短い SHA・コミット日時・Status を出力します。
本アプリの既定の 1 件では、完全な FullSHA・取得日時（UTC）・csproj の Version も出力します。
Version が読めない場合は警告と `Version=unknown` を出し、版を推測しません。

ローカル変更・ステージ済み・未追跡ファイル、origin の不一致、別リポジトリや非空の取得先では停止します。
通信失敗は失敗コードで終了し、既存の HEAD とファイルを維持します。
新規 clone は一時取得先で成功してから配置し、並行取得はロックで拒否します。
異常終了で `.sync-lock` が残った場合は、同期が実行中でないと確認してから空のロックディレクトリを除きます。
CI に本体取得を追加せず、ネットワーク不要の `test-sync-reference.sh` だけを規約検査で実行します。

## 事実の記録と再現性

日次更新とポインタ更新 PR を廃止し、再現性は gitlink ではなく文書の確認済み SHA で保ちます。
事実には完全なコミット SHA・該当ソースへの固定リンク・参照日を併記してください。
現在の取得先が過去の確認 SHA と同じとは限りません。再確認せず過去の SHA や参照日を更新しません。
`main` はリリース前の変更も含み、Version だけでリリース済みとは判断しません。

過去のカレンダー DOM・拡張スタイル・Extensions API の確認結果は
[_development/参照資料.md](../_development/参照資料.md) と
[検証記録](../_development/検証記録.md) に残します。
今回、過去の事実を最新ソースで再確認したものとは扱いません。

移行元は Template の承認済みコミット
`3312f003fbe9827090e20bc643800824bdf30e22`（2026-10-27 参照）の
[配布手順][distribution]です。背景は
vehiclevisionjp/VehicleVision.PleasanterTools.Template#61・
vehiclevisionjp/VehicleVision.PleasanterTools.Template#63 です。

[distribution]: https://github.com/vehiclevisionjp/VehicleVision.PleasanterTools.Template/blob/3312f003fbe9827090e20bc643800824bdf30e22/_documents/参照ソースのオンデマンド取得への配布手順.md
