# QR Code Validation - Database Matching Analysis

**Last Updated**: October 20, 2025  
**Purpose**: Document how QR code fields are matched against database tables for validation

---

## 🎫 QR Code Format

### Current QR Code Structure
```
EventID: 17
Event: Sample Event Name
Seat: A-15
Name: John Doe
Payment ID: pi_3SI62L03DpFjygI00B3Y7uvn
```

OR Alternative Format:
```
EventID: 17, Event: Sample Event Name, Seat: A-15, Name: John, ID: pi_3SI62L03DpFjygI00B3Y7uvn
```

### QR Code Fields (5 fields)
| Field | Example | Description |
|-------|---------|-------------|
| **EventID** | `17` | Event identifier (integer) |
| **Event** | `Sample Event Name` | Event title (string) |
| **Seat** | `A-15` | Seat number or ticket identifier (string) |
| **Name** | `John` or `John Doe` | Attendee first name (string) |
| **Payment ID** (or **ID**) | `pi_3SI62L03DpFjygI00B3Y7uvn` | Stripe Payment Intent ID or Organizer booking ID (string) |

---

## 🗄️ Database Tables Involved

### 1. **Events Table**
**Purpose**: Validate event exists and is active

| QR Field | Maps To | Table Column | Join Condition |
|----------|---------|--------------|----------------|
| `EventID` | → | `Events.Id` | `WHERE Events.Id = EventID` |

**Query**:
```sql
SELECT Id, Title, Description, Date, Location, Status, 
       Organizer.Name, Organizer.ContactEmail, ImageUrl
FROM Events
LEFT JOIN Organizers ON Events.OrganizerId = Organizers.Id
WHERE Events.Id = [EventID from QR]
```

**Validation Checks**:
- ✅ Event exists (`Events.Id` found)
- ✅ Event is active (`Events.Status != 'Cancelled'`)
- ⚠️ Event date not in past (optional warning, not blocking)

---

### 2. **BookingLineItems Table** (Primary Lookup)
**Purpose**: Find the specific ticket record

| QR Field | Maps To | Table Column | Match Logic |
|----------|---------|--------------|-------------|
| `Payment ID` | → | `BookingLineItems.QRCode` | Direct match OR |
| `Payment ID` | → | `Booking.PaymentIntentId` | Via JOIN |
| `Seat` | → | `BookingLineItems.SeatDetails` | JSON contains check OR |
| `Seat` | → | `BookingLineItems.ItemName` | String contains check |

**Query**:
```sql
SELECT bli.*, b.CustomerFirstName, b.CustomerLastName, b.CustomerEmail, 
       b.PaymentIntentId, b.PaymentStatus, b.Status
FROM BookingLineItems bli
INNER JOIN Bookings b ON bli.BookingId = b.Id
WHERE (bli.QRCode = [PaymentID] OR b.PaymentIntentId = [PaymentID])
  AND (bli.SeatDetails LIKE '%[SeatNumber]%' OR bli.ItemName LIKE '%[SeatNumber]%')
```

**Key Fields Retrieved**:
- `BookingId` - Links to Booking record
- `LineItemId` - Unique line item identifier
- `CustomerName` - From Booking (FirstName + LastName)
- `CustomerEmail` - From Booking
- `SeatNumber` - From QR code (not directly stored in separate column)
- `TicketType` - `ItemName` field
- `Price` - `UnitPrice` field
- `PaymentStatus` - From Booking parent record
- `Status` - `Active`, `Cancelled`, `Refunded`

**⚠️ Current Issue**: 
- **Payment ID** is checked against both `QRCode` field AND `PaymentIntentId`
- **Seat Number** is checked inside JSON `SeatDetails` or `ItemName` (not a direct column)
- This is **loose matching** and may cause false positives if:
  - Multiple tickets share the same Payment Intent ID
  - Seat number appears in wrong line item's data

---

### 3. **Bookings Table** (Parent Record)
**Purpose**: Get booking-level information

| QR Field | Maps To | Table Column | Access Method |
|----------|---------|--------------|---------------|
| `Payment ID` | → | `Bookings.PaymentIntentId` | Via JOIN from BookingLineItems |
| N/A | → | `Bookings.CustomerFirstName` | Retrieved for customer name |
| N/A | → | `Bookings.CustomerLastName` | Retrieved for customer name |
| N/A | → | `Bookings.CustomerEmail` | Retrieved for contact |
| N/A | → | `Bookings.PaymentStatus` | Payment validation |
| N/A | → | `Bookings.Status` | Booking status (Active/Cancelled/Refunded) |

**Validation Checks**:
- ✅ Payment completed (`PaymentStatus = 'Completed'` or `'OrganizerDirect'`)
- ✅ Booking active (`Status = 'Active'`, not `'Cancelled'` or `'Refunded'`)

