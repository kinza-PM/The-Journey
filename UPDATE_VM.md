# Update VM with Latest Code from Azure DevOps

This guide helps you update your Azure VM with the latest code from Azure DevOps Repos.

## Quick Update Commands

### Step 1: Connect to VM

```bash
ssh thejourneyapi@4.236.186.123
```

### Step 2: Navigate to Project Directory

```bash
cd ~/The-Journey
```

### Step 3: Check Current Remote

```bash
git remote -v
```

**Expected output:**
```
origin  https://dev.azure.com/journey-devops/JourneyApp/_git/The-Journey.git (fetch)
origin  https://dev.azure.com/journey-devops/JourneyApp/_git/The-Journey.git (push)
```

**If it shows GitHub instead, update it:**
```bash
git remote set-url origin https://dev.azure.com/journey-devops/JourneyApp/_git/The-Journey.git
git remote -v  # Verify it's updated
```

### Step 4: Stop Running API (if running)

If the API is running in a screen session:
```bash
screen -r thejourney-api
# Press Ctrl+C to stop the API
# Press Ctrl+A then D to detach
```

Or if using systemd:
```bash
sudo systemctl stop thejourney-api
```

### Step 5: Pull Latest Code

```bash
# Fetch latest changes
git fetch origin

# Pull latest code from main branch
git pull origin main

# Or if you're on a different branch and want to update to main:
git checkout main
git pull origin main
```

### Step 6: Verify Updates

```bash
# Check latest commit
git log --oneline -5

# Check current branch
git branch
```

### Step 7: Rebuild and Restart API

```bash
cd TheJourney.Api

# Restore packages (if needed)
dotnet restore

# Build the project
dotnet build --configuration Release

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

# Run the API
dotnet run --configuration Release --no-launch-profile
```

### Step 8: Verify API is Running

You should see:
```
Now listening on: http://0.0.0.0:5000
```

Test in browser:
```
http://4.236.186.123:5000/swagger
```

---

## Complete Update Script

Save this as `update-vm.sh` and run it:

```bash
#!/bin/bash

# Update VM with Latest Code from Azure DevOps

echo "=== Updating TheJourney API on VM ==="

# Navigate to project
cd ~/The-Journey || exit 1

# Check if we're in a git repository
if [ ! -d .git ]; then
    echo "Error: Not a git repository. Cloning..."
    cd ~
    git clone https://dev.azure.com/journey-devops/JourneyApp/_git/The-Journey.git
    cd The-Journey
fi

# Update remote to Azure DevOps (if needed)
CURRENT_REMOTE=$(git remote get-url origin)
if [[ ! "$CURRENT_REMOTE" == *"dev.azure.com"* ]]; then
    echo "Updating remote to Azure DevOps..."
    git remote set-url origin https://dev.azure.com/journey-devops/JourneyApp/_git/The-Journey.git
fi

# Fetch and pull latest code
echo "Fetching latest code..."
git fetch origin

echo "Pulling latest changes..."
git pull origin main

# Show latest commits
echo ""
echo "Latest commits:"
git log --oneline -5

echo ""
echo "=== Update complete! ==="
echo "Next steps:"
echo "1. Stop current API (if running)"
echo "2. cd TheJourney.Api"
echo "3. Set environment variables"
echo "4. dotnet run --configuration Release --no-launch-profile"
```

**To use the script:**
```bash
# Make it executable
chmod +x update-vm.sh

# Run it
./update-vm.sh
```

---

## If Repository Doesn't Exist on VM

If you need to clone fresh:

```bash
cd ~
git clone https://dev.azure.com/journey-devops/JourneyApp/_git/The-Journey.git
cd The-Journey
```

---

## Troubleshooting

### Error: "Permission denied (publickey)"

Make sure you're using the correct SSH key or password.

### Error: "fatal: not a git repository"

You're not in the project directory. Run:
```bash
cd ~/The-Journey
```

### Error: "Updates were rejected"

Someone else pushed changes. Pull first:
```bash
git pull origin main
```

### Error: "Merge conflict"

Resolve conflicts or reset:
```bash
# Option 1: Reset to match remote exactly (WARNING: loses local changes)
git fetch origin
git reset --hard origin/main

# Option 2: Stash local changes
git stash
git pull origin main
git stash pop
```

### API Not Starting After Update

1. Check environment variables are set:
```bash
echo $PG_HOST
echo $ASPNETCORE_URLS
```

2. Check for build errors:
```bash
cd TheJourney.Api
dotnet build --configuration Release
```

3. Check logs:
```bash
# If using systemd
sudo systemctl status thejourney-api
sudo journalctl -u thejourney-api -n 50
```

---

## Quick One-Liner Update

```bash
cd ~/The-Journey && git remote set-url origin https://dev.azure.com/journey-devops/JourneyApp/_git/The-Journey.git && git pull origin main
```

---

## After Update Checklist

- [ ] Code pulled successfully
- [ ] Latest commit shows in `git log`
- [ ] API builds without errors (`dotnet build`)
- [ ] Environment variables are set
- [ ] API starts and listens on `0.0.0.0:5000`
- [ ] Swagger UI accessible at `http://4.236.186.123:5000/swagger`
- [ ] Test an API endpoint to verify it's working

