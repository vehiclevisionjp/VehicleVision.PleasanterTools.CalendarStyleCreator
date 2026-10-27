FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props ./
COPY src/CalendarStyleCreator/ src/CalendarStyleCreator/
RUN dotnet restore src/CalendarStyleCreator/CalendarStyleCreator.csproj --locked-mode
RUN dotnet publish src/CalendarStyleCreator/CalendarStyleCreator.csproj -c Release --no-restore -o /app
FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /tools/CalendarStyleCreator
COPY --from=build /app/ ./
COPY LICENSE NOTICE ./
ENTRYPOINT ["dotnet", "VehicleVision.PleasanterTools.CalendarStyleCreator.dll"]
