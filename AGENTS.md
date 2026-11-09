# Studium-Users — Project State & Agent Context (`AGENTS.md`)

> **MANDATORY AGENT DIRECTIVE:**
> After **every user prompt**, always evaluate whether we reached a useful architectural conclusion, resolved a design question, or made code/configuration changes. If so, **update both the relevant sections and Section 3 (`Recent Changes History`) in this `./Studium-Users/AGENTS.md` file before finishing your turn** so any future chat session immediately has full context of the project state.

---

## 1. Architectural Decisions & Conclusions Log

### 1.1 Target Framework (`.NET 10.0`)
- **Conclusion:** Uses **.NET 10 SDK (`10.0.114`)** and EF Core / Npgsql `10.x` packages across all Clean Architecture projects.
- **Decision:** Keep `net10.0` across [`Studium-Users.csproj`](./Studium-Users.csproj) and the multi-stage [`Dockerfile`](./Dockerfile).

### 1.2 Ingress, API Gateway & Inter-Service Communication (RabbitMQ vs SQS)
- **Decision:**
  - Expose a single unified set of endpoints under `/api/users` in [`UsersController.cs`](./Api/Controllers/UsersController.cs).
  - Use **Wolverine (`Wolverine.RabbitMQ`)** with static pre-generated handlers (`TypeLoadMode.Static` in production via `codegen write` in `Dockerfile`), `.AutoProvision()` enabled.
  - Listen to queue `studium-users-events-queue`.
  - Publish `UserStatusChangedEvent` to queue `studium-booking-events-queue`.
  - Publish `AuditIntegrationEvent` (`[MessageIdentity("studium.audit.integration-event")]`) to queue `studium-audit-events-queue`.
  - Configured transactional outbox via `opts.Policies.AutoApplyTransactions()`.
  - Poison Message & Dead-Letter Handling: Configured Wolverine transient exception retries (`NpgsqlException`, `TimeoutException`, `HttpRequestException`) with exponential cooldowns and unhandled errors routing to `.error` queue.

### 1.3 Centralized Audit Decoupling & Ingress Direct Routing
- **Decision:**
  - Removed `AuditLogs` entity and table from `Studium-Users` via migration `20261109182659_Remove_AuditLogs_Table`.
  - Auditable actions publish asynchronous `AuditIntegrationEvent` messages to `studium-audit-events-queue` via RabbitMQ.
  - Removed obsolete proxy endpoint `GET /api/users/{id}/audit-logs` and internal `AuditClient` HTTP client dependency.
  - Frontend queries `Studium-Audit` directly via Ingress path routing (`/api/audit/resources/{id}`), adhering to microservice boundaries and preventing backend coupling.

### 1.4 Monthly Activity Window Decay (1st of the Month Window)
- **Context:** User activity counters previously grew unbounded over time without a reset mechanism.
- **Decision:** Added `LastActivityResetUtc` to track the start of the monthly window (first of the month to current day). Both event processing (`UserActivityHandler`) and profile retrieval (`GetUserById`) call `EnsureMonthlyActivityWindow(nowUtc)`, zeroing counters when a calendar month boundary is crossed before accumulating current month infractions and reservations.

### 1.5 Database Resilience & Health Check Segregation
- Configured Npgsql's transient execution strategy (`EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null)`).
- Segregated Kubernetes probes: shallow liveness at `/health` and deep readiness at `/health/ready` via `DatabaseHealthCheck` (`UsersDbContext.Database.CanConnectAsync()`).

### 1.6 Inbox Pattern Deduplication & Throttled Lazy Pruning
- **Context:** Integration events from RabbitMQ can be redelivered or replayed. Duplicate activity events would skew infraction counters and falsely trigger suspensions.
- **Decision:**
  - Added `InboxMessage` entity (`Id: Guid`, `HandlerName: string`, `ReceivedAtUtc: DateTime`) mapped to table `InboxMessages`.
  - `UserActivityHandler` checks `await dbContext.InboxMessages.AnyAsync(m => m.Id == eventId)` on incoming messages with `EventId`. If duplicate, logs warning and returns immediately.
  - Inbox messages older than 7 days are pruned using an in-memory 6-hour throttled check (`DateTime.UtcNow - _lastPrunedUtc > TimeSpan.FromHours(6)`), invoking `ExecuteDeleteAsync` without incurring periodic cron job overhead.

### 1.7 Dynamic Suspension Policy & Auto-Unblock Expiration
- **Context:** Suspension threshold and duration belong strictly to the Users domain and should be configurable without code changes.
- **Decision:**
  - Added `UserOptions` (`MaxNoShowsBeforeSuspension = 3`, `SuspensionDays = 7`) bound to configuration section `UserOptions`.
  - Added `BlockedUntilUtc` to `User` entity and EF Core migration `20261109215459_Add_InboxMessages_And_BlockedUntilUtc`.
  - Implemented `CheckAutoUnblock(nowUtc)` on `User` entity, executed during profile retrieval (`GetUserById`). If suspension expired, user is automatically unblocked and changes persisted.

