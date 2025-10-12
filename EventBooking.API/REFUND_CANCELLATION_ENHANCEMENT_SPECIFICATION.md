# 📋 **Event Booking System - Organizer Sales Management Enhancement**

## 🎯 **Project Overview**

### **Objective**
Implement simple sales management functionality for organizers to manage their issued tickets efficiently without complex refund workflows.

### **Business Requirements**
1. ~~**Admin-Only Online Payment Refunds**: Allow administrators to refund and cancel tickets purchased through Stripe online payments (whole booking refunds only)~~ **DEFERRED to Admin Dashboard**
2. **Organizer Payment Management**: Enable organizers to mark their issued tickets as paid/unpaid and update customer information (individual ticket level)
3. ~~**Organizer Refund/Cancellation**: Allow organizers to refund and cancel tickets they have issued directly (individual ticket level with partial refund capability)~~ **SIMPLIFIED to Cancel Status Only**

### **Simplified Organizer Features**
- **Mark as Paid/Unpaid**: Simple toggle for payment status
- **Update Customer Details**: Edit first name, last name, email
- **Cancel Ticket**: Change status to "Cancelled" (no refund processing)
- **Simple Table Interface**: Basic CRUD operations, no complex analytics

### **Key Architectural Distinctions**
- ~~**Online Stripe Payments**: Stored in `Bookings` + `BookingLineItems` → **Whole booking refunds only**~~ **DEFERRED - Admin handles via Stripe**
- **Organizer-Issued Tickets**: Stored in `OrganizerTicketPayments` → **Simple status management (Paid/Unpaid/Cancelled)**

### **Core Constraints**
- **Simple Operations**: Only basic status updates and customer info edits
- **Dashboard Exclusion**: Cancelled tickets excluded from counting and revenue calculations  
- **Role-Based Security**: Organizer can only access their own tickets
- **No Complex Refunding**: No refund processing, audit trails, or financial transactions
- **UI Integration**: New "Sales Management" tab with simple table interface

---

## 🗄️ **Database Schema Changes** *(Simplified - No Refund Tracking)*

### **1. Enhanced Status Values**

#### **OrganizerTicketPayments Table** *(SIMPLIFIED)*
- **Current Status Values**: `"Active"` (default)
- **New Status Values**: `"Cancelled"` (simple cancellation, no refund processing)
- **Payment Status**: Use existing `IsPaidToOrganizer` boolean field
- **Customer Info**: Use existing `CustomerFirstName`, `CustomerLastName`, `CustomerEmail` fields

### **2. No Additional Audit Columns Needed**
- **No RefundedAt/RefundedBy columns** - keeping it simple
- **Use existing UpdatedAt** for basic change tracking
- **No complex audit trail** - just simple status changes

### **3. Minimal Performance Indexes** *(Only what's needed)*
```sql
-- For efficient status filtering in counting queries
CREATE INDEX IX_OrganizerTicketPayments_Status_EventId ON OrganizerTicketPayments(Status, EventId);
```

---

## 🎨 **UI Integration Specification**

### **New Sales Management Tab**

#### **Location & Navigation**
- **Tab Position**: Next to existing "Booking Details" tab in organizer interface
- **Tab Name**: "Sales Management" 
- **Access Control**: Only visible to users with "Organizer" role
- **Event Context**: Tab shows data for the currently selected event

#### **Tab Content - Simple Table Interface**

##### **Ticket Management Table** *(Simplified)*
```
┌─────────────────────────────────────────────────────────────────────────────────────────────────┐
│ 🎫 SALES MANAGEMENT                                                                               │
├─────────────────────────────────────────────────────────────────────────────────────────────────┤
│ [🔍 Search by name/email]    [📋 Filter: All ▼]    [💾 Save Changes]                           │
├─────────────────────────────────────────────────────────────────────────────────────────────────┤
│ ID │ First Name    │ Last Name     │ Email              │ Price │ Paid │ Status   │ Actions    │
├─────────────────────────────────────────────────────────────────────────────────────────────────┤
│ 1  │ [John      ]  │ [Doe       ]  │ [john@example.com] │ $50   │ ☑Yes │ Active   │ [Cancel]   │
│ 2  │ [Jane      ]  │ [Smith     ]  │ [jane@example.com] │ $50   │ ☐No  │ Active   │ [Cancel]   │
│ 3  │ [Bob       ]  │ [Wilson    ]  │ [bob@example.com ] │ $50   │ ☑Yes │Cancelled │ [Restore]  │
└─────────────────────────────────────────────────────────────────────────────────────────────────┘
```

