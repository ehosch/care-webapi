# CLAUDE.md — care-webapi

.NET 9 ASP.NET Core Web API for the Care Coordination site (single-tenant care/shift
coordination app for a family/caregiver group). Scaffolded fresh (not forked) but
modeled on `gatekeeper-webapi-net9`'s Clean Architecture patterns, with all
multi-tenancy stripped out. Full product spec: `../CLAUDE_CARE.md`. Companion
frontend: `../care-wasm/`.

## Architecture

```
src/
├── Core/Domain/          # Entities: Invite, Document, ShiftTemplate, Shift, ReplacementRequest, ShiftNote
├── Core/Application/     # MediatR/FluentValidation scaffolding (mostly empty — Phase 1+ fills this in)
├── Infrastructure/       # EF Core (MySQL/Pomelo), ASP.NET Identity, JWT auth, Hangfire, Serilog, NSwag
├── Host/                 # ASP.NET Core entry point, controllers, Configurations/
└── Migrators/Migrators.MySQL/  # EF Core migrations project
```

No `Core/Shared` layer and no `Multitenancy/` folders — single-tenant, so `ApplicationUser`
(Infrastructure/Identity) has no tenant claim, and `TokenService` has no tenant
active/expiry gate (unlike gatekeeper's `TokenService.GetTokenAsync`).

**Roles**: plain ASP.NET Identity `IdentityRole` (`Admin`, `Member`), seeded at
startup by `ApplicationDbInitializer` — no granular permission-claim catalog like
gatekeeper's `FSHPermissions` (not needed with only 2 roles).

## Common commands

```bash
dotnet build
dotnet run --project src/Host          # dev ports: http 5100, https 7100
dotnet test --no-build --verbosity normal

# Add a migration
dotnet ef migrations add <Name> \
  --project src/Migrators/Migrators.MySQL/Care.WebApi.Migrators.MySQL.csproj \
  --startup-project src/Host/Care.WebApi.Host.csproj \
  --context ApplicationDbContext --output-dir Migrations
```

Migrations apply automatically on startup (`ApplicationDbInitializer.InitializeDatabaseAsync`,
called from `Program.cs`) — no manual `dotnet ef database update` step needed.

## Configuration files

**Committed** (non-sensitive): `cors.json`, `logger.json`.

**Gitignored** (create from `*.example.json` templates): `database.json`,
`hangfire.json`, `mail.json`, `security.json` (JWT key, 32+ chars), `sms.json`
(Twilio). When absent (Docker), all values come from `__`-separated env vars —
see `docker-compose.yml` / `.env.example`.

## Docker

```bash
# 1. MySQL (dedicated instance, NOT shared with gatekeeper/NIM)
cd ../databases/care-mysql && docker compose up -d

# 2. API
cp .env.example .env   # fill in values
docker compose up -d --build
```

| Container | Host Port | Network |
|---|---|---|
| `care-mysql` | 3307 → 3306 | `care-mysql_default` |
| `care-webapi` | 5010 → 8080 | joins `care-mysql_default` |

**Required Hangfire env vars in Docker** — `hangfire.json` is gitignored/excluded
from the image, but `HangfireSettings:Route` and `HangfireSettings:Dashboard:*`
have no code-level defaults. Omitting them throws
`ArgumentNullException: Value cannot be null. (Parameter 'pathMatch')` from
`UseHangfireDashboard` at startup. `docker-compose.yml` sets these explicitly:
`HangfireSettings__Route`, `HangfireSettings__Dashboard__AppPath`,
`HangfireSettings__Dashboard__StatsPollingInterval`,
`HangfireSettings__Dashboard__DashboardTitle`. Don't drop these if you touch
the compose file.

## Known gotchas from Phase 0 scaffolding

- **EF Core design-time host build.** `dotnet ef migrations add` triggers
  `HostAbortedException` (not `StopTheHostException`, despite that being the
  name gatekeeper's `Program.cs` checks for) when the tool inspects services
  without fully running the app. `Program.cs`'s catch clause here checks for
  both type names — don't revert to checking only `StopTheHostException`, or
  every `migrations add`/`dbcontext info` run logs a scary (but harmless)
  `Log.Fatal`.
- **`Host` must reference `Migrators.MySQL` directly** (not just transitively
  via `Infrastructure`) — the EF migrations assembly
  (`Care.WebApi.Migrators.MySQL`) has to land in `Host`'s output directory or
  `dotnet ef migrations add` fails with "File ...Migrators.MySQL.dll not
  found." Matches gatekeeper's own `Host.csproj` reference list.

## Data model

See `../CLAUDE_CARE.md` for the full spec (data model, phased build plan,
notification triggers). Phase 0 (this scaffold) only covers entities +
auth/Hangfire/Docker wiring — no business logic (CQRS handlers, controllers
beyond `TokensController`) exists yet. `ShiftGenerationJob` is a logging stub;
real `ShiftTemplate → Shift` rollover logic is Phase 3.
