# Run API in Background on VM

This guide shows you how to run the API in the background so it keeps running even after you close the terminal.

## Option 1: Using `screen` (Recommended - Easy)

### Step 1: Install screen (if not installed)

```bash
sudo apt-get update
sudo apt-get install -y screen
```

### Step 2: Create a startup script

```bash
cd ~/The-Journey/TheJourney.Api
nano start-api-background.sh
```

Paste this content:

```bash
#!/bin/bash

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

# Navigate to API directory
cd ~/The-Journey/TheJourney.Api

# Run the API
dotnet run --configuration Release --no-launch-profile
```

Save and exit (Ctrl+X, Y, Enter)

### Step 3: Make it executable

```bash
chmod +x start-api-background.sh
```

### Step 4: Start API in screen

```bash
screen -S thejourney-api
./start-api-background.sh
```

### Step 5: Detach from screen

Press: `Ctrl+A` then `D`

The API is now running in the background!

### Useful screen commands:

```bash
# List all screen sessions
screen -ls

# Reattach to the API session
screen -r thejourney-api

# Kill the screen session (stops API)
screen -X -S thejourney-api quit
```

---

## Option 2: Using `systemd` (Production - Auto-start on boot)

### Step 1: Create systemd service file

```bash
sudo nano /etc/systemd/system/thejourney-api.service
```

Paste this content:

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
StandardOutput=journal
StandardError=journal

[Install]
WantedBy=multi-user.target
```

Save and exit (Ctrl+X, Y, Enter)

### Step 2: Enable and start the service

```bash
# Reload systemd
sudo systemctl daemon-reload

# Enable service to start on boot
sudo systemctl enable thejourney-api

# Start the service
sudo systemctl start thejourney-api

# Check status
sudo systemctl status thejourney-api
```

### Useful systemd commands:

```bash
# Check status
sudo systemctl status thejourney-api

# View logs
sudo journalctl -u thejourney-api -f

# Stop the service
sudo systemctl stop thejourney-api

# Restart the service
sudo systemctl restart thejourney-api

# Disable auto-start on boot
sudo systemctl disable thejourney-api
```

---

## Option 3: Using `nohup` (Simple but less robust)

```bash
cd ~/The-Journey/TheJourney.Api

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

# Run in background with nohup
nohup dotnet run --configuration Release --no-launch-profile > api.log 2>&1 &

# Check if it's running
ps aux | grep dotnet

# View logs
tail -f api.log

# Stop it
pkill -f "dotnet.*TheJourney.Api"
```

---

## Quick Setup Script (All-in-One)

Save this as `setup-background-api.sh`:

```bash
#!/bin/bash

echo "=== Setting up TheJourney API to run in background ==="

# Install screen if not installed
if ! command -v screen &> /dev/null; then
    echo "Installing screen..."
    sudo apt-get update
    sudo apt-get install -y screen
fi

# Create startup script
cd ~/The-Journey/TheJourney.Api
cat > start-api-background.sh << 'EOF'
#!/bin/bash
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
cd ~/The-Journey/TheJourney.Api
dotnet run --configuration Release --no-launch-profile
EOF

chmod +x start-api-background.sh

echo ""
echo "=== Setup complete! ==="
echo ""
echo "To start API in background:"
echo "  screen -S thejourney-api ./start-api-background.sh"
echo ""
echo "Then press Ctrl+A, then D to detach"
echo ""
echo "To reattach later:"
echo "  screen -r thejourney-api"
echo ""
echo "To stop:"
echo "  screen -X -S thejourney-api quit"
```

Make it executable and run:
```bash
chmod +x setup-background-api.sh
./setup-background-api.sh
```

---

## Recommended: Use systemd (Production)

For production, use systemd as it:
- Auto-starts on boot
- Auto-restarts if it crashes
- Better logging
- More reliable

Use screen for quick testing or development.

