# Stripe Event Ticket Type Analyzer
# This script analyzes Stripe checkout sessions to break down event revenue by ticket types
# It uses database integration to automatically determine the correct date range for Stripe API calls
# Created: September 2025
# Usage: .\Analyze-EventTicketTypes.ps1 -EventName "Event Name" [-UseDateFilter] [-StripeSecretKey "sk_live_..."]

param(
    [Parameter(Mandatory=$true)]
    [string]$EventName,
    
    [Parameter(Mandatory=$false)]
    [string]$StripeSecretKey = "",
    
    [Parameter(Mandatory=$false)]
    [switch]$IncludeProcessingFees,
    
    [Parameter(Mandatory=$false)]
    [switch]$UseDateFilter = $true,
    
    [Parameter(Mandatory=$false)]
    [int]$MaxSessions = 1000,
    
    [Parameter(Mandatory=$false)]
    [switch]$ExactMatch = $true,
    
    [Parameter(Mandatory=$false)]
    [string]$ExportPath = "",
    
    [Parameter(Mandatory=$false)]
    [switch]$ExportCSV
)

# Load configuration from appsettings
$configPath = Join-Path $PSScriptRoot "..\appsettings.Production.json"
if (-not (Test-Path $configPath)) {
    Write-Error "Config file not found at $configPath"
    exit 1
}

$config = Get-Content $configPath | ConvertFrom-Json

# Load Stripe key from config if not provided
if ([string]::IsNullOrEmpty($StripeSecretKey)) {
    $StripeSecretKey = $config.Stripe.SecretKey
    Write-Host "Loaded Stripe key from appsettings.Production.json" -ForegroundColor Green
}

# Load database connection string
$connectionString = $config.ConnectionStrings.DefaultConnection

# Setup headers
$headers = @{
    'Authorization' = "Bearer $StripeSecretKey"
}

Write-Host "=== STRIPE EVENT TICKET ANALYZER ===" -ForegroundColor Yellow
Write-Host "Event: $EventName" -ForegroundColor Cyan
Write-Host "Analysis Date: $(Get-Date)" -ForegroundColor Cyan
Write-Host ""

# Get event booking date range from database
$eventDateInfo = $null
$startDate = $null

if ($UseDateFilter) {
    Write-Host "1. Getting event booking date range from database..." -ForegroundColor Yellow
    
    try {
        # Parse connection string components
        $connParams = @{}
        $connectionString -split ';' | ForEach-Object {
            if ($_ -match '(.+)=(.+)') {
                $connParams[$matches[1]] = $matches[2]
            }
        }
        
        $server = $connParams['Server']
        $database = $connParams['Initial Catalog']
        $userId = $connParams['User ID']
        $password = $connParams['Password']
        
        # Query to get event booking dates
        $query = if ($ExactMatch) {
            @"
SELECT e.Id, e.Title, MIN(b.CreatedAt) as FirstBooking, MAX(b.CreatedAt) as LastBooking, COUNT(b.Id) as BookingCount
FROM Events e 
LEFT JOIN Bookings b ON e.Id = b.EventId 
WHERE e.Title = '$EventName' AND b.Id IS NOT NULL
GROUP BY e.Id, e.Title
"@
        } else {
            @"
SELECT e.Id, e.Title, MIN(b.CreatedAt) as FirstBooking, MAX(b.CreatedAt) as LastBooking, COUNT(b.Id) as BookingCount
FROM Events e 
LEFT JOIN Bookings b ON e.Id = b.EventId 
WHERE e.Title LIKE '%$EventName%' AND b.Id IS NOT NULL
GROUP BY e.Id, e.Title
"@
        }
        
        $result = sqlcmd -S $server -d $database -U $userId -P $password -Q $query -h -1 -W
        
        if ($result -and $result.Count -gt 0) {
            # Parse the result (assuming first non-header line contains the data)
            $dataLine = ($result | Where-Object { $_ -notmatch '^-+$' -and $_ -notmatch '^\s*$' -and $_ -notmatch 'Id\s+Title' })[0]
            
            if ($dataLine) {
                # Extract the first booking date (this is a simplified parser)
                if ($dataLine -match '(\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2}\.\d+)') {
                    $startDate = [DateTime]::Parse($matches[1])
                    Write-Host "   Found event booking period starting: $startDate" -ForegroundColor Green
                }
            }
        }
        
        if (-not $startDate) {
            Write-Warning "   Could not find booking dates in database for '$EventName'"
            Write-Host "   Falling back to recent sessions method..." -ForegroundColor Yellow
        }
        
    } catch {
        Write-Warning "   Database query failed: $($_.Exception.Message)"
        Write-Host "   Falling back to recent sessions method..." -ForegroundColor Yellow
    }
}

