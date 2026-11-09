# Studium-Users

Stack: `.NET 10.0` Web API + PostgreSQL (`studium_users`) + RabbitMQ (`WolverineFx.RabbitMQ`) + Amazon Cognito (`JwtBearer`) + Scalar API Reference (`/scalar/v1`).

> **Note on Messaging Architecture:** Messaging is powered by **RabbitMQ** (AMQP) via Wolverine for academic compliance. A legacy AWS SQS version is preserved on branch `origin/sqs`.

## Scope
- **User Profiles & Notification Preferences:** View profile and update `PhoneNumber`, `NotifyReminders`, and `NotifyConfirmations` (`NotifyCancellationsAndWaitlist` is always `true`).
- **Admin User Management & Blocking:** Search/filter users, inspect 30-day reservation/no-show counters, block accounts with a mandatory reason, and unblock accounts.
- **Centralized Audit Integration:** Publishes domain audit events (`AuditIntegrationEvent`) to RabbitMQ queue `studium-audit-events-queue` for persistent compliance in `Studium-Audit`, and proxies user audit lookups to `Studium-Audit` via synchronous HTTP (`GET /api/audit/resources/{id}`).
- **Async RabbitMQ Integration:** Consumes campus activity events (`UserActivityIntegrationEvent`) via Wolverine RabbitMQ queues to maintain 30-day user activity counters and enforce account suspension policies, and publishes user status/preference updates (`UserStatusChangedEvent`).

## Local Environment Setup & Testing

### 1. Start PostgreSQL in Docker
Create and start the local PostgreSQL container on the first run:
```bash
docker run --name studium-users-db \
  -e POSTGRES_USER=postgres \
  -e POSTGRES_PASSWORD=postgres \
  -e POSTGRES_DB=studium_users \
  -p 5432:5432 \
  -d postgres:16-alpine
```
*(If the container already exists, simply run `docker start studium-users-db`)*

### 2. Restore & Run the Application
```bash
dotnet restore
dotnet run --project Api/Studium-Users.Api.csproj
```
Once running in `Development` mode, open the interactive API reference at `http://localhost:5155/scalar/v1`.

### 3. Run Unit Tests
```bash
dotnet test
```
