# Test login endpoint
$body = @{
    email = "admin@thejourney.com"
    password = "Admin@123Secure"
} | ConvertTo-Json

Write-Host "Testing login endpoint..."
Write-Host "POST http://localhost:5097/api/auth/login"
Write-Host "Body: $body"
Write-Host ""

try {
    $response = Invoke-RestMethod -Uri "http://localhost:5097/api/auth/login" -Method Post -Body $body -ContentType "application/json"
    Write-Host "✅ Login successful!" -ForegroundColor Green
    Write-Host "Token: $($response.token)" -ForegroundColor Cyan
    Write-Host "Message: $($response.message)" -ForegroundColor Green
} catch {
    Write-Host "❌ Login failed:" -ForegroundColor Red
    Write-Host $_.Exception.Message
    if ($_.ErrorDetails) {
        Write-Host $_.ErrorDetails.Message
    }
}

