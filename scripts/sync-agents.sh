#!/usr/bin/env bash
# 編集元の全文を複写し、相対リンクをリポジトリ直下へ合わせる。
set -euo pipefail
cd "$(dirname "$0")/.."
build() {
    cat <<'HEADER'
# エージェント向け指示

<!-- .github/copilot-instructions.md から生成。直接編集せず scripts/sync-agents.sh を実行する。 -->

作業前に [.github/copilot-instructions.md](.github/copilot-instructions.md) を全文読むこと。
要件と検証状況は [_documents/アーキテクチャ方針.md](_documents/アーキテクチャ方針.md) を確認する。

HEADER
    sed -E \
        -e 's/^# /## /' \
        -e 's#\]\(([^)/:#][^):]*)\)#](.github/\1)#g' \
        -e 's#\]\(\.github/\.\./#](#g' .github/copilot-instructions.md
}
case "${1:-}" in
    --check)
        if ! diff -u AGENTS.md <(build); then
            echo 'Agent instructions are out of sync. Run bash scripts/sync-agents.sh.' >&2
            exit 1
        fi
        echo 'Agent instructions check passed.'
        ;;
    '')
        build > AGENTS.md
        echo 'Generated AGENTS.md.'
        ;;
    *)
        echo 'Usage: sync-agents.sh [--check]' >&2
        exit 1
        ;;
esac