#### **Simple User Actions**

1. **Edit Customer Details**: 
   - Inline editing of First Name, Last Name, Email
   - Click field to edit, press Enter to save

2. **Toggle Payment Status**:
   - Click checkbox to mark as Paid/Unpaid
   - Immediate save on toggle

3. **Cancel/Restore Ticket**:
   - Cancel button changes status to "Cancelled"
   - Restore button changes status back to "Active"
   - Simple confirmation dialog

---

## 🔧 **Entity Framework Migration**

### **Migration File: `AddOrganizerSalesManagement.cs`** *(Minimal Changes)*
```csharp
public partial class AddOrganizerSalesManagement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // No new columns needed - using existing fields:
        // - Status (existing) - will support "Cancelled" value
        // - IsPaidToOrganizer (existing) - for payment status
        // - CustomerFirstName, CustomerLastName, CustomerEmail (existing)
        // - UpdatedAt (existing) - for basic change tracking

        // Create minimal performance index for status filtering
        migrationBuilder.CreateIndex(
            name: "IX_OrganizerTicketPayments_Status_EventId",
            table: "OrganizerTicketPayments",
            columns: new[] { "Status", "EventId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Drop index
        migrationBuilder.DropIndex(name: "IX_OrganizerTicketPayments_Status_EventId", table: "OrganizerTicketPayments");
    }
}
```

---

## 🏗️ **Model Updates** *(Minimal Changes - Use Existing Fields)*

### **OrganizerTicketPayment Model** *(No New Properties Needed)*
```csharp
public class OrganizerTicketPayment
{
    // ...existing properties...
    
    // EXISTING FIELDS TO USE:
    // - Status (string) - will support "Active", "Cancelled"
    // - IsPaidToOrganizer (bool) - for payment status toggle
    // - CustomerFirstName (string) - editable
    // - CustomerLastName (string) - editable  
    // - CustomerEmail (string) - editable
    // - UpdatedAt (DateTime) - for basic change tracking
    
    /// <summary>
    /// Check if this ticket is cancelled (simple status check)
    /// </summary>
    public bool IsCancelled => Status == "Cancelled";
    
    /// <summary>
    /// Full customer name for display
    /// </summary>
    public string CustomerFullName => $"{CustomerFirstName} {CustomerLastName}".Trim();
}
```

---

## 🎯 **Business Logic Implementation** *(Simplified)*

### **Simple Organizer Sales Management Service**

