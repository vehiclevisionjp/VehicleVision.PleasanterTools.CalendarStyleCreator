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
# 設定ファイル。相対パスは設定ファイルのあるフォルダ基準で解決される。
$configPath = Join-Path $smokeHome 'calendarstyle.json'
@{ calendars = @(); csv = @('holidays.csv'); from = 2026; to = 2026; name = 'SmokeHolidays'; siteIds = @(100); output = $styles } | ConvertTo-Json | Set-Content $configPath -Encoding utf8
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
        # ネットワークを使わない取得元（CSV のみ）で、本体の ExtendedStyles フォルダへ書き出す。
        Invoke-Checked -Arguments @('generate','/calendar-id','none','/csv',$csv,'/from','2026','/to','2026','/output',$styles,'/name','CalendarHolidays') -ExpectedText 'Done\. 4 holiday'
        $generated = Join-Path $styles 'CalendarHolidays.json'
        $style = Get-Content -LiteralPath $generated -Raw -Encoding utf8 | ConvertFrom-Json
        if ($style.Name -ne 'CalendarHolidays' -or $style.Style -notmatch 'data-id="2026/1/1"' -or $style.Style -notmatch 'data-date="2026-01-01"') { throw 'Generated extended style is missing expected selectors.' }
        if ($style.Style -notmatch '元日') { throw 'Japanese holiday names were not preserved in the generated file.' }
        $script:count++
        Invoke-Checked -Arguments @('generate','/config',$configPath) -ExpectedText 'Done\. 4 holiday'
        $fromConfig = Get-Content -LiteralPath (Join-Path $styles 'SmokeHolidays.json') -Raw -Encoding utf8 | ConvertFrom-Json
        if (@($fromConfig.SiteIdList).Count -ne 1 -or $fromConfig.SiteIdList[0] -ne 100) { throw 'Config file settings were not applied.' }
        $script:count++
        Invoke-Checked -Arguments @('generate','-output',$styles,'/csv',$csv) -ExpectedExit 2 -ExpectedText 'Options start with /'
        Invoke-Checked -Arguments @('generate','/csv',$csv,'/calendar-id','none') -ExpectedExit 2 -ExpectedText 'generate requires /output'
        Invoke-Checked -Arguments @('generate','/ical-url','http://example.invalid/a.ics','/output',$styles) -ExpectedExit 2 -ExpectedText 'must be an absolute https URL'
        # API 登録: 到達できない URL でも資格情報を出力せず、英語のエラーで終了する。
        $env:CALENDARSTYLE_PLEASANTER_API_KEY = 'PRIVATE_TEST_MARKER_DO_NOT_LOG'
        Invoke-Checked -Arguments @('register','/calendar-id','none','/csv',$csv,'/url','http://127.0.0.1:1') -ExpectedExit 2 -ExpectedText 'Could not connect to Pleasanter'
        Invoke-Checked -Arguments @('register','/calendar-id','none','/csv',$csv,'/url','http://pleasanter.example.invalid') -ExpectedExit 2 -ExpectedText 'must be https'
    } finally { Pop-Location }
} finally {
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $previous[$name]) }
}
Write-Output "App Service layout smoke checks passed ($Platform): $count."
Write-Output "Fixture retained under: $smokeHome"
