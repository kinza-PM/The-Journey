# TheJourney API - Admin Login & RBAC

Testing Azure 

A professional .NET 8 Web API with PostgreSQL, JWT and Session authentication, Role-Based Access Control (RBAC), and comprehensive audit logging.

## ✅ Status: READY TO USE

- ✅ Database migrations applied successfully
- ✅ SuperAdmin seeded and ready
- ✅ All ticket requirements implemented
- ✅ JWT and Session authentication supported
- ✅ Failed login attempts logged
- ✅ Role validation enforced (no role = no access)

## 🚀 Quick Start

### 1. Start the API
```powershell
cd TheJourney.Api
.\start-api.ps1
```

### 2. Test Login
Open Swagger UI: **https://localhost:7145/swagger**

Use credentials:
- **Email**: `admin@thejourney.com`
- **Password**: `Admin@123Secure`

### 3. Authentication Options

**JWT Authentication (default):**
```json
POST /api/auth/login
{
  "email": "admin@thejourney.com",
  "password": "Admin@123Secure"
}
```

**Session Authentication:**
```json
POST /api/auth/login?authType=SESSION
{
  "email": "admin@thejourney.com",
  "password": "Admin@123Secure"
}
```

## 📋 Configuration

### Database
- **Host**: journey.postgres.database.azure.com
- **User**: journeyDev
- **Database**: postgres
- **Status**: ✅ Connected and migrated

### Seeded SuperAdmin
- **Email**: admin@thejourney.com
- **Password**: Admin@123Secure
- **Role**: SuperAdmin

### Environment Variables
Set these in Cursor workspace secrets or use the startup script:
- `PG_HOST`, `PG_USER`, `PG_PASSWORD`, `PG_DB`
- `JWT_SECRET`, `JWT_ISSUER`, `JWT_AUDIENCE`
- `SEED_ADMIN_EMAIL`, `SEED_ADMIN_PASSWORD`
- `LOCKOUT_MAX_ATTEMPTS` (default: 5)
- `LOCKOUT_MINUTES` (default: 15)

## 🎯 Features Implemented

### ✅ Ticket Requirements Completed

1. **JWT/Session Authentication** ✅
   - JWT token-based authentication
   - Session-based authentication with cookies
   - Automatic scheme selection (JWT or Session)

2. **RBAC Enforcement** ✅
   - Role-based authorization policies
   - SuperAdminOnly policy
   - AdminAccess policy (Admin + SuperAdmin)
   - RequireRole policy (any authenticated admin with role)

3. **Failed Login Logging** ✅
   - All login attempts logged to `LoginAttempts` table
   - Tracks: email, success/failure, reason, IP address, user agent, timestamp
   - Audit trail for security monitoring

4. **Account Lockout** ✅
   - Configurable max attempts (default: 5)
   - Temporary lockout with expiration
   - Automatic unlock after lockout period

5. **Role Validation** ✅
   - No role assigned → Access denied
   - Role validation on login
   - Role validation on token generation
   - Enforced in all authorization policies

### Additional Features
- ✅ Password hashing with BCrypt
- ✅ Database migrations with EF Core
- ✅ SuperAdmin seeding on startup
- ✅ Swagger UI with authentication support
- ✅ Protected endpoints with role-based access
- ✅ Comprehensive error handling

## 📁 Project Structure

```
TheJourney.Api/
├── Infrastructure/
│   └── Database/
│       ├── AppDbContext.cs
│       └── AppDbContextFactory.cs
├── Modules/
│   ├── Auth/
│   │   ├── Controllers/
│   │   │   └── AuthController.cs
│   │   ├── Models/
│   │   │   ├── Admin.cs
│   │   │   └── LoginAttempt.cs
│   │   └── Services/
│   │       └── AuthService.cs
│   └── Admin/ (placeholder)
├── Migrations/
│   ├── 20251105131552_InitialCreate.cs
│   └── 20251105141239_AddLoginAttempts.cs
├── Program.cs
└── start-api.ps1
```

## 🔐 API Endpoints

### POST `/api/auth/login`
Login with email and password. Supports JWT (default) or Session authentication.

**Query Parameters:**
- `authType` (optional): `JWT` or `SESSION` (default: `JWT`)

**Request:**
```json
{
  "email": "admin@thejourney.com",
  "password": "Admin@123Secure"
}
```

**Response (JWT):**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "message": "Login successful"
}
```

**Response (Session):**
```json
{
  "sessionId": "guid-here",
  "message": "Login successful"
}
```

### POST `/api/auth/logout`
Logout and clear session (requires authentication).

### GET `/api/auth/protected`
Protected endpoint requiring authentication and a valid role.

**Headers (JWT):**
```
Authorization: Bearer <your-jwt-token>
```

**Headers (Session):**
```
Cookie: TheJourney.Session=<session-cookie>
```

### GET `/api/auth/superadmin-only`
Protected endpoint accessible only to SuperAdmin users.

### GET `/api/auth/admin-access`
Protected endpoint accessible to Admin and SuperAdmin users.

## 🔍 Failed Login Logging

All login attempts are logged to the `LoginAttempts` table:

**Query failed attempts:**
```sql
SELECT * FROM "LoginAttempts" 
WHERE "IsSuccess" = false 
ORDER BY "AttemptedAt" DESC;
```

**Query all attempts for a user:**
```sql
SELECT * FROM "LoginAttempts" 
WHERE "Email" = 'admin@thejourney.com' 
ORDER BY "AttemptedAt" DESC;
```

## 🛡️ Security Features

- **Password Hashing**: BCrypt with automatic salt
- **JWT Tokens**: Secure token-based authentication with role claims
- **Session Authentication**: Cookie-based sessions with secure flags
- **Account Lockout**: Configurable lockout after failed attempts
- **Role Validation**: No role = No access (enforced)
- **Audit Logging**: All login attempts logged with IP and user agent
- **SSL/TLS**: Secure database connections
- **Environment Variables**: All secrets stored in environment, never in code

## 🔧 Development

### Run Migrations
```bash
dotnet ef database update
```

### Build
```bash
dotnet build
```

### Start API
```powershell
.\start-api.ps1
```

## 📝 Database Tables

### Admins
- `Id`, `Email`, `PasswordHash`, `Role`
- `FailedLoginAttempts`, `IsLocked`, `LockUntil`
- `CreatedAt`

### LoginAttempts
- `Id`, `Email`, `IsSuccess`, `FailureReason`
- `IpAddress`, `UserAgent`, `AttemptedAt`
- `AdminId` (foreign key to Admins)

## 🎉 Ready to Use!

Everything is configured and ready. Simply run `.\start-api.ps1` and start testing!