#### **Service: `OrganizerSalesManagementService`** *(Simple CRUD Operations)*
```csharp
public interface IOrganizerSalesManagementService
{
    // Get tickets for sales management table
    Task<List<OrganizerTicketSalesDTO>> GetTicketsForSalesManagementAsync(int eventId);
    
    // Simple updates
    Task<bool> UpdateCustomerDetailsAsync(int paymentId, UpdateCustomerDetailsRequest request);
    Task<bool> TogglePaymentStatusAsync(int paymentId, bool isPaid);
    Task<bool> CancelTicketAsync(int paymentId);
    Task<bool> RestoreTicketAsync(int paymentId);
}

public class OrganizerSalesManagementService : IOrganizerSalesManagementService
{
    private readonly AppDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    
    public async Task<List<OrganizerTicketSalesDTO>> GetTicketsForSalesManagementAsync(int eventId)
    {
        var currentUserId = GetCurrentUserId();
        
        var tickets = await _context.OrganizerTicketPayments
            .Where(p => p.EventId == eventId && p.Event.Organizer.UserId == currentUserId)
            .Select(p => new OrganizerTicketSalesDTO
            {
                Id = p.Id,
                CustomerFirstName = p.CustomerFirstName,
                CustomerLastName = p.CustomerLastName,
                CustomerEmail = p.CustomerEmail,
                TicketPrice = p.TicketPrice,
                IsPaid = p.IsPaidToOrganizer,
                Status = p.Status,
                UpdatedAt = p.UpdatedAt
            })
            .OrderBy(p => p.CustomerLastName)
            .ThenBy(p => p.CustomerFirstName)
            .ToListAsync();
            
        return tickets;
    }
    
    public async Task<bool> UpdateCustomerDetailsAsync(int paymentId, UpdateCustomerDetailsRequest request)
    {
        var currentUserId = GetCurrentUserId();
        var payment = await GetOrganizerTicketPaymentAsync(paymentId, currentUserId);
        
        payment.CustomerFirstName = request.CustomerFirstName?.Trim();
        payment.CustomerLastName = request.CustomerLastName?.Trim();
        payment.CustomerEmail = request.CustomerEmail?.Trim();
        payment.UpdatedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();
        return true;
    }
    
    public async Task<bool> TogglePaymentStatusAsync(int paymentId, bool isPaid)
    {
        var currentUserId = GetCurrentUserId();
        var payment = await GetOrganizerTicketPaymentAsync(paymentId, currentUserId);
        
        payment.IsPaidToOrganizer = isPaid;
        payment.PaidDate = isPaid ? DateTime.UtcNow : null;
        payment.UpdatedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();
        return true;
    }
    
    public async Task<bool> CancelTicketAsync(int paymentId)
    {
        var currentUserId = GetCurrentUserId();
        var payment = await GetOrganizerTicketPaymentAsync(paymentId, currentUserId);
        
        payment.Status = "Cancelled";
        payment.UpdatedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();
        return true;
    }
    
    public async Task<bool> RestoreTicketAsync(int paymentId)
    {
        var currentUserId = GetCurrentUserId();
        var payment = await GetOrganizerTicketPaymentAsync(paymentId, currentUserId);
        
        payment.Status = "Active";
        payment.UpdatedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();
        return true;
    }
    
    private async Task<OrganizerTicketPayment> GetOrganizerTicketPaymentAsync(int paymentId, string userId)
    {
        var payment = await _context.OrganizerTicketPayments
            .Include(p => p.Event.Organizer)
            .FirstOrDefaultAsync(p => p.Id == paymentId);
            
        if (payment == null || payment.Event.Organizer.UserId != userId)
            throw new UnauthorizedAccessException("Ticket not found or access denied");
            
        return payment;
    }
    
    private string GetCurrentUserId()
    {
        return _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value 
               ?? throw new UnauthorizedAccessException("User not authenticated");
    }
}
```

#### **Simple API Endpoints**
```csharp
[HttpGet("organizer/events/{eventId}/sales-management")]
[Authorize(Roles = "Organizer")]
public async Task<ActionResult<List<OrganizerTicketSalesDTO>>> GetTicketsForSalesManagement(int eventId)

[HttpPut("organizer/payments/{paymentId}/customer-details")]
[Authorize(Roles = "Organizer")]
public async Task<ActionResult<bool>> UpdateCustomerDetails(int paymentId, UpdateCustomerDetailsRequest request)

[HttpPut("organizer/payments/{paymentId}/toggle-payment")]
[Authorize(Roles = "Organizer")]
public async Task<ActionResult<bool>> TogglePaymentStatus(int paymentId, TogglePaymentRequest request)

[HttpPut("organizer/payments/{paymentId}/cancel")]
[Authorize(Roles = "Organizer")]
public async Task<ActionResult<bool>> CancelTicket(int paymentId)

[HttpPut("organizer/payments/{paymentId}/restore")]
[Authorize(Roles = "Organizer")]
public async Task<ActionResult<bool>> RestoreTicket(int paymentId)
```

---

## 📊 **Counting Logic Updates** *(Simplified)*

### **Simple Organizer Ticket Counting**
```csharp
// Count active organizer tickets (exclude cancelled)
public async Task<int> GetOrganizerTicketsSoldAsync(int eventId)
{
    var organizerTickets = await _context.OrganizerTicketPayments
        .Where(otp => otp.Status == "Active" && // Exclude cancelled tickets
                      otp.EventId == eventId)
        .CountAsync();
    
    return organizerTickets;
}

// Count paid organizer tickets
public async Task<int> GetOrganizerTicketsPaidAsync(int eventId)
{
    var paidTickets = await _context.OrganizerTicketPayments
        .Where(otp => otp.Status == "Active" && 
                      otp.EventId == eventId &&
                      otp.IsPaidToOrganizer == true)
        .CountAsync();
    
    return paidTickets;
}

// Get organizer revenue (only active and paid tickets)
public async Task<decimal> GetOrganizerRevenueAsync(int eventId)
{
    return await _context.OrganizerTicketPayments
        .Where(otp => otp.Status == "Active" &&
                      otp.EventId == eventId &&
                      otp.IsPaidToOrganizer == true)
        .SumAsync(otp => otp.TicketPrice);
}
```