# Fetch checkout sessions (with date filter if available)
$sessionFetchMessage = if ($startDate) {
    "2. Fetching checkout sessions from Stripe (from $($startDate.ToString('yyyy-MM-dd')))..."
} else {
    "2. Fetching checkout sessions from Stripe (recent sessions)..."
}
Write-Host $sessionFetchMessage -ForegroundColor Yellow
$allSessions = @()
$hasMore = $true
$startingAfter = $null

try {
    # Build the base URL with date filter if available
    $baseUrl = "https://api.stripe.com/v1/checkout/sessions?limit=100"
    if ($startDate) {
        # Convert to Unix timestamp for Stripe API
        $unixTimestamp = [int][double]::Parse((Get-Date $startDate -UFormat %s))
        $baseUrl += "&created[gte]=$unixTimestamp"
        Write-Host "   Using date filter: sessions from $($startDate.ToString('yyyy-MM-dd HH:mm:ss'))" -ForegroundColor Cyan
    } else {
        Write-Host "   Using session limit: maximum $MaxSessions sessions" -ForegroundColor Cyan
    }
    
    while ($hasMore) {
        $url = $baseUrl
        if ($startingAfter) {
            $url += "&starting_after=$startingAfter"
        }
        
        $response = Invoke-RestMethod -Uri $url -Headers $headers -Method GET
        $allSessions += $response.data
        
        $hasMore = $response.has_more
        if ($hasMore -and $response.data.Count -gt 0) {
            $startingAfter = $response.data[-1].id
            
            # Only apply session limit if not using date filter
            if (-not $startDate -and $allSessions.Count -ge $MaxSessions) {
                Write-Host "   Reached session limit of $MaxSessions" -ForegroundColor Yellow
                break
            }
        } else {
            $hasMore = $false
        }
    }
    
    Write-Host "   Total sessions fetched: $($allSessions.Count)" -ForegroundColor Green
} catch {
    Write-Error "Failed to fetch Stripe sessions: $($_.Exception.Message)"
    exit 1
}

# Filter for the specified event
Write-Host ""
Write-Host "3. Filtering for '$EventName' events..." -ForegroundColor Yellow

$eventSessions = if ($ExactMatch) {
    $allSessions | Where-Object { 
        $_.metadata -and 
        $_.metadata.eventTitle -and 
        $_.metadata.eventTitle -eq $EventName -and
        $_.payment_status -eq "paid"
    }
} else {
    $allSessions | Where-Object { 
        $_.metadata -and 
        $_.metadata.eventTitle -and 
        $_.metadata.eventTitle -like "*$EventName*" -and
        $_.payment_status -eq "paid"
    }
}

Write-Host "   Event sessions found: $($eventSessions.Count)" -ForegroundColor Green

if ($eventSessions.Count -eq 0) {
    Write-Warning "No paid sessions found for event '$EventName'"
    Write-Host ""
    Write-Host "Available events in recent sessions:" -ForegroundColor Yellow
    $allSessions | Where-Object { $_.metadata -and $_.metadata.eventTitle } | 
                   Group-Object { $_.metadata.eventTitle } | 
                   Select-Object -First 10 | 
                   ForEach-Object { Write-Host "  - $($_.Name) ($($_.Count) sessions)" }
    exit 0
}

