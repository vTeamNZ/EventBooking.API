# EventBooking.API - Public Endpoints Documentation

**API Base URL**: `https://kiwilanka.co.nz/api/`  
**Last Updated**: October 20, 2025  
**API Version**: 1.0

---

## 🔓 Public Endpoints (No Authentication Required)

These endpoints are marked with `[AllowAnonymous]` and can be accessed without authentication.

---

### 1. 🎫 QR Code Validation

**Purpose**: Validate scanned QR codes for event entry

#### POST /tickets/validate-qr

**URL**: `https://kiwilanka.co.nz/api/tickets/validate-qr`

**Request Body**:
```json
{
  "qrData": "EventID: 17\nEvent: My Event\nSeat: A-15\nName: John Doe\nPayment ID: pi_xxx",
  "scanLocation": "Mobile App",
  "scanNotes": "Optional notes"
}
```

**Response**: See `MOBILE_APP_API_GUIDE.md` for complete details

**Features**:
- ✅ Validates ticket authenticity
- ✅ Returns ticket, event, and entry information
- ✅ Tracks entry count (re-entry detection)
- ✅ Logs all validation attempts to database
- ✅ Includes food order details

---

### 2. 🎭 Events Endpoints

#### GET /events

**URL**: `https://kiwilanka.co.nz/api/events`

**Purpose**: Get list of all active events

**Response**:
```json
[
  {
    "id": 17,
    "title": "My Event",
    "description": "Event description",
    "date": "2025-10-25T18:00:00Z",
    "location": "Main Venue",
    "imageUrl": "/uploads/event-17.jpg",
    "status": 2,
    "organizerId": 5,
    "venueId": 3,
    "venue": { /* venue details */ },
    "organizer": { /* organizer details */ },
    "ticketTypes": [ /* ticket types */ ]
  }
]
```

**Features**:
- ✅ Only returns ACTIVE events (status = 2)
- ✅ Includes venue, organizer, and ticket types
- ✅ Sorted by date (upcoming first, then past)
- ✅ Public access for event discovery

---

#### GET /events/{id}

**URL**: `https://kiwilanka.co.nz/api/events/17`

**Purpose**: Get details of a specific event by ID

**Response**: Single event object with venue, organizer, and ticket types

---

#### GET /events/by-title/{slug}

**URL**: `https://kiwilanka.co.nz/api/events/by-title/my-event-title`

**Purpose**: Get event by URL-friendly slug (generated from title)

**Example**: `"My Event 2025!"` → `/events/by-title/my-event-2025`

**Response**: Single event object

**Slug Generation Rules**:
- Lowercase
- Spaces → hyphens
- Remove: `' " & , . ! ? ( ) [ ] { } : ; / \ | * + = @ # $ % ^ \` ~ < >`
- `&` → `and`
- Remove consecutive hyphens

---

#### PUT /events/{id}

**URL**: `https://kiwilanka.co.nz/api/events/17`

**Purpose**: Update event details

⚠️ **Note**: Currently marked `[AllowAnonymous]` but should probably be protected

---

### 3. 🪑 Seats Endpoints

#### GET /seats/event/{eventId}/layout

**URL**: `https://kiwilanka.co.nz/api/seats/event/17/layout`

**Purpose**: Get seat layout and availability for an event

**Response**:
```json
{
  "eventId": 17,
  "mode": "EventHall",
  "venue": {
    "id": 3,
    "name": "Main Hall",
    "width": 20,
    "height": 15
  },
  "hasHorizontalAisles": true,
  "horizontalAisleRows": "5,10",
  "hasVerticalAisles": true,
  "verticalAisleSeats": "J,K",
  "aisleWidth": 2,
  "ticketTypes": [ /* ticket types with colors */ ],
  "seats": [
    {
      "id": 123,
      "seatNumber": "A-15",
      "rowNumber": "A",
      "seatIndex": 15,
      "status": "Available",
      "price": 50.00,
      "ticketTypeId": 5,
      "ticketType": { /* ticket type details */ }
    }
  ]
}
```

**Features**:
- ✅ Clears expired reservations automatically
- ✅ Returns real-time seat availability
- ✅ Includes ticket types for color coding
- ✅ Aisle information for visual layout
- ✅ Required for public seat selection

**Seat Statuses**:
- `Available` - Can be selected
- `Reserved` - Temporarily held (expires after timeout)
- `Sold` - Already purchased
- `Blocked` - Not available for sale

---

#### POST /seats/reserve-multiple

**URL**: `https://kiwilanka.co.nz/api/seats/reserve-multiple`

**Purpose**: Temporarily reserve seats during checkout

**Request Body**:
```json
{
  "eventId": 17,
  "seatIds": [123, 124, 125],
  "reservationId": "unique-guid-here"
}
```

**Response**:
```json
{
  "success": true,
  "message": "3 seats reserved for 5 minutes",
  "reservedSeats": [ /* seat details */ ],
  "expiresAt": "2025-10-20T10:35:45Z"
}
```

**Features**:
- ✅ Reserves seats for 5 minutes
- ✅ Prevents double booking
- ✅ Automatically expires after timeout
- ✅ Used during payment flow