---

## 🔐 **Security & Authorization**

### **Role-Based Access Control**

#### **Admin Permissions**
- ✅ Refund any online payment booking (whole booking only)
- ✅ View all refund audit trails across all events
- ✅ Access system-wide refund reports and statistics
- ✅ View partial refund summaries for organizer tickets (read-only)
- ❌ Cannot refund individual organizer-issued tickets
- ❌ Cannot mark organizer tickets as paid

#### **Organizer Permissions**
- ✅ Mark their own tickets as paid (individual ticket level)
- ✅ Update customer details for their tickets (individual ticket level)
- ✅ Refund/cancel their own issued tickets (individual ticket level)
- ✅ View partial refund summaries for their bookings
- ✅ Bulk operations on their own tickets
- ✅ View refund history for their events only
- ❌ Cannot refund online payment bookings
- ❌ Cannot access other organizers' ticket data
- ❌ Cannot refund tickets from other organizers

### **Data Validation Rules**
```csharp
// Admin refund validation
public async Task<ValidationResult> ValidateAdminRefundAsync(int bookingId, string adminUserId)
{
    var booking = await _context.Bookings
        .Include(b => b.BookingLineItems)
        .FirstOrDefaultAsync(b => b.Id == bookingId);
    
    if (booking == null)
        return ValidationResult.Error("Booking not found");
        
    if (booking.PaymentStatus != "Completed")
        return ValidationResult.Error("Only completed payments can be refunded");
        
    if (booking.Status != "Active")
        return ValidationResult.Error("Booking is not in active status");
        
    if (booking.BookingLineItems.Any(li => li.Status != "Active"))
        return ValidationResult.Error("Some line items are already refunded");
    
    return ValidationResult.Success();
}

// Organizer refund validation
public async Task<ValidationResult> ValidateOrganizerRefundAsync(int paymentId, string organizerUserId)
{
    var payment = await _context.OrganizerTicketPayments
        .Include(p => p.Event.Organizer)
        .FirstOrDefaultAsync(p => p.Id == paymentId);
    
    if (payment == null)
        return ValidationResult.Error("Ticket payment not found");
        
    if (payment.Event.Organizer.UserId != organizerUserId)
        return ValidationResult.Error("Access denied - not your ticket");
        
    if (payment.Status != "Active")
        return ValidationResult.Error("Ticket is not in active status");
    
    return ValidationResult.Success();
}
```

---

## 📱 **API Endpoints Specification** *(Simplified)*

### **Organizer Sales Management Endpoints**

#### **1. Get Tickets for Sales Management**
```http
GET /api/organizer/events/{eventId}/sales-management
Authorization: Bearer {token} (Organizer role required)

Response:
{
  "tickets": [
    {
      "id": 456,
      "customerFirstName": "John",
      "customerLastName": "Doe",
      "customerEmail": "john@example.com",
      "ticketPrice": 75.00,
      "isPaid": true,
      "status": "Active",
      "updatedAt": "2025-10-10T14:30:00Z"
    }
  ]
}
```

#### **2. Update Customer Details**
```http
PUT /api/organizer/payments/{paymentId}/customer-details
Authorization: Bearer {token} (Organizer role required)

Request:
{
  "customerFirstName": "John",
  "customerLastName": "Doe",
  "customerEmail": "john.doe@example.com"
}

Response:
{
  "success": true,
  "message": "Customer details updated successfully"
}
```

#### **3. Toggle Payment Status**
```http
PUT /api/organizer/payments/{paymentId}/toggle-payment
Authorization: Bearer {token} (Organizer role required)

Request:
{
  "isPaid": true
}

Response:
{
  "success": true,
  "message": "Payment status updated successfully"
}
```

#### **4. Cancel Ticket**
```http
PUT /api/organizer/payments/{paymentId}/cancel
Authorization: Bearer {token} (Organizer role required)

Response:
{
  "success": true,
  "message": "Ticket cancelled successfully"
}
```

