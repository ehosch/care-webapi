# Care Coordination — WebAPI

A self-hosted, single-tenant .NET 9 Web API for coordinating family/caregiver
support for a patient: invite-based membership, a recurring 24-hour shift
calendar, self-assign/replacement requests, shift notes, a document library,
and email/SMS notifications.

This is the backend for the Care Coordination project. The Blazor
WebAssembly frontend lives in the companion
[`care-wasm`](https://github.com/ehosch/care-wasm) repository.

## Status

Early scaffold (Phase 0) — authentication, background jobs, and the data model
are wired up, but the shift calendar, documents, and notification features
described below are not yet implemented. See [Roadmap](#roadmap).

## Tech stack

- **.NET 9** Web API, JWT authentication (ASP.NET Core Identity)
- **MySQL** via EF Core / Pomelo
- **Hangfire** for background jobs (rolling shift generation, notification dispatch)
- **NSwag** for OpenAPI/Swagger and typed client generation
- Clean Architecture: `Core/Domain`, `Core/Application`, `Infrastructure`, `Host`

## Quickstart (full stack)

The fastest way to run the whole app — MySQL + this API + the
[`care-wasm`](https://github.com/ehosch/care-wasm) frontend — using the
published images, no source checkout or build required beyond this one file:

```bash
curl -O https://raw.githubusercontent.com/ehosch/care-webapi/main/docker-compose.full.yml
curl -O https://raw.githubusercontent.com/ehosch/care-webapi/main/.env.full.example
cp .env.full.example .env.full   # edit — see Customization below
docker compose -f docker-compose.full.yml --env-file .env.full up -d
```

The API listens on `API_PORT` (default `5010`) and the WASM app on
`WASM_PORT` (default `5011`). Check both are up:

```bash
curl http://localhost:5010/api/health
curl http://localhost:5011/
```

### Customization

Everything customizable lives in `.env.full` (never commit this file — it's
gitignored):

| Variable | What it controls |
|---|---|
| `WASM_URL` | Public URL of the frontend — sets the API's CORS allow-list. Must match wherever `care-wasm` is actually reachable from a browser. |
| `API_URL` | Public URL of this API — baked into the frontend's `ApiBaseUrl` at container start. |
| `API_PORT` / `WASM_PORT` | Host ports, if you're not fronting this with a reverse proxy. |
| `DB_ROOT_PASSWORD` | MySQL root password — pick your own, don't use the example value. |
| `JWT_KEY` | JWT signing key — **32+ characters**, generate a random one (e.g. `openssl rand -base64 32`). |
| `MAIL_HOST` / `MAIL_PORT` / `MAIL_FROM` / `MAIL_USERNAME` / `MAIL_PASSWORD` | Any SMTP provider works — Mailgun, SendGrid, Postmark, your own relay. Leave `MAIL_USERNAME`/`MAIL_PASSWORD` blank for an unauthenticated relay. |
| `TWILIO_ACCOUNT_SID` / `TWILIO_AUTH_TOKEN` / `TWILIO_FROM_NUMBER` | Optional — leave blank to run without SMS notifications. |
| `HANGFIRE_PASSWORD` | Basic-auth password for the `/jobs` Hangfire dashboard (user is always `Admin`). |

Behind a reverse proxy (nginx, Caddy, Traefik) with your own domain, point
`WASM_URL`/`API_URL` at the public HTTPS URLs and proxy `API_PORT`/`WASM_PORT`
to them — the containers themselves don't need to know about TLS.

**First-boot note:** MySQL's official image restarts itself internally right
after first-time initialization. `care-webapi`'s healthcheck-gated startup can
still race that restart and crash once or twice before succeeding — this is
expected and self-heals within about a minute (`restart: on-failure` retries
automatically). If `docker compose ps` still doesn't show `care-webapi` as
running after a couple of minutes, check `docker logs care-webapi` for an
actual configuration problem (wrong password, missing `JWT_KEY`, etc.).

## Local development

For working on the source itself rather than just running the app:

1. Copy config templates and fill in your own values:
   ```bash
   cp src/Host/Configurations/database.example.json src/Host/Configurations/database.json
   cp src/Host/Configurations/hangfire.example.json src/Host/Configurations/hangfire.json
   cp src/Host/Configurations/mail.example.json src/Host/Configurations/mail.json
   cp src/Host/Configurations/security.example.json src/Host/Configurations/security.json
   cp src/Host/Configurations/sms.example.json src/Host/Configurations/sms.json
   ```
   `security.json`'s JWT key must be **32+ characters**.
2. Start a MySQL instance (see `docker-compose.yml` in this repo, or point at
   any MySQL 8 instance) and update the connection strings above.
3. `dotnet run --project src/Host`
4. Swagger UI: `https://localhost:7100/swagger`

### Docker (build from source)

```bash
cp .env.example .env   # fill in your own values — never commit .env
docker compose up -d --build
```

The API listens on host port `5010` by default.

### Pre-built image

Tagged releases and `main` are published to `ghcr.io/ehosch/care-webapi`:

```bash
docker pull ghcr.io/ehosch/care-webapi:latest
```

## Configuration

Non-sensitive config (`cors.json`, `logger.json`) is committed. Sensitive
config (`database.json`, `hangfire.json`, `mail.json`, `security.json`,
`sms.json`) is gitignored — copy from the matching `*.example.json` template.
In Docker/production, every setting can instead be supplied via `__`-separated
environment variables (e.g. `SecuritySettings__JwtSettings__Key`) — see
`docker-compose.yml` / `docker-compose.full.yml`.

**Never commit real values for `security.json`'s JWT key, database
credentials, mail/SMTP credentials, or Twilio keys.**

## Troubleshooting

**`docker compose pull` fails with `denied: denied` even though the image is
public.** A stale `docker login ghcr.io` credential (e.g. an expired or
insufficiently-scoped personal access token from an unrelated project) takes
precedence over anonymous pull. Fix: `docker logout ghcr.io`, then retry.

**`care-webapi` restarts a few times on first boot, then comes up fine.** See
the first-boot note under Quickstart above — this is a known MySQL
initialization race, not a misconfiguration.

## Roadmap

Invite/registration flow, the shift calendar, self-assign/replacement
requests, shift notes, document library, and email/SMS notifications are
planned but not yet built. See `CLAUDE.md` for current architecture notes.

## License

[GNU General Public License v3.0](LICENSE).
