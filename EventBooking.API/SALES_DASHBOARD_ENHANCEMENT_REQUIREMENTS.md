# Sales Dashboard Enhancement Requirements
## Hybrid Revenue Analytics Implementation

### 📋 Overview
Enhance the existing "Analytics & Charts" tab in `OrganizerSalesDashboardEnhanced.tsx` by adding four new revenue analysis sub-tabs that combine data from both the EventBooking database and Stripe API. This hybrid approach provides comprehensive financial insights for event organizers.

**Current State**: Basic analytics dashboard exists with daily charts, ticket breakdown, and booking details.  
**Enhancement Goal**: Add sophisticated revenue analysis capabilities within the existing dashboard structure.

---

## 🎯 Implementation Location
- **Target File**: `event-booking-frontend/src/pages/OrganizerSalesDashboardEnhanced.tsx`
- **Parent Tab**: "📈 Analytics & Charts" (existing tab)
- **Position**: Insert new revenue analysis section **after** the existing Daily Sales Chart
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
-- ✅ ALREADY IMPLEMENTED - OrganizerTicketPayments table exists
-- Table created with full schema including:
-- - Id, BookingLineItemId, EventId, TicketTypeId, TicketPrice
-- - CustomerFirstName, CustomerLastName, CustomerEmail, CustomerMobile  
-- - SeatDetails, IsPaidToOrganizer, CreatedAt, UpdatedAt
-- - Associated service: OrganizerTicketPaymentService.cs

-- ✅ Migration scripts available in Scripts/migrate-organizer-tickets.sql
-- ✅ Interface: IOrganizerTicketPaymentService.cs
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

// Tab 2: Stripe Revenue (Convert from existing PowerShell script)
[HttpGet("events/{eventId}/stripe-revenue")]
public async Task<ActionResult<StripeRevenueAnalysisDTO>> GetStripeRevenue(int eventId)

// Tab 3: Organizer Revenue (Extend existing OrganizerTicketPaymentService)
[HttpGet("events/{eventId}/organizer-revenue")] 
public async Task<ActionResult<OrganizerRevenueDTO>> GetOrganizerRevenue(int eventId)

// Tab 4: Revenue Summary (combines Tab 2 + 3)
[HttpGet("events/{eventId}/revenue-summary")]
public async Task<ActionResult<RevenueSummaryDTO>> GetRevenueSummary(int eventId)
```

**Note**: Current dashboard uses `organizerSalesService.ts` - these new endpoints will extend the existing service.

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
// Update existing OrganizerSalesDashboardEnhanced.tsx
// Insert within the Analytics tab content, after daily chart

{activeTab === 'analytics' && (
  <div className="space-y-6">
    {/* Existing Daily Sales Chart */}
    <div className="bg-gray-50 rounded-lg p-2 sm:p-4">
      <Bar data={chartData} options={chartOptions} />
    </div>

    {/* NEW: Revenue Analysis Section */}
    <div className="revenue-analysis-section bg-white rounded-lg shadow-lg">
      <div className="p-6">
        <h3 className="text-lg font-semibold text-gray-900 mb-4">
          💰 Revenue Analysis
        </h3>
        
        {/* Revenue Sub-tabs */}
        <div className="revenue-tabs">
          <div className="border-b border-gray-200 mb-6">
            <nav className="-mb-px flex space-x-8">
              <button className="revenue-tab-btn">📊 Tickets Summary</button>
              <button className="revenue-tab-btn">💳 Paid to KiwiLanka</button>
              <button className="revenue-tab-btn">🏢 Paid to Organizer</button>
              <button className="revenue-tab-btn">📈 Revenue Summary</button>
            </nav>
          </div>
          
          <div className="revenue-tab-content">
            <TicketsSummaryTab eventId={selectedEventId} />
            <KiwiLankaRevenueTab eventId={selectedEventId} />
            <OrganizerRevenueTab eventId={selectedEventId} />
            <RevenueSummaryTab eventId={selectedEventId} />
          </div>
        </div>
      </div>
    </div>

    {/* Existing Ticket Types Breakdown */}
    <div className="bg-gray-50 rounded-lg p-6">
      <h4 className="text-lg font-semibold text-gray-900 mb-4">
        Ticket Types Breakdown
      </h4>
      {/* ...existing ticket breakdown code... */}
    </div>
  </div>
)}
```

---

## 🔄 Integration Requirements