---

### 4. 🍔 Food Items Endpoints

#### GET /fooditems/event/{eventId}

**URL**: `https://kiwilanka.co.nz/api/fooditems/event/17`

**Purpose**: Get available food items for an event

**Response**:
```json
[
  {
    "id": 45,
    "eventId": 17,
    "name": "Combo Meal",
    "description": "Burger + Fries + Drink",
    "price": 15.00,
    "category": "Meals",
    "isAvailable": true,
    "imageUrl": "/uploads/food-45.jpg"
  }
]
```

**Features**:
- ✅ Public menu viewing
- ✅ Used for food selection during booking
- ✅ Prices and availability info

---

### 5. 👤 Organizers Endpoints

#### GET /organizers

**URL**: `https://kiwilanka.co.nz/api/organizers`

**Purpose**: Get list of all event organizers

**Response**:
```json
[
  {
    "id": 5,
    "name": "Event Company",
    "contactEmail": "contact@eventcompany.com",
    "phoneNumber": "+64 21 123 4567",
    "organizationName": "Event Company Ltd",
    "website": "https://eventcompany.com",
    "facebookUrl": "https://facebook.com/eventcompany",
    "youtubeUrl": "https://youtube.com/@eventcompany",
    "isVerified": true,
    "createdAt": "2025-01-15T10:00:00Z",
    "userName": "organizer@email.com",
    "fullName": "John Smith"
  }
]
```

**Features**:
- ✅ Public organizer directory
- ✅ Contact information
- ✅ Social media links
- ✅ Verification status

---

#### GET /organizers/{id}

**URL**: `https://kiwilanka.co.nz/api/organizers/5`

**Purpose**: Get details of a specific organizer

**Response**: Single organizer object

---

### 6. 💳 Payment Endpoints

#### GET /payment/config

**URL**: `https://kiwilanka.co.nz/api/payment/config`

**Purpose**: Get Stripe publishable key for frontend

**Response**:
```json
{
  "publishableKey": "pk_live_xxxxxxxxxxxxx"
}
```

**Usage**: Required to initialize Stripe on frontend

---

#### POST /payment/create-payment-intent

**URL**: `https://kiwilanka.co.nz/api/payment/create-payment-intent`

**Purpose**: Create Stripe payment intent for booking

**Request Body**:
```json
{
  "eventId": 17,
  "selectedSeats": [
    {
      "seatId": 123,
      "ticketTypeId": 5,
      "price": 50.00
    }
  ],
  "selectedFoodItems": [
    {
      "foodItemId": 45,
      "quantity": 2,
      "price": 15.00
    }
  ],
  "customerName": "John Doe",
  "customerEmail": "john@example.com",
  "customerPhone": "+64 21 123 4567",
  "reservationId": "unique-guid",
  "afterPay": false
}
```

**Response**:
```json
{
  "clientSecret": "pi_xxx_secret_xxx",
  "paymentIntentId": "pi_xxx"
}
```

**Features**:
- ✅ Creates Stripe payment intent
- ✅ Calculates total including processing fees
- ✅ Supports Afterpay integration
- ✅ Required for payment processing

---

#### POST /payment/webhook

**URL**: `https://kiwilanka.co.nz/api/payment/webhook`

**Purpose**: Handle Stripe webhook events (payment confirmations)

**Authentication**: Stripe signature verification

**Events Handled**:
- `checkout.session.completed` - Payment successful
- `payment_intent.succeeded` - Payment processed
- `payment_intent.payment_failed` - Payment failed

**Features**:
- ✅ Processes successful payments
- ✅ Confirms bookings
- ✅ Generates QR codes
- ✅ Sends confirmation emails
- ✅ Updates seat status to "Sold"

---

#### GET /payment/success

**URL**: `https://kiwilanka.co.nz/api/payment/success?session_id=xxx`

**Purpose**: Payment success callback page

**Query Parameters**:
- `session_id` - Stripe checkout session ID

**Response**: HTML page with success message

---

#### GET /payment/cancel

**URL**: `https://kiwilanka.co.nz/api/payment/cancel?session_id=xxx`

**Purpose**: Payment cancellation callback page

**Response**: HTML page with cancellation message

---

#### POST /payment/create-checkout-session

**URL**: `https://kiwilanka.co.nz/api/payment/create-checkout-session`

**Purpose**: Create Stripe checkout session (alternative payment flow)

**Request Body**: Similar to create-payment-intent

**Response**:
```json
{
  "sessionId": "cs_xxx",
  "url": "https://checkout.stripe.com/xxx"
}
```

---

#### POST /payment/create-afterpay-session

**URL**: `https://kiwilanka.co.nz/api/payment/create-afterpay-session`

**Purpose**: Create Afterpay/Clearpay payment session

**Request Body**: Similar to create-payment-intent

**Response**:
```json
{
  "sessionId": "cs_xxx",
  "url": "https://checkout.stripe.com/xxx"
}
```

---

#### POST /payment/refund

**URL**: `https://kiwilanka.co.nz/api/payment/refund`

**Purpose**: Process refund for a booking

**Request Body**:
```json
{
  "bookingId": 123,
  "reason": "Event cancelled"
}
```

