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
  - Only a single entry point (Ingress / Frontend gateway) needs to be exposed to the internet; individual backend microservices (`Studium-Users`, `Studium-Catalog`, `Studium-Booking`, `Studium-Notifications`) do not need separate public exposure.
  - The frontend communicates via the single gateway (`Frontend -> Gateway/Ingress -> Microservice REST Endpoints`), and **microservice-to-microservice communication is handled asynchronously via RabbitMQ using Wolverine (`WolverineFx.RabbitMQ`)**.
  - A legacy AWS SQS implementation is safely preserved on git branch `origin/sqs`.
- **Decision:**
  - Expose a **single unified set of endpoints** under `/api/users` in [`UsersController.cs`](./Controllers/UsersController.cs) rather than splitting into separate public vs. internal controllers.
  - Use **Wolverine (`Wolverine.RabbitMQ`)** with static pre-generated handlers (`TypeLoadMode.Static` in production via `codegen write` in `Dockerfile`), `.AutoProvision()` enabled, connecting to `amqp://studium:StudiumRabbitMQ2026!@studium-rabbitmq-service.studium.svc.cluster.local:5672`.
  - Listen to queue `studium-users-events-queue`.
  - Publish `UserStatusChangedEvent` to queues `studium-booking-events-queue` and `studium-notifications-queue`.
  - Keep lightweight event contract DTOs (`BookingIntegrationEvent.cs`) per microservice repository rather than introducing a shared cross-repo NuGet package.

### 1.3 Amazon RDS PostgreSQL & Schema Initialization
- **Context:** Amazon RDS only provisions an empty PostgreSQL instance/database (`studium_users`); it does not create tables or indexes automatically.
- **Decision:** Keep `db.Database.MigrateAsync()` on startup; the initial EF Core migration (`Migrations/20261108045337_Initial_Migration.cs`) is generated for the finalized `Users` and `AuditLogs` schema.

### 1.4 Infrastructure & CI/CD Organization
- **Conclusion:** All shared AWS CDK infrastructure (`NetworkConstruct`, `CognitoConstruct`, `DatabaseConstruct`, `MessagingConstruct`, and `EksConstruct`) is centralized in the dedicated **`Studium-Infra`** repository (`../Studium-Infra`), while each microservice repository (`Studium-Users`, `Studium-Catalog`, `Studium-Booking`, `Studium-Notifications`) contains its own `Dockerfile`, Kubernetes `k8s/` manifests, and `.github/workflows/` CI/CD pipeline.

### 1.5 Domain Entities, Fluent API Configurations, Enums, Mapping & Auditing
- **1-to-1 Folder & Namespace Alignment:**
  - Every file's C# namespace matches its exact folder structure (`Studium_Users.Domain.Entities.Users`, `Studium_Users.Domain.Entities.AuditLogs`, `Studium_Users.Services.UserEventsPublishers`, etc.). Entity and service subfolders are pluralized (`Users/`, `AuditLogs/`, `UserEventsPublishers/`) to prevent C# class-vs-namespace name collisions.
- **Fluent API Separation & Default Values:**
  - Domain entities are free of DataAnnotations (`[Key]`, `[Required]`, `[MaxLength]`) and free of redundant property initializer defaults (`= true`, `= 0`, etc.).
  - Each entity lives in its own folder under `Domain/Entities/<Entities>/` alongside a dedicated `IEntityTypeConfiguration<T>` (`UserConfig.cs`, `AuditLogConfig.cs`), where all column defaults (`.HasDefaultValue(...)`, `.HasDefaultValueSql("now()")`) are configured.
- **Object Mapping (`Mapster`) & API Docs (`Scalar.AspNetCore`):**
  - Uses **`Mapster`** (`user.Adapt<T>()`, `.ProjectToType<T>()`) to eliminate manual `FromEntity` boilerplate across DTOs, controllers, and event publishing.
  - Uses `.NET 10` native **`Microsoft.AspNetCore.OpenApi`** + **`Scalar.AspNetCore`** for modern API documentation (`/scalar/v1`), configured with Bearer JWT security scheme.

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
│       ├── Users/                     # User entity, UserRole enum, UserConfig (EF Core)
│       └── AuditLogs/                 # AuditLog entity, AuditAction enum, AuditLogConfig
├── Data/
│   ├── UsersDbContext.cs              # EF Core PostgreSQL context applying entity configurations
│   └── Migrations/                    # Initial database migration
├── Services/
│   ├── DTOs/                          # UserDetailDto, BookingIntegrationEvent, UserStatusChangedEvent
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

- **2026-11-09 (Migrated Messaging from AWS SQS to RabbitMQ):**
  - Replaced `WolverineFx.AmazonSqs` with `WolverineFx.RabbitMQ` (v6.63.0) centrally in `Directory.Packages.props`, `Api/Studium-Users.Api.csproj`, and `Services/Studium-Users.Services.csproj`.
  - Updated `Api/Extensions/MessagingExtensions.cs` to configure `opts.UseRabbitMq(...)` connecting to `studium-rabbitmq-service.studium.svc.cluster.local:5672` with `.AutoProvision()`.
  - Added RabbitMQ Deployment & ClusterIP Service manifests directly into `k8s/deployment.yaml`.
  - Re-generated static Wolverine handlers using `codegen write`. Verified 25 unit tests passed cleanly.
  - SQS version preserved on branch `origin/sqs`.
- **2026-11-08 (Fix AuditLog Concurrency Exception):**
  - Configured `AuditLogConfig` with `ValueGeneratedNever()` and overrode `SaveChangesAsync` in `UsersDbContext` to enforce `EntityState.Added` for immutable audit entries, resolving the `DbUpdateConcurrencyException` when toggling user blocks or updating profiles.
