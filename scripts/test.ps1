$ErrorActionPreference = 'Stop'
dotnet run --project tests/CalendarStyleCreator.Tests -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Unit checks failed.' }
