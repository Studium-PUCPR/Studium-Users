# Studium-Users — Project State & Agent Context (`AGENTS.md`)

> **MANDATORY AGENT DIRECTIVE:**
> After **every user prompt**, always evaluate whether we reached a useful architectural conclusion, resolved a design question, or made code/configuration changes. If so, **update both the relevant sections and Section 3 (`Recent Changes History`) in this `./Studium-Users/AGENTS.md` file before finishing your turn** so any future chat session immediately has full context of the project state.

---

## 1. Architectural Decisions & Conclusions Log

### 1.1 Target Framework (`.NET 10.0`)
- **Conclusion:** Although the initial specification template referenced `.NET 8`, the development environment uses the **.NET 10 SDK (`10.0.114`)** and EF Core / Npgsql `10.x` packages.
- **Decision:** Keep `net10.0` across [`Studium-Users.csproj`](./Studium-Users.csproj) and the multi-stage [`Dockerfile`](./Dockerfile) (`mcr.microsoft.com/dotnet/sdk:10.0` & `aspnet:10.0`).

### 1.2 Ingress, API Gateway & Inter-Service Communication (RabbitMQ vs SQS)
- **Conclusion:**
  - Only a single entry point (Ingress / Frontend gateway) needs to be exposed to the internet; individual backend microservices do not need separate public exposure.
  - The frontend communicates via the single gateway (`Frontend -> Gateway/Ingress -> Microservice REST Endpoints`), and **microservice-to-microservice communication is handled asynchronously via RabbitMQ using Wolverine (`WolverineFx.RabbitMQ`)**.
  - A legacy AWS SQS implementation is safely preserved on git branch `origin/sqs`.
- **Decision:**
  - Expose a **single unified set of endpoints** under `/api/users` in [`UsersController.cs`](./Controllers/UsersController.cs).
  - Use **Wolverine (`Wolverine.RabbitMQ`)** with static pre-generated handlers (`TypeLoadMode.Static` in production via `codegen write` in `Dockerfile`), `.AutoProvision()` enabled, connecting to `amqp://studium:StudiumRabbitMQ2026!@studium-rabbitmq-service.studium.svc.cluster.local:5672`.
  - Listen to queue `studium-users-events-queue`.
  - Publish `UserStatusChangedEvent` to queue `studium-booking-events-queue`.
  - Publish `AuditIntegrationEvent` (`[MessageIdentity("studium.audit.integration-event")]`) to queue `studium-audit-events-queue`.
  - Configured transactional outbox via `opts.Policies.AutoApplyTransactions()`.

### 1.3 Centralized Audit Decoupling & Inter-Service HTTP Communication
- **Context:** Auditing was separated from `Studium-Users` into the dedicated `Studium-Audit` microservice to guarantee single responsibility and satisfy academic requirements for five distinct autonomous services with isolated databases.
- **Decision:**
  - Removed `AuditLogs` entity and table from `Studium-Users` via migration `20261109182659_Remove_AuditLogs_Table`.
  - Auditable actions (profile update, user blocked/unblocked) publish asynchronous `AuditIntegrationEvent` messages to `studium-audit-events-queue` via RabbitMQ so auditing never blocks user workflows.
  - User audit log queries (`GET /api/users/{id}/audit-logs`) synchronously delegate to `Studium-Audit` (`GET /api/audit/resources/{id}`) using a typed `IHttpClientFactory` named client (`AuditClient`) pointing to `http://studium-audit-service.studium.svc.cluster.local`.
  - Forward incoming caller `Authorization` header to `AuditClient` to satisfy RBAC in `Studium-Audit`.
  - Wrapped HTTP queries in a 3-attempt exponential backoff retry policy (200ms, 500ms, 1000ms).

