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
- **Conclusion:** All shared AWS CDK infrastructure (`NetworkConstruct`, `CognitoConstruct`, `DatabaseConstruct`, `MessagingConstruct`, and `EksConstruct`) is centralized in the dedicated **`Studium-Infra`** repository (`../Studium-Infra`), while each microservice repository (`Studium-Users`, `Studium-Catalog`, `Studium-Booking`, `Studium-Notifications`) contains its own `Dockerfile`, Kubernetes `k8s/` manifests, and `.github/workflows/` CI/CD pipeline.

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

### 2.1 Multi-Project Clean Architecture Structure (`./Studium-Users`)
- [`Studium-Users.sln`](./Studium-Users.sln), [`Directory.Packages.props`](./Directory.Packages.props) & [`Directory.Build.props`](./Directory.Build.props) — Root solution file, Central Package Management (CPM), and shared MSBuild settings (`net10.0`, `Nullable`, `ImplicitUsings`).
- **`Domain/` ([`Studium-Users.Domain.csproj`](./Domain/Studium-Users.Domain.csproj)):**
  - [`Domain/Entities/Users/User.cs`](./Domain/Entities/Users/User.cs) & [`UserConfig.cs`](./Domain/Entities/Users/UserConfig.cs) — Rich `User` entity (`ToggleBlock`, `UpdateProfile`, counters) + `UserRole` enum + Fluent API configuration.
  - [`Domain/Entities/AuditLogs/AuditLog.cs`](./Domain/Entities/AuditLogs/AuditLog.cs) & [`AuditLogConfig.cs`](./Domain/Entities/AuditLogs/AuditLogConfig.cs) — Positional `AuditLog` `record` + `AuditAction` enum + Fluent API configuration.
  - [`Domain/Exceptions/DomainException.cs`](./Domain/Exceptions/DomainException.cs) — Domain exception carrying `ErrorCode` and `Message`.
- **`Data/` ([`Studium-Users.Data.csproj`](./Data/Studium-Users.Data.csproj)):**
  - [`Data/UsersDbContext.cs`](./Data/UsersDbContext.cs) & [`Data/Migrations/20261108045337_Initial_Migration.cs`](./Data/Migrations/20261108045337_Initial_Migration.cs) — EF Core DbContext (`ApplyConfigurationsFromAssembly(typeof(User).Assembly)`) and PostgreSQL migrations.
- **`Services/` ([`Studium-Users.Services.csproj`](./Services/Studium-Users.Services.csproj)):**
  - [`Services/DTOs/UserDtos.cs`](./Services/DTOs/UserDtos.cs) & [`Services/DTOs/EventDtos.cs`](./Services/DTOs/EventDtos.cs) — Positional record DTOs (`UserDetailDto`, `UserListItemDto`, `UpdateProfileRequest`, `BlockUserRequest`, `PagedUsersResponse<T>`) and SQS event contracts.
  - [`Services/BookingEventsHandlers/BookingEventsHandler.cs`](./Services/BookingEventsHandlers/BookingEventsHandler.cs) — Wolverine handler updating 30-day activity counters from `BookingIntegrationEvent`.
- **`Api/` ([`Studium-Users.Api.csproj`](./Api/Studium-Users.Api.csproj)):**
  - [`Api/Program.cs`](./Api/Program.cs) — Declarative composition root (~55 lines) with startup `MigrateAsync()`, `DomainExceptionHandler`, `AddStudiumMessaging()`, `AddStudiumAuthentication()`, OpenAPI + Scalar (`/scalar/v1`), and `/health`.
  - [`Api/Controllers/UsersController.cs`](./Api/Controllers/UsersController.cs) — Unified `/api/users` REST controller with JWT auto-provisioning and `IsSelfOrAdmin` IDOR protection.
  - [`Api/Middleware/DomainExceptionHandler.cs`](./Api/Middleware/DomainExceptionHandler.cs) — `.NET 10` `IExceptionHandler` mapping `DomainException` to `400 Bad Request`.
  - [`Api/Extensions/AuthenticationExtensions.cs`](./Api/Extensions/AuthenticationExtensions.cs) & [`Api/Extensions/MessagingExtensions.cs`](./Api/Extensions/MessagingExtensions.cs) — Modular DI & `ClaimsPrincipal` extensions.
