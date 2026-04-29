# Docker + Railway deployment guide

The DWA V&V System ships with a multi-stage Dockerfile at the repo root, so it
runs on any container host — Azure App Service (current), Railway, Fly.io,
AWS App Runner, local Docker — without platform-specific code changes.

## 1. Local run with docker-compose

Spin up the app + its SQL Server dependency on your machine:

```bash
docker compose up --build
```

Then browse http://localhost:8080. Demo users are seeded automatically using the
password in `docker-compose.yml` (`Identity__InitialDemoPassword=DwaDemo2026!`).

To tear everything down including the data volume:

```bash
docker compose down
```

The `sqlserver` service has a healthcheck, so `app` only starts once the database
is accepting queries. First-boot migrations + seeding take ~20 seconds.

## 2. Railway deployment

### 2.1 One-time setup

1. Install the Railway CLI (optional but convenient):
   ```bash
   npm i -g @railway/cli
   railway login
   ```
2. In the Railway dashboard, create a new **Project** → **Deploy from GitHub** →
   pick this repo → branch `demo/azure-deploy`. Railway auto-detects the
   `Dockerfile` and honours `railway.toml`.

### 2.2 Database decision

Railway does not offer managed SQL Server. Two supported paths:

**Path A — point Railway → the existing Azure SQL (fastest for next-Tuesday demo):**

- On Azure SQL `sql-dwa-vv-demo`, add Railway's outbound IP ranges to the
  firewall (Railway publishes these at <https://docs.railway.app/reference/static-ip-addresses>)
  *or* temporarily enable the "Allow Azure services and resources to access this server"
  firewall toggle.
- In Railway → your service → **Variables**:
  ```
  ASPNETCORE_ENVIRONMENT=Production
  ConnectionStrings__Default=Server=tcp:sql-dwa-vv-demo.database.windows.net,1433;Initial Catalog=dwa_val_ver;User Id=dwaadmin;Password=<sql-password>;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
  Identity__InitialDemoPassword=<pick-one>
  ```
- Railway injects `PORT`; Kestrel binds to it via `ASPNETCORE_URLS` in the
  Dockerfile, so no Railway-port variable is needed.

**Path B — run SQL Server on Railway:**

Railway lets you deploy any container via a **Database → Add a custom image**
step. Use `mcr.microsoft.com/mssql/server:2022-latest` with the same envvars as
our `docker-compose.yml`, attach a Railway volume to `/var/opt/mssql`, and
point the app's `ConnectionStrings__Default` at the internal service URL.

### 2.3 Deploy

Once variables are set, push to the configured branch and Railway rebuilds +
redeploys. Or trigger manually:

```bash
railway up
```

### 2.4 Railway-specific notes

- **Port:** `PORT` is injected per service; no manual bind needed.
- **HTTPS:** Railway terminates TLS at its edge; the app cookie is configured
  with `SameSite=Lax` and `SecurePolicy=Always`, which works as long as Railway's
  public URL is HTTPS (it always is).
- **Logs:** `railway logs --tail` streams stdout. First boot will show
  `Seeded role SystemAdmin…` through to `Seeded demo user readonly@dwa.demo`.
- **Letter PDFs:** currently stored on the local filesystem inside the container
  (`wwwroot/_uploads`). They survive only until the next deploy/restart. For
  persistence, attach a Railway **Volume** to `/app/wwwroot/_uploads` OR switch
  `IBlobStore` to the (deferred) Azure-Blob implementation.

## 3. Image size sanity-check

Expected after `docker build .`:

| Stage | Approx size |
|---|---|
| `build` (SDK) | ~1.4 GB (intermediate) |
| `runtime` (aspnet + fontconfig + fonts) | ~280 MB |

Only the `runtime` image ships. The multi-stage build discards the SDK layer.

## 4. Health-checking

The Dockerfile doesn't bake in an app-level healthcheck (that needs a real HTTP
probe; Docker's `HEALTHCHECK` can do it but adds curl/wget to the image). Both
Railway and Azure App Service probe an HTTP path externally — Railway uses
`healthcheckPath = "/Account/Login"` from `railway.toml`.

## 5. Common failure modes

| Symptom | Fix |
|---|---|
| `System.Net.Sockets.SocketException: No such host is known.` in the app logs | `ConnectionStrings__Default` points at a hostname the container can't resolve. For docker-compose use `sqlserver`; for Railway-pointing-at-Azure-SQL use the FQDN. |
| `A network-related or instance-specific error occurred` on Azure SQL from Railway | Azure SQL firewall blocks the Railway IP. Add the range or toggle "Allow Azure services". |
| `Identity:InitialDemoPassword not set; skipping demo-user seed.` | The env var wasn't passed into the container. Double-underscore format: `Identity__InitialDemoPassword`. |
| `QuestPDF.DocumentLayoutException` on first letter render | Fonts missing. The Dockerfile installs `fonts-dejavu-core` — if you've stripped it, reinstall. |
| 502 / "Application Error" on Railway cold start | Startup exceeded Railway's 60s default; check `railway logs`. First-boot migrations can take 30s+ on a cold DB. Already budgeted in the healthcheck timeout. |
