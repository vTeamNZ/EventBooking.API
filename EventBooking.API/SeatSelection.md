## Feature Requirement: Seat Selection System

### Overview
Implement a flexible seat selection system that supports three types of event configurations:

---

### 1. Event Hall Seat Booking

- Display the hall layout visually, including clear indication of the stage location.
- Users should be able to select individual seats based on the layout.
- Different sections (e.g., VIP, General, Balcony) should support different pricing.
- Selected seats should be highlighted and reserved in real-time to prevent double booking.

---

### 2. Table Seating with Seat Booking

- Display a layout of tables with seat counts and positions.
- Clearly show the stage (if applicable).
- Allow seat or entire table selection depending on event settings.
- Pricing should vary based on table or section (e.g., front tables vs back tables).
- Optionally, support group booking (e.g., entire table of 8).

---

### 3. General Admission (No Seat Selection)

- No seat selection UI is required.
- Simply display ticket types (e.g., Adult, Child, Family) and pricing.
- User can select ticket quantities and proceed to checkout.

---

### Additional Notes

- The system should be configurable to switch between these three modes per event.
- The seat/table layouts can be configurable via admin panel.
- Prevent selection of already booked or unavailable seats/tables.
- All selected options should be stored and passed through to checkout and payment.