#### **5. Restore Ticket**
```http
PUT /api/organizer/payments/{paymentId}/restore
Authorization: Bearer {token} (Organizer role required)

Response:
{
  "success": true,
  "message": "Ticket restored successfully"
}
```

---

## 🧪 **Testing Requirements**

### **Unit Tests**

#### **Database Migration Tests**
- ✅ Verify new refund tracking columns are created correctly
- ✅ Verify performance indexes are created for all tables
- ✅ Verify migration rollback works without data loss
- ✅ Test data integrity during migration process
- ✅ Verify foreign key relationships are maintained

#### **Service Layer Tests**

##### **Admin Refund Service Tests**
- ✅ Whole booking refund validation logic
- ✅ Automatic user ID population from JWT token
- ✅ Status validation (only Active bookings can be refunded)
- ✅ Payment status validation (only Completed payments)
- ✅ Transaction rollback on partial failures
- ✅ Seat status restoration to Available

##### **Organizer Refund Service Tests**
- ✅ Individual ticket refund validation
- ✅ Partial refund scenario testing
- ✅ Bulk refund operations
- ✅ Organizer ownership validation
- ✅ Cross-organizer access prevention
- ✅ Status filtering in partial refund summaries

##### **Counting Logic Tests**
- ✅ Stripe ticket counting excludes refunded BookingLineItems
- ✅ Organizer ticket counting excludes refunded OrganizerTicketPayments
- ✅ Revenue calculations exclude refunded amounts
- ✅ Partial refund impact on totals
- ✅ Mixed refund scenarios (some tickets refunded, others active)

#### **API Endpoint Tests**

##### **Authentication & Authorization Tests**
- ✅ Admin endpoints reject non-admin users
- ✅ Organizer endpoints reject non-organizer users
- ✅ Cross-organizer access prevention
- ✅ JWT token validation and user ID extraction
- ✅ Role-based access control enforcement

##### **Request/Response Validation Tests**
- ✅ Required field validation
- ✅ Data type validation
- ✅ Email format validation
- ✅ Price range validation
- ✅ Error response format consistency

##### **Business Logic Integration Tests**
- ✅ Admin refund flow end-to-end
- ✅ Organizer payment marking flow
- ✅ Individual ticket refund flow
- ✅ Bulk operations accuracy
- ✅ Partial refund calculations

### **Integration Tests**

#### **End-to-End Workflow Tests**

##### **Admin Refund Workflow**
- ✅ Admin logs in with admin role
- ✅ Admin views refundable bookings for event
- ✅ Admin refunds entire booking
- ✅ All BookingLineItems marked as refunded
- ✅ Seats become available again
- ✅ Dashboard immediately excludes refunded tickets from counts
- ✅ Audit trail correctly records admin action

##### **Organizer Payment Management Workflow**
- ✅ Organizer logs in and views their events
- ✅ Organizer sees unpaid tickets for their event
- ✅ Organizer updates customer details
- ✅ Organizer marks individual tickets as paid
- ✅ Dashboard immediately reflects payment status changes
- ✅ Bulk payment operations work correctly

##### **Organizer Partial Refund Workflow**
- ✅ Organizer views tickets for a specific booking
- ✅ Organizer selects individual tickets to refund
- ✅ Partial refund is processed successfully
- ✅ Remaining tickets stay active and usable
- ✅ Dashboard shows correct partial refund statistics
- ✅ Revenue calculations reflect partial refunds

##### **Mixed Scenario Tests**
- ✅ Event with both Stripe and Organizer tickets
- ✅ Some tickets refunded, others active
- ✅ Dashboard shows accurate combined statistics
- ✅ Counting logic correctly handles mixed states
- ✅ User can create new booking after refunds

#### **Performance Tests**
- ✅ Refund operations complete within acceptable time limits
- ✅ Database queries use indexes efficiently
- ✅ Bulk operations don't cause timeouts
- ✅ Dashboard loading time not impacted by refund data

#### **Data Consistency Tests**
- ✅ Concurrent refund operations don't cause conflicts
- ✅ Partial refund calculations remain accurate
- ✅ Audit trail data integrity maintained
- ✅ Related data updates are atomic

---

## 📈 **Performance Considerations**

### **Database Query Optimization**

