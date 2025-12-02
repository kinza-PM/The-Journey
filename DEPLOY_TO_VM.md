# Deploy TheJourney API to Azure VM

This guide helps you deploy the latest code from **Azure DevOps Repos** to your Azure VM.

**Repository:** https://dev.azure.com/journey-devops/JourneyApp/_git/The-Journey.git

## Prerequisites

- Azure VM running Ubuntu (already set up)
- SSH access to the VM
- .NET 9 SDK installed on the VM
- PostgreSQL database connection details

## Step 1: Connect to VM

```powershell
# From your local machine (PowerShell)
ssh thejourneyapi@4.236.186.123
```

## Step 2: Navigate to Project Directory

```bash
cd ~/The-Journey
```

## Step 3: Pull Latest Code from Azure DevOps

**If this is the first time setting up on VM:**

```bash
# Clone the repository from Azure DevOps
cd ~
git clone https://dev.azure.com/journey-devops/JourneyApp/_git/The-Journey.git
cd The-Journey
```

**If repository already exists on VM:**

```bash
# Pull latest changes from Azure DevOps
git pull origin main

# If you need to reset to match Azure DevOps exactly:
# git fetch origin
# git reset --hard origin/main
```

**If VM is still pointing to GitHub, update the remote:**

```bash
# Check current remote
git remote -v

# Change to Azure DevOps
git remote set-url origin https://dev.azure.com/journey-devops/JourneyApp/_git/The-Journey.git

# Verify
git remote -v

# Pull latest code
git pull origin main
```

## Step 4: Set Environment Variables

```bash
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
```

## Step 5: Build and Run

```bash
cd TheJourney.Api

# Restore packages
dotnet restore

# Build
dotnet build --configuration Release

# Run (use --no-launch-profile to ignore launchSettings.json)
dotnet run --configuration Release --no-launch-profile
```

## Step 6: Verify API is Running

You should see:
```
Now listening on: http://0.0.0.0:5000
```

Access Swagger UI:
```
http://4.236.186.123:5000/swagger
```

## Step 7: Keep API Running (Background)

### Option 1: Use `screen` (Recommended)

```bash
# Install screen if needed
sudo apt-get update && sudo apt-get install -y screen

# Start a screen session
screen -S thejourney-api

# Inside screen, set environment variables and run API
# (Copy all export commands from Step 4)
cd ~/The-Journey/TheJourney.Api
dotnet run --configuration Release --no-launch-profile

# Detach from screen: Press Ctrl+A, then D
# Reattach later: screen -r thejourney-api
```

### Option 2: Use `systemd` (Production - Auto-start on boot)

Create a systemd service:

```bash
sudo nano /etc/systemd/system/thejourney-api.service
```

Add this content:

```ini
[Unit]
Description=TheJourney API
After=network.target

[Service]
Type=simple
User=thejourneyapi
WorkingDirectory=/home/thejourneyapi/The-Journey/TheJourney.Api
Environment="PG_HOST=journey.postgres.database.azure.com"
Environment="PG_USER=journeyDev"
Environment="PG_PASSWORD=M.AbuBakar@Password"
Environment="PG_DB=postgres"
Environment="JWT_SECRET=TheJourney-Super-Secret-JWT-Key-For-Authentication-2024!"
Environment="JWT_ISSUER=TheJourney.Api"
Environment="JWT_AUDIENCE=TheJourney.Api"
Environment="SEED_ADMIN_EMAIL=admin@thejourney.com"
Environment="SEED_ADMIN_PASSWORD=Admin@123Secure"
Environment="API_BASE_URL=http://4.236.186.123:5000"
Environment="ENABLE_SWAGGER=true"
Environment="CORS_ALLOWED_ORIGINS=*"
Environment="ASPNETCORE_URLS=http://0.0.0.0:5000"
ExecStart=/usr/bin/dotnet run --configuration Release --no-launch-profile
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
```

Then enable and start:

```bash
sudo systemctl daemon-reload
sudo systemctl enable thejourney-api
sudo systemctl start thejourney-api
sudo systemctl status thejourney-api
```

## Troubleshooting

### Port 5000 Not Accessible

1. Go to Azure Portal
2. Navigate to your VM: `thejourneyvm`
3. Click **"Networking"**
4. Click **"Add inbound port rule"**
5. Set:
   - Port: `5000`
   - Protocol: `TCP`
   - Action: `Allow`
   - Name: `Allow-API-Port`
6. Click **"Add"**

### API Still Listening on localhost

Make sure you set:
```bash
export ASPNETCORE_URLS="http://0.0.0.0:5000"
```

And use:
```bash
dotnet run --configuration Release --no-launch-profile
```

### Database Connection Issues

Verify your PostgreSQL connection details:
```bash
echo $PG_HOST
echo $PG_USER
echo $PG_DB
# Don't echo password for security
```

## API Base URL for Mobile Developers

```
Base URL: http://4.236.186.123:5000
Swagger UI: http://4.236.186.123:5000/swagger
```

