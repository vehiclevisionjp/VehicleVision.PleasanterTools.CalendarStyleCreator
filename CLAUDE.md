# Claude Code 向け指示

@.github/copilot-instructions.md

要件と検証状況は `_documents/アーキテクチャ方針.md` を読む。
Pleasanter 本体は非追跡の参照専用ソース。読む前に `bash scripts/sync-reference.sh` で同期する。
`PLEASANTER_REFERENCE_DIR` があれば、その場所を直接読む。コード・リンクを実装へ取り込まない。
根拠には完全な SHA・ソースへの固定リンク・参照日を残し、実機未検証なら明記する。
共有メモリは [Internal リポジトリ](https://github.com/vehiclevisionjp/VehicleVision.PleasanterTools.ClaudeMemory)の `CalendarStyleCreator/` フォルダ。正式な規約は本体に残す。
