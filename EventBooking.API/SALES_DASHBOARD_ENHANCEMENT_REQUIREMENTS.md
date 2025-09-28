# Sales Dashboard Enhancement Requirements
## Hybrid Revenue Analytics Implementation

### 📋 Overview
Enhance the existing "Analytics & Charts" tab by adding four new revenue analysis tabs that combine data from both the EventBooking database and Stripe API. This hybrid approach provides comprehensive financial insights for event organizers.

---

## 🎯 Implementation Location
- **Parent Tab**: "Analytics & Charts" 
- **Position**: Insert new tab navigation **after** the Daily Sales Trends Graph/Chart
- **Placement**: **Before** the existing "Ticket Types Breakdown" panel

---

## 📊 Tab Structure & Requirements

### **Tab 1: "Tickets Summary"** 
**Data Source**: EventBooking Database (All ticket sources)
**Purpose**: Complete operational ticket status overview

#### Data Includes:
- ✅ Tickets sold via Stripe (paid to KiwiLanka)
- ✅ Tickets issued by organizer (both paid and unpaid to organizer)
- ✅ Total capacity per ticket type

#### Display Format:
```
| Ticket Type    | Sold      | Available | Total | Utilization |
|---------------|-----------|-----------|-------|-------------|
| Front         | 30 (30%)  | 70 (70%)  | 100   | 30%         |
| Upper Front   | 20 (20%)  | 80 (80%)  | 100   | 20%         |
| Back          | 45 (90%)  | 5 (10%)   | 50    | 90%         |
| **Total**     | 95 (47%)  | 105 (53%) | 200   | 47%         |
```

#### Database Queries:
```sql
-- Get ticket type capacity
SELECT tt.Name, tt.Price, 
       COUNT(s.Id) as TotalCapacity,
       COUNT(CASE WHEN s.IsReserved = 1 THEN 1 END) as SoldTickets
FROM TicketTypes tt
LEFT JOIN Seats s ON s.TicketTypeId = tt.Id
WHERE tt.EventId = @EventId
GROUP BY tt.Id, tt.Name, tt.Price
```

---

### **Tab 2: "Paid to KiwiLanka"** 
**Data Source**: Stripe API Only
**Purpose**: Revenue processed through KiwiLanka platform

#### Integration Points:
- Use existing `Analyze-EventTicketTypes.ps1` logic
- Convert PowerShell logic to C# API endpoint
- Match `stripe.metadata.eventTitle` with `Events.Title`

#### Display Format:
```
💳 STRIPE REVENUE ANALYSIS

140 NZD tickets:
  Revenue: $2,240 NZD
  Quantity: 16 tickets  
  Seat combinations: 7
  Transactions: 12

120 NZD tickets:
  Revenue: $3,240 NZD
  Quantity: 27 tickets
  Seat combinations: 9  
  Transactions: 18

100 NZD tickets:
  Revenue: $3,700 NZD
  Quantity: 37 tickets
  Seat combinations: 17
  Transactions: 25

TOTALS:
  Total Stripe Revenue: $9,180 NZD
  Total Tickets via Stripe: 80
  Total Stripe Transactions: 55
  Average Ticket Price: $114.75 NZD
```

#### New API Endpoint:
```csharp
[HttpGet("events/{eventId}/stripe-revenue")]
public async Task<ActionResult<StripeRevenueAnalysisDTO>> GetStripeRevenue(int eventId)
```

---

### **Tab 3: "Paid to Organizer"**
**Data Source**: EventBooking Database (OrganizerDirect payments)
**Purpose**: Track direct organizer sales and payment status

#### Features:
- List all organizer-issued tickets
- Payment status management (Paid/Unpaid checkboxes)
- Revenue calculation for organizer direct sales

#### Display Format:
```
🏢 ORGANIZER DIRECT SALES

Front ($140):
  Issued: 10 tickets
  Paid: 7 tickets ✅
  Unpaid: 3 tickets ❌
  Total Revenue: $980 NZD
  
Upper Front ($120):
  Issued: 15 tickets  
  Paid: 12 tickets ✅
  Unpaid: 3 tickets ❌
  Total Revenue: $1,440 NZD

Back ($80):
  Issued: 25 tickets
  Paid: 20 tickets ✅
  Unpaid: 5 tickets ❌
  Total Revenue: $1,600 NZD

ORGANIZER TOTALS:
  Total Issued: 50 tickets
  Total Paid: 39 tickets (78%)
  Total Unpaid: 11 tickets (22%)
  Total Organizer Revenue: $4,020 NZD
```