# Load fee configuration from appsettings
$processingFeeConfig = $config.ProcessingFee
$afterPayFeeConfig = $config.AfterPayFee

# Analyze ticket types
Write-Host ""
Write-Host "4. Analyzing ticket types..." -ForegroundColor Yellow

$ticketTypes = @{}
$totalStripeRevenue = 0
$totalStripeFees = 0
$totalPlatformFees = 0
$totalPlatformAfterPayFees = 0
$afterPayTransactions = 0
$regularTransactions = 0

# Arrays to store detailed transaction data for CSV export
$detailedTransactions = @()
$ticketTypeBreakdown = @()

foreach ($session in $eventSessions) {
    $totalStripeRevenue += $session.amount_total / 100
    
    # Calculate fees for this session
    $sessionTicketTotal = 0
    $isAfterPay = $session.metadata.useAfterPay -eq "True"
    
    # Store session details for CSV export
    $sessionDetail = @{
        SessionId = $session.id
        CustomerName = "$($session.metadata.customerFirstName) $($session.metadata.customerLastName)"
        CustomerEmail = $session.customer_email
        PaymentStatus = $session.payment_status
        PaymentMethod = if ($isAfterPay) { "AfterPay" } else { "Stripe" }
        TotalAmount = $session.amount_total / 100
        CreatedDate = ([DateTime]'1970-01-01Z').AddSeconds($session.created).ToString("yyyy-MM-dd HH:mm:ss")
        EventTitle = $session.metadata.eventTitle
        TicketDetails = @()
    }
    
    if ($session.metadata.ticketDetails) {
        try {
            $ticketDetails = $session.metadata.ticketDetails | ConvertFrom-Json
            
            foreach ($ticket in $ticketDetails) {
                $type = $ticket.Type
                $quantity = [int]$ticket.Quantity
                $unitPrice = [decimal]$ticket.UnitPrice
                $revenue = $quantity * $unitPrice
                $sessionTicketTotal += $revenue
                
                # Add to session detail for CSV
                $sessionDetail.TicketDetails += @{
                    TicketType = $type
                    Quantity = $quantity
                    UnitPrice = $unitPrice
                    TotalPrice = $revenue
                }
                
                if (-not $ticketTypes.ContainsKey($type)) {
                    $ticketTypes[$type] = @{
                        Revenue = 0
                        Quantity = 0
                        Transactions = 0
                        UnitPrices = @()
                        CustomerEmails = @()
                    }
                }
                
                $ticketTypes[$type].Revenue += $revenue
                $ticketTypes[$type].Quantity += $quantity
                $ticketTypes[$type].Transactions += 1
                $ticketTypes[$type].UnitPrices += $unitPrice
                $ticketTypes[$type].CustomerEmails += $session.metadata.customerFirstName + " " + $session.metadata.customerLastName
            }
            
            # Calculate fees for this session
            if ($processingFeeConfig -and $afterPayFeeConfig) {
                if ($isAfterPay) {
                    $afterPayTransactions++
                    # AfterPay: 6% + $0.30 (Stripe portion)
                    $sessionStripeFee = ($sessionTicketTotal * ($afterPayFeeConfig.Percentage / 100)) + $afterPayFeeConfig.FixedAmount
                    $sessionPlatformFee = 0  # No regular platform fee for AfterPay
                    
                    # Platform AfterPay fee: Additional platform charge on top of AfterPay
                    # This is typically the difference between what customer pays and what goes to Stripe
                    $sessionPlatformAfterPayFee = ($session.amount_total / 100) - $sessionTicketTotal - $sessionStripeFee
                    if ($sessionPlatformAfterPayFee -lt 0) { $sessionPlatformAfterPayFee = 0 }
                    
                    $totalPlatformAfterPayFees += $sessionPlatformAfterPayFee
                } else {
                    $regularTransactions++
                    # Regular: 2.85% + $0.30 (Stripe) + Platform fee
                    $sessionStripeFee = ($sessionTicketTotal * 0.0285) + 0.30
                    
                    # Platform fee: 2.5% with $10 max
                    $platformFeeAmount = $sessionTicketTotal * ($processingFeeConfig.Percentage / 100)
                    $sessionPlatformFee = [Math]::Min($platformFeeAmount, $processingFeeConfig.MaxFee)
                }
                
                $totalStripeFees += $sessionStripeFee
                $totalPlatformFees += $sessionPlatformFee
                
                # Add fee details to session
                $sessionDetail.StripeFee = [Math]::Round($sessionStripeFee, 2)
                $sessionDetail.PlatformFee = [Math]::Round($sessionPlatformFee, 2)
                $sessionDetail.PlatformAfterPayFee = [Math]::Round($sessionPlatformAfterPayFee, 2)
            }
            
            # Add session to detailed transactions
            $detailedTransactions += $sessionDetail
            
        } catch {
            Write-Warning "Error parsing session $($session.id): $($_.Exception.Message)"
        }
    }
}

