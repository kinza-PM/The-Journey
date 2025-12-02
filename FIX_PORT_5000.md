# Fix Port 5000 Permission Denied Error

## Problem
```
System.Net.Sockets.SocketException (13): Permission denied
```

This happens when:
1. Port 5000 is already in use by another process
2. There's a permission issue binding to the port

## Solution 1: Check if Port 5000 is in Use

```bash
# Check what's using port 5000
sudo lsof -i :5000

# Or use netstat
sudo netstat -tulpn | grep :5000

# Or use ss
sudo ss -tulpn | grep :5000
```

## Solution 2: Kill Process Using Port 5000

If something is using port 5000:

```bash
# Find the process ID (PID)
sudo lsof -i :5000

# Kill the process (replace PID with actual process ID)
sudo kill -9 <PID>

# Or kill all dotnet processes
pkill -f dotnet
```

## Solution 3: Use a Different Port

If you can't free port 5000, use a different port:

```bash
# Use port 5001 instead
export ASPNETCORE_URLS="http://0.0.0.0:5001"

# Then run
dotnet run --configuration Release --no-launch-profile
```

**Don't forget to:**
- Update Azure VM networking to allow port 5001
- Update API_BASE_URL environment variable
- Access Swagger at `http://4.236.186.123:5001/swagger`

## Solution 4: Use Port 8080 (Common Alternative)

```bash
export ASPNETCORE_URLS="http://0.0.0.0:8080"
dotnet run --configuration Release --no-launch-profile
```

## Quick Fix Commands

```bash
# Check and kill any process on port 5000
sudo lsof -i :5000 | grep LISTEN | awk '{print $2}' | xargs sudo kill -9

# Wait a moment, then try again
dotnet run --configuration Release --no-launch-profile
```