#### Database Schema Enhancement:
```sql
-- Add payment tracking to BookingLineItems or create new table
ALTER TABLE BookingLineItems 
ADD OrganizerPaidStatus BIT DEFAULT 0;

-- Or create dedicated table:
CREATE TABLE OrganizerTicketPayments (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    BookingLineItemId INT FOREIGN KEY,
    IsPaidToOrganizer BIT DEFAULT 0,
    PaidDate DATETIME2 NULL,
    Notes NVARCHAR(500) NULL,
    UpdatedAt DATETIME2 DEFAULT GETDATE()
);
```

---

### **Tab 4: "Revenue Summary"**
**Data Source**: Hybrid (Combination of Tab 2 + Tab 3)
**Purpose**: Complete financial overview and reconciliation

#### Four Summary Panels:

##### **Panel 1: Total Event Capacity Value**
```
💰 MAXIMUM POSSIBLE REVENUE (If 100% Sold Out)

Front tickets: $140 × 100 = $14,000
Upper Front: $120 × 100 = $12,000  
Back: $80 × 50 = $4,000
----------------------------------
Total Possible Revenue: $30,000
```

##### **Panel 2: KiwiLanka Revenue (from Tab 2)**
```
💳 REVENUE VIA KIWILANKAN PLATFORM

Front tickets: $140 × 16 = $2,240
Upper Front: $120 × 27 = $3,240
Back: $80 × 37 = $2,960
----------------------------------
Total KiwiLanka Revenue: $8,440
```

##### **Panel 3: Organizer Revenue (from Tab 3)**  
```
🏢 REVENUE VIA ORGANIZER DIRECT

Front tickets: $140 × 7 = $980
Upper Front: $120 × 12 = $1,440
Back: $80 × 20 = $1,600
----------------------------------
Total Organizer Revenue: $4,020
```

##### **Panel 4: Combined Summary**
```
📊 TOTAL EVENT REVENUE SUMMARY

Revenue via KiwiLanka: $8,440 (67.7%)
Revenue via Organizer: $4,020 (32.3%) 
----------------------------------
Total Revenue Generated: $12,460
Remaining Capacity Value: $17,540
Overall Event Utilization: 41.5%

💡 Revenue Split:
- Platform Commission (Est.): $422 (5%)
- Stripe Fees (Est.): $241 (2.9%) 
- Net to Organizer: $11,797 (94.7%)
```

---

## 🛠 Technical Implementation Plan

### **New API Endpoints Required:**

```csharp
// Tab 1: Tickets Summary
[HttpGet("events/{eventId}/ticket-capacity")]
public async Task<ActionResult<List<TicketCapacityDTO>>> GetTicketCapacity(int eventId)

// Tab 2: Stripe Revenue  
[HttpGet("events/{eventId}/stripe-revenue")]
public async Task<ActionResult<StripeRevenueAnalysisDTO>> GetStripeRevenue(int eventId)

// Tab 3: Organizer Revenue
[HttpGet("events/{eventId}/organizer-revenue")] 
public async Task<ActionResult<OrganizerRevenueDTO>> GetOrganizerRevenue(int eventId)

// Tab 4: Revenue Summary (combines Tab 2 + 3)
[HttpGet("events/{eventId}/revenue-summary")]
public async Task<ActionResult<RevenueSummaryDTO>> GetRevenueSummary(int eventId)
```

### **New DTOs Required:**

