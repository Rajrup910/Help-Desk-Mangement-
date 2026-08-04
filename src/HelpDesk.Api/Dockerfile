# Build from the repository root:
#   docker build -f src/HelpDesk.Api/Dockerfile -t helpdesk-api .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first, with only the project files copied, so the layer caches across code edits.
COPY HelpDesk.sln ./
COPY src/HelpDesk.Contracts/HelpDesk.Contracts.csproj src/HelpDesk.Contracts/
COPY src/HelpDesk.Api/HelpDesk.Api.csproj src/HelpDesk.Api/
RUN dotnet restore src/HelpDesk.Api/HelpDesk.Api.csproj

COPY src/HelpDesk.Contracts/ src/HelpDesk.Contracts/
COPY src/HelpDesk.Api/ src/HelpDesk.Api/
RUN dotnet publish src/HelpDesk.Api/HelpDesk.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# The SQLite file lives on a volume so tickets survive a container restart.
RUN mkdir -p /app/data && chown -R $APP_UID:$APP_UID /app/data
VOLUME /app/data

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ConnectionStrings__DefaultConnection="Data Source=/app/data/helpdesk.db"
EXPOSE 8080

# Run as the non-root user the base image provides.
USER $APP_UID

HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
    CMD ["dotnet", "--info"]

ENTRYPOINT ["dotnet", "HelpDesk.Api.dll"]