- **Centralized CDK Repository (`../Studium-Infra`):**
  - [`Studium-Infra.csproj`](../Studium-Infra/Studium-Infra.csproj) & [`Stacks/StudiumStack.cs`](../Studium-Infra/Stacks/StudiumStack.cs) — `.NET 10` C# AWS CDK project (`Amazon.CDK.Lib` `2.275.0`) provisioning `NetworkConstruct`, `CognitoConstruct`, `DatabaseConstruct`, `MessagingConstruct`, `EcrConstruct` (container registries), and `EksConstruct` (EKS 1.31 Cluster + Managed Nodegroup + IAM roles) for the `Studium-PUCPR` organization.
- **`Tests/` ([`Studium-Users.Tests.csproj`](./Tests/Studium-Users.Tests.csproj)):**
  - xUnit test suite (26 passing tests) covering `Domain`, `DTOs`, and `Services`.
- [`Dockerfile`](./Dockerfile) & [`.dockerignore`](./.dockerignore) — Multi-stage `.NET 10.0` container image pre-generating Wolverine code (`dotnet run ... -- codegen write`) into the image so production runs with `TypeLoadMode.Static`.
- [`k8s/deployment.yaml`](./k8s/deployment.yaml) — Kubernetes manifests defining the `studium` namespace, `studium-users` Deployment (with liveness/readiness health probes on `/health`), ClusterIP Service, and HorizontalPodAutoscaler (HPA).
- [`.github/workflows/ci-cd.yml`](./.github/workflows/ci-cd.yml) — GitHub Actions CI/CD pipeline running unit tests, building & tagging Docker images with pre-compiled Wolverine codegen, pushing to ECR, and deploying to `studium-eks-cluster`.

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
- **2026-11-08 (Created C# AWS CDK Infrastructure Project & Institutional Cognito Setup):**
  - Created `Infra/Studium-Users.Infra.csproj`, `Infra/cdk.json`, `Infra/Program.cs`, and `Infra/Stacks/StudiumUsersStack.cs` using `Amazon.CDK.Lib` (`2.275.0`).
  - Configured the Amazon Cognito User Pool (`studium-user-pool`) for institutional/SSO users (`SelfSignUpEnabled = false`), OAuth 2.0 Authorization Code Grant + Hosted UI domain (`studium-auth`), `custom:registration_number` attribute, and `Student`/`Professor`/`Administrator` groups.
- **2026-11-08 (Added Central Package Management & Shared Build Props):**
  - Added `Directory.Packages.props` (`ManagePackageVersionsCentrally=true`) and `Directory.Build.props` to centralize all NuGet package versions and target framework settings across `Studium-Users.csproj`, `Tests/Studium-Users.Tests.csproj`, and `Infra/Studium-Users.Infra.csproj`.
- **2026-11-08 (Split Solution into 6 Clean Architecture Projects):**
  - Split the codebase into `Domain/Studium-Users.Domain.csproj`, `Data/Studium-Users.Data.csproj`, `Services/Studium-Users.Services.csproj`, `Api/Studium-Users.Api.csproj`, `Infra/Studium-Users.Infra.csproj`, and `Tests/Studium-Users.Tests.csproj` under `Studium-Users.sln`.
  - Removed all `<DefaultItemExcludes>` workarounds and decoupled `User.ToggleBlock` from `DTOs` so `Domain` has zero outward project dependencies.
- **2026-11-08 (Extracted CognitoConstruct & Simplified CDK Stack):**
  - Extracted Cognito resources into `Infra/Constructs/CognitoConstruct.cs` so `Infra/Stacks/StudiumUsersStack.cs` remains a clean composition root.
  - Referenced `Studium-Users.Domain` from `Infra` to use `Enum.GetNames<UserRole>()` with `UserPool.AddGroup` and `UserPool.UserPoolProviderUrl`.
- **2026-11-08 (Evaluated Aurora DSQL vs. Standard RDS PostgreSQL & Added DatabaseConstruct):**
  - Chose Standard RDS PostgreSQL 16 (`db.t4g.micro`, Single-AZ, 20 GB GP2, Free Tier eligible) in the Default VPC over Aurora DSQL (which lacks foreign keys, standard password auth, and multi-DDL transactions required by EF Core `MigrateAsync()`) and over Aurora Serverless v2 (no Free Tier).
  - Created `Infra/Constructs/DatabaseConstruct.cs` and wired it into `Infra/Stacks/StudiumUsersStack.cs`.
- **2026-11-08 (Centralized VPC into NetworkConstruct):**
  - Created `Infra/Constructs/NetworkConstruct.cs` to resolve the shared `StudiumDefaultVpc` once, output `StudiumVpcId`, and pass `network.Vpc` into `DatabaseConstruct` and future compute constructs.
- **2026-11-08 (Removed Outbound UserEventsPublisher & Added MessagingConstruct):**
  - Removed `Services/UserEventsPublishers/`, `UserStatusEvent`, `UserEventType`, and `OutboundQueueNames` since `Studium-Users` only consumes inbound `BookingIntegrationEvent` messages from `studium-users-events-queue` (other services query `GET /api/users/{id}` directly for user permissions/preferences).
  - Created `Infra/Constructs/MessagingConstruct.cs` provisioning `studium-users-events-queue` (`ReceiveMessageWaitTime = Duration.Seconds(20)`) and wired it into `Infra/Stacks/StudiumUsersStack.cs`.
- **2026-11-08 (Extracted AWS CDK into Dedicated Studium-Infra Repository):**
  - Moved the C# AWS CDK project out of `Studium-Users/Infra` into a dedicated repository at `../Studium-Infra` (`Studium-PUCPR/Studium-Infra`) so shared AWS resources (VPC, Cognito, RDS PostgreSQL, EKS, SQS) are managed centrally without cross-repo CloudFormation collisions.
- **2026-11-08 (Added EksConstruct to Studium-Infra):**
  - Created `Constructs/EksConstruct.cs` in `../Studium-Infra` provisioning the `studium-users` ECR repository (`EmptyOnDelete = true`, `MaxImageCount = 5`), the `studium-eks-cluster` EKS 1.31 cluster, and the `studium-eks-nodes` managed EC2 nodegroup (`t3.small`, min 1 / desired 2 / max 3) with IAM permissions for ECR and SQS.
- **2026-11-08 (Split EcrConstruct out of EksConstruct):**
  - Moved the `studium-users` ECR repository into `Constructs/EcrConstruct.cs` in `../Studium-Infra` since container registries have an independent lifecycle (used by CI/CD image pushes) from the EKS cluster; EKS-only IAM roles remain inside `EksConstruct`.
- **2026-11-08 (Wolverine Pre-Codegen, Kubernetes Manifests & GitHub Actions CI/CD):**
  - Configured Wolverine static code generation in `Api/Program.cs` (`CritterStackDefaults`, `RunJasperFxCommands`, and `DatabaseMigrationHostedService` so migrations only run on app host start).
  - Updated `Dockerfile` to run `dotnet run ... -- codegen write` during the build stage; the resulting pre-compiled assembly is packaged into the Docker image, eliminating runtime dynamic compilation in EKS.
  - Added `k8s/deployment.yaml` with Namespace, ConfigMap, Deployment (probes, secret bindings, resources), ClusterIP Service, and HPA (2-5 replicas).
  - Created `.github/workflows/ci-cd.yml` with OIDC AWS credentials, test verification, ECR image push, and EKS `kubectl apply`.
- **2026-11-08 (Added ParametersConstruct & Region sa-east-1):**
  - Updated region default to `sa-east-1` across infrastructure and manifests.
  - Added `Constructs/ParametersConstruct.cs` in `Studium-Infra` to create SSM Parameter Store entries (`/studium/users/cognito/authority`, `/studium/users/cognito/client-id`, and `/studium/users/database/connection-string`) linking Cognito and RDS endpoints.
