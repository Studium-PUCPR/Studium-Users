# Studium-Users — Project State & Agent Context (`AGENTS.md`)

> **MANDATORY AGENT DIRECTIVE:**
> After **every user prompt**, always evaluate whether we reached a useful architectural conclusion, resolved a design question, or made code/configuration changes. If so, **update both the relevant sections and Section 3 (`Recent Changes History`) in this `./Studium-Users/AGENTS.md` file before finishing your turn** so any future chat session immediately has full context of the project state.

---

## 1. Architectural Decisions & Conclusions Log

### 1.1 Target Framework (`.NET 10.0`)
- **Conclusion:** Although the initial specification template referenced `.NET 8`, the development environment uses the **.NET 10 SDK (`10.0.114`)** and EF Core / Npgsql `10.x` packages.
- **Decision:** Keep `net10.0` across [`Studium-Users.csproj`](./Studium-Users.csproj) and the multi-stage [`Dockerfile`](./Dockerfile) (`mcr.microsoft.com/dotnet/sdk:10.0` & `aspnet:10.0`).

### 1.2 Ingress, API Gateway & Inter-Service Communication
- **Conclusion:**
  - Only a single entry point (Ingress / Frontend gateway) needs to be exposed to the internet; individual backend microservices (`Studium-Users`, `Studium-Catalog`, `Studium-Booking`, `Studium-Notifications`) do not need separate public exposure.
  - Using `Frontend -> API Gateway -> Lambda -> SQS` is unnecessary complexity for this project. Instead, the frontend communicates via the single gateway (`Frontend -> Gateway/Ingress -> Microservice REST Endpoints`), and **microservice-to-microservice communication is handled asynchronously via Amazon SQS using the AWS SDK (`AWSSDK.SQS`)**.
- **Decision:**
  - Expose a **single unified set of endpoints** under `/api/users` in [`UsersController.cs`](./Controllers/UsersController.cs) rather than splitting into separate public vs. internal controllers.
  - Use `AWSSDK.SQS` with native **`sqsClient.GetQueueUrlAsync(queueName)`** (configuring queue names `UsersQueueName` and `OutboundQueueNames` in `appsettings.json` rather than hardcoded queue URLs) for both consuming inbound events (`BookingEventsConsumer`) and publishing outbound user status/preference events (`UserEventsPublisher`).
  - Keep lightweight event contract DTOs (`EventDtos.cs`) per microservice repository rather than introducing a shared cross-repo NuGet package (since `Studium-Notifications` is Python FastAPI and independent repositories avoid private NuGet feed friction).
  - Dropped `AWSSDK.CognitoIdentityProvider` and Cognito global sign-out on block (`RevokeCognitoSessionsAsync`) because JWT validation is handled by `JwtBearer` and blocked users only lose reservation privileges (RN09), not the ability to log in and view their profile.

### 1.3 Amazon RDS PostgreSQL & Schema Initialization
- **Context:** Amazon RDS only provisions an empty PostgreSQL instance/database (`studium_users`); it does not create tables or indexes automatically.
- **Decision:** Keep `db.Database.MigrateAsync()` on startup; the initial EF Core migration (`Migrations/20261108045337_Initial_Migration.cs`) is generated for the finalized `Users` and `AuditLogs` schema.

### 1.4 Infrastructure & CI/CD Organization
- **Conclusion:** Rather than isolating all Kubernetes/AWS infrastructure and CI/CD configurations into a separate `Studium-Infra` repository, **each microservice repository will contain its own relevant infrastructure manifests and CI/CD workflow YAMLs** (to be implemented in a later phase).

### 1.5 Domain Entities, Fluent API Configurations, Enums, Mapping & Auditing
- **1-to-1 Folder & Namespace Alignment:**
  - Every file's C# namespace matches its exact folder structure (`Studium_Users.Domain.Entities.Users`, `Studium_Users.Domain.Entities.AuditLogs`, `Studium_Users.Services.UserEventsPublishers`, etc.). Entity and service subfolders are pluralized (`Users/`, `AuditLogs/`, `UserEventsPublishers/`) to prevent C# class-vs-namespace name collisions.
- **Fluent API Separation & Default Values:**
  - Domain entities are free of DataAnnotations (`[Key]`, `[Required]`, `[MaxLength]`) and free of redundant property initializer defaults (`= true`, `= 0`, etc.).
  - Each entity lives in its own folder under `Domain/Entities/<Entities>/` alongside a dedicated `IEntityTypeConfiguration<T>` (`UserConfig.cs`, `AuditLogConfig.cs`), where all column defaults (`.HasDefaultValue(...)`, `.HasDefaultValueSql("now()")`) are configured.
