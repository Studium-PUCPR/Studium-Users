# Studium-Users (.NET Microservice)

Scope: RF4, RF6, RF18, RNF14.

Responsibilities:

Profiles & Preferences: Syncs user records from Cognito, allowing edits to phone numbers and notification toggles (UC004 / M16).

Admin User Management: Tracks 30-day no-show strikes and lets admins block/unblock accounts (UC006 / W06–W07), calling Cognito to revoke active sessions and publishing UserBlocked to SNS.

Notifications & In-App Feed (SQS Consumer): Subscribes its SQS queue to the SNS topics from Studium-Booking and Studium-Catalog. Whenever an event arrives, it persists the alert for the in-app Notification Center (UC018 / M17), checks user preference toggles (RN20), and dispatches transactional emails/push alerts via Amazon SES / SNS.

Security Audit Trail: Stores immutable audit records for logins, blocks, and critical actions (RNF14).
