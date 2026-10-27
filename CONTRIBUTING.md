# 貢献する

本リポジトリは現在、AGPL-3.0-or-later 単独で提供しています。
CLA は将来の商用提供を含むライセンス選択の余地を残すための許諾であり、
現在の提供条件を変えたり、利用者に商用ライセンスを提供したりするものではありません。

開発前に [共通規約](.github/copilot-instructions.md) と [開発ガイド](_documents/開発ガイド.md) を読んでください。

Issue に変更の目的・制約を記録し、develop から作業ブランチを作ります。コミット・Issue・PR・ドキュメントは日本語で、アプリのコンソール出力は英語にします。

Release ビルド、単体チェック、`npm run lint:md` を実行し、結果と未検証の範囲を PR に書いてください。作業中は Draft と WIP:、本文には Closes #N。レビュー可能になったら Draft と WIP: を外します。master はリリース確定用です。

本体参照ソースは Git で追跡せず、[必要時に取得](_reference/README.md)し、配布物へ含めません。
確認した完全な SHA・ソースへの固定リンク・参照日を記録してください。
Google の API キー・Pleasanter の API キー・実運用サイトの設定データをコミットしないでください。

共有メモリは Internal の `.ClaudeMemory` リポジトリに置き、正式な仕様と規約は本体の資料へ残します。

## CLA へ署名する

**公開リポジトリへの貢献は、取り込む前に [CLA.md](CLA.md) への署名が要ります。**
現在の提供ライセンスにかかわらず、将来の商用提供も含めて再許諾できる状態を保つためです。
CLA は草案なので、正式な運用の前に管理者が法務の確認を受けてください。

1. [CLA.md](CLA.md) を読む
2. [「CLA への署名」の Issue][cla-issue] を作り、同意する
3. 管理者が同意と許諾権限を確認し、[.github/cla/signed.yml](.github/cla/signed.yml) へ追記する
4. PR のチェック `cla` で確認する（必須化は導入 PR のマージ後に別途行う）

**会社の業務として貢献する場合は、法人用の署名も要ります。** 勤務先の承認を先に取ってください。
署名一覧は管理者が変更します。貢献者自身の PR で追記しても、その PR の判定には使いません。
一覧の変更を対象 base ブランチへ取り込んだ後、PR を更新してチェックを再実行してください。
未署名ならチェックが失敗し、案内コメントが付きます。

Dependabot・GitHub Actions・`my-repo-automation-bot[bot]` の自動更新 PR は対象外です。
管理者の確認手順と、必須化前の配置条件は [開発運用](_documents/開発運用.md) にあります。
現在の提供条件は [ライセンスの説明](LICENSING.md) を参照してください。

[cla-issue]:
  https://github.com/vehiclevisionjp/VehicleVision.PleasanterTools.CalendarStyleCreator/issues/new?template=cla.yml
