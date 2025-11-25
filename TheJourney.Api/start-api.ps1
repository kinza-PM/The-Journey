# Set environment variables
$env:PG_HOST="journey.postgres.database.azure.com"
$env:PG_USER="journeyDev"
$env:PG_PASSWORD="M.AbuBakar@"
$env:PG_DB="postgres"
$env:JWT_SECRET="TheJourney-Super-Secret-JWT-Key-For-Authentication-2024!"
$env:JWT_ISSUER="TheJourney.Api"
$env:JWT_AUDIENCE="TheJourney.Api"
$env:SEED_ADMIN_EMAIL="admin@thejourney.com"
$env:SEED_ADMIN_PASSWORD="Admin@123Secure"
$env:LOCKOUT_MAX_ATTEMPTS="5"
$env:LOCKOUT_MINUTES="15"
$env:OTP_EXPIRY_MINUTES="10"
$env:MAILTRAP_SMTP_HOST="sandbox.smtpme.mailtrap.io"
$env:MAILTRAP_SMTP_PORT="2525"
$env:MAILTRAP_SMTP_USERNAME="53f5b609367e98"
$env:MAILTRAP_SMTP_PASSWORD="c990bd5dea90e1"
$env:MAILTRAP_FROM_EMAIL="verify@thejourney.com"
$env:STUDENT_JWT_EXPIRY_MINUTES="60"
$env:STUDENT_LOCKOUT_MAX_ATTEMPTS="5"
$env:STUDENT_LOCKOUT_MINUTES="15"
$env:PASSWORD_RESET_EXPIRY_MINUTES="30"

# LinkedIn OAuth config - replace placeholders with your app values
$env:LINKEDIN_CLIENT_ID=""
$env:LINKEDIN_CLIENT_SECRET=""

# Example redirect URI for local development. Make sure this matches the redirect registered in your LinkedIn app.
$env:LINKEDIN_REDIRECT_URI="http://localhost:5097/api/mobile/auth/linkedin/callback"

Write-Host "Environment variables set. Starting API..."
dotnet run