**Response**:
```json
{
  "success": true,
  "refundId": "re_xxx",
  "amount": 100.00
}
```

⚠️ **Note**: Currently public, should probably be protected

---

#### GET /payment/booking/{bookingId}

**URL**: `https://kiwilanka.co.nz/api/payment/booking/123`

**Purpose**: Get booking details by ID

⚠️ **Note**: Currently public, contains sensitive data

---

#### GET /payment/debug-session/{sessionId}

**URL**: `https://kiwilanka.co.nz/api/payment/debug-session/cs_xxx`

**Purpose**: Debug Stripe session details

**Response**: Session metadata and payment status

---

### 7. 🔐 Authentication Endpoints

#### POST /auth/register

**URL**: `https://kiwilanka.co.nz/api/auth/register`

**Purpose**: Register new user account

**Request Body**:
```json
{
  "fullName": "John Doe",
  "email": "john@example.com",
  "password": "SecurePass123!",
  "role": "Customer"
}
```

**Roles**: `Customer`, `Organizer`

**Response**:
```json
{
  "message": "User registered successfully",
  "userId": "user-guid-here"
}
```

---

#### POST /auth/login

**URL**: `https://kiwilanka.co.nz/api/auth/login`

**Purpose**: Login and get JWT token

**Request Body**:
```json
{
  "email": "john@example.com",
  "password": "SecurePass123!"
}
```

**Response**:
```json
{
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "expiration": "2025-10-21T10:30:45Z",
  "user": {
    "id": "user-guid",
    "email": "john@example.com",
    "fullName": "John Doe",
    "role": "Customer"
  }
}
```

---

#### Other Auth Endpoints

All other endpoints in `AuthController` are also public:
- `POST /auth/create-admin` - Create admin (requires existing admin auth)
- `POST /auth/forgot-password` - Password reset request
- `POST /auth/reset-password` - Reset password with token
- `POST /auth/change-password` - Change password when logged in
- `GET /auth/verify-email` - Email verification
- etc.

---

### 8. 🔧 Diagnostics Endpoints

#### GET /api/diagnostics/controllers

**URL**: `https://kiwilanka.co.nz/api/diagnostics/controllers`

**Purpose**: List all controllers and routes in the API

**Response**:
```json
{
  "assemblyName": "EventBooking.API...",
  "controllerCount": 20,
  "controllers": [
    {
      "name": "EventsController",
      "namespace": "EventBooking.API.Controllers",
      "routes": ["[controller]"]
    }
  ]
}
```

**Usage**: Development/debugging tool

---

### 9. 📊 Admin Endpoints (Public but should be protected)

#### GET /admin/payment-page-config

**URL**: `https://kiwilanka.co.nz/api/admin/payment-page-config`

**Purpose**: Get payment page configuration

⚠️ **Note**: Marked `[AllowAnonymous]` for payment page access

---

## 🔒 Protected Endpoints (Authentication Required)

These require JWT token in `Authorization: Bearer {token}` header:

### Admin Only (`[Authorize(Roles = "Admin")]`)
- POST /auth/create-admin
- POST /organizers
- Most admin controller endpoints

### Admin + Organizer (`[Authorize(Roles = "Admin,Organizer")]`)
- POST /events
- PUT /events/{id}/submit-for-review
- DELETE /events/{id}
- POST /fooditems
- PUT /fooditems/{id}
- DELETE /fooditems/{id}
- PUT /organizers/{id}
- Most event management endpoints

### User-Specific
- GET /bookings - User's bookings
- POST /reservations - Create reservation
- etc.

---

## ⚠️ Security Considerations

### Endpoints That Should Be Protected:
1. ❌ `PUT /events/{id}` - Currently public, should require auth
2. ❌ `POST /payment/refund` - Currently public, should be admin-only
3. ❌ `GET /payment/booking/{bookingId}` - Currently public, contains sensitive data

### Recommendations:
1. ✅ Add `[Authorize]` to payment refund endpoint
2. ✅ Protect booking details endpoint
3. ✅ Add rate limiting to public endpoints
4. ✅ Add CORS restrictions
5. ✅ Review all `[AllowAnonymous]` attributes

---

## 📝 Summary

### Total Public Endpoints: ~20+

**By Category**:
- 🎫 QR Validation: 1
- 🎭 Events: 4
- 🪑 Seats: 2
- 🍔 Food: 1
- 👤 Organizers: 2
- 💳 Payment: 10+
- 🔐 Auth: 8+
- 🔧 Diagnostics: 1

**Most Important for Mobile/Web Apps**:
1. ✅ POST /tickets/validate-qr (QR scanner)
2. ✅ GET /events (event listing)
3. ✅ GET /events/{id} (event details)
4. ✅ GET /seats/event/{eventId}/layout (seat map)
5. ✅ GET /fooditems/event/{eventId} (food menu)
6. ✅ POST /payment/create-payment-intent (checkout)
7. ✅ POST /payment/webhook (payment confirmation)

---

**Last Updated**: October 20, 2025  
**API Status**: Production  
**Base URL**: https://kiwilanka.co.nz/api/
