#!/bin/bash

# TheJourney API - Background Startup Script
# This script sets environment variables and starts the API

# Add dotnet to PATH (if not already there)
export PATH="$PATH:/usr/share/dotnet:/home/thejourneyapi/.dotnet"

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
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR" || {
    echo "Error: Could not change to script directory"
    exit 1
}

# Verify dotnet is available
if ! command -v dotnet &> /dev/null; then
    echo "Error: dotnet command not found. Please ensure .NET SDK is installed and in PATH."
    echo "Current PATH: $PATH"
    exit 1
fi

# Verify we're in the right directory
if [ ! -f "TheJourney.Api.csproj" ]; then
    echo "Error: TheJourney.Api.csproj not found. Current directory: $(pwd)"
    exit 1
fi

echo "Starting TheJourney API..."
echo "Working directory: $(pwd)"
echo "Dotnet version: $(dotnet --version)"

# Run the API (this will keep running)
dotnet run --configuration Release --no-launch-profile

