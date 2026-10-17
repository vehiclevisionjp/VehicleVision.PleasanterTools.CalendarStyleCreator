param([ValidateSet('win-x86','win-x64','linux-x64')][string]$Platform = $(if ($IsWindows) { 'win-x64' } else { 'linux-x64' }), [string]$PackageDirectory = 'artifacts/package', [string]$DotnetPath = 'dotnet')
$ErrorActionPreference = 'Stop'
$repoPath = (Get-Location).Path
$version = ([xml](Get-Content (Join-Path $repoPath 'Directory.Build.props') -Raw)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$zipPath = Join-Path $repoPath (Join-Path $PackageDirectory "VehicleVision.PleasanterTools.CalendarStyleCreator-$version-portable.zip")
if (-not (Test-Path -LiteralPath $zipPath)) { throw 'Build the distribution ZIP before running App Service smoke checks.' }
$smokeHome = Join-Path $repoPath ('temp/appservice-' + [guid]::NewGuid().ToString('N'))
$wwwroot = Join-Path $smokeHome 'site/wwwroot'
$pleasanter = Join-Path $wwwroot 'Implem.Pleasanter'
$styles = Join-Path $pleasanter 'App_Data/Parameters/ExtendedStyles'
$outside = Join-Path $smokeHome 'data/console working directory'
New-Item -ItemType Directory -Force $styles, $outside, (Join-Path $wwwroot 'Implem.CodeDefiner') | Out-Null
Expand-Archive -LiteralPath $zipPath -DestinationPath $wwwroot
Copy-Item examples/holidays.csv (Join-Path $smokeHome 'holidays.csv')
$csv = Join-Path $smokeHome 'holidays.csv'
# パラメーターのフォルダ（Pleasanter の App_Data/Parameters に倣った Holidays.json・Style.json・Placement.json）。相対パスはこのフォルダ基準で解決される。
$parameters = Join-Path $smokeHome 'Parameters'
New-Item -ItemType Directory -Force $parameters | Out-Null
@{ GoogleCalendarIds = @(); CsvFiles = @('../holidays.csv'); FromYear = 2026; ToYear = 2026 } | ConvertTo-Json | Set-Content (Join-Path $parameters 'Holidays.json') -Encoding utf8
@{ Prune = $true; OutputPath = $styles } | ConvertTo-Json | Set-Content (Join-Path $parameters 'Placement.json') -Encoding utf8
@{ Prefix = 'Smoke_' } | ConvertTo-Json | Set-Content (Join-Path $parameters 'Style.json') -Encoding utf8
$app = Join-Path $wwwroot 'CalendarStyleCreator/VehicleVision.PleasanterTools.CalendarStyleCreator.dll'
$names = @('WEBSITE_SITE_NAME','WEBSITE_INSTANCE_ID','DOTNET_ENVIRONMENT','CALENDARSTYLE_PLEASANTER_API_KEY','CALENDARSTYLE_ICAL_BEARER_TOKEN','CALENDARSTYLE_ICAL_BASIC_USER')
$previous = @{}
foreach ($name in $names) { $previous[$name] = [Environment]::GetEnvironmentVariable($name) }
$count = 0
function Invoke-Checked([string[]]$Arguments, [int]$ExpectedExit = 0, [string]$ExpectedText) {
    # stderr も文字列として捕捉し、文字コードに依存する非 ASCII の出力がないことを確認する。
    $lines = & $DotnetPath $app @Arguments 2>&1
    $code = $LASTEXITCODE
    $text = $lines -join "`n"
    if ($code -ne $ExpectedExit) { throw "Unexpected CLI exit code: $code. $text" }
    if ($text -match '[^\x00-\x7F]') { throw 'Non-ASCII console output detected.' }
    if ($text -match 'PRIVATE_TEST_MARKER_DO_NOT_LOG') { throw 'A credential leaked into console output.' }
    if ($ExpectedText -and $text -notmatch $ExpectedText) { throw "Missing expected CLI message: $ExpectedText" }
    $script:count++
}
try {
    $env:WEBSITE_SITE_NAME = 'CalendarStyleCreator-local-appservice-test'
    $env:WEBSITE_INSTANCE_ID = 'local-test-instance'
    $env:DOTNET_ENVIRONMENT = 'Production'
    $env:CALENDARSTYLE_PLEASANTER_API_KEY = ''
    $env:CALENDARSTYLE_ICAL_BEARER_TOKEN = ''
    $env:CALENDARSTYLE_ICAL_BASIC_USER = ''
    Push-Location $outside
    try {
        Invoke-Checked -Arguments @('help') -ExpectedText 'Usage: CalendarStyleCreator'
        # 既定のパラメーター（Pleasanter と同じ App_Data/Parameters 構成）が配布物に含まれる。
        foreach ($file in 'Holidays.json', 'Style.json', 'Placement.json') { if (-not (Test-Path -LiteralPath (Join-Path $wwwroot "CalendarStyleCreator/App_Data/Parameters/$file"))) { throw "Default parameter file is missing from the package: $file" } }
        $script:count++
        # ネットワークを使わない取得元（CSV のみ）で、本体の ExtendedStyles フォルダへ書き出す。
        Invoke-Checked -Arguments @('generate','/calendar-id','none','/csv',$csv,'/from','2026','/to','2027','/output',$styles) -ExpectedText 'Done. 7 holiday'
        $core = Join-Path $styles 'vvcsc_core.css'
        $year = Join-Path $styles 'vvcsc_2026.css'
        if (-not (Test-Path -LiteralPath $core) -or -not (Test-Path -LiteralPath (Join-Path $styles 'vvcsc_2027.css'))) { throw 'Core and per-year styles were not generated.' }
        $css = Get-Content -LiteralPath $year -Raw -Encoding utf8
        if ($css -notmatch 'data-id="2026/1/1"' -or $css -notmatch 'data-date="2026-01-01"') { throw 'Generated extended style is missing expected selectors.' }
        if ($css -notmatch '元日') { throw 'Japanese holiday names were not preserved in the generated file.' }
        # CSV の第 3 列（種類）: 平日（振替出勤）は土日の色を打ち消す。
        if ($css -notmatch '振替出勤' -or $css -notmatch 'background-color: transparent') { throw 'The weekday type in the CSV was not applied.' }
        $script:count++
        # 管理対象（接頭辞）の古いファイルは /prune のときだけ消え、それ以外のファイルには触れない。
        Set-Content -LiteralPath (Join-Path $styles 'vvcsc_2025.css') -Value '/* old */'
        Set-Content -LiteralPath (Join-Path $styles 'mine.css') -Value '/* mine */'
        Invoke-Checked -Arguments @('generate','/calendar-id','none','/csv',$csv,'/from','2026','/to','2027','/output',$styles) -ExpectedText 'WARNING: Obsolete style'
        if (-not (Test-Path -LiteralPath (Join-Path $styles 'vvcsc_2025.css'))) { throw 'Obsolete file was deleted without /prune.' }
        Invoke-Checked -Arguments @('generate','/calendar-id','none','/csv',$csv,'/from','2026','/to','2027','/output',$styles,'/prune') -ExpectedText 'Deleted obsolete style'
        if ((Test-Path -LiteralPath (Join-Path $styles 'vvcsc_2025.css')) -or -not (Test-Path -LiteralPath (Join-Path $styles 'mine.css'))) { throw '/prune did not limit itself to managed files.' }
        $script:count++
        Invoke-Checked -Arguments @('generate','/parameters',$parameters) -ExpectedText 'Done. 7 holiday'
        if (-not (Test-Path -LiteralPath (Join-Path $styles 'Smoke_core.css')) -or -not (Test-Path -LiteralPath (Join-Path $styles 'Smoke_2026.css'))) { throw 'Parameter files were not applied.' }
        $script:count++
        Invoke-Checked -Arguments @('generate','/csv',$csv,'/calendar-id','none','/output',$styles,'/site-id','100') -ExpectedExit 2 -ExpectedText 'applies to all sites'
        Invoke-Checked -Arguments @('generate','-output',$styles,'/csv',$csv) -ExpectedExit 2 -ExpectedText 'Options start with /'
        # /output も /p も省略: 隣の Implem.Pleasanter を既定の出力先にする（配布 ZIP の標準の配置）。
        Remove-Item -LiteralPath (Join-Path $styles 'vvcsc_2026.css')
        Invoke-Checked -Arguments @('generate','/csv',$csv,'/calendar-id','none','/from','2026','/to','2026') -ExpectedText 'Done. 7 holiday'
        if (-not (Test-Path -LiteralPath (Join-Path $styles 'vvcsc_2026.css'))) { throw 'The default output folder beside the tool was not used.' }
        Invoke-Checked -Arguments @('generate','/csv',$csv,'/calendar-id','none','/p',$pleasanter,'/from','2026','/to','2026') -ExpectedText 'Done. 7 holiday'
        Invoke-Checked -Arguments @('generate','/ical-url','http://example.invalid/a.ics','/output',$styles) -ExpectedExit 2 -ExpectedText 'must be an absolute https URL'
        # API 登録: 到達できない URL でも資格情報を出力せず、英語のエラーで終了する。
        $env:CALENDARSTYLE_PLEASANTER_API_KEY = 'PRIVATE_TEST_MARKER_DO_NOT_LOG'
        Invoke-Checked -Arguments @('register','/calendar-id','none','/csv',$csv,'/url','http://127.0.0.1:1') -ExpectedExit 2 -ExpectedText 'WARNING: The API key is sent over http'
        Invoke-Checked -Arguments @('register','/calendar-id','none','/csv',$csv,'/url','ftp://pleasanter.example.invalid') -ExpectedExit 2 -ExpectedText 'http or https'
    } finally { Pop-Location }
} finally {
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $previous[$name]) }
}
Write-Output "App Service layout smoke checks passed ($Platform): $count."
Write-Output "Fixture retained under: $smokeHome"
# 最後に実行した CLI の終了コード（期待した失敗の 2）がスクリプトの終了コードにならないよう、成功を明示する。
exit 0