#### **Indexing Strategy**
```sql
-- Primary filtering indexes
CREATE INDEX IX_Bookings_Status_EventId ON Bookings(Status, EventId);
CREATE INDEX IX_BookingLineItems_Status_ItemType ON BookingLineItems(Status, ItemType);
CREATE INDEX IX_OrganizerTicketPayments_Status_EventId ON OrganizerTicketPayments(Status, EventId);

-- Partial refund tracking
CREATE INDEX IX_OrganizerTicketPayments_BookingLineItemId_Status 
ON OrganizerTicketPayments(BookingLineItemId, Status);

-- Audit trail queries
CREATE INDEX IX_Bookings_RefundedAt ON Bookings(RefundedAt) WHERE RefundedAt IS NOT NULL;
CREATE INDEX IX_OrganizerTicketPayments_RefundedAt 
ON OrganizerTicketPayments(RefundedAt) WHERE RefundedAt IS NOT NULL;

-- Organizer ownership queries
CREATE INDEX IX_OrganizerTicketPayments_EventId_Status_OrganizerUserId 
ON OrganizerTicketPayments(EventId, Status) 
INCLUDE (RefundedAt, RefundedBy);
```

#### **Query Optimization Patterns**
```csharp
// Efficient status filtering with indexes
var activeTickets = await _context.OrganizerTicketPayments
    .Where(otp => otp.Status == "Active" && otp.EventId == eventId)  // Uses IX_OrganizerTicketPayments_Status_EventId
    .CountAsync();

// Efficient partial refund summary
var refundSummary = await _context.OrganizerTicketPayments
    .Where(otp => otp.BookingLineItemId == bookingLineItemId)  // Uses IX_OrganizerTicketPayments_BookingLineItemId_Status
    .GroupBy(otp => otp.Status)
    .Select(g => new { Status = g.Key, Count = g.Count(), Revenue = g.Sum(x => x.TicketPrice) })
    .ToListAsync();

// Avoid N+1 queries in bulk operations
var tickets = await _context.OrganizerTicketPayments
    .Include(otp => otp.Event.Organizer)  // Single query with joins
    .Where(otp => paymentIds.Contains(otp.Id))
    .ToListAsync();
```

### **Caching Strategy**

#### **Cache Invalidation on Refunds**
```csharp
public class CachedEventStatisticsService
{
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(5);
    
    public async Task<EventStatistics> GetEventStatisticsAsync(int eventId)
    {
        var cacheKey = $"event_stats_{eventId}";
        
        if (_cache.TryGetValue(cacheKey, out EventStatistics cachedStats))
            return cachedStats;
            
        var stats = await CalculateEventStatisticsAsync(eventId);
        _cache.Set(cacheKey, stats, _cacheExpiry);
        return stats;
    }
    
    public void InvalidateEventCache(int eventId)
    {
        var cacheKey = $"event_stats_{eventId}";
        _cache.Remove(cacheKey);
    }
}

// Invalidate cache after refunds
public async Task RefundTicketAsync(int paymentId)
{
    var payment = await RefundSingleTicketAsync(paymentId);
    _cacheService.InvalidateEventCache(payment.EventId);  // Clear stale cache
}
```

#### **Dashboard Performance Optimization**
```csharp
// Pre-calculate common statistics
public async Task<DashboardSummary> GetOptimizedDashboardAsync(int eventId)
{
    // Single query for all counts
    var ticketCounts = await _context.Database.SqlQueryRaw<TicketCountResult>(@"
        SELECT 
            SUM(CASE WHEN Source = 'Stripe' AND Status = 'Active' THEN TicketCount ELSE 0 END) as ActiveStripeTickets,
            SUM(CASE WHEN Source = 'Organizer' AND Status = 'Active' THEN 1 ELSE 0 END) as ActiveOrganizerTickets,
            SUM(CASE WHEN Source = 'Organizer' AND Status = 'Refunded' THEN 1 ELSE 0 END) as RefundedOrganizerTickets
        FROM (
            SELECT 'Stripe' as Source, bli.Status, bli.Quantity as TicketCount
            FROM BookingLineItems bli 
            INNER JOIN Bookings b ON bli.BookingId = b.Id
            WHERE b.EventId = {0} AND bli.ItemType = 'Ticket'
            
            UNION ALL
            
            SELECT 'Organizer' as Source, otp.Status, 1 as TicketCount
            FROM OrganizerTicketPayments otp
            WHERE otp.EventId = {0}
        ) combined", eventId).FirstOrDefaultAsync();
    
    return new DashboardSummary
    {
        ActiveTickets = ticketCounts.ActiveStripeTickets + ticketCounts.ActiveOrganizerTickets,
        PartialRefundInfo = new PartialRefundInfo
        {
            RefundedTickets = ticketCounts.RefundedOrganizerTickets,
            // ... other calculations
        }
    };
}
```

