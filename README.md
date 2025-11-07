# TheJourney API – Admin Authentication

.NET 8 Web API providing admin login, JWT and session authentication, role-based access control, and login audit logging backed by PostgreSQL.

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
```

## Setup
```powershell
cd TheJourney.Api
dotnet restore
dotnet ef database update   # applies migrations + seeds SuperAdmin if env vars are present
```

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

## Dev Notes
- Keep secrets out of source control; rely on environment variables or secret managers.
- Update migrations with `dotnet ef migrations add <Name>` and run `dotnet ef database update`.
- Adjust seeding logic if multiple environments need different admin accounts.