- **Object Mapping (`Mapster`) & API Docs (`Scalar.AspNetCore`):**
  - Uses **`Mapster`** (`user.Adapt<T>()`, `.ProjectToType<T>()`) to eliminate manual `FromEntity` boilerplate across DTOs, controllers, and event publishing.
  - Uses `.NET 10` native **`Microsoft.AspNetCore.OpenApi`** + **`Scalar.AspNetCore`** (`app.MapOpenApi(); app.MapScalarApiReference();` at `/scalar/v1`), replacing manual Swagger generation.
- **Enums (`int` in DB, `string` in HTTP JSON):**
  - `UserRole` (`Student`, `Professor`, `Administrator`), `AuditAction` (`UserLogin`, `ProfileUpdated`, `UserBlocked`, `UserUnblocked`), `BookingEventType`, and `UserEventType` are strongly typed C# enums stored as `int` in PostgreSQL.
- **Rich Domain Entity (`User`) & Self-Contained Auditing (`AuditLog`):**
  - [`AuditLog`](./Domain/Entities/AuditLogs/AuditLog.cs) has no lifecycle or state transitions after creation (RNF14), so it is a lean **positional `record`** (`Guid Id, string UserId, AuditAction Action, DateTime TimestampUtc`) without `Details` or `IpAddress` bloat.
  - [`User`](./Domain/Entities/Users/User.cs) is a **Rich Domain Entity (`class`)** with `{ get; init; }` on read-only institutional properties (`Id`, `FullName`, `Email`, `RegistrationNumber`, `Role`, `CreatedAtUtc`), `{ get; private set; }` on mutable state, no redundant `UpdatedAtUtc`, and a one-to-many `List<AuditLog> AuditLogs` navigation property (`DeleteBehavior.Restrict`).
  - `User` records its own `AuditLog` entries directly inside its domain methods (`new User`, `UpdateProfile`, `Block`, `Unblock`), eliminating `AuditService` and committing both the `User` state change and `AuditLog` insert in a single `db.SaveChangesAsync()` call.
- **Language & DTO Standards:**
  - All backend code, enum values, error codes (`INVALID_PHONE_FORMAT`, `REASON_REQUIRED`, `CANNOT_BLOCK_SELF`, `USER_NOT_FOUND`, `UNAUTHENTICATED_USER`), and exception messages are standardized in **English**.

### 1.6 Core Engineering, SOLID & Clean Code Directives
- **SOLID & Single Responsibility:** One primary type per file (interfaces and implementations always separated). Classes must have a single reason to change.
- **Thin `Program.cs` (Composition Root):** `Program.cs` should only contain declarative DI setup, extension method calls, and middleware registration—never inline multi-line business logic, DB queries, or complex loops.
- **Design Patterns Where Relevant:** Prefer idiomatic .NET and DDD patterns (`IExceptionHandler` for global exception mapping, `IEntityTypeConfiguration<T>` for persistence configuration, Rich Domain Entities for invariants and self-contained auditing, `Wolverine` convention handlers for messaging, `Mapster` for DTO projection).
- **Zero Redundancy:** Define defaults in one place (Fluent API configs), avoid manual mapping blocks, and keep all code, enums, error codes, and comments in English.

---

## 2. `Studium-Users` Microservice State

