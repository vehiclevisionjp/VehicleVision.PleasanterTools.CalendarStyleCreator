# 共通開発規約

応答、ドキュメント、Issue、PR、コミットは日本語。実行時のコンソール出力は英語固定。API キーや資格情報はコンソールへ出さない。

コンソールを英語にする理由は、Azure の開発者コンソール（Kudu）のエンコーディング制約で日本語が文字化けするため。利用者向け資料にはこの実装上の理由を混ぜず、開発ガイドに記載する。

.NET 10、AGPL-3.0-or-later。Pleasanter 本体は `_reference/Implem.Pleasanter` の参照専用サブモジュール。配布物に含めず、プロジェクト参照しない。

`develop` が開発基準、`master` はリリース用。初期配置後は Issue → develop から作業ブランチ → Release ビルドと影響するテスト → push → Draft PR（WIP:、Closes #N）。

正式な要件は `_documents/` に置く。共通規約はこのファイルが単一の参照元。AGENTS.md と CLAUDE.md から参照する。共有メモリは Internal の `VehicleVision.PleasanterTools.CalendarStyleCreator.ClaudeMemory` を使い、資格情報は記録しない。

README は利用者向けの製品説明と操作の入口。ビルド・テスト・CI・設計・エージェント・リリースは開発者向け資料へ分離する。出典確認・実機検証の記録は `_development/` に置き、利用手順へ混ぜない。文書を追加したら `_documents/README.md` の該当する入口から案内する。

外部仕様（Google Calendar API・Pleasanter の拡張スタイルと Extensions API・カレンダー画面の DOM）は一次情報で確認し、参照日と URL を `_development/参照資料.md` に残す。未検証の動作を検証済みと書かない。依存追加時はライセンスとロックファイルを確認する。標準ライブラリで足りる間は外部パッケージを増やさない。

生成物は決定的であること（同じ入力から同じ CSS）。拡張スタイルへの配置は「ファイル出力」と「Extensions API 登録」の 2 方式を同じ生成結果から作る。API 登録は同名の拡張を更新し、重複作成しない。

GitHub Actions はメジャーバージョンタグ（@v7 など）で指定し、SHA 固定はしない。
