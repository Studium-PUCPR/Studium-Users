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
  - Expose a single unified set of endpoints under `/api/users` in [`UsersController.cs`](./Controllers/UsersController.cs).
  - Use **Wolverine (`Wolverine.RabbitMQ`)** with static pre-generated handlers (`TypeLoadMode.Static` in production via `codegen write` in `Dockerfile`), `.AutoProvision()` enabled.
  - Listen to queue `studium-users-events-queue`.
  - Publish `UserStatusChangedEvent` to queue `studium-booking-events-queue`.
  - Publish `AuditIntegrationEvent` (`[MessageIdentity("studium.audit.integration-event")]`) to queue `studium-audit-events-queue`.
  - Configured transactional outbox via `opts.Policies.AutoApplyTransactions()`.
  - Poison Message & Dead-Letter Handling: Configured Wolverine transient exception retries (`NpgsqlException`, `TimeoutException`, `HttpRequestException`) with exponential cooldowns and unhandled errors routing to `.error` queue.

### 1.3 Centralized Audit Decoupling & Inter-Service HTTP Communication
- **Decision:**
  - Removed `AuditLogs` entity and table from `Studium-Users` via migration `20261109182659_Remove_AuditLogs_Table`.
  - Auditable actions publish asynchronous `AuditIntegrationEvent` messages to `studium-audit-events-queue` via RabbitMQ.
  - User audit log queries (`GET /api/users/{id}/audit-logs`) synchronously delegate to `Studium-Audit` (`GET /api/audit/resources/{id}`) forwarding incoming `Authorization` headers with 3-attempt exponential backoff retry.

### 1.4 Monthly Activity Window Decay (1st of the Month Window)
- **Context:** User activity counters previously grew unbounded over time without a reset mechanism.
- **Decision:** Added `LastActivityResetUtc` to track the start of the monthly window (first of the month to current day). Both event processing (`UserActivityHandler`) and profile retrieval (`GetUserById`) call `EnsureMonthlyActivityWindow(nowUtc)`, zeroing counters when a calendar month boundary is crossed before accumulating current month infractions and reservations.

### 1.5 Database Resilience & Health Check Segregation
- Configured Npgsql's transient execution strategy (`EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null)`).
- Segregated Kubernetes probes: shallow liveness at `/health` and deep readiness at `/health/ready` via `DatabaseHealthCheck` (`UsersDbContext.Database.CanConnectAsync()`).

---

## 2. Directory Structure & Key Files
```
Studium-Users/
├── Domain/
│   ├── Common/                        # BaseEntity, ValueObject, DomainException
│   └── Entities/
│       └── Users/                     # User entity (with EnsureMonthlyActivityWindow), UserRole enum, UserConfig
├── Data/
│   ├── UsersDbContext.cs              # EF Core PostgreSQL context applying entity configurations
│   └── Migrations/                    # Initial, Remove_AuditLogs_Table, Add_LastActivityResetUtc migrations
├── Services/
│   ├── DTOs/                          # UserDetailDto, UserActivityIntegrationEvent, UserStatusChangedEvent, AuditIntegrationEvent
│   └── UserActivityHandlers/          # Wolverine handler updating monthly counters and auto-suspending on 5 no-shows
└── Api/
    ├── Controllers/                   # UsersController with search, get by id, update profile, toggle block, audit logs
    ├── Extensions/                    # WebApplicationBuilder setup (Auth, Messaging, DatabaseHealthCheck)
    ├── HostedServices/                # DatabaseMigrationHostedService (runs migrations on startup)
    ├── Middleware/                    # DomainExceptionHandler (RFC 7807 ProblemDetails mapping)
    └── Internal/Generated/            # Wolverine pre-generated handlers generated via codegen write
```

---

## 3. Recent Changes History

- **2026-11-09 (Monthly Window Decay, DB Resilience & Deep Readiness Probes):**
  - Added `LastActivityResetUtc` property and `EnsureMonthlyActivityWindow` method on `User` entity to decay counters at calendar month boundaries.
  - Scaffolded EF Core migration `Add_LastActivityResetUtc_To_User`.
  - Configured `EnableRetryOnFailure(3)` on PostgreSQL connection.
  - Added deep readiness probe `/health/ready` (`DatabaseHealthCheck`) and pointed Kubernetes `readinessProbe` to it.
  - Configured Wolverine exception policies for transient retries and dead-letter error queue routing.
  - Regenerated Wolverine static handlers; 26/26 unit tests pass.
- **2026-11-09 (Resilient Audit Forwarding, Message Identity & Outbox Transactions):**
  - Updated `GetUserAuditLogs` in `UsersController` to forward the incoming `Authorization` header to `AuditClient` and apply a 3-attempt exponential backoff retry.
  - Added `[MessageIdentity("studium.audit.integration-event")]` to `AuditIntegrationEvent` in `Services/DTOs/EventDtos.cs`.
  - Enabled transactional outbox via `opts.Policies.AutoApplyTransactions()` in `MessagingExtensions.cs`.
- **2026-11-09 (Ubiquitous Language Refactor — UserActivityHandler):**
  - Renamed `BookingEventsHandler` to `UserActivityHandler` in `Services/UserActivityHandlers/`.
- **2026-11-09 (Extracted Audit into Dedicated Studium-Audit Microservice):**
  - Removed `AuditLog` entity, `AuditLogConfig`, and `AuditLogs` DbSet from `Studium-Users`.
- **2026-11-09 (Migrated Messaging from AWS SQS to RabbitMQ):**
  - Replaced `WolverineFx.AmazonSqs` with `WolverineFx.RabbitMQ` (v6.63.0).