---

### 4. **QREntryLogs Table** (Entry Tracking)
**Purpose**: Track entry history and detect re-entries

| QR Field | Maps To | Table Column | Purpose |
|----------|---------|--------------|---------|
| **Entire QR String** | → | `QREntryLogs.QRData` | Unique identifier for this scan |
| `EventID` | → | `QREntryLogs.EventID` | Event tracking |
| `Payment ID` | → | `QREntryLogs.PaymentGUID` | Payment tracking |
| `Seat` | → | `QREntryLogs.SeatNumber` | Seat tracking |
| `Name` | → | `QREntryLogs.AttendeeeName` | Attendee tracking |

**Query**:
```sql
SELECT *
FROM QREntryLogs
WHERE QRData = [Full QR String]
  AND (ValidationResult = 'Valid' OR ValidationResult = 'ValidReEntry')
ORDER BY ScanTime ASC
```

**Entry Info Calculated**:
- `HasPreviousEntry` - COUNT > 0
- `FirstEntryTime` - MIN(ScanTime)
- `LastEntryTime` - MAX(ScanTime)
- `EntryCount` - COUNT(*)
- `LastScanLocation` - Last record's ScanLocation

**⚠️ Important**: 
- Uses **full QR string** as unique identifier
- Even 1 character difference = different ticket

---

### 5. **EventBookings Table** (Legacy Fallback)
**Purpose**: Support old booking system (before BookingLineItems)

| QR Field | Maps To | Table Column | Match Logic |
|----------|---------|--------------|-------------|
| `Payment ID` | → | `EventBookings.PaymentGUID` | Direct match |
| `Seat` | → | `EventBookings.SeatNo` | Direct match (exact) |

**Query** (Fallback only if BookingLineItems not found):
```sql
SELECT *
FROM EventBookings
WHERE PaymentGUID = [PaymentID]
  AND SeatNo = [SeatNumber]
```

**⚠️ Legacy System**: Only used if modern BookingLineItems lookup fails

---

## 🔍 Validation Flow

### Step-by-Step Process

```
1. Parse QR Code
   ↓
   Extract: EventID, Event, Seat, Name, Payment ID
   ↓
   Validate: All 5 fields present?
   ├─ No → Return "Invalid" ❌
   └─ Yes → Continue

2. Validate Event
   ↓
   Query: Events table WHERE Id = EventID
   ↓
   Check: Event exists?
   ├─ No → Return "EventNotFound" ❌
   └─ Yes → Continue
   ↓
   Check: Event date in future?
   ├─ No → Return "Expired" ⚠️ (Warning only)
   └─ Yes → Continue

3. Find Ticket in Database
   ↓
   Query: BookingLineItems + Bookings
   WHERE (QRCode = PaymentID OR PaymentIntentId = PaymentID)
     AND (SeatDetails CONTAINS Seat OR ItemName CONTAINS Seat)
   ↓
   Found?
   ├─ No → Try legacy EventBookings table
   │         ↓
   │         Found?
   │         ├─ No → Return "NotFound" ❌
   │         └─ Yes → Continue with legacy data
   └─ Yes → Continue

4. Validate Ticket Status
   ↓
   Check: Booking.Status = "Active"?
   ├─ No → Return "Inactive" / "Cancelled" / "Refunded" ❌
   └─ Yes → Continue
   ↓
   Check: Booking.PaymentStatus completed?
   ├─ No → Return "PaymentPending" ⚠️
   └─ Yes → Continue

5. Check Entry History
   ↓
   Query: QREntryLogs WHERE QRData = [Full QR String]
   ↓
   Previous entries exist?
   ├─ No → First entry
   │        ↓
   │        Log entry → Return "Valid" ✅
   └─ Yes → Re-entry
            ↓
            Log entry → Return "ValidReEntry" ⚠️
```

---

## ⚠️ Current Matching Issues

### Issue #1: Loose Payment ID Matching
**Problem**: 
```csharp
WHERE (bli.QRCode == qrData.PaymentGUID || bli.Booking.PaymentIntentId == qrData.PaymentGUID)
```

**Impact**:
- If Payment Intent ID = `pi_ABC123` covers multiple tickets (A-1, A-2, A-3)
- QR for seat A-1 could match line item for seat A-2
- Relies on secondary check: `SeatDetails CONTAINS seat OR ItemName CONTAINS seat`

**Risk**: Medium
- Organizer bookings use Payment ID format: `ORG_[GUID]`
- User bookings use Stripe format: `pi_[ID]`
- Both should be unique per booking, but not per ticket

---