### 1.4 Ubiquitous Language & Decoupled Activity Subscriptions
- **Context:** Naming internal handlers after sender services (`BookingEventsHandler`) introduced conceptual coupling that obscured domain responsibility.
- **Decision:**
  - Renamed `BookingEventsHandler` to `UserActivityHandler` in `Services/UserActivityHandlers/`.
  - Subscribes to `UserActivityIntegrationEvent` (`[MessageIdentity("studium.users.user-activity")]`) to update `TotalReservations30Days`, `CompletedReservations30Days`, `CancelledReservations30Days`, and enforce the 5 no-shows suspension threshold.

---

## 2. Directory Structure & Key Files
```
Studium-Users/
├── Domain/
│   ├── Common/                        # BaseEntity, ValueObject, DomainException
│   └── Entities/
│       └── Users/                     # User entity, UserRole enum, UserConfig (EF Core)
├── Data/
│   ├── UsersDbContext.cs              # EF Core PostgreSQL context applying entity configurations
│   └── Migrations/                    # Initial database migration and Remove_AuditLogs_Table migration
├── Services/
│   ├── DTOs/                          # UserDetailDto, UserActivityIntegrationEvent, UserStatusChangedEvent, AuditIntegrationEvent
│   └── UserActivityHandlers/          # Wolverine handler updating 30-day activity counters and auto-suspending on 5 no-shows
└── Api/
    ├── Controllers/                   # UsersController with search, get by id, update profile, toggle block, audit logs
    ├── Extensions/                    # WebApplicationBuilder setup (Auth, RabbitMQ Messaging, DB)
    ├── HostedServices/                # DatabaseMigrationHostedService (runs migrations on startup)
    ├── Middleware/                    # DomainExceptionHandler (RFC 7807 ProblemDetails mapping)
    └── Internal/Generated/            # Wolverine pre-generated handlers generated via codegen write
```

---

## 3. Recent Changes History

- **2026-11-09 (Resilient Audit Forwarding, Message Identity & Outbox Transactions):**
  - Updated `GetUserAuditLogs` in `UsersController` to forward the incoming `Authorization` header to `AuditClient` and apply a 3-attempt exponential backoff retry.
  - Added `[MessageIdentity("studium.audit.integration-event")]` to `AuditIntegrationEvent` in `Services/DTOs/EventDtos.cs`.
  - Enabled transactional outbox via `opts.Policies.AutoApplyTransactions()` in `MessagingExtensions.cs`.
  - Regenerated Wolverine static handlers and verified all 25 unit tests pass.
- **2026-11-09 (Ubiquitous Language Refactor — UserActivityHandler):**
  - Renamed `BookingEventsHandler` to `UserActivityHandler` in `Services/UserActivityHandlers/`.
  - Replaced `BookingIntegrationEvent` with `UserActivityIntegrationEvent` (`[MessageIdentity("studium.users.user-activity")]`) to decouple the message contract from service-specific naming.
  - Cleaned up obsolete SQS configuration block from `appsettings.json`.
  - Regenerated Wolverine static handlers (`UserActivityIntegrationEventHandler1310681706.cs`) and verified all 25 unit tests pass.
- **2026-11-09 (Extracted Audit into Dedicated Studium-Audit Microservice):**
  - Removed `AuditLog` entity, `AuditLogConfig`, and `AuditLogs` DbSet from `Studium-Users`.
  - Scaffolded EF Core migration `20261109182659_Remove_AuditLogs_Table` to drop table `AuditLogs` from `studium_users`.
  - Configured asynchronous publishing of `AuditIntegrationEvent` to `studium-audit-events-queue` via Wolverine RabbitMQ.
  - Registered `AuditClient` with `IHttpClientFactory` in `Api/Program.cs` to synchronously query `Studium-Audit` at `/api/audit/resources/{id}` for `GET /api/users/{id}/audit-logs`.
- **2026-11-09 (Migrated Messaging from AWS SQS to RabbitMQ):**
  - Replaced `WolverineFx.AmazonSqs` with `WolverineFx.RabbitMQ` (v6.63.0) centrally in `Directory.Packages.props`, `Api/Studium-Users.Api.csproj`, and `Services/Studium-Users.Services.csproj`.
  - SQS version preserved on branch `origin/sqs`.
