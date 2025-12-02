# Restart TheJourney API
Write-Host "Stopping any running instances on port 5097..." -ForegroundColor Yellow

# Stop any process using port 5097
$connections = Get-NetTCPConnection -LocalPort 5097 -ErrorAction SilentlyContinue
if ($connections) {
    $connections | ForEach-Object {
        $process = Get-Process -Id $_.OwningProcess -ErrorAction SilentlyContinue
        if ($process) {
            Write-Host "Stopping process: $($process.ProcessName) (PID: $($process.Id))" -ForegroundColor Yellow
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        }
    }
    Start-Sleep -Seconds 2
}

Write-Host "Starting TheJourney API..." -ForegroundColor Green
Set-Location "$PSScriptRoot"

# Set environment variables if needed
$env:ASPNETCORE_ENVIRONMENT = "Development"

# Start the API
Write-Host "API will be available at: http://localhost:5097" -ForegroundColor Cyan
Write-Host "Swagger UI will be available at: http://localhost:5097/swagger" -ForegroundColor Cyan
Write-Host ""
Write-Host "Press Ctrl+C to stop the API" -ForegroundColor Yellow
Write-Host ""

dotnet run


