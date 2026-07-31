# CLAUDE.md — care-webapi

.NET 9 ASP.NET Core Web API for the Care Coordination site (single-tenant care/shift
coordination app for a family/caregiver group). Scaffolded fresh (not forked) but
modeled on `gatekeeper-webapi-net9`'s Clean Architecture patterns, with all
multi-tenancy stripped out. Full product spec: `../CLAUDE_CARE.md`. Companion
frontend: `../care-wasm/`.

## Architecture

```
src/
├── Core/Domain/          # Entities: Invite, Document, Shift, ReplacementRequest, ShiftNote
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

**Superseded by Phase 9** — `ShiftType`, `ShiftTemplate`, and the
`ShiftGenerationJob` described below were removed entirely; see Phase 9's
section for the replacement model. Left here for history/context on why
things were originally built this way.

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

## Known gotchas from Phase 8 (Friendly Errors + Adjustable Shift Times)

- **NSwag's generated `ApiException.Message` concatenates the raw HTTP
  response body** (`message + "\n\nStatus: ...\nResponse: \n" + rawJson`).
  care-wasm was displaying this directly to users, including
  ASP.NET Core's own `ValidationProblemDetails` JSON and a `traceId`. Fixed
  entirely on the care-wasm side (`ApiErrorHelper`, see that repo's
  `CLAUDE.md`) — this repo's contribution was only adding explicit,
  friendlier `ErrorMessage` text to the most user-facing DataAnnotations
  (`Password` `MinLength(8)`, `EmailAddress` checks in
  `Core/Application/Identity/Users/Requests.cs`) so the underlying message
  itself reads naturally once surfaced correctly.
- **`ShiftDto.GapAfterMinutes` and the `ConfirmGap`/409 flow described
  below are gone as of Phase 9** — see that section. `AdjustShiftTimesAsync`
  enforcing admin-or-assignee (not just Admin) is still true, though; that
  part carried forward unchanged.

## Known gotchas from Phase 9 (Blockless Shift Scheduling)

Live production testing of Phase 8's Day/Evening/Overnight calendar surfaced
a real bug (extending a shift's boundary *outward* into a neighboring Open
shift silently did nothing — `AdjustShiftTimesAsync` only ever checked for a
*positive* gap, never an overlap) and a bigger ask: drop the fixed shift-type
model entirely. A day is now just 24 fillable hourly blocks with no
predesignated Day/Evening/Overnight structure — a "shift" doesn't exist until
someone claims or is assigned a contiguous run of blocks.

- **`ShiftType` and `ShiftTemplate` are gone** — deleted, not deprecated.
  `Shift` no longer has a `ShiftType` property; there is no more nightly
  `ShiftGenerationJob` (deleted), no more `ShiftTemplate` seeding in
  `ApplicationDbInitializer`, and no more rolling-4-week generation window.
  A day starts with zero `Shift` rows; coverage only exists once someone
  creates it.
- **Uncovered time is the *absence* of a row, not `Status.Open`** —
  `ShiftStatus.Open` is never produced anymore. The enum ordinal is kept
  (not renumbered/removed) purely because EF stores it as a plain int with
  no `HasConversion`; renumbering would silently reinterpret existing
  `Assigned`(1)/`ReplacementRequested`(2) rows. `Shift.AssignedUserId`
  flipped from `string?` to `string` (non-nullable) — a row's existence now
  *implies* assignment.
- **`ClaimShiftAsync` is deleted** — claiming previously-uncovered time is
  now just `CreateShiftAsync` with yourself as the assignee. There's no
  more distinction between "claim an existing Open shift" and "create a
  new one"; they're the same operation.
- **The `confirmGap`/409 gap-confirmation flow from Phase 8 is gone
  entirely, not just its `GapAfterMinutes` display field** — under the new
  model, adjacent shifts always stay glued together when a boundary moves
  (grow into a neighbor → shrink or fully absorb it; shrink your own edge →
  an adjacent shift reclaims the vacated space, or it's just left
  uncovered if nothing's there). There is no remaining code path that can
  leave a deliberate gap between two shifts that were touching, so nothing
  needs a confirmation step for that case anymore.
- **Growing into a shift you don't fully swallow *shrinks* it; growing
  into one you *do* fully swallow deletes it** (and notifies its former
  assignee — `NotifyShiftRemovedAsync`, no confirmation gate). A resize
  that would require *splitting* an existing shift (sticking out on both
  sides of the new range) throws `ConflictException` instead of actually
  splitting — confirmed unreachable through the designed UI, since only
  the cells immediately adjacent to a shift's current boundary are ever
  clickable, but the service still guards against it defensively.
- **`ResolveOverlapsAsync` in `ShiftService.cs` is the crux of the whole
  feature** — shared by `CreateShiftAsync` (growing from nothing) and the
  growing side of `AdjustShiftTimesAsync`. It finds every existing shift
  overlapping the target range via real interval-overlap math
  (`FindOverlappingShiftsAsync`, not a `(Date, ShiftType)` lookup, since
  shifts no longer align to any fixed type or template) and either shrinks
  or deletes each one. The shrinking side of a resize (pulling your own
  boundary in) is handled separately — `FindShiftEndingExactlyAtAsync`/
  `FindShiftStartingExactlyAtAsync` look for whichever single shift touches
  the vacated instant, since shrinking can only ever affect one neighbor,
  never several.
- **Deleting a shift blocks on a pending replacement request** (own
  `ConflictException`, both in `DeleteShiftAsync` and inside
  `ResolveOverlapsAsync`'s full-absorb branch) — can't silently vanish a
  shift someone's actively trying to get covered. Shift notes have no such
  guard and are just deleted along with the shift (`ReplacementRequest`/
  `ShiftNote` both have plain `Guid ShiftId` columns with no FK/cascade
  configured — confirmed via `CareConfigurations.cs` — so EF won't
  cascade-delete them on its own; `DeleteShiftAsync` and
  `ResolveOverlapsAsync` both do it explicitly).
- **A single shift can't exceed 24 hours** (`MaxShiftDuration` guard in
  `ShiftService.cs`) — the `Date`/`StartTime`/`EndTime` representation
  (kept as-is rather than switching to `StartsAt`/`EndsAt` `DateTime`
  columns) relies on "if `EndTime <= StartTime`, it wrapped to the next
  day," which can't cleanly represent anything longer than that anyway.
  No legitimate use case in this app needs one continuous unbroken shift
  longer than a day.
- **The `RemoveShiftTypeAndTemplates` migration deletes data, not just
  schema** — `DELETE FROM Shifts WHERE AssignedUserId IS NULL` runs before
  the column/table drops, removing every `Open` row (the only ones that
  could have a null `AssignedUserId`) since they have no place under the
  new model. This is real, irreversible data loss by design and runs
  unattended the moment a new deployment boots
  (`ApplicationDbInitializer` auto-applies pending migrations) — back up
  the `Shifts` table before deploying this to a database with real data.
  `Down()` only restores schema shape; it can't resurrect deleted rows.

## Known gotchas from Phase 10 (Patient Settings, Notification Links, Times)

Four independent fixes from live testing after Phase 9 shipped.

- **New `AppSettings` single-row table** (`Core/Domain/Common/AppSettings.cs`,
  `Infrastructure/Common/AppSettingsService.cs`) holds just one field,
  `PatientName`, deliberately not a generic key-value settings store — add
  more dedicated fields here if more settings show up, don't build a
  generic framework speculatively. `ApplicationDbInitializer` seeds exactly
  one empty row if the table is empty; `AppSettingsService` always reads/
  writes the first (only) row via `FirstOrDefaultAsync`, no "well-known ID"
  needed. `GET /api/settings` has no role restriction (the Home page needs
  it as any authenticated user); `PUT` is Admin-only.
- **Invite email/SMS pull the patient name directly via `_db.AppSettings`**
  in `UserService.CreateAndSendInviteAsync`, not through
  `IAppSettingsService` — `UserService` already has `ApplicationDbContext`
  injected, so a second service dependency would be pure ceremony for a
  single-row read. Falls back to today's generic wording when unset.
- **`NotificationService` needed a frontend base URL for the first time**
  (the replacement-requested link) — it runs via Hangfire background jobs,
  so there's no HTTP request/`Origin` header available like
  `UsersController`'s invite/reset flows use. Reused the already-required
  `CorsSettings:Blazor` config value (semicolon-separated allowed origins)
  instead of adding a new config key — takes the first entry. Injected
  `IConfiguration` into `NotificationService`'s constructor for this.
- **`ReplacementRequestDto` gained `StartTime`/`EndTime`** — these were
  dropped entirely (not just `ShiftType`) when Phase 9 removed the fixed
  shift model, leaving the Replacement Requests page showing only a date
  with no indication of how long the shift actually is.  `GetReplacementQueueAsync`
  already loads each request's associated `Shift`, so this was just adding
  two fields to the existing DTO construction, no new query.
- **Document preview (PDF/image inline instead of forced download) needed
  zero backend changes** — `DocumentDto.ContentType` and the raw bytes
  already existed; the frontend fully controls download-vs-inline-view via
  its own Blob/JS-interop handling, regardless of what `Content-Disposition`
  the `File()` result sets. See care-wasm's `CLAUDE.md` for the actual fix.

## Known gotchas from Phase 11 (Self-Service My Account)

Live testing surfaced a gap: nobody — Admin or Member — had any way to
change their own email, phone number, or password while logged in. Admins
could edit *other* users' phone numbers from the Users page, and the
logged-out forgot-password flow covered password recovery, but there was
no self-service equivalent for an already-authenticated user.

- **Email changes require confirming the new address first, not an
  immediate change** — a deliberate, more-work choice (locked in via an
  explicit design decision, not a default) to match how every other
  sensitive account action in this app already works (invites, password
  resets). `RequestEmailChangeAsync` rejects if `FindByEmailAsync(newEmail)`
  already resolves to a user, then `GenerateChangeEmailTokenAsync` + emails
  a link to **the new address** (not the old one) — the account's current
  email never changes until that link is clicked.
  `ConfirmEmailChangeAsync` calls `ChangeEmailAsync` then
  `SetUserNameAsync` to keep `UserName`/`Email` in sync, matching how
  `CreateInviteAsync` always sets them equal at creation.
- **All four new `me/*`-prefixed actions on `UsersController` use only the
  controller's default `[Authorize]`** (no role restriction) and are
  scoped via `RequestingUserId`, not a route `{id}` — there's no way for
  one user to touch another's account through these endpoints, unlike the
  existing Admin-only `{id}`-scoped routes (`GetUsersAsync`,
  `UpdatePhoneNumberAsync`, etc.) which this phase left untouched.
  `ChangePasswordAsync` just forwards to `UserManager.ChangePasswordAsync`
  — a wrong current password surfaces through Identity's own
  `IdentityResult` errors, no separate check needed.
- **`POST /api/users/confirm-email-change` is `[AllowAnonymous]`** — same
  reasoning as `reset-password`: the browser clicking the emailed link may
  not have an active session (or may have a *different* user's session
  cached), so it can't require a Bearer token. Added to care-wasm's
  `JwtAuthenticationHeaderHandler.AnonymousPaths` allowlist — see that
  repo's Phase 1 gotcha and `CLAUDE.md` for why forgetting this silently
  breaks the logged-out case.
- **No backend changes needed for the phone-number field** — it reuses
  the existing `UpdatePhoneNumberRequest`/`UpdatePhoneNumberAsync`, just
  exposed through a new self-scoped `PUT /api/users/me/phone-number`
  route alongside the pre-existing Admin-only `{id}` one.

## Known gotchas from Phase 12 (Invite by phone number only)

An Admin can now create an invite with just a phone number (no email) —
useful now that SMS is set up via Twilio. Deliberately narrow scope: the
invitee still sets a real email for themselves on the Register page before
their account is created, and login stays email+password for everyone —
no phone-based login, no changes to `TokenService`/password reset/the
`RequireConfirmedAccount` gate.

- **`Invite` gained `UserId` as its primary lookup key, replacing `Email`**
  (`Email` is now nullable) — `RegisterAsync` looks up the pending user via
  `FindByIdAsync(invite.UserId)`, not `FindByEmailAsync(invite.Email)`,
  since the latter can't work once `Email` is null. A migration
  (`AddInviteUserId`) backfills `UserId` for existing rows from a join
  against `AspNetUsers.Email` — purely additive/backfill, no data loss.
- **`options.User.RequireUniqueEmail` is now `false`** (`Infrastructure/Identity/Startup.cs`).
  Identity's built-in `UserValidator` otherwise hard-rejects *any* user
  with a null `Email`, regardless of phone number — this would have
  blocked every phone-only account outright. Duplicate-email protection
  is still enforced manually in `CreateInviteAsync`'s existing
  `FindByEmailAsync(email) is not null` check (unchanged) — a matching
  manual duplicate-phone check (`Users.AnyAsync(u => u.PhoneNumber == phoneNumber)`)
  was added for the phone-only path, so no protection was actually lost.
- **A phone-only invited `ApplicationUser` gets `UserName` set to a
  sanitized version of the phone number** (strip everything except a
  leading `+` and digits), not the raw phone string — Identity's default
  `AllowedUserNameCharacters` rejects spaces/parens/dashes. `Name` is set
  to the raw phone number as a placeholder, same "placeholder until they
  register their real name" convention the email path already used.
- **Setting a user's email post-creation must go through
  `UserManager.SetEmailAsync`/`SetUserNameAsync`, never direct property
  assignment** (`user.Email = x`) — direct assignment doesn't update the
  `NormalizedEmail`/`NormalizedUserName` columns that `FindByEmailAsync`
  (used by login) actually queries against, so a user could register with
  their new email and then be unable to log in with it. Caught during
  implementation, before it shipped — `RegisterAsync`'s phone-only branch
  uses `SetEmailAsync`/`SetUserNameAsync` for exactly this reason. This
  gotcha only applies to *already-persisted* users; setting properties
  directly on a brand-new `ApplicationUser` before `CreateAsync` (as
  `CreateInviteAsync` does for both the email and phone-only paths) is
  fine, since `CreateAsync` normalizes both fields itself right before
  the initial insert.
- **New `GET /api/users/invite-info?token=` (`[AllowAnonymous]`)** lets the
  Register page ask, before the user fills anything in, whether the
  invite already has an email (`RequiresEmail: false`) or needs one
  collected at registration time (`RequiresEmail: true`). Like every other
  `[AllowAnonymous]` route, this had to be added to care-wasm's
  `JwtAuthenticationHeaderHandler.AnonymousPaths` allowlist — missed on
  the first pass, caught via live testing (a logged-out visitor following
  a real invite link got force-redirected to `/login` instead of seeing
  the Register form, since the page's own `GetInviteInfoAsync` call
  never got a chance to run). See that repo's Phase 1 gotcha for why this
  allowlist exists at all.
- **`UserDto.Email` is now `string?`** — a phone-only invited user has no
  email until they register. `GetUsersAsync`/`GetUserAsync` no longer
  force-unwrap (`user.Email!`); care-wasm's `Users.razor` shows `"—"` for
  a null email, matching the existing null-`PhoneNumber` display
  convention on the same table.

## Known gotchas from Phase 15 (Per-trigger notification toggles + shift reminder)

(Phases 13/14 were care-wasm-only — a first-login onboarding wizard and a
version-number display — see that repo's `CLAUDE.md` for both; nothing
needed changing here for either.)

- **`AppSettings` gained 14 flat `bool` columns** (`NotifyXEmail`/`NotifyXSms`
  per trigger), not a generic notification-preferences table — same
  "add dedicated fields, don't build a generic framework speculatively"
  precedent as Phase 10's original `PatientName` field. All default
  `true` (both the C# property initializer and `.HasDefaultValue(true)`
  in `AppSettingsConfig.cs`) so existing deployments keep today's
  always-on behavior after the migration lands.
- **`AppSettingsDto` is used as both the `GET` response shape and the
  `UpdateSettingsAsync` input shape** — deliberate, since Settings'
  read/write shapes mirror each other exactly (unlike Users' DTOs),
  avoiding a 15-parameter service method. `SettingsController` still
  translates its own Host-layer `UpdateSettingsRequest` into an
  `AppSettingsDto` before calling the service, keeping the Host request
  type out of the Application layer.
- **`NotificationService` now injects `IAppSettingsService`** (a real,
  justified dependency here, unlike `UserService`'s existing single-field
  precedent for reading just `PatientName` via raw `_db.AppSettings`) —
  every `Notify*Async` method fetches the full settings DTO once (outside
  any per-recipient loop, for the two broadcast triggers) and passes the
  matching `NotifyXEmail`/`NotifyXSms` booleans into `EnqueueNotification`,
  which now gates each channel independently instead of always sending
  both.
- **New `Shift.ReminderSentAt` (nullable `DateTime`)** is the guard against
  ever double-sending the new shift-reminder trigger for the same shift.
- **`Infrastructure/Care/ShiftReminderJob.cs`** runs on a Hangfire
  recurring schedule (`*/10 * * * *`, registered via `UseShiftReminderJob()`
  in `Infrastructure/BackgroundJobs/Startup.cs`, called from
  `Infrastructure/Startup.cs`'s `UseInfrastructure` chain) — no immediate
  one-off enqueue at startup, unlike the old (deleted) shift-generation
  job, since there's no "empty calendar on fresh deploy" problem here to
  solve. **Use a raw cron string (`"*/10 * * * *"`), not
  `Cron.MinuteInterval(10)`** — the latter is obsolete in the installed
  Hangfire 1.8.14 and produces a build warning, which this repo doesn't
  tolerate (0-warnings bar).
- **Query pattern mirrors `ShiftService.FindOverlappingShiftsAsync`
  exactly**: narrow by `Date` range in SQL first
  (`ReminderSentAt == null && Date >= today && Date <= tomorrow`),
  materialize, then do the precise absolute-time comparison in memory
  (same `Date.ToDateTime(TimeOnly.FromTimeSpan(StartTime))` construction
  as `ShiftService.GetAbsoluteStart`) — a shift whose absolute start falls
  in `(now, now.AddHours(1)]` gets notified and stamped.
- **Uses `DateTime.Now` (server local time), not `.UtcNow`**, to match how
  `Shift.Date`/`StartTime` are already treated everywhere else in this
  codebase as naive local wall-clock values with no timezone conversion
  anywhere (`GetAbsoluteStart`, `NotificationTemplates.FormatTime`, etc.)
  — using UTC here would be a new, inconsistent assumption. If this app
  is ever deployed where the container's system timezone doesn't match
  the family's real local timezone, every existing shift-time display has
  the same latent issue, not just this new job.
- **Verified via a manual Hangfire dashboard "trigger now" call, not the
  natural 10-minute tick** — the dashboard's trigger endpoint needs
  specific `Content-Type: application/x-www-form-urlencoded` handling and
  still 422s on antiforgery validation from a bare `curl`, but the
  underlying job trigger fires anyway before that later validation step
  fails, which is enough to confirm behavior without waiting out the full
  interval.

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
feature and fix from the original spec is done. Phase 8 (post-go-live,
after real production use) added friendlier validation error text and an
admin-or-assignee `AdjustShiftTimesAsync` endpoint for nudging a shift's
start/end time. Phase 9 replaced the fixed Day/Evening/Overnight model
entirely — `ShiftType`/`ShiftTemplate`/`ShiftGenerationJob` are gone, a day
is just fillable hourly blocks with no predesignated structure, a "shift"
is created the moment blocks are assigned/claimed, and adjusting a shift's
boundary always cascades into whatever's adjacent (shrink/absorb-delete a
neighbor it grows into, let a neighbor reclaim space it shrinks away from)
with a notification rather than the old gap-confirmation flow. See Phase
9's gotchas section above for the full detail — this superseded most of
Phase 3/4's original framing and all of Phase 8's gap-indicator work.
Phase 10 (four independent fixes from live testing) added the single-row
`AppSettings`/`PatientName` concept and its `SettingsController`, a
replacement-requests link on the replacement-requested notification, and
`StartTime`/`EndTime` on `ReplacementRequestDto`. Phase 11 added
self-service account management (`GET /api/users/me`,
`POST /api/users/me/change-password`,
`POST /api/users/me/request-email-change`,
`POST /api/users/confirm-email-change`) so any logged-in user can manage
their own email/phone/password without Admin involvement. Phase 12 let
Admins invite by phone number alone (no email required at invite time) —
the invitee sets their own email on the Register page before their
account activates; login stays email+password for everyone. Phase 15
added per-trigger email/SMS notification toggles and a new "shift starts
in about an hour" reminder alert, both admin-configurable from the
Settings page.
