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
├── Core/Application/     # Interfaces (ITokenService, IUserService, IMailService, ISmsService, INotificationService, IDocumentService, IDocumentStorageService, IShiftService — shifts + replacement requests + notes) + DTOs — see note below
├── Infrastructure/       # EF Core (MySQL/Pomelo), ASP.NET Identity, JWT auth, Hangfire, Mailing, Sms, Notifications, Serilog, NSwag
├── Host/                 # ASP.NET Core entry point, controllers, Configurations/
└── Migrators/Migrators.MySQL/  # EF Core migrations project
```

No `Core/Shared` layer and no `Multitenancy/` folders — single-tenant, so `ApplicationUser`
(Infrastructure/Identity) has no tenant claim, and `TokenService` has no tenant
active/expiry gate (unlike gatekeeper's `TokenService.GetTokenAsync`).

**Roles**: plain ASP.NET Identity `IdentityRole` (`Admin`, `Member`), seeded at
startup by `ApplicationDbInitializer` — no granular permission-claim catalog like
gatekeeper's `FSHPermissions` (not needed with only 2 roles).

**No MediatR for Identity features.** `TokenService`/`UserService` implement
Application-layer interfaces (`ITokenService`/`IUserService`) directly against
`UserManager<ApplicationUser>`, and `TokensController`/`UsersController` call
them straight — no MediatR commands. Wrapping this in MediatR would force
Application-layer handlers to depend on Infrastructure-only types
(`ApplicationUser`, `UserManager`, `ApplicationDbContext`), breaking the layer
boundary for no benefit at this scale. Request DTOs use DataAnnotations
(`[Required]`, `[EmailAddress]`, `[MinLength]`) — `[ApiController]` validates
them automatically, no FluentValidation/pipeline-behavior wiring needed.
**On a positional record, DataAnnotations attributes must NOT use the
`[property: ...]` target** (e.g. `[Required] string Email`, not
`[property: Required] string Email`) — with the property-only target, ASP.NET
Core's model binder throws `"...validation metadata defined on property
'Email' that will be ignored... must be associated with the constructor
parameter"` at request time. Found this via a live 500 while testing the
invite endpoint.

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

Uploaded documents live on disk under the `care-documents` named volume
(`/app/documents` inside the container, `DocumentStorageSettings__StoragePath`)
— not in MySQL. Back it up alongside the database if the actual files matter,
not just their metadata.