### 1.8 Dynamic CORS Configuration
- **Decision:** Created reusable `CorsExtensions.AddStudiumCors(IConfiguration)` reading comma-separated allowed origins from `Cors:AllowedOrigins` or environment variable `CORS__ALLOWEDORIGINS` (defaulting to allow all for local development).

---

## 2. Directory Structure & Key Files
```
Studium-Users/
├── Domain/
│   ├── Common/                        # BaseEntity, ValueObject, DomainException
│   └── Entities/
│       ├── Users/                     # User entity (with EnsureMonthlyActivityWindow, BlockedUntilUtc, CheckAutoUnblock), UserRole enum, UserConfig
│       └── Inbox/                     # InboxMessage entity & InboxMessageConfig
├── Data/
│   ├── UsersDbContext.cs              # EF Core PostgreSQL context applying entity configurations (Users, InboxMessages)
│   └── Migrations/                    # Initial, Remove_AuditLogs_Table, Add_LastActivityResetUtc, Add_InboxMessages_And_BlockedUntilUtc
├── Services/
│   ├── DTOs/                          # UserDetailDto, UserListItemDto, UserActivityIntegrationEvent, UserStatusChangedEvent, AuditIntegrationEvent
│   ├── Options/                       # UserOptions (MaxNoShowsBeforeSuspension, SuspensionDays)
│   └── UserActivityHandlers/          # Wolverine handler with Inbox deduplication, throttled lazy pruning, and dynamic options
└── Api/
    ├── Controllers/                   # UsersController with search, get by id, update profile, toggle block
    ├── Extensions/                    # WebApplicationBuilder setup (Auth, Messaging, CorsExtensions, DatabaseHealthCheck)
    ├── HostedServices/                # DatabaseMigrationHostedService (runs migrations on startup)
    ├── Middleware/                    # DomainExceptionHandler (RFC 7807 ProblemDetails mapping)
    └── Internal/Generated/            # Wolverine pre-generated handlers generated via codegen write
```

---

## 3. Recent Changes History

- **2026-11-09 (Inbox Deduplication, Temporary Suspensions, Audit Proxy Removal & Dynamic CORS):**
  - Added `InboxMessage` entity and `InboxMessages` DbSet to store processed event IDs.
  - Implemented event deduplication and 6-hour throttled lazy pruning in `UserActivityHandler`.
  - Extracted `UserOptions` (`MaxNoShowsBeforeSuspension: 3`, `SuspensionDays: 7`) and added `BlockedUntilUtc` to `User` entity.
  - Implemented `CheckAutoUnblock` in `User` entity and integrated into `GetUserById`.
  - Scaffolded EF Core migration `20261109215459_Add_InboxMessages_And_BlockedUntilUtc`.
  - Removed obsolete `GetUserAuditLogs` proxy endpoint and `AuditClient` HTTP service.
  - Created `CorsExtensions.cs` and hooked it into `Program.cs`.
  - Regenerated Wolverine static handlers; 29/29 unit tests pass.
- **2026-11-09 (Monthly Window Decay, DB Resilience & Deep Readiness Probes):**
  - Added `LastActivityResetUtc` property and `EnsureMonthlyActivityWindow` method on `User` entity to decay counters at calendar month boundaries.
  - Scaffolded EF Core migration `Add_LastActivityResetUtc_To_User`.
  - Configured `EnableRetryOnFailure(3)` on PostgreSQL connection.
  - Added deep readiness probe `/health/ready` (`DatabaseHealthCheck`) and pointed Kubernetes `readinessProbe` to it.
  - Configured Wolverine exception policies for transient retries and dead-letter error queue routing.
  - Regenerated Wolverine static handlers.
- **2026-11-09 (Resilient Audit Forwarding, Message Identity & Outbox Transactions):**
  - Added `[MessageIdentity("studium.audit.integration-event")]` to `AuditIntegrationEvent` in `Services/DTOs/EventDtos.cs`.
  - Enabled transactional outbox via `opts.Policies.AutoApplyTransactions()` in `MessagingExtensions.cs`.
- **2026-11-09 (Ubiquitous Language Refactor — UserActivityHandler):**
  - Renamed `BookingEventsHandler` to `UserActivityHandler` in `Services/UserActivityHandlers/`.
- **2026-11-09 (Extracted Audit into Dedicated Studium-Audit Microservice):**
  - Removed `AuditLog` entity, `AuditLogConfig`, and `AuditLogs` DbSet from `Studium-Users`.
- **2026-11-09 (Migrated Messaging from AWS SQS to RabbitMQ):**
  - Replaced `WolverineFx.AmazonSqs` with `WolverineFx.RabbitMQ` (v6.63.0).