### **Stripe API Integration:**
- Convert existing `Scripts/Analyze-EventTicketTypes.ps1` logic to C# API endpoint
- Use same Stripe credentials from `appsettings.Production.json`
- Match events by `stripe.metadata.eventTitle` with `Events.Title`
- Handle Stripe API rate limits and errors gracefully
- **Implementation Note**: PowerShell script already functional - translate HTTP calls and data processing

### **Database Integration:**
- ✅ OrganizerTicketPayments table already exists and populated
- ✅ OrganizerTicketPaymentService already provides CRUD operations
- Extend existing `organizerSalesService.ts` with new API endpoints
- Add indexes for performance on large event datasets (if needed)
- Implement proper authorization (organizer can only see their events)

### **UI/UX Requirements:**
- Integrate with existing `OrganizerSalesDashboardEnhanced.tsx` styling
- Maintain current mobile-responsive design patterns
- Loading states for API calls (especially Stripe API integration)
- Error handling with user-friendly messages
- Extend existing auto-refresh capability to include revenue tabs

---

## 📅 Implementation Priority

### **Phase 1** (Prerequisites): ✅ **COMPLETED**
- ✅ Implement Organizer ticket payment management system (`OrganizerTicketPaymentService.cs`)
- ✅ Add payment status tracking to database schema (`OrganizerTicketPayments` table)
- ✅ Create organizer ticket management UI (basic dashboard exists)
- ✅ Data migration scripts available (`Scripts/migrate-organizer-tickets.sql`)

### **Phase 2** (Core Revenue Analytics): 🔲 **IN PROGRESS**
- 🔲 Create new API endpoints for 4 revenue analysis tabs
- 🔲 Convert PowerShell Stripe integration to C# API endpoints
- 🔲 Build frontend revenue analysis sub-tabs within existing dashboard
- 🔲 Implement responsive design consistent with current UI

### **Phase 3** (Integration & Polish): 🔲 **PLANNED**
- 🔲 Add error handling and loading states
- 🔲 Performance optimization for large datasets
- 🔲 Cross-browser testing
- 🔲 User acceptance testing with organizers

---

## 🔍 Current Implementation Status

### ✅ **Completed Infrastructure**
- **Database**: `OrganizerTicketPayments` table with complete schema
- **Backend Service**: `OrganizerTicketPaymentService.cs` with full CRUD operations
- **Interface**: `IOrganizerTicketPaymentService.cs` defining contracts
- **Dashboard**: `OrganizerSalesDashboardEnhanced.tsx` with basic analytics
- **Service Layer**: `organizerSalesService.ts` for frontend API integration
- **Stripe Script**: `Scripts/Analyze-EventTicketTypes.ps1` (ready for C# conversion)

### 🔲 **Missing Components (This Enhancement)**
- Revenue analysis sub-tabs within existing dashboard
- 4 new API endpoints for detailed revenue breakdown  
- Stripe API integration in C# backend
- Advanced revenue summary and reconciliation panels

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
- [ ] Organizers can view complete revenue breakdown across both platforms within existing dashboard
- [ ] Real-time synchronization between database and Stripe data via new API endpoints
- [ ] Clear visibility into payment status for organizer-issued tickets (leveraging existing OrganizerTicketPayments)
- [ ] Financial reconciliation between projected and actual revenue across platforms
- [ ] Mobile-friendly interface maintaining current dashboard design consistency
- [ ] Performance under load (events with 1000+ transactions) using existing optimization patterns

---

## 🏗️ Technical Architecture Notes

### **File Structure Impact**
```
EventBooking.API/
├── Controllers/
│   ├── EventsController.cs          // Add new revenue endpoints here
│   └── OrganizersController.cs      // Or extend organizer endpoints
├── Services/
│   ├── OrganizerTicketPaymentService.cs  // ✅ Already exists
│   ├── StripeRevenueService.cs           // 🔲 New service needed
│   └── RevenueAnalyticsService.cs        // 🔲 New service for summaries
├── DTOs/
│   └── Revenue/                     // 🔲 New folder for revenue DTOs
└── Scripts/
    └── Analyze-EventTicketTypes.ps1 // ✅ Reference for C# conversion

event-booking-frontend/
├── src/pages/
│   └── OrganizerSalesDashboardEnhanced.tsx  // 🔲 Extend existing file
├── src/services/
│   └── organizerSalesService.ts             // 🔲 Add revenue API calls
└── src/components/revenue/                  // 🔲 New revenue components
    ├── TicketsSummaryTab.tsx
    ├── KiwiLankaRevenueTab.tsx
    ├── OrganizerRevenueTab.tsx
    └── RevenueSummaryTab.tsx
```

*This document reflects current implementation state as of September 28, 2025 and should be updated as requirements evolve during implementation.*