There's also `docker-compose.full.yml` + `.env.full.example` — a standalone
quickstart that pulls both the `care-webapi` and `care-wasm` published images
plus a fresh MySQL, for people who just want to run the app (documented in
README's "Quickstart (full stack)"). Keep both compose files' env var lists
in sync if `Configurations/*.json` settings gain new fields.

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

- **`Program.cs`'s top-level catch must call `Environment.Exit(1)` after
  `Log.Fatal`.** Without it, a fatal startup exception (e.g. the database
  isn't reachable yet) still exits the process with code 0 — Docker's
  `restart: on-failure` policy only triggers on a non-zero exit, so the
  container would just die once and stay dead instead of retrying. Found
  this the hard way testing `docker-compose.full.yml`'s cold start.
- **MySQL's official image restarts itself internally after first-time init**
  (creating the database/root password), and a plain `mysqladmin ping`
  healthcheck can report "healthy" during the brief window *before* that
  restart — so `depends_on: condition: service_healthy` doesn't fully
  eliminate a race on a truly cold volume. `docker-compose.full.yml` still
  crash-loops `care-webapi` a few times on first boot before the
  `Environment.Exit(1)` fix above lets `restart: on-failure` catch it. This
  is expected and self-heals within about a minute — documented in the
  README's Quickstart/Troubleshooting sections rather than "fixed", since
  it's inherent to the base MySQL image, not something a healthcheck tweak
  reliably prevents.
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

## Known gotchas from Phase 1 (Auth & Users)

- **`AddIdentity()` + JWT bearer auth: the single-string
  `AddAuthentication(JwtBearerDefaults.AuthenticationScheme)` overload only
  sets `DefaultScheme`, not `DefaultChallengeScheme`.** `AddIdentity()` (called
  first, per `Infrastructure/Auth/Startup.cs`'s "must add identity before
  auth" ordering) registers its own cookie scheme and explicitly sets
  `DefaultChallengeScheme` to it. Since that's a different property than the
  one the string overload sets, Identity's cookie scheme wins — every
  `[Authorize]` challenge redirected to a nonexistent `/Account/Login`
  (a `302`) instead of returning `401`, discovered when the first real
  protected endpoint (`POST /api/users/invite`) was tested. Fixed in
  `Infrastructure/Auth/Jwt/Startup.cs` by using the lambda overload and
  setting both `DefaultAuthenticateScheme` and `DefaultChallengeScheme`
  explicitly — matching gatekeeper's own `AddJwtAuth`, which already does
  this (don't regress to the simpler string overload).
- **A controller action returning bare `Ok()`/`IActionResult` with no
  `[ProducesResponseType]` produces an OpenAPI schema of
  `application/octet-stream`**, so NSwag generates a `Task<FileResponse>`
  client method instead of `Task`. Every no-body action in
  `UsersController` has `[ProducesResponseType(StatusCodes.Status200OK)]`
  specifically to keep the generated care-wasm client methods as plain
  `Task` — add the same attribute to any new bare-`Ok()` action.
- **Invite/password-reset links are logged at `Information` level**
  (`UserService.CreateAndSendInviteAsync`/`ForgotPasswordAsync`) in addition
  to being emailed via a Hangfire-enqueued `IMailService.SendAsync`. This is
  deliberate, not leftover debug logging — a self-hoster without SMTP
  configured yet shouldn't be locked out of onboarding. Don't remove it.
- **`care-wasm`'s `JwtAuthenticationHeaderHandler` needs every new
  `[AllowAnonymous]` route added to its own allowlist** — see that repo's
  `CLAUDE.md`. Forgetting this makes a logged-out user get redirected to
  `/login` instead of reaching the anonymous page, even though the API
  itself is configured correctly.

## Known gotchas from Phase 2 (Documents)

- **`Document` is the current version; `DocumentVersion` archives what it
  looked like *before* a replace** — not the other way around. On replace,
  the new upload gets a brand-new storage key (no file copying), a
  `DocumentVersion` row is written with the *outgoing* file's fields (which
  stays on disk, now only referenced from the archive row), then `Document`
  itself is overwritten in place and `Version` incremented. `DeleteAsync`
  removes the DB rows first (cascade handles `DocumentVersion`s), then
  best-effort deletes every physical file (current + archived) — matches
  `SmtpMailService`'s "log failures, don't throw" resilience style.
- **The storage key is always a fresh `Guid`, never the original filename or
  any user input.** `LocalDocumentStorageService` still runs
  `Path.GetFileName()` on the key before touching disk as defense-in-depth,
  but the real protection against path traversal is that user input never
  reaches the filesystem path at all — `Document.FileName`/`ContentType` are
  metadata only, used solely for the `Content-Disposition`/`Content-Type`
  response headers on download.
- **Download actions are the *correct*, wanted case of NSwag's
  `Task<FileResponse>` generation** — unlike `UsersController`'s bare-`Ok()`
  actions (Phase 1 gotcha above), `DocumentsController`'s download endpoints
  really do return a binary stream, so no `[ProducesResponseType]` override
  is needed there. `Replace`/`Delete` still need it, same as Phase 1, since
  they return bare `Ok()`.

## Known gotchas from Phase 3 (Care Calendar Core)

- **`ShiftTemplate`/`Shift`/`ShiftType`/`ShiftStatus` entities, their EF
  configs, and DbSets all existed since the Phase 0 scaffold** — only the
  service/controller/UI and the actual generation logic were missing. Don't
  assume a domain type without a controller is unimplemented at the model
  layer too; check `Core/Domain` first.
- **`ShiftGenerationJob` needs both a daily `RecurringJob.AddOrUpdate` *and*
  an immediate one-off `BackgroundJob.Enqueue`** on every app start
  (`Infrastructure/BackgroundJobs/Startup.cs`'s `UseShiftGenerationJob`).
  Hangfire recurring jobs only fire on the next cron tick, not at
  registration time — without the immediate enqueue, a fresh deployment's
  calendar would stay empty until the next midnight UTC.
- **`Shift(Date, ShiftType)` has a unique composite index** (added in the
  `AddShiftUniqueIndex` migration) as insurance against the generation job
  ever double-inserting a shift for the same slot — cheap to add, expensive
  to debug a duplicate-shift bug without it.
- **This is the first feature where `DateOnly` crosses the API boundary**
  (the `weekStart` query parameter and `ShiftDto.Date`). NSwag's configured
  `dateType: System.DateTimeOffset` (`ApiClient/nswag.json`) means the
  generated care-wasm client uses `DateTimeOffset`, not `DateOnly` — care-wasm
  code converts with `DateOnly.FromDateTime(dto.Date.Date)` when comparing
  against grid cells.
- **Shift-block times are fixed defaults seeded once at startup**
  (`ApplicationDbInitializer`), not admin-editable yet — matches the spec's
  own "adjustable, admin-configurable later" framing for `ShiftTemplate`.
  Don't add template CRUD without checking this is still the desired scope.

## Known gotchas from Phase 4 (Self-Assign & Replacement Requests)

- **`ReplacementRequest`/`ReplacementRequestStatus` entities, DbSet, and a
  no-op `ReplacementRequestConfig` all existed since Phase 0** — same
  "entity pre-built, logic missing" situation Phase 3 found for
  `ShiftTemplate`/`Shift`. No FK constraint on `ReplacementRequest.ShiftId`
  (plain column, no navigation property) — matches this codebase's flat-FK
  style elsewhere (`Document.UploadedByUserId`, etc.), so Phase 4 didn't
  introduce a real EF relationship either.
- **All new business rules live in `ShiftService`, not a separate
  `ReplacementRequestService`** — claiming a replacement request mutates
  both the `ReplacementRequest` row and the `Shift` row atomically in one
  `ApplicationDbContext`, so keeping both in one service avoids a
  cross-service transaction.
- **Exception choice follows an existing split, not a new convention**:
  state-conflicts (shift already claimed, request no longer pending) throw
  `ConflictException` (409); "wrong person" checks (not the assigned user
  requesting a replacement, not the requester cancelling) throw
  `ForbiddenException` (403) — matches `UserService`'s existing
  `ConflictException` usage for state rules and
  `ConfigureJwtBearerOptions.cs`'s existing `ForbiddenException` usage for
  authorization-flavored rules.
- **`AssignShiftAsync` (Admin direct-assign) must cancel any `Pending`
  `ReplacementRequest` for that shift** before applying the new assignment —
  otherwise an Admin overriding a shift with an open replacement request
  leaves a stale `Pending` row in the queue pointing at a shift that's
  already been reassigned. Don't remove this call if you touch
  `AssignShiftAsync` again.
- **`ShiftDto` carries `PendingReplacementRequestId`/
  `PendingReplacementRequestedByUserId` so `GetShiftsAsync` alone gives the
  calendar page everything it needs** (no second fetch to know who to show
  a "Cancel" button to) — safe because the service enforces at most one
  active `Pending` request per shift (creating a new one requires
  `Status == Assigned`, which flips to `ReplacementRequested` immediately).

## Known gotchas from Phase 5 (Shift Notes)

- **`ShiftNote` existed since Phase 0 with only a no-op `ShiftNoteConfig`**
  (property constraints, no index) — same pattern every prior phase found;
  check `Core/Domain` before assuming a spec feature needs a new entity.
- **`AddShiftNoteAsync` returns the created `ShiftNoteDto`, not bare
  `Ok()`** — deliberately, so care-wasm's notes dialog can append the new
  note to the thread instantly instead of a full reload. Because it returns
  actual JSON data (not void), no `[ProducesResponseType]` override is
  needed on `AddNoteAsync` — that gotcha only applies to bare-`Ok()`
  actions, same reasoning as `DocumentsController`'s `UploadAsync`.
- **This is a deliberately append-only feature** — no edit/delete
  endpoints exist, matching the spec's literal "add a note"/"read it"
  wording. Don't add them without checking this is still the intended
  scope; nothing about `ShiftNote` (no `Status` field, unlike
  `ReplacementRequest`'s scaffolded `Cancelled` value) hints at more.
- **`ShiftDto.NoteCount` is computed via a `GroupBy` count query in
  `GetShiftsAsync`, not a stored column** — keeps the calendar's per-cell
  badge in sync automatically; don't try to cache/store it on `Shift`
  itself, it would just go stale.

## Known gotchas from Phase 6 (Notifications)

- **All the Docker/env plumbing for SMS was already wired since Phase 0** —
  `docker-compose.yml`, `docker-compose.full.yml`, `.env.example`, and
  `.env.full.example` already set `SmsSettings__AccountSid`/`AuthToken`/
  `FromNumber` from `TWILIO_ACCOUNT_SID`/`TWILIO_AUTH_TOKEN`/
  `TWILIO_FROM_NUMBER`, and the `Twilio` NuGet package was already
  referenced. Only the application code (`ISmsService`, `INotificationService`,
  the hook points, phone-number collection) was actually missing — same
  "scaffold anticipated this phase" pattern every prior phase found for its
  own domain entities.
- **`TwilioSmsService` uses `CreateMessageOptions` + `TwilioClient.Init`,
  not a `CreateAsync(body:, from:, to:, username:, password:)` overload** —
  the installed `Twilio` 7.8.1 package's `MessageResource.CreateAsync` only
  has one overload, `CreateAsync(CreateMessageOptions, ITwilioRestClient)`.
  Verified against the package's own XML docs
  (`~/.nuget/packages/twilio/7.8.1/lib/net6.0/Twilio.xml`) before writing
  the service, rather than guessing at a plausible-looking overload.
- **Both `SmtpMailService` and `TwilioSmsService` catch and log instead of
  throwing** — a notification failure (bad/missing credentials, network
  issue) must never block the underlying save (shift assign, replacement
  request, document upload). `TwilioSmsService` additionally no-ops with an
  `Information`-level log (not an error) when `SmsSettings` is entirely
  unconfigured, distinguishing "SMS isn't set up" from "SMS attempt failed."
- **"Shift assigned" fires on both `AssignShiftAsync` (Admin) and
  `ClaimShiftAsync` (self-claim)** — the self-claim case is a confirmation
  to the same user who just acted, not a notification to someone else.
  Don't skip it thinking it's redundant; it's what the spec's "by admin or
  self-claim confirmation" phrasing calls for.
- **Broadcast notifications (`NotifyReplacementRequestedAsync`,
  `NotifyDocumentUploadedAsync`) exclude the actor via `Id != excludeUserId`
  on `UserManager.Users`, filtered to `Status == Active`** — invited-but-not-yet-registered
  users never receive anything, only currently active members.
- **Invites can now go by SMS too, if the Admin supplies a phone number at
  invite time** — `CreateInviteRequest.PhoneNumber` sets
  `ApplicationUser.PhoneNumber` immediately when the invite row is created
  (before the invitee has ever registered), so `CreateAndSendInviteAsync`
  has something to send an SMS to right away. If no phone number is
  supplied at invite time, the invite stays email-only — there's nothing to
  send to yet. This goes through `IBackgroundJobClient`/`ISmsService`
  directly in `UserService.cs`, the same way the invite/reset emails do —
  *not* through `INotificationService`, since invites predate that Phase 6
  abstraction and aren't one of its four shift/document trigger points.
  `ResendInviteAsync` automatically gets SMS too once a phone number is on
  file, since it calls the same shared `CreateAndSendInviteAsync` helper.
- **`RegisterAsync` only overwrites `PhoneNumber` if the registrant
  actually supplies one** (`if (!string.IsNullOrWhiteSpace(phoneNumber))`)
  — fixed after finding it would otherwise silently clear a phone number
  an Admin already set at invite time, if the invitee leaves the Register
  page's optional phone field blank.
- **Phone numbers are optional everywhere** — `RegisterRequest.PhoneNumber`
  has no `[Required]`/format validation (not worth a phone-format library
  dependency for one optional field in a personal-scale app), and
  `NotificationService.EnqueueNotification` only enqueues the SMS half when
  `user.PhoneNumber` is non-empty. A user with no phone just silently gets
  the email half — never an error.

## Data model

See `../CLAUDE_CARE.md` for the full spec (data model, phased build plan,
notification triggers). Phase 1 added `IUserService`/`UsersController`
(invites, registration, roles, forgot/reset password) and
`IMailService`/`SmtpMailService` (plain HTML templates in
`Infrastructure/Mailing/EmailTemplates.cs`, enqueued via Hangfire). Phase 2
added `IDocumentService`/`DocumentsController` (upload/replace/delete with
full version history) and `IDocumentStorageService`/`LocalDocumentStorageService`
(local-disk storage, one file per storage key, no shared code with
gatekeeper's base64-JSON-payload `LocalFileStorageService` — different enough
upload shape that a purpose-built service was simpler than adapting it).
Phase 3 added `IShiftService`/`ShiftsController` (rolling 4-week shift
generation via `ShiftGenerationJob`, admin direct-assign). Phase 4 added
self-claim of open shifts and a full replacement-request flow (create,
cancel, claim, queue) via `ReplacementRequestsController` plus additions to
`IShiftService`/`ShiftsController`. Phase 5 added a per-shift append-only
note thread (`GetShiftNotesAsync`/`AddShiftNoteAsync`). Phase 6 added
`ISmsService`/`TwilioSmsService` and `INotificationService`/
`NotificationService`, hooked into all four shift/document trigger points,
plus phone-number collection (`RegisterRequest.PhoneNumber`,
`IUserService.UpdatePhoneNumberAsync`). Phase 7 fixed the calendar's
mobile-overflow gotcha (see care-wasm's `CLAUDE.md`) — every planned
feature and fix from the original spec is done; only the actual homelab
deployment remains, which is an operational step, not a code change.
