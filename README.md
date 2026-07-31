# Care Coordination — WebAPI

A self-hosted, single-tenant .NET 9 Web API for coordinating family/caregiver
support for a patient: invite-based membership, a recurring 24-hour shift
calendar, self-assign/replacement requests, shift notes, a document library,
and email/SMS notifications.

This is the backend for the Care Coordination project. The Blazor
WebAssembly frontend lives in the companion
[`care-wasm`](https://github.com/ehosch/care-wasm) repository.

## Status

All phases in the original build plan are done — invites, registration,
login, roles, forgot/reset password, a document library with full version
history, a blockless care calendar (no fixed shift types — any contiguous
stretch of a day can be claimed or assigned as its own shift) with
self-claim, admin assign/reassign/delete, a replacement-request
queue/claim flow, a per-shift note thread, and email/SMS notifications for
shift-assigned, replacement-requested, replacement-claimed, shift-removed,
shift-time-changed, and document-uploaded events all work end-to-end. See
[Roadmap](#roadmap).

## First login

The first time the app starts against an empty database, it seeds one Admin
account from `DEFAULT_ADMIN_EMAIL`/`DEFAULT_ADMIN_PASSWORD` (falling back to
`admin@example.com` / `ChangeMe123!` if you didn't set them). **Log in and
change that password immediately** — from the Users page, use "Forgot
password?" on the login screen, since there's no in-app change-password
form yet.

From there: Users page → Invite → enter an email (and, optionally, a phone
number if you want the invite sent by text too, via Twilio) → the invitee
gets a link to `/register?token=...` to set their name and password. **If
you haven't configured a working mail provider yet, the invite/reset link
is also logged at `Information` level** — `docker logs care-webapi` (or the
console, for `dotnet run`) — so onboarding isn't blocked on getting SMTP
right first.

## Documents

Admins upload documents (title, category, any file type up to 50 MB); every
active user can list and download them. Replacing a document keeps the
outgoing file as a downloadable version in history rather than discarding it
— see `GET /api/documents/{id}/versions`. Files are stored on local disk
under a Docker named volume (`care-documents`), not in the database — back
that volume up alongside your MySQL data if you care about the uploaded
files, not just the metadata.

## Care Calendar

There's no fixed shift schedule — a day is just 24 fillable hourly blocks.
Nothing is a "shift" until someone claims or is assigned a contiguous run
of them; uncovered time is simply the absence of a `Shift` row, not a row
with some "open" status. Any active user can view the week grid
(`GET /api/shifts?weekStart=`) and create a new shift over currently
uncovered time for themselves (`POST /api/shifts`, defaults the assignee to
you); Admins can create one for anyone, reassign an existing shift
(`PUT /api/shifts/{id}/assign`), or delete one outright
(`DELETE /api/shifts/{id}`, blocked if it has a pending replacement
request).

Any shift's Admin, or the member it's assigned to, can adjust its
start/end time (`PUT /api/shifts/{id}/times`). Adjacent shifts always stay
glued together when a boundary moves: growing into a neighboring shift
shrinks it, or deletes it outright (with a notification to whoever was
assigned) if your growth fully swallows it; shrinking your own shift lets
an adjacent one reclaim the vacated time if one exists, or simply leaves it
uncovered if nothing's there. There's no confirmation step for any of
this — it always cascades, and the affected person is notified after the
fact.

If you're assigned to a shift you can no longer cover, request a
replacement (`POST /api/shifts/{id}/replacement-requests`, optional reason)
— it appears in an open queue (`GET /api/replacement-requests`) any other
active user can claim (`POST /api/replacement-requests/{id}/claim`), which
reassigns the shift to them and releases you. You can also cancel your own
still-open request (`DELETE /api/replacement-requests/{id}`) if you end up
able to cover it after all. An Admin directly reassigning a shift
automatically cancels any pending replacement request on it.

Any active user can also add a note to any shift and read every note left
on it (`GET`/`POST /api/shifts/{id}/notes`) — a simple append-only comment
thread, not tied to who's assigned, useful for context like "Mom needs help
with X today."

## Notifications

Email and SMS notifications fire automatically for: shift assigned (by an
Admin or a self-claim confirmation), replacement requested (broadcast to
every other active member — anyone might be able to cover it), replacement
claimed (to whoever originally requested it), and new document uploaded
(broadcast to every other active member). Invite emails (Phase 1) are
unaffected — email only, since there's no phone number on file yet at
invite time.

SMS requires a phone number on file — optionally set during registration,
or by an Admin from the Users page at any time. A user with no phone number
just gets the email half; nothing errors or blocks. SMTP (`mail.json`) and
Twilio (`sms.json`) are both optional — if either is unconfigured, that
channel is skipped and logged, same graceful-degradation behavior Phase 1's
email already had.

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
| `DEFAULT_ADMIN_EMAIL` / `DEFAULT_ADMIN_PASSWORD` | Seeded once, only if the database is empty. Change the password after first login — see [First login](#first-login). |
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

Every feature in the original spec is built, plus a post-launch
rearchitecture of the calendar away from a fixed Day/Evening/Overnight
shift model to fully blockless scheduling — see `CLAUDE.md` for current
architecture notes.

## License

[GNU General Public License v3.0](LICENSE).