### **Bulk Operation Optimization**
```csharp
// Efficient bulk refund processing
public async Task<BulkRefundResult> RefundMultipleTicketsAsync(List<int> paymentIds)
{
    // Single query to fetch all tickets
    var tickets = await _context.OrganizerTicketPayments
        .Where(p => paymentIds.Contains(p.Id))
        .ToListAsync();
    
    // Batch update all tickets
    var currentTime = DateTime.UtcNow;
    var currentUserId = GetCurrentUserId();
    
    foreach (var ticket in tickets)
    {
        ticket.Status = "Refunded";
        ticket.RefundedAt = currentTime;
        ticket.RefundedBy = currentUserId;
    }
    
    // Single SaveChanges call for all updates
    await _context.SaveChangesAsync();
    
    return new BulkRefundResult
    {
        Success = true,
        RefundedCount = tickets.Count,
        TotalRefundedAmount = tickets.Sum(t => t.TicketPrice)
    };
}
```

---

## 🚀 **Deployment Strategy**

### **Phase 1: Database Foundation (Week 1)**
1. **Database Migration Deployment**
   - Apply migration to add refund tracking columns
   - Create performance indexes
   - Verify existing data integrity
   - Test migration rollback procedures

2. **Model Updates**
   - Deploy updated entity models
   - Update Entity Framework configurations
   - Test model changes in staging environment

3. **Validation & Testing**
   - Run performance tests on existing queries
   - Verify index usage with query plans
   - Test data access patterns with new columns

### **Phase 2: Service Layer Implementation (Week 2)**
1. **Core Services Deployment**
   - Deploy AdminBookingRefundService
   - Deploy enhanced OrganizerTicketPaymentService
   - Deploy OrganizerRefundService with partial refund support
   - Update TicketAvailabilityService counting logic

2. **Feature Flags**
   - Deploy services with feature flags disabled
   - Enable gradual rollout per user role
   - Test functionality in staging environment

3. **Performance Monitoring**
   - Monitor database query performance
   - Track service response times
   - Validate cache invalidation logic

### **Phase 3: API Endpoints (Week 3)**
1. **Admin Endpoints**
   - Deploy admin refund endpoints
   - Test role-based authorization
   - Validate audit trail functionality

2. **Organizer Endpoints**
   - Deploy organizer payment management endpoints
   - Deploy individual ticket refund endpoints
   - Test partial refund calculations

3. **Integration Testing**
   - End-to-end workflow validation
   - Cross-role access testing
   - Bulk operation validation

### **Phase 4: Frontend Integration (Week 4)**
1. **Dashboard Updates**
   - Update dashboard components to show refund status
   - Add partial refund indicators
   - Implement real-time count updates

2. **Admin Interface**
   - Add admin refund management interface
   - Implement audit trail viewing
   - Add bulk refund capabilities

3. **Organizer Interface**
   - Add organizer payment management interface
   - Implement individual ticket refund controls
   - Add partial refund summary views

### **Phase 5: Production Release (Week 5)**
1. **Full Feature Enablement**
   - Enable all refund/cancellation features
   - Remove feature flags
   - Monitor system performance

2. **User Training & Documentation**
   - Provide admin training on refund procedures
   - Create organizer guides for payment management
   - Document troubleshooting procedures

3. **Monitoring & Optimization**
   - Monitor system performance impact
   - Collect user feedback
   - Optimize based on usage patterns

---

## 📋 **Acceptance Criteria** *(Simplified)*

### **Core Organizer Sales Management Features**

#### **Simple Table Management**
- ✅ Organizer can view all their issued tickets in a simple table format
- ✅ Table shows: Customer Name, Email, Price, Payment Status, Ticket Status
- ✅ Search functionality by customer name or email
- ✅ Filter by status (All, Active, Cancelled)
- ✅ Only organizer's own tickets are accessible (cross-organizer prevention)

