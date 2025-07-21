-- Fix Bookings table by adding missing customer fields
-- Based on the expected schema and application requirements

PRINT 'Adding missing columns to Bookings table...'

-- Add CustomerEmail
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Bookings' AND COLUMN_NAME = 'CustomerEmail')
ALTER TABLE Bookings ADD CustomerEmail nvarchar(max) NOT NULL DEFAULT '';

-- Add CustomerFirstName  
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Bookings' AND COLUMN_NAME = 'CustomerFirstName')
ALTER TABLE Bookings ADD CustomerFirstName nvarchar(max) NOT NULL DEFAULT '';

-- Add CustomerLastName
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Bookings' AND COLUMN_NAME = 'CustomerLastName')
ALTER TABLE Bookings ADD CustomerLastName nvarchar(max) NOT NULL DEFAULT '';

-- Add CustomerMobile
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Bookings' AND COLUMN_NAME = 'CustomerMobile')
ALTER TABLE Bookings ADD CustomerMobile nvarchar(max) NOT NULL DEFAULT '';

-- Add PaymentStatus
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Bookings' AND COLUMN_NAME = 'PaymentStatus')
ALTER TABLE Bookings ADD PaymentStatus nvarchar(max) NOT NULL DEFAULT 'pending';

-- Add Metadata for additional booking information
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Bookings' AND COLUMN_NAME = 'Metadata')
ALTER TABLE Bookings ADD Metadata nvarchar(max) NOT NULL DEFAULT '{}';

PRINT 'Bookings table updated successfully!'

-- Verify the updated structure
PRINT 'Updated Bookings table structure:'
SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE 
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'Bookings' 
ORDER BY ORDINAL_POSITION;
