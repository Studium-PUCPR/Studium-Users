# Studium-Users

Stack: .NET + PostgreSQL (db-users).

Scope: User profiles and notification preferences (UC004), admin user bans/unbans (UC006), and immutable audit logs (RNF14).

REST Endpoint for Service Discovery: Exposes GET http://studium-users-service/api/users/{id}/eligibility inside the EKS cluster so Studium-Booking can verify if a user is blocked before creating a reservation.   