### 2.1 Project Structure (`./Studium-Users`)
- [`Studium-Users.csproj`](./Studium-Users.csproj) — `.NET 10.0` Web API (`AWSSDK.SQS`, `Mapster`, `Microsoft.AspNetCore.Authentication.JwtBearer`, `Microsoft.AspNetCore.OpenApi`, `Microsoft.EntityFrameworkCore.Design`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `Scalar.AspNetCore`).
- [`Program.cs`](./Program.cs) — Declarative composition root (~55 lines) wiring DI, `DomainExceptionHandler`, `AddStudiumMessaging()`, `AddStudiumAuthentication()`, OpenAPI + Scalar UI (`/scalar/v1`), `/health` probe, and startup migration/seeding.
- [`Middleware/DomainExceptionHandler.cs`](./Middleware/DomainExceptionHandler.cs) — `.NET 10` `IExceptionHandler` mapping `DomainException` to `400 Bad Request` JSON responses.
- [`Extensions/AuthenticationExtensions.cs`](./Extensions/AuthenticationExtensions.cs) & [`Extensions/MessagingExtensions.cs`](./Extensions/MessagingExtensions.cs) — Modular DI registration extensions for Cognito JWT Bearer auth (`RoleClaimType = "cognito:groups"`) and Wolverine SQS messaging.
- [`Domain/Entities/Users/User.cs`](./Domain/Entities/Users/User.cs) & [`UserConfig.cs`](./Domain/Entities/Users/UserConfig.cs) — Rich `User` entity + `UserRole` enum + Fluent API configuration.
- [`Domain/Entities/AuditLogs/AuditLog.cs`](./Domain/Entities/AuditLogs/AuditLog.cs) & [`AuditLogConfig.cs`](./Domain/Entities/AuditLogs/AuditLogConfig.cs) — Positional `AuditLog` `record` + `AuditAction` enum + Fluent API configuration.
- [`Domain/Exceptions/DomainException.cs`](./Domain/Exceptions/DomainException.cs) — Domain exception carrying `ErrorCode` and `Message`.
- [`DTOs/UserDtos.cs`](./DTOs/UserDtos.cs) & [`DTOs/EventDtos.cs`](./DTOs/EventDtos.cs) — Clean positional record DTOs mapped via `Mapster` and SQS event contracts (`BookingEventType` and `UserEventType` enums).
- [`Data/UsersDbContext.cs`](./Data/UsersDbContext.cs) & [`Migrations/20261108045337_Initial_Migration.cs`](./Migrations/20261108045337_Initial_Migration.cs) — EF Core DbContext (`ApplyConfigurationsFromAssembly`) and initial PostgreSQL schema migration.
- [`Controllers/UsersController.cs`](./Controllers/UsersController.cs) — Unified `/api/users` controller using `Mapster` (`Adapt`, `ProjectToType`) and PostgreSQL `EF.Functions.ILike` search.
- [`Services/UserEventsPublishers/IUserEventsPublisher.cs`](./Services/UserEventsPublishers/IUserEventsPublisher.cs) & [`UserEventsPublisher.cs`](./Services/UserEventsPublishers/UserEventsPublisher.cs) — Resolves queue URLs via `sqsClient.GetQueueUrlAsync` (`AWS:SQS:OutboundQueueNames`) and publishes `UserStatusEvent` payloads.
- [`Workers/BookingEventsConsumer.cs`](./Workers/BookingEventsConsumer.cs) — `BackgroundService` resolving `AWS:SQS:UsersQueueName` via `GetQueueUrlAsync` and polling for `ReservationCreated`, `ReservationCheckedIn`, `ReservationCancelled`, and `NoShowRecorded`.
- [`Dockerfile`](./Dockerfile) & [`.dockerignore`](./.dockerignore) — Multi-stage `.NET 10.0` container image exposing port `8080` with build context exclusions.
- [`Tests/Studium-Users.Tests.csproj`](./Tests/Studium-Users.Tests.csproj) — xUnit test suite covering `User` domain invariants/audit logs (`UserTests.cs`), Mapster DTO projections (`DtoMappingTests.cs`), and SQS event publishing (`UserEventsPublisherTests.cs`).

---

## 3. Recent Changes History
- **2026-11-07 (Initial Scaffold):**
  - Flattened `Studium-Users` directory structure into `./Studium-Users`.
  - Decided on single unified `/api/users` endpoint controller + async SQS inter-service messaging via `AWSSDK.SQS` (no Lambda/API Gateway->SQS hop).
  - Scaffolded full `Studium-Users` microservice.
- **2026-11-07 (Domain & Entity Refactoring):**
  - Moved DataAnnotations out of entities into dedicated Fluent API configurations and moved all default values exclusively into the Fluent API configs.
  - Refactored `AuditLog` into a positional `record` and `User` into a Rich Domain Entity (`private set` on mutable fields, `init` on read-only fields, domain methods throwing `DomainException` handled by global middleware in `Program.cs`).
  - Removed `UpdatedAtUtc`, `Status`, and `NotifyCancellationsAndWaitlist` bloat from `User` and DTOs.
  - Converted `UserRole` (`Student`, `Professor`, `Administrator`), `AuditAction`, `BookingEventType`, and `UserEventType` to strongly typed enums mapped as `int` in PostgreSQL.
  - Added one-to-many `User -> List<AuditLog>` relationship (`DeleteBehavior.Restrict`), standardized all messages/enums in English, added self-block prevention (`CANNOT_BLOCK_SELF`), and switched user search to `EF.Functions.ILike`.