### Issue #2: Seat Number Contains Check
**Problem**:
```csharp
WHERE bli.SeatDetails.Contains(qrData.SeatNumber) OR bli.ItemName.Contains(qrData.SeatNumber)
```

**Impact**:
- `SeatDetails` is a JSON field: `{"allocatedSeats": ["A-1", "A-2"], "ticketTypeId": 5}`
- `ItemName` is text: `"VIP Ticket"`
- Contains check can match unintended records

**Example False Positive**:
```
QR Seat: "A-1"
Line Item 1: SeatDetails = '{"allocatedSeats": ["A-1", "A-2"]}' ← MATCH ✅
Line Item 2: SeatDetails = '{"allocatedSeats": ["A-10", "A-11"]}' ← FALSE MATCH ⚠️ (contains "A-1")
```

**Risk**: Medium-High
- Could match wrong ticket if seat numbers overlap (e.g., A-1 and A-10)

---

### Issue #3: QR Fields NOT Directly Used

| QR Field | Used in Validation? | Notes |
|----------|---------------------|-------|
| `EventID` | ✅ **Yes** | Used to validate event exists |
| `Event` (Name) | ❌ **No** | Parsed but not validated against database |
| `Seat` | ⚠️ **Partially** | Used in CONTAINS check, not exact match |
| `Name` | ❌ **No** | Parsed but not validated against customer name |
| `Payment ID` | ✅ **Yes** | Used to find booking record |

**Implications**:
- ✅ EventID and Payment ID are the **primary matching keys**
- ⚠️ Seat number is **secondary filter** (loose matching)
- ❌ Event name and attendee name are **NOT validated**
- ❌ No check if QR's "Event" matches database Event.Title
- ❌ No check if QR's "Name" matches Booking.CustomerFirstName

**Risk**: Low-Medium
- Someone could modify QR to have wrong event name or attendee name
- System would still validate if EventID + PaymentID + Seat match

---

## 📊 Recommended Database Schema

### Option 1: Add Dedicated Columns to BookingLineItems

```sql
ALTER TABLE BookingLineItems
ADD COLUMN SeatNumber VARCHAR(50) NULL,
ADD COLUMN AttendeeName VARCHAR(200) NULL;

-- Index for fast QR lookups
CREATE INDEX IX_BookingLineItems_QR_Lookup 
ON BookingLineItems(QRCode, SeatNumber);
```

**Benefits**:
- Exact seat number matching (no JSON parsing)
- Can store attendee name per ticket
- Faster queries (indexed columns)

**Migration Strategy**:
```sql
UPDATE BookingLineItems
SET SeatNumber = JSON_VALUE(SeatDetails, '$.allocatedSeats[0]')
WHERE ItemType = 'Ticket';
```

---

### Option 2: Create Dedicated QRTickets Table

```sql
CREATE TABLE QRTickets (
    Id INT PRIMARY KEY IDENTITY,
    BookingLineItemId INT NOT NULL,
    BookingId INT NOT NULL,
    EventId INT NOT NULL,
    QRCode NVARCHAR(500) NOT NULL UNIQUE, -- Full QR string or hash
    PaymentGUID NVARCHAR(255) NOT NULL,
    SeatNumber NVARCHAR(50) NOT NULL,
    AttendeeName NVARCHAR(200) NOT NULL,
    TicketTypeId INT NULL,
    Status NVARCHAR(50) NOT NULL DEFAULT 'Active',
    CreatedAt DATETIME NOT NULL DEFAULT GETUTCDATE(),
    
    CONSTRAINT FK_QRTickets_BookingLineItems 
        FOREIGN KEY (BookingLineItemId) REFERENCES BookingLineItems(Id),
    CONSTRAINT FK_QRTickets_Bookings 
        FOREIGN KEY (BookingId) REFERENCES Bookings(Id),
    CONSTRAINT FK_QRTickets_Events 
        FOREIGN KEY (EventId) REFERENCES Events(Id)
);

-- Optimal index for QR validation
CREATE UNIQUE INDEX IX_QRTickets_Lookup 
ON QRTickets(PaymentGUID, EventId, SeatNumber);
```

**Benefits**:
- ✅ One row per physical ticket
- ✅ Exact matching on all QR fields
- ✅ Fast lookups with compound index
- ✅ Decouples QR from BookingLineItems (which can be consolidated)
- ✅ Easy to add ticket-specific fields (scanned_at, validated_by, etc.)

**Query** (with new table):
```sql
SELECT *
FROM QRTickets qt
INNER JOIN BookingLineItems bli ON qt.BookingLineItemId = bli.Id
INNER JOIN Bookings b ON qt.BookingId = b.Id
WHERE qt.PaymentGUID = @PaymentID
  AND qt.EventId = @EventID
  AND qt.SeatNumber = @SeatNumber
  AND qt.Status = 'Active';
```

