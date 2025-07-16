# Test script for the fixed seat reservation API
# This script tests the seat reservation endpoint that was failing with 500 errors

$apiBaseUrl = "https://kiwilanka.co.nz/api"
$eventId = 6  # From your error logs
$sessionId = "test_session_$(Get-Date -Format 'yyyyMMdd_HHmmss')"

Write-Host "Testing Seat Reservation API Fix" -ForegroundColor Green
Write-Host "=================================" -ForegroundColor Green
Write-Host "API Base URL: $apiBaseUrl" -ForegroundColor Yellow
Write-Host "Event ID: $eventId" -ForegroundColor Yellow
Write-Host "Session ID: $sessionId" -ForegroundColor Yellow
Write-Host ""

# Test 1: Get event layout to ensure seats exist
Write-Host "Test 1: Getting event layout..." -ForegroundColor Cyan
try {
    $layoutResponse = Invoke-RestMethod -Uri "$apiBaseUrl/seats/event/$eventId/layout" -Method GET
    Write-Host "✅ Layout request successful" -ForegroundColor Green
    Write-Host "   Event Mode: $($layoutResponse.mode)" -ForegroundColor White
    Write-Host "   Seats Count: $($layoutResponse.seats.Count)" -ForegroundColor White
    
    if ($layoutResponse.seats.Count -gt 0) {
        $testSeat = $layoutResponse.seats | Where-Object { $_.status -eq 0 } | Select-Object -First 1
        if ($testSeat) {
            Write-Host "   Test seat found: $($testSeat.seatNumber) (ID: $($testSeat.id))" -ForegroundColor White
        } else {
            Write-Host "   ⚠️ No available seats found for testing" -ForegroundColor Yellow
            $testSeat = $layoutResponse.seats | Select-Object -First 1
            Write-Host "   Using first seat: $($testSeat.seatNumber) (ID: $($testSeat.id))" -ForegroundColor White
        }
    } else {
        Write-Host "❌ No seats found in layout" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "❌ Layout request failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""

# Test 2: Test seat reservation (the previously failing endpoint)
Write-Host "Test 2: Testing seat reservation..." -ForegroundColor Cyan
$reservePayload = @{
    EventId = $eventId
    SeatId = $testSeat.id
    SessionId = $sessionId
    Row = $testSeat.row
    Number = $testSeat.number
} | ConvertTo-Json

try {
    $reserveResponse = Invoke-RestMethod -Uri "$apiBaseUrl/seats/reserve" -Method POST -Body $reservePayload -ContentType "application/json"
    Write-Host "✅ Seat reservation successful!" -ForegroundColor Green
    Write-Host "   Message: $($reserveResponse.message)" -ForegroundColor White
    Write-Host "   Seat: $($reserveResponse.seatNumber)" -ForegroundColor White
    Write-Host "   Price: $($reserveResponse.price)" -ForegroundColor White
    Write-Host "   Reserved Until: $($reserveResponse.reservedUntil)" -ForegroundColor White
} catch {
    Write-Host "❌ Seat reservation failed: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "   Response: $responseBody" -ForegroundColor Red
    }
    exit 1
}

Write-Host ""

# Test 3: Test getting reservations by session
Write-Host "Test 3: Getting reservations by session..." -ForegroundColor Cyan
try {
    $reservationsResponse = Invoke-RestMethod -Uri "$apiBaseUrl/seats/reservations/$eventId/$sessionId" -Method GET
    Write-Host "✅ Get reservations successful!" -ForegroundColor Green
    Write-Host "   Reservations count: $($reservationsResponse.Count)" -ForegroundColor White
    if ($reservationsResponse.Count -gt 0) {
        foreach ($reservation in $reservationsResponse) {
            Write-Host "   Reserved: $($reservation.seatNumber) - $($reservation.price)" -ForegroundColor White
        }
    }
} catch {
    Write-Host "⚠️ Get reservations failed (this might be expected for new sessions): $($_.Exception.Message)" -ForegroundColor Yellow
}

Write-Host ""

# Test 4: Test releasing the seat
Write-Host "Test 4: Testing seat release..." -ForegroundColor Cyan
$releasePayload = @{
    SeatId = $testSeat.id
    SessionId = $sessionId
} | ConvertTo-Json

try {
    $releaseResponse = Invoke-RestMethod -Uri "$apiBaseUrl/seats/release" -Method POST -Body $releasePayload -ContentType "application/json"
    Write-Host "✅ Seat release successful!" -ForegroundColor Green
    Write-Host "   Message: $($releaseResponse.message)" -ForegroundColor White
} catch {
    Write-Host "❌ Seat release failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "🎉 API Fix Testing Complete!" -ForegroundColor Green
Write-Host "The seat reservation endpoint is now working properly." -ForegroundColor Green