- **2026-11-07 (Audit & Service Cleanup):**
  - Removed `Details` and `IpAddress` from `AuditLog`, deleted `AuditService.cs`, and moved audit recording directly into `User` domain methods (`RecordAudit`).
  - Split `IUserEventsPublisher.cs` and `UserEventsPublisher.cs`, removed Cognito global sign-out (`AWSSDK.CognitoIdentityProvider`).
  - Moved `AGENTS.md` into `./Studium-Users/AGENTS.md` so it is versioned in Git and fixed `.agents/rules/maintain-agents-md.md` to point to the exact file path.
- **2026-11-07 (Namespaces, Mapster, GetQueueUrlAsync & Scalar):**
  - Renamed subfolders (`Domain/Entities/Users`, `Domain/Entities/AuditLogs`, `Services/UserEventsPublishers`) so all C# namespaces match their exact folder paths 1-to-1 without class-vs-namespace collisions.
  - Added `Mapster` (`7.4.0`) and removed all manual `FromEntity` mapping methods from `UserDtos.cs`, `UsersController.cs`, and `UserEventsPublisher.cs`.
  - Switched `UserEventsPublisher` and `BookingEventsConsumer` to resolve SQS queue URLs natively via `sqsClient.GetQueueUrlAsync(queueName)` (`AWS:SQS:UsersQueueName` and `AWS:SQS:OutboundQueueNames`) and removed redundant SQS `MessageAttributes` and SNS-unwrapping helpers.
  - Replaced `OpenApiDocGenerator.cs` with `.NET 10` native `Microsoft.AspNetCore.OpenApi` + `Scalar.AspNetCore` (`app.MapOpenApi()` and `app.MapScalarApiReference()`).
- **2026-11-08 (Unit Test Suite, DTO Summaries, Inlined Event Serialization & README):**
  - Added full XML `<summary>` and `<param>` documentation to all records in `DTOs/UserDtos.cs`.
  - Inlined `JsonSerializer.Serialize(user.Adapt<UserStatusEvent>() with { EventType = eventType, OccurredAtUtc = DateTime.UtcNow })` in `UserEventsPublisher.cs`.
  - Created xUnit unit test project in `Tests/Studium-Users.Tests.csproj` (excluded from main web compilation via `DefaultItemExcludes`) with 26 passing tests across `Domain/UserTests.cs`, `DTOs/DtoMappingTests.cs`, and `Services/UserEventsPublisherTests.cs`.
  - Updated `README.md` with Docker PostgreSQL startup, `dotnet restore`/`dotnet run`, Scalar API URL, and `dotnet test` instructions.
- **2026-11-08 (BookingEventsConsumer & BookingEventsHandler Refactoring):**
  - Separated business event processing into `Services/BookingEventsHandlers/IBookingEventsHandler.cs` and `BookingEventsHandler.cs` (with direct `UsersDbContext` injection).
  - Refactored `Workers/BookingEventsConsumer.cs` to use `IServiceScopeFactory`, lazy cached `_queueUrl ??= (await sqsClient.GetQueueUrlAsync(_queueName, ct)).QueueUrl`, exponential backoff on SQS polling errors, and added `Tests/Workers/BookingEventsConsumerTests.cs` (29 total passing unit tests).
- **2026-11-08 (Switched to WolverineFx.AmazonSqs):**
  - Replaced manual `AWSSDK.SQS` polling and publishing (`Workers/BookingEventsConsumer.cs`) with `WolverineFx.AmazonSqs` (`6.62.0`).
  - Configured `builder.Host.UseWolverine(...)` in `Program.cs` (`UseAmazonSqsTransport`, `ListenToSqsQueue`, and `PublishMessage<UserStatusEvent>().ToSqsQueue`).
  - Simplified `UserEventsPublisher` to a single expression delegating to Wolverine `IMessageBus.PublishAsync(...)` and `BookingEventsHandler` to a pure Wolverine convention-based handler.
- **2026-11-08 (Auth & Handler Refinements):**
  - Updated `BookingEventsHandler` to throw `KeyNotFoundException` when a user is missing so Wolverine can retry/DLQ out-of-order messages instead of silently acknowledging them.
  - Simplified `DevHeaderAuthenticationHandler` with a `GetHeader` helper, renamed `custom:matricula` to `custom:registration_number`, and enabled `options.MapInboundClaims = false` on `AddJwtBearer` in `Program.cs` to avoid duplicate SOAP claim mappings.
- **2026-11-08 (SOLID & Clean Code Directives + User-Agnostic Paths):**
  - Standardized user-agnostic relative paths (`./Studium-Users/...`) and documented core SOLID, Clean Code, Design Pattern, and thin `Program.cs` directives.