# Calculate totals
$totalTicketRevenue = ($ticketTypes.Values | ForEach-Object { $_.Revenue } | Measure-Object -Sum).Sum
$totalTickets = ($ticketTypes.Values | ForEach-Object { $_.Quantity } | Measure-Object -Sum).Sum
$totalTransactions = $eventSessions.Count
$totalPlatformRevenue = $totalPlatformFees + $totalPlatformAfterPayFees
$actualTotalFees = $totalStripeRevenue - $totalTicketRevenue

# Prepare ticket type breakdown for CSV
foreach ($entry in $ticketTypes.GetEnumerator()) {
    $type = $entry.Key
    $data = $entry.Value
    $avgPrice = [Math]::Round($data.Revenue / $data.Quantity, 2)
    
    $ticketTypeBreakdown += [PSCustomObject]@{
        TicketType = $type
        TotalRevenue = $data.Revenue
        TotalQuantity = $data.Quantity
        AveragePrice = $avgPrice
        TransactionCount = $data.Transactions
        RevenuePercentage = [Math]::Round(($data.Revenue / $totalTicketRevenue) * 100, 2)
        QuantityPercentage = [Math]::Round(($data.Quantity / $totalTickets) * 100, 2)
    }
}

# Display results
Write-Host ""
Write-Host "=== ANALYSIS RESULTS ===" -ForegroundColor Green
Write-Host ""
Write-Host "OVERALL SUMMARY:" -ForegroundColor Cyan
Write-Host "  Event Name: $EventName"
if ($startDate) {
    Write-Host "  Analysis Period: From $($startDate.ToString('yyyy-MM-dd')) (database-driven)" -ForegroundColor Cyan
} else {
    Write-Host "  Analysis Method: Recent $MaxSessions sessions (fallback mode)" -ForegroundColor Cyan
}
Write-Host "  Total Ticket Revenue: $totalTicketRevenue NZD (excluding processing fees)"
if ($IncludeProcessingFees) {
    Write-Host "  --- PROCESSING FEES BREAKDOWN ---" -ForegroundColor Yellow
    Write-Host "  Stripe Processing Fees: $([Math]::Round($totalStripeFees, 2)) NZD"
    Write-Host "  Platform Fees (Regular): $([Math]::Round($totalPlatformFees, 2)) NZD"
    if ($totalPlatformAfterPayFees -gt 0) {
        Write-Host "  Platform AfterPay Fees: $([Math]::Round($totalPlatformAfterPayFees, 2)) NZD"
    }
    Write-Host "  Platform Total Fees: $([Math]::Round($totalPlatformRevenue, 2)) NZD" -ForegroundColor Green
    Write-Host "  Total All Fees: $([Math]::Round($actualTotalFees, 2)) NZD"
    Write-Host "  --- PAYMENT METHOD BREAKDOWN ---" -ForegroundColor Yellow
    Write-Host "  Regular Payments: $regularTransactions transactions"
    Write-Host "  AfterPay Payments: $afterPayTransactions transactions"
    Write-Host "  Total Stripe Revenue: $([Math]::Round($totalStripeRevenue, 2)) NZD (including all fees)"
}
Write-Host "  Total Tickets Sold: $totalTickets"
Write-Host "  Total Transactions: $totalTransactions"
Write-Host "  Unique Ticket Types: $($ticketTypes.Count)"
Write-Host "  Average Ticket Price: $([Math]::Round($totalTicketRevenue / $totalTickets, 2)) NZD"
Write-Host ""

