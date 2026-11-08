# Studium-Users

Stack: `.NET 10.0` Web API + PostgreSQL (`studium_users`) + Amazon SQS (`AWSSDK.SQS`) + Amazon Cognito (`JwtBearer`) + Scalar API Reference (`/scalar/v1`).

## Scope
- **User Profiles & Notification Preferences:** View profile and update `PhoneNumber`, `NotifyReminders`, and `NotifyConfirmations` (`NotifyCancellationsAndWaitlist` is always `true`).
- **Admin User Management & Blocking:** Search/filter users, inspect 30-day reservation/no-show counters, block accounts with a mandatory reason, and unblock accounts.
- **Immutable Security Audit Trail:** Automatically records `UserLogin`, `ProfileUpdated`, `UserBlocked`, `UserUnblocked`, and `UnauthorizedAccess` in `AuditLogs`.
- **Async SQS Integration:** Consumes booking events (`ReservationCreated`, `ReservationCheckedIn`, `ReservationCancelled`, `NoShowRecorded`) via `BookingEventsConsumer` and publishes user status/preference updates via `UserEventsPublisher`.

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