- **2026-11-08 (Program.cs Composition Root Refactoring & API Gateway 403 Delegation):**
  - Removed the `403 Forbidden` PostgreSQL audit middleware and `AuditAction.UnauthorizedAccess` so access-denial logging is handled at the API Gateway / CloudWatch level instead of hitting the primary database on every rejected request.
  - Extracted `Middleware/DomainExceptionHandler.cs` (`IExceptionHandler`), `Extensions/AuthenticationExtensions.cs`, and `Extensions/MessagingExtensions.cs`, shrinking `Program.cs` from 215 lines to a 55-line declarative composition root.
- **2026-11-08 (Removed DevHeaderAuthenticationHandler & Simplified Cognito Auth):**
  - Deleted `Auth/DevHeaderAuthenticationHandler.cs` and `SmartAuth` policy routing in preparation for real AWS Cognito setup via CDK.
  - Simplified `Extensions/AuthenticationExtensions.cs` by setting `RoleClaimType = "cognito:groups"` directly in `TokenValidationParameters` and using `policy.RequireRole(nameof(UserRole.Administrator))`.
- **2026-11-08 (Simplified JwtBearerOptions in AuthenticationExtensions):**
  - Replaced redundant `ValidateIssuer`/`ValidIssuer`/`ValidateAudience`/`ValidAudience`/`ValidateLifetime` properties in `TokenValidationParameters` with native `options.Authority` and `options.Audience` on `JwtBearerOptions`.
- **2026-11-08 (Evaluated HTTP REST vs. gRPC):**
  - Concluded that keeping HTTP/JSON REST controllers (paired with Wolverine/SQS for async inter-service events) is optimal over gRPC because `Studium-Users` is primarily consumed by the browser frontend through AWS API Gateway (which natively speaks HTTP/JSON, not native gRPC), avoiding dual-protocol (`.proto` + REST) maintenance overhead.
- **2026-11-08 (UsersController Endpoint Consolidation):**
  - Consolidated `GET /api/users/me` and `PUT /api/users/me` into `GET /api/users/{id}` and `PUT /api/users/{id}` (since the frontend passes the authenticated user ID directly).
  - Removed the duplicate `[HttpGet("/api/internal/users/{id}/eligibility")]` route attribute and deleted the `GetOrProvisionCurrentUserAsync` / `ResolveRoleFromClaims` helpers from `UsersController.cs`.
- **2026-11-08 (DTO & Controller Streamlining + IDOR Protection):**
  - Renamed `AdminUserDetailDto` to `UserDetailDto` and `AdminUserListItemDto` to `UserListItemDto`, and deleted redundant `UserProfileDto`, `UserEligibilityDto`, and `GET /api/users/{id}/eligibility`.
  - Changed `GET /api/users/audit-logs` to `GET /api/users/{id}/audit-logs` so it returns audit logs for a specific user `{id}`.
  - Added `IsSelfOrAdmin(id)` protection on `GET /api/users/{id}` and `PUT /api/users/{id}` to prevent IDOR access to other users' profiles.
- **2026-11-08 (Unified Block/Unblock Toggle & ClaimsPrincipal Extensions):**
  - Added `User.ToggleBlock(reason, adminUserId)` to `User.cs` and merged `PATCH /api/users/{id}/block` and `PATCH /api/users/{id}/unblock` into a single `PATCH /api/users/{id}/block` endpoint (`ToggleBlockUser`).
  - Moved `GetUserId()` and `IsSelfOrAdmin(targetUserId)` into `Extensions/AuthenticationExtensions.cs` as `ClaimsPrincipal` extension methods, leaving `UsersController.cs` free of private helper methods.
- **2026-11-08 (Deleted DbInitializer Seeder & Added JWT Auto-Provisioning on GET /api/users/{id}):**
  - Deleted `Data/DbInitializer.cs` so hardcoded seed users never collide with real Amazon Cognito `sub` UUIDs or unique emails.
  - Added `ClaimsPrincipal.ToUserEntity()` in `Extensions/AuthenticationExtensions.cs` and auto-provisioning on `GET /api/users/{id}` when a user requests their own profile for the first time after Cognito registration/login.
- **2026-11-08 (Initial EF Core Migration):**
  - Generated `Migrations/20261108045337_Initial_Migration.cs` for the finalized `Users` and `AuditLogs` tables.
- **2026-11-08 (Added Startup Database Migration & .dockerignore):**
  - Added startup `await db.Database.MigrateAsync()` in `Program.cs` so containers automatically apply pending EF Core migrations on boot.
  - Added `.dockerignore` to exclude `bin/`, `obj/`, `.git/`, and `Tests/` from Docker builds.