```csharp
public class TicketCapacityDTO {
    public string TicketTypeName { get; set; }
    public decimal TicketPrice { get; set; }
    public int SoldTickets { get; set; }
    public int AvailableTickets { get; set; }
    public int TotalCapacity { get; set; }
    public decimal UtilizationPercentage { get; set; }
}

public class StripeRevenueAnalysisDTO {
    public List<StripePricingTierDTO> PricingTiers { get; set; }
    public decimal TotalStripeRevenue { get; set; }
    public int TotalStripeTickets { get; set; }
    public int TotalStripeTransactions { get; set; }
    public decimal AverageTicketPrice { get; set; }
}

public class OrganizerRevenueDTO {
    public List<OrganizerTicketTypeDTO> TicketTypes { get; set; }
    public int TotalIssued { get; set; }
    public int TotalPaid { get; set; }
    public int TotalUnpaid { get; set; }
    public decimal TotalOrganizerRevenue { get; set; }
}

public class RevenueSummaryDTO {
    public decimal MaxPossibleRevenue { get; set; }
    public decimal KiwiLankaRevenue { get; set; }
    public decimal OrganizerRevenue { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal RemainingCapacityValue { get; set; }
    public decimal UtilizationPercentage { get; set; }
}
```

### **Frontend Component Structure:**

```tsx
// Inside Analytics Tab, after Daily Chart
<div className="revenue-analysis-section">
  <div className="revenue-tabs">
    <TabNavigation>
      <Tab value="tickets-summary">📊 Tickets Summary</Tab>
      <Tab value="kiwilankan-revenue">💳 Paid to KiwiLanka</Tab>
      <Tab value="organizer-revenue">🏢 Paid to Organizer</Tab>  
      <Tab value="revenue-summary">📈 Revenue Summary</Tab>
    </TabNavigation>
    
    <TabContent>
      <TicketsSummaryTab />      // Database table view
      <KiwiLankaRevenueTab />    // Stripe pricing tiers  
      <OrganizerRevenueTab />    // Organizer direct sales
      <RevenueSummaryTab />      // Four summary panels
    </TabContent>
  </div>
</div>
```

---

## 🔄 Integration Requirements

### **Stripe API Integration:**
- Convert existing PowerShell script logic to C# 
- Use same Stripe credentials from `appsettings.Production.json`
- Match events by `stripe.metadata.eventTitle` with `Events.Title`
- Handle Stripe API rate limits and errors gracefully

### **Database Integration:**
- Extend existing queries to include organizer payment tracking
- Add indexes for performance on large event datasets  
- Implement proper authorization (organizer can only see their events)

### **UI/UX Requirements:**
- Consistent styling with existing dashboard theme
- Mobile-responsive design for all four tabs
- Loading states for API calls (especially Stripe)
- Error handling with user-friendly messages
- Auto-refresh capability (30-second intervals)

---

## 📅 Implementation Priority

### **Phase 1** (Prerequisite): 
- ✅ Implement Organizer ticket payment management system
- ✅ Add payment status tracking to database schema
- ✅ Create organizer ticket management UI

### **Phase 2** (Core Dashboard):
- 🔲 Create new API endpoints (Tabs 1-4)
- 🔲 Integrate Stripe API in C# backend
- 🔲 Build frontend tab components
- 🔲 Implement responsive design

### **Phase 3** (Polish & Testing):
- 🔲 Add error handling and loading states
- 🔲 Performance optimization
- 🔲 Cross-browser testing
- 🔲 User acceptance testing with organizers

---

## 💡 Example Event Scenarios

### **Scenario 1: Mixed Sales Event**
- **Event**: "Cultural Night 2025"
- **Capacity**: 300 tickets across 3 tiers
- **Sales**: 60% via KiwiLanka, 40% via organizer direct
- **Status**: Event in progress, some organizer payments pending

### **Scenario 2: Sold Out Event**
- **Event**: "New Year Concert"  
- **Capacity**: 500 tickets, 100% sold
- **Sales**: 80% via KiwiLanka, 20% via organizer
- **Status**: All organizer payments received

### **Scenario 3: Low Sales Event**
- **Event**: "Community Workshop"
- **Capacity**: 100 tickets, 30% sold  
- **Sales**: 20% via KiwiLanka, 10% via organizer
- **Status**: Many organizer payments still pending

---

## ✅ Success Criteria
- [ ] Organizers can view complete revenue breakdown across both platforms
- [ ] Real-time synchronization between database and Stripe data  
- [ ] Clear visibility into payment status for organizer-issued tickets
- [ ] Financial reconciliation between projected and actual revenue
- [ ] Mobile-friendly interface for on-the-go event management
- [ ] Performance under load (events with 1000+ transactions)

---

*This document should be updated as requirements evolve during implementation.*
