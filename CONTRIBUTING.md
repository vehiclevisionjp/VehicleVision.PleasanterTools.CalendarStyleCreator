# 貢献する

開発前に [共通規約](.github/copilot-instructions.md) と [開発ガイド](_documents/開発ガイド.md) を読んでください。

Issue に変更の目的・制約を記録し、develop から作業ブランチを作ります。コミット・Issue・PR・ドキュメントは日本語で、アプリのコンソール出力は英語にします。

Release ビルド、単体チェック、`npm run lint:md` を実行し、結果と未検証の範囲を PR に書いてください。作業中は Draft と WIP:、本文には Closes #N。レビュー可能になったら Draft と WIP: を外します。master はリリース確定用です。

本体参照ソースはサブモジュールに固定し、配布物へ含めません。Google の API キー・Pleasanter の API キー・実運用サイトの設定データをコミットしないでください。

本リポジトリは AGPL-3.0-or-later です。