# Pricing tier analysis
Write-Host "PRICING TIER BREAKDOWN:" -ForegroundColor Green
Write-Host "======================"

$priceGroups = @{}
foreach ($type in $ticketTypes.Keys) {
    $data = $ticketTypes[$type]
    $avgPrice = [Math]::Round($data.Revenue / $data.Quantity, 0)
    
    if (-not $priceGroups.ContainsKey($avgPrice)) {
        $priceGroups[$avgPrice] = @{
            Revenue = 0
            Quantity = 0
            Groups = 0
        }
    }
    
    $priceGroups[$avgPrice].Revenue += $data.Revenue
    $priceGroups[$avgPrice].Quantity += $data.Quantity
    $priceGroups[$avgPrice].Groups += 1
}

$sortedPrices = $priceGroups.Keys | Sort-Object -Descending
foreach ($price in $sortedPrices) {
    $data = $priceGroups[$price]
    $revenuePercent = [Math]::Round(($data.Revenue / $totalTicketRevenue) * 100, 1)
    $quantityPercent = [Math]::Round(($data.Quantity / $totalTickets) * 100, 1)
    
    Write-Host ""
    Write-Host "$price NZD tickets:" -ForegroundColor White
    Write-Host "  Revenue: $($data.Revenue) NZD ($revenuePercent%)"
    Write-Host "  Quantity: $($data.Quantity) tickets ($quantityPercent%)"
    Write-Host "  Seat combinations: $($data.Groups)"
}

# Top performers
Write-Host ""
Write-Host "ALL REVENUE COMBINATIONS (HIGHEST TO LOWEST):" -ForegroundColor Magenta
Write-Host "=============================================="

$sortedByRevenue = $ticketTypes.GetEnumerator() | 
                   Sort-Object { $_.Value.Revenue } -Descending

$rank = 1
foreach ($entry in $sortedByRevenue) {
    $type = $entry.Key
    $data = $entry.Value
    $avgPrice = [Math]::Round($data.Revenue / $data.Quantity, 0)
    
    Write-Host ""
    Write-Host "#$rank - $type" -ForegroundColor White
    Write-Host "     Revenue: $($data.Revenue) NZD"
    Write-Host "     Tickets: $($data.Quantity) @ $avgPrice NZD each"
    Write-Host "     Transactions: $($data.Transactions)"
    
    $rank++
}

# Row analysis (if seat-based)
if ($ticketTypes.Keys | Where-Object { $_ -match 'Seat[s]?\s*\([A-Z]\d+' }) {
    Write-Host ""
    Write-Host "VENUE ROW ANALYSIS:" -ForegroundColor Yellow
    Write-Host "=================="
    
    $rowStats = @{}
    foreach ($type in $ticketTypes.Keys) {
        $data = $ticketTypes[$type]
        
        if ($type -match 'Seat[s]?\s*\(([A-Z])\d+') {
            $row = $matches[1]
            if (-not $rowStats.ContainsKey($row)) {
                $rowStats[$row] = @{ Revenue = 0; Quantity = 0 }
            }
            $rowStats[$row].Revenue += $data.Revenue
            $rowStats[$row].Quantity += $data.Quantity
        }
    }
    
    Write-Host ""
    Write-Host "Revenue by Row:" -ForegroundColor Cyan
    $sortedRows = $rowStats.GetEnumerator() | Sort-Object { $_.Value.Revenue } -Descending
    foreach ($rowEntry in $sortedRows) {
        $row = $rowEntry.Key
        $stats = $rowEntry.Value
        $avgPrice = [Math]::Round($stats.Revenue / $stats.Quantity, 0)
        Write-Host "  Row $row`: $($stats.Revenue) NZD ($($stats.Quantity) tickets @ avg $avgPrice NZD)"
    }
}

