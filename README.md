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

## Getting started

### Local development

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

### Docker

```bash
cp .env.example .env   # fill in your own values — never commit .env
docker compose up -d --build
```

The API listens on host port `5010` by default.

### Pre-built image

Tagged releases and `main` are published to
`ghcr.io/ehosch/care-webapi`:

```bash
docker pull ghcr.io/ehosch/care-webapi:latest
```

## Configuration

Non-sensitive config (`cors.json`, `logger.json`) is committed. Sensitive
config (`database.json`, `hangfire.json`, `mail.json`, `security.json`,
`sms.json`) is gitignored — copy from the matching `*.example.json` template.
In Docker/production, every setting can instead be supplied via `__`-separated
environment variables (e.g. `SecuritySettings__JwtSettings__Key`) — see
`docker-compose.yml`.

**Never commit real values for `security.json`'s JWT key, database
credentials, mail/SMTP credentials, or Twilio keys.**

## Roadmap

Invite/registration flow, the shift calendar, self-assign/replacement
requests, shift notes, document library, and email/SMS notifications are
planned but not yet built. See `CLAUDE.md` for current architecture notes.

## License

[GNU General Public License v3.0](LICENSE).
