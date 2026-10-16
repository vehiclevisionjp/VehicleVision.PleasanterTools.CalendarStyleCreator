# 検証用の本体に拡張スタイルと週の開始曜日を反映して再起動する（本体はパラメーターを起動時に読む）。
# 使い方: pwsh _development/e2e/prepare.ps1 [-StyleCss <generate が出した .css>] [-FirstDayOfWeek 0|1]
#   -StyleCss を省略すると拡張スタイルを空にする。-FirstDayOfWeek は 0=日曜、1=月曜（本体の既定）。
param([string]$StyleCss, [int]$FirstDayOfWeek = 1)
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '../..')
$work = Join-Path $root 'temp/e2e'
$styles = Join-Path $work 'ExtendedStyles'
New-Item -ItemType Directory -Force $styles | Out-Null

# 本体イメージの General.json（FirstDayOfWeek を変更）と Security.json（Administrator を特権ユーザー PrivilegedUsers にして
# Extensions API を使えるようにする）を元に差し替えファイルを作る。BOM を壊さないよう docker cp でファイルのまま取り出す。
$image = 'implem/pleasanter:1.5.8.1'
$container = docker create $image
try {
    foreach ($name in 'General', 'Security') {
        docker cp "${container}:/app/App_Data/Parameters/$name.json" (Join-Path $work "$name.orig.json") | Out-Null
    }
} finally { docker rm $container | Out-Null }
$text = [IO.File]::ReadAllText((Join-Path $work 'General.orig.json')) -replace '"FirstDayOfWeek":\s*\d+', "`"FirstDayOfWeek`": $FirstDayOfWeek"
[IO.File]::WriteAllText((Join-Path $work 'General.json'), $text, [Text.UTF8Encoding]::new($false))
$text = [IO.File]::ReadAllText((Join-Path $work 'Security.orig.json')) -replace '"PrivilegedUsers":\s*null', '"PrivilegedUsers": ["Administrator"]'
[IO.File]::WriteAllText((Join-Path $work 'Security.json'), $text, [Text.UTF8Encoding]::new($false))

# 本体が読むのは ExtendedStyles/*.css（*.json は読まれない）。generate が出した .css をそのまま置く。
Get-ChildItem $styles -File | Remove-Item
if ($StyleCss) { Copy-Item -LiteralPath $StyleCss -Destination $styles }

Push-Location $root
try { docker compose up -d --wait; docker compose restart pleasanter; docker compose up -d --wait } finally { Pop-Location }
