# エージェント向け指示

<!-- .github/copilot-instructions.md から生成。直接編集せず scripts/sync-agents.sh を実行する。 -->

作業前に [.github/copilot-instructions.md](.github/copilot-instructions.md) を全文読むこと。
要件と検証状況は [_documents/アーキテクチャ方針.md](_documents/アーキテクチャ方針.md) を確認する。

## 共通開発規約

応答、ドキュメント、Issue、PR、コミットは日本語。実行時のコンソール出力は英語固定。API キーや資格情報はコンソールへ出さない。

コンソールを英語にする理由は、Azure の開発者コンソール（Kudu）のエンコーディング制約で日本語が文字化けするため。
利用者向け資料にはこの実装上の理由を混ぜず、開発ガイドに記載する。

.NET 10、デュアルライセンス（AGPL-3.0-or-later ＋ 商用。草案。[LICENSING.md](LICENSING.md)）。
Pleasanter 本体は AI の事実確認専用で、Git では追跡しない。取り込むと商用側を提供できなくなる。
依存 OSS は寛容ライセンス（MIT / BSD 系 / Apache-2.0）に限定する（例外なし。LICENSING.md）。
コードをコピー・流用・リンク・プロジェクト参照せず、ビルド・配布物にも含めない。
事実確認の前に `bash scripts/sync-reference.sh` で上流 `main` の先頭へ更新する。
既定先は `_reference/Implem.Pleasanter`。
`PLEASANTER_REFERENCE_DIR` を指定した場合は、その場所を直接読み、ローカルへのリンクは作らない。
CI は参照ソースを取得・使用しない。詳細は [参照ソース](_reference/README.md) を読む。

`develop` が開発基準、`master` はリリース用。初期配置後は Issue → develop から作業ブランチ →
Release ビルドと影響するテスト → push → Draft PR（WIP:、Closes #N）。

正式な要件は `_documents/` に置く。共通規約はこのファイルが単一の参照元。AGENTS.md と CLAUDE.md から参照する。
共有メモリは Internal の `VehicleVision.PleasanterTools.CalendarStyleCreator.ClaudeMemory` を使い、
資格情報は記録しない。

README は利用者向けの製品説明と操作の入口。ビルド・テスト・CI・設計・エージェント・リリースは開発者向け資料へ分離する。
出典確認・実機検証の記録は `_development/` に置き、利用手順へ混ぜない。
文書を追加したら `_documents/README.md` の該当する入口から案内する。

外部仕様（Google カレンダーの公開 iCal・Pleasanter の拡張スタイルと Extensions API・カレンダー画面の DOM）は
一次情報で確認し、参照日と URL を `_development/参照資料.md` に残す。未検証の動作を検証済みと書かない。
依存追加時はライセンスとロックファイルを確認する。標準ライブラリで足りる間は外部パッケージを増やさない。

本体ソースに基づく事実には、確認した完全なコミット SHA・ソースへの固定リンク・参照日を併記する。
過去の確認結果は取得先の最新状態と区別し、再確認せず SHA や参照日だけを更新しない。
実機未検証ならその旨を残す。規約を変更したら `bash scripts/sync-agents.sh` で AGENTS.md を再生成し、
`bash scripts/sync-agents.sh --check` で確認する。共有メモリより本ファイルを優先する。

生成物は決定的であること（同じ入力から同じ CSS）。
拡張スタイルへの配置は「ファイル出力」と「Extensions API 登録」の 2 方式を同じ生成結果から作る。
API 登録は同名の拡張を更新し、重複作成しない。

GitHub Actions はメジャーバージョンタグ（@v7 など）で指定し、SHA 固定はしない。
