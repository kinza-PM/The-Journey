# TheJourney API – Admin Authentication

A professional .NET 8 Web API with PostgreSQL, JWT and Session authentication, Role-Based Access Control (RBAC), and comprehensive audit logging. Provides admin login, JWT and session authentication, role-based access control, and login audit logging backed by PostgreSQL.

## Tech Stack
- ASP.NET Core 8
- Entity Framework Core + Npgsql
- PostgreSQL
- JWT & cookie-based sessions
- BCrypt password hashing

## Prerequisites
- .NET 8 SDK
- PostgreSQL (or Azure Database for PostgreSQL)
- PowerShell (for convenience scripts)

## Configuration
Set the following environment variables before running the API. The `start-api.ps1` script demonstrates how to configure them locally.

```
PG_HOST
PG_USER
PG_PASSWORD
PG_DB
JWT_SECRET          (≥ 32 chars)
JWT_ISSUER
JWT_AUDIENCE
SEED_ADMIN_EMAIL
SEED_ADMIN_PASSWORD
LOCKOUT_MAX_ATTEMPTS (default 5)
LOCKOUT_MINUTES      (default 15)
OTP_EXPIRY_MINUTES   (default 10)
MAILTRAP_SMTP_HOST
MAILTRAP_SMTP_PORT
MAILTRAP_SMTP_USERNAME
MAILTRAP_SMTP_PASSWORD
MAILTRAP_FROM_EMAIL
STUDENT_JWT_EXPIRY_MINUTES (default 60)
STUDENT_LOCKOUT_MAX_ATTEMPTS (default 5)
STUDENT_LOCKOUT_MINUTES (default 15)
PASSWORD_RESET_EXPIRY_MINUTES (default 30)
```

## Setup
```powershell
cd TheJourney.Api
dotnet restore
dotnet ef database update   # applies migrations + seeds SuperAdmin if env vars are present
```

If you are using Mailtrap's sandbox SMTP credentials, configure auto-forwarding in the Mailtrap UI so OTP and reset emails reach your test inboxes.

## Run
```powershell
# recommended – sets env vars then runs the API
.\start-api.ps1

# manual alternative
dotnet run
```

Swagger UI: `https://localhost:7145/swagger`

## Authentication Flow
- `POST /api/auth/login` (query `authType=SESSION` to request cookie-based session)
- `POST /api/auth/logout`
- `GET /api/auth/protected` – any authenticated admin with a role
- `GET /api/auth/admin-access` – Admin or SuperAdmin
- `GET /api/auth/superadmin-only` – SuperAdmin only
- `POST /api/mobile/auth/signup` – student signup via email (OTP via email; password requires ≥8 chars, uppercase, special character)
- `POST /api/mobile/auth/verify` – confirm OTP for email
- `POST /api/mobile/auth/resend-otp` – request another OTP when expired
- `POST /api/mobile/auth/login` – JWT login with email/password
- `POST /api/mobile/auth/forgot-password` – request password reset OTP (sent to email)
- `POST /api/mobile/auth/reset-password` – verify OTP and set new password (requires OTP code, new password, and confirm password)

Successful and failed attempts are stored in `LoginAttempts`. Accounts lock after `LOCKOUT_MAX_ATTEMPTS` failures and unlock automatically after `LOCKOUT_MINUTES`.

## Structure
```
TheJourney.Api/
├── Infrastructure/Database/    # AppDbContext + factory
├── Modules/Auth/               # Controllers, services, models
├── Migrations/                 # EF Core migrations
├── Program.cs                  # bootstrap + auth configuration
├── start-api.ps1               # env setup script
└── test-login.ps1              # sample login test
```

## Deployment

### Quick Start
See [AZURE_QUICK_START.md](./AZURE_QUICK_START.md) for a 5-step deployment guide.

### Full Deployment Guide
See [DEPLOYMENT.md](./DEPLOYMENT.md) for complete step-by-step instructions to deploy to Azure App Service using Azure DevOps.

### Deployment Files
- `azure-pipelines.yml` - Azure DevOps CI/CD pipeline configuration
- `appsettings.Production.json` - Production configuration settings

## Dev Notes
- Keep secrets out of source control; rely on environment variables or secret managers.
- Update migrations with `dotnet ef migrations add <Name>` and run `dotnet ef database update`.
- Adjust seeding logic if multiple environments need different admin accounts.
- CORS is configured for mobile apps - set `CORS_ALLOWED_ORIGINS` environment variable in production.
