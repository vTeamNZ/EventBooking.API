# 🧪 BookingLineItems Architecture Testing Script

## Test Overview
This script tests the new unified BookingLineItems architecture with real Stripe payments.

## Prerequisites
- EventBooking.API running on localhost:5000
- kwdb02 database with clean architecture
- Stripe live keys configured
- Test event and ticket types available

## Test Scenarios

### 1. General Admission Event Test
- Event with general admission (no specific seats)
- Multiple ticket types
- Include food items
- Test QR generation and email delivery

### 2. Allocated Seating Event Test  
- Event with specific seat selection
- Test seat reservation and booking
- QR generation per seat
- Email with multiple tickets

### 3. Mixed Items Test
- Combination of tickets and food
- Test unified BookingLineItems creation
- Validate all metadata storage

## Test Data Requirements

### Event Setup
- Need at least one event with SeatSelectionMode.GeneralAdmission
- Need at least one event with SeatSelectionMode.EventHall
- Events should have associated TicketTypes
- Events should have FoodItems (optional)

### Customer Data
- Email: test@kiwilanka.co.nz
- Name: Test Customer
- Mobile: +64-21-123-4567

## Expected Outcomes

### Database Verification
1. Booking record created in Bookings table
2. BookingLineItems records created (tickets + food)
3. QR codes populated in BookingLineItems
4. Seat status updated for allocated seating
5. No BookingTickets or BookingFoods records (old tables)

### File System Verification  
1. QR ticket PDFs generated in storage path
2. QR codes scannable and valid

### Email Verification
1. Customer email with ticket attachments
2. Organizer notification email
3. Email content includes booking details

## Test Commands

### Step 1: Start API
```powershell
cd "c:\Users\gayantd\source\repos\vTeamNZ\EventBooking.API\EventBooking.API"
dotnet run
```

### Step 2: Verify Database Connection
```sql
-- Check if using kwdb02
SELECT DB_NAME() as CurrentDatabase;

-- Verify tables exist
SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_SCHEMA = 'dbo' 
  AND TABLE_NAME IN ('Bookings', 'BookingLineItems', 'Events', 'TicketTypes');

-- Check for test data
SELECT COUNT(*) as EventCount FROM Events;
SELECT COUNT(*) as TicketTypeCount FROM TicketTypes;
```

### Step 3: Test API Endpoints
```bash
# Health check
curl -X GET "http://localhost:5000/health"

# Get events
curl -X GET "http://localhost:5000/api/events"

# Get ticket types for an event
curl -X GET "http://localhost:5000/api/events/{eventId}/ticket-types"
```

### Step 4: Create Test Payment
Use the Stripe payment flow to create a booking with:
- Event ID
- Ticket details
- Food details (if available)
- Customer information

### Step 5: Verify Results
```sql
-- Check booking creation
SELECT TOP 5 * FROM Bookings ORDER BY CreatedAt DESC;

-- Check booking line items
SELECT b.Id as BookingId, 
       bli.ItemType, 
       bli.ItemName, 
       bli.Quantity, 
       bli.UnitPrice,
       bli.TotalPrice,
       CASE WHEN bli.QRCode IS NOT NULL AND bli.QRCode != '' THEN 'Has QR' ELSE 'No QR' END as QRStatus
FROM Bookings b
INNER JOIN BookingLineItems bli ON b.Id = bli.BookingId
ORDER BY b.CreatedAt DESC, bli.Id;

-- Check seat updates (for allocated seating)
SELECT EventId, SeatNumber, Status, ReservedBy 
FROM Seats 
WHERE Status = 2 -- Booked
ORDER BY EventId, SeatNumber;
```

## 🎯 Success Criteria

### ✅ Database Success
- [✓] Booking record created with proper customer and payment details
- [✓] BookingLineItems created for each ticket and food item
- [✓] ItemType correctly set ('Ticket', 'Food')
- [✓] JSON metadata properly stored in SeatDetails and ItemDetails
- [✓] QR codes populated for ticket items
- [✓] Seat status updated for allocated seating events

### ✅ Integration Success
- [✓] QR ticket PDFs generated and accessible
- [✓] Emails sent to customer and organizer
- [✓] No errors in application logs
- [✓] Stripe payment properly processed

### ✅ Architecture Success
- [✓] No records created in old BookingTickets table
- [✓] No records created in old BookingFoods table
- [✓] All booking data in unified BookingLineItems structure
- [✓] Clean separation between booking master and detail records

## 🚨 Common Issues & Solutions

### Issue: Connection String
**Problem**: Still connecting to kwdb01
**Solution**: Update appsettings.Development.json to use kwdb02

### Issue: Missing Test Data
**Problem**: No events or ticket types in kwdb02
**Solution**: Run seed scripts or create test events manually

### Issue: Stripe Configuration
**Problem**: Invalid Stripe keys or webhook setup
**Solution**: Verify Stripe keys in appsettings and webhook endpoints

### Issue: QR Generation Failure
**Problem**: QR service not generating tickets
**Solution**: Check storage path permissions and QR service configuration

## 📋 Test Execution Checklist

- [ ] API starts without errors
- [ ] Database connection to kwdb02 confirmed
- [ ] Test events available
- [ ] Stripe payment flow accessible
- [ ] Payment successfully processed
- [ ] Booking record created
- [ ] BookingLineItems populated
- [ ] QR codes generated
- [ ] Emails sent
- [ ] Files created in storage
- [ ] No old table records created

## 🎉 Expected Final State

After successful testing:
1. **Clean Architecture**: All booking data in BookingLineItems
2. **Functional QR**: Scannable tickets generated
3. **Email Integration**: Notifications working
4. **Seat Management**: Proper status updates
5. **Future Ready**: Architecture supports tickets, food, merchandise

This validates that the new BookingLineItems architecture is production-ready!
