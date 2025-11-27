# Quick Commands to Update VM

Run these commands on your VM:

## Step 1: Navigate to Project
```bash
cd ~/The-Journey
```

## Step 2: Check Current Status
```bash
git status
git log --oneline -5
```

## Step 3: Pull Latest Code
```bash
git fetch origin
git pull origin main
```

## Step 4: Verify Update
```bash
git log --oneline -5
```

## Step 5: Stop Current API (if running)
```bash
# If using screen
screen -r thejourney-api
# Press Ctrl+C, then Ctrl+A, then D

# Or if using systemd
sudo systemctl stop thejourney-api
```

## Step 6: Rebuild and Restart
```bash
cd TheJourney.Api

# Set environment variables
export PG_HOST="journey.postgres.database.azure.com"
export PG_USER="journeyDev"
export PG_PASSWORD="M.AbuBakar@Password"
export PG_DB="postgres"
export JWT_SECRET="TheJourney-Super-Secret-JWT-Key-For-Authentication-2024!"
export JWT_ISSUER="TheJourney.Api"
export JWT_AUDIENCE="TheJourney.Api"
export SEED_ADMIN_EMAIL="admin@thejourney.com"
export SEED_ADMIN_PASSWORD="Admin@123Secure"
export API_BASE_URL="http://4.236.186.123:5000"
export ENABLE_SWAGGER="true"
export CORS_ALLOWED_ORIGINS="*"
export ASPNETCORE_URLS="http://0.0.0.0:5000"

# Build
dotnet build --configuration Release

# Run
dotnet run --configuration Release --no-launch-profile
```

