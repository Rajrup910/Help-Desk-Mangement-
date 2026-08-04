# Build from the repository root:
#   docker build -f src/HelpDesk.Mvc/Dockerfile -t helpdesk-mvc .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY HelpDesk.sln ./
COPY src/HelpDesk.Contracts/HelpDesk.Contracts.csproj src/HelpDesk.Contracts/
COPY src/HelpDesk.Mvc/HelpDesk.Mvc.csproj src/HelpDesk.Mvc/
RUN dotnet restore src/HelpDesk.Mvc/HelpDesk.Mvc.csproj

COPY src/HelpDesk.Contracts/ src/HelpDesk.Contracts/
COPY src/HelpDesk.Mvc/ src/HelpDesk.Mvc/
RUN dotnet publish src/HelpDesk.Mvc/HelpDesk.Mvc.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ApiSettings__BaseUrl="http://api:8080/"
EXPOSE 8080

USER $APP_UID

ENTRYPOINT ["dotnet", "HelpDesk.Mvc.dll"]
