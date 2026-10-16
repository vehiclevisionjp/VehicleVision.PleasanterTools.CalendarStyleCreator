# 認証付き iCal（Bearer / Basic / リダイレクト）を、Docker の nginx で実際に確認する。ネットワークは localhost だけ。
param([int]$Port = 58890, [string]$App = 'src/CalendarStyleCreator/bin/Release/net10.0/VehicleVision.PleasanterTools.CalendarStyleCreator.dll')
$ErrorActionPreference = 'Stop'
$work = Join-Path (Get-Location) ('temp/ical-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $work | Out-Null
$container = 'calendarstyle-ical-check'
$names = @('T_TOKEN', 'T_USER', 'T_PASS', 'CALENDARSTYLE_ICAL_BEARER_TOKEN')
$previous = @{}
foreach ($name in $names) { $previous[$name] = [Environment]::GetEnvironmentVariable($name) }
try {
    "BEGIN:VCALENDAR`r`nBEGIN:VEVENT`r`nDTSTART;VALUE=DATE:20261103`r`nSUMMARY:Private holiday`r`nEND:VEVENT`r`nEND:VCALENDAR`r`n" | Set-Content (Join-Path $work 'team.ics') -NoNewline
    # nginx の auth_basic は {SHA} 形式（SHA-1 の Base64）を読める。
    $hash = '{SHA}' + [Convert]::ToBase64String([Security.Cryptography.SHA1]::HashData([Text.Encoding]::UTF8.GetBytes('pass123')))
    "user:$hash" | Set-Content (Join-Path $work 'htpasswd')
    @'
events {}
http {
  server {
    listen 80;
    root /data;
    types { text/calendar ics; }
    location /basic/ { auth_basic "x"; auth_basic_user_file /etc/nginx/htpasswd; rewrite ^/basic/(.*)$ /$1 break; }
    location /bearer/ { if ($http_authorization != "Bearer tok-123") { return 401; } rewrite ^/bearer/(.*)$ /$1 break; }
    location /redirect/ { return 302 /bearer/team.ics; }
  }
}
'@ | Set-Content (Join-Path $work 'nginx.conf')
    docker rm -f $container 2>$null | Out-Null
    docker run -d --rm --name $container -p "127.0.0.1:${Port}:80" -v "$work/nginx.conf:/etc/nginx/nginx.conf:ro" -v "$work/htpasswd:/etc/nginx/htpasswd:ro" -v "$work/team.ics:/data/team.ics:ro" nginx:alpine | Out-Null
    for ($i = 0; $i -lt 30; $i++) { try { Invoke-WebRequest "http://127.0.0.1:$Port/redirect/x" -MaximumRedirection 0 -SkipHttpErrorCheck | Out-Null; break } catch { Start-Sleep -Milliseconds 300 } }
    $base = "http://127.0.0.1:$Port"
    $count = 0
    function Run([string[]]$Arguments, [int]$ExpectedExit, [string]$ExpectedText) {
        $text = (& dotnet $App @Arguments 2>&1) -join "`n"
        if ($LASTEXITCODE -ne $ExpectedExit) { throw "Unexpected exit $LASTEXITCODE for '$($Arguments -join ' ')': $text" }
        if ($ExpectedText -and $text -notmatch $ExpectedText) { throw "Missing '$ExpectedText' in: $text" }
        if ($text -match 'tok-123|pass123|bad-token') { throw 'A credential leaked into console output.' }
        $script:count++
    }
    $config = Join-Path $work 'config.json'
    @{ calendars = @(); from = 2026; to = 2026; output = (Join-Path $work 'out')
       icalUrls = @(@{ url = "$base/bearer/team.ics"; bearerTokenEnv = 'T_TOKEN' }, @{ url = "$base/basic/team.ics"; basicUserEnv = 'T_USER'; basicPasswordEnv = 'T_PASS' }) } | ConvertTo-Json -Depth 5 | Set-Content $config -Encoding utf8
    $env:T_TOKEN = 'tok-123'; $env:T_USER = 'user'; $env:T_PASS = 'pass123'
    Run @('generate', '/config', $config) 0 'Done\. 1 holiday'
    if ((Get-Content (Join-Path $work 'out/vvcsc_2026.css') -Raw -Encoding utf8) -notmatch 'Private holiday') { throw 'Holiday from the authenticated iCal is missing.' }
    $env:T_TOKEN = 'bad-token'
    Run @('generate', '/config', $config) 2 'HTTP 401'
    $env:T_TOKEN = 'tok-123'; $env:T_PASS = 'wrong'
    Run @('generate', '/config', $config) 2 'HTTP 401'
    $env:T_PASS = 'pass123'; $env:T_TOKEN = $null
    Run @('generate', '/config', $config) 2 'T_TOKEN is not set'
    Run @('generate', '/calendar-id', 'none', '/ical-url', "$base/bearer/team.ics", '/output', (Join-Path $work 'o1')) 2 'HTTP 401'
    $env:CALENDARSTYLE_ICAL_BEARER_TOKEN = 'tok-123'
    Run @('generate', '/calendar-id', 'none', '/ical-url', "$base/bearer/team.ics", '/from', '2026', '/to', '2026', '/output', (Join-Path $work 'o2')) 0 'Done\. 1 holiday'
    Run @('generate', '/calendar-id', 'none', '/ical-url', "$base/redirect/x", '/output', (Join-Path $work 'o3')) 2 'redirected'
    Write-Output "iCal authentication checks passed: $count."
} finally {
    docker rm -f $container 2>$null | Out-Null
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $previous[$name]) }
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}
