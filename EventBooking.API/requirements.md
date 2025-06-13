# Event Booking Platform – KiwiLanka

## Overview

This is a web-based platform for listing and booking events in New Zealand. The application will allow public users to view upcoming events, select tickets and food options, and make payments online via Stripe.

## Functional Requirements

### 1. Home Page – Event Listing
- Display all events stored in the database.
- Each event card includes:
  - Title
  - Date and Time
  - Location
  - Short description
  - Button to view and book

### 2. Event Details & Ticket Selection
- Clicking an event opens a ticket selection page.
- Tickets are pulled dynamically from the database per event.
- Each event can have multiple ticket types (e.g., Adult, Child, Group).
- User selects quantity per ticket type.
- “Next” button proceeds to food selection.

### 3. Food Selection
- Event-specific food items are retrieved from the database.
- Each food item includes name, description, price, and quantity input.
- User can skip this step.

### 4. Payment Page
- Displays a summary of:
  - Selected tickets
  - Selected food items
  - Total price
- Initiates payment using Stripe API.
- On success:
  - Booking details (tickets and food) are saved to the database.
  - Confirmation email is sent (if email support is enabled later).

### 5. Data Storage
- Event, TicketType, FoodItem, and Booking data are persisted in a relational database (e.g., SQL Server).
- Admin data entry can be done manually via SQL queries.

## Technical Stack

- **Frontend**: React.js with React Router
- **Backend**: ASP.NET Core Web API
- **Database**: SQL Server Express
- **Payment**: Stripe API
- **Hosting**: Azure Windows VM (IIS + SQL Express)

## Entities (Tables)

### Event
- Id
- Title
- Description
- DateTime
- Location

### TicketType
- Id
- EventId (FK)
- Name
- Price

### FoodItem
- Id
- EventId (FK)
- Name
- Description
- Price

### Booking
- Id
- EventId (FK)
- CreatedAt
- TotalAmount

### BookingTicket
- Id
- BookingId (FK)
- TicketTypeId (FK)
- Quantity

### BookingFood
- Id
- BookingId (FK)
- FoodItemId (FK)
- Quantity

## Notes
- No admin panel is needed in this phase.
- All event, ticket, and food data will be manually inserted into the database.
- Phase 2 will include seat selection and table reservations.
