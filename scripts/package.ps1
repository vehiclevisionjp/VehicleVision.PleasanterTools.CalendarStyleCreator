param([string]$Output = 'artifacts/package')
$ErrorActionPreference = 'Stop'
$version = ([xml](Get-Content Directory.Build.props -Raw)).Project.PropertyGroup.VersionPrefix | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { throw 'VersionPrefix is not defined in Directory.Build.props.' }
$folder = Join-Path $Output 'portable/CalendarStyleCreator'
dotnet publish src/CalendarStyleCreator/CalendarStyleCreator.csproj -c Release --self-contained false --no-restore -p:DebugType=none -p:DebugSymbols=false -o $folder
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
Copy-Item LICENSE, NOTICE -Destination $folder
if (Get-ChildItem -LiteralPath $folder -Recurse -Force | Where-Object { $_.Name -match '^(\.git($|modules$)|_reference$|Implem\.)' }) {
    throw 'Reference source or Git metadata leaked into distribution.'
}
$zipName = "VehicleVision.PleasanterTools.CalendarStyleCreator-$version-portable.zip"
Compress-Archive -Path $folder -DestinationPath (Join-Path $Output $zipName) -Force
$hash = Get-FileHash (Join-Path $Output $zipName) -Algorithm SHA256
"$($hash.Hash.ToLowerInvariant())  $zipName" | Set-Content (Join-Path $Output "$zipName.sha256") -Encoding utf8