#### **Customer Information Updates**
- ✅ Organizer can edit customer first name, last name, and email inline
- ✅ Changes are saved immediately with basic validation
- ✅ Email format validation
- ✅ Required field validation for names

#### **Payment Status Management**
- ✅ Organizer can toggle payment status (Paid/Unpaid) with checkbox
- ✅ Payment status changes immediately reflect in the table
- ✅ Payment status changes immediately reflect in dashboard statistics
- ✅ Simple boolean toggle - no complex payment methods or notes

#### **Ticket Cancellation**
- ✅ Organizer can cancel individual tickets with simple "Cancel" button
- ✅ Cancelled tickets can be restored with "Restore" button
- ✅ Confirmation dialog before cancellation
- ✅ Cancelled tickets immediately excluded from all dashboard counts
- ✅ No complex refund processing - just status change

### **System-Wide Requirements**
- ✅ **Counting Accuracy**: Cancelled tickets excluded from dashboard counts
  - Only "Active" status tickets counted in totals
  - Only "Active" and "Paid" tickets counted in revenue
  
- ✅ **Performance**: Simple operations with minimal database impact
  - Basic status filtering with single index
  - No complex audit trails or relationships
  
- ✅ **Security**: Role-based access control enforced
  - JWT token validation and user identification
  - Cross-organizer access prevention
  
- ✅ **Data Integrity**: Basic change tracking
  - UpdatedAt timestamp for simple audit
  - No complex audit trails or user tracking
  
- ✅ **User Experience**: Simple and intuitive interface
  - Inline editing where possible
  - Immediate feedback on changes
  - Clear status indicators

### **What's NOT Included (Deferred to Admin Dashboard)**
- ❌ **No Refund Processing**: No financial refund transactions
- ❌ **No Audit Trails**: No RefundedAt/RefundedBy tracking
- ❌ **No Bulk Operations**: No multi-select actions
- ❌ **No Analytics**: No summaries, charts, or complex reporting
- ❌ **No Payment Methods**: No payment method tracking or notes
- ❌ **No Admin Features**: All admin refund functionality deferred

---

## 🔍 **Future Enhancements (Deferred to Admin Dashboard)**

### **Admin Refund Management (Future Phase)**
1. **Online Payment Refunds**
   - Integration with Stripe refund API for automatic payment refunds
   - Whole booking refund processing with audit trails
   - Admin-only access to refund online payments
   - Complete financial transaction handling

2. **Advanced Organizer Features**
   - Bulk operations (select multiple tickets for actions)
   - Payment method tracking and notes
   - Advanced audit trails with RefundedAt/RefundedBy tracking
   - Partial refund analytics and summaries

3. **Enhanced Reporting & Analytics**
   - Revenue impact analysis for refunds
   - Sales performance dashboards
   - Customer behavior analysis
   - Automated reconciliation reports

4. **Communication Enhancements**
   - Automated email notifications for status changes
   - SMS notifications for ticket updates
   - Customer communication templates
   - Integration with customer support systems

---

**Document Version**: 3.0 (Simplified Organizer Sales Management)
**Last Updated**: October 12, 2025
**Prepared By**: Development Team
**Focus**: Simple sales management for organizers - no complex refunding
**Key Simplifications**: 
- No RefundedAt/RefundedBy audit columns
- No bulk operations
- No complex analytics or summaries
- Simple cancel/restore functionality instead of refunds

---

## 📊 **Quick Reference Summary** *(Simplified)*

| Feature | What Organizer Can Do | What's Deferred |
|---------|----------------------|-----------------|
| **Customer Info** | ✅ Edit first name, last name, email | ❌ Mobile, address, other fields |
| **Payment Status** | ✅ Toggle paid/unpaid (checkbox) | ❌ Payment methods, notes, dates |
| **Ticket Status** | ✅ Cancel/restore tickets | ❌ Complex refund processing |
| **Interface** | ✅ Simple table with inline editing | ❌ Bulk actions, analytics, summaries |
| **Audit** | ✅ Basic UpdatedAt timestamp | ❌ Complex audit trails, user tracking |

**Core Principle**: Keep it simple - just basic CRUD operations for ticket management without complex financial or audit features.
