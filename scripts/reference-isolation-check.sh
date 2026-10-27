#!/usr/bin/env bash
# 参照ソース（_reference/Implem.Pleasanter。AGPL v3）が、このツールのビルドへ混ざらないことを検査する。
#
# **Template の同名の検査は、`Implem.` という文字列が src / tests に 1 つでもあれば落とす。**
# このツールは、利用者の環境にある Pleasanter のフォルダ（`Implem.Pleasanter`、`Implem.CodeDefiner` と同じ作法）を
# 探す機能を持ち、その名前を文字列として扱う（Options.cs・試験）。それは取り込みではないので、
# ここでは「取り込み」だけを見る。
#   - .csproj / .slnx / Directory.Build.props が、Implem.* や _reference を参照していないこと
#     （ProjectReference・Compile・Reference・Include など）
#   - ソースが Implem.* の名前空間を using / 宣言していないこと
#   - 既定取得先・環境変数で指定した共有取得先がソースへ取り込まれていないこと
set -euo pipefail
cd "$(dirname "$0")/.."

check_references() {
    local result=0
    grep "$@" || result=$?
    if [[ "$result" -gt 1 ]]; then
        echo 'Reference isolation scan failed.' >&2
        exit "$result"
    fi
    return "$result"
}
shopt -s nullglob
build_files=(Directory.Build.props ./*.slnx ./*.sln ./*.targets)
source_dirs=()
for directory in src tests; do
    if [[ -d "$directory" ]]; then
        source_dirs+=("$directory")
    fi
done
if [[ "${#source_dirs[@]}" -gt 0 ]] &&
    check_references -REin 'Implem\.|_reference' "${source_dirs[@]}" \
        --include='*.csproj' --include='*.props' --include='*.targets'; then
    echo 'Reference source must not be referenced from a project file.' >&2
    exit 1
fi
if check_references -Ein 'Implem\.|_reference' "${build_files[@]}"; then
    echo 'Reference source must not be included in the solution.' >&2
    exit 1
fi
if [[ "${#source_dirs[@]}" -gt 0 ]] &&
    check_references -REn '_reference|^[[:space:]]*(using|namespace)[[:space:]]+(static[[:space:]]+)?Implem\.' \
        "${source_dirs[@]}" --include='*.cs'; then
    echo 'Reference source paths or namespaces must not be imported or declared.' >&2
    exit 1
fi
if [[ -n "${PLEASANTER_REFERENCE_DIR:-}" ]]; then
    reference_dir="$PLEASANTER_REFERENCE_DIR"
    if command -v cygpath > /dev/null 2>&1; then
        reference_dir="$(cygpath -am "$reference_dir")"
    fi
    reference_dir="${reference_dir//\\//}"
    if [[ "$reference_dir" != /* && ! "$reference_dir" =~ ^[A-Za-z]:/ ]]; then
        reference_dir="$PWD/$reference_dir"
    fi
    reference_dir="${reference_dir%/}"
    if [[ -z "$reference_dir" ]]; then
        echo 'PLEASANTER_REFERENCE_DIR must identify a reference directory, not the filesystem root.' >&2
        exit 1
    fi
    patterns=(-e "$reference_dir" -e "${reference_dir//\//\\}")
    if command -v cygpath > /dev/null 2>&1; then
        posix_reference="$(cygpath -au "$reference_dir")"
        patterns+=(-e "$posix_reference" -e "${posix_reference//\//\\}")
    fi
    if check_references -Fin "${patterns[@]}" "${build_files[@]}" ||
        { [[ "${#source_dirs[@]}" -gt 0 ]] &&
            check_references -RFin "${patterns[@]}" "${source_dirs[@]}" \
                --include='*.csproj' --include='*.props' --include='*.targets' --include='*.cs'; }; then
        echo 'Shared reference source must not be imported into the tool.' >&2
        exit 1
    fi
fi
echo 'Reference isolation check passed.'