Write-Host ""
Write-Host "=== ANALYSIS COMPLETE ===" -ForegroundColor Green

# CSV Export functionality
if ($ExportCSV -or -not [string]::IsNullOrEmpty($ExportPath)) {
    Write-Host ""
    Write-Host "5. Exporting data to CSV files..." -ForegroundColor Yellow
    
    # Determine export directory
    $exportDir = if ([string]::IsNullOrEmpty($ExportPath)) {
        Join-Path $PSScriptRoot "Exports"
    } else {
        $ExportPath
    }
    
    # Create export directory if it doesn't exist
    if (-not (Test-Path $exportDir)) {
        New-Item -ItemType Directory -Path $exportDir -Force | Out-Null
    }
    
    # Generate timestamp for files
    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $eventNameSafe = $EventName -replace '[^\w\-_\.]', '_'
    
    try {
        # 1. Export ticket type breakdown
        $ticketTypeFile = Join-Path $exportDir "TicketTypes_${eventNameSafe}_${timestamp}.csv"
        $ticketTypeBreakdown | Sort-Object TotalRevenue -Descending | Export-Csv -Path $ticketTypeFile -NoTypeInformation
        Write-Host "   ✓ Ticket types exported to: $ticketTypeFile" -ForegroundColor Green
        
        # 2. Export detailed transactions with expanded ticket details
        $transactionDetails = @()
        foreach ($transaction in $detailedTransactions) {
            if ($transaction.TicketDetails.Count -gt 0) {
                foreach ($ticket in $transaction.TicketDetails) {
                    $transactionDetails += [PSCustomObject]@{
                        SessionId = $transaction.SessionId
                        CustomerName = $transaction.CustomerName
                        CustomerEmail = $transaction.CustomerEmail
                        PaymentStatus = $transaction.PaymentStatus
                        PaymentMethod = $transaction.PaymentMethod
                        CreatedDate = $transaction.CreatedDate
                        EventTitle = $transaction.EventTitle
                        TicketType = $ticket.TicketType
                        Quantity = $ticket.Quantity
                        UnitPrice = $ticket.UnitPrice
                        TicketTotalPrice = $ticket.TotalPrice
                        SessionTotalAmount = $transaction.TotalAmount
                        StripeFee = if ($transaction.StripeFee) { $transaction.StripeFee } else { 0 }
                        PlatformFee = if ($transaction.PlatformFee) { $transaction.PlatformFee } else { 0 }
                        PlatformAfterPayFee = if ($transaction.PlatformAfterPayFee) { $transaction.PlatformAfterPayFee } else { 0 }
                    }
                }
            } else {
                # Transaction without ticket details
                $transactionDetails += [PSCustomObject]@{
                    SessionId = $transaction.SessionId
                    CustomerName = $transaction.CustomerName
                    CustomerEmail = $transaction.CustomerEmail
                    PaymentStatus = $transaction.PaymentStatus
                    PaymentMethod = $transaction.PaymentMethod
                    CreatedDate = $transaction.CreatedDate
                    EventTitle = $transaction.EventTitle
                    TicketType = "Unknown"
                    Quantity = 0
                    UnitPrice = 0
                    TicketTotalPrice = 0
                    SessionTotalAmount = $transaction.TotalAmount
                    StripeFee = if ($transaction.StripeFee) { $transaction.StripeFee } else { 0 }
                    PlatformFee = if ($transaction.PlatformFee) { $transaction.PlatformFee } else { 0 }
                    PlatformAfterPayFee = if ($transaction.PlatformAfterPayFee) { $transaction.PlatformAfterPayFee } else { 0 }
                }
            }
        }
        
        $transactionsFile = Join-Path $exportDir "Transactions_${eventNameSafe}_${timestamp}.csv"
        $transactionDetails | Sort-Object CreatedDate | Export-Csv -Path $transactionsFile -NoTypeInformation
        Write-Host "   ✓ Detailed transactions exported to: $transactionsFile" -ForegroundColor Green
        
        # 3. Export summary report
        $summaryData = @(
            [PSCustomObject]@{ Metric = "Event Name"; Value = $EventName }
            [PSCustomObject]@{ Metric = "Total Ticket Revenue (NZD)"; Value = $totalTicketRevenue }
            [PSCustomObject]@{ Metric = "Total Tickets Sold"; Value = $totalTickets }
            [PSCustomObject]@{ Metric = "Total Transactions"; Value = $totalTransactions }
            [PSCustomObject]@{ Metric = "Regular Payment Transactions"; Value = $regularTransactions }
            [PSCustomObject]@{ Metric = "AfterPay Transactions"; Value = $afterPayTransactions }
            [PSCustomObject]@{ Metric = "Total Stripe Revenue (NZD)"; Value = [Math]::Round($totalStripeRevenue, 2) }
            [PSCustomObject]@{ Metric = "Stripe Processing Fees (NZD)"; Value = [Math]::Round($totalStripeFees, 2) }
            [PSCustomObject]@{ Metric = "Platform Fees Regular (NZD)"; Value = [Math]::Round($totalPlatformFees, 2) }
            [PSCustomObject]@{ Metric = "Platform AfterPay Fees (NZD)"; Value = [Math]::Round($totalPlatformAfterPayFees, 2) }
            [PSCustomObject]@{ Metric = "Average Ticket Price (NZD)"; Value = [Math]::Round($totalTicketRevenue / $totalTickets, 2) }
            [PSCustomObject]@{ Metric = "Unique Ticket Types"; Value = $ticketTypes.Count }
            [PSCustomObject]@{ Metric = "Analysis Date"; Value = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss") }
        )
        
        $summaryFile = Join-Path $exportDir "Summary_${eventNameSafe}_${timestamp}.csv"
        $summaryData | Export-Csv -Path $summaryFile -NoTypeInformation
        Write-Host "   ✓ Summary report exported to: $summaryFile" -ForegroundColor Green
        
        # 4. Export comparison template for database matching
        $comparisonTemplate = @()
        foreach ($entry in ($ticketTypeBreakdown | Sort-Object TicketType)) {
            $comparisonTemplate += [PSCustomObject]@{
                TicketType = $entry.TicketType
                Stripe_Quantity = $entry.TotalQuantity
                Stripe_Revenue = $entry.TotalRevenue
                Database_Quantity = ""  # Empty for manual filling
                Database_Revenue = ""   # Empty for manual filling
                Quantity_Difference = ""  # Empty for manual calculation
                Revenue_Difference = ""   # Empty for manual calculation
                Notes = ""              # Empty for manual notes
            }
        }
        
        $comparisonFile = Join-Path $exportDir "Comparison_Template_${eventNameSafe}_${timestamp}.csv"
        $comparisonTemplate | Export-Csv -Path $comparisonFile -NoTypeInformation
        Write-Host "   ✓ Database comparison template exported to: $comparisonFile" -ForegroundColor Green
        
        Write-Host ""
        Write-Host "   📁 All files exported to: $exportDir" -ForegroundColor Cyan
        Write-Host "   📊 Use the comparison template to validate against your database results" -ForegroundColor Cyan
        
    } catch {
        Write-Error "Failed to export CSV files: $($_.Exception.Message)"
    }
}

Write-Host "Script: $($MyInvocation.MyCommand.Name)" -ForegroundColor Gray
Write-Host "Generated: $(Get-Date)" -ForegroundColor Gray