---

## 🎯 Proposed Matching Logic (Strict)

### Recommended Validation Query

```csharp
// Parse QR components
var qr = ParseQRData(qrData);

// STEP 1: Validate event (unchanged)
var eventExists = await _context.Events
    .Where(e => e.Id == qr.EventID && e.Status != "Cancelled")
    .AnyAsync();

// STEP 2: Find exact ticket match (IMPROVED)
var ticket = await _context.BookingLineItems
    .Include(bli => bli.Booking)
    .Where(bli => 
        // PRIMARY KEY: Payment Intent ID from Booking
        bli.Booking.PaymentIntentId == qr.PaymentGUID &&
        
        // SECONDARY KEY: Event ID must match
        bli.Booking.EventId == qr.EventID &&
        
        // TERTIARY KEY: Exact seat number in SeatDetails JSON
        EF.Functions.JsonValue(bli.SeatDetails, "$.allocatedSeats") CONTAINS qr.SeatNumber
        // OR: bli.SeatNumber == qr.SeatNumber (if column added)
    )
    .FirstOrDefaultAsync();

// STEP 3: Validate customer name (OPTIONAL NEW CHECK)
if (ticket != null && !string.IsNullOrEmpty(qr.FirstName))
{
    var nameMatch = ticket.Booking.CustomerFirstName
        .Equals(qr.FirstName, StringComparison.OrdinalIgnoreCase);
    
    if (!nameMatch)
    {
        _logger.LogWarning("QR name mismatch: QR={QRName}, DB={DBName}", 
            qr.FirstName, ticket.Booking.CustomerFirstName);
        // Decide: Block or allow with warning?
    }
}

// STEP 4: Check entry history (unchanged)
var entryCount = await _context.QREntryLogs
    .Where(log => log.QRData == qrData && log.ValidationResult.StartsWith("Valid"))
    .CountAsync();
```

---

## 📝 Summary of Field Matching

### Fields ACTUALLY Used for Validation

| QR Field | Used? | Database Match | Match Type |
|----------|-------|----------------|------------|
| **EventID** | ✅ Yes | `Events.Id` | Exact (INT) |
| **Event Name** | ❌ No | Not validated | N/A |
| **Seat** | ⚠️ Partial | `SeatDetails` JSON or `ItemName` | Contains (Loose) |
| **Name** | ❌ No | Not validated | N/A |
| **Payment ID** | ✅ Yes | `Booking.PaymentIntentId` OR `BookingLineItems.QRCode` | Exact (String) |

### Matching Priority
1. **Payment ID** (Primary key - most important)
2. **EventID** (Secondary key - validates event context)
3. **Seat Number** (Tertiary key - identifies specific ticket)
4. **Name** (NOT USED - could add as 4th validation layer)
5. **Event Name** (NOT USED - redundant with EventID)

### Entry Tracking Key
- **Full QR String** (all 5 fields combined) → `QREntryLogs.QRData`
- This makes each unique QR scannable independently

---

## 🚨 Security Risks

### Risk 1: QR Code Cloning
**Scenario**: Someone copies a valid QR code and shares it
**Current Protection**: Re-entry detection logs all scans
**Mitigation**: Entry count warning system (already implemented)

### Risk 2: QR Code Tampering
**Scenario**: Someone modifies event name or attendee name in QR
**Current Protection**: ❌ None - these fields not validated
**Mitigation**: Add name validation, or use signed QR codes

### Risk 3: Seat Number Spoofing
**Scenario**: Change seat "A-1" to "A-10" in QR but keep same Payment ID
**Current Protection**: ⚠️ Contains check may fail if both exist
**Mitigation**: Use exact seat matching with dedicated column

---

## 🔧 Recommendations

### Immediate (No Schema Change)
1. ✅ Add EventID validation (already done)
2. ✅ Add entry logging (already done)
3. ⚠️ Improve seat matching logic:
   ```csharp
   // Use JSON exact match instead of Contains
   EF.Functions.JsonContains(bli.SeatDetails, $"{{\"allocatedSeats\":[\"{qr.SeatNumber}\"]}}")
   ```

### Short-Term (Minor Schema Change)
1. Add `SeatNumber` column to `BookingLineItems`
2. Add `AttendeeName` column to `BookingLineItems`
3. Populate from existing JSON data
4. Update validation to use exact column matching

### Long-Term (New Table)
1. Create `QRTickets` table (one row per ticket)
2. Generate during booking creation
3. Use for all QR validations
4. Enables ticket-specific features (transfer, resale, etc.)

---

**Next Steps**: Review current matching logic and decide on schema improvements based on business requirements and risk tolerance.
