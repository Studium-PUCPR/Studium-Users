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
  - Only a single entry point (Ingress / Frontend gateway) needs to be exposed to the internet; individual backend microservices (`Studium-Users`, `Studium-Catalog`, `Studium-Booking`, `Studium-Notifications`, `Studium-Audit`) do not need separate public exposure.
  - The frontend communicates via the single gateway (`Frontend -> Gateway/Ingress -> Microservice REST Endpoints`), and **microservice-to-microservice communication is handled asynchronously via RabbitMQ using Wolverine (`WolverineFx.RabbitMQ`)**.
  - A legacy AWS SQS implementation is safely preserved on git branch `origin/sqs`.
- **Decision:**
  - Expose a **single unified set of endpoints** under `/api/users` in [`UsersController.cs`](./Controllers/UsersController.cs).
  - Use **Wolverine (`Wolverine.RabbitMQ`)** with static pre-generated handlers (`TypeLoadMode.Static` in production via `codegen write` in `Dockerfile`), `.AutoProvision()` enabled, connecting to `amqp://studium:StudiumRabbitMQ2026!@studium-rabbitmq-service.studium.svc.cluster.local:5672`.
  - Listen to queue `studium-users-events-queue`.
  - Publish `UserStatusChangedEvent` to queue `studium-booking-events-queue`.
  - Publish `AuditIntegrationEvent` to queue `studium-audit-events-queue`.
  - Keep lightweight event contract DTOs per microservice repository rather than introducing a shared cross-repo NuGet package.

### 1.3 Centralized Audit Decoupling & Inter-Service HTTP Communication
- **Context:** Auditing was separated from `Studium-Users` into the dedicated `Studium-Audit` microservice to guarantee single responsibility and satisfy academic requirements for five distinct autonomous services with isolated databases.
- **Decision:**
  - Removed `AuditLogs` entity and table from `Studium-Users` via migration `20261109182659_Remove_AuditLogs_Table`.
  - Auditable actions (profile update, user blocked/unblocked) publish asynchronous `AuditIntegrationEvent` messages to `studium-audit-events-queue` via RabbitMQ so auditing never blocks user workflows.
  - User audit log queries (`GET /api/users/{id}/audit-logs`) synchronously delegate to `Studium-Audit` (`GET /api/audit/resources/{id}`) using a typed `IHttpClientFactory` named client (`AuditClient`) pointing to `http://studium-audit-service.studium.svc.cluster.local`.

---

## 2. Project Structure & File Map

```
Studium-Users/
├── Directory.Build.props              # Enforces net10.0, Nullable, ImplicitUsings, TreatWarningsAsErrors
├── Directory.Packages.props           # Central Package Management (CPM) pinning dependencies
├── Dockerfile                         # Multi-stage container build with static Wolverine pre-codegen
├── k8s/
│   └── deployment.yaml                # Namespace, ConfigMap, Deployment, Service, HPA, and RabbitMQ
├── .github/workflows/
│   └── ci-cd.yml                      # CI/CD pipeline building, testing, pushing to ECR and deploying to EKS
├── Domain/
│   ├── Common/                        # BaseEntity, ValueObject, DomainException
│   └── Entities/
│       └── Users/                     # User entity, UserRole enum, UserConfig (EF Core)
├── Data/
│   ├── UsersDbContext.cs              # EF Core PostgreSQL context applying entity configurations
│   └── Migrations/                    # Initial database migration and Remove_AuditLogs_Table migration
├── Services/
│   ├── DTOs/                          # UserDetailDto, BookingIntegrationEvent, UserStatusChangedEvent, AuditIntegrationEvent
│   └── BookingEventsHandlers/         # Wolverine handler updating counters and auto-blocking on 5 no-shows
└── Api/
    ├── Controllers/                   # UsersController with search, get by id, update profile, toggle block, audit logs
    ├── Extensions/                    # WebApplicationBuilder setup (Auth, RabbitMQ Messaging, DB)
    ├── HostedServices/                # DatabaseMigrationHostedService (runs migrations on startup)
    ├── Middleware/                    # DomainExceptionHandler (RFC 7807 ProblemDetails mapping)
    └── Internal/Generated/            # Wolverine pre-generated handlers generated via codegen write
```

---

## 3. Recent Changes History

- **2026-11-09 (Extracted Audit into Dedicated Studium-Audit Microservice):**
  - Removed `AuditLog` entity, `AuditLogConfig`, and `AuditLogs` DbSet from `Studium-Users`.
  - Scaffolded EF Core migration `20261109182659_Remove_AuditLogs_Table` to drop table `AuditLogs` from `studium_users`.
  - Configured asynchronous publishing of `AuditIntegrationEvent` to `studium-audit-events-queue` via Wolverine RabbitMQ.
  - Registered `AuditClient` with `IHttpClientFactory` in `Api/Program.cs` to synchronously query `Studium-Audit` at `/api/audit/resources/{id}` for `GET /api/users/{id}/audit-logs`.
  - Updated unit tests, re-generated static Wolverine handlers, and verified all 25 unit tests pass.
- **2026-11-09 (Migrated Messaging from AWS SQS to RabbitMQ):**
  - Replaced `WolverineFx.AmazonSqs` with `WolverineFx.RabbitMQ` (v6.63.0) centrally in `Directory.Packages.props`, `Api/Studium-Users.Api.csproj`, and `Services/Studium-Users.Services.csproj`.
  - Updated `Api/Extensions/MessagingExtensions.cs` to configure `opts.UseRabbitMq(...)` connecting to `studium-rabbitmq-service.studium.svc.cluster.local:5672` with `.AutoProvision()`.
  - Added RabbitMQ Deployment & ClusterIP Service manifests directly into `k8s/deployment.yaml`.
  - Re-generated static Wolverine handlers using `codegen write`. Verified 25 unit tests passed cleanly.
  - SQS version preserved on branch `origin/sqs`.
- **2026-11-08 (Fix AuditLog Concurrency Exception):**
  - Configured `AuditLogConfig` with `ValueGeneratedNever()` and overrode `SaveChangesAsync` in `UsersDbContext` to enforce `EntityState.Added` for immutable audit entries.
