# Set environment variables
$env:PG_HOST="journey.postgres.database.azure.com"
$env:PG_USER="journeyDev"
$env:PG_PASSWORD="Secure@PgSQL17"
$env:PG_DB="postgres"
$env:JWT_SECRET="TheJourney-Super-Secret-JWT-Key-For-Authentication-2024!"
$env:JWT_ISSUER="TheJourney.Api"
$env:JWT_AUDIENCE="TheJourney.Api"
$env:SEED_ADMIN_EMAIL="admin@thejourney.com"
$env:SEED_ADMIN_PASSWORD="Admin@123Secure"
$env:LOCKOUT_MAX_ATTEMPTS="5"
$env:LOCKOUT_MINUTES="15"

Write-Host "Environment variables set. Starting API..."
dotnet run

