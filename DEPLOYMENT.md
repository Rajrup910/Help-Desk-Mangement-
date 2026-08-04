# Deployment Guide

**Help Desk Management System — Rajrup Roy Chowdhury (IN26010404)**

Two deployable units:

| Unit | Project | Listens on | Talks to |
| --- | --- | --- | --- |
| API | `src/HelpDesk.Api` | 8080 | the database |
| Web UI | `src/HelpDesk.Mvc` | 8080 | the API over HTTP |

The UI has no database access, so **the API must be reachable from the UI's server** — not from the
user's browser. Deploy the API first.

---

## 0. Pre-flight checklist

Run these from the repository root before any deployment.

```bash
dotnet restore
dotnet build -c Release --nologo
dotnet test --nologo
```

Then verify a Release build actually serves traffic:

```bash
dotnet run --project src/HelpDesk.Api -c Release --urls http://localhost:5285
curl http://localhost:5285/health      # expect {"status":"Healthy",...}
```

Decide these before you continue:

- [ ] Database provider — SQLite (default) or SQL Server
- [ ] The public URL of the API
- [ ] The public URL of the UI (needed for the API's CORS list if any browser calls it directly)

---

## 1. Publish

```bash
dotnet publish src/HelpDesk.Api -c Release -o ./publish/api
dotnet publish src/HelpDesk.Mvc -c Release -o ./publish/web
```

Each folder is self-contained apart from the shared .NET 8 runtime. Add
`-r win-x64 --self-contained true` if the target machine has no runtime installed.

---

## 2. Configure

Never edit `appsettings.json` on the server — supply environment variables instead. Nesting uses
a double underscore.

**API**

| Variable | Example |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ASPNETCORE_HTTP_PORTS` | `8080` |
| `Database__Provider` | `Sqlite` or `SqlServer` |
| `ConnectionStrings__DefaultConnection` | `Data Source=/var/helpdesk/helpdesk.db` |
| `Cors__AllowedOrigins__0` | `https://helpdesk.example.com` |

**Web UI**

| Variable | Example |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ASPNETCORE_HTTP_PORTS` | `8080` |
| `ApiSettings__BaseUrl` | `https://helpdesk-api.example.com/` (**trailing slash required**) |

> The trailing slash matters. `HttpClient` drops the last path segment of a `BaseAddress` that does
> not end in `/`. `Program.cs` appends one defensively, but set it correctly anyway.

### Database notes

**SQLite (default).** The schema is created and seeded on first run. Point the connection string at
a directory that survives redeploys and is writable by the app's user — *not* the publish folder,
which most platforms replace on deploy.

**SQL Server.** Create the database and a login first, then:

```bash
Database__Provider=SqlServer
ConnectionStrings__DefaultConnection="Server=sql.example.com;Database=HelpDeskDb;User Id=helpdesk;Password=...;TrustServerCertificate=True"
```

The app calls `EnsureCreated()` at startup, which creates the schema if it is absent. For a system
that will evolve, switch to EF migrations before the first production release:

```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate --project src/HelpDesk.Api
# then replace EnsureCreatedAsync() with MigrateAsync() in Program.cs
dotnet ef database update --project src/HelpDesk.Api
```

---

## 3. Deployment targets

### A. Docker Compose (simplest full-stack option)

```bash
docker compose up --build -d
docker compose ps
docker compose logs -f api
```

UI on <http://localhost:8080>, API docs on <http://localhost:8081/swagger>. Ticket data lives in the
`helpdesk-data` named volume and survives `docker compose down`. To wipe it:
`docker compose down -v`.

To publish the images:

```bash
docker build -f src/HelpDesk.Api/Dockerfile -t <registry>/helpdesk-api:1.0 .
docker build -f src/HelpDesk.Mvc/Dockerfile -t <registry>/helpdesk-web:1.0 .
docker push <registry>/helpdesk-api:1.0
docker push <registry>/helpdesk-web:1.0
```

---

### B. IIS on Windows Server

1. Install the **[.NET 8 Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/8.0)** on the
   server, then `iisreset`. (Without it IIS returns HTTP 500.19.)
2. Create two sites, e.g. `helpdesk-api` on port 8081 and `helpdesk-web` on port 80.
3. For each, set the application pool's **.NET CLR version to "No Managed Code"** — the app runs
   out-of-process in its own dotnet process.
4. Copy `publish/api` and `publish/web` into the site folders.
5. Grant the app pool identity (`IIS AppPool\<pool name>`) **Modify** rights on the folder holding
   the SQLite file.
6. Set the environment variables from step 2 — per site under
   *Configuration Editor → system.webServer/aspNetCore → environmentVariables*, or in each site's
   `web.config`:

```xml
<aspNetCore processPath="dotnet" arguments=".\HelpDesk.Mvc.dll" hostingModel="outofprocess">
  <environmentVariables>
    <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
    <environmentVariable name="ApiSettings__BaseUrl" value="http://localhost:8081/" />
  </environmentVariables>
</aspNetCore>
```

7. Browse the API's `/health` before opening the UI.

---

### C. Azure App Service

```bash
az group create --name helpdesk-rg --location centralindia

az appservice plan create --name helpdesk-plan --resource-group helpdesk-rg --sku B1 --is-linux

az webapp create --name helpdesk-api-rrc --plan helpdesk-plan \
  --resource-group helpdesk-rg --runtime "DOTNETCORE:8.0"
az webapp create --name helpdesk-web-rrc --plan helpdesk-plan \
  --resource-group helpdesk-rg --runtime "DOTNETCORE:8.0"
```

Configure, then deploy:

```bash
az webapp config appsettings set --name helpdesk-api-rrc --resource-group helpdesk-rg --settings \
  ASPNETCORE_ENVIRONMENT=Production \
  Database__Provider=Sqlite \
  ConnectionStrings__DefaultConnection="Data Source=/home/data/helpdesk.db" \
  Cors__AllowedOrigins__0="https://helpdesk-web-rrc.azurewebsites.net"

az webapp config appsettings set --name helpdesk-web-rrc --resource-group helpdesk-rg --settings \
  ASPNETCORE_ENVIRONMENT=Production \
  ApiSettings__BaseUrl="https://helpdesk-api-rrc.azurewebsites.net/"

cd publish/api && zip -r ../api.zip . && cd ../..
cd publish/web && zip -r ../web.zip . && cd ../..

az webapp deploy --name helpdesk-api-rrc --resource-group helpdesk-rg --src-path publish/api.zip --type zip
az webapp deploy --name helpdesk-web-rrc --resource-group helpdesk-rg --src-path publish/web.zip --type zip
```

> Use `/home/...` for the SQLite path. Only `/home` is persistent on App Service; anywhere else is
> wiped on restart. For anything beyond a demo, use Azure SQL and set `Database__Provider=SqlServer`.

---

### D. Linux VM with systemd + Nginx

Copy `publish/api` to `/var/www/helpdesk-api` and `publish/web` to `/var/www/helpdesk-web`, then
create `/etc/systemd/system/helpdesk-api.service`:

```ini
[Unit]
Description=Help Desk API
After=network.target

[Service]
WorkingDirectory=/var/www/helpdesk-api
ExecStart=/usr/bin/dotnet /var/www/helpdesk-api/HelpDesk.Api.dll
Restart=always
RestartSec=10
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_HTTP_PORTS=5001
Environment=ConnectionStrings__DefaultConnection=Data Source=/var/lib/helpdesk/helpdesk.db

[Install]
WantedBy=multi-user.target
```

and `/etc/systemd/system/helpdesk-web.service` with port `5000` and
`Environment=ApiSettings__BaseUrl=http://localhost:5001/`.

```bash
sudo mkdir -p /var/lib/helpdesk && sudo chown www-data:www-data /var/lib/helpdesk
sudo systemctl daemon-reload
sudo systemctl enable --now helpdesk-api helpdesk-web
sudo systemctl status helpdesk-api
```

Nginx reverse proxy:

```nginx
server {
    listen 80;
    server_name helpdesk.example.com;

    location / {
        proxy_pass         http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade $http_upgrade;
        proxy_set_header   Connection keep-alive;
        proxy_set_header   Host $host;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
    }
}
```

```bash
sudo nginx -t && sudo systemctl reload nginx
sudo certbot --nginx -d helpdesk.example.com    # TLS
```

---

### E. Render / Railway (free-tier friendly)

Two services from the same repository:

| | API | Web |
| --- | --- | --- |
| Dockerfile path | `src/HelpDesk.Api/Dockerfile` | `src/HelpDesk.Mvc/Dockerfile` |
| Docker context | repository root | repository root |
| Env | `ASPNETCORE_HTTP_PORTS=8080` | `ASPNETCORE_HTTP_PORTS=8080`, `ApiSettings__BaseUrl=https://<api-service>.onrender.com/` |
| Disk | mount at `/app/data` | — |

Deploy the API first, copy its URL into the web service's `ApiSettings__BaseUrl`, then deploy the web
service. Without a mounted disk the SQLite file resets on every deploy.

---

## 4. Post-deployment verification

```bash
API=https://your-api-host
WEB=https://your-web-host

curl -s $API/health                                  # {"status":"Healthy",...}
curl -s "$API/api/tickets?pageSize=1" | head -c 200   # a JSON page
curl -s -o /dev/null -w "%{http_code}\n" $API/swagger # 200
curl -s -o /dev/null -w "%{http_code}\n" $WEB/        # 200
```

Then click through the UI once:

- [ ] Dashboard loads with the summary cards and both bar charts filled in
- [ ] Search, a filter and a sort header each change the results
- [ ] Raising a ticket redirects to its detail page and shows the intake history entry
- [ ] A status button moves the ticket and adds a history entry
- [ ] Submitting an invalid form shows red field messages, not a crash
- [ ] Deleting a ticket returns to the list with a green success message

---

## 5. Production hardening

Not required for the assignment, but the honest list of what is missing before real users:

| Gap | Why it matters | Fix |
| --- | --- | --- |
| **No authentication** | Anyone reachable can read and delete every ticket | ASP.NET Core Identity or Entra ID; `[Authorize]` on the controllers |
| **No rate limiting** | The create endpoint can be flooded | `builder.Services.AddRateLimiter(...)` (built into .NET 8) |
| **`EnsureCreated()`** | Cannot evolve the schema without dropping data | Switch to EF migrations (see §2) |
| **No backups** | SQLite is one file; losing it loses everything | Scheduled copy of the `.db`, or managed SQL with PITR |
| **Logs to console only** | Nothing to search after an incident | Serilog → file/Seq, or Application Insights |
| **Swagger is public** | Exposes the full API surface | Wrap `app.UseSwagger()` in `if (app.Environment.IsDevelopment())` |
| **No HTTPS enforcement on the API** | Ticket contents travel in clear text | Terminate TLS at the proxy and enable HSTS |

---

## 6. Rollback

```bash
# Docker
docker compose down
docker compose up -d --build   # or re-tag the previous image

# systemd
sudo systemctl stop helpdesk-web helpdesk-api
sudo rm -rf /var/www/helpdesk-api && sudo mv /var/www/helpdesk-api.bak /var/www/helpdesk-api
sudo systemctl start helpdesk-api helpdesk-web

# Azure
az webapp deployment slot swap --name helpdesk-web-rrc --resource-group helpdesk-rg \
  --slot staging --target-slot production
```

Always take a copy of the SQLite file (or a SQL Server backup) before deploying a release that
changes the schema — `EnsureCreated()` will not migrate existing tables, so a model change against
an existing database fails at runtime rather than silently corrupting data.
